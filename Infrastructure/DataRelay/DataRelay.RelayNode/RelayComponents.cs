using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using Microsoft.Ccr.Core;
using MySpace.Common.HelperObjects;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.Server.Common;
using MySpace.Logging;
using MySpace.Common;
using System.Text;

namespace MySpace.DataRelay
{
	/// <summary>
	/// This class encapsulates all access, loading, <see cref="RelayMessage"/> mapping 
	/// relating to instances of <see cref="IRelayComponent"/>.
	/// </summary>
	/// <remarks>
	///		<para>
	///		Responsible for: 1) Loading & Unloading Components. 2) Mapping components to 
	///		the data Type Id's they support. 3) Ensure ignored types are ignored. 
	///		4) Handing <see cref="RelayMessage"/>s to the correct components to be handled.
	///		</para>
	/// </remarks>
	internal class RelayComponents
	{
		private RelayNodeConfig relayNodeConfig;

		private readonly MsReaderWriterLock typeComponentInListsLock = new MsReaderWriterLock(System.Threading.LockRecursionPolicy.SupportsRecursion);
		private readonly MsReaderWriterLock typeComponentOutListsLock = new MsReaderWriterLock(System.Threading.LockRecursionPolicy.SupportsRecursion);

		private static readonly LogWrapper log = new LogWrapper();
		private readonly object _reloadShutdownLock = new object();
		private bool hasReplicationComponent = false;

		private readonly static ParameterlessDelegate _logConfigProblem;

		/// <summary>
		/// A map indexed by <see cref="MessageType"/> values. If the value is false,
		/// we ignore all messages of that type; otherwise, we process them.
		/// </summary>
		private bool[] doHandleMessageByType = new bool[(int)MessageType.NumTypes];

		/// <summary>
		/// Indexed by TypeId
		/// </summary>
		private List<IRelayComponent>[] inComponents;

		/// <summary>
		/// Indexed by TypeId
		/// </summary>
		private List<IRelayComponent>[] outComponents;

		/// <summary>
		/// Indexed by TypeId
		/// </summary>
		private List<IReplicationComponent>[] inReplicatorComponents;

		/// <summary>
		/// Indexed by TypeId
		/// </summary>
		private List<IReplicationComponent>[] outReplicatorComponents;

		/// <summary>
		/// The config we use.
		/// </summary>
		private RelayComponentCollection config = null;

		/// <summary>
		/// The loaded components keyed by component name.
		/// </summary>
		private readonly Dictionary<string, IRelayComponent> loadedComponents = new Dictionary<string, IRelayComponent>(20);

		/// <summary>
		/// The currently active components, when a new set is created <see cref="Interlocked.Exchange"/> is used
		/// and all other access copies a local variable on first access.
		/// </summary>
		private List<IRelayComponent> activeComponents = new List<IRelayComponent>(10);

		/// <summary>
		/// A value that provides all TTL logic for expiring those message that support delete
		/// expiration.
		/// </summary>
		private LifetimeTtl ttl = null;

		/// <summary>
		/// Initializes the <see cref="Components"/> with a config.
		/// </summary>
		/// <param name="config">The <see cref="RelayNodeConfig"/> to use.</param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="config"/> is <see langword="null"/>.</para>
		/// </exception>
		public RelayComponents(RelayNodeConfig config)
		{
			if (config == null) throw new ArgumentNullException("config");
			SetConfig(config);
		}

		static RelayComponents()
		{
			_logConfigProblem = Algorithm.FrequencyBoundMethod(() => log.Error("FreqBound: Save With Confirm message type received and no replication components configured. Need to have config with Replicator == true (for Forwarder)"), TimeSpan.FromSeconds(2));
		}

		public int MaxTypeId
		{
			get
			{
				return maxTypeId;
			}
		}
		private short maxTypeId;

		/// <summary>
		/// Sets a reference to the <see cref="RelayNodeConfig"/> and to some sub configs and values that are 
		/// frequently used.
		/// </summary>
		/// <param name="configuration">The config to use.</param>
		private void SetConfig(RelayNodeConfig configuration)
		{
			if (configuration != null)
			{
				this.relayNodeConfig = configuration;
				this.maxTypeId = configuration.TypeSettings.MaxTypeId;
				this.config = configuration.RelayComponents;
			}
		}

		private bool CheckForConfigurationProblems(RelayMessage message)
		{
			if (message.MessageType == MessageType.SaveWithConfirm && hasReplicationComponent == false)
			{
				_logConfigProblem();
				message.ResultOutcome = RelayOutcome.Error;
				message.ResultDetails = "Server misconfigured or message sent to wrong server, doesn't support confirmed messages.";
				message.Payload = null;
				message.QueryData = null;
				return true;
			}
			return false;
		}

		public IEnumerable<ITask> HandleOutMessage(RelayMessageAsyncResult async)
		{
			RelayMessage message = async.Message;
			short typeId = message.TypeId;
			IRelayComponent component = null;
			Port<Exception> port = new Port<Exception>();
			List<IRelayComponent> components = null;
			List<IReplicationComponent> replicationComponents = null;

			if (CheckForConfigurationProblems(message)) yield break;

			GetOutComponents(message.TypeId, out components, out replicationComponents);

			if (components != null)
			{
				for (int i = 0; i < components.Count; i++)
				{
					component = components[i];
					//imporant we declare this here because of closure
					IAsyncDataHandler asyncHandler = component as IAsyncDataHandler;
					if (asyncHandler != null)
					{
						try
						{
							asyncHandler.BeginHandleMessage(message, null, asyncResult =>
							{
								try
								{
									asyncHandler.EndHandleMessage(asyncResult);
									port.Post(null);
								}
								catch (Exception exc)
								{
									message.ResultOutcome = RelayOutcome.Error;
									message.ResultDetails = "server error " + exc.ToString();
									if (log.IsErrorEnabled)
										log.ErrorFormat("Exception in EndHandleMessage: {0}", exc);
									port.Post(exc);
								}
							});
						}
						catch (Exception exc)
						{
							if (log.IsErrorEnabled)
								log.ErrorFormat("Exception in async HandleOutMessage: {0}", exc);
							port.Post(exc);
						}
						Exception hadException = null;
						yield return Arbiter.Receive(false, port, exc => hadException = exc);
						if (hadException != null) yield break;
					}
					else
					{
						try
						{
							component.HandleMessage(message);
						}
						catch (Exception exc)
						{
							message.ResultOutcome = RelayOutcome.Error;
							message.ResultDetails = "server error " + exc.ToString();
							if (log.IsErrorEnabled)
								log.ErrorFormat("Exception in HandleOutMessage: {0}", exc);
						}
					}
					if (message.Payload != null)
					{
						break;
					}
				}

				//  Null out query data so we don't waste network
				//  bandwidth sending input info back to the client
				message.QueryData = null;
			}
			
			Replicate(message, replicationComponents);

			try
			{
				RelayMessage deleteMessage = this.ttl.ProcessExpiredTTLDelete(message);
				if (deleteMessage != null)
				{
					HandleInMessage(deleteMessage);
				}
			}
			catch (Exception exc)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception in HandleOutMessage: {0}", exc);
			}
		}

