using System;
using System.Collections.Generic;
using System.IO;
using MySpace.Common.Storage;
using MySpace.ResourcePool;
using Serializer = MySpace.Common.IO.Serializer;

namespace MySpace.Storage
{
	/// <summary>
	/// Object list for binary storages that support storing multiple
	/// entries under the same key.
	/// </summary>
	/// <typeparam name="T">The type of object stored in the list.</typeparam>
	/// <typeparam name="THeader">The type of header stored in the list.</typeparam>
	internal sealed class ObjectListForMultiples<T, THeader> : IObjectList<T, THeader>
	{
		private THeader _header;

		/// <summary>
		/// Gets underlying binary storage the list is stored in.
		/// </summary>
		/// <value>The underlying <see cref="SerializingObjectStorage"/>.</value>
		public SerializingObjectStorage Storage { get; private set; }

		/// <summary>
		/// Gets the cursor used to read header and items from
		/// <see cref="Storage"/>.
		/// </summary>
		/// <value>The <see cref="IBinaryCursor"/> used.</value>
		public IBinaryCursor Cursor { get; private set; }

		/// <summary>
		/// 	<para>Gets the key the binary entries are stored in
		///		<see cref="Storage"/>.</para>
		/// </summary>
		/// <value>
		/// 	<para>The <see cref="StorageKey"/> the entries
		///		stored under.</para>
		/// </value>
		public StorageKey Key { get; private set; }
		
		private DataBuffer _keySpace;

		/// <summary>
		/// 	<para>Gets when the list expires.</para>
		/// </summary>
		/// <value>
		/// 	<para>The expiration <see cref="DateTime"/>.</para>
		/// </value>
		public DateTime Expires { get; private set; }

		/// <summary>
		/// 	<para>Gets the delegate that creates new blank
		///		instances of <typeparamref name="T"/> for
		///		deserialization.</para>
		/// </summary>
		/// <value>
		/// 	<para>The creating <see cref="Func{T}"/>.</para>
		/// </value>
		public Func<T> Creator { get; private set; }
		
		private bool _supportsKeySpacePartitions;

		/// <summary>
		/// Reads the current value of <see cref="Cursor"/>.
		/// </summary>
		/// <param name="itemValue">A <see cref="ResourcePoolItem{MemoryStream}"/> that provides
		/// streams used for deserializing the value.</param>
		/// <param name="valueBuffer">Outputs the <see cref="DataBuffer"/> value read.</param>
		/// <returns>The <see cref="Int32"/> length of the cursor read.</returns>
		public int ReadCursor(ResourcePoolItem<MemoryStream> itemValue, out DataBuffer valueBuffer)
		{
			var valueStream = itemValue.Item;
			valueBuffer = SerializingObjectStorage.GetReadBuffer(valueStream);
			var results = Cursor.Get(valueBuffer, true);
			if (results < 0) return results;
			var bfExcessValue = SerializingObjectStorage.GetExcessBuffer(valueStream, valueBuffer, results);
			if (!bfExcessValue.IsEmpty)
			{
				var len = valueBuffer.Length;
				valueBuffer = bfExcessValue;
				if (!Storage.Storage.SupportsIncompleteReads)
				{
					bfExcessValue = bfExcessValue.RestrictOffset(len);
					results = Cursor.Get(len, bfExcessValue, false);
					if (results >= 0) results += len;
				} else
				{
					results = Cursor.Get(0, bfExcessValue, false);
				}
			}
			return results;
		}

		private ObjectListForMultiples(SerializingObjectStorage storage, DataBuffer keySpace,
			StorageKey key, Func<T> creator,
			Action<ObjectListForMultiples<T, THeader>, ResourcePoolItem<MemoryStream>> cont)
		{
			Storage = storage;
			_keySpace = Storage.SupportsKeySpaces ? keySpace : DataBuffer.Empty;
			_supportsKeySpacePartitions = Storage.GetKeySpacePartitionSupport(
				_keySpace);
			Key = _supportsKeySpacePartitions ? key : new StorageKey(key.Key, 0);
			Creator = creator;
			using (var itemValue = storage.StreamPool.GetItem())
			{
				cont(this, itemValue);
			}
		}

		private bool SetCursor()
		{
			if (Cursor == null)
			{
				Cursor = Storage.Storage.GetCursor(_keySpace, Key);
				//if (!Cursor.MoveNext())
				//{
				//    throw new ApplicationException(string.Format(
				//        "No list for multiples found for key space '{0}', key '{1}'",
				//        _keySpace, Key));
				//}
				return true;
			}
			return false;
		}

		private void SetCursorAndCheckPopulated()
		{
			if (SetCursor())
			{
				var dummy = new byte[1];
				if (Cursor.Get(dummy, true) < 0)
				{
					throw new ApplicationException(string.Format(
						"No list for multiples found for key space '{0}', key '{1}'",
						_keySpace, Key));
				}
			}
		}

		/// <summary>
		/// 	<para>Disposes and discards <see cref="Cursor"/>.</para>
		/// </summary>
		public void ClearCursor()
		{
			if (Cursor != null)
			{
				Cursor.Dispose();
				Cursor = null;
			}
		}

