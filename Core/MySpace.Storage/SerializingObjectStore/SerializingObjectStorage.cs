using System;
using System.Collections.Generic;
using System.IO;
using MySpace.Common.HelperObjects;
using MySpace.Common.Storage;
using MySpace.ResourcePool;
using Serializer = MySpace.Common.IO.Serializer;

namespace MySpace.Storage
{
	/// <summary>
	/// Object store that serializes to an underlying binary store.
	/// </summary>
	internal sealed class SerializingObjectStorage : IObjectStorage
	{
		/// <summary>
		/// Gets the pool of memory streams to use for serialization.
		/// </summary>
		/// <value>A <see cref="MemoryStreamPool"/> to use.</value>
		internal MemoryStreamPool StreamPool { get; private set; }

		/// <summary>
		/// Gets the underlying binary store.
		/// </summary>
		/// <value>The underlying <see cref="IBinaryStorage"/>.</value>
		internal IBinaryStorage Storage { get; private set; }

		private bool _allowsDrops;

		private Dictionary<DataBuffer, KeyspaceInfo> _keyspaceInfos;

		private MsReaderWriterLock _keyspaceInfoLock;

		private void AddKeySpaceInfo<T>(DataBuffer keySpace)
		{
			if (!_allowsDrops) return;
			_keyspaceInfoLock.ReadUpgradable(() =>
			{
				if (!_keyspaceInfos.ContainsKey(keySpace))
				{
					_keyspaceInfoLock.Write(() =>
					{
						_keyspaceInfos[keySpace] = KeyspaceInfo.Create<T>();
					});
				}
			});
		}

		private void AddKeySpaceInfoForList<T, THeader>(DataBuffer keySpace)
		{
			if (!_allowsDrops) return;
			_keyspaceInfoLock.ReadUpgradable(() =>
			{
				if (!_keyspaceInfos.ContainsKey(keySpace))
				{
					_keyspaceInfoLock.Write(() =>
					{
						_keyspaceInfos[keySpace] = KeyspaceInfo.CreateForList<T, THeader>(
							Storage.GetAllowsMultiple(keySpace));
					});
				}
			});
		}

		private KeyspaceInfo GetKeySpaceInfo(DataBuffer keySpace)
		{
			KeyspaceInfo ret = null;
			_keyspaceInfoLock.Read(() =>
				_keyspaceInfos.TryGetValue(keySpace, out ret));
			return ret;
		}

		#region IObjectStorage Implementation
		private const int _headerLength = 2 * sizeof(long);

		/// <summary>
		/// Creates a buffer from a memory stream for reading from a binary store.
		/// </summary>
		/// <remarks>The buffer encapsulates the entire allocated capacity of
		/// the memory stream to allow the largest possible entries to be read
		/// in their entirety.</remarks>
		/// <param name="stream">The <see cref="MemoryStream"/> to use.</param>
		/// <returns>A new <see cref="DataBuffer"/> that encapsulates the entire
		/// <see cref="MemoryStream.Capacity"/> of <paramref name="stream"/>.</returns>
		internal static DataBuffer GetReadBuffer(MemoryStream stream)
		{
			var cap = stream.Capacity;
			stream.SetLength(cap);
			var data = stream.GetBuffer();
			var len = data.Length;
			var origin = len - cap;
			len -= origin;
			return DataBuffer.Create(data, origin, len);
		}

		/// <summary>
		/// Creates a buffer from a memory stream for writing to a binary store.
		/// </summary>
		/// <remarks>The buffer encapsulates the entire length of the the memory
		/// stream so that the entire contents are written.</remarks>
		/// <param name="stream">The <see cref="MemoryStream"/> to use.</param>
		/// <returns>A new <see cref="DataBuffer"/> that encapsulates the entire
		/// <see cref="MemoryStream.Length"/> of <paramref name="stream"/>.</returns>
		internal static DataBuffer GetWriteBuffer(MemoryStream stream)
		{
			var data = stream.GetBuffer();
			var len = data.Length;
			var origin = len - stream.Capacity;
			return DataBuffer.Create(data, origin, (int)stream.Position);
		}

