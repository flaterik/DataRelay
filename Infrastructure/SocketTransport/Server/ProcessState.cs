using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Net;
using MySpace.ResourcePool;

namespace MySpace.SocketTransport
{
	public class ProcessState
	{
		internal Socket socket = null;	// Client socket.
		internal bool Idle = false;
		internal short commandId;
		internal short messageId;
		internal bool sendReply;
		internal ResourcePoolItem<MemoryStream> message;
		internal int messageLength;
		internal IPEndPoint remoteEndpoint; //when there's an error, the socket loses track of it.		
		internal ResourcePoolItem<MemoryStream> replyBuffer; //for the reply + header

		internal ProcessState(Socket socket, short commandId, short messageId, bool sendReply, ResourcePoolItem<MemoryStream> message, int messageLength)
		{
			this.socket = socket;
			this.commandId = commandId;
			this.messageId = messageId;
			this.sendReply = sendReply;
			this.message = message;
			this.messageLength = messageLength;
			this.remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
		}

	}

}
