using System;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>Encapsulates a message header for messages that originate from the server.</para>
	/// </summary>
	public class ServerMessageHeader
	{
		private const int _headerSize = 6;
		private const int _messageLengthOffset = 0;
		private const int _messageIdOffset = _messageLengthOffset + sizeof(int);

		private readonly byte[] _readData = new byte[_headerSize];
		private int _readCount;
		private int _messageLength;
		private short _messageId;

		/// <summary>
		/// 	<para>Gets a value indicating whether this instance contains a full message header.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if this instance is complete; otherwise, <see langword="false"/>.</para>
		/// </value>
		public bool IsComplete
		{
			get { return _readCount == _headerSize; }
		}

		/// <summary>
		/// 	<para>Clears any header data contained by this instance; sets <see cref="IsComplete"/> to <see langword="false"/>.</para>
		/// </summary>
		public void Clear()
		{
			_readCount = 0;
		}

		/// <summary>
		///	<para>Gets the full length of the message including the header size.</para>
		/// </summary>
		/// <value>The full length of the message including the header size.</value>
		/// <exception cref="InvalidOperationException">
		///	<para>The header is incomplete.</para>
		/// </exception>
		public int MessageLength
		{
			get
			{
				if (!IsComplete) throw new InvalidOperationException("Length cannot be accessed until the entire header has been read.");
				return _messageLength;
			}
		}

		/// <summary>
		///	<para>Gets the length of the message data, not including the header size.</para>
		/// </summary>
		/// <value>The length of the message data, not including the header size.</value>
		/// <exception cref="InvalidOperationException">
		///	<para>The header is incomplete.</para>
		/// </exception>
		public int MessageDataLength
		{
			get
			{
				if (!IsComplete) throw new InvalidOperationException("Length cannot be accessed until the entire header has been read.");
				return _messageLength - _headerSize;
			}
		}

		/// <summary>
		///	<para>Gets message id contained in this header.</para>
		/// </summary>
		/// <value>The message id contained in this header.</value>
		/// <exception cref="InvalidOperationException">
		///	<para>The header is incomplete.</para>
		/// </exception>
		public short MessageId
		{
			get
			{
				if (!IsComplete) throw new InvalidOperationException("MessageId cannot be accessed until the entire header has been read.");
				return _messageId;
			}
		}

		/// <summary>
		/// 	<para>Reads the message header from <paramref name="data"/>
		/// 	starting at <paramref name="offset"/>.</para>
		/// </summary>
		/// <param name="data">The data to read the header from.</param>
		/// <param name="offset">The offset to start reading at.</param>
		/// <param name="count">
		///	<para>The maximum number of bytes to read. If this exceeds the expected header size
		///	the remaining bytes will not be read.</para>
		/// </param>
		/// <returns>
		///	<para>The number of bytes read.</para>
		/// </returns>
		public int Read(byte[] data, int offset, int count)
		{
			if (data == null) throw new ArgumentNullException("data");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "offset must be positive.");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "count must be positive.");
			if (offset + count > data.Length) throw new ArgumentOutOfRangeException("offset", "offset + count must fall within the bounds of data.");

			if (IsComplete) Clear();
			int num = Math.Min(_headerSize - _readCount, count);
			Buffer.BlockCopy(data, offset, _readData, _readCount, num);
			_readCount += num;
			if (IsComplete)
			{
				_messageLength = BitConverter.ToInt32(_readData, _messageLengthOffset);
				_messageId = BitConverter.ToInt16(_readData, _messageIdOffset);
				return num;
			}
			return num;
		}
	}
}
