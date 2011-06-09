using System;
using System.Collections.Generic;
using System.IO;
using MySpace.Common;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Formatters;
using MySpace.DataRelay.SocketTransport;
using MySpace.Logging;
using MySpace.ResourcePool;
using MySpace.SocketTransport;

namespace MySpace.DataRelay.Transports
{
	public class SocketTransportAdapter : IAsyncRelayTransport, IRelayTransportExtended
	{
		private static readonly MemoryStreamPool bufferPool = new MemoryStreamPool(1024);
		private static readonly LogWrapper log = new LogWrapper();
		private readonly int _defaultChunkLength;
		private readonly RelayNodeDefinition _node;
		private SocketClient _socketClient;
		private AsyncSocketClient _asyncSocketClient;
		private MySpace.SocketTransport.SocketSettings _settings;

		public SocketTransportAdapter(RelayNodeDefinition node,
			RelayNodeGroupDefinition group, int chunkLength)
		{
			_node = node;
			_defaultChunkLength = chunkLength;
			LoadSettings(node, group);
		}

		public void LoadSettings(RelayNodeDefinition node, RelayNodeGroupDefinition group)
		{
			MySpace.SocketTransport.SocketSettings newSettings = BuildSettings(group.SocketSettings);
			if (newSettings.SameAs(_settings)) return;
			IDisposable disposableObject = null;
			try
			{
				_settings = newSettings;
				_socketClient = new SocketClient(node.IPEndPoint, _settings);
				disposableObject = _asyncSocketClient;
				_asyncSocketClient = new AsyncSocketClient(node.IPEndPoint, new SocketPoolConfig
				{
					ReceiveTimeout = _settings.ReceiveTimeout,
					NetworkOrdered = _settings.UseNetworkOrder,
					ConnectTimeout = _settings.ConnectTimeout,
					LoanCapacity = _settings.PoolSize
				});
			}
			finally
			{
				if (disposableObject != null)
				{
					try
					{
						disposableObject.Dispose();
					}
					catch (Exception ex)
					{
						log.Error("Failed to dispose AsyncSocketClient", ex);
					}
				}
			}
		}

		public void GetConnectionStats(out int openConnections, out int activeConnections)
		{
			_socketClient.GetSocketCounts(_node.IPEndPoint, _settings, out openConnections, out activeConnections);
		}

		private static MySpace.SocketTransport.SocketSettings BuildSettings(Common.Schemas.SocketSettings socketSettings)
		{
			MySpace.SocketTransport.SocketSettings settings = SocketClient.GetDefaultSettings();
			if (socketSettings != null)
			{
				if (socketSettings.ConnectTimeout > 0)
				{
					settings.ConnectTimeout = socketSettings.ConnectTimeout;
				}
				if (socketSettings.MaximumMessageSize > 0)
				{
					settings.MaximumReplyMessageSize = socketSettings.MaximumMessageSize;
				}
				if (socketSettings.ReceiveBufferSize > 0)
				{
					settings.ReceiveBufferSize = socketSettings.ReceiveBufferSize;
				}
				if (socketSettings.ReceiveTimeout > 0)
				{
					settings.ReceiveTimeout = socketSettings.ReceiveTimeout;
				}
				if (socketSettings.SendBufferSize > 0)
				{
					settings.SendBufferSize = socketSettings.SendBufferSize;
				}
				if (socketSettings.SendTimeout > 0)
				{
					settings.SendTimeout = socketSettings.SendTimeout;
				}
				if (socketSettings.PoolSize > 0)
				{
					settings.PoolSize = socketSettings.PoolSize;
				}
			}

			return settings;
		}

		public void SendMessage(SerializedRelayMessage message)
		{
			if (message.IsTwoWayMessage)
			{
				throw new ApplicationException("Cannot send pre-serialized out message.");
			}

			_socketClient.SendOneWay((int)SocketCommand.HandleOneWayMessage, message.MessageStream);
		}

