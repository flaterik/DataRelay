using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using MySpace.Common.Storage;
using MySpace.Common.HelperObjects;
using System.Xml;
using System.Collections;

namespace MySpace.Storage
{
	/// <summary>
	/// Provides a simple in memory instance retaining implementation of
	/// <see cref="GenericFactory{T}"/> of <see cref="IObjectStorage"/>. Useful
	/// for testing and troubleshooting.
	/// </summary>
	public class InMemoryObjectStorageFactory : GenericFactory<IObjectStorage>
	{
		/// <summary>
		/// 	<para>Overriden. Obtains an instance from this factory.</para>
		/// </summary>
		/// <returns>
        /// 	<para>A implementation of <see cref="IObjectStorage"/>.</para></returns>
		public override IObjectStorage ObtainInstance()
		{
			var ret =  new InMemoryObjectStorage();
			((IObjectStorage) ret).Initialize(null);
			return ret;
		}

		/// <summary>
		/// 	<para>Overriden. Reads the factory configuration.</para>
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="XmlReader"/> to read from.</para>
		/// </param>
		public override void ReadXml(XmlReader reader)
		{
		}

		/// <summary>
		/// 	<para>Overriden. Writes the factory configuration.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="XmlWriter"/> to write to.</para>
		/// </param>
		public override void WriteXml(XmlWriter writer)
		{
		}

		internal class InMemoryObjectStorage : IObjectStorage
		{
			struct ObjectDecorator
			{
				public object Instance { get; set; }
				public DateTime Expires { get; set; }
				public DateTime Updated { get; set; }
				public StorageEntry<T> ToEntry<T>()
				{
					return new StorageEntry<T>((T)Instance, Updated, Expires);
				}
				public static ObjectDecorator FromEntry<T>(StorageEntry<T> entry)
				{
					if (!entry.IsFound) throw new ArgumentException();
					return new ObjectDecorator
					{
						Instance = entry.Instance,
						Expires = entry.Expires,
						Updated = entry.Updated
					};
				}
			}

			enum LockType
			{
				Read,
				ReadUpgradable,
				Write
			}

			class LockingDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
			{
				private readonly Dictionary<TKey, TValue> _dictionary;

				private readonly ReaderWriterLockSlim _lock;

				[ThreadStatic]
				private static LockSpec _lastLock;

				public LockingDictionary()
				{
					_dictionary = new Dictionary<TKey, TValue>();
					_lock = new ReaderWriterLockSlim(
						System.Threading.LockRecursionPolicy.NoRecursion);
				}

				public IDisposable Lock(LockType type)
				{
					return new LockSpec(type, this);
				}

				private void EnterLock()
				{
					if (_lastLock == null)
					{
						throw new ApplicationException("No lock started");
					}
					_lastLock.Enter();
				}

				private void EnterWriteLock()
				{
					if (_lastLock == null)
					{
						throw new ApplicationException("No lock started");
					}
					if (_lastLock.Type < LockType.Write)
					{
						throw new ApplicationException("Not in write lock");
					}
					_lastLock.Enter();
				}

				public bool TryGetValue(TKey key, out TValue value)
				{
					EnterLock();
					return _dictionary.TryGetValue(key, out value);
				}

				public TValue this[TKey key]
				{
					get
					{
						EnterLock();
						return _dictionary[key];
					}
					set
					{
						EnterWriteLock();
						_dictionary[key] = value;
					}
				}

				public void Add(TKey key, TValue value)
				{
					EnterWriteLock();
					_dictionary.Add(key, value);
				}

				public bool Remove(TKey key)
				{
					EnterWriteLock();
					return _dictionary.Remove(key);
				}

				public void Clear()
				{
					EnterWriteLock();
					_dictionary.Clear();
				}

				public void Dispose()
				{
					_lastLock = null;
					_lock.Dispose();
				}

				public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
				{
					EnterLock();
					return _dictionary.GetEnumerator();
				}

				IEnumerator IEnumerable.GetEnumerator()
				{
					return GetEnumerator();
				}

				class LockSpec : IDisposable
				{
					public readonly LockType Type;

					private readonly LockingDictionary<TKey, TValue> _dictionary;

					private readonly LockSpec _previous;

					private bool _started;

					public LockSpec(LockType type, LockingDictionary<TKey, TValue> dictionary)
					{
						Type = type;
						_dictionary = dictionary;
						_previous = _lastLock;
						_lastLock = this;
					}

					private void DoEnter()
					{
						switch (Type)
						{
							case LockType.Read:
								_dictionary._lock.EnterReadLock();
								break;
							case LockType.ReadUpgradable:
								_dictionary._lock.EnterUpgradeableReadLock();
								break;
							case LockType.Write:
								_dictionary._lock.EnterWriteLock();
								break;							
						}
					}