		public void HandleOutMessage(RelayMessage message)
		{
			List<IRelayComponent> components = null;
			List<IReplicationComponent> replicationComponents = null;
			if(CheckForConfigurationProblems(message)) return;

			GetOutComponents(message.TypeId, out components, out replicationComponents);

			if (components != null)
			{
				for (int i = 0; i < components.Count; i++)
				{
					try
					{
						components[i].HandleMessage(message);
					}
					catch (Exception exc)
					{
						message.ResultOutcome = RelayOutcome.Error;
						message.ResultDetails = "server error " + exc.ToString();
						throw;
					}

					if (message.Payload != null)
					{
						break;
					}
				}
			}

			Replicate(message, replicationComponents);

			//  Null out query data so we don't waste network
			//  bandwidth sending input info back to the client
			message.QueryData = null;

			RelayMessage deleteMessage = this.ttl.ProcessExpiredTTLDelete(message);
			if (deleteMessage != null)
			{
				HandleInMessage(deleteMessage);
			}
		}

		/// <summary>
		/// Call this method process an 'In' <see cref="RelayMessage"/> by providing it's
		/// corresponding <see cref="RelayMessageProcessingContext"/> via <see cref="RelayMessageWithContext"/>
		/// 
		/// The <see cref="RelayMessageProcessingContext"/> is used to augment normal processing.
		/// </summary>
		/// <param name="messageWithContext">This container class contians both the <see cref="RelayMessage"/> and
		/// the corresponding <see cref="RelayMessageProcessingContext"/></param>
		public void HandleInMessage(RelayMessageWithContext messageWithContext)
		{
			this.ttl.ApplyDefaultTTL(messageWithContext.RelayMessage);

			List<IRelayComponent> components = null;
			List<IReplicationComponent> replicationComponents = null;
			int typeId = messageWithContext.RelayMessage.TypeId;

			if (CheckForConfigurationProblems(messageWithContext.RelayMessage)) return;

			GetInComponents(typeId, out components, out replicationComponents);

			if (components != null)
			{
				for (int i = 0; i < components.Count; i++)
				{
					IRelayComponent component = components[i];
					bool passExclustionListFilter = true;

					foreach (Type excludedType in messageWithContext.ProcessingContext.ExclusionComponentList)
					{
						if (component.GetType() == excludedType ||
							  component.GetType().IsSubclassOf(excludedType))
						{
							passExclustionListFilter = false;
							break;
						}
					}

					if (passExclustionListFilter)
					{
						component.HandleMessage(messageWithContext.RelayMessage);
					}
				}
			}

			Replicate(messageWithContext.RelayMessage, replicationComponents);
		}

		/// <summary>
		/// Gets a <see cref="List{T}"/> of IN components by <paramref name="typeId"/>.
		/// </summary>
		/// <param name="typeId">The type id.</param>
		/// <param name="components">The components for the given <paramref name="typeId"/>, may be null.</param>
		/// <param name="replicationComponents">The replication components for the given <paramref name="typeId"/>, may be null. </param>
		/// <returns>The list of components. May be <see langword="null"/>.</returns>
		private void GetInComponents(int typeId, out List<IRelayComponent> components, out List<IReplicationComponent> replicationComponents)
		{
			List<IRelayComponent> comp = null;
			List<IReplicationComponent> repComp = null;

			typeComponentInListsLock.Read(() =>
			{
				if (typeId >= 0 && typeId < inComponents.Length && typeId < inReplicatorComponents.Length)
				{
					comp = inComponents[typeId];
					repComp = inReplicatorComponents[typeId];
				}
				else
				{
                    if (log.IsErrorEnabled)
                        log.ErrorFormat("UNSUPPORTED TYPE ID: {0}", typeId);
				}
			});
			components = comp;
			replicationComponents = repComp;
		}

		/// <summary>
		/// Gets a <see cref="List{T}"/> of OUT components by <paramref name="typeId"/>.
		/// </summary>
		/// <param name="typeId">The type id.</param>
		/// <param name="components">The components for the given <paramref name="typeId"/>. May be null.</param>
		/// <param name="replicationComponents">The components for the given <paramref name="typeId"/>. May be null.</param>
		/// <returns>The list of componenets. May be <see langword="null"/>.</returns>
		private void GetOutComponents(int typeId, out List<IRelayComponent> components, out List<IReplicationComponent> replicationComponents)
		{
			List<IRelayComponent> comp = null;
			List<IReplicationComponent> repComp = null;

			typeComponentOutListsLock.Read(() =>
			{
				if (typeId >= 0 && typeId < outComponents.Length && typeId < outReplicatorComponents.Length)
				{
					comp = outComponents[typeId];
					repComp = outReplicatorComponents[typeId];
				}
				else
				{
                    if (log.IsErrorEnabled)
                        log.ErrorFormat("UNSUPPORTED TYPE ID: {0}", typeId);
				}
			});

			components = comp;
			replicationComponents = repComp;
		}

		public void HandleOutMessage(RelayMessageWithContext messageWithContext)
		{
			List<IRelayComponent> components = null;
			List<IReplicationComponent> replicationComponents = null;
			
			if(CheckForConfigurationProblems(messageWithContext.RelayMessage)) return;

			GetOutComponents(messageWithContext.RelayMessage.TypeId, out components, out replicationComponents);

			if (components != null)
			{
				for (int i = 0; i < components.Count; i++)
				{
					IRelayComponent component = components[i];
					bool passExclustionListFilter = true;

					foreach (Type excludedType in messageWithContext.ProcessingContext.ExclusionComponentList)
					{
						if (component.GetType() == excludedType ||
							  component.GetType().IsSubclassOf(excludedType))
						{
							passExclustionListFilter = false;
							break;
						}
					}

					if (passExclustionListFilter)
					{
						try
						{
							component.HandleMessage(messageWithContext.RelayMessage);
						}
						catch (Exception exc)
						{
							messageWithContext.RelayMessage.ResultOutcome = RelayOutcome.Error;
							messageWithContext.RelayMessage.ResultDetails = "server error " + exc.ToString();
							throw;
						}
					}
				}
			}

			Replicate(messageWithContext.RelayMessage, replicationComponents);
		}

