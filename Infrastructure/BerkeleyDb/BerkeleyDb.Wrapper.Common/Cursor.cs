using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using MySpace.Common.Storage;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// The abstract base class for a managed wrapper of a Berkeley Db cursor.
	/// </summary>
	public abstract class Cursor : IDisposable
	{
		// Methods
		/// <summary>
		/// <para>Deletes the current cursor entry.</para>
		/// </summary>
		/// <param name="flags">
		/// <para>The <see cref="DeleteOpFlags" /> specifying the write options.</para>
		/// </param>
		/// <returns>
		/// <para>Whether or not there was a current entry to be deleted.</para>
		/// </returns>
		public abstract bool Delete(DeleteOpFlags flags);
		/// <summary>
		/// <para>Reads a cursor entry into user supplied buffers.</para>
		/// </summary>
		/// <param name="key">
		/// <para>The key <see cref="DataBuffer" />. Will be read for exact or wildcard
		/// searches. Will be written for all exact searches.</para>
		/// </param>
		/// <param name="value">
		/// <para>The value <see cref="DataBuffer" /> written to.</para>
		/// </param>
		/// <param name="offset">
		/// <para>The offset within the entry value to begin copying data. If negative then it
		/// tries a full entry fetch; otherwise it does a partial fetch from this offset and
		/// of the length of <see paramref="value" />.</para>
		/// </param>
		/// <param name="position">
		/// <para>The <see cref="CursorPosition" /> specifying the position at
		/// which to read.</para>
		/// </param>
		/// <param name="flags">
		/// <para>The <see cref="GetOpFlags" /> specifying the read options.</para>
		/// </param>
		/// <returns>
		/// <para>The <see cref="Lengths" /> specifying the lengths of the key
		/// and value entries. These can be greater than the lengths of the data
		/// read if the data buffers are too small. Negative values match the
		/// conditional literals in <see cref="Lengths" />.</para>
		/// </returns>
		public abstract Lengths Get(DataBuffer key, DataBuffer value, int offset, CursorPosition position, GetOpFlags flags);
		/// <summary>
		/// <para>Reads a cursor entry into streams.</para>
		/// </summary>
		/// <param name="key">
		/// <para>The key <see cref="DataBuffer" />. Will be read for exact or wildcard
		/// searches. Will be written for all exact searches.</para>
		/// </param>
		/// <param name="offset">
		/// <para>The offset within the entry value to begin copying data.</para>
		/// </param>
		/// <param name="position">
		/// <para>The <see cref="CursorPosition" /> specifying the position at
		/// which to read.</para>
		/// </param>
		/// <param name="length">
		/// <para>The length of the segment within the entry value to copying data.
		/// Use a negative value to read to the end.</para>
		/// </param>
		/// <param name="flags">
		/// <para>The <see cref="GetOpFlags" /> specifying the read options.</para>
		/// </param>
		/// <returns>
		/// <para>The <see cref="Streams" /> holding the key and value
		/// <see cref="T:System.IO.Stream" />s, as well as operation return code. The
		/// return code will match on of the conditional literals
		/// in <see cref="Lengths" />.</para>
		/// </returns>
		public abstract Streams Get(DataBuffer key, int offset, int length, CursorPosition position, GetOpFlags flags);
		/// <summary>
		/// <para>Reads a cursor entry into user supplied buffers.</para>
		/// </summary>
		/// <param name="key">
		/// <para>The key <see cref="DataBuffer" />. Will be read for exact or wildcard
		/// searches. Will be written for all exact searches.</para>
		/// </param>
		/// <param name="keyOffset">
		/// <para>The offset within the entry key to begin copying data. If negative then it
		/// tries a full entry fetch; otherwise it does a partial fetch from this offset and
		/// of the length of <see paramref="key" /></para>
		/// </param>
		/// <param name="value">
		/// <para>The value <see cref="DataBuffer" /> written to.</para>
		/// </param>
		/// <param name="valueOffset">
		/// <para>The offset within the entry value to begin copying data. If negative then it
		/// tries a full entry fetch; otherwise it does a partial fetch from this offset and
		/// of the length of <see paramref="value" /></para>
		/// </param>
		/// <param name="position">
		/// <para>The <see cref="CursorPosition" /> specifying the position at
		/// which to read.</para>
		/// </param>
		/// <param name="flags">
		/// <para>The <see cref="GetOpFlags" /> specifying the read options.</para>
		/// </param>
		/// <returns>
		/// <para>The <see cref="Lengths" /> specifying the lengths of the key
		/// and value entries. These can be greater than the lengths of the data
		/// read if the data buffers are too small. Negative values match the
		/// conditional literals in <see cref="Lengths" />.</para>
		/// </returns>
		public abstract Lengths Get(DataBuffer key, int keyOffset, DataBuffer value, int valueOffset, CursorPosition position, GetOpFlags flags);
		/// <summary>
		/// <para>Reads a cursor entry into buffers.</para>
		/// </summary>
		/// <param name="key">
		/// <para>The key <see cref="DataBuffer" />. Will be read for exact or wildcard
		/// searches. Will be written for all exact searches.</para>
		/// </param>
		/// <param name="offset">
		/// <para>The offset within the entry value to begin copying data.</para>
		/// </param>
		/// <param name="position">
		/// <para>The <see cref="CursorPosition" /> specifying the position at
		/// which to read.</para>
		/// </param>
		/// <param name="length">
		/// <para>The length of the segment within the entry value to copying data.
		/// Use a negative value to read to the end.</para>
		/// </param>
		/// <param name="flags">
		/// <para>The <see cref="GetOpFlags" /> specifying the read options.</para>
		/// </param>
		/// <returns>
		/// <para>The <see cref="Buffers" /> holding the key and value
		/// <see cref="T:System.Byte" /> arrays, as well as operation return code. The
		/// return code will match on of the conditional literals
		/// in <see cref="Lengths" />.</para>
		/// </returns>
		public abstract Buffers GetBuffers(DataBuffer key, int offset, int length, CursorPosition position, GetOpFlags flags);
		/// <summary>
		/// <para>Writes a cursor entry.</para>
		/// </summary>
		/// <param name="key">
		/// <para>The key <see cref="DataBuffer" /> to write.</para>
		/// </param>
		/// <param name="value">
		/// <para>The value <see cref="DataBuffer" /> to write.</para>
		/// </param>
		/// <param name="offset">
		/// <para>The offset within the entry value to begin writing data.</para>
		/// </param>
		/// <param name="length">
		/// <para>The length of the segment within the entry value to write data.
		/// Use a negative value to read to the end.</para>
		/// </param>
		/// <param name="position">
		/// <para>The <see cref="CursorPosition" /> specifying the position at
		/// which to write.</para>
		/// </param>
		/// <param name="flags">
		/// <para>The <see cref="PutOpFlags" /> specifying the write options.</para>
		/// </param>
		/// <returns>
		/// <para>The <see cref="Lengths" /> specifying the lengths of the key
		/// and value written. Negative values match the conditional literals
		/// in <see cref="Lengths" />.</para>
		/// </returns>
		public abstract Lengths Put(DataBuffer key, DataBuffer value, int offset, int length, CursorPosition position, PutOpFlags flags);
		/// <summary>
		/// Disposes of this instance.
		/// </summary>
		public abstract void Dispose();
	}
}
