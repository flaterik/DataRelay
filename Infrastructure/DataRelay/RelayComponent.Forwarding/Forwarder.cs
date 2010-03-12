using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Responsible for forwading <see cref="RelayMessage"/> to other relay node servers and
	/// is used by clients to send requests into the relay transport.
	/// </summary>
	public class Forwarder : IAsyncDataHandler, IReplicationComponent
	{
		/// <summary>
		/// The official name of this component, for use in configuration files.
		/// </summary>
		public static readonly string ComponentName = "Forwarding";

		private static readonly LogWrapper _log = new LogWrapper();
		private static readonly object _reloadLock = new object(); //static because there are singletons involved

		[ThreadStatic]
		private static AutoResetEvent _outMessageWaitHandle;
		private static AutoResetEvent OutMessageWaitHandle
		{
			get
			{
				if (_outMessageWaitHandle == null)
				{
					_outMessageWaitHandle = new AutoResetEvent(false);
				}
				return _outMessageWaitHandle;
			}
		}

		private DateTime _initDate = DateTime.Now;
		private RelayNodeDefinition _myNodeDefinition;
		private bool _enableAsyncBulkGets;

		#region IRelayComponent Members

		/// <summary>
		/// Returns a unique human readable component name.  This name MUST match the name used
		/// in the component config file.
		/// </summary>
		/// <returns>The name of the component.</returns>
		public string GetComponentName()
		{
			return ComponentName;
		}

		/// <summary>
		/// this (or <see cref="ReloadConfig"/>) gets called (depending) when the TypeSettings.Config file
		/// are changed.  
		/// </summary>
		/// <remarks>
		/// Added: craigbro
		/// cachedTypeSettings now held with the forwarder for reference of
		/// exceptions on Synchronous "in" messages.  
		/// To reload, we just null out the cached TypeSettingsCollection object and the
		/// accessor will reload it on next call
		/// While ConfigurationManager.GetSection is quite performant after the 1st hit,
		/// keeping them cached is about twice as fast.		
		/// </remarks>
		/// <param name="config"></param>
		/// <param name="runState"></param>
		private void LoadConfig(RelayNodeConfig config, ComponentRunState runState)
		{
			if (config != null)
			{
				if (config.RelayComponents != null)
				{
					object configObject = config.RelayComponents.GetConfigFor(GetComponentName());
					ForwardingConfig forwardingConfig = configObject as ForwardingConfig;
					if(forwardingConfig == null)
					{
						if(_log.IsInfoEnabled)
						{
							_log.Info("No forwarding configuration supplied. Using defaults.");
						}
						forwardingConfig = new ForwardingConfig();

					}
					NodeManager.Initialize(config, forwardingConfig, GetErrorQueues(runState));
					_enableAsyncBulkGets = forwardingConfig.EnableAsyncBulkGets;
					_myNodeDefinition = NodeManager.Instance.GetMyNodeDefinition();
                    
					short maxTypeId = 0;
					if(config.TypeSettings != null)
					{
						maxTypeId = config.TypeSettings.MaxTypeId;
					}
					DebugWriter.SetTraceSettings(maxTypeId,forwardingConfig.TraceSettings);
					DebugWriter.WriteCallingMethod = forwardingConfig.WriteCallingMethod;
					DebugWriter.WriteMessageTrace = forwardingConfig.WriteMessageTrace;
				}
				else
				{
					NodeManager.Initialize(null, null, null);
				}
			}
			else
			{
				NodeManager.Initialize(null, null, null);
			}
		}

		/// <summary>
		/// this (or <see cref="LoadConfig"/>) gets called (depending) when the TypeSettings.Config file
		/// are changed.  
		/// </summary>
		/// <remarks>
		/// Added: craigbro
		/// cachedTypeSettings now held with the forwarder for reference of
		/// exceptions on Synchronous "in" messages.  
		/// To reload, we just null out the cached TypeSettingsCollection object and the
		/// accessor will reload it on next call
		/// While ConfigurationManager.GetSection is quite performant after the 1st hit,
		/// keeping them cached is about twice as fast.		
		/// </remarks>
		/// <param name="config">The config to reload</param>
		public void ReloadConfig(RelayNodeConfig config)
		{
			lock (_reloadLock)
			{
				if (config != null)
				{
					try
					{
						object configObject = config.RelayComponents.GetConfigFor(GetComponentName());
						ForwardingConfig forwardingConfig = configObject as ForwardingConfig;
						if (forwardingConfig == null)
						{
							if(_log.IsInfoEnabled)
							{
								_log.InfoFormat("No forwarding configuration supplied. Reloading using defaults.");
							}
							forwardingConfig = new ForwardingConfig();
						}
						NodeManager.Instance.ReloadConfig(config, forwardingConfig);
						
						_enableAsyncBulkGets = forwardingConfig.EnableAsyncBulkGets;
						_myNodeDefinition = NodeManager.Instance.GetMyNodeDefinition();

						short maxTypeId = 0;
						if (config.TypeSettings != null)
						{
							maxTypeId = config.TypeSettings.MaxTypeId;
						}
						DebugWriter.SetTraceSettings(maxTypeId, forwardingConfig.TraceSettings);
						DebugWriter.WriteCallingMethod = forwardingConfig.WriteCallingMethod;
						DebugWriter.WriteMessageTrace = forwardingConfig.WriteMessageTrace;
					}
					catch (Exception ex)
					{
						if (_log.IsErrorEnabled)
							_log.ErrorFormat("Exception reloading config: {0}", ex);
					}
				}
			}
		}

		/// <summary>
		/// Returns a ComponentRunState with any existing error queues, so that they can be persisted through
		/// AppDomain reloads.
		/// </summary>		
		public ComponentRunState GetRunState()
		{
			ComponentRunState runState = new ComponentRunState(GetComponentName());
			Dictionary<string, Dictionary<string, MessageQueue>> errorQueues = NodeManager.Instance.GetErrorQueues();
			ErrorQueueState state = new ErrorQueueState {ErrorQueues = errorQueues};
			runState.SerializedState = MySpace.Common.IO.Serializer.Serialize(state, false);
			return runState;
		}

		private static Dictionary<string, Dictionary<string, MessageQueue>> GetErrorQueues(ComponentRunState runState)
		{
			if (runState == null || runState.SerializedState == null) return null;

			try
			{
				MemoryStream stream = new MemoryStream(runState.SerializedState);
				ErrorQueueState state = MySpace.Common.IO.Serializer.Deserialize<ErrorQueueState>(stream, false);

				if (state != null) return (state.ErrorQueues);

				return null;
			}
			catch (Exception ex)
			{
				if (_log.IsErrorEnabled)
					_log.ErrorFormat("Exception getting Error Queues from Run State: {0}", ex);
				return null;
			}
		}

		/// <summary>
		/// Returns the results of GetHtmlStatus in a ForwarderRuntimeInfo object.
		/// </summary>		
		public ComponentRuntimeInfo GetRuntimeInfo()
		{
			ForwarderRuntimeInfo info = new ForwarderRuntimeInfo {HtmlStatus = GetHtmlStatus()};
			return info;
		}

		/// <summary>
		/// Initializes the component. Any error queues in runState will be reinstantiated.
		/// </summary>        
		public void Initialize(RelayNodeConfig config, ComponentRunState runState)
		{
			LoadConfig(config, runState);
			_initDate = DateTime.Now;
		}

		/// <summary>
		/// Shut down the component.
		/// </summary>
		public void Shutdown()
		{
			if (_log.IsInfoEnabled)
				_log.Info("Relay Forwarder shutting down.");
			NodeManager.Instance.Shutdown();
			if (_log.IsInfoEnabled)
				_log.Info("Relay Forwarder shutdown complete.");
		}

		#endregion

		private void SetHydrationPolicy(RelayMessage message)
		{
			if (message.IsTwoWayMessage && !message.IsGroupBroadcastMessage && !message.IsClusterBroadcastMessage)
			{
				var typeSetting = NodeManager.Instance.Config.TypeSettings.TypeSettingCollection[message.TypeId];
				if (typeSetting != null && typeSetting.HydrationPolicy != null)
				{
					message.HydrationPolicy = typeSetting.HydrationPolicy;
				}
			}
		}

		private void SetHydrationPolicy(IList<RelayMessage> messages)
		{
			TypeSetting currentTypeSetting = null;
			for (int i = 0, n = messages.Count; i < n; ++i)
			{
				var message = messages[i];

				if (message == null) continue;

				if (message.IsTwoWayMessage && !message.IsGroupBroadcastMessage && !message.IsClusterBroadcastMessage)
				{
					if (currentTypeSetting == null || currentTypeSetting.TypeId != message.TypeId)
					{
						currentTypeSetting = NodeManager.Instance.Config.TypeSettings.TypeSettingCollection[message.TypeId];
					}
					if (currentTypeSetting != null)
					{
						message.HydrationPolicy = currentTypeSetting.HydrationPolicy;
					}
				}
			}
		}

		private SimpleLinkedList<Node> PrepareMessage(RelayMessage message, bool isRetry)
		{
			if (isRetry)
			{
				message.RelayTTL++;
			}
			SimpleLinkedList<Node> nodes = NodeManager.Instance.GetNodesForMessage(message);
			message.RelayTTL--;
			SetHydrationPolicy(message);
			
			if (nodes.Count > 0 && !isRetry)
			{
				System.Net.IPAddress myAddress = NodeManager.Instance.MyIpAddress;
				if (myAddress != null)
				{
					message.AddressHistory.Add(myAddress);
				}
			}
			DebugWriter.WriteDebugInfo(message, nodes);
			return nodes;
		}

		/// <summary>
		///	Performs processing on single message
		/// </summary>
		/// <exception cref="SyncRelayOperationException">
		/// When the type of an object is defined with settings
		///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"></see> with 
		///		SyncInMessages=true and 
		///		ThrowOnSyncFailure=true
		///	failed "in" executions will throw this exception
		/// </exception>
		/// <param name="message">Message to be processed</param>
		public void HandleMessage(RelayMessage message)
		{
			
			Node node;
			if (message.IsTwoWayMessage)
			{
				int allowedRetries = NodeManager.Instance.GetRetryCountForMessage(message);
				bool triedBefore = false;
				do
				{
					if (PrepareMessage(message, triedBefore).Pop(out node))
					{
						triedBefore = true;
						node.HandleOutMessage(message);
					}
					else
					{
						message.SetError(RelayErrorType.NoNodesAvailable);
					}
				}
				while (message.ErrorType == RelayErrorType.NodeUnreachable && --allowedRetries >= 0);
			}
			else
			{
				SimpleLinkedList<Node> nodes = PrepareMessage(message, false);
				SerializedRelayMessage serializedMessage = new SerializedRelayMessage(message);
				SerializedRelayMessage serializedMessageInterZone = null;

				bool messageHandled = true; // start with "true" so that we do not pop
				// if there are no items in "nodes"
				if (nodes.Count == 0)
				{
					message.SetError(RelayErrorType.NoNodesAvailable);
				}
				else
				{
					while (nodes.Pop(out node))
					{
						TypeSetting typeSetting = NodeManager.Instance.Config.TypeSettings.TypeSettingCollection[message.TypeId];

						bool typesettingThrowOnSyncFailure = false;
						bool typesettingSyncInMessages = false;
						if (null != typeSetting && !node.NodeCluster.MeInThisCluster)
						{
							typesettingSyncInMessages = typeSetting.SyncInMessages;
							typesettingThrowOnSyncFailure = typeSetting.ThrowOnSyncFailure;
						}

						if (_myNodeDefinition != null && _myNodeDefinition.Zone != node.NodeDefinition.Zone)
						{
							// Message needs to cross Zone bounderies
							if (serializedMessageInterZone == null)
							{
								serializedMessageInterZone = new SerializedRelayMessage(RelayMessage.CreateInterZoneMessageFrom(message));
							}

							if (message.ResultOutcome == null) message.ResultOutcome = RelayOutcome.Queued;
							node.HandleInMessage(serializedMessageInterZone);
						}
						else if (typesettingSyncInMessages)
						{
							messageHandled = node.HandleInMessageSync(message, typesettingSyncInMessages, typesettingThrowOnSyncFailure);
						}
						else
						{
							if (message.ResultOutcome == null) message.ResultOutcome = RelayOutcome.Queued;
							node.HandleInMessage(serializedMessage);
						}

						if (!messageHandled)
						{
							throw new SyncRelayOperationException(string.Format("Node {0} failed to process message {1}\r\n", node, message));
						}
					}
				}
			}
		}

		/// <summary>
		///	Performs processing on a block of messages
		/// </summary>
		/// <param name="messages">A list of RealyMessage objects to be processed</param>
		/// <exception cref="SyncRelayOperationException">
		/// When the type of an object is defined with settings
		///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"></see> with 
		///		SyncInMessages=true and 
		///		ThrowOnSyncFailure=true
		///	failed executions will throw this exception
		/// </exception>
		/// <remarks>
		/// Procssing steps:
		///	1. Splits the list of messages into several lists, each containing
		///	the node, messages of a single type and settings for that type
		///	2. Processes each list individually
		///	3. Tracks the success/failure of each list
		///	4. If a list (or a message from a list) fails, and the type of message
		///	requires a "Throw" when sync fails: throws a SyncRelayOperationException, 
		///	otherwise, places failed messages into the exception queue
		/// </remarks>
		public void HandleMessages(IList<RelayMessage> messages)
		{
			SetHydrationPolicy(messages);
			NodeManager.Instance.Counters.CountMessageList(messages);
			NodeWithMessagesCollection distributedMessages = NodeManager.Instance.DistributeMessages(messages);
			NodeWithMessages nodeMessages;
			AutoResetEvent resetEvent = null;
			HandleWithCount finishedLock = null;

			List<NodeWithMessages> unhandledNodes = new List<NodeWithMessages>();

			if (_enableAsyncBulkGets && distributedMessages.Count > 1) //only do the async if there's more than 1 node to send to. No point in hitting a wait handle if we don't need to coordinate a response
			{
				int numberOfGets = 0, getsLefts = 0;
				for (int i = 0; i < distributedMessages.Count; i++)
				{
					if (distributedMessages[i].Messages.OutMessageCount > 0)
					{
						numberOfGets++;
					}
				}
				if (numberOfGets > 0)
				{
					resetEvent = OutMessageWaitHandle;
					finishedLock = new HandleWithCount(resetEvent, numberOfGets);
					getsLefts = numberOfGets; //use this to determine when we're at the last get so we use this thread to process it
				}

				for (int i = 0; i < distributedMessages.Count; i++)
				{
					nodeMessages = distributedMessages[i];
					if (nodeMessages.Messages.InMessageCount > 0)
					{
						bool inMessagesHandled = nodeMessages.NodeWithInfo.Node.HandleInMessages(nodeMessages.Messages.InMessages,
							nodeMessages.NodeWithInfo.SyncInMessages, nodeMessages.NodeWithInfo.SkipErrorQueueForSync);
						if (!inMessagesHandled)
						{
							unhandledNodes.Add(nodeMessages);
						}
					}
					if (nodeMessages.Messages.OutMessageCount > 0)
					{
						if (--getsLefts > 0)
						{
							nodeMessages.NodeWithInfo.Node.PostOutMessages(
										  new MessagesWithLock(nodeMessages.Messages.OutMessages, finishedLock));
						}
						else
						{
							nodeMessages.NodeWithInfo.Node.HandleOutMessages(nodeMessages.Messages.OutMessages);
							finishedLock.Decrement();
						}
					}
				}
				if (numberOfGets > 0)
				{
					resetEvent.WaitOne();
				}
				return;
			}
			else
			{
				for (int i = 0; i < distributedMessages.Count; i++)
				{
					nodeMessages = distributedMessages[i];
					if (nodeMessages.Messages.InMessageCount > 0)
					{
						bool inMessagesHandled = nodeMessages.NodeWithInfo.Node.HandleInMessages(nodeMessages.Messages.InMessages, nodeMessages.NodeWithInfo.SyncInMessages, nodeMessages.NodeWithInfo.SkipErrorQueueForSync);
						if (!inMessagesHandled)
						{
							unhandledNodes.Add(nodeMessages);
						}
					}
					if (nodeMessages.Messages.OutMessageCount > 0)
					{
						nodeMessages.NodeWithInfo.Node.HandleOutMessages(nodeMessages.Messages.OutMessages);
					}
				}
			}

			if (unhandledNodes.Count > 0)
			{
				bool bThrow = false;
				StringBuilder detailBuilder = new StringBuilder();
				detailBuilder.Append("Error handling sync in messages\r\n");
				foreach (NodeWithMessages nwm in unhandledNodes)
				{
					if (nwm.NodeWithInfo.SkipErrorQueueForSync && nwm.NodeWithInfo.SyncInMessages)
					{
						detailBuilder.AppendFormat("Node:{0}\r\n", nwm.NodeWithInfo.Node.GetType().Name);
						detailBuilder.AppendFormat("\tMessages: {0}\r\n", nwm.Messages.InMessageCount);
						bThrow = true;
					}
				}
				if (bThrow)
				{
					throw new SyncRelayOperationException(detailBuilder.ToString());
				}


				if (_log.IsInfoEnabled)
					_log.Info(detailBuilder.ToString());

			}
		}

		/// <summary>
		/// Called when a message would be dropped entirely from delivery. 
		/// This is most likely to occur if an error queue is full.
		/// </summary>        
		public delegate void NotifyDroppedMessage(SerializedRelayMessage relayMessage);

		/// <summary>
		/// An event for notifying clients that a message was dropped completely from delivery.
		/// This is not wrapped with a setter/defaulthandler so that functions that would call 
		/// it in a loop can avoid the loop entirely if it's null.		
		/// </summary>
		public static event NotifyDroppedMessage MessageDropped;

		/// <summary>
		/// Calls the NotifyDroppedMessage event with the supplied message.
		/// </summary>    
		public static void RaiseMessageDropped(SerializedRelayMessage message)
		{
			if (MessageDropped != null)
			{
				MessageDropped(message);
			}
		}

		/// <summary>
		/// Calls the NotifyDroppedMessage event with the supplied message.
		/// </summary>        
		public static void RaiseMessageDropped(RelayMessage message)
		{
			if (MessageDropped != null)
			{
				SerializedRelayMessage serializedMessage = new SerializedRelayMessage(message);
				MessageDropped(serializedMessage);
			}
		}

		/// <summary>
		/// Returns an Html page with information about every defined relay node.
		/// </summary>
		/// <returns></returns>
		public string GetHtmlStatus()
		{
			StringBuilder statusBuilder = new StringBuilder();
			GetHtmlStatus(statusBuilder);
			return statusBuilder.ToString();
		}

		/// <summary>
		/// Adds an HTML representation of the clients status for each service to the supplied stringbuilder.
		/// </summary>
		public void GetHtmlStatus(StringBuilder statusBuilder)
		{
			statusBuilder.Append(@"<style>");
			statusBuilder.Append(@".nodeGroupBox { BORDER-RIGHT: black thin solid; BORDER-TOP: black thin solid; FONT-SIZE: 8pt; BORDER-LEFT: black thin solid; BORDER-BOTTOM: black thin solid; BACKGROUND-COLOR: white }");
			statusBuilder.Append(@".nodeGroupTypeIDBox { BORDER-RIGHT: black thin solid; BORDER-TOP: black thin solid; FONT-SIZE: 8pt; BORDER-LEFT: black thin solid; BORDER-BOTTOM: black thin solid; BACKGROUND-COLOR: #99FF99 }");
			statusBuilder.Append(@".nodeClusterBox { BORDER-RIGHT: green thin solid; BORDER-TOP: green thin solid; FONT-SIZE: 8pt; BORDER-LEFT: green thin solid; BORDER-BOTTOM: green thin solid; BACKGROUND-COLOR: white }");
			statusBuilder.Append(@".happyServer { BORDER-RIGHT: blue thin solid; BORDER-TOP: blue thin solid; FONT-SIZE: 8pt; BORDER-LEFT: blue thin solid; BORDER-BOTTOM: blue thin solid; BACKGROUND-COLOR: #ffff99 }");
			statusBuilder.Append(@".dangerousServer { BORDER-RIGHT: blue thin solid; BORDER-TOP: blue thin solid; FONT-SIZE: 8pt; BORDER-LEFT: blue thin solid; BORDER-BOTTOM: blue thin solid; BACKGROUND-COLOR: red }");
			statusBuilder.Append(@".chosenServer { BORDER-RIGHT: blue thin solid; BORDER-TOP: blue thin solid; FONT-SIZE: 8pt; BORDER-LEFT: blue thin solid; BORDER-BOTTOM: blue thin solid; BACKGROUND-COLOR: pink }");
			statusBuilder.Append(@".inactiveServer { BORDER-RIGHT: blue thin solid; BORDER-TOP: blue thin solid; FONT-SIZE: 8pt; BORDER-LEFT: blue thin solid; BORDER-BOTTOM: blue thin solid; BACKGROUND-COLOR: gray }");
			statusBuilder.Append(@".unresolvedServer { BORDER-RIGHT: red thin solid; BORDER-TOP: red thin solid; FONT-SIZE: 8pt; BORDER-LEFT: red thin solid; BORDER-BOTTOM: red thin solid; BACKGROUND-COLOR: orange }");
			statusBuilder.Append(@"</style>");

			statusBuilder.Append("<table class=\"nodeGroupBox\">");
			statusBuilder.Append("<tr><td><b>Current Server Time:</b></td><td>" + DateTime.Now + "</td></tr>");
			statusBuilder.Append("<tr><td><b>Initialization Time:</b></td><td>" + _initDate + "</td></tr>");
			statusBuilder.Append(@"</table>" + Environment.NewLine);
			statusBuilder.Append("<br>" + Environment.NewLine);
			if (NodeManager.Instance.NodeGroups != null)
			{
				foreach (NodeGroup group in NodeManager.Instance.NodeGroups)
				{
					group.GetHtmlStatus(statusBuilder, NodeManager.Instance.Config.TypeSettings.TypeSettingCollection);
					statusBuilder.Append("<hr>" + Environment.NewLine);
				}
			}
			else
			{
				statusBuilder.Append("<strong>No Nodes Defined. Check config files!</strong>");
			}
		}
		/// <summary>
		/// Returns statistical information collected by the <see cref="Forwarder"/>.
		/// </summary>
		/// <returns>Returns the <see cref="ForwarderStatus"/> object.</returns>
		public ForwarderStatus GetForwarderStatus()
		{
			ForwarderStatus forwarderStatus = new ForwarderStatus();
			forwarderStatus.RelayStatistics = new RelayStatistics();

			forwarderStatus.RelayStatistics.CurrentServerTime = DateTime.Now;
			forwarderStatus.RelayStatistics.InitializationTime = _initDate;

			if (NodeManager.Instance.NodeGroups != null)
			{
				foreach (NodeGroup group in NodeManager.Instance.NodeGroups)
				{
					forwarderStatus.NodeGroupStatuses.Add(group.GetNodeGroupStatus(NodeManager.Instance.Config.TypeSettings.TypeSettingCollection));
				}
			}
			return forwarderStatus;
		}

		#region IReplicationComponent Members

		/// <summary>
		///  Replicates a list of messages.
		/// </summary>
		/// <param name="messages">The list of messages.</param>
		/// <returns>The number of messages replicated.</returns>
		public int Replicate(IList<RelayMessage> messages)
		{
			List<RelayMessage> replicationMessages = new List<RelayMessage>(messages.Count);
			for (int i = 0; i < messages.Count; i++)
			{
				RelayMessage replicate = DoReplicate(messages[i]);
				if (replicate != null) replicationMessages.Add(replicate);
			}
			HandleMessages(replicationMessages);
			return replicationMessages.Count;
		}

		/// <summary>
		/// Replicates a message if the message can be replicated.
		/// </summary>
		/// <param name="message">The message to replicate.</param>
		/// <returns>Returns true if replicated.</returns>
		public bool Replicate(RelayMessage message)
		{
			RelayMessage replicationMessage = DoReplicate(message);
			if (replicationMessage != null)
			{
				HandleMessage(replicationMessage);
				return true;
			}

			return false;

		}

		private static RelayMessage DoReplicate(RelayMessage message)
		{
			if (message.IsTwoWayMessage == false) return message;
			else
			{
				switch (message.MessageType)
				{
					case MessageType.SaveWithConfirm:
						return new RelayMessage(message, MessageType.Save);
					case MessageType.UpdateWithConfirm:
						return new RelayMessage(message, MessageType.Update);
					case MessageType.DeleteWithConfirm:
						return new RelayMessage(message, MessageType.Delete);
					case MessageType.DeleteAllInTypeWithConfirm:
						return new RelayMessage(message, MessageType.DeleteAllInType);
					case MessageType.NotificationWithConfirm:
						return new RelayMessage(message, MessageType.Notification);
					case MessageType.IncrementWithConfirm:
						return new RelayMessage(message, MessageType.Increment);
					case MessageType.DeleteAllWithConfirm:
						return new RelayMessage(message, MessageType.DeleteAll);
					case MessageType.DeleteInAllTypesWithConfirm:
						return new RelayMessage(message, MessageType.DeleteInAllTypes);
					default:
						return null;
				}
			}
		}

		#endregion

		#region IAsyncDataHandler Members

		/// <summary>
		/// Begins asynchronous processing of a single <see cref="T:MySpace.DataRelay.RelayMessage"/>.
		/// </summary>
		/// <param name="message">The <see cref="T:MySpace.DataRelay.RelayMessage"/>.</param>
		/// <param name="state">Callers can put any state they like here.</param>
		/// <param name="callback">The method to call upon completion.</param>
		/// <returns>
		/// Returns an <see cref="T:System.IAsyncResult"/>.
		/// </returns>
		public IAsyncResult BeginHandleMessage(RelayMessage message, object state, AsyncCallback callback)
		{
			if (!message.IsTwoWayMessage)
			{
				// cheat for now and just handle in messages synchronously
				// as long as the type doesn't use sync in messages then
				// we won't block on IO anyway.
				HandleMessage(message);
				return SynchronousAsyncResult.CreateAndComplete(callback, state);
			}

			
			SimpleLinkedList<Node> nodes = PrepareMessage(message, false);

			Node node;
			if (nodes.Pop(out node))
			{
				var result = new AsynchronousResult<Node>((ar, n, m) =>
				{
					n.EndHandleOutMessage(ar);
					int allowedRetries = NodeManager.Instance.GetRetryCountForMessage(message);
					while (message.ErrorType == RelayErrorType.NodeUnreachable && --allowedRetries >= 0)
					{
						if (PrepareMessage(message, true).Pop(out node))
						{
							node.HandleOutMessage(message);
						}
						else
						{
							message.SetError(RelayErrorType.NoNodesAvailable);
						}
					}
				}, node, message);
				var origCallback = callback;
				if (callback != null)
				{
					callback = ar =>
					{
						result.InnerResult = ar;
						origCallback(result);
					};
				}
				result.InnerResult = node.BeginHandleOutMessage(message, callback, state);
				return result;
			}
			else
			{
				message.SetError(RelayErrorType.NoNodesAvailable);
			}
			return SynchronousAsyncResult.CreateAndComplete(callback, state);
		}

		/// <summary>
		/// Begins asynchronous processing of a <see cref="T:System.Collections.Generic.List`1"/> of <see cref="T:MySpace.DataRelay.RelayMessage"/>s.
		/// </summary>
		/// <param name="messages">The list of <see cref="T:MySpace.DataRelay.RelayMessage"/>s.</param>
		/// <param name="state">Callers can put any state they like here.</param>
		/// <param name="callback">The method to call upon completion.</param>
		/// <returns>
		/// Returns an <see cref="T:System.IAsyncResult"/>.
		/// </returns>
		/// <remarks>
		///	<para>This method simply redirects to the synchronous <see cref="HandleMessages"/> method for now.
		///	In the future this may change if we have compelling use-cases for making bulk message handling asynchronous.</para>
		/// </remarks>
		IAsyncResult IAsyncDataHandler.BeginHandleMessages(IList<RelayMessage> messages, object state, AsyncCallback callback)
		{
			// cheat and handle lists synchronously for now
			HandleMessages(messages);
			return SynchronousAsyncResult.CreateAndComplete(callback, state);
		}

		/// <summary>
		/// Ends asynchronous processing of a single <see cref="T:MySpace.DataRelay.RelayMessage"/>.
		/// </summary>
		/// <param name="asyncResult">The <see cref="T:System.IAsyncResult"/> from <see cref="M:MySpace.DataRelay.IAsyncDataHandler.BeginHandleMessage(MySpace.DataRelay.RelayMessage,System.Object,System.AsyncCallback)"/></param>
		public void EndHandleMessage(IAsyncResult asyncResult)
		{
			if (asyncResult is SynchronousAsyncResult) return;

			((AsynchronousResult)asyncResult).Complete();
			Thread.MemoryBarrier();
		}

		/// <summary>
		/// Ends asynchronous processing of a <see cref="T:System.Collections.Generic.List`1"/> of <see cref="T:MySpace.DataRelay.RelayMessage"/>s.
		/// </summary>
		/// <param name="asyncResult">The <see cref="T:System.IAsyncResult"/> from <see cref="M:MySpace.DataRelay.IAsyncDataHandler.BeginHandleMessages(System.Collections.Generic.IList{MySpace.DataRelay.RelayMessage},System.Object,System.AsyncCallback)"/></param>
		void IAsyncDataHandler.EndHandleMessages(IAsyncResult asyncResult)
		{
			if (asyncResult is SynchronousAsyncResult) return;

			((AsynchronousResult)asyncResult).Complete();
			Thread.MemoryBarrier();
		}

		#endregion

		private abstract class AsynchronousResult : IAsyncResult
		{
			protected AsynchronousResult() { }

			public IAsyncResult InnerResult { get; set; }

			public abstract void Complete();

			#region IAsyncResult Members

			object IAsyncResult.AsyncState
			{
				get { return InnerResult.AsyncState; }
			}

			WaitHandle IAsyncResult.AsyncWaitHandle
			{
				get { return InnerResult.AsyncWaitHandle; }
			}

			bool IAsyncResult.CompletedSynchronously
			{
				get { return InnerResult.CompletedSynchronously; }
			}

			bool IAsyncResult.IsCompleted
			{
				get { return InnerResult.IsCompleted; }
			}

			#endregion
		}

		private delegate void EndHandleMessageAction(IAsyncResult asyncResult, Node node, RelayMessage message);

		private class AsynchronousResult<T> : AsynchronousResult
		{
			private readonly EndHandleMessageAction _completeCallback;
			private readonly Node _node;
			private readonly RelayMessage _message;

			public AsynchronousResult(EndHandleMessageAction endAction, Node node, RelayMessage message)
			{
				_completeCallback = endAction;
				_node = node;
				_message = message;
			}

			public override void Complete()
			{
				_completeCallback(InnerResult, _node, _message);
			}
		}
	}
}