					private void DoExit()
					{
						switch (Type)
						{
							case LockType.Read:
								_dictionary._lock.ExitReadLock();
								break;
							case LockType.ReadUpgradable:
								_dictionary._lock.ExitUpgradeableReadLock();
								break;
							case LockType.Write:
								_dictionary._lock.ExitWriteLock();
								break;
						}
					}

					public void Enter()
					{
						if (_started) return;
						try
						{
						}
						finally
						{
							DoEnter();
							_started = true;
						}
					}

					void IDisposable.Dispose()
					{
						if (!_started) return;
						try
						{
							DoExit();
						}
						finally
						{
							_started = false;
							_lastLock = _previous;
						}
					}
				}
			}

			private LockingDictionary<int, LockingDictionary<StorageKey, ObjectDecorator>> _keySpaces =
				new LockingDictionary<int, LockingDictionary<StorageKey, ObjectDecorator>>();

			private LockingDictionary<StorageKey, ObjectDecorator> GetSingleKeySpace(DataBuffer keySpace)
			{
				var hash = keySpace.GetHashCode();
				LockingDictionary<StorageKey, ObjectDecorator> keySpaceDct;
				// upgradeable blocks other upgradeable, so try read first
				using(_keySpaces.Lock(LockType.Read))
				{
					if (_keySpaces.TryGetValue(hash, out keySpaceDct))
					{
						return keySpaceDct;
					}					
				}
				// didn't find it, so now try upgradeable to write if need be
				using (_keySpaces.Lock(LockType.ReadUpgradable))
				{
					if (!_keySpaces.TryGetValue(hash, out keySpaceDct))
					{
						using(_keySpaces.Lock(LockType.Write))
						{
							keySpaceDct = new LockingDictionary<StorageKey, ObjectDecorator>();
							_keySpaces.Add(hash, keySpaceDct);							
						}
					}
					return keySpaceDct;					
				}
			}

			private StorageEntry<T> GetCore<T>(DataBuffer keySpace, StorageKey key)
			{
				var space = GetSingleKeySpace(keySpace);
				using(space.Lock(LockType.Read))
				{
					ObjectDecorator dec;
					if (space.TryGetValue(key, out dec))
					{
						return dec.ToEntry<T>();
					}
					return new StorageEntry<T>();					
				}
			}


			#region IObjectStorage Members

			StorageEntry<T> IObjectStorage.Get<T>(DataBuffer keySpace, StorageKey key, T instance)
			{
				return GetCore<T>(keySpace, key);
			}

			StorageEntry<T> IObjectStorage.Get<T>(DataBuffer keySpace, StorageKey key, Func<T> creator)
			{
				return GetCore<T>(keySpace, key);
			}

			StorageEntry<T> IObjectStorage.GetOrCreate<T>(DataBuffer keySpace, StorageKey key, Func<StorageEntry<T>> creator)
			{
				var space = GetSingleKeySpace(keySpace);
				ObjectDecorator dec;
				// get
				using(space.Lock(LockType.Read))
				{
					if (space.TryGetValue(key, out dec))
					{
						return dec.ToEntry<T>();
					}					
				}
				// not found, so create
				using(space.Lock(LockType.ReadUpgradable))
				{
					if (space.TryGetValue(key, out dec))
					{
						return dec.ToEntry<T>();
					}
					using(space.Lock(LockType.Write))
					{
						var ret = creator();
						dec = ObjectDecorator.FromEntry(ret);
						space.Add(key, dec);
						return ret;						
					}
				}
			}

			void IObjectStorage.Put<T>(DataBuffer keySpace, StorageKey key, StorageEntry<T> entry)
			{
				var space = GetSingleKeySpace(keySpace);
				using(space.Lock(LockType.Write))
				{
					space[key] = new ObjectDecorator
					{
						Instance = entry.Instance,
						Expires = entry.Expires,
						Updated = entry.Updated
					};					
				}
			}

			bool IObjectStorage.Delete(DataBuffer keySpace, StorageKey key)
			{
				var space = GetSingleKeySpace(keySpace);
				using(space.Lock(LockType.Write))
				{
					return space.Remove(key);					
				}
			}

			bool IObjectStorage.DeleteVersion(DataBuffer keySpace, StorageKey key, DateTime updated)
			{
				var space = GetSingleKeySpace(keySpace);
				ObjectDecorator dec;
				using(space.Lock(LockType.ReadUpgradable))
				{
					if (space.TryGetValue(key, out dec))
					{
						if (dec.Updated.CompareTo(updated) <= 0)
						{
							using(space.Lock(LockType.Write))
							{
								space.Remove(key);
								return true;								
							}
						}
					}					
				}
				return false;
			}

			bool IObjectStorage.Exists(DataBuffer keySpace, StorageKey key)
			{
				return GetCore<object>(keySpace, key).IsFound;
			}

