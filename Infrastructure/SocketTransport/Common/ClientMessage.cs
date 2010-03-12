using System;
using System.IO;
using System.Net;
using MySpace.Common;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>Encapsulates methods for reading/writing messages that originate from the client
	/// 	that are compatible with MySpace's existing socket transport system.</para>
	/// </summary>
	public static class ClientMessage
	{
		private const int _messageStarterOffset = 0;
		private const int _messageLengthOffset = _messageStarterOffset + sizeof(short);
		private const int _messageCommandIdOffset = _messageLengthOffset + sizeof(int);
		private const int _isSyncOffset = _messageCommandIdOffset + sizeof(int);

		private const int _headerSize = _isSyncOffset + 1;
		private const int _terminatorSize = 2;
		private const int _envelopeSize = _headerSize + _terminatorSize;

		private const short _messageStarterHost = short.MaxValue;
		private static readonly short _messageStarterNetwork = IPAddress.HostToNetworkOrder(_messageStarterHost);
		private static readonly byte[] _messageTerminatorHost = BitConverter.GetBytes(short.MinValue);
		private static readonly byte[] _messageTerminatorNetwork = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(short.MinValue));

		private static readonly Pool<byte[]> _headerBufferPool = new Pool<byte[]>(
			() => new byte[_headerSize],
			null,
			new PoolConfig
			{
				FetchOrder = PoolFetchOrder.Lifo,
				PoolCapacity = Environment.ProcessorCount
			});

		/// <summary>
		///	<para>The number of bytes that will be prefixed to the data sent to the host.</para>
		/// </summary>
		public static readonly int HeaderSize = _headerSize;

		/// <summary>
		///	<para>The number of bytes that will be appended to the data sent to the host.</para>
		/// </summary>
		public static readonly int TerminatorSize = _terminatorSize;

		/// <summary>
		///	<para>The number of bytes added to messages.</para>
		///	<para><see cref="EnvelopeSize"/> == <see cref="HeaderSize"/> + <see cref="TerminatorSize"/>.</para>
		/// </summary>
		public static readonly int EnvelopeSize = _envelopeSize;

		private static void _writeMessageHeader(
			Stream destination,
			bool networkOrdered,
			int messageLength,
			short commandId,
			short messageId,
			bool isRoundTrip)
		{
			using(var buffer = _headerBufferPool.Borrow())
			{
				if (networkOrdered)
				{
					BitConverterEx.WriteBytes(buffer.Item, _messageStarterOffset, _messageStarterNetwork);
					BitConverterEx.WriteBytes(buffer.Item, _messageLengthOffset, IPAddress.HostToNetworkOrder(messageLength + _envelopeSize));
					if (messageId == 0)
					{
						BitConverterEx.WriteBytes(buffer.Item, _messageCommandIdOffset, IPAddress.HostToNetworkOrder((int)commandId));
					}
					else
					{
						BitConverterEx.WriteBytes(buffer.Item, _messageCommandIdOffset, IPAddress.HostToNetworkOrder(messageId));
						BitConverterEx.WriteBytes(buffer.Item, _messageCommandIdOffset + sizeof(short), IPAddress.HostToNetworkOrder(commandId));
					}
					BitConverterEx.WriteBytes(buffer.Item, _isSyncOffset, isRoundTrip);
				}
				else
				{
					BitConverterEx.WriteBytes(buffer.Item, _messageStarterOffset, _messageStarterHost);
					BitConverterEx.WriteBytes(buffer.Item, _messageLengthOffset, messageLength + _envelopeSize);
					if (messageId == 0)
					{
						BitConverterEx.WriteBytes(buffer.Item, _messageCommandIdOffset, (int)commandId);
					}
					else
					{
						BitConverterEx.WriteBytes(buffer.Item, _messageCommandIdOffset, commandId);
						BitConverterEx.WriteBytes(buffer.Item, _messageCommandIdOffset + sizeof(short), messageId);
					}
					BitConverterEx.WriteBytes(buffer.Item, _isSyncOffset, isRoundTrip);
				}
				destination.Write(buffer.Item, 0, _headerSize);
			}
		}

		private static void _writeMessageTerminator(Stream destination, bool networkOrdered)
		{
			destination.Write(networkOrdered ? _messageTerminatorNetwork : _messageTerminatorHost, 0, _terminatorSize);
		}

		/// <summary>
		/// Writes the message to send to a socket server.
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of object contained in the message.</para>
		/// </typeparam>
		/// <param name="destination">The destination stream to write the message to.</param>
		/// <param name="networkOrdered">
		///	<para><see langword="true"/> to use network-ordered endianness for the header
		///	information; <see langword="false"/> otherwise.</para></param>
		/// <param name="commandId">The command id to send to the server.</param>
		/// <param name="messageId">The message id.</param>
		/// <param name="isRoundTrip">
		///	<para><see langword="true"/> if a reply is expected from the server;
		///	<see langword="false"/> otherwise.</para>
		/// </param>
		/// <param name="message">The object to encode in the message.</param>
		/// <param name="messageSerializer">
		///	<para>A method that will serialize <paramref name="message"/> into <paramref name="destination"/>.</para>
		/// </param>
		public static void WriteMessage<T>(
			Stream destination,
			bool networkOrdered,
			short commandId,
			short messageId,
			bool isRoundTrip,
			T message,
			Procedure<T, Stream> messageSerializer)
		{
			if (destination == null) throw new ArgumentNullException("destination");
			if (messageSerializer == null) throw new ArgumentNullException("messageSerializer");
			if (!destination.CanSeek) throw new ArgumentException("destination must be seakable (Stream.CanSeek)", "destination");

			int startPosition = (int)destination.Position;
			_writeMessageHeader(destination, networkOrdered, -1, commandId, messageId, isRoundTrip);
			messageSerializer(message, destination);
			int endPosition = (int)destination.Position;
			destination.Seek(startPosition + _messageLengthOffset, SeekOrigin.Begin);
			int messageLength = endPosition - startPosition + _terminatorSize;
			if (networkOrdered) messageLength = IPAddress.HostToNetworkOrder(messageLength);
			using(var buffer = _headerBufferPool.Borrow())
			{
				BitConverterEx.WriteBytes(buffer.Item, 0, messageLength);
				destination.Write(buffer.Item, 0, sizeof(int));
			}
			destination.Seek(endPosition, SeekOrigin.Begin);
			_writeMessageTerminator(destination, networkOrdered);
		}
	}
}
