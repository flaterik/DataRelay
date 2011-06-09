using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
		private RelayNodeDefinition _nodeDefinition;
		internal NodeGroup NodeGroup;
		internal NodeCluster NodeCluster;
		internal ErrorQueue MessageErrorQueue;

		private static readonly LogWrapper _log = new LogWrapper();

		private readonly IRelayTransport _transport;
		private bool _repostMessageLists;
		private DispatcherQueue _inMessageQueue;
		private DispatcherQueue _outMessageQueue;

		private int _messageBurstLength = 1;
		private TimeSpan _messageBurstTimeoutSpan;
		private Port<List<SerializedRelayMessage>> _inMessagesPort = new Port<List<SerializedRelayMessage>>();
		private Port<SerializedRelayMessage> _inMessagePort = new Port<SerializedRelayMessage>();
		private Port<MessagesWithLock> _outMessagesPort = new Port<MessagesWithLock>();
		private int _maxQueueLength;
		private readonly BatchedQueue<SerializedRelayMessage> _batch;

		internal string GetMessageQueueName()
		{
			return NodeCluster.GetMessageQueueNameFor(_nodeDefinition);
		}

		internal Node(RelayNodeDefinition nodeDefinition, NodeGroup ownerGroup, NodeCluster ownerCluster, ForwardingConfig forwardingConfig, DispatcherQueue inMessageQueue, DispatcherQueue outMessageQueue)
		{
			_nodeDefinition = nodeDefinition;
			NodeGroup = ownerGroup;
			NodeCluster = ownerCluster;
			_messageCounts = new int[RelayMessage.NumberOfTypes];
			_lastMessageTimes = new double[RelayMessage.NumberOfTypes];
			_averageMessageTimes = new double[RelayMessage.NumberOfTypes];

			Zone = DetermineZone(_nodeDefinition);

			if (EndPoint != null)
			{
				_transport = TransportFactory.CreateTransportForNode(nodeDefinition, ownerGroup.GroupDefinition,
				                                                     forwardingConfig.MessageChunkLength);
			}
			else
			{
				_transport = new NullTransport();
			}

			_inMessageQueue = inMessageQueue;
			_outMessageQueue = outMessageQueue;

			ExtractCommonConfigValues(forwardingConfig);

			MessageErrorQueue = new ErrorQueue(ownerGroup.GetQueueConfig(), _nodeDefinition);

			// async, no skipping of the error queue (duh)
			Arbiter.Activate(_inMessageQueue,
			                 Arbiter.Receive<List<SerializedRelayMessage>>(true, _inMessagesPort,
			                                                               messages => SendInMessages(messages, false)));
			Arbiter.Activate(_inMessageQueue,
			                 Arbiter.Receive(true, _inMessagePort, SendInMessage));

			Arbiter.Activate(_outMessageQueue,
			                 Arbiter.Receive<MessagesWithLock>(true, _outMessagesPort,
			                                                   delegate(MessagesWithLock messages)
			                                                   	{
																	BeginDoHandleOutMessages(messages.Messages, BulkAsyncEndHandleOutMessages, messages);
			                                                   	}));

			_batch = new BatchedQueue<SerializedRelayMessage>(_messageBurstLength, _messageBurstTimeoutSpan, ProcessBatch);
		}

		internal void BulkAsyncEndHandleOutMessages(IAsyncResult asyncResult)
		{
			try
			{
				EndDoHandleOutMessages(asyncResult);
			}
			finally
			{
				MessagesWithLock locker = asyncResult.AsyncState as MessagesWithLock;
				if (locker != null)
					locker.Locker.Decrement();
			}
		}
		
		private void ExtractCommonConfigValues(ForwardingConfig forwardingConfig)
		{
			if (forwardingConfig != null)
			{
				_messageBurstLength = forwardingConfig.MessageBurstLength;
				_messageBurstTimeoutSpan = TimeSpan.FromMilliseconds(forwardingConfig.MessageBurstTimeout);
				_repostMessageLists = forwardingConfig.RepostMessageLists;
			}
		}

		internal static ushort DetermineZone(RelayNodeDefinition nodeDefinition)
		{
			if (nodeDefinition == null)
				return 0;
			
			//if the node definition contains a zone, use that
			if (nodeDefinition.Zone != 0)
				return nodeDefinition.Zone;

			IPEndPoint endPoint = nodeDefinition.IPEndPoint;

			//if not, use the zone as determined by the zone definitions
			if (endPoint != null)
			{
				return NodeManager.Instance.GetZoneForAddress(endPoint.Address);									
			}

			return 0;
		}

		private void ProcessBatch(List<SerializedRelayMessage> list)
		{
			try
			{
				//we do this so it functions the way it used to, if there was only 1 item it went to the single message route.
				if (list.Count == 1)
				{
					_inMessagePort.Post(list[0]);
				}
				else 
				{
					_inMessagesPort.Post(list);
				}
			}
			catch (Exception exc)
			{
				_log.Error(exc);
			}
		}

		private void EnqueueInMessage(SerializedRelayMessage message)
		{
			_batch.Enqueue(message);
		}

		internal void ReloadMapping(RelayNodeDefinition relayNodeDefinition, ForwardingConfig forwardingConfig)
		{
			_nodeDefinition = relayNodeDefinition;
			ExtractCommonConfigValues(forwardingConfig);
			
			Zone = DetermineZone(_nodeDefinition);
			
			_batch.BatchTimeout = _messageBurstTimeoutSpan;
			_batch.BatchSize = _messageBurstLength;

			if (MessageErrorQueue == null)
			{
				MessageErrorQueue = new ErrorQueue(NodeGroup.GetQueueConfig(), _nodeDefinition);
			}
			else
			{
				MessageErrorQueue.ReloadConfig(NodeGroup.GetQueueConfig());
			}

			SocketTransportAdapter socketTransport = _transport as SocketTransportAdapter;

			if (socketTransport != null)
			{
				socketTransport.LoadSettings(_nodeDefinition, NodeGroup.GroupDefinition);
			}

			//changes in other forwarding settings are handled by the nodemanager using the Set & Start NewDispatcher methods.		
		}


		internal void SetNewDispatcherQueues(DispatcherQueue newInDispatcherQueue, DispatcherQueue newOutDispatcherQueue)
		{
			_inMessageQueue = newInDispatcherQueue;
			_outMessageQueue = newOutDispatcherQueue;

			//start posting messages to these new ports. when the old dispatcher is done, we'll link them to the new queue and they'll start processing
			Interlocked.Exchange<Port<List<SerializedRelayMessage>>>(ref _inMessagesPort, new Port<List<SerializedRelayMessage>>());
			Interlocked.Exchange<Port<MessagesWithLock>>(ref _outMessagesPort, new Port<MessagesWithLock>());
			Interlocked.Exchange(ref _inMessagePort, new Port<SerializedRelayMessage>());

			_batch.SetProcessOnce();

			Arbiter.Activate(_inMessageQueue,
					  Arbiter.Receive<List<SerializedRelayMessage>>(true, _inMessagesPort,
																	messages => SendInMessages(messages, false)));

			Arbiter.Activate(_inMessageQueue,
							 Arbiter.Receive(true, _inMessagePort, SendInMessage));

			Arbiter.Activate(_outMessageQueue,
				 Arbiter.Receive<MessagesWithLock>(true, _outMessagesPort,
				 delegate(MessagesWithLock messages) { SendOutMessages(messages.Messages); messages.Locker.Decrement(); }));

		}

		private void SendInMessage(SerializedRelayMessage message)
		{
			if (!Activated || message == null || message.MessageStream == null)
			{
				return;
			}

			if (DangerZone)
			{
				EnqueueErroredInMessage(message);
				return;
			}

			try
			{
				if (GatherStats || TypeSpecificStatisticsManager.Instance.GatherStats(message.TypeId))
				{
					Stopwatch watch = Stopwatch.StartNew();

					_transport.SendMessage(message);
					watch.Stop();
					if(GatherStats)CaculateStatisics((int)message.MessageType, watch.ElapsedMilliseconds);
					TypeSpecificStatisticsManager.Instance.CalculateStatistics(message.TypeId, watch.ElapsedMilliseconds);
				}
				else
				{
					_transport.SendMessage(message);
				}
				NodeManager.Instance.Counters.CountMessage(message);
			}
			catch (Exception ex)
			{
				EnqueueErroredInMessage(message);
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
		private bool SendMessage(RelayMessage message, bool useSyncForInMessages, bool skipErrorQueueForSync)
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
					if (GatherStats || TypeSpecificStatisticsManager.Instance.GatherStats(message.TypeId))
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
						if (GatherStats) CaculateStatisics((int)message.MessageType, watch.ElapsedMilliseconds);
						TypeSpecificStatisticsManager.Instance.CalculateStatistics(message.TypeId, watch.ElapsedMilliseconds);
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
				return NodeSynchronousAsyncResult.CreateAndComplete(SendMessage(message, useSyncForInMessages, skipErrorQueueForSync), callback, asyncState);
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
						CaculateStatisics((int)message.MessageType, watch.ElapsedMilliseconds);
						if (TypeSpecificStatisticsManager.Instance.GatherStats(message.TypeId))
						{
							TypeSpecificStatisticsManager.Instance.CalculateStatistics(message.TypeId, watch.ElapsedMilliseconds);
						}
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
				//TODO is this finally a bug? aren't the messages instrument above?
				if (watch != null)
				{
					watch.Stop();
					CaculateStatisics((int)message.MessageType, watch.ElapsedMilliseconds);
					TypeSpecificStatisticsManager.Instance.CalculateStatistics(message.TypeId, watch.ElapsedMilliseconds);
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

		private IAsyncResult BeginDoHandleOutMessages(List<RelayMessage> messages, AsyncCallback callback, object asyncState)
		{
			if (_log.IsDebugEnabled)
			{
				_log.DebugFormat("Node {0} beginning do handle out message", this);
			}
			var asyncTransport = _transport as IAsyncRelayTransport;

			if (asyncTransport == null || messages == null)
			{
				return NodeSynchronousAsyncResult.CreateAndComplete(SendOutMessages(messages), callback, asyncState);
			}

			if (!Activated)
			{
				for (int i = 0; i < messages.Count; i++)
				{
					messages[i].ResultOutcome = RelayOutcome.NotSent;
				}
				return NodeSynchronousAsyncResult.CreateAndComplete(false, callback, asyncState);
			}

			if (DangerZone)
			{
				//this is only called for synchronous messages, which aren't error queued
				for (int i = 0; i < messages.Count; i++)
				{
					messages[i].SetError(RelayErrorType.NodeInDanagerZone);
				}
				return NodeSynchronousAsyncResult.CreateAndComplete(false, callback, asyncState);
			}

			var watch = GatherStats ? Stopwatch.StartNew() : null;
			try
			{
				var result = new AsynchronousOutListResult(messages);
				for (int i = 0; i < messages.Count; i++)
				{
					messages[i].ResultOutcome = RelayOutcome.Queued; // close enough
				}
				
				result.InnerResult = asyncTransport.BeginSendMessageList(messages, asyncResult =>
				{
					if (watch != null)
					{
						_bulkOutMessageInfo.CaculateStatisics(messages.Count, watch.ElapsedMilliseconds);
						TypeSpecificStatisticsManager.Instance.CalculateBulkOutStatistics(
							messages[0].TypeId,
							messages.Count, watch.ElapsedMilliseconds);
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
				for (int i = 0; i < messages.Count; ++i)
				{
					messages[i].SetError(ex);
				}
				NodeGroup.LogNodeOutMessageException(messages, this, ex);

				return NodeSynchronousAsyncResult.CreateAndComplete(true, callback, asyncState);
			}
		}

		private bool EndDoHandleOutMessages(IAsyncResult asyncResult)
		{
			if (_log.IsDebugEnabled)
				_log.DebugFormat("Node {0} ending do handle out message", this);

			var synchResult = asyncResult as NodeSynchronousAsyncResult;
			if (synchResult != null) return synchResult.MessageHandled;

			AsynchronousOutListResult result = asyncResult as AsynchronousOutListResult;
			if (result != null)
			{
				try
				{
					((IAsyncRelayTransport) _transport).EndSendMessageList(result.InnerResult);
					return true;
				}
				catch (Exception ex)
				{
					InstrumentException(ex);
					if (result.Messages != null)
					{
						for (int i = 0; i < result.Messages.Count; i++)
						{
							result.Messages[i].SetError(ex);
						}
					}
					NodeGroup.LogNodeOutMessageException(result.Messages, this, ex);
					
					return true;
				}
			}
			else
			{
				_log.ErrorFormat("EndDoHandleMessages go null or incorrectly typed asyncResult");
				return false;
			}
		}


		/// <summary>
		/// Processes a single message
		/// Calls SendMessage if the message is to be processed synchronously
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
				return SendMessage(message, true, skipErrorQueueForSync);
			}

			message.ResultOutcome = RelayOutcome.Queued;
			SerializedRelayMessage serializedMessage = new SerializedRelayMessage(message);
			EnqueueInMessage(serializedMessage);

			return true;
		}

		internal void HandleInMessage(SerializedRelayMessage serializedRelayMessage)
		{
			EnqueueInMessage(serializedRelayMessage);
		}

		internal void HandleOutMessage(RelayMessage message)
		{
			// out messages are always sync
			// use false / false for useSyncForInMessages / skipErrorQueueForSync
			SendMessage(message, false, false);
		}

		internal IAsyncResult BeginHandleOutMessage(RelayMessage message, AsyncCallback callback, object asyncState)
		{
			return BeginDoHandleMessage(message, false, false, callback, asyncState);
		}

		internal void EndHandleOutMessage(IAsyncResult asyncResult)
		{
			EndDoHandleMessage(asyncResult);
		}

		internal IAsyncResult BeginHandleOutMessages(List<RelayMessage> messages, AsyncCallback callback, object asyncState)
		{
			return BeginDoHandleOutMessages(messages, callback, asyncState);
		}

		internal void EndHandleOutMessages(IAsyncResult asyncResult)
		{
			EndDoHandleOutMessages(asyncResult);
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
		internal bool SendInMessages(SerializedRelayMessage[] messages, bool skipErrorQueueForSync)
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
				EnqueueErroredInMessages(messages);
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
					_bulkInMessageInfo.CaculateStatisics(messages.Length, watch.ElapsedMilliseconds);
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
					EnqueueErroredInMessages(messages);
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
		internal bool SendInMessages(List<SerializedRelayMessage> messages, bool skipErrorQueueForSync)
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
				EnqueueErroredInMessages(messages);
				return true;
			}

			bool messagesHandled = true;

			try
			{
				if (GatherStats || TypeSpecificStatisticsManager.Instance.GatherStats(messages[0].TypeId))
				{
					Stopwatch watch = Stopwatch.StartNew();
					_transport.SendInMessageList(messages);
					watch.Stop();
					if(GatherStats) _bulkInMessageInfo.CaculateStatisics(messages.Count, watch.ElapsedMilliseconds);
					//In theory the BulkMessages can be heterogeneous but in practice they are homogeneous.
					//Therefore, only the first message is used to determine the TypeId
					TypeSpecificStatisticsManager.Instance.CalculateBulkInStatistics(messages[0].TypeId,
						messages.Count, watch.ElapsedMilliseconds);
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
					EnqueueErroredInMessages(messages);
				}
				InstrumentException(ex);
				NodeGroup.LogNodeInMessageException(messages, this, ex);
			}

			return messagesHandled;
		}

		internal bool SendOutMessages(List<RelayMessage> messages)
		{
			if (!Activated) return false;

			if (DangerZone)
			{
				for (int i = 0; i < messages.Count; i++)
				{
					messages[i].SetError(RelayErrorType.NodeInDanagerZone);
				}
				return false;
			}
			try
			{
				if (GatherStats || TypeSpecificStatisticsManager.Instance.GatherStats(messages[0].TypeId))
				{
					Stopwatch watch = Stopwatch.StartNew();
					_transport.SendOutMessageList(messages);
					watch.Stop();
					if(GatherStats) _bulkOutMessageInfo.CaculateStatisics(messages.Count, watch.ElapsedMilliseconds);
					TypeSpecificStatisticsManager.Instance.CalculateBulkOutStatistics(
						messages[0].TypeId,
						messages.Count, watch.ElapsedMilliseconds);
				}
				else
				{
					_transport.SendOutMessageList(messages);
				}

				NodeManager.Instance.Counters.CountOutMessages(messages);
				return true;
			}
			catch (Exception ex)
			{
				InstrumentException(ex);
				for (int i = 0; i < messages.Count; ++i)
				{
					messages[i].SetError(ex);
				}
				NodeGroup.LogNodeOutMessageException(messages, this, ex);
				return false;
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
				bool syncMessagesHandled = SendInMessages(messages, skipErrorQueueForSync);
				return syncMessagesHandled;
			}

			if (messages.Count == 0) return true;

			if (_repostMessageLists)
			{
				for (int i = 0; i < messages.Count; i++)
				{
					EnqueueInMessage(messages[i]);
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

		private void EnqueueErroredInMessage(SerializedRelayMessage message)
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

		private void EnqueueErroredInMessages(List<SerializedRelayMessage> messages)
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

		private void EnqueueErroredInMessages(SerializedRelayMessage[] messages)
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
					SendInMessages(list.InMessages, false);
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
				return _nodeDefinition.Activated;
			}
		}

		internal bool GatherStats
		{
			get
			{
				return _nodeDefinition.GatherStatistics;
			}
		}

		internal IPEndPoint EndPoint
		{
			get
			{
				return _nodeDefinition.IPEndPoint;
			}
		}

		internal string Host
		{
			get
			{
				return _nodeDefinition.Host;
			}
		}

		internal int Port
		{
			get
			{
				return _nodeDefinition.Port;
			}
		}

		internal ushort Zone { get; private set; }

		#endregion

		#region Statistics and DangerZone

		private readonly int[] _messageCounts;
		private readonly double[] _lastMessageTimes;
		private readonly double[] _averageMessageTimes;

		private readonly BulkMessageInfo _bulkOutMessageInfo = new BulkMessageInfo();
		private readonly BulkMessageInfo _bulkInMessageInfo = new BulkMessageInfo();

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

		private void CaculateStatisics(int messageType, long milliseconds)
		{
			Interlocked.Increment(ref _messageCounts[messageType]);
			Interlocked.Exchange(ref _lastMessageTimes[messageType], milliseconds);
			Interlocked.Exchange(ref _averageMessageTimes[messageType], CalculateAverage(_averageMessageTimes[messageType], milliseconds, _messageCounts[messageType]));
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
					|| error == SocketError.ConnectionReset
					|| error == SocketError.ConnectionAborted)
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
				if (_bulkInMessageInfo.MessageCount > 0)
				{
					messageType = "Bulk In";
					NodeGroup.AddPropertyLine(sb, messageType + " Messages", _bulkInMessageInfo.MessageCount, 0);
					NodeGroup.AddPropertyLine(sb, "Avg " + messageType + " Time (ms)", _bulkInMessageInfo.AverageMessageTime, 3);
					NodeGroup.AddPropertyLine(sb, "Last " + messageType + " Time (ms)", _bulkInMessageInfo.LastMessageTime, 3);
					NodeGroup.AddPropertyLine(sb, "Last Bulk In Message Length", _bulkInMessageInfo.LastMessageLength, 0);
					NodeGroup.AddPropertyLine(sb, "Avg Bulk In Message Length", _bulkInMessageInfo.AverageMessageLength, 0);
				}

				if (_bulkOutMessageInfo.MessageCount > 0)
				{
					messageType = "Bulk Out";
					NodeGroup.AddPropertyLine(sb, messageType + " Messages", _bulkOutMessageInfo.MessageCount, 0);
					NodeGroup.AddPropertyLine(sb, "Avg " + messageType + " Time (ms)", _bulkOutMessageInfo.AverageMessageTime, 3);
					NodeGroup.AddPropertyLine(sb, "Last " + messageType + " Time (ms)", _bulkOutMessageInfo.LastMessageTime, 3);
					NodeGroup.AddPropertyLine(sb, "Last Bulk Out Message Length", _bulkOutMessageInfo.LastMessageLength, 0);
					NodeGroup.AddPropertyLine(sb, "Avg Bulk Out Message Length", _bulkOutMessageInfo.AverageMessageLength, 0);
				}
			}

			sb.Append("</table>" + Environment.NewLine);
		}

        /// <summary>
        /// Gets a random Node for retry purposes from another node in the same Cluster and Zone.
        /// </summary>
        /// <param name="alreadyTried">The already tried.</param>
        /// <returns></returns>
        internal Node GetRetryNodeFromCluster(IList<Node> alreadyTried)
        {
            var random = new Random();

            var candidateNodes =
                NodeCluster
                .SelectNodesInZoneForMessage()
                .Except(alreadyTried)
                .Where(node => node.Activated && !node.DangerZone)
                .OrderBy(r => random.Next());

            return candidateNodes.FirstOrDefault();
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
						MessageCountInfo messageCountInfo = new MessageCountInfo();
						messageCountInfo.MessageType = messageType;
						messageCountInfo.MessageCount = _messageCounts[i];
						messageCountInfo.AverageMessageTime = _averageMessageTimes[i];
						messageCountInfo.LastMessageTime = _lastMessageTimes[i];
						nodeStatus.MessageCounts.Add(messageCountInfo);
					}
				}
				nodeStatus.BulkInMessageInfo = _bulkInMessageInfo.GetStatus();
				nodeStatus.BulkOutMessageInfo = _bulkOutMessageInfo.GetStatus();
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
		/// The Host + Port of this Node.
		/// </summary>        
		public override string ToString()
		{
			return Host + ":" + Port;
		}

		/// <summary>
		/// The Host + Port + Group of this Node.
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

		private class AsynchronousOutListResult : IAsyncResult
		{
			public AsynchronousOutListResult(List<RelayMessage> messages)
			{
				Messages = messages;
			}

			public IAsyncResult InnerResult { get; set; }

			public List<RelayMessage> Messages { get; private set; }

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

			public static NodeSynchronousAsyncResult CreateAndComplete(bool messageHandled, AsyncCallback callback, object asyncState)
			{
				NodeSynchronousAsyncResult result;
				if (asyncState == null)
				{
					result = messageHandled ? _trueWithNullState : _falseWithNullState;
				}
				else
				{
					result = new NodeSynchronousAsyncResult(messageHandled, asyncState);
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