		public void HandleInMessage(RelayMessage message)
		{
			this.ttl.ApplyDefaultTTL(message);

			List<IRelayComponent> components = null;
			List<IReplicationComponent> replicationComponents = null;
			
			if(CheckForConfigurationProblems(message)) return;
			GetInComponents(message.TypeId, out components, out replicationComponents);

			if (components != null)
			{
				for (int i = 0; i < components.Count; i++)
				{
					components[i].HandleMessage(message);
				}
			}

			Replicate(message, replicationComponents);
		}

		public void HandleInMessages(IList<RelayMessage> messages)
		{
			if (messages == null) return;

			for (int i = 0; i < messages.Count; i++)
			{
				this.ttl.ApplyDefaultTTL(messages[i]);
				if(CheckForConfigurationProblems(messages[i])) return;
			}

			TypeIdMessageListPair[] typedMessages = TypeIdMessageListPair.SplitMessagesByType(this.maxTypeId, messages, this.doHandleMessageByType, this);
			List<IRelayComponent> components = null;
			List<IReplicationComponent> replicationComponent = null;

			for (int typeId = 0; typeId < typedMessages.Length; typeId++)
			{
				if (typedMessages[typeId] != null)
				{
					GetInComponents(typeId, out components, out replicationComponent);

					if (components != null)
					{
						for (int i = 0; i < components.Count; i++)
						{
							components[i].HandleMessages(typedMessages[typeId].GetMessages());
						}
					}

					Replicate(typedMessages[typeId].GetMessages(), replicationComponent);
				}
			}
		}