		public void SendMessage(RelayMessage message)
		{
			if (message.IsTwoWayMessage)
			{
				MemoryStream replyStream;
				ResourcePoolItem<MemoryStream> bufferItem = null;
				try
				{
					bufferItem = bufferPool.GetItem();
					RelayMessageFormatter.WriteRelayMessage(message, bufferItem.Item);
					bufferItem.Item.Seek(0, SeekOrigin.Begin);
					replyStream = _socketClient.SendSync(
						(int)SocketCommand.HandleSyncMessage,
						bufferItem.Item);
				}
				finally
				{
					if (bufferItem != null) bufferPool.ReleaseItem(bufferItem);
				}
				if (replyStream != null)
				{
					RelayMessage replyMessage = RelayMessageFormatter.ReadRelayMessage(replyStream);
					message.ExtractResponse(replyMessage);
				}
				//this doesn't make any sense, the incoming message already 
				//has error occured with no respones? fwise 5/09
				else if (message.ErrorOccurred)
				{
					message.Payload = null;
				}
			}
			else
			{
				ResourcePoolItem<MemoryStream> bufferItem = null;
				try
				{
					bufferItem = bufferPool.GetItem();
					RelayMessageFormatter.WriteRelayMessage(message, bufferItem.Item);
					bufferItem.Item.Seek(0, SeekOrigin.Begin);
					_socketClient.SendOneWay(
						(int)SocketCommand.HandleOneWayMessage,
						bufferItem.Item);
				}
				finally
				{
					if (bufferItem != null) bufferPool.ReleaseItem(bufferItem);
				}
			}
		}

		public void SendInMessageList(SerializedRelayMessage[] messages)
		{
			ResourcePoolItem<MemoryStream> pooledBuffer;
			MemoryStream nextMessageChunk;
			int chunkLength = _defaultChunkLength;
			int cursor = 0;
			if (messages.Length > 0)
			{
				if (chunkLength == 0 || chunkLength > messages.Length)
				{
					//0 means "don't chunk"
					chunkLength = messages.Length;
				}

				pooledBuffer = bufferPool.GetItem();
				try
				{
					nextMessageChunk = pooledBuffer.Item;
					byte[] lengthBytes = BitConverter.GetBytes(chunkLength);
					while (cursor < messages.Length)
					{
						//make sure that the next chunk doesn't go past the end of the list
						if ((cursor + chunkLength) > messages.Length)
						{
							chunkLength = messages.Length - cursor;
							BitConverterEx.WriteBytes(lengthBytes, 0, chunkLength);
						}

						nextMessageChunk.Write(lengthBytes, 0, 4);

						for (int end = cursor + chunkLength; cursor < end; cursor++)
						{
							messages[cursor].MessageStream.WriteTo(nextMessageChunk);
						}

						_socketClient.SendOneWay((int)SocketCommand.HandleOneWayMessages, nextMessageChunk);
						nextMessageChunk.Seek(0, SeekOrigin.Begin);
						nextMessageChunk.SetLength(0);
					}
				}
				finally
				{
					bufferPool.ReleaseItem(pooledBuffer);
				}
			}
		}

		public void SendInMessageList(List<SerializedRelayMessage> messages)
		{
			ResourcePoolItem<MemoryStream> pooledBuffer;
			MemoryStream nextMessageChunk;
			int chunkLength = _defaultChunkLength;
			int cursor = 0;
			if (messages.Count > 0)
			{
				if (chunkLength == 0 || chunkLength > messages.Count)
				{
					//0 means "don't chunk"
					chunkLength = messages.Count;
				}

				pooledBuffer = bufferPool.GetItem();
				try
				{
					nextMessageChunk = pooledBuffer.Item;
					byte[] lengthBytes = BitConverter.GetBytes(chunkLength);
					while (cursor < messages.Count)
					{
						//make sure that the next chunk doesn't go past the end of the list
						if ((cursor + chunkLength) > messages.Count)
						{
							chunkLength = messages.Count - cursor;
							BitConverterEx.WriteBytes(lengthBytes, 0, chunkLength);
						}

						nextMessageChunk.Write(lengthBytes, 0, 4);

						for (int end = cursor + chunkLength; cursor < end; cursor++)
						{
							messages[cursor].MessageStream.WriteTo(nextMessageChunk);
						}

						_socketClient.SendOneWay((int)SocketCommand.HandleOneWayMessages, nextMessageChunk);
						nextMessageChunk.Seek(0, SeekOrigin.Begin);
						nextMessageChunk.SetLength(0);
					}
				}
				finally
				{
					bufferPool.ReleaseItem(pooledBuffer);
				}
			}
		}

