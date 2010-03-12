using System;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace MySpace.SocketTransport
{
	internal delegate MemoryStream CreateSyncMessageDelegate(Int32 commandID, MemoryStream messageStream, bool useNetworkOrder);

	/// <summary>
	/// Provides a base for sockets managed by socket pools.
	/// </summary>
	internal class ManagedSocket : Socket
	{
        private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
		internal static Int32 ReplyEnvelopeLength = 4; //NOT for async receives
		internal bool Idle = false;
		internal long CreatedTicks;
		internal byte[] receiveBuffer;
		internal MemoryStream messageBuffer;

		internal SocketSettings settings;

		private ManualResetEvent connectResetEvent = new ManualResetEvent(false);
		private AsyncCallback buildSocketCallBack;
		private AsyncCallback receiveCallBack;

		private SocketPool myPool = null;

		internal SocketError LastError = SocketError.Success;

		internal ManagedSocket(SocketSettings settings, SocketPool socketPool)
			: base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
		{
			this.CreatedTicks = DateTime.UtcNow.Ticks;
			this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, settings.SendBufferSize);
			this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, settings.ReceiveBufferSize);
			this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, settings.SendTimeout);
			this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, settings.ReceiveTimeout);
			this.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);
			this.settings = settings;
			buildSocketCallBack = new AsyncCallback(BuildSocketCallBack);
			receiveCallBack = new AsyncCallback(ReceiveCallback);
			this.myPool = socketPool;
		}



		protected void ReceiveCallback(IAsyncResult state)
		{
			SocketError error = SocketError.Success;
			int received = 0;

			try
			{
				received = this.EndReceive(state, out error);
			}
			catch (ObjectDisposedException)
			{
				return;
			}
			catch (SocketException sex) //this really shouldn't happen given which EndReceive overload was called
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Socket Exception {0} while doing endReceive from {1}", sex.Message, RemoteEndPoint);
				LastError = sex.SocketErrorCode;
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception {0} while doing endReceive from {1}", ex, RemoteEndPoint);
			}

			if (error == SocketError.OperationAborted)
			{
				Debug.WriteLine("Got operation aborted on thread id " + System.Threading.Thread.CurrentThread.ManagedThreadId);
				try
				{
					BeginReceive(GetReceiveBuffer(settings.ReceiveBufferSize), 0, settings.ReceiveBufferSize, SocketFlags.None, receiveCallBack, null);
				}
				catch (ObjectDisposedException) { } //operation aborted might be caused by shutdown, if so an ODE will be thrown and we can ignore it and stop processing because the process is dead
				catch (SocketException sex)
				{
					LastError = sex.SocketErrorCode;
				}
				return;
			}
			if (error == SocketError.Success)
			{
				short messageId = 0;
				if (received == 0)
				{
					PostError(SocketError.ConnectionReset);
					return;
				}
				try
				{
					MemoryStream messageBuffer = GetMessageBuffer(settings.MaximumReplyMessageSize);
					MemoryStream replyStream = null;

					//while (received < socketMessagingProvider.ReplyEnvelopeSize)
					const int envelopeLength = 6;
					while (received < envelopeLength)//TODO: fix this hard coded value!
					{
						received += Receive(receiveBuffer, received, receiveBuffer.Length - received, SocketFlags.None);
					} //Now we have at least the messageSize & messageId
					int messageSize = BitConverter.ToInt32(receiveBuffer, 0);
					messageId = BitConverter.ToInt16(receiveBuffer, 4);
					if (settings.UseNetworkOrder)
					{
						messageSize = IPAddress.NetworkToHostOrder(messageSize);
						messageId = IPAddress.NetworkToHostOrder(messageId);
					}

					messageBuffer.Write(receiveBuffer, envelopeLength, received - envelopeLength);

					int replyLength = messageSize - envelopeLength;

					while (messageBuffer.Position < replyLength)
					{
						received = Receive(receiveBuffer);
						messageBuffer.Write(receiveBuffer, 0, received);
					}

					replyStream = CreateGetReplyResponse(messageBuffer, replyLength);
					this.BeginReceive(GetReceiveBuffer(settings.ReceiveBufferSize), 0, settings.ReceiveBufferSize, SocketFlags.None, receiveCallBack, null);

					PostReply(messageId, replyStream);
				}
				catch (SocketException sex)
				{
					LastError = sex.SocketErrorCode;
				}
				catch (ObjectDisposedException)
				{
				}
				catch (Exception ex)
				{
					if (log.IsErrorEnabled)
						log.ErrorFormat("Exception {0} while processing receive.", ex);
				}
			}
			else
			{
				PostError(error);
			}
		}

		#region Persocket async reply
		private MemoryStream replyStream;
		internal EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
		short currentMessageId = 1;

		private void PostError(SocketError error)
		{
			replyStream = null;
			LastError = error;
			waitHandle.Set();
		}

		private void PostReply(short messageId, MemoryStream replyStream)
		{
			if (messageId == currentMessageId)
			{
				this.replyStream = replyStream;
				waitHandle.Set();
			}
			else
			{
				Debug.WriteLine(String.Format("Wrong message id received. Expected {0} got {1}", currentMessageId, messageId));
				this.replyStream = null;
				waitHandle.Set();
			}
		}

		internal MemoryStream GetReply()
		{
			MemoryStream reply;
			if (waitHandle.WaitOne(this.ReceiveTimeout, false))
			{
				if (LastError != SocketError.Success) throw new SocketException((int)LastError);
				reply = replyStream;
				replyStream = null;
				return reply;
			}
			else
			{
				replyStream = null;
				throw new SocketException((int)SocketError.TimedOut);
			}
		}

		#endregion

		private static Byte[] emptyReplyBytes = { 241, 216, 255, 255 };

		internal static MemoryStream CreateGetReplyResponse(MemoryStream messageBuffer, Int32 replyLength)
		{
			MemoryStream replyStream = null;

			if (replyLength == 4) //might be "emptyReply"
			{
				Byte[] message = messageBuffer.ToArray();
				if (message[0] == emptyReplyBytes[0]
					&&
					message[1] == emptyReplyBytes[1]
					&&
					message[2] == emptyReplyBytes[2]
					&&
					message[3] == emptyReplyBytes[3]
					)
				{
					Debug.WriteLine("Empty reply received.", "SocketClient");
					return replyStream;
				}

			}

			//if we got here, it's not empty.
			messageBuffer.Seek(0, SeekOrigin.Begin);
			replyStream = new MemoryStream((int)replyLength);
			replyStream.Write(messageBuffer.GetBuffer(), 0, replyLength);
			replyStream.Seek(0, SeekOrigin.Begin);

			return replyStream;
		}

		public void Release()
		{
			myPool.ReleaseSocket(this);
		}


		public void Connect(IPEndPoint remoteEndPoint, int timeout)
		{
			ManagedConnectState state = new ManagedConnectState(this);
			this.BeginConnect(remoteEndPoint, buildSocketCallBack, state);
			if (!connectResetEvent.WaitOne(timeout, false))
			{
				connectResetEvent.Reset();
				throw new SocketException((int)SocketError.HostUnreachable);
			}
			else
			{
				if (state.exception != null)
				{
					//done this way because the CallBack is on another thread, so any exceptions it throws will not be caught by calling code
					throw state.exception;
				}
				else
				{
					connectResetEvent.Reset();
				}
			}

			this.BeginReceive(GetReceiveBuffer(settings.ReceiveBufferSize), 0, settings.ReceiveBufferSize, SocketFlags.None, receiveCallBack, null);
		}

		private void BuildSocketCallBack(IAsyncResult ar)
		{
			ManagedConnectState state = ar.AsyncState as ManagedConnectState;

			try
			{
				state.socket.EndConnect(ar);
			}
			catch (SocketException sex)
			{
				state.exception = sex;
				connectResetEvent.Set();
				return;
			}
			//Interlocked.Increment(ref socketCount);
			connectResetEvent.Set();
		}

		internal byte[] GetReceiveBuffer(int bufferSize)
		{
			if (receiveBuffer == null || receiveBuffer.Length != bufferSize)
			{
				receiveBuffer = new byte[bufferSize];
			}

			return receiveBuffer;
		}

		internal MemoryStream GetMessageBuffer(int bufferSize)
		{
			if (messageBuffer == null)
			{
				messageBuffer = new MemoryStream(bufferSize);
			}
			else
			{
				messageBuffer.Seek(0, SeekOrigin.Begin);
			}
			return messageBuffer;
		}

		~ManagedSocket()
		{
			try
			{
				if (Connected)
				{
					Shutdown(SocketShutdown.Both);
				}
				Close();
			}
			catch (SocketException)
			{ }
			catch (ObjectDisposedException)
			{ }
		}

	}
	internal class ManagedConnectState
	{
		internal ManagedConnectState(Socket socket)
		{
			this.socket = socket;
		}

		internal Socket socket;
		internal SocketException exception = null;
	}
}