		public IEnumerable<ITask> HandleOutMessages(RelayMessageListAsyncResult asyncMessages)
		{
			IList<RelayMessage> messages = asyncMessages.Messages;

			if (messages == null) throw new ArgumentException("asyncMessages.Messages is null");

			for (int i = 0; i < messages.Count; i++)
			{
				if (CheckForConfigurationProblems(messages[i])) yield break;
			}

			TypeIdMessageListPair[] typedMessages = TypeIdMessageListPair.SplitMessagesByType(this.maxTypeId, messages, this.doHandleMessageByType, this);
			IList<RelayMessage> typeMessages;
			IRelayComponent component = null;
			Port<Exception> port = new Port<Exception>();
			List<IRelayComponent> componentList = null;
			List<IReplicationComponent> replicationComponentList = null;

			for (int typeId = 0; typeId < typedMessages.Length; typeId++)
			{
				if (typedMessages[typeId] != null)
				{
					typeMessages = typedMessages[typeId].GetMessages();

					GetOutComponents(typeId, out componentList, out replicationComponentList);
					
					if (componentList != null)
					{
						bool gotAll = false;

						for (int componentIndex = 0; componentIndex < componentList.Count && !gotAll; componentIndex++)
						{
							component = componentList[componentIndex];

							//imporant we declare this here because of closure
							IAsyncDataHandler asyncHandler = component as IAsyncDataHandler;
							if (asyncHandler != null)
							{
								try
								{
									asyncHandler.BeginHandleMessages(messages, null, asyncResult =>
									{
										try
										{
											asyncHandler.EndHandleMessages(asyncResult);
											port.Post(null);
										}
										catch (Exception exc)
										{
											if (log.IsErrorEnabled)
												log.ErrorFormat("Exception in EndHandleMessages: {0}", exc);
											port.Post(exc);
										}
									});
								}
								catch (Exception exc)
								{
									if (log.IsErrorEnabled)
										log.ErrorFormat("Exception in async HandleOutMessages: {0}", exc);
									port.Post(exc);
								}
								Exception hadException = null;
								yield return Arbiter.Receive(false, port, exc => hadException = exc);
								if (hadException != null) yield break;
							}
							else
							{
								try
								{
									component.HandleMessages(typeMessages);
								}
								catch (Exception exc)
								{
									if (log.IsErrorEnabled)
										log.ErrorFormat("Exception in HandleOutMessages: {0}", exc);
								}
							}
							gotAll = true;
							for (int messageIndex = 0; messageIndex < typeMessages.Count; messageIndex++)
							{
								if (typeMessages[messageIndex].Payload == null)
								{
									gotAll = false;
									break;
								}
							}
						}
					}

					Replicate(typeMessages, replicationComponentList);
				}
			}

			//  Null out query data so we don't waste network
			//  bandwidth sending input info back to the client
			for (int i = 0; i < messages.Count; i++)
			{
				messages[i].QueryData = null;
			}

			try
			{
				//only returns deletes if they are enabled.
				IList<RelayMessage> deletes = this.ttl.ProcessExpiredTTLDeletes(messages);
				if (deletes != null)
				{
					HandleInMessages(deletes);
				}
			}
			catch (Exception exc)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception handling ttl deletes in HandleOutMessages: {0}", exc);                
			}
		}

		public void HandleOutMessages(IList<RelayMessage> messages)
		{
			TypeIdMessageListPair[] typedMessages = TypeIdMessageListPair.SplitMessagesByType(maxTypeId, messages, doHandleMessageByType, this);
			IList<RelayMessage> typeMessages;
			List<IRelayComponent> componentList = null;
			List<IReplicationComponent> replicationComponentList = null;
			bool gotAll = false;

			for (int i = 0; i < messages.Count; i++)
			{
				if(CheckForConfigurationProblems(messages[i])) return;
			}

			for (int typeId = 0; typeId < typedMessages.Length; typeId++)
			{
				if (typedMessages[typeId] != null)
				{
					typeMessages = typedMessages[typeId].GetMessages();

					GetOutComponents(typeId, out componentList, out replicationComponentList);

					gotAll = false; //have to init this for every typeMessage list or we'll only handle the first
					if (componentList != null)
					{
						for (int componentIndex = 0; componentIndex < componentList.Count && !gotAll; componentIndex++)
						{
							componentList[componentIndex].HandleMessages(typeMessages);
							gotAll = true;
							for (int messageIndex = 0; messageIndex < typeMessages.Count; messageIndex++)
							{
								if (typeMessages[messageIndex].Payload == null)
								{
									gotAll = false;
								}
							}
						}
					}
					
					Replicate(typeMessages, replicationComponentList);
				}
			}

			//  Null out query data so we don't waste network
			//  bandwidth sending input info back to the client
			for (int i = 0; i < messages.Count; i++)
			{
				messages[i].QueryData = null;
			}

			//only returns deletes if they are enabled.
			IList<RelayMessage> deletes = this.ttl.ProcessExpiredTTLDeletes(messages);
			if (deletes != null)
			{
				HandleInMessages(deletes);
			}
		}

		/// <summary>
		/// Returns a value indicating if the given <see lang="string"/> version and 
		/// instance <see cref="Version"/> indicate the same version.
		/// </summary>
		/// <param name="version">The <see cref="Version"/> instance to compare.</param>
		/// <param name="versionString">The string value to compare.</param>
		/// <returns>Returns true if the two version values are equal; otherwise, false.</returns>
		private bool AreVersionsEqual(Version version, string versionString)
		{
			try
			{
				string[] versions = versionString.Split('.');
				if (versions.Length != 4)
				{
                    if (log.IsErrorEnabled)
                        log.ErrorFormat("Invalid version string {0}", versionString);
					return false;
				}

				if (versions[0] == "*")
				{
					return true;
				}
				else
				{
					if (Int32.Parse(versions[0]) == version.Major)
					{
						if (versions[1] == "*")
						{
							return true;
						}
						else
						{
							if (Int32.Parse(versions[1]) == version.Minor)
							{
								if (versions[2] == "*")
								{
									return true;
								}
								else
								{
									if (Int32.Parse(versions[2]) == version.Build)
									{
										if (versions[3] == "*")
										{
											return true;
										}
										else
										{
											if (Int32.Parse(versions[3]) == version.Revision)
											{
												return true;
											}
										}
									}
								}
							}
						}
					}
				}

				return false;
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error compaing version string {0} to Version {1}: {2}", versionString, version.ToString(4), ex);
				return false;
			}
		}

		/// <summary>
		/// Helper Method to add the <paramref name="component"/> to the given <see cref="typeComponentLists"/> using the <paramref name="typeList"/>
		/// to determine what array positions to use.
		/// </summary>
		/// <remarks>	
		///		<para>The idea here is to use the <paramref name="typeComponentLists"/> as a map between 
		///		<see cref="RelayMessage.TypeId"/> and the <see cref="IRelayComponent"/> that handle those type ids.
		///		</para>
		/// </remarks>
		/// <param name="component">The <see cref="IRelayComponent"/> to add to the map.</param>
		/// <param name="typeComponentLists">The map.</param>
		/// <param name="typeList">The <see cref="TypeList"/> used to determine where in the map to reference the <paramref name="component"/>.</param>
		private static void AddComponentToTypeLists<T>(T component, List<T>[] typeComponentLists, TypeList typeList)
		{
			if (typeList == null || typeList.Count == 0) return;

			//if * is specified for type list
			if (typeList[0] == -1) //all types
			{
				for (int typeId = 0; typeId < typeComponentLists.Length; typeId++)
				{
					if (typeComponentLists[typeId] == null) typeComponentLists[typeId] = new List<T>();
					typeComponentLists[typeId].Add(component);
				}
			}
			else //specified list
			{
				foreach (int typeId in typeList)
				{
					if (typeId >= typeComponentLists.Length)
					{
						log.ErrorFormat("Can't add type Id {0} to list because it is >= to the length of the list!", typeId);
					}
					else
					{
						if (typeComponentLists[typeId] == null) typeComponentLists[typeId] = new List<T>();
						typeComponentLists[typeId].Add(component);
					}
					
				}
			}
		}

		/// <summary>
		/// Gets a value indicating if the <see cref="MessageType"/> is one that <see cref="Components"/>
		/// can handle and process messages for.
		/// </summary>
		/// <param name="type">The <see cref="MessageType"/> to evaluate for handling.</param>
		/// <returns>Returns true if the <see cref="MessageType"/> will be handled by the current components,
		/// otherwise returns false.</returns>
		public bool DoHandleMessagesOfType(MessageType type)
		{
			int intType = (int)type;
			if (MessageType.NumTypes == type || intType >= doHandleMessageByType.Length || intType < 0) return false;

			return doHandleMessageByType[intType];
		}

		#region private class TypeIdMessageListPair

		/// <summary>
		/// Helper class for <see cref="RelayNode"/>.  Represents a list of <see cref="RelayMessage"/>
		/// where all the messages contained have the same <see cref="RelayMessage.TypeId"/> as the
		/// <see cref="TypeId"/>.
		/// </summary>
		private class TypeIdMessageListPair
		{
			private readonly List<RelayMessage> messages = new List<RelayMessage>();

			private TypeIdMessageListPair(short typeId)
			{
				this.TypeId = typeId;
			}

			/// <summary>
			/// Adds a <see cref="RelayMessage"/> to the list.
			/// </summary>
			/// <param name="message"></param>
			private void Add(RelayMessage message)
			{
				if (message == null) return;

				if (message.TypeId != TypeId)
				{
					//log and allow
					if(log.IsWarnEnabled)
                    log.WarnFormat("RelayMessage.TypeId {0} not consistent with TypeIdMessageListPair.TypeId {1}"
						, message.TypeId, TypeId);
				}
				this.messages.Add(message);
			}

			/// <summary>
			/// Gets the <see cref="RelayMessage.TypeId"/> that each <see cref="RelayMessage"/> has.
			/// </summary>
			internal short TypeId { get; private set; }

			/// <summary>
			/// Gets the <see cref="RelayMessage"/> for the given index.
			/// </summary>
			/// <param name="index"></param>
			/// <returns></returns>
			internal RelayMessage this[int index]
			{
				get { return this.messages[index]; }
			}

			/// <summary>
			/// Gets the number of <see cref="RelayMessage"/>s in the collection.
			/// </summary>
			internal int Count
			{
				get { return this.messages.Count; }
			}

			/// <summary>
			/// Get the list of messages. Don't use this to modify the list contents.
			/// </summary>
			/// <remarks>
			///		<para>Considered implementing this as <see cref="ReadOnlyCollection{T}"/>, however,
			///		since this is a private class and this is a heavily used function, we're
			///		breaking encapsulation instead.</para>
			/// </remarks>
			/// <returns></returns>
			internal IList<RelayMessage> GetMessages()
			{
				return this.messages;
			}

			/// <summary>
			/// Enumerates through the <paramref name="messages"/> list and aggregates messsage that have the same
			/// <see cref="RelayMessage.TypeId"/> and returns a list of the aggregates. Any messages that have been marked as 
			/// redirected will not be put in the returned array
			/// </summary>
			/// <param name="maxTypeId">The largest <see cref="RelayMessage.TypeId"/> to be encounterd (for optimization).</param>
			/// <param name="messages">A list of message to enumerate.</param>
			/// <param name="handleMessageByType">A map of <see cref="RelayMessage.TypeId"/> and booleans indicating if a
			/// given type id should be ignored or handled.</param>
			/// <returns>An array of <see cref="TypeIdMessageListPair"/> containing the aggregated messages.</returns>
			internal static TypeIdMessageListPair[] SplitMessagesByType(int maxTypeId, IList<RelayMessage> messages, bool[] handleMessageByType, RelayComponents parent)
			{
				TypeIdMessageListPair[] typedMessages = new TypeIdMessageListPair[maxTypeId + 1];
				for (int i = 0; i < messages.Count; i++)
				{
					RelayMessage message = messages[i];
					if (handleMessageByType[(int)message.MessageType] && !message.WasRedirected)
					{
						parent.CheckForConfigurationProblems(message);
						short typeId = message.TypeId;
						if (typedMessages[typeId] == null)
						{
							typedMessages[typeId] = new TypeIdMessageListPair(typeId);
						}
						typedMessages[typeId].Add(message);
					}
				}
				return typedMessages;
			}
		}

		#endregion

		/// <summary>
		/// Sets up the <see cref="ttl"/> instance.
		/// </summary>
		private void SetupLifetimeTtl()
		{
			bool deletesEnabled = false;
			TypeSettingCollection typeSettings = null;
			RelayNodeConfig cfg = relayNodeConfig;
			if (cfg != null)
			{
				deletesEnabled = cfg.SendExpirationDeletes;
				if (cfg.TypeSettings != null)
				{
					typeSettings = cfg.TypeSettings.TypeSettingCollection;
				}
			}

			this.ttl = new LifetimeTtl(deletesEnabled, typeSettings);
		}

		/// <summary>
		/// Initializes the current instance using the config provided in the Ctor or <see cref="ReloadConfig"/>.
		/// Intended to only be called once and use <see cref="ReloadConfig"/> for all other changes.
		/// </summary>
		/// <param name="runStates">Any state information to initialize components, possibly from
		/// before a server reload.</param>
		/// <param name="ignoredMessageTypes">A string array of <see cref="MessageType"/>s to ignore.</param>
		public void Initialize(ComponentRunState[] runStates, string[] ignoredMessageTypes)
		{
            if (log.IsInfoEnabled)
                log.Info("Beginning component initialization.");
			if (this.config == null)
			{
				const string noConfig = "No configuration found.";
                if (log.IsErrorEnabled)
                    log.Error(noConfig);
				throw new InvalidOperationException(noConfig);
			}

			SetupLifetimeTtl();

			//still run the following even if there are no configs, because there is setup done
			LoadAndInitializeUnloadedComponents(runStates);

			BuildActiveComponents();
			IList<MessageType> ignoredTypes = ConvertFromStringMessageType(ignoredMessageTypes);

			BuildInOutLists(maxTypeId, ignoredTypes);

			WarmUpComponents();

            if (log.IsInfoEnabled)
                log.Info("Completed component initialize.");

			PrintSummary();
		}

		private void Replicate(RelayMessage message, IList<IReplicationComponent> replicationComponents)
		{
			if (replicationComponents != null)
			{
				for (int i = 0; i < replicationComponents.Count; i++)
				{
					replicationComponents[i].Replicate(message);
				}
			}
		}

		private void Replicate(IList<RelayMessage> messages, IList<IReplicationComponent> replicationComponents)
		{
			if (replicationComponents != null)
			{
				for (int i = 0; i < replicationComponents.Count; i++)
				{
					replicationComponents[i].Replicate(messages);
				}
			}
		}

		private void PrintSummary()
		{
			try
			{
				List<IRelayComponent> active = this.activeComponents;
				Dictionary<string, IRelayComponent> loaded = this.loadedComponents;

                if (log.IsInfoEnabled)
                {
                    log.Info("Loaded Components:");
                    foreach (IRelayComponent component in loaded.Values)
                    {
                        log.Info(component.GetComponentName());
                    }

                    log.Info("Active Components:");
                    for (int i = 0; i < active.Count; i++)
                    {
                        log.Info(active[i].GetComponentName());
                    }
                }
			}
			catch (Exception exc)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Failed to print summary: {0}", exc);
			}
		}

		/// <summary>
		/// Builds the list of <see cref="IRelayComponent"/>s that hanlde "in" and "out" <see cref="RelayMessage"/>s.
		/// </summary>
		/// <param name="maxTypeId">The maximum <see cref="RelayMessage.TypeId"/> value to handle (used for optimization).</param>
		/// <param name="ignoredMessageTypes">A list of <see cref="MessageType"/> to ignore.</param>
		private void BuildInOutLists(short maxTypeId, IList<MessageType> ignoredMessageTypes)
		{
            log.Debug("Building In Out Handling");

			hasReplicationComponent = false;
			List<IRelayComponent>[] inLists = new List<IRelayComponent>[maxTypeId + 1];
			List<IRelayComponent>[] outLists = new List<IRelayComponent>[maxTypeId + 1];
			List<IReplicationComponent>[] inReplicationLists = new List<IReplicationComponent>[maxTypeId + 1];
			List<IReplicationComponent>[] outReplicationLists = new List<IReplicationComponent>[maxTypeId + 1];

			if (this.config != null && this.config.Count > 0)
			{
				Dictionary<string, MySpace.DataRelay.Common.Schemas.RelayComponent> configs = new Dictionary<string, MySpace.DataRelay.Common.Schemas.RelayComponent>();
				for (int i = 0; i < config.Count; i++)
				{
					configs[config[i].Name] = config[i];
				}

				for (int i = 0; i < activeComponents.Count; i++)
				{
					IRelayComponent component = activeComponents[i];
					MySpace.DataRelay.Common.Schemas.RelayComponent componentConfig = null;
					if (configs.TryGetValue(component.GetComponentName(), out componentConfig))
					{
						if (componentConfig.Replicator)
						{
							IReplicationComponent replicationComponent = component as IReplicationComponent;
							//ensure we support the correct interface
							if (replicationComponent == null)
							{
								if (log.IsErrorEnabled) 
								{
									log.ErrorFormat("If the config say Replicator true then the replication component must support IReplicationComponent for {0}",
										component.GetComponentName());
								}
								break;
							}
							if (component.GetComponentName() == "Forwarding")  // i know, it's gross
							{
								hasReplicationComponent = true;
							}
							AddComponentToTypeLists(replicationComponent, inReplicationLists, componentConfig.InTypeIds);
							AddComponentToTypeLists(replicationComponent, outReplicationLists, componentConfig.OutTypeIds);
						}
						else
						{
							AddComponentToTypeLists(component, inLists, componentConfig.InTypeIds);
							AddComponentToTypeLists(component, outLists, componentConfig.OutTypeIds);
						}
					}
					else 
					{
						if(log.IsErrorEnabled)
                            log.ErrorFormat(@"NON-OPERATIONAL COMPONENT!! ""{0}"" - Config not found. Relay Component name doesn't match the value in the config file."
							, component.GetComponentName());
					}
				}
			}

            
            log.Debug("About to use new in out handling and setup ignored RelayMessage types.");

			GetDiff(this.inComponents, inLists, "IN").ForEach(msg => log.Info(msg));

			typeComponentInListsLock.Write(() =>
				this.inComponents = inLists
				);

			GetDiff(this.outComponents, outLists, "OUT").ForEach(msg => log.Info(msg));

			typeComponentOutListsLock.Write(() =>
				this.outComponents = outLists);

			GetDiff(this.inReplicatorComponents, inReplicationLists, "IN REPLICATION");

			typeComponentInListsLock.Write(() =>
				this.inReplicatorComponents = inReplicationLists
				);

			GetDiff(this.outReplicatorComponents, outReplicationLists, "OUT REPLICATION");

			typeComponentOutListsLock.Write(() =>
				this.outReplicatorComponents = outReplicationLists
				);

			this.maxTypeId = maxTypeId;

			SetupIgnoredMessageTypes(ignoredMessageTypes);

            if (log.IsDebugEnabled)
            {
                log.Debug("Now using new in out handling and ignored RelayMessage types (if any are defined).");
                log.Debug("Finished building In Out Handling");
            }            
		}

		internal static List<string> GetDiff<T>(List<T>[] origList, List<T>[] newList, string messageInOut) where T: IRelayComponent
		{
			//only print diff if there are two lists
			if (origList == null || newList == null) return new List<string>();
			int maxListTypeId = Math.Max(origList.Length, newList.Length);
			List<string> output = new List<string>();

			for (int typeId = 0; typeId < maxListTypeId; typeId++)
			{
				List<T> origCmps = null;
				List<T> newCmps = null;

				if (typeId < origList.Length && origList[typeId] != null)
				{
					origCmps = new List<T>(origList[typeId]);
				}

				if (typeId < newList.Length && newList[typeId] != null)
				{
					newCmps = new List<T>(newList[typeId]);
				}

				//if neither is null, then diff and print only the differences
				if (origCmps != null && newCmps != null)
				{
					for (int i = 0; i < origCmps.Count; i++)
					{
						IRelayComponent origCmp = origCmps[i];
						for (int j = 0; j < newCmps.Count; j++)
						{
							IRelayComponent newCmp = newCmps[j];
							if (origCmp.GetComponentName().Equals(newCmp.GetComponentName()))
							{
								//remove from both
								origCmps.RemoveAt(i);
								i--;
								newCmps.RemoveAt(j);
								break;
							}
						}
					}
					//whatever's left in new is added, whatever's left in old was removed.
				}
				if (origCmps != null)
				{
					//if either is null, the other is all new
					foreach (IRelayComponent cmp in origCmps)
					{
						string msg = string.Format("Relay Component {0} REMOVED {1} message support for TypeId {2}", cmp.GetComponentName(), messageInOut, typeId);
						output.Add(msg);
					}
				}

				if (newCmps != null)
				{
					foreach (IRelayComponent cmp in newCmps)
					{
						string msg = string.Format("Relay Component {0} ADDED {1} message support for TypeId {2}", cmp.GetComponentName(), messageInOut, typeId);
						output.Add(msg);
					}
				}
			}
			return output;
		}

		/// <summary>
		/// Builds the list of active components.
		/// </summary>
		private void BuildActiveComponents()
		{
			if (this.config == null || this.config.Count == 0) return;

            if (log.IsInfoEnabled)
                log.Info("Building active component list.");

			//dump last active & creat new list
			List<IRelayComponent> newList = new List<IRelayComponent>(config.Count);

			//create list of active
			IRelayComponent component = null;
			for (int i = 0; i < config.Count; i++)
			{
				if (loadedComponents.TryGetValue(config[i].Name, out component))
				{
					newList.Add(component);
					if(log.IsInfoEnabled)
                        log.InfoFormat(@"Component considered active: ""{0}""", component.GetComponentName());
				}
			}

			this.activeComponents = newList;

            if (log.IsInfoEnabled)
                log.Info("Finished building active component list.");
		}

		private void WarmUpComponents()
		{
			if (activeComponents == null) return;
			foreach (IRelayComponent component in activeComponents)
			{
				IWarmableComponent warmable = component as IWarmableComponent;
				if (warmable != null) warmable.WarmUp();
			}
		}

		/// <summary>
		/// Loads any components that are declared in the config that have not yet been loaded into memory.
		/// </summary>
		/// <remarks>
		/// <para>When used to load the first time, components aren't intialized so that state information
		/// can be used for intitialization.  On a config reload, only new assemblies are loaded and no state info
		/// is used</para>
		/// </remarks>
		/// <param name="componentRunStates">The run states.</param>
		private IList<IRelayComponent> LoadAndInitializeUnloadedComponents(ComponentRunState[] componentRunStates)
		{
			List<IRelayComponent> newlyLoaded = new List<IRelayComponent>();

			if (config == null) return newlyLoaded;

            if (log.IsInfoEnabled)
                log.Info("Loading and intializing unloaded components");
			for (int i = 0; i < config.Count; i++)
			{
				MySpace.DataRelay.Common.Schemas.RelayComponent componentConfig = config[i];
				if (IsLoaded(componentConfig) == false)
				{
					IRelayComponent component = null;
					try
					{
						if(log.IsInfoEnabled)
                            log.InfoFormat("Loading component {0}", componentConfig.Name);
						component = LoadNewInstance(config[i]);
						if(log.IsInfoEnabled)
                            log.InfoFormat("Successfully Loaded Component {0}", componentConfig.Name);
					}
					catch (Exception exc)
					{
                        if (log.IsErrorEnabled)
                            log.ErrorFormat("FAILED TO LOAD COMPONENT {0}: {1}", componentConfig.Name, exc);						
						throw;
					}

					if (component != null)
					{
						string componentName = component.GetComponentName();
						try
						{
							const bool ignoreCase = true;
							ComponentRunState runState = null;
							if (componentRunStates != null)
							{
								//Array.Find doesn't allow first parm to be null
								runState = Array.Find(componentRunStates, (state) =>
								{
									if (state == null) return false;
									return (string.Compare(state.ComponentName, componentName, ignoreCase) == 0);
								}
								);
							}

							if(log.IsInfoEnabled)
                                log.InfoFormat(@"Initializing Componenet: ""{0}""", componentName);
							component.Initialize(this.relayNodeConfig, runState);
							this.loadedComponents[config[i].Name] = component;
                            if (log.IsInfoEnabled)
                                log.InfoFormat(@"Component Initialized: ""{0}""", componentName);
						}
						catch (Exception exc)
						{
                            if (log.IsErrorEnabled)
                                log.ErrorFormat("FAILED TO INITIALIZE COMPONENT {0}: {1}", componentName, exc);						                            
							try
							{
								//try and cleanup
								component.Shutdown();
							}
							catch (Exception e)
							{
                                if (log.IsErrorEnabled)
                                    log.ErrorFormat("EXCEPTION SHUTTING DOWN FAILED COMPONENT {0}: {1}", componentName, e);						                                                            
							}
							throw;
						}

						newlyLoaded.Add(component);
					}
				}
			}
            if (log.IsInfoEnabled)
                log.Info("Finished loading and intializing unloaded components");
			return newlyLoaded;
		}

		/// <summary>
		/// Sets the list of <see cref="MessageType"/>s as the list of types to ignore handling for.
		/// </summary>
		/// <param name="ignoreTypes">The list of <see cref="MessageType"/> to ignore.</param>
		private void SetupIgnoredMessageTypes(IList<MessageType> ignoreTypes)
		{
			if (ignoreTypes == null) return;

			Array actualTypes = Enum.GetValues(typeof(MessageType));
			int largestValue = (int)((MessageType)actualTypes.GetValue(actualTypes.Length - 1)) + 1;
			bool[] doHandle = new bool[largestValue];

			//initialize temp array
			for (int i = 0; i < doHandle.Length; i++)
			{
				doHandle[i] = true;
			}

			//build temp array with correct values

			for (int i = 0; i < ignoreTypes.Count; i++)
			{
				foreach (MessageType existingType in actualTypes)
				{
					if (existingType == ignoreTypes[i])
					{
						if(log.IsInfoEnabled)
                            log.InfoFormat("Ignoring message type {0}", existingType);
						doHandle[(int)existingType] = false;
						break;
					}
				}
			}

			//swap temp array into live array.
			Interlocked.Exchange(ref this.doHandleMessageByType, doHandle);
		}

		/// <summary>
		/// Converts an array of string <see cref="MessageType"/>s to a list of <see cref="MessageType"/>
		/// instances.
		/// </summary>
		/// <remarks>
		///		<para>If a type is not known, it will be logged and skipped.</para>
		/// </remarks>
		/// <param name="messageTypes">An array of strings that represent <see cref="MessageType"/>s.</param>
		/// <returns>Returns a list of <see cref="MessageType"/>.</returns>
		private IList<MessageType> ConvertFromStringMessageType(string[] messageTypes)
		{
			if (messageTypes == null) return new MessageType[0];

			List<MessageType> newList = new List<MessageType>(messageTypes.Length);
			Type type = typeof(MessageType);
			const bool ignoreCase = true;
			for (int i = 0; i < messageTypes.Length; i++)
			{
				try
				{
					if (messageTypes[i] != null)
					{
						object result = Enum.Parse(type, messageTypes[i], ignoreCase);
						if (result != null) newList.Add((MessageType)result);
					}
				}
				catch
				{
					if(log.IsInfoEnabled)
                        log.InfoFormat("Can't ignore unknown ignored type: {0}", messageTypes[i]);
				}
			}
			return newList;
		}

		/// <summary>
		/// Loads a new instance of <see cref="IRelayComponent"/> using the given <see cref="RelayComponent"/>
		/// config file.
		/// </summary>
		/// <param name="relayComponent">The config to use.</param>
		/// <returns>A new <see cref="IRelayComponent"/> instance. Never <see langword="null"/>.</returns>
		/// <exception cref="ApplicationException">Thrown when a new instance can't be created for any reason.
		/// </exception>
		private IRelayComponent LoadNewInstance(MySpace.DataRelay.Common.Schemas.RelayComponent relayComponent)
		{
			IRelayComponent component = null;

			Type componentType = Type.GetType(relayComponent.Type);
			if (componentType != null)
			{
				Assembly relayAssembly = Assembly.GetAssembly(componentType);
				if (relayAssembly == null)
				{
					throw new ApplicationException(string.Format("Assembly not found for {0}", componentType));
				}

				if (AreVersionsEqual(relayAssembly.GetName().Version, relayComponent.Version))
				{
					component = relayAssembly.CreateInstance(componentType.FullName) as IRelayComponent;
				}
				else
				{
					throw new ApplicationException(string.Format("Version mismatch for component {0}. Version defined: {1} , version found {2}", relayComponent.Name, relayComponent.Version, relayAssembly.GetName().Version));
				}
			}
			else
			{
				throw new ApplicationException(string.Format("Problem loading type {0} for component {1}", relayComponent.Type, relayComponent.Name));
			}

			return component;
		}

		/// <summary>
		/// Gets a value indicating if the <see cref="IRelayComponent"/> represented by the
		/// <see cref="RelayComponent"/> config has been loaded.
		/// </summary>
		/// <param name="relayComponent">The <see cref="RelayComponent"/> config.</param>
		/// <returns>Returns true if the <see cref="IRelayComponent"/> has been loaded; otherwise, 
		/// returns false.</returns>
		private bool IsLoaded(MySpace.DataRelay.Common.Schemas.RelayComponent relayComponent)
		{
			return loadedComponents.ContainsKey(relayComponent.Name);
		}

		/// <summary>
		/// Applies a new configuration and will use the new settings including loading new components and
		/// disabling existing components.  Meant to be called after <see cref="Initialize"/>.
		/// </summary>
		/// <param name="newConfig">The new <see cref="RelayNodeConfig"/> to use.</param>
		/// <param name="ignoredMessageTypes">A string array of <see cref="MessageType"/>s to ignore.</param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="newConfig"/> is <see langword="null"/>.</para>
		/// </exception>
		public void ReloadConfig(RelayNodeConfig newConfig, string[] ignoredMessageTypes)
		{
			if (newConfig == null) throw new ArgumentNullException("newConfig");
            if (log.IsInfoEnabled)
                log.Info("Config Reload: Beginning component reload.");

			lock (_reloadShutdownLock)
			{
				SetConfig(newConfig);

				//reload configs

				if (this.config == null || this.config.Count == 0)
				{
                    if (log.IsErrorEnabled)
                        log.Error("Config Reload: No configuration found, no components loaded.");
				}

				SetupLifetimeTtl();

				//still run the following even if there are no configs, because there is setup done
				ComponentRunState[] runState = null; //no run state save on config reload

				IList<IRelayComponent> newlyLoaded = null;

				try
				{
					newlyLoaded = LoadAndInitializeUnloadedComponents(runState);
				}
				catch (Exception exc)
				{

                    if (log.IsErrorEnabled)
                        log.Error("Exception initializing components during config reload: {0}", exc);                    
				}
				BuildActiveComponents();
				IList<MessageType> ignoredTypes = ConvertFromStringMessageType(ignoredMessageTypes);
				
				if (newlyLoaded != null)
				{
					foreach (IRelayComponent component in this.activeComponents)
					{
						//only reload existing items.
						if (newlyLoaded.Contains(component) == false)
						{
							try
							{
								component.ReloadConfig(relayNodeConfig);
							}
							catch (Exception exc)
							{
                                if (log.IsErrorEnabled)
                                    log.ErrorFormat("Failed to reload config for component {0}: {1}", component.GetComponentName(), exc);                                                                        
							}
						}
					}
				}
				
				BuildInOutLists(maxTypeId, ignoredTypes);

                if (log.IsInfoEnabled)
                    log.Info("Config Reload: Completed component reload.");

				PrintSummary();
			}
		}

		/// <summary>
		/// Get the <see cref="ComponentRunState"/> from the curently running components.
		/// </summary>
		/// <returns>Returns an array of <see cref="ComponentRunState"/> or null.</returns>
		public ComponentRunState[] GetComponentRunStates()
		{
			IList<IRelayComponent> runningComponents = this.activeComponents; //important to avoid threading issues
			ComponentRunState[] runstates = new ComponentRunState[runningComponents.Count];
			for (int i = 0; i < runningComponents.Count; i++)
			{
				if (runningComponents[i] != null)
				{
					try
					{
						runstates[i] = runningComponents[i].GetRunState();
					}
					catch (Exception ex)
					{
                        if (log.IsErrorEnabled)
                            log.ErrorFormat("Error getting run state for component {0}: {1}", activeComponents[i].GetComponentName(), ex);
						runstates[i] = null;
					}
				}
			}
			return runstates;
		}

		/// <summary>
		/// Gets a relay component, given its name, from the set of loaded components.  The component may
		/// not be active.
		/// </summary>
		/// <param name="componentName">The name of the component.</param>
		/// <returns>
		/// The relay component as an <see cref="IDataHandler"/> or null if there is no component
		/// with the specified name.
		/// </returns>
		public IDataHandler GetComponent(string componentName)
		{
			IRelayComponent component;

			return loadedComponents.TryGetValue(componentName, out component) ? component : null;
		}

		/// <summary>
		/// Gets the <see cref="ComponentRuntimeInfo"/> for a relay component.
		/// </summary>
		/// <param name="componentName">The name of the component.</param>
		/// <returns>
		/// The <see cref="ComponentRuntimeInfo"/> or null if the component does not exist or has
		/// no runtime info.
		/// </returns>
		public ComponentRuntimeInfo GetComponentRuntimeInfo(string componentName)
		{
			IRelayComponent component;

			return loadedComponents.TryGetValue(componentName, out component) ? component.GetRuntimeInfo() : null;
		}

		/// <summary>
		/// Get the <see cref="ComponentRuntimeInfo"/> from the currently running components.
		/// </summary>
		/// <returns>Returns an array of <see cref="ComponentRuntimeInfo"/> or null.</returns>
		public ComponentRuntimeInfo[] GetComponentsRuntimeInfo()
		{
			IList<IRelayComponent> runningComponents = this.activeComponents; //important to avoid threading issues
			List<ComponentRuntimeInfo> infoList = new List<ComponentRuntimeInfo>(runningComponents.Count);
			for (int i = 0; i < runningComponents.Count; i++)
			{
				ComponentRuntimeInfo info = runningComponents[i].GetRuntimeInfo();
				if (info != null)
				{
					infoList.Add(info);
				}
			}
			return infoList.ToArray();
		}

		internal string GetComponentsDescription()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("Active Components:");
			IList<IRelayComponent> runningComponents = activeComponents; //important to avoid threading issues
			foreach (IRelayComponent component in runningComponents)
			{
				sb.AppendFormat("{0} ({1})\n",component.GetComponentName(), component.GetType());
			}
			return sb.ToString();
		}

		/// <summary>
		/// Calls <see cref="IRelayComponent.Shutdown"/> on all loaded components.
		/// </summary>
		public void Shutdown()
		{
			lock (_reloadShutdownLock)
			{
				RelayServicesClient.Instance.RelayNodeServices = null;
				//If reload got triggered while shutting down, could be a problem enumerating,
				//that's why we're in a lock
				foreach (IRelayComponent component in loadedComponents.Values)
				{
                    if (log.IsInfoEnabled)
                        log.InfoFormat("Shutting down component: {0}", component.GetComponentName());
					try
					{
						component.Shutdown();
					}
					catch (Exception exc)
					{
                        if (log.IsErrorEnabled)
                            log.ErrorFormat("Exception shutting down component {0}: {1}", component.GetComponentName(), exc);
                        
					}
				}
			}
		}

		
	}
}
