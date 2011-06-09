using System;
using System.Collections.Generic;
using MySpace.DataRelay.Formatters;
using MySpace.DataRelay.SocketTransport;
using MySpace.Logging;
using MySpace.SocketTransport;
using System.IO;
using Microsoft.Ccr.Core;
using System.Threading;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Provides and implementation for <see cref="IAsyncMessageHandler"/> to handle socket messages
	/// asynchronously.
	/// </summary>
	internal class SocketServerAsyncMessageHandler : IAsyncMessageHandler
	{
		private static readonly LogWrapper log = new LogWrapper();
		private readonly IAsyncDataHandler _dataHandler;
		private readonly IRelayNode _relayNode;

		/// <summary>
		/// Initializes a new instance of the <see cref="SocketServerAsyncMessageHandler"/> class.
		/// </summary>
		/// <param name="dataHandler">The data handler.</param>
		/// <param name="relayNode">The relay node.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="dataHandler"/> is null
		/// or when <paramref name="relayNode"/> is null.</exception>
		public SocketServerAsyncMessageHandler(IAsyncDataHandler dataHandler, IRelayNode relayNode)
		{
			if (dataHandler == null) throw new ArgumentNullException("dataHandler");
			if (relayNode == null) throw new ArgumentNullException("relayNode");
			_dataHandler = dataHandler;
			_relayNode = relayNode;
		}

		#region IAsyncMessageHandler Members

		/// <summary>
		/// Begins the handling of a complete message from a stream. All references 
		/// to <see cref="MessageState.Message"/> must be released by the end of this method.
		/// </summary>
		/// <param name="message">The complete message that is to be handled.</param>
		/// <param name="callback">The delegate to call when complete.</param>
		/// <returns>Returns an <see cref="IAsyncResult"/>.</returns>
		/// <remarks>
		///		<para>
		///		All implementors must release any references to <see cref="MessageState.Message"/>
		///		by the time that <see cref="BeginHandleMessage"/> returns.
		///		</para>
		/// </remarks>
		public IAsyncResult BeginHandleMessage(MessageState message, AsyncCallback callback)
		{
			SocketHandlerAsyncResult result = new SocketHandlerAsyncResult(null, callback);
			//don't use callback directly, use result.Complete();
			callback = null;
			const bool wasSyncronous = true;
			//VERY IMPORTANT! don't hold any references to message or it's properties after leaving this method
			try
			{
				SocketCommand command = SocketCommand.Unknown;
				RelayMessage relayMessage = null;
				List<RelayMessage> relayMessages = null;

                try
                {
                    command = (SocketCommand)message.CommandId;
                }
                catch
                {
                    if (RelayNode.log.IsErrorEnabled)
                        RelayNode.log.ErrorFormat("Unrecognized commandID {0} sent to Relay Service via socket transport", message.CommandId);
                    result.CompleteOperation(wasSyncronous);
                    return result;
                }

				switch (command)
				{
					case SocketCommand.Unknown:
                        if (RelayNode.log.IsErrorEnabled)
                            RelayNode.log.Error("SocketCommand.Unknown received");
						result.CompleteOperation(wasSyncronous);
						return result;						
					case SocketCommand.HandleOneWayMessage:
					case SocketCommand.HandleSyncMessage:
						relayMessage = RelayMessageFormatter.ReadRelayMessage(message.Message);
						relayMessage.ResultOutcome = RelayOutcome.Received;
						_dataHandler.BeginHandleMessage(relayMessage, null, async => {
							try
							{
								_dataHandler.EndHandleMessage(async);
								if (command == SocketCommand.HandleSyncMessage)
								{
									result.ReplyMessage = relayMessage;
								}
							}
							catch (Exception exc)
							{
								result.Exception = exc;
							}
							finally
							{
								result.CompleteOperation(async.CompletedSynchronously);
							}
						});
						break;
					case SocketCommand.HandleOneWayMessages:
					case SocketCommand.HandleSyncMessages:
						relayMessages = RelayMessageFormatter.ReadRelayMessageList(message.Message, msg => msg.ResultOutcome = RelayOutcome.Received);
						_dataHandler.BeginHandleMessages(relayMessages, null, async =>
							{
								try
								{
									_dataHandler.EndHandleMessages(async);
									if (command == SocketCommand.HandleSyncMessages)
									{
										result.ReplyMessages = relayMessages;
									}
								}
								catch (Exception exc)
								{
									result.Exception = exc;
								}
								finally
								{
									result.CompleteOperation(async.CompletedSynchronously);
								}
							});
						break;
					case SocketCommand.GetRuntimeInfo:
						_enqueueGetComponentRuntimeInfo(result);
						break;
					default:
                        if (RelayNode.log.IsErrorEnabled)
                            RelayNode.log.ErrorFormat("Unhandled command {0} sent to Relay Service via socket transport", command);
						result.CompleteOperation(wasSyncronous);
						return result;
				}
			}
			catch (Exception exc)
			{
				result.Exception = exc;
				result.CompleteOperation(wasSyncronous);
			}
			
			return result;
		}

		private void _enqueueGetComponentRuntimeInfo(object state)
		{
			SocketHandlerAsyncResult asyncResult = (SocketHandlerAsyncResult)state;

			try
			{
				asyncResult.RuntimeInfo = _relayNode.GetComponentsRuntimeInfo();
			}
			catch (Exception exc)
			{
				asyncResult.Exception = exc;
			}
			const bool wasSynchronous = false;
			asyncResult.CompleteOperation(wasSynchronous);
		}

		/// <summary>
		/// Ends the handling of a message and returns the memory stream to send back to the client.
		/// </summary>
		/// <param name="asyncResult">The <see cref="IAsyncResult"/> returned from <see cref="BeginHandleMessage"/>.</param>
		/// <returns>Retuns a <see cref="MemoryStream"/> to send back to the client, if the stream is <see langword="null"/> an empty response is sent.</returns>
		public MemoryStream EndHandleMessage(IAsyncResult asyncResult)
		{
			MemoryStream replyStream = null;
			SocketHandlerAsyncResult socketAsyncResult = (SocketHandlerAsyncResult)asyncResult;

			if (socketAsyncResult.Exception != null)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception handling async message: {0}", socketAsyncResult.Exception);                
				throw socketAsyncResult.Exception;
			}

			//only 1 should be not null
			int replyCount = 0;
			if (socketAsyncResult.ReplyMessage != null) replyCount++;
			if (socketAsyncResult.ReplyMessages != null) replyCount++;
			if (socketAsyncResult.RuntimeInfo != null) replyCount++;
			if (replyCount > 1)
			{
				throw new InvalidOperationException(
					string.Format("Only 1 reply at a time is supported. ReplyMessage: {0}, ReplyMessages: {1}, RuntimeInfo: {2}",
					socketAsyncResult.ReplyMessage,
					socketAsyncResult.ReplyMessages,
					socketAsyncResult.RuntimeInfo));
			}
			
			if (socketAsyncResult.ReplyMessage != null)
			{
				replyStream = RelayMessageFormatter.WriteRelayMessage(socketAsyncResult.ReplyMessage);
			}
			else if (socketAsyncResult.ReplyMessages != null)
			{
				replyStream = RelayMessageFormatter.WriteRelayMessageList(socketAsyncResult.ReplyMessages);
			}
			else if (socketAsyncResult.RuntimeInfo != null)
			{
				replyStream = (MemoryStream)RelayMessageFormatter.WriteRuntimeInfo(socketAsyncResult.RuntimeInfo);
			}

			return replyStream;
		}

		#endregion

		#region IMessageHandler Members

		/// <summary>
		/// Not supported. 
		/// </summary>
		/// <param name="commandID"></param>
		/// <param name="messageStream"></param>
		/// <param name="messageLength"></param>
		/// <returns></returns>
		public MemoryStream HandleMessage(int commandID, System.IO.MemoryStream messageStream, int messageLength)
		{
			throw new NotSupportedException();
		}

		#endregion
	}
}
