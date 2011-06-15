using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;

namespace MySpace.Storage
{
	/// <summary>
	/// Iterates over binary values, and performs reads and writes.
	/// </summary>
	public interface IBinaryCursor : IDisposable
	{
		/// <summary>
		/// Gets the key that this cursor's values are stored under.
		/// </summary>
		/// <value>The <see cref="StorageKey"/> of the values.</value>
		StorageKey Key { get; }

		/// <summary>
		/// Reads the value of an entry.
		/// </summary>
		/// <param name="outputBuffer">The value read.
		/// <see cref="DataBuffer.IsWritable"/> must be <see langword="true"/>.</param>
		/// <param name="moveNext"><see langword="true"/> to move to the next
		/// entry for the read, <see langword="false"/> to read from the current
		/// entry.</param>
		/// <returns>The length of the data read. If the entry doesn't exist,
		/// then a negative value.</returns>
		int Get(DataBuffer outputBuffer, bool moveNext);
		
		/// <summary>
		/// Reads part of the value of an entry.
		/// </summary>
		/// <param name="offset">The origin within the value to begin
		/// reading.</param>
		/// <param name="outputBuffer">The part of the value read.
		/// <see cref="DataBuffer.IsWritable"/> must be <see langword="true"/>.</param>
		/// <param name="moveNext"><see langword="true"/> to move to the next
		/// entry for the read, <see langword="false"/> to read from the current
		/// entry.</param>
		/// <returns>The length of the data read. If the entry doesn't exist,
		/// then a negative value.</returns>
		int Get(int offset, DataBuffer outputBuffer, bool moveNext);

		/// <summary>
		/// Reads the value of an entry as a stream.
		/// </summary>
		/// <param name="moveNext"><see langword="true"/> to move to the next
		/// entry for the read, <see langword="false"/> to read from the current
		/// entry.</param>
		/// <returns><see cref="Stream"/> containing the value;
		/// <see langword="null"/> if the entry doesn't exist.</returns>
		/// <remarks>
		/// <para><see cref="IBinaryStorage.StreamsData"/> of <see cref="Storage"/>
		/// must be <see langword="true"/> to use this method.</para>
		/// <para>Users should retain the streams as briefly as possible, and make
		/// sure to cleanup by calling <see cref="Stream.Close"/> or
		/// <see cref="Stream.Dispose()"/> as soon as possible.</para>
		/// </remarks>
		Stream Get(bool moveNext);

		/// <summary>
		/// Reads part of the value of an entry as a stream.
		/// </summary>
		/// <param name="offset">The origin within the value to begin
		/// reading.</param>
		/// <param name="length">The length of the portion of the value to
		/// read.</param>
		/// <param name="moveNext"><see langword="true"/> to move to the next
		/// entry for the read, <see langword="false"/> to read from the current
		/// entry.</param>
		/// <returns><see cref="Stream"/> containing the value;
		/// <see langword="null"/> if the entry doesn't exist.</returns>
		/// <remarks>
		/// <para><see cref="IBinaryStorage.StreamsData"/> of <see cref="Storage"/>
		/// must be <see langword="true"/> to use this method.</para>
		/// <para>Users should retain the streams as briefly as possible, and make
		/// sure to cleanup by calling <see cref="Stream.Close"/> or
		/// <see cref="Stream.Dispose()"/> as soon as possible.</para>
		/// </remarks>
		Stream Get(int offset, int length, bool moveNext);
		
		/// <summary>
		/// Writes the value of an entry.
		/// </summary>
		/// <param name="inputBuffer">The value written.</param>
		/// <param name="isNew"><see langword="true"/> to write as a new entry,
		/// <see langword="false"/> to write the current entry.</param>
		/// <returns>The length of the value data written.</returns>
		int Put(DataBuffer inputBuffer, bool isNew);

		/// <summary>
		/// Writes part of the value of an entry.
		/// </summary>
		/// <param name="offset">The origin within the value to begin
		/// reading.</param>
		/// <param name="length">The length of the entry's data to be overwritten.</param>
		/// <param name="inputBuffer">The value data written.</param>
		/// <param name="isNew"><see langword="true"/> to write as a new entry,
		/// <see langword="false"/> to write the current entry.</param>
		/// <returns>The length of the value data written.</returns>
		int Put(int offset, int length, DataBuffer inputBuffer, bool isNew);

		/// <summary>
		/// Deletes the current entry.
		/// </summary>
		void Delete();

		/// <summary>
		/// Gets the storage from which this cursor was created.
		/// </summary>
		/// <value>The source <see cref="IBinaryStorage"/>.</value>
		IBinaryStorage Storage { get; }
	}
}
