using System;

namespace MySpace.Shared
{
	/// <summary>
	/// Respresents a Minimum priority queue.  This class is thread safe.
	/// </summary>
	/// <typeparam name="T">Must be a reference type and represents the type of item that heap manages.</typeparam>
	public class MinimumPriorityQueue<T> where T : class
	{
		/// <summary>
		/// The minimum size of the Queue.
		/// </summary>
		/// <remarks>
		/// The minimum size is set to a larger value because resizing is expensive.
		/// </remarks>
		public const int MinSize = 1000;
		/// <summary>
		/// The lowest value that priority can be.
		/// </summary>
		public const int MinimumPriority = 0;

		/// <summary>
		/// The heap, a tree stored as an array.
		/// </summary>
		private QueueItem<T>[] _data;

		/// <summary>
		/// The number of items currently in the heap.
		/// </summary>
		private int _heapSize = 0; //base 1

		/// <summary>
		/// Used for both the initial size and growth and shrink equations.
		/// </summary>
		private readonly int _initialSize = 0; //base 1

		/// <summary>
		/// Syncronization object for the whole class.
		/// </summary>
		private readonly object _syncRoot = new object();

		/// <summary>
		/// How much to grow as a percent of <see cref="_initialSize"/>. 0.5 is 50% of 
		/// <see cref="_initialSize"/> and 2 is 200% <see cref="_initialSize"/>.
		/// </summary>
		internal const float GrowthFactor = 1;

		/// <summary>
		/// How much free space is required before performing a shrink
		/// </summary>
		internal const int ShrinkFactor = 10;

		/// <summary>
		/// 	<para>Initializes the heap with an initial size.  Resize operation are very expensive so consider using a size that is large enough.</para>
		/// </summary>
		/// <param name="initialSize">
		/// 	<para>The initial size of the the heap. Must be greater than or equal to 1000.</para>
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialSize"/> is 
		/// less than 1000.
		/// </exception>
		public MinimumPriorityQueue(int initialSize)
		{
			if (initialSize < MinSize)
			{
				throw new ArgumentOutOfRangeException("initialSize", initialSize, string.Format("Initial size must be greater than or equal to {0}.", MinSize));
			}
			_data = new QueueItem<T>[initialSize];
			_initialSize = initialSize;
		}


		/// <summary>
		/// Returns and removes the minimum item off the heap.  Runs in O(lg n) time.
		/// </summary>
		/// <returns>Returns and removes the minimum item if the heap is not empty; otherwise, returns null.</returns>
		public T Pop()
		{
			if (_heapSize < 1) return null;
			lock (_syncRoot)
			{
				if (_heapSize < 1) return null;

				QueueItem<T> min = _data[0];
				//take the last item, put it at the beginning to ensure 
				//min heap works property is maintained
				int lastItemIndex = _heapSize - 1;
				_data[0] = _data[lastItemIndex];
				_data[lastItemIndex] = QueueItem<T>.Empty; //free
				_heapSize--;
				_minHeapify(0);
				_shrinkIfNeccesary();
				return min.Item;
			}
		}

		/// <summary>
		/// Returns the minimum item off the heap without removing it.  Runs in O(1) time.
		/// </summary>
		/// <param name="priority">Out parameter that is set to the priority of the minimum
		/// item if the heap is not empty; otherwise, is one less than the <see cref="MinimumPriority"/>.</param>
		/// <returns>Returns the minimum item if the heap is not empty; otherwise, returns null.</returns>
		public T Peek(out int priority)
		{
			priority = MinimumPriority - 1;
			if (_heapSize < 1) return null;
			lock (_syncRoot)
			{
				if (_heapSize < 1) return null;
				else
				{
					priority = _data[0].Priority;
					return _data[0].Item;
				}
			}
		}

		/// <summary>
		/// 	<para>Conditionally returns and removes the minimum item off the heap if the <see cref="Predicate{T}"/> evaluates true.  Runs in O(lg n) time.</para>
		/// </summary>
		/// <param name="predicate">
		/// 	<para>A <see cref="Predicate{T}"/> to evaluate; never, null.</para>
		/// </param>
		/// <returns>
		/// 	<para>Returns and removes the minimum item if the heap is not empty; otherwise, returns null.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="predicate"/> is <see langword="null"/>.</para>
		/// </exception>
		public T PopConditionally(Predicate<int> predicate)
		{
			if (predicate == null) throw new ArgumentNullException("predicate");

			if (_heapSize < 1) return null;

			lock (_syncRoot)
			{
				if (_heapSize < 1) return null;
				if (predicate(_data[0].Priority))
				{
					return Pop();
				}
			}
			return null;
		}

