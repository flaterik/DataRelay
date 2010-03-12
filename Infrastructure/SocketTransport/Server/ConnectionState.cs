using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Net;

namespace MySpace.SocketTransport
{
	public class ConnectionState : IDisposable
	{
		//public Socket workSocket = null;	// Client socket.

		internal IPEndPoint remoteEndPoint = null;
		private Socket workSocket = null;
		public Socket WorkSocket
		{
			get
			{
				return workSocket;
			}
			set
			{
				workSocket = value;
				if (workSocket != null)
				{
					remoteEndPoint = (IPEndPoint)workSocket.RemoteEndPoint;
				}
			}
		}

		private Socket replySocket = null;
		public Socket ReplySocket
		{
			get
			{
				if (replySocket == null)
				{
					return workSocket;
				}
				else
				{
					return replySocket;
				}
			}
			set
			{
				replySocket = value;
			}
		}
		
		private int bufferSize = 0;
		private int messageBufferInitialSize = 0;

		public int BufferSize
		{
			get
			{
				return bufferSize;
			}
		}

		public int MessageBufferInitialSize
		{
			get
			{
				return messageBufferInitialSize;
			}
		}

		public byte[] networkBuffer;// Receive buffer.

		public int messageSize = -1;
		public int messagePosition = 0;

		public MemoryStream messageBuffer;

		public ConnectionState(int bufferSize, int initialMessageSize)
		{
			this.bufferSize = bufferSize;
			this.messageBufferInitialSize = initialMessageSize;

			networkBuffer = new byte[this.bufferSize];

			this.messageBuffer = new MemoryStream(MessageBufferInitialSize);
		}

		public ConnectionState()
		{
			this.bufferSize = 1024;
			this.messageBufferInitialSize = 20480;

			networkBuffer = new byte[this.bufferSize];

			this.messageBuffer = new MemoryStream(MessageBufferInitialSize);
		}


		#region IDisposable Members
		private bool disposed = false;
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					if (messageBuffer != null)
					{
						messageBuffer.Dispose();
					}
					try
					{
						if (workSocket != null)
						{
							if (workSocket.Connected)
							{
								workSocket.Shutdown(SocketShutdown.Both);
							}
							workSocket.Close();
						}						
					}
					catch{}
					try
					{
						if (replySocket != null)
						{
							if (replySocket.Connected)
							{
								replySocket.Shutdown(SocketShutdown.Both);
							}
							replySocket.Close();
						}
					}
					catch { }
				}
			}
			disposed = true;
		}

		#endregion
	}
}