		/// <summary>
		/// Creates a buffer from a memory stream for reading remaining data
		/// from a binary store after an initial read with a too short buffer.
		/// </summary>
		/// <remarks>A binary store entry can be larger than the buffer used for
		/// reading. This method will create a buffer to read the remainder, if
		/// any is needed.</remarks>
		/// <param name="stream">The <see cref="MemoryStream"/> to use. This
		/// should be the same as the one used for the initial read, so the
		/// remainder is appended directly.</param>
		/// <param name="buffer">The <see cref="DataBuffer"/> used for the initial
		/// read. Normally, this would be the returned from <see cref="GetReadBuffer"/>.</param>
		/// <param name="entryLen">The length of the entry, obtained from the return of
		/// <see cref="IBinaryStorage.Get(DataBuffer, StorageKey, DataBuffer)"/>.</param>
		/// <returns><para>If a second read is needed, i.e. if <paramref name="entryLen"/> is
		/// greater than <see cref="DataBuffer.ByteLength"/> of <paramref name="buffer"/>,
		/// then then <paramref name="stream"/> has the necessary capacity increased and
		/// a <see cref="DataBuffer"/> encapsulating the segment of data for the remaining
		/// entry portion is returned.</para>
		/// <para>Otherwise, <see cref="DataBuffer.Empty"/> is returned.</para></returns>
		internal static DataBuffer GetExcessBuffer(MemoryStream stream, DataBuffer buffer, int entryLen)
		{
			var excess = entryLen - buffer.ByteLength;
			if (excess > 0)
			{
				stream.Capacity += excess;
				stream.SetLength(entryLen);
			}
			else
			{
				stream.SetLength(entryLen);
				return DataBuffer.Empty;
			}
			return DataBuffer.Create(stream.GetBuffer(), buffer.Offset, entryLen);
		}

		private StorageEntry<T> GetCore<T>(DataBuffer buffer, MemoryStream stream, T instance)
		{
			var seg = buffer.ByteArraySegmentValue;
			if (stream == null)
			{
				stream = new MemoryStream(seg.Array, seg.Offset, seg.Count);
			}
			var ticks = BitConverter.ToInt64(seg.Array, seg.Offset);
			// expires listed first, since SetExpires can change it by itself
			// and this way it doesn't have to worry about future changes
			// to header
			var expires = new DateTime(ticks);
			ticks = BitConverter.ToInt64(seg.Array, seg.Offset + sizeof(long));
			var updated = new DateTime(ticks);
			stream.Position = _headerLength;
			Serializer.Deserialize(stream, instance);
			return new StorageEntry<T>(instance, updated, expires);
		}

		private MemoryStream GetStream(ResourcePoolItem<MemoryStream> item, DataBuffer keySpace,
			StorageKey key, out DataBuffer buffer)
		{
			var stream = item.Item;
			buffer = GetReadBuffer(stream);
			var entryLen = Storage.Get(keySpace, key, buffer);
			if (entryLen < 0)
			{
				return null;
			}
			var bfExcess = GetExcessBuffer(stream, buffer, entryLen);
			if (bfExcess.IsEmpty)
			{
				buffer = buffer.Restrict(entryLen);
			}
			else
			{
				if (Storage.SupportsIncompleteReads)
				{
					Storage.Get(keySpace, key, buffer.Length,
						bfExcess.RestrictOffset(buffer.Length));
				}
				else
				{
					Storage.Get(keySpace, key, 0, bfExcess);
				}
				buffer = bfExcess;
			}
			return stream;
		}

		/// <summary>
		/// 	<para>Reads an object from the store.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of the object.</para>
		/// </typeparam>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <param name="instance">
		/// 	<para>An empty instance for stores that use
		///		deserialization.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="StorageEntry{T}"/> containing the entry.</para>
		/// </returns>
		public StorageEntry<T> Get<T>(DataBuffer keySpace, StorageKey key, T instance)
		{
			if (Storage.StreamsData)
			{
				var data = Storage.GetBuffer(keySpace, key);
				if (data == null)
				{
					return StorageEntry<T>.NotFound;
				}
				DataBuffer buffer = data;
				using(var stream = new MemoryStream(data, false))
				{
					return GetCore(buffer, stream, instance);
				}
			}
			else
			{
				using (var item = StreamPool.GetItem())
				{
					DataBuffer buffer;
					var stream = GetStream(item, keySpace, key, out buffer);
					if (stream == null)
					{
						return StorageEntry<T>.NotFound;
					}
					return GetCore(buffer, stream, instance);
				}					
			}
		}

