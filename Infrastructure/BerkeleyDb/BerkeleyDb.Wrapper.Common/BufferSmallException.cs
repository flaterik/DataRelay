using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// The exception that is thrown when a small buffer error occurs in Berkeley Db.
	/// </summary>
	public class BufferSmallException : BdbException
	{
		/// <summary>
		/// Gets the length of the buffer.
		/// </summary>
		/// <value>The <see cref="UInt32"/> length of the buffer.</value>
		public uint BufferLength { get; private set; }
		/// <summary>
		/// Gets the length of the record.
		/// </summary>
		/// <value>The <see cref="UInt32"/> length of the record.</value>
		public uint RecordLength { get; private set; }
		/// <summary>
		/// Initializes a new instance of the <see cref="BufferSmallException"/> class.
		/// </summary>
		/// <param name="bufferLength">The length of the buffer.</param>
		/// <param name="recordLength">The length of the record.</param>
		/// <param name="message">The error message.</param>
		public BufferSmallException(uint bufferLength, uint recordLength, string message) :
			base((int)DbRetVal.BUFFER_SMALL, message)
		{
			BufferLength = bufferLength;
			RecordLength = recordLength;
		}
	}
}
