using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Ccr.Core;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Performance;
using MySpace.DataRelay.Transports;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	internal class Node
	{
		internal RelayNodeDefinition NodeDefinition;
		internal NodeGroup NodeGroup;
		internal NodeCluster NodeCluster;
		internal MessageQueue MessageErrorQueue;
		internal int HopsFromHere = -1;

		private static readonly LogWrapper _log = new LogWrapper();

		private readonly IRelayTransport _transport;
		private bool _receivesActive = true;
		private bool _repostMessageLists;
		private DispatcherQueue _inMessageQueue;
		private DispatcherQueue _outMessageQueue;

		private int _messageBurstLength = 1;
		private int _messageBurstTimeout = 500;
		private TimeSpan _messageBurstTimeoutSpan;
		private Port<List<SerializedRelayMessage>> _inMessagesPort = new Port<List<SerializedRelayMessage>>();
		private Port<SerializedRelayMessage> _serializedMessagePort = new Port<SerializedRelayMessage>();
		private Port<MessagesWithLock> _outMessagesPort = new Port<MessagesWithLock>();


		internal static string GetMessageQueueNameFor(RelayNodeDefinition nodeDefinition)
		{
			return "Relay Node " + nodeDefinition;
		}

		internal Node(RelayNodeDefinition nodeDefinition, NodeGroup ownerGroup, NodeCluster ownerCluster, ForwardingConfig forwardingConfig, DispatcherQueue inMessageQueue, DispatcherQueue outMessageQueue)
		{
			DetectedZone = 0;
			NodeDefinition = nodeDefinition;
			NodeGroup = ownerGroup;
			NodeCluster = ownerCluster;
			_messageCounts = new int[RelayMessage.NumberOfTypes];
			_lastMessageTimes = new double[RelayMessage.NumberOfTypes];
			_averageMessageTimes = new double[RelayMessage.NumberOfTypes];
			if (EndPoint != null)
			{
				_transport = TransportFactory.CreateTransportForNode(nodeDefinition, ownerGroup.GroupDefinition, forwardingConfig.MessageChunkLength);
				DetectedZone = NodeManager.Instance.GetZoneForAddress(EndPoint.Address);
				if (forwardingConfig.MapNetwork)
				{
					HopsFromHere = HowManyHopsFromHere();
				}
				else
				{
					HopsFromHere = 0;
				}
			}
			else
			{
				_transport = new NullTransport();
			}

			_inMessageQueue = inMessageQueue;
			_outMessageQueue = outMessageQueue;

			if (forwardingConfig != null)
			{
				_messageBurstLength = forwardingConfig.MessageBurstLength;
				_messageBurstTimeout = forwardingConfig.MessageBurstTimeout;
				_messageBurstTimeoutSpan = TimeSpan.FromMilliseconds(_messageBurstTimeout);
				MessageErrorQueue = new MessageQueue(ownerGroup.GetQueueConfig());
				_repostMessageLists = forwardingConfig.RepostMessageLists;
			}

			ActivateBurstReceive(1); //the burst length will be used after the first message is received

			// async, no skipping of the error queue (duh)
			Arbiter.Activate(_inMessageQueue,
				Arbiter.Receive<List<SerializedRelayMessage>>(true, _inMessagesPort,
															  messages => DoHandleInMessages(messages, false)));

			Arbiter.Activate(_outMessageQueue,
				Arbiter.Receive<MessagesWithLock>(true, _outMessagesPort,
				delegate(MessagesWithLock messages) { HandleOutMessages(messages.Messages); messages.Locker.Decrement(); }));
		}

		private static readonly byte[] _pingBuffer = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
		private int HowManyHopsFromHere()
		{
			if (EndPoint != null)
			{
				try
				{
					Ping pinger = new Ping();
					PingOptions options = new PingOptions(1, true);
					PingReply reply;
					while (options.Ttl < NodeCluster.MaximumHops)
					{
						reply = pinger.Send(EndPoint.Address, 500, _pingBuffer, options);
						if (reply != null)
						{
							if (reply.Status == IPStatus.Success)
							{
								return options.Ttl;
							}

							if (reply.Status == IPStatus.TtlExpired)
							{
								options.Ttl++;
							}
							else
							{
								if (_log.IsWarnEnabled)
									_log.WarnFormat(
										"Error Calculating Number of Hops for Node {0} ({1}): {2}. Using default of 2.",
										this,
										NodeGroup.GroupName,
										reply.Status
										);
								return 2;
							}
						}
						if (_log.IsWarnEnabled)
							_log.WarnFormat("Node {0} ({1}) could not be pinged. Using {3} as number of hops.",
								 this,
								 NodeGroup.GroupName,
								 NodeCluster.MaximumHops,
								 NodeCluster.MaximumHops);
						return NodeCluster.MaximumHops;
					}
					if (_log.IsWarnEnabled)
						_log.WarnFormat("Node {0} ({1}) is more than {2} hops away. Using {3}",
							 this,
							 NodeGroup.GroupName,
							 NodeCluster.MaximumHops,
							 NodeCluster.MaximumHops);
					return NodeCluster.MaximumHops;
				}
				catch (Exception ex)
				{
					if (_log.IsWarnEnabled)
						_log.WarnFormat("Error Calculating Number of Hops for Node {0}: {1}. Using default of 2.",
							 this, ex);
					return 2;
				}
			}
			return -1;
		}

		private void ActivateBurstReceive(int count)
		{
			try
			{
				if (_receivesActive)
				{
					if (count > 1)
					{
						Port<DateTime> timeoutPort = new Port<DateTime>();
						//post a message to the timeoutPort if the burstTimeout is exceeded without receiving enough messages
						_inMessageQueue.EnqueueTimer(_messageBurstTimeoutSpan, timeoutPort);
						// activate the Arbiter/Receive channels.
						Arbiter.Activate(
							_inMessageQueue,
							Arbiter.Choice(
							//We got enough messages for our defined burst size
								Arbiter.MultipleItemReceive<SerializedRelayMessage>(false, _serializedMessagePort, count,
								delegate(SerializedRelayMessage[] messages)
								{
									// already in a queue, no point in skipping it
									DoHandleInMessages(messages, false);
									ActivateBurstReceive(_messageBurstLength);
								}),
							//we did not get enough messages, use the TimeoutHandler
								Arbiter.Receive<DateTime>(false, timeoutPort,
									date =>
										ActivateBurstReceive(
											(_serializedMessagePort.ItemCount < _messageBurstLength)
												? _serializedMessagePort.ItemCount
												: _messageBurstLength))
							)
						);
					}
					else
					{
						Arbiter.Activate(_inMessageQueue,
							Arbiter.Receive<SerializedRelayMessage>(false, _serializedMessagePort,
								message =>
								{
									DoHandleMessage(message);
									ActivateBurstReceive(_messageBurstLength);
								}));
					}
				}
			}
			catch (Exception ex)
			{
				if (_log.IsErrorEnabled)
					_log.ErrorFormat("Error activating burst in {0}: {1}.", this, ex);
				ActivateBurstReceive(1);
			}
		}

		internal void ReloadMapping(RelayNodeDefinition relayNodeDefinition, ForwardingConfig forwardingConfig)
		{
			NodeDefinition = relayNodeDefinition;
			Interlocked.Exchange(ref _messageBurstLength, forwardingConfig.MessageBurstLength);
			Interlocked.Exchange(ref _messageBurstTimeout, forwardingConfig.MessageBurstTimeout);
			_repostMessageLists = forwardingConfig.RepostMessageLists;
			if (EndPoint != null)
			{
				DetectedZone = NodeManager.Instance.GetZoneForAddress(EndPoint.Address);
			}
			else
			{
				DetectedZone = 0;
			}
			_messageBurstTimeoutSpan = TimeSpan.FromMilliseconds(_messageBurstTimeout);

			if (MessageErrorQueue == null)
			{
				MessageErrorQueue = new MessageQueue(NodeGroup.GetQueueConfig());
			}
			else
			{
				MessageErrorQueue.ReloadConfig(NodeGroup.GetQueueConfig());
			}

			SocketTransportAdapter socketTransport = _transport as SocketTransportAdapter;

			if (socketTransport != null)
			{
				socketTransport.LoadSettings(NodeDefinition, NodeGroup.GroupDefinition);
			}

			//changes in other forwarding settings are handled by the nodemanager using the Set & Start NewDispatcher methods.		
		}

		internal void SetNewDispatcherQueues(DispatcherQueue newInDispatcherQueue, DispatcherQueue newOutDispatcherQueue)
		{
			_receivesActive = false; //stop the burst receive loop

			_inMessageQueue = newInDispatcherQueue;
			_outMessageQueue = newOutDispatcherQueue;


			//start posting messages to these new ports. when the old dispatcher is done, we'll link them to the new queue and they'll start processing
			Interlocked.Exchange<Port<SerializedRelayMessage>>(ref _serializedMessagePort, new Port<SerializedRelayMessage>());
			Interlocked.Exchange<Port<List<SerializedRelayMessage>>>(ref _inMessagesPort, new Port<List<SerializedRelayMessage>>());
			Interlocked.Exchange<Port<MessagesWithLock>>(ref _outMessagesPort, new Port<MessagesWithLock>());

			_receivesActive = true;

			Arbiter.Activate(_inMessageQueue,
					  Arbiter.Receive<List<SerializedRelayMessage>>(true, _inMessagesPort,
																	messages => DoHandleInMessages(messages, false)));

			ActivateBurstReceive(1);

			Arbiter.Activate(_outMessageQueue,
				 Arbiter.Receive<MessagesWithLock>(true, _outMessagesPort,
				 delegate(MessagesWithLock messages) { HandleOutMessages(messages.Messages); messages.Locker.Decrement(); }));
		}

		private void DoHandleMessage(SerializedRelayMessage message)
		{
			if (!Activated || message == null || message.MessageStream == null)
			{
				return;
			}

			if (DangerZone)
			{
				EnqueueMessage(message);
				return;
			}

			try
			{
				if (GatherStats)
				{
					Stopwatch watch = Stopwatch.StartNew();

					_transport.SendMessage(message);
					watch.Stop();
					CaculateStatisics(message, watch.ElapsedMilliseconds);
				}
				else
				{
					_transport.SendMessage(message);
				}
				NodeManager.Instance.Counters.CountMessage(message);
			}
			catch (Exception ex)
			{

				EnqueueMessage(message);
				InstrumentException(ex);
				NodeGroup.LogNodeException(message, this, ex);
			}
		}

		/// <summary>
		/// Processes a single message
		/// </summary>
		/// <param name="message">Message to be processed</param>
		/// <param name="useSyncForInMessages">Default: false
		/// The type (from TypeSettings.config) can require synchronous handling for messages
		/// </param>
		/// <param name="skipErrorQueueForSync">Default: false
		/// The type (from TypeSettings.config) can require that should the message processing fail,
		/// the message will NOT be sent to the Error Queue for retry.  Instead, the function returns
		/// false.
		/// Has no effect if useSyncForInMessages is false.
		/// </param>
		/// <returns>
		/// useSyncForInMessages = false	always returns True (message processed Async)
		/// useSyncForInMessages = true, skipErrorQueueForSync = false	always returns True (errors placed in queue for retry)
		/// useSyncForInMessages = true, skipErrorQueueForSync = true	returns true if the message processing succeeded
		/// </returns>
		private bool DoHandleMessage(RelayMessage message, bool useSyncForInMessages, bool skipErrorQueueForSync)
		{
			try
			{
				if (!Activated || message == null)
				{
					if (skipErrorQueueForSync && useSyncForInMessages)
					{
						return false;
					}
					return true;
				}

				if (DangerZone)
				{
					//this is only called for synchronous messages, which aren't error queued
					message.SetError(RelayErrorType.NodeInDanagerZone);
					if (skipErrorQueueForSync && useSyncForInMessages)
					{
						return false;
					}
					return true;
				}

				bool messageHandled = true;

				try
				{
					if (GatherStats)
					{
						Stopwatch watch = Stopwatch.StartNew();
						if (useSyncForInMessages)
						{
							// using the system this way allows us to continue on if the 
							// Transport does not expose IRelayTransportExtended.
							// The old handling (Transport.SendMessage) will send "put" 
							// messages one-way, preventing certain errors from being
							// reported, but this does not break any existing code
							IRelayTransportExtended TransportEx = _transport as IRelayTransportExtended;
							if (null != TransportEx)
							{
								TransportEx.SendSyncMessage(message);
							}
							else
							{
								_transport.SendMessage(message);
							}
						}
						else
						{
							_transport.SendMessage(message);
						}
						watch.Stop();
						CaculateStatisics(message, watch.ElapsedMilliseconds);
					}
					else
					{
						if (useSyncForInMessages)
						{
							// using the system this way allows us to continue on if the 
							// Transport does not expose IRelayTransportExtended.
							// The old handling (Transport.SendMessage) will send "put" 
							// messages one-way, preventing certain errors from being
							// reported, but this does not break any existing code
							IRelayTransportExtended TransportEx = _transport as IRelayTransportExtended;
							if (null != TransportEx)
							{
								TransportEx.SendSyncMessage(message);
							}
							else
							{
								_transport.SendMessage(message);
							}
						}
						else
						{
							_transport.SendMessage(message);
						}
					}
					
					if (message.ErrorOccurred)
					{
						messageHandled = false;
					}
					
					NodeManager.Instance.Counters.CountMessage(message);
				}
				catch (Exception ex)
				{
					if (skipErrorQueueForSync && useSyncForInMessages)
					{
						messageHandled = false;
					}

					//this is only called for get messages, which aren't error queued				
					InstrumentException(ex);
					message.SetError(ex);
					NodeGroup.LogNodeException(message, this, ex);
				}
				return messageHandled;
			}
			finally
			{
				if (message != null && message.ResultOutcome == null)
				{
					message.ResultOutcome = RelayOutcome.NotSent;
				}
			}
		}

		private IAsyncResult BeginDoHandleMessage(RelayMessage message, bool useSyncForInMessages, bool skipErrorQueueForSync, AsyncCallback callback, object asyncState)
		{
			var asyncTransport = _transport as IAsyncRelayTransport;

			if (asyncTransport == null) 
			{
				return NodeSynchronousAsyncResult.CreateAndComplete(DoHandleMessage(message, useSyncForInMessages, skipErrorQueueForSync), callback, asyncState);
			}

			bool alwaysHandled = !useSyncForInMessages || !skipErrorQueueForSync;

			if (!Activated || message == null)
			{
				if (message != null) message.ResultOutcome = RelayOutcome.NotSent;
				return NodeSynchronousAsyncResult.CreateAndComplete(alwaysHandled, callback, asyncState);
			}

			if (DangerZone)
			{
				//this is only called for synchronous messages, which aren't error queued
				message.SetError(RelayErrorType.NodeInDanagerZone);
				return NodeSynchronousAsyncResult.CreateAndComplete(alwaysHandled, callback, asyncState);
			}

			var watch = GatherStats ? Stopwatch.StartNew() : null;
			try
			{
				NodeManager.Instance.Counters.CountMessage(message);
				var result = new AsynchronousResult(message, useSyncForInMessages, skipErrorQueueForSync);
				message.ResultOutcome = RelayOutcome.Queued; // close enough
				result.InnerResult = asyncTransport.BeginSendMessage(message, useSyncForInMessages, asyncResult =>
				{
					if (watch != null)
					{
						watch.Stop();
						CaculateStatisics(message, watch.ElapsedMilliseconds);
					}
					if (callback != null)
					{
						result.InnerResult = asyncResult;
						callback(result);
					}
				}, asyncState);
				return result;
			}
			catch (Exception ex)
			{
				//this is only called for get messages, which aren't error queued
				InstrumentException(ex);
				message.SetError(ex);
				NodeGroup.LogNodeException(message, this, ex);

				return NodeSynchronousAsyncResult.CreateAndComplete(alwaysHandled, callback, asyncState);
			}
			finally
			{
				if (watch != null)
				{
					watch.Stop();
					CaculateStatisics(message, watch.ElapsedMilliseconds);
				}
			}
		}

		private bool EndDoHandleMessage(IAsyncResult asyncResult)
		{
			var synchResult = asyncResult as NodeSynchronousAsyncResult;
			if (synchResult != null) return synchResult.MessageHandled;

			var result = (AsynchronousResult)asyncResult;
			try
			{
				((IAsyncRelayTransport)_transport).EndSendMessage(result.InnerResult);
				return true;
			}
			catch (Exception ex)
			{
				InstrumentException(ex);
				result.Message.SetError(ex);
				NodeGroup.LogNodeException(result.Message, this, ex);
				return !result.UseSyncForInMessages || !result.SkipErrorQueueForSync;
			}
		}

		/// <summary>
		/// Processes a single message
		/// Calls DoHandleMessage if the message is to be processed synchronously
		/// Posts message to process queue otherwise
		/// </summary>
		/// <param name="message">Message to be processed</param>
		/// <param name="useSyncForInMessages">Default: false
		/// The type (from TypeSettings.config) can require synchronous handling for messages
		/// </param>
		/// <param name="skipErrorQueueForSync">Default: false
		/// The type (from TypeSettings.config) can require that should the message processing fail,
		/// the message will NOT be sent to the Error Queue for retry.  Instead, the function returns
		/// false.
		/// Has no effect if useSyncForInMessages is false.
		/// </param>
		/// <returns>
		/// useSyncForInMessages = false	always returns True (message processed Async)
		/// useSyncForInMessages = true, skipErrorQueueForSync = false	always returns True (errors placed in queue for retry)
		/// useSyncForInMessages = true, skipErrorQueueForSync = true	returns true if the message processing succeeded
		/// </returns>
		internal bool HandleInMessageSync(RelayMessage message, bool useSyncForInMessages, bool skipErrorQueueForSync)
		{
			if (useSyncForInMessages)
			{
				return DoHandleMessage(message, useSyncForInMessages, skipErrorQueueForSync);
			}

			message.ResultOutcome = RelayOutcome.Queued;
			SerializedRelayMessage serializedMessage = new SerializedRelayMessage(message);
			_serializedMessagePort.Post(serializedMessage);

			return true;
		}

		internal void HandleInMessage(SerializedRelayMessage serializedRelayMessage)
		{
			_serializedMessagePort.Post(serializedRelayMessage);
		}

		internal void HandleOutMessage(RelayMessage message)
		{
			// out messages are always sync
			// use false / false for useSyncForInMessages / skipErrorQueueForSync
			DoHandleMessage(message, false, false);
		}

		internal IAsyncResult BeginHandleOutMessage(RelayMessage message, AsyncCallback callback, object asyncState)
		{
			return BeginDoHandleMessage(message, false, false, callback, asyncState);
		}

		internal void EndHandleOutMessage(IAsyncResult asyncResult)
		{
			EndDoHandleMessage(asyncResult);
		}

		/// <summary>
		/// Processes an array of RelayMessages
		/// </summary>
		/// <param name="messages">Array of messages to be processed</param>
		/// <param name="skipErrorQueueForSync">True if synchronous messages that fail should not be 
		/// placed into the error queue for retry.
		/// </param>
		/// <returns>
		/// skipErrorQueueForSync = false	always returns True (message processed Async)
		/// skipErrorQueueForSync = true	returns true if the message processing succeeded
		/// </returns>
		internal bool DoHandleInMessages(SerializedRelayMessage[] messages, bool skipErrorQueueForSync)
		{
			if (!Activated)
			{
				return false;
			}
			if (DangerZone)
			{
				if (skipErrorQueueForSync)
				{
					return false;
				}
				EnqueueInMessages(messages);
				return true;
			}

			bool messagesHandled = true;

			try
			{
				if (GatherStats)
				{
					Stopwatch watch = Stopwatch.StartNew();
					_transport.SendInMessageList(messages);
					watch.Stop();
					CaculateInStatisics(messages, watch.ElapsedMilliseconds);
				}
				else
				{
					_transport.SendInMessageList(messages);
				}
				NodeManager.Instance.Counters.CountInMessages(messages);
			}
			catch (Exception ex)
			{
				if (!skipErrorQueueForSync)
				{
					EnqueueInMessages(messages);
				}
				else
				{
					messagesHandled = false;
				}
				InstrumentException(ex);
				NodeGroup.LogNodeInMessageException(messages, this, ex);
			}

			return messagesHandled;
		}

		/// <summary>
		/// Processes a <see cref="List{SerializedRelayMessage}"/>
		/// </summary>
		/// <remarks>
		///		This method doesn't handle sync IN messages in the way that the singular version does and
		///		may yield different results since the relay message isn't returned from the server in this case,
		///		where as the singular case it is.
		/// </remarks>
		/// <param name="messages">List of messages to be processed</param>
		/// <param name="skipErrorQueueForSync">True if synchronous messages that fail should not be 
		/// placed into the error queue for retry.
		/// </param>
		/// <returns>
		/// skipErrorQueueForSync = false	always returns true (message processed Async)
		/// skipErrorQueueForSync = true	returns true if the message processing succeeded
		/// </returns>
		internal bool DoHandleInMessages(List<SerializedRelayMessage> messages, bool skipErrorQueueForSync)
		{
			if (!Activated)
			{
				return false;
			}
			if (DangerZone)
			{
				if (skipErrorQueueForSync)
				{
					return false;
				}
				EnqueueMessages(messages);
				return true;
			}

			bool messagesHandled = true;

			try
			{
				if (GatherStats)
				{
					Stopwatch watch = Stopwatch.StartNew();
					_transport.SendInMessageList(messages);
					watch.Stop();
					CaculateInStatisics(messages, watch.ElapsedMilliseconds);
				}
				else
				{
					_transport.SendInMessageList(messages);
				}
				
				NodeManager.Instance.Counters.CountInMessages(messages);
			}
			catch (Exception ex)
			{
				if (skipErrorQueueForSync)
				{
					messagesHandled = false;
				}
				else
				{
					EnqueueMessages(messages);
				}
				InstrumentException(ex);
				NodeGroup.LogNodeInMessageException(messages, this, ex);
			}

			return messagesHandled;
		}

		internal void HandleOutMessages(List<RelayMessage> messages)
		{
			if (!Activated) return;

			if (DangerZone)
			{
				for (int i = 0; i < messages.Count; i++)
				{
					messages[i].SetError(RelayErrorType.NodeInDanagerZone);
				}
				return;
			}
			try
			{
				if (GatherStats)
				{
					Stopwatch watch = Stopwatch.StartNew();
					_transport.SendOutMessageList(messages);
					watch.Stop();
					CaculateOutStatisics(messages, watch.ElapsedMilliseconds);
				}
				else
				{
					_transport.SendOutMessageList(messages);
				}

				NodeManager.Instance.Counters.CountOutMessages(messages);
			}
			catch (Exception ex)
			{
				InstrumentException(ex);
				for (int i = 0; i < messages.Count; ++i)
				{
					messages[i].SetError(ex);
				}
				NodeGroup.LogNodeOutMessageException(messages, this, ex);
			}
		}

		/// <summary>
		/// Processes a List&lt;&gt; of RelayMessages
		/// If useSyncForInMessages == true, processes messages immediately
		/// If useSyncForInMessages == false, places messages into the message queue
		/// </summary>
		/// <param name="messages"></param>
		/// <param name="useSyncForInMessages"></param>
		/// <param name="skipErrorQueueForSync"></param>
		/// <returns>
		/// useSyncForInMessages = false	always returns True (message processed Async)
		/// useSyncForInMessages = true, skipErrorQueueForSync = false	always returns True (errors placed in queue for retry)
		/// useSyncForInMessages = true, skipErrorQueueForSync = true	returns true if the message processing succeeded
		/// </returns>
		internal bool HandleInMessages(List<SerializedRelayMessage> messages, bool useSyncForInMessages, bool skipErrorQueueForSync)
		{
			// okay, we need to make a list of items that must be run sync (it is now a type level setting)
			if (useSyncForInMessages)
			{
				// now handle the sync only
				bool syncMessagesHandled = DoHandleInMessages(messages, skipErrorQueueForSync);
				return syncMessagesHandled;
			}

			if (_repostMessageLists)
			{
				for (int i = 0; i < messages.Count; i++)
				{
					_serializedMessagePort.Post(messages[i]);
				}
			}
			else
			{
				_inMessagesPort.Post(messages);
			}

			// always retrun true for async
			return true;
		}

		internal void PostOutMessages(MessagesWithLock messagesWithLock)
		{
			_outMessagesPort.Post(messagesWithLock);
		}

		private void EnqueueMessage(SerializedRelayMessage message)
		{
			if (MessageErrorQueue != null)
			{
				MessageErrorQueue.Enqueue(message);
			}
			else
			{
				Forwarder.RaiseMessageDropped(message);
			}
		}

		private void EnqueueMessages(List<SerializedRelayMessage> messages)
		{
			if (MessageErrorQueue != null)
			{
				MessageErrorQueue.Enqueue(messages);
			}
			else
			{
				for (int i = 0; i < messages.Count; i++)
				{
					Forwarder.RaiseMessageDropped(messages[i]);
				}
			}

		}

		private void EnqueueInMessages(SerializedRelayMessage[] messages)
		{
			if (MessageErrorQueue != null)
			{
				MessageErrorQueue.Enqueue(messages);
			}
			else
			{
				for (int i = 0; i < messages.Length; i++)
				{
					Forwarder.RaiseMessageDropped(messages[i]);
				}
			}
		}

		internal void ProcessQueue()
		{
			if (!DangerZone) //if this IS in dangerzone, they'll just be requeued anyway
			{
				SerializedMessageList list = MessageErrorQueue.Dequeue();
				if (list != null)
				{
					if (_log.IsInfoEnabled)
						_log.InfoFormat("Node {0} dequeueing and processing {1} Messages",
							 this, list.InMessageCount);
					DoHandleInMessages(list.InMessages, false);
				}
			}
		}

		internal SerializedMessageList DequeueErrors()
		{
			return MessageErrorQueue.Dequeue();
		}

		#region Properties
		internal bool Activated
		{
			get
			{
				return NodeDefinition.Activated;
			}
		}

		internal bool GatherStats
		{
			get
			{
				return NodeDefinition.GatherStatistics;
			}
		}

		internal IPEndPoint EndPoint
		{
			get
			{
				return NodeDefinition.IPEndPoint;
			}
		}

		internal string Host
		{
			get
			{
				return NodeDefinition.Host;
			}
		}

		internal int Port
		{
			get
			{
				return NodeDefinition.Port;
			}
		}

		internal ushort Zone
		{
			get
			{
				return NodeDefinition.Zone;
			}
		}

		internal ushort DetectedZone { get; private set; }

		#endregion

		#region Statistics and DangerZone

		private readonly int[] _messageCounts;
		private readonly double[] _lastMessageTimes;
		private readonly double[] _averageMessageTimes;

		private int _bulkInMessageCount;
		private double _lastBulkInMessageLength;
		private double _averageBulkInMessageLength;
		private double _lastBulkInMessageTime;
		private double _averageBulkInMessageTime;

		private int _bulkOutMessageCount;
		private double _lastBulkOutMessageLength;
		private double _averageBulkOutMessageLength;
		private double _lastBulkOutMessageTime;
		private double _averageBulkOutMessageTime;

		private AggregateCounter _serverUnreachableCounter = new AggregateCounter(120); //60 seconds ticked every 500 ms
		private int _serverUnreachableErrorsLast2WaitPeriods; //the value from the aggregate counter the last time it ticked
		private const int _serverUnreachableBaseWaitSeconds = 30;
		private const int _serverUnreachableMaxWaitSeconds = 60 * 5;

		private int _serverUnreachableWaitSeconds = 60; //initial value. will be increased as errors are encountered
		private int _serverUnreachableErrors;
		private DateTime _lastServerUnreachable;

		private readonly AggregateCounter _serverDownCounter = new AggregateCounter(60); //30 seconds, ticked every 500 ms
		private int _serverDownErrorsLast30Seconds;
		private int _serverDownErrors;
		private DateTime _lastServerDownTime;

		public bool DangerZone
		{
			get
			{
				if (Unreachable)
				{
					return true;
				}

				if (_serverDownErrors > 0 &&
					_serverDownErrorsLast30Seconds > NodeGroup.GroupDefinition.DangerZoneThreshold &&
					_lastServerDownTime.AddSeconds(NodeGroup.GroupDefinition.DangerZoneSeconds) > DateTime.Now)
				{
					return true;
				}

				return false;
			}
		}

		public bool Unreachable
		{
			get
			{
				return (_serverUnreachableErrors > 0 &&
					_lastServerUnreachable.AddSeconds(_serverUnreachableWaitSeconds) > DateTime.Now);
			}
		}

		private void CaculateStatisics(SerializedRelayMessage message, long milliseconds)
		{
			int index = (int)message.MessageType;
			Interlocked.Increment(ref _messageCounts[index]);
			Interlocked.Exchange(ref _lastMessageTimes[index], milliseconds);
			Interlocked.Exchange(ref _averageMessageTimes[index], CalculateAverage(_averageMessageTimes[index], milliseconds, _messageCounts[index]));
		}

		private void CaculateStatisics(RelayMessage message, long milliseconds)
		{
			int index = (int)message.MessageType;
			Interlocked.Increment(ref _messageCounts[index]);
			Interlocked.Exchange(ref _lastMessageTimes[index], milliseconds);
			Interlocked.Exchange(ref _averageMessageTimes[index], CalculateAverage(_averageMessageTimes[index], milliseconds, _messageCounts[index]));
		}

		private void CaculateOutStatisics(ICollection<RelayMessage> messages, long milliseconds)
		{
			Interlocked.Increment(ref _bulkOutMessageCount);
			Interlocked.Exchange(ref _lastBulkOutMessageLength, (messages.Count));
			Interlocked.Exchange(ref _averageBulkOutMessageLength, CalculateAverage(_averageBulkOutMessageLength, _lastBulkOutMessageLength, _bulkOutMessageCount));
			Interlocked.Exchange(ref _lastBulkOutMessageTime, milliseconds);
			Interlocked.Exchange(ref _averageBulkOutMessageTime, CalculateAverage(_averageBulkOutMessageTime, _lastBulkOutMessageTime, _bulkOutMessageCount));
		}

		private void CaculateInStatisics(SerializedRelayMessage[] messages, long milliseconds)
		{
			Interlocked.Increment(ref _bulkInMessageCount);
			Interlocked.Exchange(ref _lastBulkInMessageLength, messages.Length);
			Interlocked.Exchange(ref _averageBulkInMessageLength, CalculateAverage(_averageBulkInMessageLength, _lastBulkInMessageLength, _bulkInMessageCount));
			Interlocked.Exchange(ref _lastBulkInMessageTime, milliseconds);
			Interlocked.Exchange(ref _averageBulkInMessageTime, CalculateAverage(_averageBulkInMessageTime, _lastBulkInMessageTime, _bulkInMessageCount));
		}

		private void CaculateInStatisics(IList<SerializedRelayMessage> messages, long milliseconds)
		{
			Interlocked.Increment(ref _bulkInMessageCount);
			Interlocked.Exchange(ref _lastBulkInMessageLength, (messages.Count));
			Interlocked.Exchange(ref _averageBulkInMessageLength, CalculateAverage(_averageBulkInMessageLength, _lastBulkInMessageLength, _bulkInMessageCount));
			Interlocked.Exchange(ref _lastBulkInMessageTime, milliseconds);
			Interlocked.Exchange(ref _averageBulkInMessageTime, CalculateAverage(_averageBulkInMessageTime, _lastBulkInMessageTime, _bulkInMessageCount));
		}

		private static double CalculateAverage(double baseLine, double newSample, double iterations)
		{
			return ((baseLine * (iterations - 1)) + newSample) / iterations;
		}

		private void InstrumentException(Exception exc)
		{
			if (exc is SocketException)
			{
				SocketError error = ((SocketException)exc).SocketErrorCode;
				if (error == SocketError.HostUnreachable
					|| error == SocketError.HostNotFound
					|| error == SocketError.ConnectionRefused
					|| error == SocketError.ConnectionReset)
				{
					IncrementServerUnreachable();
				}
				else
				{
					IncrementServerDown();
				}
			}
			else if (exc is ThreadAbortException || exc is NullReferenceException)
			{
				IncrementServerDown();
			}
		}

		internal void IncrementServerDown()
		{
			Interlocked.Increment(ref _serverDownErrors);
			_serverDownCounter.IncrementCounter();
			_lastServerDownTime = DateTime.Now;
		}

		internal void IncrementServerUnreachable()
		{
			if (_serverUnreachableErrorsLast2WaitPeriods >= 2) //if we've gotten too many in the last 2 wait period, then increase the wait time
			{
				if (_serverUnreachableWaitSeconds <= _serverUnreachableMaxWaitSeconds)
				{
					_serverUnreachableWaitSeconds = (int)(_serverUnreachableWaitSeconds * 1.5);
					_serverUnreachableCounter = new AggregateCounter(_serverUnreachableWaitSeconds * 4); //want twice the wait period, and then twice that many seconds because it's ticked every 500ms				
				}
			}
			else if (_serverUnreachableErrorsLast2WaitPeriods == 0 && _serverUnreachableWaitSeconds != _serverUnreachableBaseWaitSeconds)
			{
				//reset to base
				_serverUnreachableWaitSeconds = _serverUnreachableBaseWaitSeconds;
				_serverUnreachableCounter = new AggregateCounter(_serverUnreachableWaitSeconds * 4); //want twice the wait period, and then twice that many seconds because it's ticked every 500ms								
			}

			Interlocked.Increment(ref _serverUnreachableErrors);
			_serverUnreachableCounter.IncrementCounter();
			_lastServerUnreachable = DateTime.Now;
		}

		#endregion

		#region Descriptives

		internal void GetHtmlStatus(StringBuilder sb)
		{
			bool chosen = (NodeCluster.ChosenNode == this);
			if (_transport is NullTransport)
			{
				sb.Append("<table class=\"unresolvedServer\">");
			}
			else if (DangerZone)
			{
				sb.Append("<table class=\"dangerousServer\">");
			}
			else if (!Activated)
			{
				sb.Append("<table class=\"inactiveServer\">");
			}
			else if (chosen)
			{
				sb.Append("<table class=\"chosenServer\">");
			}
			else
			{
				sb.Append("<table class=\"happyServer\">");
			}
			sb.Append(Environment.NewLine);
			NodeGroup.AddHeaderLine(sb, Host + ":" + Port);
			if (NodeCluster.ChosenNode == this)
			{
				NodeGroup.AddHeaderLine(sb, "(Selected Node)");
			}

			NodeGroup.AddPropertyLine(sb, "Zone", Zone.ToString());
			NodeGroup.AddPropertyLine(sb, "Hops", HopsFromHere.ToString());
			NodeGroup.AddPropertyLine(sb, "DetectedZone", DetectedZone.ToString());

			int openConnections, activeConnections;
			_transport.GetConnectionStats(out openConnections, out activeConnections);
			NodeGroup.AddPropertyLine(sb, "Active/Open Connections", activeConnections + " / " + openConnections);
			NodeGroup.AddPropertyLine(sb, "Gathering Stats", GatherStats.ToString());

			if (_serverUnreachableErrors > 0)
			{
				NodeGroup.AddPropertyLine(sb, "Server unreachable errors (total)", _serverUnreachableErrors, 0);
				NodeGroup.AddPropertyLine(sb, "Server unreachable errors (last " + (2 * _serverUnreachableWaitSeconds) + "s)", _serverUnreachableErrorsLast2WaitPeriods, 0);
				NodeGroup.AddPropertyLine(sb, "Last Server Unreachable", DescribeLastUnreachable());
			}

			if (_serverDownErrors > 0)
			{
				NodeGroup.AddPropertyLine(sb, "Server downtime errors (total)", _serverDownErrors, 0);
				NodeGroup.AddPropertyLine(sb, "Server downtime errors (last 30s)", _serverDownErrorsLast30Seconds, 0);
				NodeGroup.AddPropertyLine(sb, "Last Server Downtime", DescribeLastServerDown());
			}

			if (MessageErrorQueue != null && MessageErrorQueue.InMessageQueueCount > 0)
			{
				NodeGroup.AddPropertyLine(sb, "Queued Messages", MessageErrorQueue.InMessageQueueCount, 0);
			}

			if (GatherStats)
			{
				string messageType;
				for (int i = 0; i < RelayMessage.NumberOfTypes; i++)
				{
					messageType = ((MessageType)i).ToString();
					if (_messageCounts[i] > 0)
					{
						NodeGroup.AddPropertyLine(sb, messageType + " Messages", _messageCounts[i], 0);
						NodeGroup.AddPropertyLine(sb, "Avg " + messageType + " Time (ms)", _averageMessageTimes[i], 3);
						NodeGroup.AddPropertyLine(sb, "Last " + messageType + " Time (ms)", _lastMessageTimes[i], 3);
					}
				}
				if (_bulkInMessageCount > 0)
				{
					messageType = "Bulk In";
					NodeGroup.AddPropertyLine(sb, messageType + " Messages", _bulkInMessageCount, 0);
					NodeGroup.AddPropertyLine(sb, "Avg " + messageType + " Time (ms)", _averageBulkInMessageTime, 3);
					NodeGroup.AddPropertyLine(sb, "Last " + messageType + " Time (ms)", _lastBulkInMessageTime, 3);
					NodeGroup.AddPropertyLine(sb, "Last Bulk In Message Length", _lastBulkInMessageLength, 0);
					NodeGroup.AddPropertyLine(sb, "Avg Bulk In Message Length", _averageBulkInMessageLength, 0);
				}

				if (_bulkOutMessageCount > 0)
				{
					messageType = "Bulk Out";
					NodeGroup.AddPropertyLine(sb, messageType + " Messages", _bulkOutMessageCount, 0);
					NodeGroup.AddPropertyLine(sb, "Avg " + messageType + " Time (ms)", _averageBulkOutMessageTime, 3);
					NodeGroup.AddPropertyLine(sb, "Last " + messageType + " Time (ms)", _lastBulkOutMessageTime, 3);
					NodeGroup.AddPropertyLine(sb, "Last Bulk Out Message Length", _lastBulkOutMessageLength, 0);
					NodeGroup.AddPropertyLine(sb, "Avg Bulk Out Message Length", _averageBulkOutMessageLength, 0);
				}
			}

			sb.Append("</table>" + Environment.NewLine);
		}

		internal NodeStatus GetNodeStatus()
		{
			NodeStatus nodeStatus = new NodeStatus();

			bool chosen = (NodeCluster.ChosenNode == this);
			if (_transport is NullTransport)
			{
				nodeStatus.Status = ServerStatus.unresolvedServer.ToString();
			}
			else if (DangerZone)
			{
				nodeStatus.Status = ServerStatus.dangerousServer.ToString();
			}
			else if (!Activated)
			{
				nodeStatus.Status = ServerStatus.inactiveServer.ToString();
			}
			else if (chosen)
			{
				nodeStatus.Status = ServerStatus.chosenServer.ToString();
			}
			else
			{
				nodeStatus.Status = ServerStatus.happyServer.ToString();
			}
            
			nodeStatus.Host = Host;
			nodeStatus.Port = Port;
			nodeStatus.Zone = Zone;
			nodeStatus.Hops = HopsFromHere;
			nodeStatus.DetectedZone = DetectedZone;
			int openConnections, activeConnections;
			_transport.GetConnectionStats(out openConnections, out activeConnections);
			nodeStatus.OpenConnections = openConnections;
			nodeStatus.ActiveConnections = activeConnections;
			nodeStatus.GatheringStats = GatherStats;

			if (_serverUnreachableErrors > 0)
			{
				nodeStatus.ServerUnreachableErrorInfo = new ServerUnreachableErrorInfo();
				nodeStatus.ServerUnreachableErrorInfo.Errors = _serverUnreachableErrors;
				nodeStatus.ServerUnreachableErrorInfo.WaitPeriodSeconds = 2 * _serverUnreachableWaitSeconds;
				nodeStatus.ServerUnreachableErrorInfo.ErrorsLast2WaitPeriods = _serverUnreachableErrorsLast2WaitPeriods;
				nodeStatus.ServerUnreachableErrorInfo.LastTime = _lastServerUnreachable;
				TimeSpan difference = DateTime.Now.Subtract(_lastServerUnreachable);
				nodeStatus.ServerUnreachableErrorInfo.LastTimeDescription = "(" + DescribeTimeSpan(difference) + " ago)";
			}

			if (_serverDownErrors > 0)
			{
				nodeStatus.ServerDownErrorInfo = new ServerDownErrorInfo();
				nodeStatus.ServerDownErrorInfo.Errors = _serverDownErrors;
				nodeStatus.ServerDownErrorInfo.ErrorsLast30Seconds = _serverDownErrorsLast30Seconds;
				nodeStatus.ServerDownErrorInfo.LastTime = _lastServerDownTime;
				TimeSpan difference = DateTime.Now.Subtract(_lastServerDownTime);
				nodeStatus.ServerDownErrorInfo.LastTimeDescription = "(" + DescribeTimeSpan(difference) + " ago)";
			}

			if (MessageErrorQueue != null && MessageErrorQueue.InMessageQueueCount > 0)
			{
				nodeStatus.InMessageQueueCount = MessageErrorQueue.InMessageQueueCount;
			}

			if (GatherStats)
			{
				string messageType;
				for (int i = 0; i < RelayMessage.NumberOfTypes; i++)
				{
					 messageType = ((MessageType)i).ToString();

					if (_messageCounts[i] > 0)
					{
						if (nodeStatus.MessageCounts == null)
						{
							nodeStatus.MessageCounts = new List<MessageCountInfo>();
						}
						MessageCountInfo messageCountInfo = new MessageCountInfo();
						messageCountInfo.MessageType = messageType;
						messageCountInfo.MessageCount = _messageCounts[i];
						messageCountInfo.AverageMessageTime = _averageMessageTimes[i];
						messageCountInfo.LastMessageTime = _lastMessageTimes[i];
						nodeStatus.MessageCounts.Add(messageCountInfo);
					}
				}
				if (_bulkInMessageCount > 0)
				{
					nodeStatus.BulkInMessageInfo = new BulkMessageInfo();
					nodeStatus.BulkInMessageInfo.MessageCount = _bulkInMessageCount;
					nodeStatus.BulkInMessageInfo.AverageMessageTime = _averageBulkInMessageTime;
					nodeStatus.BulkInMessageInfo.LastMessageTime = _lastBulkInMessageTime;
					nodeStatus.BulkInMessageInfo.LastMessageLength = _lastBulkInMessageLength;
					nodeStatus.BulkInMessageInfo.AverageMessageLength = _averageBulkInMessageLength;
				}

				if (_bulkOutMessageCount > 0)
				{
					nodeStatus.BulkOutMessageInfo = new BulkMessageInfo();
					nodeStatus.BulkOutMessageInfo.MessageCount = _bulkOutMessageCount;
					nodeStatus.BulkOutMessageInfo.AverageMessageTime = _averageBulkOutMessageTime;
					nodeStatus.BulkOutMessageInfo.LastMessageTime = _lastBulkOutMessageTime;
					nodeStatus.BulkOutMessageInfo.LastMessageLength = _lastBulkOutMessageLength;
					nodeStatus.BulkOutMessageInfo.AverageMessageLength = _averageBulkOutMessageLength;
				}
			}
			return nodeStatus;
		}
		private string DescribeLastServerDown()
		{
			TimeSpan difference = DateTime.Now.Subtract(_lastServerDownTime);
			return _lastServerDownTime + "<br>(" + DescribeTimeSpan(difference) + " ago)";
		}

		private string DescribeLastUnreachable()
		{
			TimeSpan difference = DateTime.Now.Subtract(_lastServerUnreachable);
			return _lastServerUnreachable + "<br>(" + DescribeTimeSpan(difference) + " ago)";
		}

		private static string DescribeTimeSpan(TimeSpan span)
		{
			if (span.TotalSeconds < 60)
			{
				return span.TotalSeconds.ToString("N0") + " seconds";
			}
			if (span.TotalMinutes < 60)
			{
				return span.TotalMinutes.ToString("N0") + " minutes";
			}
			if (span.TotalHours < 24)
			{
				return span.TotalHours.ToString("N1") + " hours";
			}
			return span.TotalDays.ToString("N1") + " days";
		}

		/// <summary>
		/// The Host + Port of this Node
		/// </summary>        
		public override string ToString()
		{
			return Host + ":" + Port;
		}

		/// <summary>
		/// The Host + Port + Group of this Node
		/// </summary>        
		public string ToExtendedString()
		{
			return Host + ":" + Port + " (" + NodeGroup.GroupName + ")";
		}
		#endregion

		internal void AggregateCounterTicker()
		{
			int count = _serverDownCounter.Tick();
			if (count != -1)
			{
				_serverDownErrorsLast30Seconds = _serverDownCounter.Tick();
			}
			else
			{
				if (_log.IsDebugEnabled)
					_log.DebugFormat("Node tried to tick its aggregate counters simultaneously!", this);
			}

			count = _serverDownCounter.Tick();
			if (count != -1)
			{
				_serverUnreachableErrorsLast2WaitPeriods = _serverUnreachableCounter.Tick();
			}
			else
			{
				if (_log.IsDebugEnabled)
					_log.DebugFormat("Node tried to tick its aggregate counters simultaneously!", this);
			}
		}

		private class AsynchronousResult : IAsyncResult
		{
			public AsynchronousResult(RelayMessage message, bool useSyncForInMessages, bool skipErrorQueueForSync)
			{
				Message = message;
				UseSyncForInMessages = useSyncForInMessages;
				SkipErrorQueueForSync = skipErrorQueueForSync;
			}

			public IAsyncResult InnerResult { get; set; }

			public RelayMessage Message { get; private set; }

			public bool UseSyncForInMessages { get; private set; }

			public bool SkipErrorQueueForSync { get; private set; }

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

		private class NodeSynchronousAsyncResult : SynchronousAsyncResult
		{
			private static readonly NodeSynchronousAsyncResult _falseWithNullState = new NodeSynchronousAsyncResult(false, null);
			private static readonly NodeSynchronousAsyncResult _trueWithNullState = new NodeSynchronousAsyncResult(true, null);

			public static NodeSynchronousAsyncResult CreateAndComplete(bool messageHandle, AsyncCallback callback, object asyncState)
			{
				NodeSynchronousAsyncResult result;
				if (asyncState == null)
				{
					result = messageHandle ? _trueWithNullState : _falseWithNullState;
				}
				else
				{
					result = new NodeSynchronousAsyncResult(messageHandle, asyncState);
				}
				if (callback != null) callback(result);
				return result;
			}

			private readonly bool _messageHandled;

			private NodeSynchronousAsyncResult(bool messageHandled, object asyncState)
				: base(asyncState)
			{
				_messageHandled = messageHandled;
			}

			public bool MessageHandled { get { return _messageHandled; } }
		}
	}
}