		/// <summary>
		/// 	<para>Reads an object from the store.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of the object.</para>
		/// </typeparam>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <param name="creator">
		/// 	<para>Delegate that provides an empty instance for
		///		deserialization.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="StorageEntry{T}"/> containing the entry.</para>
		/// </returns>
		public StorageEntry<T> Get<T>(DataBuffer keySpace, StorageKey key, Func<T> creator)
		{
			if (Storage.StreamsData)
			{
				var data = Storage.GetBuffer(keySpace, key);
				if (data == null)
				{
					return StorageEntry<T>.NotFound;
				}
				DataBuffer buffer = data;
				using (var stream = new MemoryStream(data, false))
				{
					return GetCore(buffer, stream, creator());
				}
			}
			else
			{
				using (var item = StreamPool.GetItem())
				{
					DataBuffer buffer;
					var stream = GetStream(item, keySpace, key, out buffer);
					if (stream == null)
					{
						return StorageEntry<T>.NotFound;
					}
					return GetCore(buffer, stream, creator());
				}
			}
		}

		/// <summary>
		/// 	<para>Reads an object from the store, or creates and stores an object if not found.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of the object.</para>
		/// </typeparam>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <param name="creator">
		/// 	<para>A <see cref="Func{StorageEntry}"/> that returns the <see cref="StorageEntry{T}"/> to store and return if not found. Can also be called to provide instance for deserialization for serializing stores.</para>
		/// </param>
		/// <returns>
		/// 	<para>The object read or created.</para>
		/// </returns>
		public StorageEntry<T> GetOrCreate<T>(DataBuffer keySpace, StorageKey key, Func<StorageEntry<T>> creator)
		{
			var newEntry = creator();
			var entry = Get(keySpace, key, () => newEntry.Instance);
			if (!entry.IsFound)
			{
				entry = newEntry;
				Put(keySpace, key, entry);
			}
			return entry;
		}

		/// <summary>
		/// 	<para>Writes an object to the store.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of the object.</para>
		/// </typeparam>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <param name="entry">
		/// 	<para>The entry to write.</para>
		/// </param>
		public void Put<T>(DataBuffer keySpace, StorageKey key, StorageEntry<T> entry)
		{
			AddKeySpaceInfo<T>(keySpace);
			using (var item = StreamPool.GetItem())
			{
				var stream = item.Item;
				stream.Write(BitConverter.GetBytes(entry.Expires.Ticks), 0, sizeof(long));
				stream.Write(BitConverter.GetBytes(entry.Updated.Ticks), 0, sizeof(long));
				Serializer.Serialize(stream, entry.Instance);
				Storage.Put(keySpace, key, GetWriteBuffer(stream));
			}
		}

		/// <summary>
		/// 	<para>Deletes an object from the store.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <returns>
		/// 	<para>
		/// 		<see langword="true"/> if the entry existed prior; otherwise <see langword="false"/>.</para>
		/// </returns>
		public bool Delete(DataBuffer keySpace, StorageKey key)
		{
			return Storage.Delete(keySpace, key);
		}

		/// <summary>
		/// 	<para>Deletes an object from the store by version.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <param name="updated">
		/// 	<para>The last updated <see cref="DateTime"/> of the version to be deleted. If the entry's last updated date is not newer than this value, then the entry will be deleted, otherwise not.</para>
		/// </param>
		/// <returns>
		/// 	<para>
		/// 		<see langword="true"/> if the entry was deleted; otherwise <see langword="false"/>. The entry is deleted if it existed prior and was as old or older than <paramref name="updated"/>.</para>
		/// </returns>
		public bool DeleteVersion(DataBuffer keySpace, StorageKey key, DateTime updated)
		{
			using (var item = StreamPool.GetItem())
			{
				var stream = item.Item;
				if (stream.Capacity < sizeof(long))
				{
					stream.Capacity = sizeof(long);
				}
				var buffer = GetReadBuffer(stream);
				buffer = buffer.Restrict(sizeof(long));
				var len = Storage.Get(keySpace, key, sizeof(long), buffer);
				if (len < 0) return false;
				var seg = buffer.ByteArraySegmentValue;
				var ticks = BitConverter.ToInt64(seg.Array, seg.Offset);
				var entryUpdated = new DateTime(ticks);
				if (entryUpdated <= updated)
				{
					return Storage.Delete(keySpace, key);
				}
				return false;
			}
		}

