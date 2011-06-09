using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;

namespace BerkeleyDbWrapper
{
	public class DatabaseEntry
	{
		// Fields
		public byte[] Buffer;
		public int Length = 0;
		public int StartPosition = 0;

		// Methods
		public DatabaseEntry(int capacity)
		{
			this.Buffer = new byte[capacity];
		}

		public DatabaseEntry(byte[] buffer)
		{
			Buffer = buffer;
			Length = buffer.Length;
		}

		public static implicit operator DataBuffer(DatabaseEntry entry)
		{
			byte[] buffer = null;
			if (entry == null)
			{
				return DataBuffer.Empty;
			}
			buffer = entry.Buffer;
			if (buffer == null)
			{
				return DataBuffer.Empty;
			}
			int blen = buffer.Length;
			if (blen == 0)
			{
				return DataBuffer.Empty;
			}
			int start = entry.StartPosition;
			if (start >= blen)
			{
				start = blen - 1;
			}
			if (start < 0)
			{
				start = 0;
			}
			int len = entry.Length;
			if ((start + len) > blen)
			{
				len = blen - start;
			}
			if (len <= 0)
			{
				return DataBuffer.Empty;
			}
			return DataBuffer.Create(buffer, start, len);
		}

		public void Resize(int size)
		{
			this.Buffer = new byte[size];
		}
	}


}