		public void SendOutMessageList(List<RelayMessage> messages)
		{
			ResourcePoolItem<MemoryStream> pooledBuffer;
			MemoryStream nextMessageChunk;
			int chunkLength;// = _defaultChunkLength;
			if (messages.Count > 0)
			{
				chunkLength = messages.Count; //chunking is... problematic... for out messages
				pooledBuffer = bufferPool.GetItem();
				try
				{
					nextMessageChunk = pooledBuffer.Item;
					int cursor = 0;
					while (cursor < messages.Count)
					{
						int currentLocalIndexStart = cursor;
						nextMessageChunk.Seek(0, SeekOrigin.Begin);
						cursor += RelayMessageFormatter.WriteRelayMessageList(messages, cursor, chunkLength, nextMessageChunk);
						MemoryStream replyStream = _socketClient.SendSync((int)SocketCommand.HandleSyncMessages, nextMessageChunk);
						if (replyStream != null)
						{
							List<RelayMessage> replyMessages = RelayMessageFormatter.ReadRelayMessageList(replyStream);
							if (replyMessages.Count != messages.Count)
							{
								string logMsg = string.Format("Reply messages from {0} has length {1} but request messages has length {2}. Discarding replies.",
									 _node, replyMessages.Count, messages.Count);
								log.Error(logMsg);
#if DEBUG
								throw new ApplicationException(logMsg);
#else
								break;
#endif
							}
							for (int i = 0; i < replyMessages.Count; i++)
							{
								try
								{
									if (replyMessages[i].Id != messages[i + currentLocalIndexStart].Id)
									{
										string logMsg = string.Format("OutMessage Receive Got Wrong Id on Reply Message. Message Sent: {0} Message Received: {1}",
											 messages[i + currentLocalIndexStart],
											 replyMessages[i]);
										log.Error(logMsg);
#if DEBUG
										throw new ApplicationException(logMsg);
#endif
									}
									else
									{
										messages[i + currentLocalIndexStart].ExtractResponse(replyMessages[i]);
									}
								}
								catch (ArgumentOutOfRangeException)
								{
									string errMsg = string.Format("Bad index while processing out message list for {0}. i = {1}. currentLocalIndexStart = {2}. Cursor = {3}. Message count = {4}.",
										 _node, i, currentLocalIndexStart, cursor, messages.Count);
									log.Error(errMsg);
									cursor = messages.Count + 1; //break out of while loop as well
#if DEBUG
									throw new ArgumentOutOfRangeException(errMsg);
#else
									break;
#endif
								}
							}
						}
					}
				}
				finally
				{
					bufferPool.ReleaseItem(pooledBuffer);
				}
			}
		}

		#region IRelayTransportExtended Members

