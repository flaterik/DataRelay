using System;
using MySpace.Common.HelperObjects;
using System.Diagnostics;
using System.Threading;

namespace MySpace.ResourcePool
{
	/// <summary>
	/// A Pool of objects of type <typeparamref name="T"/>. 
    /// An unlimited number of items can be created, and any released items will be reused.
	/// </summary>
	/// <typeparam name="T">The type of the item to pool.</typeparam>	
	public class ResourcePool<T>
	{
		/// <summary>
		/// Supply to the maxItemReuses constructor parameter or the MaxItemReuses property to indicate infinite reuse of pooled streams.
		/// </summary>
		public const int InfiniteReuse = -1;

		/// <summary>
		/// Used to create a new item for the pool.
		/// </summary>
		/// <returns>A new item for the pool.</returns>
		public delegate T BuildItemDelegate();
		/// <summary>
		/// If an item needs to be reset before it is used again, use a delegate of this type to do so.
		/// </summary>
		/// <param name="item">The item to reset.</param>
		public delegate void ResetItemDelegate(T item);
		private ResourcePoolItem<T> nextItem;
		private readonly MsReaderWriterLock poolLock = new MsReaderWriterLock(System.Threading.LockRecursionPolicy.NoRecursion);
		
		/// <summary>
		/// The delegate for building new items.
		/// </summary>
		protected BuildItemDelegate _buildItemDelegate;
		/// <summary>
		/// The delegate for resetting items.
		/// </summary>
		protected ResetItemDelegate _resetItemDelegate;

		private PerformanceCounter _allocatedItemsCounter;
		private PerformanceCounter _itemsInUseCounter;

		/// <summary>
		/// A PerformanceCounter used to track how many items have been allocated. This library will not create performance counters, you must supply them.
		/// </summary>
		public PerformanceCounter AllocatedItemsCounter
		{
			get
			{
				return _allocatedItemsCounter;
			}
			set
			{
				_allocatedItemsCounter =  value;
			}
		}

		/// <summary>
		/// A PerformanceCounter used to track how many items are currently in use. This library will not create performance counters, you must supply them.
		/// </summary>
		public PerformanceCounter ItemsInUseCounter
		{
			get
			{
				return _itemsInUseCounter;
			}
			set
			{
				_itemsInUseCounter = value;
			}
		}

		/// <summary>
		/// The maximum number of times an item will be reused before it is disposed.
		/// </summary>
		protected int _maxItemUses = 100;

		/// <summary>
		/// The maximum number of times an item will be reused before it is disposed. Use ResourcePool.InfiniteReuse to reuse items indefinitely.
		/// </summary>
		public int MaxItemReuses
		{
			
			get
			{				
				return _maxItemUses;
			}
			set
			{
				if (value == InfiniteReuse || value >= 0)
				{
					Interlocked.Exchange(ref _maxItemUses, value);
				}
			}
		}

        /// <summary>
        /// The minimum number of items in the pool. 
        /// </summary>
        protected short _minimumItemCount;

        /// <summary>
        /// The minimum number of items in the pool. This can only be set on instantiation.
        /// </summary>
        public short MinimumItemCount
        {
            get
            {
                return _minimumItemCount;
            }
            set
            {
                _minimumItemCount = value;
            }
        }

		/// <summary>
		/// For creating decendent classes. If you use this you MUST create a buildItemDelegate.
		/// </summary>
		protected ResourcePool()
		{
		}
		
		/// <summary>
		/// Create a new resource pool that uses <paramref name="buildItemDelegate"/> to build its items.
		/// </summary>
		/// <param name="buildItemDelegate">The delegate for creating new items in the pool.</param>
		public ResourcePool(BuildItemDelegate buildItemDelegate) : this()
		{
			if(buildItemDelegate == null)
			{
				throw new ArgumentNullException("buildItemDelegate");
			}			
			_buildItemDelegate = buildItemDelegate;
		}

		/// <summary>
		/// Create a new resource pool that uses <paramref name="buildItemDelegate"/> to build its items and reuses them <paramref name="maxItemReuses"/> times.
		/// </summary>
		/// <param name="buildItemDelegate">The delegate for creating new items in the pool.</param>
		/// <param name="maxItemReuses">The number of times created items will be reused.  Use ResourcePool.InfiniteReuse to reuse items indefinitely.</param>
		public ResourcePool(BuildItemDelegate buildItemDelegate, int maxItemReuses)
			: this(buildItemDelegate)
		{
			_maxItemUses = maxItemReuses;
		}

