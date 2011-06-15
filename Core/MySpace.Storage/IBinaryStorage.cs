using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;

namespace MySpace.Storage
{
	/// <summary>
	/// An <see cref="IStorage"/> for binary data.
	/// </summary>
	public interface IBinaryStorage : IStorage
	{
		/// <summary>
		/// Reads an entry's data from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="output">The data read.
		/// <see cref="DataBuffer.IsWritable"/> must be
		/// <see langword="true"/>.</param>
		/// <returns>The length of the entry. If the entry doesn't exist,
		/// then a negative value. If <paramref name="output"/> isn't long
		/// enough, then it will have data copied to it if
		/// <see cref="SupportsIncompleteReads"/> is true.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="output"/> is not <see cref="DataBuffer.IsWritable"/>.
		/// </exception>
		int Get(DataBuffer keySpace, StorageKey key, DataBuffer output);

		/// <summary>
		/// Reads part of an entry's data from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="offset">The origin within the entry's data to
		/// begin reading.</param>
		/// <param name="output">The data read.
		/// <see cref="DataBuffer.IsWritable"/> must be
		/// <see langword="true"/>.</param>
		/// <returns>The length of the data read. If the entry doesn't exist,
		/// then a negative value.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="output"/> is not <see cref="DataBuffer.IsWritable"/>.
		/// </exception>
		int Get(DataBuffer keySpace, StorageKey key, int offset, DataBuffer output);

		/// <summary>
		/// Reads an entry's data from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <returns>A <see cref="Stream"/> containing the entry data;
		/// <see langword="null"/> if the entry doesn't exist. It should
		/// report <see cref="Stream.Length"/> if possible.</returns>
		/// <remarks>
		/// <para><see cref="StreamsData"/> must be <see langword="true"/>
		/// to use this method.</para>
		/// <para>Users should retain the stream as briefly as possible, and make
		/// sure to cleanup by calling <see cref="Stream.Close"/> or
		/// <see cref="Stream.Dispose()"/> as soon as possible.</para>
		/// </remarks>
		Stream Get(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Reads part of an entry's data from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="offset">The origin within the entry's data to
		/// begin reading.</param>
		/// <param name="length">The length of the entry's data to be read.</param>
		/// <returns>A <see cref="Stream"/> containing the entry data;
		/// <see langword="null"/> if the entry doesn't exist. It should
		/// report <see cref="Stream.Length"/> if possible.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 1.</para>
		/// </exception>
		/// <remarks>
		/// <para><see cref="StreamsData"/> must be <see langword="true"/>
		/// to use this method.</para>
		/// <para>Users should retain the stream as briefly as possible, and make
		/// sure to cleanup by calling <see cref="Stream.Close"/> or
		/// <see cref="Stream.Dispose()"/> as soon as possible.</para>
		/// </remarks>
		Stream Get(DataBuffer keySpace, StorageKey key, int offset, int length);

		/// <summary>
		/// Reads an entry's data from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <returns>A <see cref="Byte"/> array containing the entry data;
		/// <see langword="null"/> if the entry doesn't exist.</returns>
		/// <remarks>
		/// <para><see cref="StreamsData"/> must be <see langword="true"/>
		/// to use this method.</para>
		/// </remarks>
		byte[] GetBuffer(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Reads part of an entry's data from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="offset">The origin within the entry's data to
		/// begin reading.</param>
		/// <param name="length">The length of the entry's data to be read.</param>
		/// <returns>A <see cref="Byte"/> array containing the entry data;
		/// <see langword="null"/> if the entry doesn't exist.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 1.</para>
		/// </exception>
		/// <remarks>
		/// <para><see cref="StreamsData"/> must be <see langword="true"/>
		/// to use this method.</para>
		/// </remarks>
		byte[] GetBuffer(DataBuffer keySpace, StorageKey key, int offset, int length);

		/// <summary>
		/// Writes an entry's data to the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="input">The data written.</param>
		/// <returns>The length of the data written.</returns>
		int Put(DataBuffer keySpace, StorageKey key, DataBuffer input);

		/// <summary>
		/// Writes part of an entry's data to the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="offset">The origin within the entry's data to
		/// begin writing.</param>
		/// <param name="length">The length of the entry's data to be overwritten.</param>
		/// <param name="input">The data written.</param>
		/// <returns>The length of the data written.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		int Put(DataBuffer keySpace, StorageKey key, int offset, int length, DataBuffer input);

		/// <summary>
		/// Deletes an entry from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <returns><see langword="true"/> if the entry existed prior;
		/// otherwise <see langword="false"/>.</returns>
		bool Delete(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Gets whether the entry exists within the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <returns><see langword="true"/> if the entry exists;
		/// otherwise <see langword="false"/>.</returns>
		bool Exists(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Gets the length of an entry within the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <returns>The length of the entry data. If the entry doesn't exist,
		/// then a negative value.</returns>
		int GetLength(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Gets a cursor that iterates over all entries within a key space
		/// for a particular key.
		/// </summary>
		/// <param name="keySpace">The key space within which the iteration is
		/// performed.</param>
		/// <param name="key">The key of the entries to iterate over.</param>
		/// <returns>An <see cref="IBinaryCursor"/> used for iteration.</returns>
		IBinaryCursor GetCursor(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Clears all entries of a particular key space.
		/// </summary>
		/// <param name="keySpace">The key space of the entries to be cleared.</param>
		/// <returns>The <see cref="Int32"/> count of objects deleted. Will be
		/// negative if the implementation doesn't report that implementation.</returns>
		int Clear(DataBuffer keySpace);

		/// <summary>
		/// Gets whether or not multiple entries under the same key are supported by
		/// the store.
		/// </summary>
		/// <param name="keySpace">The <see cref="DataBuffer"/> specifying the
		/// keyspace.</param>
		/// <returns><see langword="true"/> if supported; otherwise <see langword="false"/>.</returns>
		/// <remarks>If allowed, multiple entries are accessed via <see cref="IBinaryCursor"/>.</remarks>
		bool GetAllowsMultiple(DataBuffer keySpace);

		/// <summary>
		/// Occurs when an entry is dropped from the store. Does not occur
		/// for deletions initiated by client called deletions, but only when the
		/// store itself drops the entry, for example due to lack of space or time
		/// based expiration.
		/// </summary>
		event EventHandler<BinaryEventArgs> Dropped;

		/// <summary>
		/// Gets whether this store allows data to be streamed out, in addition to
		/// methods that require a read buffer to be provided by the caller.
		/// </summary>
		/// <value><see langword="true"/> if supported; otherwise <see langword="false"/>.</value>
		/// <remarks><para>The following methods can only be called if
		/// <see langword="true"/>:</para>
		/// <para><see cref="Get(DataBuffer, StorageKey)"/></para>
		/// <para><see cref="Get(DataBuffer, StorageKey, Int32, Int32)"/></para>
		/// <para><see cref="GetBuffer(DataBuffer, StorageKey)"/></para>
		/// <para><see cref="GetBuffer(DataBuffer, StorageKey, Int32, Int32)"/></para>
		/// </remarks>
		bool StreamsData { get; }

		/// <summary>
		/// Gets whether this store will copy data up to the available space when
		/// a read buffer is provided that is too small for the entry.
		/// </summary>
		bool SupportsIncompleteReads { get; }
	}
}