		/// <summary>
		/// Gets the current number of items in the queue.
		/// </summary>
		/// <value>An <see cref="Int32"/> from 0 to <see cref="Int32.MaxValue"/>.</value>
		public int Count
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				lock (_syncRoot)
				{
					return _heapSize;
				}
			}
		}

		/// <summary>
		/// 	<para>Adds a new item to the heap. Runs in O(lg n) unless a resize occurs. Runs in O(n lg n) for a resize.</para>
		/// </summary>
		/// <param name="item">The item to add to the queue.
		/// </param>
		/// <param name="priority">The priority that the item being added should have.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="item"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when priority is less than zero.
		/// </exception>
		public void Push(T item, int priority)
		{
			if (item == null) throw new ArgumentNullException("item");
			if (priority < MinimumPriority)
			{
				throw new ArgumentOutOfRangeException("priority", priority, "Priority must be greater than or equal to MinimumPriority.");
			}

			lock (_syncRoot)
			{
				_heapSize++;
				if (_heapSize > _data.Length)
				{
					_grow();
				}

				int lastPositionIndex = _heapSize - 1;
				_data[lastPositionIndex] = new QueueItem<T>(priority, item);
				_heapifyLastItemWithParents();
			}
		}

		/// <summary>
		/// Maintains the min heap property (parent is less than or equal to it's children)
		/// by ensuring the data at the last position is moved upward 
		/// from child to parent to it's correct position. Runs in O(lg n).
		/// </summary>
		private void _heapifyLastItemWithParents()
		{
			int itemIndex = _heapSize - 1;

			int parentIndex = -1;
			for (int i = itemIndex; i > 0; i = parentIndex)
			{
				parentIndex = _getParentIndex(i);
				if (_data[i].Priority < _data[parentIndex].Priority)
				{
					_swap(i, parentIndex);
				}
				else
				{
					break; //let's go home the min heap property is good
				}
			}
		}

		/// <summary>
		/// Maintains the min heap property of parent is less than or equal to it's children.
		/// Runs in O(lg n).  Starts at <paramref name="startAtIndex"/> and works down from
		/// parent to child.
		/// </summary>
		/// <param name="startAtIndex">The index to start working down from.</param>
		private void _minHeapify(int startAtIndex)
		{
			if (startAtIndex >= _heapSize) return;

			int l = _getLeftChildIndex(startAtIndex);
			int r = _getRightChildIndex(startAtIndex);
			int smallestIndex = startAtIndex;

			if (l < _heapSize && _data[l].Priority < _data[smallestIndex].Priority)
			{
				smallestIndex = l;
			}

			if (r < _heapSize && _data[r].Priority < _data[smallestIndex].Priority)
			{
				smallestIndex = r;
			}

			if (smallestIndex != startAtIndex)
			{
				_swap(startAtIndex, smallestIndex);
				_minHeapify(smallestIndex);
			}
		}

		/// <summary>
		/// Swaps the data at positions <paramref name="indexA"/> and <paramref name="indexB"/>
		/// in the <see cref="_data"/> array.
		/// </summary>
		/// <param name="indexA">The index for the data that will be swapped with the data stored at <paramref name="indexB"/>.</param>
		/// <param name="indexB">The index for the data that will be swapped with the data stored at <paramref name="indexA"/>.</param>
		private void _swap(int indexA, int indexB)
		{
			QueueItem<T> tmp = _data[indexB];
			_data[indexB] = _data[indexA];
			_data[indexA] = tmp;
		}

		/// <summary>
		/// Returns the left child index of the given index.
		/// </summary>
		/// <param name="index">Index of the item to find the child for.</param>
		/// <returns>The index of the left child.</returns>
		private static int _getLeftChildIndex(int index)
		{
			return index * 2 + 1;  //breaks when index * 2 + 1 > int.MaxValue
		}

		/// <summary>
		/// Returns the right child index of the given index.
		/// </summary>
		/// <param name="index">Index of the item to find the child for.</param>
		/// <returns>The index of the right child.</returns>
		private static int _getRightChildIndex(int index)
		{
			return index * 2 + 2; //breaks when index * 2 + 2> int.MaxValue
		}

		/// <summary>
		/// Returns the parent index of the given index.
		/// </summary>
		/// <param name="index">The index of the item to find the parent for.</param>
		/// <returns>Returns the index of the parent.</returns>
		private static int _getParentIndex(int index)
		{
			return (index - 1) / 2;
		}

		/// <summary>
		/// If a shrink occurs, performs in O(n)... ouch!
		/// </summary>
		private void _shrinkIfNeccesary()
		{
			if (_data.Length == _initialSize) return; // don't shrink smaller than initial size.

			int emptySpace = _data.Length - _heapSize;
			int threshold = _initialSize * ShrinkFactor;
			
			//if the difference is shrink factor times the initial size do a shrink
			if (emptySpace >= threshold)
			{
				//shrink 
				int newSize = _heapSize + Convert.ToInt32(_initialSize * GrowthFactor);
				if (newSize < _initialSize) //don't shrink smaller than initial size
				{
					newSize = _initialSize;
				}
			
				Array.Resize(ref _data, newSize);
			}
		}

		/// <summary>
		/// Performs in O(n)... ouch!
		/// </summary>
		private void _grow()
		{
			int newSize = _data.Length + (int)(_initialSize * GrowthFactor);
			Array.Resize(ref _data, newSize);
		}

		#region private struct QueueItem<U>

		internal struct QueueItem<U> where U : class
		{
			public int Priority;
			public U Item;

			public QueueItem(int priority, U item)
			{
				Priority = priority;
				Item = item;
			}

			public static QueueItem<U> Empty
			{
				get
				{
					return new QueueItem<U>(MinimumPriorityQueue<U>.MinimumPriority - 1, null);
				}
			}
		}

		#endregion

	}
}