        /// <summary>
        /// Create a new resource pool that uses <paramref name="buildItemDelegate"/> to build its items, and creates <paramref name="minimumItemCount"/> permanent ones.
        /// </summary>
        /// <param name="buildItemDelegate">The delegate for creating new items in the pool.</param>
        /// <param name="minimumItemCount">The minimum number of items in the pool. These will be created at instantiation and kept alive indefinitely.</param>
        public ResourcePool(BuildItemDelegate buildItemDelegate, short minimumItemCount)
            : this(buildItemDelegate)
        {
            _minimumItemCount = minimumItemCount;
            InitializeStaticItems(minimumItemCount);
        }

        /// <summary>
        /// Creates <paramref name="minimumItemCount"/> idle static items. 
        /// </summary>
        /// <param name="minimumItemCount">The number of items to create</param>
        protected void InitializeStaticItems(short minimumItemCount)
        {
            ResourcePoolItem<T> item;
            poolLock.Write(() =>
                           	{
								for (int i = 0; i < minimumItemCount; i++)
								{
									item = new ResourcePoolItem<T>(_buildItemDelegate(), this);
									item.Static = true;
									item.Idle = true;
									item.NextItem = nextItem;
									nextItem = item;
									IncrementAllocatedItems();
								}           		
                           	});
        }

		/// <summary>
		/// Create a new resource pool that uses <paramref name="buildItemDelegate"/> to build its items and
		/// <paramref name="resetItemDelegate"/> to reset them.
		/// </summary>
		/// <param name="buildItemDelegate">The delegate for creating new items in the pool.</param>
		/// <param name="resetItemDelegate">The delegate for resetting items in the pool before reuse.</param>
		public ResourcePool(BuildItemDelegate buildItemDelegate, ResetItemDelegate resetItemDelegate) :
			this(buildItemDelegate)
		{
			_resetItemDelegate = resetItemDelegate;
		}

		/// <summary>
		/// Create a new resource pool that uses <paramref name="buildItemDelegate"/> to build its items,
		/// <paramref name="resetItemDelegate"/> to reset them, and will reuse them <paramref name="maxItemReuses"/> times.
		/// </summary>
		/// <param name="buildItemDelegate">The delegate for creating new items in the pool.</param>
		/// <param name="resetItemDelegate">The delegate for resetting items in the pool before reuse.</param>
		/// <param name="maxItemReuses">The number of times created items will be reused.  Use ResourcePool.InfiniteReuse to reuse items indefinitely.</param>
		public ResourcePool(BuildItemDelegate buildItemDelegate, ResetItemDelegate resetItemDelegate, int maxItemReuses)
			:this(buildItemDelegate, resetItemDelegate)
		{
			_maxItemUses = maxItemReuses;
		}


        /// <summary>
        /// Create a new resource pool that uses <paramref name="buildItemDelegate"/> to build its items,
        /// <paramref name="resetItemDelegate"/> to reset them, and creates <paramref name="minimumItemCount"/> permanent ones.
        /// </summary>
        /// <param name="buildItemDelegate">The delegate for creating new items in the pool.</param>
        /// <param name="resetItemDelegate">The delegate for resetting items in the pool before reuse.</param>
        /// <param name="minimumItemCount">The minimum number of items in the pool. These will be created at instantiation and kept alive indefinitely.</param>
        public ResourcePool(BuildItemDelegate buildItemDelegate, ResetItemDelegate resetItemDelegate, short minimumItemCount)
            : this(buildItemDelegate, resetItemDelegate)
        {
            _minimumItemCount = minimumItemCount;
        }

        /// <summary>
        /// Create a new resource pool that uses <paramref name="buildItemDelegate"/> to build its items,
        /// <paramref name="resetItemDelegate"/> to reset them, will reuse them <paramref name="maxItemReuses"/> times, 
        /// and creates <paramref name="minimumItemCount"/> permanent ones.
        /// </summary>
        /// <param name="buildItemDelegate">The delegate for creating new items in the pool.</param>
        /// <param name="resetItemDelegate">The delegate for resetting items in the pool before reuse.</param>
        /// <param name="maxItemReuses">The number of times created items will be reused.  Use ResourcePool.InfiniteReuse to reuse items indefinitely.</param>
        /// <param name="minimumItemCount">The minimum number of items in the pool. These will be created at instantiation and kept alive indefinitely.</param>
        public ResourcePool(BuildItemDelegate buildItemDelegate, ResetItemDelegate resetItemDelegate, int maxItemReuses, short minimumItemCount)
            : this(buildItemDelegate, resetItemDelegate)
        {
            _maxItemUses = maxItemReuses;
            _minimumItemCount = minimumItemCount;
            InitializeStaticItems(minimumItemCount);
        }


