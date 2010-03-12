using System;
using System.IO;
using BerkeleyDbWrapper;
using MySpace.Common.Storage;

namespace MySpace.BerkeleyDb.Facade
{
	/// <summary>
	/// The main class for BerkeleyDb.
	/// </summary>
	public partial class BerkeleyDbStorage
	{
		#region Logging

		private static void DebugLog(string callDescr, int typeId, int objectId)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("[{0} (TypeId={1}, ObjectId={2})", callDescr,
					typeId, objectId);
			}
		}

		private static void ErrorLog(string callDescr, Exception ex)
		{
			if (Log.IsErrorEnabled)
			{
				Log.Error(callDescr, ex);
			}
		}
		#endregion

		#region MemoryPool

		/// <summary>
		/// Represents an operations that takes a binary buffer and returns the length
		/// of the portion of the buffer need.
		/// </summary>
		/// <param name="buffer">The <see cref="Byte"/> array buffer.</param>
		/// <returns>The <see cref="Int32"/> length of the need buffer portion.</returns>
		public delegate int BufferUse(byte[] buffer);

		/// <summary>
		/// Performs an operation using <see cref="BerkeleyDbStorage"/>'s built in
		/// pool of resizable buffers. If the operation returns a needed length greater
		/// than the initial buffer length, then the operation is repeated with an
		/// adequately sized buffer.
		/// </summary>
		/// <param name="requestedCapacity">The initial minimum buffer length.</param>
		/// <param name="dlg">The operation as a <see cref="BufferUse"/>.</param>
		/// <returns>An allocated <see cref="Byte"/> array containing the results of the
		/// operation, copied from the buffer and sized by the return length from
		/// <paramref name="dlg"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dlg"/> is null.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="requestedCapacity"/> is negative.
		/// </exception>
		public byte[] UsingMemoryPool(int requestedCapacity, BufferUse dlg)
		{
			if (requestedCapacity <= 0) throw new ArgumentOutOfRangeException("requestedCapacity");
			if (dlg == null) throw new ArgumentNullException("dlg");
			using (var itm = memoryPoolStream.GetItem())
			{
				var stm = itm.Item;
				if (stm.Capacity < requestedCapacity)
				{
					stm.Capacity = requestedCapacity;
				}
				int size;
				byte[] buffer;
				while(true)
				{
					buffer = stm.GetBuffer();
					try
					{
						size = dlg(buffer);
					}
					catch (BufferSmallException exc)
					{
						size = (int)exc.RecordLength;
					}
					if (size > buffer.Length)
					{
						stm.Capacity = size;
					} else
					{
						break;
					}
				}
				if (size < 0) return null;
				var ret = new byte[size];
				if (size > 0)
				{
					Array.Copy(buffer, ret, size);
				}
				return ret;
			}
		}

		/// <summary>
		/// Performs an operation using <see cref="BerkeleyDbStorage"/>'s built in
		/// pool of resizable buffers. If the operation returns a needed length greater
		/// than the initial buffer length, then the operation is repeated with an
		/// adequately sized buffer.
		/// </summary>
		/// <param name="dlg">The operation as a <see cref="BufferUse"/>.</param>
		/// <returns>An allocated <see cref="Byte"/> array containing the results of the
		/// operation, copied from the buffer and sized by the return length from
		/// <paramref name="dlg"/>.</returns>
		/// <remarks>The initial buffer size is at least as big as the
		/// default buffer size.</remarks>
		public byte[] UsingMemoryPool(BufferUse dlg)
		{
			return UsingMemoryPool(initialBufferSize, dlg);
		}
		#endregion

		/// <summary>
		/// Reads data from a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="buffer">The buffer to which the read data is written.</param>
		/// <param name="options">The options for the read.</param>
		/// <returns>The length of the entry in the store.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="buffer"/> has an <see cref="DataBuffer.IsWritable"/> of
		/// <see langword="false"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="options"/> has a negative offset.</para>
		/// </exception>
		/// <remarks>
		/// <para>If the entry is longer than <paramref name="buffer"/>, then
		/// <paramref name="buffer"/> is written to its full capacity, but the
		/// entry length is still returned.
		/// </para>
		/// <para>Return value is negative if</para>
		/// <para>Entry is not found in store.</para>
		/// <para>-or-</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public int GetEntry(short typeId, int objectId, DataBuffer key, DataBuffer buffer,
			GetOptions options)
		{
			if (!buffer.IsWritable) throw new ArgumentOutOfRangeException("buffer");
			options.AssertValid("options");
			if (!CanProcessMessage(typeId)) return -1;
			DebugLog("GetEntry()", typeId, objectId);
			Database db = GetDatabase(typeId, objectId);
			try
			{
				return db.Get(key, options.Offset, buffer, options.Flags);
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
				return -1;
			}
			catch (Exception ex)
			{
				ErrorLog("GetEntry()", ex);
				throw;
			}
		}

		/// <summary>
		/// Reads data from a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="options">The options for the read.</param>
		/// <returns>A <see cref="Stream"/> that accesses
		/// the entry data via unmanaged memory allocated from BerkeleyDb.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="options"/> has a negative offset.</para>
		/// </exception>
		/// <remarks>
		/// <para></para>
		/// <para>Return value is null if</para>
		/// <para>Entry is not found in store.</para>
		/// <para>-or-</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public Stream GetEntryStream(short typeId, int objectId,
			DataBuffer key, GetOptions options)
		{
			options.AssertValid("options");
			if (!CanProcessMessage(typeId)) return null;
			DebugLog("GetEntry()", typeId, objectId);
			Database db = GetDatabase(typeId, objectId);
			try
			{
				return db.Get(key, options.Offset, options.Length, options.Flags);
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
				return null;
			}
			catch (Exception ex)
			{
				ErrorLog("GetEntry()", ex);
				throw;
			}
		}

		/// <summary>
		/// Reads data from a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="buffer">The buffer to which the read data is written.</param>
		/// <param name="options">The options for the read.</param>
		/// <returns>The length of the entry in the store.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="buffer"/> has an <see cref="DataBuffer.IsWritable"/> of
		/// <see langword="false"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="options"/> has a negative offset.</para>
		/// </exception>
		/// <remarks>
		/// <para><see cref="DataBuffer.GetHashCode"/> of <paramref name="key"/> is used
		/// as the object id.</para>
		/// <para>If the entry is longer than <paramref name="buffer"/>, then
		/// <paramref name="buffer"/> is written to its full capacity, but the
		/// entry length is still returned.
		/// </para>
		/// <para>Return value is negative if</para>
		/// <para>Entry is not found in store.</para>
		/// <para>-or-</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public int GetEntry(short typeId, DataBuffer key, DataBuffer buffer,
			GetOptions options)
		{
			return GetEntry(typeId, key.GetObjectId(), key, buffer, options);
		}

		/// <summary>
		/// Reads data from a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="options">The options for the read.</param>
		/// <returns>A <see cref="Stream"/> that accesses
		/// the entry data via unmanaged memory allocated from BerkeleyDb.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="options"/> has a negative offset.</para>
		/// </exception>
		/// <remarks>
		/// <para>Return value is null if</para>
		/// <para>Entry is not found in store.</para>
		/// <para>-or-</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public Stream GetEntryStream(short typeId, DataBuffer key,
			GetOptions options)
		{
			return GetEntryStream(typeId, key.GetObjectId(), key, options);
		}

		/// <summary>
		/// Reads data from a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="buffer">The buffer to which the read data is written.</param>
		/// <returns>The length of the entry in the store.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="buffer"/> has an <see cref="DataBuffer.IsWritable"/> of
		/// <see langword="false"/>.</para>
		/// </exception>
		/// <remarks>
		/// <para>If the entry is longer than <paramref name="buffer"/>, then
		/// <paramref name="buffer"/> is written to its full capacity, but the
		/// entry length is still returned.
		/// </para>
		/// <para>Return value is negative if</para>
		/// <para>Entry is not found in store.</para>
		/// <para>-or-</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public int GetEntry(short typeId, int objectId, DataBuffer key, DataBuffer buffer)
		{
			return GetEntry(typeId, objectId, key, buffer, GetOptions.Default);
		}

		/// <summary>
		/// Reads data from a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <returns>A <see cref="Stream"/> that accesses
		/// the entry data via unmanaged memory allocated from BerkeleyDb.</returns>
		/// <remarks>
		/// <para>Return value is null if</para>
		/// <para>Entry is not found in store.</para>
		/// <para>-or-</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public Stream GetEntryStream(short typeId, int objectId,
			DataBuffer key)
		{
			return GetEntryStream(typeId, objectId, key, GetOptions.Default);
		}

		/// <summary>
		/// Reads data from a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="buffer">The buffer to which the read data is written.</param>
		/// <returns>The length of the entry in the store.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="buffer"/> has an <see cref="DataBuffer.IsWritable"/> of
		/// <see langword="false"/>.</para>
		/// </exception>
		/// <remarks>
		/// <para><see cref="DataBuffer.GetHashCode"/> of <paramref name="key"/> is used
		/// as the object id.</para>
		/// <para>If the entry is longer than <paramref name="buffer"/>, then
		/// <paramref name="buffer"/> is written to its full capacity, but the
		/// entry length is still returned.
		/// </para>
		/// <para>Return value is negative if</para>
		/// <para>Entry is not found in store.</para>
		/// <para>-or-</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public int GetEntry(short typeId, DataBuffer key, DataBuffer buffer)
		{
			return GetEntry(typeId, key, buffer, GetOptions.Default);
		}

		/// <summary>
		/// Reads data from a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <returns>A <see cref="Stream"/> that accesses
		/// the entry data via unmanaged memory allocated from BerkeleyDb.</returns>
		/// <remarks>
		/// <para>Return value is null if</para>
		/// <para>Entry is not found in store.</para>
		/// <para>-or-</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public Stream GetEntryStream(short typeId, DataBuffer key)
		{
			return GetEntryStream(typeId, key, GetOptions.Default);
		}

		/// <summary>
		/// Writes data to a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="buffer">The buffer supplying the write data.</param>
		/// <param name="options">The options for the read.</param>
		/// <returns>Whether the write succeeded.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="options"/> has a negative offset.</para>
		/// </exception>
		/// <remarks>
		/// <para>Return value is <see langword="false"/> if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public bool SaveEntry(short typeId, int objectId, DataBuffer key, DataBuffer buffer,
			PutOptions options)
		{
			options.AssertValid("options");
			if (!CanProcessMessage(typeId)) return false;
			DebugLog("SaveEntry()", typeId, objectId);
			Database db = GetDatabase(typeId, objectId);
			try
			{
				var size = db.Put(key, options.Offset, options.Length, buffer,
					options.Flags);
				if (size != buffer.ByteLength)
					throw new BdbException((int)DbRetVal.LENGTHMISMATCH, string.Format(
						"Expected save length of {0}, got {1}", buffer.ByteLength,
						size));
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
				return false;
			}
			catch (Exception ex)
			{
				ErrorLog("SaveEntry()", ex);
				throw;
			}
			return true;
		}

		/// <summary>
		/// Writes data to a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="buffer">The buffer supplying the write data.</param>
		/// <param name="options">The options for the read.</param>
		/// <returns>Whether the write succeeded.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="options"/> has a negative offset.</para>
		/// </exception>
		/// <remarks>
		/// <para><see cref="DataBuffer.GetHashCode"/> of <paramref name="key"/> is used
		/// as the object id.</para>
		/// <para>Return value is <see langword="false"/> if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public bool SaveEntry(short typeId, DataBuffer key, DataBuffer buffer,
			PutOptions options)
		{
			return SaveEntry(typeId, key.GetObjectId(), key, buffer, options);
		}

		/// <summary>
		/// Writes data to a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="buffer">The buffer supplying the write data.</param>
		/// <returns>Whether the write succeeded.</returns>
		/// <remarks>
		/// <para>Return value is <see langword="false"/> if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public bool SaveEntry(short typeId, int objectId, DataBuffer key, DataBuffer buffer)
		{
			return SaveEntry(typeId, objectId, key, buffer, PutOptions.Default);
		}

		/// <summary>
		/// Writes data to a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <param name="buffer">The buffer supplying the write data.</param>
		/// <returns>Whether the write succeeded.</returns>
		/// <remarks>
		/// <para><see cref="DataBuffer.GetHashCode"/> of <paramref name="key"/> is used
		/// as the object id.</para>
		/// <para>Return value is <see langword="false"/> if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public bool SaveEntry(short typeId, DataBuffer key, DataBuffer buffer)
		{
			return SaveEntry(typeId, key, buffer, PutOptions.Default);
		}

		/// <summary>
		/// Deletes a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <returns>Whether the deletion succeeded.</returns>
		/// <remarks>
		/// <para>Return value is <see langword="false"/> if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>The entry specified by <paramref name="key"/> didn't exist within
		/// the store.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public bool DeleteEntry(short typeId, int objectId, DataBuffer key)
		{
			if (!CanProcessMessage(typeId)) return false;
			DebugLog("DeleteEntry()", typeId, objectId);
			Database db = GetDatabase(typeId, objectId);
			try
			{
				return db.Delete(key, DeleteOpFlags.Default);
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
				return false;
			}
			catch (Exception ex)
			{
				ErrorLog("DeleteEntry()", ex);
				throw;
			}
		}

		/// <summary>
		/// Deletes a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <returns>Whether the deletion succeeded.</returns>
		/// <remarks>
		/// <para><see cref="DataBuffer.GetHashCode"/> of <paramref name="key"/> is used
		/// as the object id.</para>
		/// <para>Return value is <see langword="false"/> if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>The entry specified by <paramref name="key"/> didn't exist within
		/// the store.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public bool DeleteEntry(short typeId, DataBuffer key)
		{
			return DeleteEntry(typeId, key.GetObjectId(), key);
		}

		/// <summary>
		/// Gets whether a BerkeleyDb store entry exists.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <returns>Whether the entry exists.</returns>
		/// <remarks>
		/// <para>Return value is <see langword="false"/> if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>The entry specified by <paramref name="key"/> didn't exist within
		/// the store.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public bool EntryExists(short typeId, int objectId, DataBuffer key)
		{
			if (!CanProcessMessage(typeId)) return false;
			DebugLog("EntryExists()", typeId, objectId);
			Database db = GetDatabase(typeId, objectId);
			var ret = db.Exists(key, ExistsOpFlags.Default);
			try
			{
				switch (ret)
				{
					case DbRetVal.SUCCESS:
						return true;
					case DbRetVal.NOTFOUND:
					case DbRetVal.KEYEMPTY:
						return false;
					default:
						throw new ApplicationException(string.Format(
							"EntryExists got unexpected value {0}", ret));
				}
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
				return false;
			}
			catch (Exception ex)
			{
				ErrorLog("EntryExists()", ex);
				throw;
			}
		}

		/// <summary>
		/// Gets whether a BerkeleyDb store entry exists.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <returns>Whether the entry exists.</returns>
		/// <remarks>
		/// <para><see cref="DataBuffer.GetHashCode"/> of <paramref name="key"/> is used
		/// as the object id.</para>
		/// <para>Return value is <see langword="false"/> if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>The entry specified by <paramref name="key"/> didn't exist within
		/// the store.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public bool EntryExists(short typeId, DataBuffer key)
		{
			return EntryExists(typeId, key.GetObjectId(), key);
		}

		/// <summary>
		/// Gets the length of a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="objectId">The object id used for store access.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <returns>The length of the entry.</returns>
		/// <remarks>
		/// <para>Return value is negative if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>The entry specified by <paramref name="key"/> didn't exist within
		/// the store.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public int GetEntryLength(short typeId, int objectId, DataBuffer key)
		{
			if (!CanProcessMessage(typeId)) return -1;
			DebugLog("GetEntryLength()", typeId, objectId);
			Database db = GetDatabase(typeId, objectId);
			try
			{
				return db.GetLength(key, GetOpFlags.Default);
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
				return -1;
			}
			catch (Exception ex)
			{
				ErrorLog("GetEntryLength()", ex);
				throw;
			}
		}

		/// <summary>
		/// Gets the length of a BerkeleyDb store entry.
		/// </summary>
		/// <param name="typeId">The type of the store accessed.</param>
		/// <param name="key">The key of the store entry accessed.</param>
		/// <returns>The length of the entry.</returns>
		/// <remarks>
		/// <para><see cref="DataBuffer.GetHashCode"/> of <paramref name="key"/> is used
		/// as the object id.</para>
		/// <para>Return value is negative if:</para>
		/// <para><paramref name="typeId"/> isn't valid.</para>
		/// <para>-or-</para>
		/// <para>The entry specified by <paramref name="key"/> didn't exist within
		/// the store.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="BdbException"/> was thrown from the underlying store
		/// (Exception is logged but not rethrown).</para>
		/// </remarks>
		public int GetEntryLength(short typeId, DataBuffer key)
		{
			return GetEntryLength(typeId, key.GetObjectId(), key);
		}
	}
}
