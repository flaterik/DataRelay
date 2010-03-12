using System;
using System.Net;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>Encapsulates methods for reading/writing messages that originate from the server
	/// 	that are compatible with MySpace's existing socket transport system.</para>
	/// </summary>
	public static class ServerMessage
	{
		private const int _headerSize = 6;
		private const int _messageLengthOffset = 0;
		private const int _messageIdOffset = _messageLengthOffset + sizeof(int);
		private static readonly byte[] _emptyReply = new byte[] { 241, 216, 255, 255 };

		/// <summary>
		///	<para>The number of bytes in the header.</para>
		/// </summary>
		public static readonly int HeaderSize = _headerSize;

		/// <summary>
		///	<para>The total message length, in bytes, of an empty reply. If a message is recieved
		///	of this length it should be checked for the empty reply condition via <see cref="IsEmptyMessage"/>.</para>
		/// </summary>
		public static readonly int EmptyReplyMessageLength = _headerSize + _emptyReply.Length;

		/// <summary>
		/// Reads a message header from <paramref name="source"/>.
		/// <paramref name="source"/> must have at least <see cref="HeaderSize"/> bytes.
		/// </summary>
		/// <param name="source">The source to read from.</param>
		/// <param name="offset">The offset of <paramref name="source"/> to start reading from.</param>
		/// <param name="networkOrdered">
		///	<para><see langword="true"/> if the header information is expected to be in network order;
		///	<see langword="false"/> otherwise.</para>
		/// </param>
		/// <param name="messageLength">The length of the message including the header.</param>
		/// <param name="messageId">The message id that was sent to the server.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="source"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="source.Length"/> is less than <see cref="HeaderSize"/>.</para>
		///	<para>- or -</para>
		///	<para>The remaining bytes following <paramref name="source"/>[<paramref name="offset"/>] (inclusive) is less than <see cref="HeaderSize"/>.</para>
		/// </exception>
		public static void ReadMessageHeader(
			byte[] source,
			int offset,
			bool networkOrdered,
			out int messageLength,
			out int messageId)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (source.Length < _headerSize)
			{
				throw new ArgumentOutOfRangeException("source", "source must be at least ServerMessage.HeaderSize bytes long.");
			}
			if (source.Length - offset < _headerSize)
			{
				throw new ArgumentOutOfRangeException("offset", "The number of bytes starting at source[offset] must be at least ServerMessage.HeaderSize.");
			}

			messageLength = BitConverter.ToInt32(source, offset + _messageLengthOffset);
			short resultMessageId = BitConverter.ToInt16(source, offset + _messageIdOffset);

			if (networkOrdered)
			{
				messageLength = IPAddress.NetworkToHostOrder(messageLength);
				resultMessageId = IPAddress.NetworkToHostOrder(resultMessageId);
			}

			messageId = (int)resultMessageId;
		}

		/// <summary>
		/// 	<para>Determines whether a message from the server is a special empty reply message.</para>
		/// </summary>
		/// <param name="messageBuffer">The buffer containing the message.</param>
		/// <param name="offset">
		///	<para>The offset where the message data begins. This is the place where the data begins
		///	and not where the message header begins.</para>
		/// </param>
		/// <param name="totalMessageLength">
		///	<para>Total length of the message. This is the same message length value that is retrieved
		///	from <see cref="ReadMessageHeader"/>. This value includes the length of message header.</para>
		/// </param>
		/// <returns>
		/// 	<para><see langword="true"/> if the message is a special empty reply message;
		/// 	otherwise, <see langword="false"/>.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="messageBuffer"/> is <see langword="null"/></para>.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="messageBuffer.Length"/> is less than
		///	<see cref="EmptyReplyMessageLength"/> - <see cref="HeaderSize"/>.</para>
		///	<para>- or -</para>
		///	<para>The remaining bytes following <paramref name="messageBuffer"/> is less than
		///	<see cref="EmptyReplyMessageLength"/> - <see cref="HeaderSize"/>.</para>
		/// </exception>
		public static bool IsEmptyMessage(
			byte[] messageBuffer,
			int offset,
			int totalMessageLength)
		{
			if (messageBuffer == null) throw new ArgumentNullException("messageBuffer");
			if (messageBuffer.Length < _emptyReply.Length)
			{
				throw new ArgumentOutOfRangeException("messageBuffer", "messageBuffer must be at least EmptyReplyMessageLength bytes long");
			}
			if (offset < 0 || offset + _emptyReply.Length > messageBuffer.Length)
			{
				throw new ArgumentOutOfRangeException("offset", string.Format(
					"offset must fall within the bounds of messageBuffer with at least {0} bytes of room for the message",
					_emptyReply.Length));
			}

			if (totalMessageLength != EmptyReplyMessageLength) return false;

			for (int i = 0; i < _emptyReply.Length; i++)
			{
				if (_emptyReply[i] != messageBuffer[offset + i]) return false;
			}
			return true;
		}
	}
}
