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
	/// 	<para>Encapsulates a socket connection to a specified end point.</para>
	/// </summary>
	internal class SocketChannel : IDisposable
	{
		private enum OperationType
		{
			None,
			OneWay,
			RoundTrip
		}

		private enum State
		{
			Uninitialized,
			Connecting,
			Connected,
			Disposed
		}

		private const int _receiveBufferSize = 8 << 10;

		private static readonly ObjectDisposedException _disposedException = new ObjectDisposedException(typeof(SocketChannel).Name);

		private static readonly LogWrapper _log = new LogWrapper();
		private static int _nextRequestId;

		private readonly EndPoint _endpoint;
		private readonly int _connectTimeout;
		private readonly Socket _socket = new Socket(
				AddressFamily.InterNetwork,
				SocketType.Stream,
				ProtocolType.Tcp)
		{
			Blocking = false
		};

		private readonly ServerMessageHeader _responseHeader = new ServerMessageHeader();
		private readonly SocketAsyncEventArgs _socketArgs;
		private readonly SocketAsyncEventArgs _receiveArgs;
		private readonly WaitCallback _timeoutHandler;
		private volatile Exception _error;
		private ITaskHandle _timeoutHandle;
		private State _state = State.Uninitialized;
		private bool _messageSent;
		private OperationType _operationType = OperationType.None;
		private IPoolItem<MemoryStream> _sendData;
		private IPoolItem<MemoryStream> _responseData;
		private bool _responseReceived;
		private Action<OneWayAsyncEventArgs> _oneWayResultAction;
		private Action<RoundTripAsyncEventArgs> _roundTripResultAction;
		private int _currentRequestId;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="SocketChannel"/> class.</para>
		/// </summary>
		/// <param name="endpoint">The endpoint to connect to.</param>
		/// <param name="connectTimeout">
		///	<para>The time, in milliseconds, to wait for a connection before timing out.</para>
		///  </param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="endpoint"/> is <see langword="null"/>.</para>
		/// </exception>
		public SocketChannel(EndPoint endpoint, int connectTimeout)
		{
			if (endpoint == null) throw new ArgumentNullException("endpoint");
			if (connectTimeout < -1) throw new ArgumentOutOfRangeException("connectTimeout", "connectTimeout must be a positive integer, 0, or Timeout.Infinite (-1).");

			_endpoint = endpoint;
			_connectTimeout = connectTimeout;

			EventHandler<SocketAsyncEventArgs> completed = (s, e) => ((ParameterlessDelegate)e.UserToken)();
			_socketArgs = new SocketAsyncEventArgs { RemoteEndPoint = endpoint };
			_socketArgs.Completed += completed;
			_receiveArgs = new SocketAsyncEventArgs { RemoteEndPoint = endpoint };
			_receiveArgs.Completed += completed;
			_receiveArgs.SetBuffer(new byte[_receiveBufferSize], 0, _receiveBufferSize);
			_timeoutHandler = HandleTimeout;
		}

		/// <summary>
		///	<para>Sends the data in <paramref name="sendData"/> to the remote end point
		///	specified during construction. Calls <paramref name="resultAction"/> when all data
		///	has been sent. Please note that this only indicates that the server received the data
		///	and does not guarantee that data was processed.</para>
		/// </summary>
		/// <param name="timeout">The time to wait, in milliseconds, before the operation times out.</param>
		/// <param name="sendData">
		///	<para>The data to send. The pool item will be disposed, returned to the owning pool,
		///	automatically when it is no longer needed. So it is important that consumers do not
		///	access it after calling this method.</para>
		/// </param>
		/// <param name="resultAction">
		///	<para>The method that will be called when the host acknowledges that the data was
		///	received, the operation fails, or the operation times out.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="sendData"/> is <see langword="null"/>.</para>
		/// </exception>
		public void SendOneWayAsync(
			int timeout,
			IPoolItem<MemoryStream> sendData,
			Action<OneWayAsyncEventArgs> resultAction)
		{
			if (sendData == null) throw new ArgumentNullException("sendData");

			_currentRequestId = Interlocked.Increment(ref _nextRequestId);

			var task = new AsyncSocketTask(this);

			task.SetEnumerator(GetSendEnumerator(
				timeout,
				sendData,
				OperationType.OneWay,
				resultAction,
				task.Callback));

			task.Execute(true);
		}

		/// <summary>
		///	<para>Sends the data in <paramref name="sendData"/> to <see cref="EndPoint"/>.
		///	Calls <paramref name="resultAction"/> when response data has been recieved,
		///	the operation times-out, or the operation fails.</para>
		/// </summary>
		/// <param name="timeout">The time to wait, in milliseconds, before the operation times out.</param>
		/// <param name="sendData">
		///	<para>The data to send. The pool item will be disposed, returned to the owning pool,
		///	automatically when it is no longer needed. So it is important that consumers do not
		///	access it after calling this method.</para>
		/// </param>
		/// <param name="resultAction">
		///	<para>The method that will be called when the end point responds,
		///	the operation fails, or the operation times out.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="sendData"/> is <see langword="null"/>.</para>
		/// </exception>
		public void SendRoundTripAsync(
			int timeout,
			IPoolItem<MemoryStream> sendData,
			Action<RoundTripAsyncEventArgs> resultAction)
		{
			if (sendData == null) throw new ArgumentNullException("sendData");

			_currentRequestId = Interlocked.Increment(ref _nextRequestId);

			var task = new AsyncSocketTask(this);

			task.SetEnumerator(GetSendEnumerator(
				timeout,
				sendData,
				OperationType.RoundTrip,
				resultAction,
				task.Callback));

			task.Execute(true);
		}

		/// <summary>
		/// 	<para>Gets the end point this instance is connected or will connect to.</para>
		/// </summary>
		/// <value>
		/// 	<para>The end point this instance is connected or will connect to.</para>
		/// </value>
		public EndPoint EndPoint
		{
			[DebuggerStepThrough]
			get { return _endpoint; }
		}

		/// <summary>
		/// 	<para>Gets a value indicating whether this instance has an error.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if this instance has error; otherwise, <see langword="false"/>.</para>
		/// </value>
		public bool HasError
		{
			[DebuggerStepThrough]
			get { return _error != null; }
		}

		private void HandleTimeout(object state)
		{
			ICompletion completion = null;
			try
			{
				lock (_socket)
				{
					SetError(SocketError.TimedOut);
					if (IsOperationComplete)
					{
						completion = CompleteOperation(false);
					}
				}
			}
			finally
			{
				if (completion != null) completion.Complete();
			}
		}

		private void ValidateSocketForUse()
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("SocketChannel");
			}

			if (_error != null)
			{
				throw new InvalidOperationException("SocketConnection was left in a bad state by a previous error.", _error);
			}

			if (_operationType != OperationType.None)
			{
				throw new InvalidOperationException("SocketConnection is already running a pending operation.");
			}
		}

		private ICompletion CompleteOperation(bool wasSynchronous)
		{
			Debug.Assert(_operationType != OperationType.None, "CompleteOperation called when _operationType was None.");

			try
			{
				if (_timeoutHandle != null && !_timeoutHandle.TrySetComplete())
				{
					SetError(SocketError.TimedOut);
				}
				_timeoutHandle = null;

				if (_operationType == OperationType.OneWay && _oneWayResultAction != null)
				{
					return OneWayAsyncEventArgs.Create(wasSynchronous, _error, _oneWayResultAction);
				}

				if (_operationType == OperationType.RoundTrip && _roundTripResultAction != null)
				{
					if (_error == null)
					{
						var result = RoundTripAsyncEventArgs.Create(wasSynchronous, null, _responseData, _roundTripResultAction);
						// don't want _responseData to be returned to the pool
						// before the consumer has a chance to read it. It will
						// be disposed when result.Complete() is called.
						_responseData = null;
						return result;
					}
					else
					{
						return RoundTripAsyncEventArgs.Create(wasSynchronous, _error, null, _roundTripResultAction);
					}
				}
				return null;
			}
			finally
			{
				Reset();
			}
		}

		private void Reset()
		{
			_operationType = OperationType.None;
			_oneWayResultAction = null;
			_roundTripResultAction = null;
			_messageSent = false;
			_responseHeader.Clear();
			_responseReceived = false;

			if (_sendData != null)
			{
				_sendData.Dispose();
				_sendData = null;
			}

			if (_responseData != null)
			{
				_responseData.Dispose();
				_responseData = null;
			}
		}

		private bool ValidateCompletedEvent(SocketAsyncEventArgs e, SocketAsyncOperation expected)
		{
			if (_error != null) return false;

			if (e.SocketError != SocketError.Success)
			{
				SetError(e.SocketError);
				return false;
			}

			if (e.LastOperation != expected)
			{
				SetError(new InvalidOperationException(string.Format(
					"Excepted last operation {0} but last operation was {1}.",
					expected,
					e.LastOperation)));
				return false;
			}

			return true;
		}

		private IEnumerator<bool> GetSendEnumerator(
			int timeout,
			IPoolItem<MemoryStream> sendData,
			OperationType type,
			Delegate resultAction,
			ParameterlessDelegate callback)
		{
			ValidateSocketForUse();

			_sendData = sendData;
			_operationType = type;
			if (_operationType == OperationType.OneWay)
			{
				_oneWayResultAction = (Action<OneWayAsyncEventArgs>)resultAction;
			}
			else if (_operationType == OperationType.RoundTrip)
			{
				_roundTripResultAction = (Action<RoundTripAsyncEventArgs>)resultAction;
				_responseData = AsyncSocketClient.MemoryPool.Borrow();
			}
			else
			{
				string message = string.Format("Unexpected operation type '{0}'", type);
				Debug.Fail(message);
				SetError(new InvalidOperationException(message));
				yield break;
			}

			_socketArgs.UserToken = callback;

			if (_state == State.Uninitialized)
			{
				LogDebugStep("Connecting...");
				_state = State.Connecting;
				var connectTimeoutHandle = TaskMonitor.RegisterMonitor(_connectTimeout, _timeoutHandler, null);
				if (_socket.ConnectAsync(_socketArgs)) yield return false;
				if (!connectTimeoutHandle.TrySetComplete())
				{
					LogDebugStep("Failed to set connection timeout handle complete.");
					SetError(SocketError.TimedOut);
					yield break;
				}
				if (!ValidateCompletedEvent(_socketArgs, SocketAsyncOperation.Connect)) yield break;

				_state = State.Connected;
				LogDebugStep("Connected.");

				ThreadPool.UnsafeQueueUserWorkItem(o =>
				{
					var target = (SocketChannel)o;
					var task = new AsyncSocketTask(this);
					task.SetEnumerator(target.GetReceiveEnumerator(task.Callback));
					task.Execute(false);
				}, this);
			}

			Debug.Assert(_sendData != null, "_sendData was not set prior to starting the send enumerator.");
			_socketArgs.SetBuffer(_sendData.Item.GetBuffer(), (int)_sendData.Item.Position, (int)_sendData.Item.Length);
			_timeoutHandle = TaskMonitor.RegisterMonitor(timeout, _timeoutHandler, null);
			// note - it seems this is necessary because of a bug in the SendAsync function
			// see - http://social.msdn.microsoft.com/Forums/en-IE/ncl/thread/40fe397c-b1da-428e-a355-ee5a6b0b4d2c
			Thread.MemoryBarrier();
			if (_log.IsDebugEnabled)
			{
				LogDebugStep(string.Format("Sending {0:###,###,###,##0} bytes", _sendData.Item.Length - _sendData.Item.Position));
			}
			if (_socket.SendAsync(_socketArgs)) yield return false;
			if (!ValidateCompletedEvent(_socketArgs, SocketAsyncOperation.Send)) yield break;

			_messageSent = true;
		}

		private IEnumerator<bool> GetReceiveEnumerator(ParameterlessDelegate callback)
		{
			_receiveArgs.UserToken = callback;

			try
			{
				while (true)
				{
					if (_error != null) yield break;
					// note - it seems this is necessary because of a bug in the SendAsync function
					// see - http://social.msdn.microsoft.com/Forums/en-IE/ncl/thread/40fe397c-b1da-428e-a355-ee5a6b0b4d2c
					Thread.MemoryBarrier();
					LogDebugStep("Receiving...");
					yield return !_socket.ReceiveAsync(_receiveArgs);
					LogDebugStep("Receive Complete.");
					if (!ValidateCompletedEvent(_receiveArgs, SocketAsyncOperation.Receive)) yield break;

					if (_receiveArgs.BytesTransferred == 0)
					{
						SetError(SocketError.ConnectionReset);
						yield break;
					}

					if (_operationType != OperationType.RoundTrip)
					{
						SetError(new InvalidOperationException("Received data when no round trip operation was pending."));
						yield break;
					}

					int position = _receiveArgs.Offset;
					int count = _receiveArgs.BytesTransferred;
					if (!_responseHeader.IsComplete)
					{
						position += _responseHeader.Read(_receiveArgs.Buffer, position, count - position);
						if (!_responseHeader.IsComplete || count == position)
						{
							if (_log.IsDebugEnabled)
							{
								LogDebugStep(string.Format("Received partial header {0:###,##0} bytes", count - position));
							}
							continue;
						}
					}
					if (_responseData == null) _responseData = AsyncSocketClient.MemoryPool.Borrow();
					int countAvailable = count - position;
					int countNeeded = _responseHeader.MessageDataLength - (int)_responseData.Item.Length;
					if (countNeeded <= countAvailable)
					{
						_responseData.Item.Write(_receiveArgs.Buffer, position, countNeeded);
						_responseData.Item.Seek(0, SeekOrigin.Begin);
						if (_responseHeader.MessageLength == ServerMessage.EmptyReplyMessageLength
							&& ServerMessage.IsEmptyMessage(_responseData.Item.GetBuffer(), (int)_responseData.Item.Position, _responseHeader.MessageLength))
						{
							_responseData.Dispose();
							_responseData = null;
						}
						_responseReceived = true;
					}
					else
					{
						_responseData.Item.Write(_receiveArgs.Buffer, position, countAvailable);
					}
					if (_log.IsDebugEnabled)
					{
						LogDebugStep(countAvailable + " of " + countNeeded + " bytes received. " + (countNeeded - countAvailable) + " bytes remaining. Complete=" + IsOperationComplete);
					}
				}
			}
			finally
			{
				LogDebugStep("Receive Loop Ended.");
			}
		}

		private void SetError(SocketError error)
		{
			SetError(new SocketException((int)error));
		}

		private void SetError(Exception error)
		{
			if (_error == null)
			{
				_error = error;
				Close();
				if (_log.IsDebugEnabled)
				{
					LogDebugStep("SetError called for the first time. Error = " + error + "\r\n\r\nCaller stack = " + Environment.StackTrace);
				}
			}
			else
			{
				if (_log.IsDebugEnabled)
				{
					LogDebugStep("SetError called more than once. Error = " + error + "\r\n\r\nCaller stack = " + Environment.StackTrace);
				}
			}
		}

		private void LogDebugStep(string message)
		{
			if (_log.IsDebugEnabled)
			{
				var ipEndpoint = _state == State.Disposed ? null :  _socket.LocalEndPoint as IPEndPoint;
				var ephemeralPort = ipEndpoint != null ? ipEndpoint.Port.ToString() : "?";
				var header = string.Format("{0} (Session={1},EphemeralPort={2}) - ", _endpoint, _currentRequestId, ephemeralPort);
				_log.Debug(header + message);
			}
		}

		private void Close()
		{
			try
			{
				_socket.Shutdown(SocketShutdown.Both);
			}
			catch (Exception ex)
			{
				_log.Error("Failed to shut socket down.", ex);
			}
			try
			{
				_socket.Close();
			}
			catch (Exception ex)
			{
				_log.Error("Failed to close socket.", ex);
			}
			try
			{
				_socketArgs.Dispose();
			}
			catch (Exception ex)
			{
				_log.Error("Failed to dispose SocketChannel._socketArgs.", ex);
			}
			try
			{
				_receiveArgs.Dispose();
			}
			catch (Exception ex)
			{
				_log.Error("Failed to dispose SocketChannel._receiveArgs.", ex);
			}
			_state = State.Disposed;
		}

		private bool IsOperationComplete
		{
			get
			{
				switch (_operationType)
				{
					case OperationType.None:
						return false;
					case OperationType.OneWay:
						return _messageSent || HasError;
					case OperationType.RoundTrip:
						return (_messageSent && _responseReceived) || HasError;
					default:
						string message = string.Format("Un-expected operation type '{0}' encountered while checking for completion.", _operationType);
						Debug.Fail(message);
						SetError(new InvalidOperationException(message));
						return true;
				}
			}
		}

		private bool IsDisposed
		{
			get { return _state == State.Disposed; }
		}

		#region IDisposable Members

		private bool _disposeCalled;

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			if (_disposeCalled) return;

			ICompletion completion = null;
			lock (_socket)
			{
				if (_disposeCalled) return;
				_disposeCalled = true;

				_error = _disposedException;

				if (_state != State.Disposed) Close();
				if (IsOperationComplete)
				{
					completion = CompleteOperation(false);
				}
			}
			if (completion != null) completion.Complete();
		}

		#endregion

		private class AsyncSocketTask
		{
			private readonly SocketChannel _socketChannel;
			private readonly ParameterlessDelegate _callback;
			private IEnumerator<bool> _asyncEnumerator;

			public AsyncSocketTask(SocketChannel socketChannel)
			{
				_socketChannel = socketChannel;
				_callback = () => Execute(false);
			}

			public ParameterlessDelegate Callback
			{
				[DebuggerStepThrough]
				get { return _callback; }
			}

			/// <summary>
			///	<para>Accepts an enumerator that performs operations on the socket.
			///	While the enumerator is running it will have aquired a lock on _socket
			///	so no other tasks can run concurrently. The enumerator should 
			///	yield break to finish, yield return false to yield control on async I/O,
			///	(the async I/O callback should then invoke <see cref="Callback"/>
			///	when it's ready to continue), yield return true to yield control so the
			///	task may check for completion then continue the task synchronously.</para>
			/// </summary>
			/// <param name="target">The target enumerator.</param>
			public void SetEnumerator(IEnumerator<bool> target)
			{
				_asyncEnumerator = target;
			}

			public void Execute(bool invokedByConsumer)
			{
				ICompletion completion = null;
				bool running = false;
				bool continueSynchronously = false;
				do
				{
					Monitor.Enter(_socketChannel._socket);
					try
					{
						running = _asyncEnumerator.MoveNext();
						continueSynchronously = running && _asyncEnumerator.Current;
					}
					catch (Exception ex)
					{
						running = false;
						_socketChannel.SetError(ex);
					}
					finally
					{
						try
						{
							if (_socketChannel.IsOperationComplete)
							{
								completion = _socketChannel.CompleteOperation(invokedByConsumer);
							}
							if (!running)
							{
								var disposable = _asyncEnumerator as IDisposable;
								if (disposable != null) disposable.Dispose();
							}
						}
						finally
						{
							Monitor.Exit(_socketChannel._socket);
							if (completion != null)
							{
								try
								{
									if (continueSynchronously)
									{
										ThreadPool.UnsafeQueueUserWorkItem(o => ((ICompletion)o).Complete(), completion);
									}
									else
									{
										completion.Complete();
									}
								}
								finally
								{
									completion = null;
								}
							}
						}
					}
				}
				while (continueSynchronously);
			}
		}
	}
}