		/// <summary>
		/// 	<para>Gets whether or not the object exists within the store.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <returns>
		/// 	<para>
		/// 		<see langword="true"/> if the entry exists; otherwise <see langword="false"/>.</para>
		/// </returns>
		public bool Exists(DataBuffer keySpace, StorageKey key)
		{
			return Storage.Exists(keySpace, key);
		}

		/// <summary>
		/// 	<para>Gets the expiration time of an object.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="Nullable{DateTime}"/> containing the expiration time if found; otherwise <see langword="null"/>.</para>
		/// </returns>
		public DateTime? GetExpires(DataBuffer keySpace, StorageKey key)
		{
			using (var item = StreamPool.GetItem())
			{
				var stream = item.Item;
				if (stream.Capacity < sizeof(long))
				{
					stream.Capacity = sizeof(long);
				}
				var buffer = GetReadBuffer(stream);
				buffer = buffer.Restrict(sizeof(long));
				var len = Storage.Get(keySpace, key, 0, buffer);
				if (len < 0) return null;
				var seg = buffer.ByteArraySegmentValue;
				var ticks = BitConverter.ToInt64(seg.Array, seg.Offset);
				return new DateTime(ticks);
			}
		}

		/// <summary>
		/// 	<para>Sets the expiration time of an object.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the entry.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the entry.</para>
		/// </param>
		/// <param name="expires">
		/// 	<para>The expires <see cref="DateTime"/>.</para>
		/// </param>
		/// <returns>
		/// 	<para>
		/// 		<see langword="true"/> if the entry exists so its time to live could be set; otherwise <see langword="false"/>.</para>
		/// </returns>
		public bool SetExpires(DataBuffer keySpace, StorageKey key, DateTime expires)
		{
			if (!Exists(keySpace, key)) return false;
			Storage.Put(keySpace, key, 0, sizeof(long), BitConverter.GetBytes(
				expires.Ticks));
			return true;
		}

		/// <summary>
		/// 	<para>Clears all entries of a particular key space.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the entries to be cleared.</para>
		/// </param>
		public void Clear(DataBuffer keySpace)
		{
			Storage.Clear(keySpace);
		}

		/// <summary>
		/// 	<para>Gets a list of objects from the store.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of the objects in the list.</para>
		/// </typeparam>
		/// <typeparam name="THeader">
		/// 	<para>The type of the list's header information.</para>
		/// </typeparam>
		/// <param name="keySpace">
		/// 	<para>The key space of the list.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the list.</para>
		/// </param>
		/// <param name="creator">
		/// 	<para>Delegate that provides an empty instance for stores that use
		///		serialization. May be <see langword="null"/> for stores if
		///		serialization not used or if store can create empty instances of
		/// 	<typeparamref name="T"/> on its own.</para>
		/// </param>
		/// <param name="headerCreator">
		/// 	<para>Delegate that provides an empty header instance for stores that use
		///		serialization. May be <see langword="null"/> for stores if
		///		serialization not used or if store can create empty instances of
		/// 	<typeparamref name="THeader"/> on its own.</para>
		/// </param>
		/// <returns>
		/// 	<para>The <see cref="StorageEntry{IObjectList}"/> containing the fetched list.</para>
		/// </returns>
		public StorageEntry<IObjectList<T, THeader>> GetList<T, THeader>(DataBuffer keySpace, StorageKey key,
			Func<T> creator, Func<THeader> headerCreator)
		{
			AddKeySpaceInfoForList<T, THeader>(keySpace);
			if (!Storage.Exists(keySpace, key))
			{
				return StorageEntry<IObjectList<T, THeader>>.NotFound;
			}
			if (Storage.GetAllowsMultiple(keySpace))
			{
				var list = new ObjectListForMultiples<T, THeader>(this,
					keySpace, key, creator, headerCreator);
				return new StorageEntry<IObjectList<T, THeader>>(list,
					DateTime.MinValue, list.Expires);
			}
			else
			{
				var list = new ObjectListForSingles<T, THeader>(this,
					keySpace, key, DataBuffer.Empty, creator, headerCreator);
				return new StorageEntry<IObjectList<T, THeader>>(list,
					DateTime.MinValue, list.Expires);
			}
		}

