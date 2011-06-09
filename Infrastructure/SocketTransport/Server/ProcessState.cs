using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Net;
using MySpace.ResourcePool;

namespace MySpace.SocketTransport
{
	internal enum ReplyType
	{
		SendReply,
		SendAck,
		None
	}
	
	public class ProcessState
	{
		internal readonly Socket Socket;	// Client socket.
		internal readonly short CommandId;
		internal readonly short MessageId;
		internal readonly ReplyType ReplyType;
		internal ResourcePoolItem<MemoryStream> Message;
		internal readonly int MessageLength;
		internal readonly IPEndPoint RemoteEndpoint; //when there's an error, the socket loses track of it.		
		internal ResourcePoolItem<MemoryStream> ReplyBuffer; //for the reply + header

		internal ProcessState(Socket socket, short commandId, short messageId, ReplyType replyType, ResourcePoolItem<MemoryStream> message, int messageLength)
		{
			Socket = socket;
			CommandId = commandId;
			MessageId = messageId;
			ReplyType = replyType;
			Message = message;
			MessageLength = messageLength;
			RemoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
		}

	}

}
