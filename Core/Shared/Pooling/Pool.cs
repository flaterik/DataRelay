using System;
using System.Collections.Generic;
using System.Threading;
using MySpace.Logging;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>Manages a shared number of <typeparamref name="T"/> instances.</para>
	/// </summary>
	/// <typeparam name="T">
	///	<para>The type of objects to manage.</para>
	/// </typeparam>
	public sealed class Pool<T> : IDisposable
	{
		private static readonly LogWrapper _log = new LogWrapper();

		#region :: Sub-classes ::

		/// <summary>
		/// 	<para>Encapsulates a resource owned by a <see cref="Pool{T}"/> instance.</para>
		/// </summary>
		private sealed class ResidentItem
		{
			private bool _isBorrowed;

			/// <summary>
			/// 	<para>Initializes a new instance of the <see cref="ResidentItem"/> class.</para>
			/// </summary>
			/// <param name="owner">The owner.</param>
			/// <param name="item">The item.</param>
			public ResidentItem(Pool<T> owner, T item)
			{
				Owner = owner;
				Item = item;
				UtcCreated = DateTime.UtcNow;
				UseCount = 0;
				IsCorrupted = false;
			}

			#region IPoolItem<T> Members

			/// <summary>
			/// Gets the owning <see cref="Pool{T}"/> instance.
			/// </summary>
			/// <value>The owning <see cref="Pool{T}"/> instance.</value>
			public Pool<T> Owner { get; private set; }

			/// <summary>
			/// Gets the item.
			/// </summary>
			/// <value>The item.</value>
			public T Item { get; private set; }

			/// <summary>
			/// Gets or sets a value indicating whether this instance is corrupted
			/// and should be removed from the owning <see cref="Pool{T}"/> instance.
			/// </summary>
			/// <value>
			/// 	<see langword="true"/> if this instance is corrupted and should be removed
			/// from the owning <see cref="Pool{T}"/> instance; otherwise, <see langword="false"/>.
			/// </value>
			public bool IsCorrupted { get; set; }

			#endregion IPoolItem<T> Members

			/// <summary>
			/// 	<para>Gets the UTC time that this instance was created.</para>
			/// </summary>
			/// <value>
			/// 	<para>The UTC time that this instance created.</para>
			/// </value>
			public DateTime UtcCreated { get; private set; }

			/// <summary>
			/// 	<para>Gets or sets the number of times this item has been used.</para>
			/// </summary>
			/// <value>
			/// 	<para>The number of times this item has been used.</para>
			/// </value>
			public int UseCount { get; private set; }

			/// <summary>
			/// 	<para>Must be called when this instance is loaned out to a
			/// consumer for internal book-keeping purposes. This item will be unborrowed
			/// 	when the consumer disposes the returned object.</para>
			/// </summary>
			/// <param name="poolVersion">
			/// 	<para>The version of <see cref="Pool{T}"/> 
			/// 	at the time that this item was borrowed.</para>
			/// </param>
			/// <returns>
			/// 	<para>An object encapsulating the borrowed item.  Disposing this object will
			/// 	return the item to the pool; <see langword="null"/> if this item has already
			///		been borrowed.</para>
			/// </returns>
			internal IPoolItem<T> Borrow(int poolVersion)
			{
				if (_isBorrowed)
				{
					throw new InvalidOperationException(string.Format("Cannot borrow an item that has already been borrowed. This indicates a bug in the {0} framework and should be investigated.", typeof(Pool<>)));
				}

				_isBorrowed = true;
				PoolVersionWhenBorrowed = poolVersion;

				if (Owner._finalizeLeaks)
				{
					return new BorrowedItemFinalized(this);
				}
				return new BorrowedItem(this);
			}

			internal int PoolVersionWhenBorrowed { get; private set; }

			/// <summary>
			/// 	<para>Returns this instance to its owning pool; if the appropriate conditions
			/// 	specified by the <see cref="PoolConfig"/> are met the item may be discarded
			/// 	and it's underlying resource will be disposed.</para>
			/// </summary>
			public void Return()
			{
				if (!_isBorrowed)
				{
					throw new InvalidOperationException("Cannot return an item that is not on loan.");
				}

				_isBorrowed = false;
				++UseCount;
				Owner._return(this);
			}
		}

		private class BorrowedItem : IPoolItem<T>
		{
			private readonly ResidentItem _parent;
			private bool _isDisposed;

			public BorrowedItem(ResidentItem parent)
			{
				_parent = parent;
			}

			public Pool<T> Owner
			{
				get { return _parent.Owner; }
			}

			public T Item
			{
				get { return _parent.Item; }
			}

			public bool IsCorrupted
			{
				get
				{
					return _parent.IsCorrupted;
				}
				set
				{
					_parent.IsCorrupted = value;
				}
			}

			public void Dispose()
			{
				if (!_isDisposed)
				{
					_isDisposed = true;
					Dispose(true);
				}
			}

			protected virtual void Dispose(bool disposing)
			{
				if (disposing)
				{
					_parent.Return();
				}
				else
				{
					Owner._decrementBorrowedCount();
				}
			}
		}

		private class BorrowedItemFinalized : BorrowedItem
		{
			public BorrowedItemFinalized(ResidentItem parent)
				: base(parent)
			{
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing) GC.SuppressFinalize(this);

				base.Dispose(disposing);
			}

			~BorrowedItemFinalized()
			{
				Dispose(false);
			}
		}

		private interface IContainer<TItem>
		{
			TItem Take();
			void Put(TItem item);
			int Count { get; }
		}

		private class FifoContainer<TItem> : IContainer<TItem>
		{
			private readonly Queue<TItem> _queue;

			public FifoContainer(int capacity)
			{
				_queue = new Queue<TItem>(capacity);
			}

			#region IContainer Members

			TItem IContainer<TItem>.Take()
			{
				return _queue.Dequeue();
			}

			void IContainer<TItem>.Put(TItem item)
			{
				_queue.Enqueue(item);
			}

			int IContainer<TItem>.Count
			{
				get { return _queue.Count; }
			}

			#endregion
		}

		private class LifoContainer<TItem> : IContainer<TItem>
		{
			private readonly Stack<TItem> _stack;

			public LifoContainer(int capacity)
			{
				_stack = new Stack<TItem>(capacity);
			}

			#region IContainer Members

			TItem IContainer<TItem>.Take()
			{
				return _stack.Pop();
			}

			void IContainer<TItem>.Put(TItem item)
			{
				_stack.Push(item);
			}

			int IContainer<TItem>.Count
			{
				get { return _stack.Count; }
			}

			#endregion
		}

		#endregion :: Sub-classes ::

		private readonly object _syncRoot = new object();
		private readonly Factory<T> _itemFactory;
		private readonly Factory<T, PoolItemPhase, bool> _itemFilter;
		private IContainer<ResidentItem> _container;
		private bool _isDisposed;
		private int _poolVersion;

		private readonly PoolFetchOrder _fetchOrder;
		private readonly int _loanCapacity;
		private readonly int _poolCapacity;
		private readonly int _maxLifespan;
		private readonly int _maxUses;
		private readonly bool _finalizeLeaks;

		private int _borrowedCount;
		private int _waiterCount;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Pool{T}"/> class.</para>
		/// </summary>
		/// <param name="itemFactory">
		///	<para>Builds new items when no re-usable items are available in the pool.</para>
		/// </param>
		/// <param name="itemFilter">
		///	<para>Called when items leave and return to the pool. Should return
		///	<see langword="true"/> if the item is re-usable and <see langword="false"/> if the item should
		///	be disposed and removed from the pool. If the item needs to be reset
		///	it should be done in this method when the phase is <see cref="PoolItemPhase.Returning"/>
		///	or <see cref="PoolItemPhase.Leaving"/>.</para>
		/// </param>
		/// <param name="config">
		///	<para>Configuration settings that dictate the behavior of the pool.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="itemFactory"/> is <see langword="null"/>.</para>
		///	<para>- or -</para>
		///	<para><paramref name="config"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="NotSupportedException">
		///	<para><paramref name="config.FetchOrder"/> is not supported;
		///	supported types are <see cref="PoolFetchOrder.Fifo"/> and
		///	<see cref="PoolFetchOrder.Lifo"/>.</para>
		/// </exception>
		/// <example>
		///	<code>
		///		public class MyClass
		///		{
		///			PoolConfig _config;
		///			Pool&lt;MyConnection&gt; _pool;
		///				
		///			public MyClass()
		///			{
		///				_config = new PoolConfig()
		///				{
		///					FetchOrder = PoolFetchOrder.Fifo; // queue-like
		///					LoanCapacity = 10; // no more than 10 items in use simultaneously
		///					PoolCapacity = 0; // pool can hold any number of connections (but won't
		///													 // because no more than 10 can be borrowed at a time)
		///					MaxUses = 0; // unlimited
		///					MaxLifespan = 60 * 5 // expire after 5 minutes
		///				};
		///				_pool = new Pool&lt;MyConnection&gt;(
		///				
		///				// Called when no existing connections are available in the pool.
		///				() =>
		///				{
		///					var conn = new MyConnection();
		///					conn.Open();
		///					return conn;
		///				},
		///				
		///				// Called when items leave and return to the pool.
		///				(connection, phase) =>
		///				{
		///					// Returning false will tell the pool to discard the item.
		///					if(!connection.IsOpen) return false;
		///					// Only clear when connections are returned to the pool.
		///					if(phase == PoolItemPhase.Returning) connection.ClearBuffer();
		///					return true;
		///				},
		///				
		///				_config);
		///			}
		///		
		///			public void SendSomeData(byte[] data)
		///			{
		///				using(var conn = _pool.Borrow())
		///				{
		///					try
		///					{
		///						conn.Item.SendSomeDataTo("127.0.0.1", data);
		///					}
		///					catch(MyException)
		///					{
		///						// Setting this to true will tell the pool to discard
		///						// the item when it is returned to the pool.
		///						conn.IsCorrupted = true;
		///						throw;
		///					}
		///				}
		///			}
		///		}
		///	</code>
		/// </example>
		public Pool(
			Factory<T> itemFactory,
			Factory<T, PoolItemPhase, bool> itemFilter,
			PoolConfig config)
		{
			if (itemFactory == null) throw new ArgumentNullException("itemFactory");
			if (config == null) throw new ArgumentNullException("config");

			_itemFactory = itemFactory;
			_itemFilter = itemFilter;

			_loanCapacity = config.LoanCapacity;
			_poolCapacity = config.PoolCapacity;
			_maxLifespan = config.MaxLifespan;
			_maxUses = config.MaxUses;
			_finalizeLeaks = _loanCapacity > 0 && config.FinalizeLeaks;
			_fetchOrder = config.FetchOrder;

			_container = _createContainer(config.PoolCapacity > 0 ? config.PoolCapacity : 10);
		}

		private IContainer<ResidentItem> _createContainer(int capacity)
		{
			if (_fetchOrder == PoolFetchOrder.Fifo)
			{
				return new FifoContainer<ResidentItem>(capacity);
			}
			else if (_fetchOrder == PoolFetchOrder.Lifo)
			{
				return new LifoContainer<ResidentItem>(capacity);
			}
			else
			{
				throw new NotSupportedException(
					string.Format("PoolFetchOrder {0} is not supported.", _fetchOrder));
			}
		}

		/// <summary>
		/// 	<para>Borrows an item from the pool. When done, return the item via
		/// 	by disposing it (<see cref="IPoolItem{T}.Dispose"/>).</para>
		/// </summary>
		/// <exception cref="ObjectDisposedException">
		///		<para>This instance has already been disposed.</para>
		/// </exception>
		/// <returns>
		/// 	<para>An <see cref="IPoolItem{T}"/> instance containing a <typeparamref name="T"/>
		/// instance that may be used by the consumer until it is returned to the pool.</para>
		/// </returns>
		/// <example>
		/// 	<code>
		/// 	// To use an item:
		/// 	using(var borrowedItem = pool.Borrow())
		/// 	{
		/// 		// ... make use of borrowedItem.Item
		/// 	}
		/// 	
		/// 	// To remove an item from the pool
		/// 	// when one becomes corrupted:
		/// 	
		/// 	// To use an item:
		/// 	using(var borrowedItem = pool.Borrow())
		/// 	{
		/// 		try
		/// 		{
		/// 			// ... make use of borrowedItem.Item
		/// 		}
		/// 		catch(StateCorruptionException)
		/// 		{
		/// 			borrowedItem.IsCorrupted = true;
		/// 			throw;
		/// 		}
		/// 	}
		/// </code>
		/// </example>
		public IPoolItem<T> Borrow()
		{
			ResidentItem resident = null;
			List<ResidentItem> expiredItems = null;
			try
			{
				int currentPoolVersion;

				lock (_syncRoot)
				{
					if (_isDisposed) throw new ObjectDisposedException(this.GetType().Name);

					currentPoolVersion = this._poolVersion; // The pool version at the time of borrowing.

					while (_loanCapacity > 0 && _borrowedCount >= _loanCapacity)
					{
						++_waiterCount;
						Monitor.Wait(_syncRoot);
						--_waiterCount;
					}

					while (_container.Count > 0)
					{
						resident = _container.Take();
						if (_isLifespanExpired(resident) || !_filter(resident, PoolItemPhase.Leaving))
						{
							if (expiredItems == null) expiredItems = new List<ResidentItem>();
							expiredItems.Add(resident);
							resident = null;
							continue;
						}
						break;
					}

					++_borrowedCount;
				}

				try
				{
					if (resident == null) resident = new ResidentItem(this, _itemFactory());

					var borrowedItem = resident.Borrow(currentPoolVersion);

					if (borrowedItem == null)
					{
						throw new InvalidOperationException(
							"The item that the pool is attempting to check out has already been borrowed.  " +
							"This exception indicates a bug within the pooling framework, and should not occur otherwise.");
					}

					return borrowedItem;
				}
				catch
				{
					_decrementBorrowedCount();
					throw;
				}
			}
			finally
			{
				if (expiredItems != null)
				{
					foreach (var item in expiredItems)
					{
						_discardItem(item);
					}
				}
			}
		}

		/// <summary>
		/// 	<para>Gets the current number of items in the pool.</para>
		/// </summary>
		/// <value>
		/// 	<para>The current number of items in the pool.</para>
		/// </value>
		/// <exception cref="ObjectDisposedException">
		///		<para>This instance has been disposed.</para>
		/// </exception>
		public int Count
		{
			get
			{
				lock (_syncRoot)
				{
					if (_isDisposed) throw new ObjectDisposedException(this.GetType().Name);

					return _container.Count;
				}
			}
		}

		/// <summary>
		/// 	<para>Clears and disposes in this pool and borrowed from this pool.
		/// 	Future borrowings are guaranteed to be new.</para>
		/// </summary>
		/// <exception cref="ObjectDisposedException">
		///		<para>This instance has already been disposed.</para>
		/// </exception>
		public void Clear()
		{
			IContainer<ResidentItem> oldContainer;

			lock (_syncRoot)
			{
				if (_isDisposed) throw new ObjectDisposedException(this.GetType().Name);

				_poolVersion++; // So that items already borrowed will be disposed.

				oldContainer = _container;
				_container = _createContainer(_container.Count);
			}

			_doClearPool(oldContainer);
		}

		private bool _filter(ResidentItem residentItem, PoolItemPhase phase)
		{
			if (_itemFilter == null) return true;
			try
			{
				return _itemFilter(residentItem.Item, phase);
			}
			catch (Exception ex)
			{
				_log.Error("Failed to re-claim an item that was returned to a {0} instance", ex);
				return false;
			}
		}

		private void _return(ResidentItem residentItem)
		{
			_decrementBorrowedCount();

			bool discard = residentItem.IsCorrupted
				|| (_maxUses > 0 && residentItem.UseCount >= _maxUses)
				|| _isLifespanExpired(residentItem);

			if (!discard) discard = !_filter(residentItem, PoolItemPhase.Returning);

			if (discard)
			{
				_discardItem(residentItem);
				return;
			}

			bool returned = false;
			lock (_syncRoot)
			{
				if (!_isDisposed
					&& residentItem.PoolVersionWhenBorrowed == this._poolVersion
					&& (_poolCapacity <= 0 || _container.Count < _poolCapacity))
				{
					_container.Put(residentItem);
					returned = true;
				}
			}
			if (!returned) _discardItem(residentItem);
		}

		private void _decrementBorrowedCount()
		{
			lock (_syncRoot)
			{
				--_borrowedCount;
				if (_waiterCount > 0)
				{
					Monitor.Pulse(_syncRoot);
				}
			}
		}

		private bool _isLifespanExpired(ResidentItem residentItem)
		{
			return _maxLifespan > 0 && DateTime.UtcNow.Subtract(residentItem.UtcCreated).TotalSeconds >= _maxLifespan;
		}

		private static void _discardItem(ResidentItem residentItem)
		{
			try
			{
				var disposableObject = residentItem.Item as IDisposable;
				if (disposableObject != null) disposableObject.Dispose();
			}
			catch (Exception ex)
			{
				_log.Error(string.Format("Failed to dispose an item that was discarded from a {0} instance", typeof(Pool<T>)), ex);
			}
		}

		/// <summary>
		/// 	<para>Clears all items from this pool, and make it disposed and ineligible for
		///		further use.</para>
		/// </summary>
		public void Dispose()
		{
			lock (_syncRoot)
			{
				_isDisposed = true;

				_doClearPool(_container);
			}
		}

		private static void _doClearPool(IContainer<ResidentItem> container)
		{
			while (container.Count > 0)
			{
				var item = container.Take();
				_discardItem(item);
			}
		}
	}
}