		/// <summary>
		/// 	<para>Creates a new empty list of objects and writes it to the store.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of the objects in the list.</para>
		/// </typeparam>
		/// <typeparam name="THeader">
		/// 	<para>The type of the list's header information.</para>
		/// </typeparam>
		/// <param name="keySpace">
		/// 	<para>The key space of the list.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the list.</para>
		/// </param>
		/// <param name="header">
		/// 	<para>A<typeparamref name="THeader"/> containing the
		/// list's header information.</para></param>
		/// <param name="expires">
		/// 	<para>The expiration <see cref="DateTime"/> of the list.</para>
		/// </param>
		/// <param name="creator">
		/// 	<para>Delegate that provides an empty instance for stores that use
		///		serialization. May be <see langword="null"/> for stores if
		///		serialization not used or if store can create empty instances of
		/// 	<typeparamref name="T"/> on its own.</para>
		/// </param>
		/// <returns>
		/// 	<para>The created <see cref="IObjectList{T, THeader}"/>. If a list already exists in the specified <paramref name="keySpace"/> and <paramref name="key"/>, that list is discarded.</para>
		/// </returns>
		public IObjectList<T, THeader> CreateList<T, THeader>(DataBuffer keySpace, StorageKey key, THeader header, DateTime expires,
			Func<T> creator)
		{
			AddKeySpaceInfoForList<T, THeader>(keySpace);
			if (Storage.GetAllowsMultiple(keySpace))
			{
				return new ObjectListForMultiples<T, THeader>(this,
					keySpace, key, creator, header, expires);
			}
			else
			{
				return new ObjectListForSingles<T, THeader>(this,
					keySpace, key, creator, header, expires);
			}
		}

		/// <summary>
		/// 	<para>Gets a list of objects from the store, or creates one if the list is not found.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of the objects in the list.</para>
		/// </typeparam>
		/// <typeparam name="THeader">
		/// 	<para>The type of the list's header information.</para>
		/// </typeparam>
		/// <param name="keySpace">
		/// 	<para>The key space of the list.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the list.</para>
		/// </param>
		/// <param name="header">
		/// 	<para>A<typeparamref name="THeader"/> containing the
		/// list's header information.</para></param>
		/// <param name="expires">
		/// 	<para>The expiration <see cref="DateTime"/> to use if the list must be created.</para>
		/// </param>
		/// <param name="creator">
		/// 	<para>Delegate that provides an empty instance for stores that use
		///		serialization. May be <see langword="null"/> for stores if
		///		serialization not used or if store can create empty instances of
		/// 	<typeparamref name="T"/> on its own.</para>
		/// </param>
		/// <param name="headerCreator">
		/// 	<para>Delegate that provides an empty header instance for stores that use
		///		serialization. May be <see langword="null"/> for stores if
		///		serialization not used or if store can create empty instances of
		/// 	<typeparamref name="THeader"/> on its own.</para>
		/// </param>
		/// <returns>
		/// 	<para>The <see cref="StorageEntry{IObjectList}"/> read or created.</para>
		/// </returns>
		public StorageEntry<IObjectList<T, THeader>> GetOrCreateList<T, THeader>(DataBuffer keySpace, StorageKey key, THeader header, DateTime expires,
			Func<T> creator, Func<THeader> headerCreator)
		{
			AddKeySpaceInfoForList<T, THeader>(keySpace);
			var allowsMultiple = Storage.GetAllowsMultiple(keySpace);
			IObjectList<T, THeader> list;
			if (Storage.Exists(keySpace, key))
			{
				if (allowsMultiple)
				{
					var objectList = new ObjectListForMultiples<T, THeader>(this,
						keySpace, key, creator, headerCreator);
					expires = objectList.Expires;
					list = objectList;
				}
				else
				{
					var objectList = new ObjectListForSingles<T, THeader>(this,
						keySpace, key, DataBuffer.Empty, creator, headerCreator);
					expires = objectList.Expires;
					list = objectList;
				}
			}
			else
			{
				if (allowsMultiple)
				{
					list = new ObjectListForMultiples<T, THeader>(this,
						keySpace, key, creator, header, expires);
				}
				else
				{
					list = new ObjectListForSingles<T, THeader>(this,
						keySpace, key, creator, header, expires);
				}
			}
			return new StorageEntry<IObjectList<T, THeader>>(list,
				DateTime.MinValue, expires);
		}

