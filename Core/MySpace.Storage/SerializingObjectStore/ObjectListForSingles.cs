using System;
using System.Collections.Generic;
using System.IO;
using MySpace.Common.Storage;
using MySpace.ResourcePool;
using Serializer = MySpace.Common.IO.Serializer;

namespace MySpace.Storage
{
	/// <summary>
	/// Object list for binary storages that do not support storing multiple
	/// entries under the same key.
	/// </summary>
	/// <typeparam name="T">The type of object stored in the list.</typeparam>
	/// <typeparam name="THeader">The type of header stored in the list.</typeparam>
	internal sealed class ObjectListForSingles<T, THeader> : IObjectList<T, THeader>
	{
		/// <summary>
		/// Gets underlying binary storage the list is stored in.
		/// </summary>
		/// <value>The underlying <see cref="SerializingObjectStorage"/>.</value>
		public SerializingObjectStorage Storage { get; private set; }

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
		/// 	<para>Gets the delegate that creates new blank
		///		instances of <typeparamref name="T"/> for
		///		deserialization.</para>
		/// </summary>
		/// <value>
		/// 	<para>The creating <see cref="Func{T}"/>.</para>
		/// </value>
		public Func<T> Creator { get; private set; }

		private THeader _header;

		/// <summary>
		/// 	<para>Gets when the list expires.</para>
		/// </summary>
		/// <value>
		/// 	<para>The expiration <see cref="DateTime"/>.</para>
		/// </value>
		public DateTime Expires { get; private set; }

		private ResourcePoolItem<MemoryStream> _pooled;
		private MemoryStream _stream;
		private long _headerLength;

		private ObjectListForSingles(SerializingObjectStorage storage, DataBuffer keySpace,
			StorageKey key, Func<T> creator)
		{
			Storage = storage;
			Key = key;
			_keySpace = keySpace;
			Creator = creator;
			_pooled = storage.StreamPool.GetItem();
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectListForSingles{T, THeader}"/> class
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
		public ObjectListForSingles(SerializingObjectStorage storage, DataBuffer keySpace,
			StorageKey key, Func<T> creator, THeader header, DateTime expires)
			: this(storage, keySpace, key, creator)
		{
			_header = header;
			Expires = expires;
			_stream = _pooled.Item;
			_stream.Write(BitConverter.GetBytes(expires.Ticks), 0, sizeof(long));
			Serializer.Serialize(_stream, header);
			_headerLength = _stream.Position;
			storage.Storage.Put(keySpace, key,
				SerializingObjectStorage.GetWriteBuffer(_stream));
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectListForSingles{T, THeader}"/> class
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
		/// <param name="buffer">
		///		<para><see cref="DataBuffer"/> that holds the entire list. If
		///		<see cref="DataBuffer.IsEmpty"/> is true, then the list is read
		///		from <paramref name="storage"/>.</para>
		/// </param>
		/// <param name="creator">
		/// 	<para>The <see cref="Func{T}"/> that produces new <typeparamref name="T"/>
		///		instances for deserialization.</para>
		/// </param>
		/// <param name="headerCreator">
		/// 	<para>The <see cref="Func{THeader}"/> that produces new <typeparamref name="THeader"/>
		///		instances for deserialization.</para>
		/// </param>
		public ObjectListForSingles(SerializingObjectStorage storage, DataBuffer keySpace,
			StorageKey key, DataBuffer buffer, Func<T> creator, Func<THeader> headerCreator)
			: this(storage, keySpace, key, creator)
		{
			ArraySegment<byte> seg;
			if (buffer.IsEmpty)
			{
				_stream = _pooled.Item;
				buffer = SerializingObjectStorage.GetReadBuffer(_stream);
				var entryLen = storage.Storage.Get(keySpace, key, buffer);
				if (entryLen < 0)
				{
					throw new ApplicationException(string.Format(
													"No list found for key space '{0}', key '{1}'",
													keySpace, key));
				}
				var bfExcess = SerializingObjectStorage.GetExcessBuffer(_stream, buffer,
																		entryLen);
				if (bfExcess.IsEmpty)
				{
					buffer = buffer.Restrict(entryLen);
				}
				else
				{
					if (storage.Storage.SupportsIncompleteReads)
					{
						storage.Storage.Get(keySpace, key, buffer.Length,
							bfExcess.RestrictOffset(buffer.Length));
					}
					else
					{
						storage.Storage.Get(keySpace, key, 0, bfExcess);
					}
					buffer = bfExcess;
				}
				seg = buffer.ByteArraySegmentValue;
			}
			else
			{
				_pooled.Dispose();
				_pooled = null;
				seg = buffer.ByteArraySegmentValue;
				_stream = new MemoryStream(seg.Array, seg.Offset, seg.Count);
			}
			var ticks = BitConverter.ToInt64(seg.Array, seg.Offset);
			// expires listed first, since SetExpires can change it by itself
			// and this way it doesn't have to worry about future changes
			// to header
			Expires = new DateTime(ticks);
			_stream.Position = sizeof(long);
			_header = headerCreator();
			Serializer.Deserialize(_stream, _header);
			_headerLength = _stream.Position;
		}

		#region IObjectList<T,THeader> Members

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
			var pos = (int)_stream.Position;
			var lenStm = (int)_stream.Length;
			_stream.Position = lenStm;
			Serializer.Serialize(_stream, instance);
			var buffer = SerializingObjectStorage.GetWriteBuffer(_stream);
			Storage.Storage.Put(_keySpace, Key, lenStm, 0, buffer.RestrictOffset(lenStm));
			_stream.Position = pos;
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
			var pos = (int)_stream.Position;
			var lenStm = (int)_stream.Length;
			_stream.Position = lenStm;
			foreach (var instance in instances)
			{
				Serializer.Serialize(_stream, instance);
			}
			var buffer = SerializingObjectStorage.GetWriteBuffer(_stream);
			Storage.Storage.Put(_keySpace, Key, lenStm, 0, buffer.RestrictOffset(lenStm));
			_stream.Position = pos;
		}

		/// <summary>
		/// 	<para>Removes all objects from the list.</para>
		/// </summary>
		public void Clear()
		{
			_stream.Position = _headerLength;
			_stream.SetLength(_headerLength);
			Storage.Storage.Put(_keySpace, Key, (int)_headerLength,
				int.MaxValue, DataBuffer.Empty);
		}

		#endregion

		#region IEnumerable<T> Members

		/// <summary>
		/// 	<para>Returns an enumerator that iterates through the collection.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.</para>
		/// </returns>
		public IEnumerator<T> GetEnumerator()
		{
			_stream.Position = _headerLength;
			while (_stream.Position < _stream.Length)
			{
				var instance = Creator();
				Serializer.Deserialize(_stream, instance);
				yield return instance;
			}
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

		#region IDisposable Members

		/// <summary>
		/// 	<para>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</para>
		/// </summary>
		public void Dispose()
		{
			if (_pooled != null)
			{
				_pooled.Dispose();
				_pooled = null;
			}
			_stream = null;
			Storage = null;
		}

		#endregion
	}
}
