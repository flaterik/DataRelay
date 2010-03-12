using System;
using System.Collections.Generic;
using System.Text;
using MySpace.SocketTransport;
using System.IO;
using MySpace.DataRelay.SocketTransport;
using MySpace.DataRelay.Formatters;

namespace MySpace.DataRelay
{
    /// <summary>
    /// Responsible for handling the data from a new socket connection.
    /// </summary>
    internal class SocketServerRelayMessageHandler : IMessageHandler
    {
		
        private readonly IDataHandler _dataHandler;
		private readonly IRelayNode _relayNode;

        /// <summary>
        /// Initializes the current instance with a contained <see cref="RelayNode"/>
        /// </summary>
		/// <param name="dataHandler">The data handler.</param>
		/// <param name="relayNode">The relay node.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="dataHandler"/> is null
		/// or when <paramref name="relayNode"/> is null.</exception>
		public SocketServerRelayMessageHandler(IDataHandler dataHandler, IRelayNode relayNode)
		{
			if (dataHandler == null) throw new ArgumentNullException("dataHandler");
			if (relayNode == null) throw new ArgumentNullException("relayNode");
			_dataHandler = dataHandler;
			_relayNode = relayNode;
        }

        #region IMessageHandler Members

        /// <summary>
        /// Handles a new message from a <see cref="MemoryStream"/> and if appropriate translates
        /// and passes the message to the contained <see cref="RelayNode"/>.
        /// </summary>
        /// <param name="commandID"></param>
        /// <param name="messageStream"></param>
        /// <param name="messageLength"></param>
        /// <returns></returns>
        public MemoryStream HandleMessage(int commandID, MemoryStream messageStream, int messageLength)
        {
            SocketCommand command = SocketCommand.Unknown;
            RelayMessage message = null;
            List<RelayMessage> messages = null;
            MemoryStream replyStream = null;

            try
            {
                command = (SocketCommand)commandID;
            }
            catch
            {
                if(RelayNode.log.IsErrorEnabled)
                    RelayNode.log.ErrorFormat("Unrecognized commandID {0} sent to Relay Service via socket transport", commandID);
            }

            Stream reply;

            switch (command)
            {
                case SocketCommand.Unknown:
                    if(RelayNode.log.IsErrorEnabled)
                        RelayNode.log.Error("SocketCommand.Unknown received");                    
                    break;
                case SocketCommand.HandleOneWayMessage:
                    message = RelayMessageFormatter.ReadRelayMessage(messageStream);
					_dataHandler.HandleMessage(message);
                    break;
                case SocketCommand.HandleSyncMessage:
                    message = RelayMessageFormatter.ReadRelayMessage(messageStream);
					message.ResultOutcome = RelayOutcome.Received;
					_dataHandler.HandleMessage(message);
                    reply = RelayMessageFormatter.WriteRelayMessage(message);
                    if (reply != null && reply != Stream.Null)
                    {
                        replyStream = (MemoryStream)reply;
                    }
                    break;
                case SocketCommand.HandleOneWayMessages:
                    messages = RelayMessageFormatter.ReadRelayMessageList(messageStream);
					_dataHandler.HandleMessages(messages);
                    break;
                case SocketCommand.HandleSyncMessages:
					messages = RelayMessageFormatter.ReadRelayMessageList(messageStream, msg => msg.ResultOutcome = RelayOutcome.Received);
					_dataHandler.HandleMessages(messages);
                    reply = RelayMessageFormatter.WriteRelayMessageList(messages);
                    if (reply != null && reply != Stream.Null)
                    {
                        replyStream = (MemoryStream)reply;
                    }
                    break;
                case SocketCommand.GetRuntimeInfo:
                    ComponentRuntimeInfo[] runtimeInfo = _relayNode.GetComponentsRuntimeInfo();
                    reply = RelayMessageFormatter.WriteRuntimeInfo(runtimeInfo);
                    if (reply != null && reply != Stream.Null)
                    {
                        replyStream = (MemoryStream)reply;
                    }
                    break;
                default:
                    if (RelayNode.log.IsErrorEnabled)
                        RelayNode.log.ErrorFormat("Unhandled command {0} sent to Relay Service via socket transport", command);
                    break;
            }


            return replyStream;

        }

        #endregion
    }	
}