		/// <summary>
		/// 	<para>Deletes a list from the store.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the list.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the list.</para>
		/// </param>
		/// <returns>
		/// 	<para>
		/// 		<see langword="true"/> if the list existed prior; otherwise <see langword="false"/>.</para>
		/// </returns>
		public bool DeleteList(DataBuffer keySpace, StorageKey key)
		{
			return Storage.Delete(keySpace, key);
		}

		/// <summary>
		/// 	<para>Gets the expiration time of a list.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the list.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the list.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="Nullable{DateTime}"/> containing the expiration time of the list if found; otherwise <see langword="null"/>.</para>
		/// </returns>
		public DateTime? GetListExpires(DataBuffer keySpace, StorageKey key)
		{
			return GetExpires(keySpace, key);
		}

		/// <summary>
		/// 	<para>Sets the expiration time of a list.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space of the list.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The key of the list.</para>
		/// </param>
		/// <param name="expires">
		/// 	<para>The expiration <see cref="DateTime"/>.</para>
		/// </param>
		/// <returns>
		/// 	<para>
		/// 		<see langword="true"/> if the list exists so its expiration time could be set; otherwise <see langword="false"/>.</para>
		/// </returns>
		public bool SetListExpires(DataBuffer keySpace, StorageKey key, DateTime expires)
		{
			return SetExpires(keySpace, key, expires);
		}

		private EventHandler<ObjectEventArgs> _dropped;
		/// <summary>
		/// Occurs when an object or list is dropped from the store. Does not occur
		/// for deletions initiated by client called deletions, but only when the
		/// store itself drops the entry, for example due to lack of space or time
		/// based expiration.
		/// </summary>
		public event EventHandler<ObjectEventArgs> Dropped
		{
			add { _dropped += value; }
			remove { _dropped -= value; }
		}

		/// <summary>
		/// 	<para>Gets whether the stores supports key spaces.</para>
		/// </summary>
		/// <value>
		/// 	<para>If <see langword="true"/>, then keys are unique only within a key space value. Otherwise, keys are unique within the entire store, and key space values are ignored.</para>
		/// </value>
		public bool SupportsKeySpaces
		{
			get { return Storage.SupportsKeySpaces; }
		}

		/// <summary>
		/// 	<para>Gets whether the store supports key partitions.</para>
		/// </summary>
		/// <value>
		/// 	<para>If <see langword="true"/>, then keys are unique within a partition. Otherwise, keys are unique within the store/key space, and partition identifiers are ignored.</para>
		/// </value>
		public bool SupportsKeySpacePartitions
		{
			get { return Storage.SupportsKeySpacePartitions; }
		}

		/// <summary>
		/// 	<para>Gets whether a particular key space supports key partitions.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The key space.</para>
		/// </param>
		/// <returns>
		/// 	<para>
		/// 		<see langword="true"/> if the key space supports partitions; otherwise <see langword="false"/>. Should return <see langword="false"/> if <see cref="SupportsKeySpacePartitions"/> is <see langword="false"/>.</para>
		/// </returns>
		public bool GetKeySpacePartitionSupport(DataBuffer keySpace)
		{
			return Storage.GetKeySpacePartitionSupport(keySpace);
		}