		/// <summary>
		/// Gets an item from the pool. If no items are available, a new one will be created.
		/// </summary>
		/// <returns>A ResourcePoolItem from this pool.</returns>
		/// <remarks>You must call ReleaseItem on the Item when you are done with it. It is recommended to use a finally block to do so.</remarks>
		public ResourcePoolItem<T> GetItem()
		{
			ResourcePoolItem<T> item = null;
			poolLock.Write(() =>
			               	{
								if (nextItem != null)
								{
									item = nextItem;
									nextItem = item.NextItem;
									item.NextItem = null;
									item.Idle = false;
									IncrementItemsInUse();
								}           		
			               	});
			if(item != null)
				return item;
			item = new ResourcePoolItem<T>(_buildItemDelegate(), this);
			IncrementAllocatedItems();
			IncrementItemsInUse();
			return item;
		}

		private void IncrementAllocatedItems()
		{
			if (_allocatedItemsCounter != null)
			{
				_allocatedItemsCounter.Increment();
			}
		}

		internal void DecrementAllocatedItems()
		{
			if (_allocatedItemsCounter != null)
			{
				_allocatedItemsCounter.Decrement();
			}
		}

		private void IncrementItemsInUse()
		{
			if (_itemsInUseCounter != null)
			{
				_itemsInUseCounter.Increment();
			}
		}

		private void DecrementItemsInUse()
		{
			if (_itemsInUseCounter != null)
			{
				_itemsInUseCounter.Decrement();
			}
		}

		/// <summary>
		/// Release the item for reuse. If the resetItemDelegate was set, it will be called before the item is reused.
		/// </summary>
		/// <param name="item">The item to release.</param>
		/// <remarks>It is recommended that this be called in a finally block after a try block which uses the item.</remarks>
		public void ReleaseItem(ResourcePoolItem<T> item)
		{
			if (item == null)
			{
				throw new ArgumentNullException("item");
			}
			DecrementItemsInUse();
			if (!item.Idle)
			{
				if (!Expired(item))
				{
					item.TimesUsed++;
					if (_resetItemDelegate != null)
					{
						_resetItemDelegate(item.Item);
					}
					poolLock.Write(() =>
					         	{
									if (nextItem == null)
									{
										nextItem = item;
									}
									else
									{
										ResourcePoolItem<T> newNextItem = item;
										ResourcePoolItem<T> currentNextItem = nextItem;
										newNextItem.NextItem = currentNextItem;
										nextItem = newNextItem;
									}
									item.Idle = true;
					         	});
				}
				else
				{					
					item.Dispose();
				}
			}
		}

		private bool Expired(ResourcePoolItem<T> item)
		{
			if (item.Static || _maxItemUses < 0)
			{
				return false;
			}
			return item.TimesUsed >= _maxItemUses;
		}

		internal void RemoveFromRotation(ResourcePoolItem<T> item)
		{
			poolLock.Write(() =>
			               	{
			               		if (nextItem != null)
			               		{
			               			if (item == nextItem)
			               			{
			               				nextItem = nextItem.NextItem;
			               			}
			               			else
			               			{
			               				ResourcePoolItem<T> pointer = item.NextItem;
			               				ResourcePoolItem<T> prevPointer = null;
			               				while (pointer != null && pointer != item)
			               				{
			               					prevPointer = pointer;
			               					pointer = pointer.NextItem;
			               				}
			               				if (pointer == item && (pointer != null && prevPointer != null))
			               				{
			               					prevPointer.NextItem = pointer.NextItem;
			               				}
			               			}
			               		}
			               	});
		}
	}

	/// <summary>
	/// Represents a Pooled resource item.
	/// </summary>
	/// <typeparam name="T">The type of the pooled item.</typeparam>
	public class ResourcePoolItem<T> : IDisposable
	{
		internal ResourcePoolItem(T item, ResourcePool<T> container)
		{
			_item = item;
			Container = container;
			if (item is IDisposable)
			{
				_isDisposable = true;
			}
			
		}

		internal ResourcePoolItem<T> NextItem;
		internal ResourcePool<T> Container;
		internal bool Idle; //Items are only created as needed - when created they are not idle
		internal int TimesUsed;
        internal bool Static;
        private readonly bool _isDisposable;

		private readonly T _item;
		/// <summary>
		/// The actual item that is being pooled.
		/// </summary>
		public T Item
		{
			get
			{
				return _item;
			}
		}

		/// <summary>
		/// Releases this item. This is equivalent to calling ReleaseItem on the pool that created the item.
		/// </summary>
		public void Release()
		{
			Container.ReleaseItem(this);
		}
		
		#region IDisposable Members
		private bool _disposed;
		/// <summary>
		/// Release the item and all associated resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (!_disposed)
			{				
				if (disposing)
				{
					if (Idle)
					{
						Container.RemoveFromRotation(this);
					}
					if (_isDisposable)
					{
						((IDisposable)_item).Dispose();
					}
					Container.DecrementAllocatedItems();
				}
			}
			_disposed = true;
		}

		#endregion
	}
}
