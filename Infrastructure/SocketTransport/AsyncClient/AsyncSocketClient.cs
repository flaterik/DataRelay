using System;
using System.IO;
using System.Net;
using MySpace.Common;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>A socket client that allows the use of asynchronous I/O.</para>
	/// </summary>
	public class AsyncSocketClient : IDisposable
	{
		// must never be 0 otherwise the server header coming back will be the wrong size (4 instead of 6)
		private const short _messageId = 1;
		private const int _initialBufferSize = 1 << 10;
		private static readonly Pool<MemoryStream> _memoryPool = new Pool<MemoryStream>(
			() => new MemoryStream(_initialBufferSize),
			(stream, phase) =>
			{
				if (phase == PoolItemPhase.Returning) stream.SetLength(0);
				return true;
			},
			new PoolConfig
			{
				FetchOrder = PoolFetchOrder.Lifo,
				MaxUses = 100
			});

		/// <summary>
		/// 	<para>Gets a memory pool that may be used within the assembly.</para>
		/// </summary>
		/// <value>
		/// 	<para>A memory pool that may be used within the assembly.</para>
		/// </value>
		internal static Pool<MemoryStream> MemoryPool { get { return _memoryPool; } }

		private readonly SocketPool _socketPool;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="AsyncSocketClient"/> class.</para>
		/// </summary>
		/// <param name="endPoint">The remote end-point this instance will connect to.</param>
		/// <param name="config">Optional configuration settings. Specify <see langword="null"/> to fallback on reasonable defaults.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="endPoint"/> is <see langword="null"/>.</para>
		/// </exception>
		public AsyncSocketClient(IPEndPoint endPoint, SocketPoolConfig config)
		{
			if (endPoint == null) throw new ArgumentNullException("endPoint");

			_socketPool = new SocketPool(endPoint, config ?? new SocketPoolConfig());
		}

		/// <summary>
		/// 	<para>Gets the end point this instance communicates with.</para>
		/// </summary>
		/// <value>
		/// 	<para>The end point this instance communicates with.</para>
		/// </value>
		public IPEndPoint EndPoint
		{
			get { return _socketPool.RemoteEndPoint; }
		}

		/// <summary>
		///	<para>Sends a <see cref="Stream"/> of containing data serialized from
		///	<paramref name="dataSerializer"/> and <paramref name="data"/> to
		///	<see cref="AsyncSocketClient.EndPoint"/> with <paramref name="commandId"/>.
		///	Calls <paramref name="resultAction"/> when all data has been sent to
		///	the destination. Please note that this is just a tcp acknowledgement that the data
		///	was received and not an acknowlegements that the data was processed.</para>
		/// </summary>
		/// <typeparam name="T">The type of data to be sent.</typeparam>
		/// <param name="commandId">The command id.</param>
		/// <param name="data">The data to send.</param>
		/// <param name="dataSerializer">
		///	<para>A method to serialize <paramref name="data"/> into a <see cref="Stream"/>.</para>
		/// </param>
		/// <param name="resultAction">
		///	<para>The method that will be called when the remote end point acknowledges that the data was
		///	recieved, the operation fails, or the operation times out.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="dataSerializer"/> is <see langword="null"/>.</para>
		///	<para>- or -</para>
		///	<para><paramref name="resultAction"/> is <see langword="null"/>.</para>
		/// </exception>
		public void SendOneWayAsync<T>(
			short commandId,
			T data,
			Procedure<T, Stream> dataSerializer,
			Action<OneWayAsyncEventArgs> resultAction)
		{
			if (dataSerializer == null) throw new ArgumentNullException("dataSerializer");
			if (resultAction == null) throw new ArgumentNullException("resultAction");

			var sendData = _memoryPool.Borrow();
			IPoolItem<SocketChannel> socket = null;
			try
			{
				ClientMessage.WriteMessage<T>(
					sendData.Item,
					_socketPool.Config.NetworkOrdered,
					commandId,
					_messageId, // must never send 0 otherwise the server header coming back will be the wrong size
					false,
					data,
					dataSerializer);
				sendData.Item.Seek(0, SeekOrigin.Begin);

				socket = _socketPool.Pool.Borrow();

				socket.Item.SendOneWayAsync(
					_socketPool.Config.ReceiveTimeout,
					sendData,
					e =>
					{
						socket.Dispose();
						resultAction(e);
					});
			}
			catch
			{
				if (socket != null)
				{
					socket.IsCorrupted = true;
					socket.Dispose();
				}
				sendData.Dispose();
				throw;
			}
		}

		/// <summary>
		///	<para>Sends a <see cref="Stream"/> of containing data serialized from
		///	<paramref name="dataSerializer"/> and <paramref name="data"/> to
		///	<see cref="AsyncSocketClient.EndPoint"/> with <paramref name="commandId"/>.
		///	Calls <paramref name="resultAction"/> when response data has been
		///	recieved from <see cref="AsyncSocketClient.EndPoint"/>.</para>
		/// </summary>
		/// <typeparam name="T">The type of data to be sent.</typeparam>
		/// <param name="commandId">The command id.</param>
		/// <param name="data">The data to send.</param>
		/// <param name="dataSerializer">
		///	<para>A method to serialize <paramref name="data"/> into a <see cref="Stream"/>.</para>
		/// </param>
		/// <param name="resultAction">
		///	<para>The method that will be called when the remote end point responds,
		///	the operation fails, or the operation times out.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="dataSerializer"/> is <see langword="null"/>.</para>
		///	<para>- or -</para>
		///	<para><paramref name="resultAction"/> is <see langword="null"/>.</para>
		/// </exception>
		public void SendRoundTripAsync<T>(
			short commandId,
			T data,
			Procedure<T, Stream> dataSerializer,
			Action<RoundTripAsyncEventArgs> resultAction)
		{
			if (dataSerializer == null) throw new ArgumentNullException("dataSerializer");
			if (resultAction == null) throw new ArgumentNullException("resultAction");

			var sendData = _memoryPool.Borrow();
			IPoolItem<SocketChannel> socket = null;
			try
			{
				ClientMessage.WriteMessage<T>(
					sendData.Item,
					_socketPool.Config.NetworkOrdered,
					commandId,
					_messageId, // must never send 0 otherwise the server header coming back will be the wrong size
					true,
					data,
					dataSerializer);
				sendData.Item.Seek(0, SeekOrigin.Begin);

				socket = _socketPool.Pool.Borrow();

				socket.Item.SendRoundTripAsync(
					_socketPool.Config.ReceiveTimeout,
					sendData,
					args =>
					{
						socket.Dispose();
						resultAction(args);
					});
			}
			catch
			{
				if (socket != null)
				{
					socket.IsCorrupted = true;
					socket.Dispose();
				}
				sendData.Dispose();
				throw;
			}
		}

		private class SocketPool
		{
			private readonly IPEndPoint _remoteEndPoint;
			private readonly SocketPoolConfig _config;
			private readonly Pool<SocketChannel> _pool;

			public SocketPool(IPEndPoint remoteEndPoint, SocketPoolConfig config)
			{
				_remoteEndPoint = remoteEndPoint;
				_config = config;
				_pool = new Pool<SocketChannel>(
					() => new SocketChannel(_remoteEndPoint, _config.ConnectTimeout),
					(socket, phase) => !socket.HasError,
					_config);
			}

			public IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }

			public SocketPoolConfig Config { get { return _config; } }

			public Pool<SocketChannel> Pool { get { return _pool; } }
		}

		#region IDisposable Members

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			_socketPool.Pool.Dispose();
		}

		#endregion
	}
}