		/// <summary>
		/// Sends a message synchronously, regardless of its type
		/// </summary>
		/// <param name="message"></param>
		/// <remarks>
		/// added cbrown
		/// due to the extensive use of the sockettransport, and the desire not
		/// to break existing code, this interface is being used to extend the 
		/// transport protocol.
		/// usage:
		///		IRelayTransportExtended xTend = Transport as IRelayTransportExtended;
		///		if (null == xTend) 
		///		{
		///			use "tradidional" handling
		///		}
		///		else
		///		{
		///			use extended handling
		///		}
		/// </remarks>
		public void SendSyncMessage(RelayMessage message)
		{
			MemoryStream replyStream;
			ResourcePoolItem<MemoryStream> bufferItem = null;
			try
			{
				bufferItem = bufferPool.GetItem();
				// there is not need to check the type, we are FORCING sync handling
				RelayMessageFormatter.WriteRelayMessage(message, bufferItem.Item);
				bufferItem.Item.Seek(0, SeekOrigin.Begin);
				replyStream = _socketClient.SendSync((int)SocketCommand.HandleSyncMessage, bufferItem.Item);
			}
			finally
			{
				if (bufferItem != null) bufferPool.ReleaseItem(bufferItem);
			}
			if (replyStream != null)
			{
				RelayMessage replyMessage = RelayMessageFormatter.ReadRelayMessage(replyStream);
				message.ExtractResponse(replyMessage);
			}
			//this doesn't make any sense, the incoming message already 
			//has error occured with no respones? fwise 5/09
			else if (message.ErrorOccurred)
			{
				message.Payload = null;
			}
		}

		/// <summary>
		/// Sends a list of messages synchronously, regardless of its type
		/// </summary>
		/// <param name="messages">The messages to send.</param>
		/// <remarks>
		/// added cbrown
		/// due to the extensive use of the sockettransport, and the desire not
		/// to break existing code, this interface is being used to extend the 
		/// transport protocol.
		/// usage:
		///		IRelayTransportExtended xTend = Transport as IRelayTransportExtended;
		///		if (null == xTend) 
		///		{
		///			use "tradidional" handling
		///		}
		///		else
		///		{
		///			use extended handling
		///		}
		/// </remarks>		
		public void SendSyncMessageList(List<RelayMessage> messages)
		{
			ResourcePoolItem<MemoryStream> pooledBuffer;
			MemoryStream nextMessageChunk;
			int chunkLength = _defaultChunkLength;
			if (messages.Count > 0)
			{
				chunkLength = messages.Count;
				pooledBuffer = bufferPool.GetItem();
				try
				{
					nextMessageChunk = pooledBuffer.Item;
					int cursor = 0;
					while (cursor < messages.Count)
					{
						int currentLocalIndexStart = cursor;
						nextMessageChunk.Seek(0, SeekOrigin.Begin);
						cursor += RelayMessageFormatter.WriteRelayMessageList(messages, cursor, chunkLength, nextMessageChunk);
						MemoryStream replyStream = _socketClient.SendSync((int)SocketCommand.HandleSyncMessages, nextMessageChunk);
						if (replyStream != null)
						{
							List<RelayMessage> replyMessages = RelayMessageFormatter.ReadRelayMessageList(replyStream);
							if (replyMessages.Count != messages.Count)
							{
								string errMsg = string.Format("Reply messages from {0} has length {1} but request messages has length {2}. Discarding replies.",
									 _node, replyMessages.Count, messages.Count);
								log.Error(errMsg);
#if DEBUG
								throw new ApplicationException(errMsg);
#else
								break;
#endif
							}
							for (int i = 0; i < replyMessages.Count; i++)
							{
								try
								{
									if (replyMessages[i].Id != messages[i + currentLocalIndexStart].Id)
									{
										string errMsg = string.Format("OutMessage Receive Got Wrong Id on Reply Message. Message Sent: {0}, Message Received: {1}",
											 messages[i + currentLocalIndexStart],
											 replyMessages[i]);
										log.Error(errMsg);
#if DEBUG
										throw new ApplicationException(errMsg);
#endif
									}
									else
									{
										messages[i + currentLocalIndexStart].ExtractResponse(replyMessages[i]);
									}
								}
								catch (ArgumentOutOfRangeException)
								{
									string errMsg = string.Format("Bad index while processing out message list for {0}. i = {1}. currentLocalIndexStart = {2}. Cursor = {3}. Message count = {4}.",
											 _node, i, currentLocalIndexStart, cursor, messages.Count);
									if (log.IsErrorEnabled) log.Error(errMsg);
									cursor = messages.Count + 1; //break out of while loop as well
#if DEBUG
									throw new ArgumentOutOfRangeException(errMsg);
#else
									break;
#endif
								}
							}
						}
					}
				}
				finally
				{
					bufferPool.ReleaseItem(pooledBuffer);
				}
			}
		}

