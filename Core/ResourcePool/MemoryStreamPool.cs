using System.IO;
using System.Threading;

namespace MySpace.ResourcePool
{	
	/// <summary>
	/// A ResourcePool of MemoryStreams.
	/// </summary>
	/// <example>
	/// <code>
	/// MemoryStreamPool memoryPoolStream = new MemoryStreamPool();
	///	ResourcePoolItem&lt;MemoryStream&gt; pooledStream = null;
	///	try
	///	{
	///		pooledStream = memoryPoolStream.GetItem();
	///		MemoryStream stream = pooledStream.Item;
	///	}
	///	finally
	///	{
	///		memoryPoolStream.ReleaseItem(pooledStream);
	///	}
	/// </code>
	/// </example>
	public sealed class MemoryStreamPool : ResourcePool<MemoryStream>
	{		
		/// <summary>
		/// Creates a new MemoryStreamPool. New MemoryStreams will have an initial capacity of 0.
		/// </summary>
		public MemoryStreamPool() 
		{		
			_buildItemDelegate = new BuildItemDelegate(BuildBuffer);
			_resetItemDelegate = new ResetItemDelegate(ResetBuffer);			
		}

		/// <summary>
		/// Creates a new MemoryStreamPool. 
		/// New MemoryStreams will have an initial capacity of <paramref name="initialBufferSize"/>
		/// </summary>
		/// <param name="initialBufferSize">The initial capacity of new MemoryStreams</param>
		public MemoryStreamPool(int initialBufferSize) : this()
		{
			initialSize = initialBufferSize;			
		}

		/// <summary>
		/// Creates a new MemoryStreamPool. 
		/// New MemoryStreams will have an initial capacity of <paramref name="initialBufferSize"/>, 
		/// and buffers will be used <paramref name="bufferReuses"/> times.
		/// </summary>
		/// <param name="initialBufferSize">The initial capacity of new MemoryStreams</param>
		/// <param name="bufferReuses">The number of times a MemoryStream will be reused. Use ResourcePool.InfiniteReuse to reuse streams indefinitely.</param>		
		public MemoryStreamPool(int initialBufferSize, int bufferReuses)
			: this(initialBufferSize)
		{
			_maxItemUses = bufferReuses;

		}

        /// <summary>
        /// Creates a new MemoryStreamPool. 
        /// New MemoryStreams will have an initial capacity of <paramref name="initialBufferSize"/>, 
        /// and buffers will be used <paramref name="bufferReuses"/> times. <paramref name="minimumItems"/> items will be created initially and reused indefinitely.
        /// </summary>
        /// <param name="initialBufferSize">The initial capacity of new MemoryStreams</param>
        /// <param name="bufferReuses">The number of times a MemoryStream will be reused. Use ResourcePool.InfiniteReuse to reuse streams indefinitely.</param>		
        /// <param name="minimumItems">The number of permenent buffers to create on instantiation.</param>
        public MemoryStreamPool(int initialBufferSize, int bufferReuses, short minimumItems)
            : this(initialBufferSize, bufferReuses)
        {
            _minimumItemCount = minimumItems;
            InitializeStaticItems(minimumItems);
            
        }

		private int initialSize;

		/// <summary>
		/// The initial capacity of new MemoryStreams.
		/// </summary>
		public int InitialBufferSize
		{
			get
			{
				return initialSize;
			}
			set
			{
				if (value > 0)
				{
					Interlocked.Exchange(ref initialSize, value);
				}
			}
		}

		/// <summary>
		/// The number of times a buffer will be reused. Use MemoryStreamPool.InfiniteReuse to reuse streams indefinitely.
		/// </summary>
		public int BufferReuses
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

		private MemoryStream BuildBuffer()
		{
			if (initialSize > 0)
			{
				return new MemoryStream(initialSize);
			}
			
			return new MemoryStream();
		}
		
		private static void ResetBuffer(MemoryStream buffer)
		{
			buffer.Seek(0, SeekOrigin.Begin);
			buffer.SetLength(0);
		}


	}
}
