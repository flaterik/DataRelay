using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MySpace.Common;
using MySpace.Logging;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// Provides a base for sockets managed by socket pools.
	/// </summary>
	internal class ManagedSocket : Socket
	{
		private static readonly LogWrapper log = new LogWrapper();
		internal static Int32 ReplyEnvelopeLength = 4; //NOT for async receives
		private static readonly Byte[] emptyReplyBytes = {241, 216, 255, 255};
		
		private const short ServerCapabilityRequestCommandId = Int16.MinValue;
		private static readonly byte[] ServerCapabilityRequestMessage, ServerCapabilityRequestMessageNetworkOrdered;
		
		private readonly SocketPool myPool;
		internal long CreatedTicks;
		internal bool Idle;
		internal SocketError LastError = SocketError.Success;
		
		private MemoryStream _messageBuffer;
		private byte[] _receiveBuffer;
		private readonly SocketSettings _settings;
		
		private IPEndPoint _remoteEndPoint;  // a copy of remote endpoint captured during Connect so that we can log even if the socket has been disposed.
		
		internal bool ServerSupportsAck { get; private set; }

		static ManagedSocket()
		{
			MemoryStream serverCapabilityRequestStream = null, serverCapabilityRequestStreamNetworkOrdered = null;
			try
			{
				serverCapabilityRequestStream = new MemoryStream();
				serverCapabilityRequestStreamNetworkOrdered = new MemoryStream();

				SocketClient.WriteMessageToStream(ServerCapabilityRequestCommandId, 1, null, true, false,
												  serverCapabilityRequestStream);
				serverCapabilityRequestStream.Seek(0, SeekOrigin.Begin);
				ServerCapabilityRequestMessage = serverCapabilityRequestStream.ToArray();

				SocketClient.WriteMessageToStream(ServerCapabilityRequestCommandId, 1, null, true, true,
												  serverCapabilityRequestStreamNetworkOrdered);
				serverCapabilityRequestStreamNetworkOrdered.Seek(0, SeekOrigin.Begin);
				ServerCapabilityRequestMessageNetworkOrdered = serverCapabilityRequestStreamNetworkOrdered.ToArray();
			}
			catch (Exception e)
			{
				log.ErrorFormat("Error initializing server capability streams: {0}", e);
			}
			finally
			{
				if(serverCapabilityRequestStream != null)
					serverCapabilityRequestStream.Close();
				if(serverCapabilityRequestStreamNetworkOrdered != null)
					serverCapabilityRequestStreamNetworkOrdered.Close();

			}
		}
		
		internal ManagedSocket(SocketSettings settings, SocketPool socketPool)
			: base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
		{
			CreatedTicks = DateTime.UtcNow.Ticks;
			SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, settings.SendBufferSize);
			SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, settings.ReceiveBufferSize);
			SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, settings.SendTimeout);
			SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, settings.ReceiveTimeout);
			SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);
			_settings = settings;
			myPool = socketPool;
		}

		protected void ReceiveCallback(IAsyncResult state)
		{
			SocketError error = SocketError.Success;
			int received = 0;

			try
			{
				received = EndReceive(state, out error);
			}
			catch (ObjectDisposedException)
			{
				// no logging, because we expect this when sockets are closed due to timing out.
				PostError(SocketError.SocketError);
				return;
			}
			catch (SocketException sex) 
			{
				// this really shouldn't happen given which EndReceive overload was called
				log.ErrorFormat("Socket Exception {0} while calling EndReceive from {1}", sex.Message, _remoteEndPoint);
				PostError(sex.SocketErrorCode);
			}
			catch (Exception ex)
			{
				log.ErrorFormat("Exception {0} while calling EndReceive from {1}", ex, _remoteEndPoint);
				PostError(SocketError.SocketError);
			}


			if (error == SocketError.Success)
			{
				if (received == 0)
				{
					// Socket has been closed by the remote side.
					PostError(SocketError.ConnectionReset);
					return;
				}

				try
				{
					MemoryStream messageBuffer = GetMessageBuffer(_settings.MaximumReplyMessageSize);
					MemoryStream replyStream;

					const int envelopeLength = 6;
					while (received < envelopeLength) //TODO: fix this hard coded value!
					{
						received += Receive(_receiveBuffer, received, _receiveBuffer.Length - received, SocketFlags.None);
					} 

					//Now we have at least the messageSize & messageId
					int messageSize = BitConverter.ToInt32(_receiveBuffer, 0);
					short messageId = BitConverter.ToInt16(_receiveBuffer, 4);
					if (_settings.UseNetworkOrder)
					{
						messageSize = IPAddress.NetworkToHostOrder(messageSize);
						messageId = IPAddress.NetworkToHostOrder(messageId);
					}

					messageBuffer.Write(_receiveBuffer, envelopeLength, received - envelopeLength);

					int replyLength = messageSize - envelopeLength;

					// synchronously wait until the entire message has been received on the socket
					while (messageBuffer.Position < replyLength)
					{
						received = Receive(_receiveBuffer);
						messageBuffer.Write(_receiveBuffer, 0, received);
					}

					replyStream = CreateGetReplyResponse(messageBuffer, replyLength);

					// Signal any waiting thread that the receive has completed
					PostReply(messageId, replyStream);

					// A new BeginReceive() to call ReceiveCallback when the next reply message comes in.
					BeginReceive(GetReceiveBuffer(_settings.ReceiveBufferSize), 0, _settings.ReceiveBufferSize, SocketFlags.None,
								 ReceiveCallback, null);
				}
				catch (SocketException sex)
				{
					log.ErrorFormat("Socket Error while receiving on {0}: {1}", _remoteEndPoint, sex);
					PostError(sex.SocketErrorCode);
				}
				catch (ObjectDisposedException)
				{
					PostError(SocketError.SocketError);
				}
				catch (Exception ex)
				{
					log.ErrorFormat("Exception while receiving on {0}: {1}", _remoteEndPoint, ex);
					PostError(SocketError.SocketError);
				}
			}
			else
			{
				log.ErrorFormat("EndReceive from {0} failed with {1}", _remoteEndPoint, error);
				PostError(error);
			}
		}

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
			replyStream = new MemoryStream(replyLength);
			replyStream.Write(messageBuffer.GetBuffer(), 0, replyLength);
			replyStream.Seek(0, SeekOrigin.Begin);

			return replyStream;
		}

		public void Release()
		{
			myPool.ReleaseSocket(this);
		}

		private string _localEndPoint = "(unconnected)";

		public void Connect(IPEndPoint remoteEndPoint, int timeoutMilliseconds)
		{
			_remoteEndPoint = remoteEndPoint;
			IAsyncResult asyncResult = BeginConnect(remoteEndPoint, null, null);
		
			// there is no other way to set a time out on a connection other than putting a time out on the wait here and manually throwing an exception
			if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMilliseconds, false))
			{
				Close();
				throw new SocketException((int) SocketError.HostUnreachable);
			}

			EndConnect(asyncResult);

			_localEndPoint = LocalEndPoint.ToString();

			BeginReceive(GetReceiveBuffer(_settings.ReceiveBufferSize), 0, _settings.ReceiveBufferSize, SocketFlags.None,
						 ReceiveCallback, null); //start a receive immediately so there's always one running. otherwise we have no way to detect if the connection is closed

			CheckServerCapabilities();
		}

		private void CheckServerCapabilities()
		{
			if (myPool.Settings.RequestServerCapabilities && ServerCapabilityRequestMessage != null && ServerCapabilityRequestMessageNetworkOrdered != null)
			{
				byte[] message;
				if (myPool.Settings.UseNetworkOrder)
					message = ServerCapabilityRequestMessageNetworkOrdered;
				else
					message = ServerCapabilityRequestMessage;

				Send(message, message.Length, SocketFlags.None);
				
				MemoryStream capabilityReplyStream = GetReply();

				//if we have more than just "send ack" capabilities to check we can do more than just a non-null check, but for now it's sufficient
				if (capabilityReplyStream == null) 
					ServerSupportsAck = false;
				else
					ServerSupportsAck = true;
			}
			else
			{
				ServerSupportsAck = false;
			}
		}

		internal byte[] GetReceiveBuffer(int bufferSize)
		{
			if (_receiveBuffer == null || _receiveBuffer.Length != bufferSize)
			{
				_receiveBuffer = new byte[bufferSize];
			}

			return _receiveBuffer;
		}

		internal MemoryStream GetMessageBuffer(int bufferSize)
		{
			if (_messageBuffer == null)
			{
				_messageBuffer = new MemoryStream(bufferSize);
			}
			else
			{
				_messageBuffer.Seek(0, SeekOrigin.Begin);
			}
			return _messageBuffer;
		}

		#region Persocket async reply

		private short currentMessageId = 1;
		private MemoryStream _replyStream;
		private readonly EventWaitHandle _waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

		private void PostError(SocketError error)
		{
			try
			{
				_replyStream = null;
				LastError = error;
				_waitHandle.Set();
			} 
			catch(Exception ex)
			{
				log.Error(ex);
			}
		}

		private void PostReply(short messageId, MemoryStream replyStream)
		{
			try
			{
				if (messageId == currentMessageId)
				{
					_replyStream = replyStream;
				}
				else
				{
					Debug.WriteLine(String.Format("Wrong message id received. Expected {0} got {1}", currentMessageId,
												  messageId));
					_replyStream = null;
				}
				_waitHandle.Set();
			}
			catch(Exception ex)
			{
				log.Error(ex);
			}
		}

		internal MemoryStream GetReply()
		{
			if (_waitHandle.WaitOne(ReceiveTimeout, false))
			{
				var reply = _replyStream;
				_replyStream = null;
				// return a valid reply even if LastError might be set.  This error will be detected on the next request.
				if (reply != null) return reply;

				if (LastError != SocketError.Success)
				{
					log.ErrorFormat("Socket Error {0} from {1} after wait handle has been set.", LastError, _remoteEndPoint);
					throw new SocketException((int) LastError);
				}
				return reply;
			}

			if (log.IsDebugEnabled)
			{
				FrequencyBoundLogDebug(String.Format("Receive timed out {0} <- {1}", _localEndPoint, _remoteEndPoint));
			}

			if (!Connected) log.ErrorFormat("Socket timeout occurred on a disconnected socket meant for {0}", _remoteEndPoint);

			_replyStream = null;
			throw new SocketException((int) SocketError.TimedOut);
		}

		#endregion

		#region Frequncy Bound Logging
		private static Dictionary<string, ParameterlessDelegate> _errorLogDelegates = new Dictionary<string, ParameterlessDelegate>();
		private static readonly object ErrorLogSync = new object();
		private static void FrequencyBoundLogDebug(string message)
		{
			ParameterlessDelegate logDelegate;
			if (!_errorLogDelegates.TryGetValue(message, out logDelegate))
			{
				lock (ErrorLogSync)
				{
					if (!_errorLogDelegates.TryGetValue(message, out logDelegate))
					{
						var errorDelegates = new Dictionary<string, ParameterlessDelegate>(_errorLogDelegates);
						logDelegate = Algorithm.FrequencyBoundMethod(
							count => 
							{ 
								if (count > 1) log.DebugFormat("{0} occurances: {1}", count + 1, message);
								else log.Debug(message); 
							} 
							, TimeSpan.FromMinutes(1) );

						errorDelegates[message] = logDelegate;
						Interlocked.Exchange(ref _errorLogDelegates, errorDelegates);
					}
				}
			}
			logDelegate();
		}
		#endregion
	}
}