		/// <summary>
		/// 	<para>Disposes and discards the current <see cref="Cursor"/>,
		///		and creates a new one.</para>
		/// </summary>
		public void ResetCursor()
		{
			ClearCursor();
			SetCursor();
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectListForMultiples{T, THeader}"/> class
		///		for an existing list.</para>
		/// </summary>
		/// <param name="storage">
		/// 	<para>The <see cref="SerializingObjectStorage"/> containing the list.</para>
		/// </param>
		/// <param name="keySpace">
		/// 	<para>The <see cref="DataBuffer"/> keyspace the list is stored in.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The single <see cref="StorageKey"/> all the binary entries are
		/// stored under.</para>
		/// </param>
		/// <param name="creator">
		/// 	<para>The <see cref="Func{T}"/> that produces new <typeparamref name="T"/>
		///		instances for deserialization.</para>
		/// </param>
		/// <param name="headerCreator">
		/// 	<para>The <see cref="Func{THeader}"/> that produces new <typeparamref name="THeader"/>
		///		instances for deserialization.</para>
		/// </param>
		public ObjectListForMultiples(SerializingObjectStorage storage, DataBuffer keySpace,
			StorageKey key, Func<T> creator, Func<THeader> headerCreator)
			: this(storage, keySpace, key, creator, (inst, itemValue) =>
			{
				inst.SetCursor();
				DataBuffer valueBuffer;
				if (inst.ReadCursor(itemValue, out valueBuffer) < 0)
				{
					throw new ApplicationException(string.Format(
                   		"No list for multiples header found for key space '{0}', key '{1}'",
                   		keySpace, key));
				}
				var seg = valueBuffer.ByteArraySegmentValue;
				var ticks = BitConverter.ToInt64(seg.Array, seg.Offset);
				inst.Expires = new DateTime(ticks);
				var stream = itemValue.Item;
				stream.Position = sizeof(long);
				inst._header = headerCreator();
				Serializer.Deserialize(stream, inst._header);
			})
		{ }

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectListForMultiples{T, THeader}"/> class
		///		for a new list.</para>
		/// </summary>
		/// <param name="storage">
		/// 	<para>The <see cref="SerializingObjectStorage"/> containing the list.</para>
		/// </param>
		/// <param name="keySpace">
		/// 	<para>The <see cref="DataBuffer"/> keyspace the list is stored in.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The single <see cref="StorageKey"/> all the binary entries are
		/// stored under.</para>
		/// </param>
		/// <param name="creator">
		/// 	<para>The <see cref="Func{T}"/> that produces new <typeparamref name="T"/>
		///		instances for deserialization.</para>
		/// </param>
		/// <param name="header">
		/// 	<para>The header <typeparamref name="THeader"/> instance to be stored.</para>
		/// </param>
		/// <param name="expires">The expiration <see cref="DateTime"/> of the new list.</param>
		public ObjectListForMultiples(SerializingObjectStorage storage, DataBuffer keySpace,
			StorageKey key, Func<T> creator, THeader header, DateTime expires)
			: this(storage, keySpace, key, creator, (inst, itemValue) =>
			{
				inst.Expires = expires;
				inst._header = header;
				var stream = itemValue.Item;
				stream.Write(BitConverter.GetBytes(expires.Ticks), 0, sizeof(long));
				Serializer.Serialize(stream, header);
				inst.Storage.Storage.Put(keySpace, key,
					SerializingObjectStorage.GetWriteBuffer(stream));
			})
		{ }

		/// <summary>
		/// 	<para>Gets the information associated with the list.</para>
		/// </summary>
		public THeader Header
		{
			get { return _header; }
		}

		/// <summary>
		/// 	<para>Adds a new object to the list.</para>
		/// </summary>
		/// <param name="instance">
		/// 	<para>The object to add.</para>
		/// </param>
		public void Add(T instance)
		{
			SetCursorAndCheckPopulated();
			using (var item = Storage.StreamPool.GetItem())
			{
				var stream = item.Item;
				Serializer.Serialize(stream, instance);
				Cursor.Put(SerializingObjectStorage.GetWriteBuffer(stream), true);
			}
		}

		/// <summary>
		/// 	<para>Adds new objects to the list.</para>
		/// </summary>
		/// <param name="instances">
		/// 	<para>The objects to add.</para>
		/// </param>
		public void AddRange(IEnumerable<T> instances)
		{
			if (instances == null) throw new ArgumentNullException("instances");
			using (var enm = instances.GetEnumerator())
			{
				if (!enm.MoveNext()) return;
				SetCursorAndCheckPopulated();
				using (var item = Storage.StreamPool.GetItem())
				{
					var stream = item.Item;
					Serializer.Serialize(stream, enm.Current);
					Cursor.Put(SerializingObjectStorage.GetWriteBuffer(stream), true);
					while (enm.MoveNext())
					{
						stream.Position = 0;
						Serializer.Serialize(stream, enm.Current);
						Cursor.Put(SerializingObjectStorage.GetWriteBuffer(stream), true);
					}
				}
			}
		}

		/// <summary>
		/// 	<para>Removes all objects from the list.</para>
		/// </summary>
		public void Clear()
		{
			using (var item = Storage.StreamPool.GetItem())
			{
				var stream = item.Item;
				stream.Write(BitConverter.GetBytes(Expires.Ticks), 0, sizeof(long));
				Serializer.Serialize(stream, _header);
				Storage.Storage.Put(_keySpace, Key,
					SerializingObjectStorage.GetWriteBuffer(stream));
			}
		}

		/// <summary>
		/// 	<para>Returns an enumerator that iterates through the collection.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.</para>
		/// </returns>
		public IEnumerator<T> GetEnumerator()
		{
			ResetCursor();
			return new CursorEnumerator<T, THeader>(this);
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		/// 	<para>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</para>
		/// </summary>
		public void Dispose()
		{
			ClearCursor();
			Storage = null;
		}
	}
}