		/// <summary>
		/// 	<para>Gets the type of transaction support the store provides, if any.</para>
		/// </summary>
		/// <value>
		/// 	<para>A <see cref="TransactionSupport"/> that specifies the transaction support.</para>
		/// </value>
		/// <remarks>
		/// 	<para>If supported, transactions will be used via the System.Transactions framework.</para>
		/// </remarks>
		public TransactionSupport TransactionSupport
		{
			get { return Storage.TransactionSupport; }
		}

		/// <summary>
		/// 	<para>Gets the type of transaction commit the store provides for transactional stores.</para>
		/// </summary>
		/// <remarks>
		/// 	<para>If supported, transactions will be used via the System.Transactions framework.</para>
		/// </remarks>
		public TransactionCommitType CommitType
		{
			get { return Storage.CommitType; }
		}

		/// <summary>
		/// 	<para>Gets the scope within which the store exists.</para>
		/// </summary>
		/// <value>
		/// 	<para>An <see cref="ExecutionScope"/> describing the scope of the store.</para>
		/// </value>
		public ExecutionScope ExecutionScope
		{
			get { return Storage.ExecutionScope; }
		}

		/// <summary>
		/// 	<para>Gets the behavior of the store as available space runs out.</para>
		/// </summary>
		/// <value>
		/// 	<para>An <see cref="OutOfSpacePolicy"/> that describes the behavior of the store as space runs out.</para>
		/// </value>
		public OutOfSpacePolicy OutOfSpacePolicy
		{
			get { return Storage.OutOfSpacePolicy; }
		}

		/// <summary>
		/// 	<para>Initializes the store.</para>
		/// </summary>
		/// <param name="config">
		/// 	<para>An object containing the configuration of the store.</para>
		/// </param>
		public void Initialize(object config)
		{
			ClearState();
			var specConfig = (SerializingObjectStorageConfig)config;
			Storage = specConfig.Storage;
			_allowsDrops = (Storage.OutOfSpacePolicy == OutOfSpacePolicy.DropEntries);
			if (_allowsDrops)
			{
				_keyspaceInfos = new Dictionary<DataBuffer, KeyspaceInfo>();
				_keyspaceInfoLock = new MsReaderWriterLock(System.Threading.LockRecursionPolicy.SupportsRecursion);
				Storage.Dropped += Storage_Dropped;
			}
			else
			{
				Storage.Dropped -= Storage_Dropped;
				_keyspaceInfos = null;
				_keyspaceInfoLock = null;
			}
			StreamPool = specConfig.StreamPool;
		}

		private void Storage_Dropped(object sender, BinaryEventArgs e)
		{
			var dropped = _dropped;
			if (dropped == null) return;
			var info = GetKeySpaceInfo(e.KeySpace);
			if (info.HeaderType == null)
			{
				if (info.ObjectCreator == null) return;
				var entry = GetCore(e.Data, null, info.ObjectCreator());
				dropped(this, new ObjectEventArgs(new ObjectReference(
					e.KeySpace, e.Key), false, entry.Instance));
			}
			else
			{
				if (info.ObjectCreator == null || info.HeaderCreator == null) return;
				var list = new ObjectListForSingles<object, object>(this,
					e.KeySpace, e.Key, e.Data, info.ObjectCreator,
					info.HeaderCreator);
				dropped(this, new ObjectEventArgs(new ObjectReference(
					e.KeySpace, e.Key), true, list));
			}
		}

		/// <summary>
		/// 	<para>Reinitializes the store. To be called after <see cref="Initialize"/>.</para>
		/// </summary>
		/// <param name="config">
		/// 	<para>An object containing the new configuration of the store.</para>
		/// </param>
		public void Reinitialize(object config)
		{
			Initialize(config);
		}

		private void ClearState()
		{
			if (Storage != null)
			{
				Storage.Dispose();
				Storage = null;
			}
			StreamPool = null;			
		}

		/// <summary>
		/// 	<para>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</para>
		/// </summary>
		public void Dispose()
		{
			ClearState();
		}
		#endregion
	}
}