		#endregion

		#region IAsyncRelayTransport Members

		IAsyncResult IAsyncRelayTransport.BeginSendMessage(SerializedRelayMessage message, AsyncCallback callback, object state)
		{
			if (message == null) throw new ArgumentNullException("message");

			if (message.IsTwoWayMessage)
			{
				throw new ApplicationException("Cannot send pre-serialized out message.");
			}

			var result = new SimpleAsyncResult(callback, state);
			_asyncSocketClient.SendOneWayAsync<MemoryStream>(
				(short)SocketCommand.HandleOneWayMessage,
				message.MessageStream,
				(messageStream, stream) => stream.Write(
					messageStream.GetBuffer(),
					(int)messageStream.Position,
					(int)messageStream.Length),
					args =>
					{
						result.Error = args.Error;
						result.CompleteOperation(args.CompletedSynchronously);
					});
			return result;
		}

		IAsyncResult IAsyncRelayTransport.BeginSendMessage(RelayMessage message, bool forceRoundTrip, AsyncCallback callback, object state)
		{
			if (message == null) throw new ArgumentNullException("message");

			if (message.IsTwoWayMessage)
			{
				var result = new RoundTripAsyncResult<RelayMessage>(callback, state)
				{
					SentMessage = message
				};
				_asyncSocketClient.SendRoundTripAsync<RelayMessage>(
					(short)SocketCommand.HandleSyncMessage,
					message,
					RelayMessageFormatter.WriteRelayMessage,
					args =>
					{
						try
						{
							if (args.Error != null)
							{
								result.Error = args.Error;
								return;
							}

							if (args.Response != null)
							{
								result.ResponseMessage = RelayMessageFormatter.ReadRelayMessage(args.Response);
							}
						}
						catch (Exception ex)
						{
							result.Error = ex;
						}
						finally
						{
							result.Complete(args.CompletedSynchronously);
						}
					});
				return result;
			}
			else
			{
				var result = new SimpleAsyncResult(callback, state);
				if (forceRoundTrip)
				{
					_asyncSocketClient.SendRoundTripAsync<RelayMessage>(
						(short)SocketCommand.HandleSyncMessage,
						message,
						RelayMessageFormatter.WriteRelayMessage,
						args =>
						{
							result.Error = args.Error;
							result.CompleteOperation(args.CompletedSynchronously);
						});
				}
				else
				{
					_asyncSocketClient.SendOneWayAsync<RelayMessage>(
							(short)SocketCommand.HandleOneWayMessage,
							message,
							RelayMessageFormatter.WriteRelayMessage,
							args =>
							{
								result.Error = args.Error;
								result.CompleteOperation(args.CompletedSynchronously);
							});
				}
				return result;
			}
		}

		void IAsyncRelayTransport.EndSendMessage(IAsyncResult result)
		{
			if (result == null) throw new ArgumentNullException("result");

			if (result is RoundTripAsyncResult<RelayMessage>)
			{
				if (!result.IsCompleted)
				{
					result.AsyncWaitHandle.WaitOne();
					result.AsyncWaitHandle.Close();
				}

				var roundTripResult = (RoundTripAsyncResult<RelayMessage>)result;

				if (roundTripResult.Error != null) throw roundTripResult.Error;

				if (roundTripResult.ResponseMessage != null)
				{
					roundTripResult.SentMessage.ExtractResponse(roundTripResult.ResponseMessage);
				}
			}
			else if (result is SimpleAsyncResult)
			{
				if (!result.IsCompleted)
				{
					result.AsyncWaitHandle.WaitOne();
					result.AsyncWaitHandle.Close();
				}

				var simpleResult = (SimpleAsyncResult)result;

				if (simpleResult.Error != null) throw simpleResult.Error;
			}
			else
			{
				throw new ArgumentException("result did not come from one of the correct SendMessage overloads.", "result");
			}
		}