			DateTime? IObjectStorage.GetExpires(DataBuffer keySpace, StorageKey key)
			{
				var dec = GetCore<object>(keySpace, key);
				if (dec.IsFound)
				{
					return dec.Expires;
				}
				return null;
			}

			bool IObjectStorage.SetExpires(DataBuffer keySpace, StorageKey key, DateTime ttl)
			{
				var space = GetSingleKeySpace(keySpace);
				using(space.Lock(LockType.ReadUpgradable))
				{
					ObjectDecorator dec;
					if (space.TryGetValue(key, out dec))
					{
						using(space.Lock(LockType.Write))
						{
							dec.Expires = ttl;
							space[key] = dec;
							return true;							
						}
					}
				}
				return false;
			}

			private IObjectStorage Iface { get { return this; } }

			StorageEntry<IObjectList<T, THeader>> IObjectStorage.GetList<T, THeader>(DataBuffer keySpace, StorageKey key, Func<T> creator, Func<THeader> headerCreator)
			{
				return GetCore<IObjectList<T, THeader>>(keySpace, key);
			}

			IObjectList<T, THeader> IObjectStorage.CreateList<T, THeader>(DataBuffer keySpace, StorageKey key, THeader header, DateTime ttl, Func<T> creator)
			{
				var ret = new MockObjectList<T, THeader>(header);
				Iface.Put(keySpace, key, new StorageEntry<MockObjectList<T, THeader>>(ret, DateTime.Now, ttl));
				return ret;
			}

			StorageEntry<IObjectList<T, THeader>> IObjectStorage.GetOrCreateList<T, THeader>(DataBuffer keySpace, StorageKey key, THeader header, DateTime expires, Func<T> creator, Func<THeader> headerCreator)
			{
				return Iface.GetOrCreate(keySpace, key, () =>
					new StorageEntry<IObjectList<T, THeader>>(
					new MockObjectList<T, THeader>(header), DateTime.Now, expires));
			}

			bool IObjectStorage.DeleteList(DataBuffer keySpace, StorageKey key)
			{
				return Iface.Delete(keySpace, key);
			}

			DateTime? IObjectStorage.GetListExpires(DataBuffer keySpace, StorageKey key)
			{
				return Iface.GetExpires(keySpace, key);
			}

			bool IObjectStorage.SetListExpires(DataBuffer keySpace, StorageKey key, DateTime ttl)
			{
				return Iface.SetExpires(keySpace, key, ttl);
			}

			void IObjectStorage.Clear(DataBuffer keySpace)
			{
				var space = GetSingleKeySpace(keySpace);
				using(space.Lock(LockType.Write))
				{
					space.Clear();
				}
			}

			event EventHandler<ObjectEventArgs> IObjectStorage.Dropped
			{
				add { }
				remove { }
			}

			bool IStorage.SupportsKeySpaces
			{
				get { return true; }
			}

			bool IStorage.SupportsKeySpacePartitions
			{
				get { return true; }
			}

			bool IStorage.GetKeySpacePartitionSupport(DataBuffer keySpace)
			{
				return true;
			}

			TransactionSupport IStorage.TransactionSupport
			{
				get { return TransactionSupport.None; }
			}

			TransactionCommitType IStorage.CommitType
			{
				get { return TransactionCommitType.SinglePhaseAndTwoPhase; }
			}

			ExecutionScope IStorage.ExecutionScope
			{
				get { return ExecutionScope.Instance; }
			}

			OutOfSpacePolicy IStorage.OutOfSpacePolicy
			{
				get { return OutOfSpacePolicy.Exception; }
			}

			void IStorage.Initialize(object config)
			{
			}

			void IStorage.Reinitialize(object config)
			{
				using(_keySpaces.Lock(LockType.Write))
				{
					_keySpaces.Clear();
				}
			}

			#endregion

			#region IDisposable Members

			void IDisposable.Dispose()
			{
				var keySpaces = Interlocked.Exchange(ref _keySpaces, null);
				if (keySpaces == null) return;
				using(keySpaces.Lock(LockType.Read))
				{
					foreach (var entry in keySpaces)
					{
						entry.Value.Dispose();
					}					
				}
				keySpaces.Dispose();
			}

			#endregion
		}

		class MockObjectList<T, THeader> : IObjectList<T, THeader>
		{
			private List<T> _list = new List<T>();

			public void Add(T instance)
			{
				_list.Add(instance);
			}

			public void AddRange(IEnumerable<T> instances)
			{
				if (instances == null) throw new ArgumentNullException("instances");
				_list.AddRange(instances);
			}

			public IEnumerator<T> GetEnumerator()
			{
				return _list.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public void Dispose()
			{
			}

			public THeader Header { get; set; }

			public MockObjectList(THeader header)
			{
				Header = header;
			}

			public void Clear()
			{
				_list.Clear();
			}
		}
	}
}
