using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace MySpace.Common.Storage
{
	/// <summary>
	/// Provides forward-only, read-only access to unmanaged blocks of memory
	/// from managed code with automatic cleanup on close.
	/// </summary>
	[SuppressUnmanagedCodeSecurity]
	public unsafe class SafeUnmanagedMemoryStream : Stream
	{
		[DllImport("kernel32.dll", EntryPoint = "CopyMemory")]
		static extern void CopyMemory(IntPtr Destination, IntPtr Source, int Length);

		private readonly byte* _cleanupPointer;
		private byte* _pointer;
		private PostAccessUnmanagedMemoryCleanup _cleanup;
		private long _remaining;
		private readonly long _length;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="SafeUnmanagedMemoryStream"/> class.</para>
		/// </summary>
		/// <param name="pointer">
		/// 	<para>Pointer to the unmanaged binary data block.</para>
		/// </param>
		/// <param name="length">
		/// 	<para>Length of the segment of the block to read.</para>
		/// </param>
		/// <param name="cleanup">
		/// 	<para>Action that cleans up block on close/dispose/finalize.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="pointer"/> is <see langword="null"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="cleanup"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		public SafeUnmanagedMemoryStream(byte* pointer, long length, PostAccessUnmanagedMemoryCleanup cleanup)
		{
			if (pointer == null) throw new ArgumentNullException("pointer");
			if (length < 0) throw new ArgumentOutOfRangeException("length");
			if (cleanup == null) throw new ArgumentNullException("cleanup");
			_pointer = pointer;
			_cleanupPointer = pointer;
			_length = length;
			_remaining = length;
			_cleanup = cleanup;
		}

		/// <summary>
		/// 	<para>Overriden. Releases the unmanaged resources used by the <see cref="System.IO.UnmanagedMemoryStream"/> and optionally releases the managed resources.</para>
		/// </summary>
		/// <param name="disposing">
		/// 	<para>true to release both managed and unmanaged resources; false to release only unmanaged resources.</para>
		/// </param>
		protected override void Dispose(bool disposing)
		{
			if (_pointer == null) return;
			try
			{
				_cleanup(_cleanupPointer);
			}
			finally
			{
				_pointer = null;
				if (disposing)
				{
					// don't need to call GC.SuppressFinalize because Stream.Close
					// and Stream.Dispose do so
					_cleanup = null;
					_remaining = 0;
				}
			}
		}

		~SafeUnmanagedMemoryStream()
		{
			Dispose(false);
		}

		/// <summary>
		/// 	<para>Overriden. Gets a value indicating whether the current stream supports reading.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the stream is open; otherwise, false.</para>
		/// </returns>
		public override bool CanRead
		{
			get { return _pointer != null; }
		}

		/// <summary>
		/// 	<para>Overriden. Gets a value indicating whether the current
		///		stream supports seeking.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the stream is open; otherwise, false.</para>
		/// </returns>
		/// <remarks>Returns true for open socket since <see cref="Length"/>
		/// and <see cref="Position"/> can be read. However, <see cref="Seek"/>,
		/// <see cref="SetLength"/> and writes of <see cref="Position"/> are not
		/// supported.</remarks>
		public override bool CanSeek
		{
			get { return _pointer != null; }
		}

		/// <summary>
		/// 	<para>Overriden. Gets a value indicating whether the current stream supports writing.</para>
		/// </summary>
		/// <returns>
		/// 	<para>false.</para>
		/// </returns>
		public override bool CanWrite
		{
			get { return false; }
		}

		/// <summary>
		/// 	<para>Overriden. Clears all buffers for this stream and causes any buffered
		///		data to be written to the underlying device. Does nothing in this class.</para>
		/// </summary>
		public override void Flush() {}

		/// <summary>
		/// 	<para>Overriden. Gets the length in bytes of the stream.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A long value representing the length of the stream in
		///		bytes.</para>
		/// </returns>
		/// <exception cref="ObjectDisposedException">
		/// 	<para>Was called after the stream was closed.</para>
		/// </exception>
		public override long Length
		{
			get
			{
				if (_pointer == null) throw new ObjectDisposedException(typeof(SafeUnmanagedMemoryStream).Name); 
				return _length;
			}
		}

		/// <summary>
		/// 	<para>Overriden. Gets or sets the position within the
		///		current stream.</para>
		/// </summary>
		/// <returns>
		/// 	<para>The current position within the stream.</para>
		/// </returns>
		/// <exception cref="ObjectDisposedException">
		/// 	<para>Was called after the stream was closed.</para>
		/// </exception>
		public override long Position
		{
			get
			{
				if (_pointer == null) throw new ObjectDisposedException(typeof(SafeUnmanagedMemoryStream).Name);
				return _length - _remaining;
			}
			set
			{
				if (_pointer == null) throw new ObjectDisposedException(typeof(SafeUnmanagedMemoryStream).Name);
				var position = value;
				if (position < 0)
				{
					position = 0;
				}
				else if (position > _length)
				{
					position = _length;
				}
				_pointer = _cleanupPointer + position;
				_remaining = _length - position;
			}
		}

		/// <summary>
		/// 	<para>Overriden. When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.</para>
		/// </summary>
		/// <returns>
		/// 	<para>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</para>
		/// </returns>
		/// <param name="buffer">
		/// 	<para>An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the bytes read from the current source.</para>
		/// </param>
		/// <param name="offset">
		/// 	<para>The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</para>
		/// </param>
		/// <param name="count">
		/// 	<para>The maximum number of bytes to be read from the current stream.</para>
		/// </param>
		/// <exception cref="System.ArgumentException">
		/// 	<para>The sum of <paramref name="offset"/> and <paramref name="count"/> is larger than the buffer length.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// 	<para>
		/// 		<paramref name="buffer"/> is null.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// 	<para>
		/// 		<paramref name="offset"/> or <paramref name="count"/> is negative.</para>
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		/// 	<para>This method was called after the stream was closed.</para>
		/// </exception>
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_pointer == null) throw new ObjectDisposedException(typeof(SafeUnmanagedMemoryStream).Name);
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset");
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			if (offset + count > buffer.Length) throw new ArgumentException();
			if (count == 0 || _remaining == 0) return 0;
			if (count < _remaining)
			{
				_remaining -= count;
			}
			else
			{
				count = (int)_remaining;
				_remaining = 0;
			}
			fixed (byte* dst = &buffer[offset])
			{
				CopyMemory(new IntPtr(dst), new IntPtr(_pointer), count);
			}
			_pointer += count;
			return count;
		}

		/// <summary>
		/// 	<para>Overriden. Reads a byte from the stream and advances the
		///		position within the stream by one byte, or returns -1 if at
		///		the end of the stream.</para>
		/// </summary>
		/// <returns>
		/// 	<para>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</para>
		/// </returns>
		/// <exception cref="ObjectDisposedException">
		/// 	<para>This method was called after the stream was closed.</para>
		/// </exception>
		public override int ReadByte()
		{
			if (_pointer == null) throw new ObjectDisposedException(typeof(SafeUnmanagedMemoryStream).Name);
			if (_remaining == 0) return -1;
			--_remaining;
			return *_pointer++;
		}

		/// <summary>
		/// 	<para>Overriden. When overridden in a derived class, sets the
		///		position within the current stream. Not supported.</para>
		/// </summary>
		/// <returns>
		/// 	<para>The new position within the current stream.</para>
		/// </returns>
		/// <param name="offset">
		/// 	<para>A byte offset relative to the <paramref name="origin"/> parameter.</para>
		/// </param>
		/// <param name="origin">
		/// 	<para>A value of type <see cref="System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</para>
		/// </param>
		/// <exception cref="ObjectDisposedException">
		/// 	<para>This method was called after the stream was closed.</para>
		/// </exception>
		public override long Seek(long offset, SeekOrigin origin)
		{
			if (_pointer == null) throw new ObjectDisposedException(typeof(SafeUnmanagedMemoryStream).Name);
			long position;
			switch(origin)
			{
				case SeekOrigin.Begin:
					position = 0;
					break;
				case SeekOrigin.Current:
					position = Position;
					break;
				case SeekOrigin.End:
					position = _length;
					break;
				default:
					throw new NotImplementedException(string.Format(
						"{0} not supported", origin));
			}
			position += offset;
			Position = position;
			return Position;
		}

		/// <summary>
		/// 	<para>Overriden. When overridden in a derived class, sets the
		///		length of the current stream. Not supported.</para>
		/// </summary>
		/// <param name="value">
		/// 	<para>The desired length of the current stream in bytes.</para>
		/// </param>
		/// <exception cref="NotSupportedException">
		/// 	<para>This method is not supported.</para>
		/// </exception>
		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// 	<para>Overriden. When overridden in a derived class, writes a
		///		sequence of bytes to the current stream and advances the
		///		current position within this stream by the number of bytes
		///		written. Not supported.</para>
		/// </summary>
		/// <param name="buffer">
		/// 	<para>An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the current stream.</para>
		/// </param>
		/// <param name="offset">
		/// 	<para>The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the current stream.</para>
		/// </param>
		/// <param name="count">
		/// 	<para>The number of bytes to be written to the current stream.</para>
		/// </param>
		/// <exception cref="NotSupportedException">
		/// 	<para>This method is not supported.</para>
		/// </exception>
		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}
	}
}