		void IAsyncRelayTransport.EndSendInMessageList(IAsyncResult result)
		{
			throw new NotSupportedException();
		}

		IAsyncResult IAsyncRelayTransport.BeginSendInMessageList(List<SerializedRelayMessage> messages, AsyncCallback callback, object state)
		{
			throw new NotSupportedException();
		}

		IAsyncResult IAsyncRelayTransport.BeginSendInMessageList(SerializedRelayMessage[] messages, AsyncCallback callback, object state)
		{
			throw new NotSupportedException();
		}

		IAsyncResult IAsyncRelayTransport.BeginSendMessageList(List<RelayMessage> messages, AsyncCallback callback, object state)
		{
			if (messages == null) throw new ArgumentNullException("messages");

			var result = new RoundTripAsyncResult<List<RelayMessage>>(callback, state)
			{
				SentMessage = messages
			};

			if (messages.Count > 0)
			{
				
				_asyncSocketClient.SendRoundTripAsync<List<RelayMessage>>(
					(short) SocketCommand.HandleSyncMessages,
					messages,
					RelayMessageFormatter.WriteRelayMessageList,
					args =>
						{
							try
							{
								if (args.Error != null)
								{
									result.Error = args.Error;
									return;
								}

								if (args.Response != null)
								{
									result.ResponseMessage = RelayMessageFormatter.ReadRelayMessageList(args.Response);
								}
							}
							catch (Exception ex)
							{
								result.Error = ex;
							}
							finally
							{
								result.Complete(args.CompletedSynchronously);
							}
						});
			}
			else
			{
				result.ResponseMessage = messages;
				result.Complete(true);
			}

			return result;
		}
		
		void IAsyncRelayTransport.EndSendMessageList(IAsyncResult result)
		{
			if (result == null) throw new ArgumentNullException("result");

			if (result is RoundTripAsyncResult<List<RelayMessage>>)
			{
				if (!result.IsCompleted)
				{
					result.AsyncWaitHandle.WaitOne();
					result.AsyncWaitHandle.Close();
				}

				var roundTripResult = (RoundTripAsyncResult<List<RelayMessage>>)result;

				if (roundTripResult.Error != null) throw roundTripResult.Error;

				if (roundTripResult.ResponseMessage != null && roundTripResult.SentMessage != null && roundTripResult.ResponseMessage.Count == roundTripResult.SentMessage.Count)
				{
					for (int i = 0; i < roundTripResult.SentMessage.Count; i++)
					{
						if(roundTripResult.SentMessage[i] != null && roundTripResult.ResponseMessage[i] != null)
							roundTripResult.SentMessage[i].ExtractResponse(roundTripResult.ResponseMessage[i]);	

					}
				}
				else if (roundTripResult.ResponseMessage == null)
				{
					log.ErrorFormat("Response for async bulk out messages to node {0} was null", _node);
				}
				else if (roundTripResult.SentMessage == null)
				{
					log.ErrorFormat("List of async out messages sent to {0} was null", _node);
				}
				else //message count mismatch
				{
					log.ErrorFormat("Sent {0} out messages to node {1} but only got {2} back", roundTripResult.SentMessage.Count, _node, roundTripResult.ResponseMessage.Count);
				}
			}
			else if (result is SimpleAsyncResult)
			{
				if (!result.IsCompleted)
				{
					result.AsyncWaitHandle.WaitOne();
					result.AsyncWaitHandle.Close();
				}

				var simpleResult = (SimpleAsyncResult)result;

				if (simpleResult.Error != null) throw simpleResult.Error;
			}
			else
			{
				throw new ArgumentException("result did not come from one of the correct SendMessage overloads.", "result");
			}
		}

		#endregion
	}
}
