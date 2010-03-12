using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>A producer / consumer queue that will block consumers when the queue is empty.</para>
	/// </summary>
	/// <typeparam name="T">
	///	<para>The type of item contained by the queue.</para>
	/// </typeparam>
	public sealed class BlockingQueue<T> : IEnumerable<T>, ICollection
	{
		private readonly Queue<T> _queue = new Queue<T>();
		private int _waitingDequeuers;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="BlockingQueue{T}"/> class.</para>
		/// </summary>
		public BlockingQueue() { }

		/// <summary>
		///	<para>Adds an object to the end of the <see cref="BlockingQueue{T}"/>.
		///	Wakes up any threads waiting for items if necessary.</para>
		/// </summary>
		/// <param name="item">
		///	<para>The object to add to the <see cref="BlockingQueue{T}" />.
		///	The value can be <see langword="null"/> for reference types.</para>
		/// </param>
		public void Enqueue(T item)
		{
			lock (SyncRoot)
			{
				_queue.Enqueue(item);

				if (_waitingDequeuers > 0)
				{
					Monitor.Pulse(SyncRoot);
				}
			}
		}

		/// <summary>
		///	<para>Removes and returns the object at the beginning of the <see cref="BlockingQueue{T}"/>.
		///	Blocks the invoking thread if no items are available.</para>
		/// </summary>
		/// <returns>
		///	<para>The object that is removed from the beginning of the <see cref="BlockingQueue{T}"/>.</para>
		/// </returns>
		public T Dequeue()
		{
			T result;
			TryDequeue(out result, Timeout.Infinite, false);
			return result;
		}

		/// <summary>
		///	<para>Removes and sets <param name="item"/> to the object
		///	at the beginning of the <see cref="BlockingQueue{T}"/>.
		///	Blocks the invoking thread if no items are available.</para>
		/// </summary>
		/// <param name="item">
		///	<para>The object that is removed from the beginning of the <see cref="BlockingQueue{T}"/>.</para>
		/// </param>
		/// <param name="millisecondsTimeout">
		///	<para>The number of milliseconds to wait if empty,
		///	or <see cref="Timeout.Infinite"/> (-1) to wait indefinitely.</para>
		/// </param>
		/// <param name="exitContext">
		///	<para><see langword="true"/> to exit the synchronization domain
		///	for the context before the wait (if in a synchronized context),
		///	and reacquire it afterward; otherwise, <see langword="false"/>.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> on success; <see langword="false"/>
		///	if <paramref name="millisecondsTimeout"/> elapsed before any items became available.</para>
		/// </returns>
		public bool TryDequeue(out T item, int millisecondsTimeout, bool exitContext)
		{
			if (millisecondsTimeout < -1)
			{
				throw new ArgumentOutOfRangeException("millisecondsTimeout", "millisecondsTimeout cannot be less than -1");
			}

			lock (SyncRoot)
			{
				if (_queue.Count > 0)
				{
					item = _queue.Dequeue();
					return true;
				}

				if (millisecondsTimeout == 0)
				{
					item = default(T);
					return false;
				}

				long ticksEntered = millisecondsTimeout > 0 ? DateTime.UtcNow.Ticks : 0L;
				while (_queue.Count == 0)
				{
					int waitTime = Timeout.Infinite;
					if (millisecondsTimeout > 0)
					{
						waitTime = millisecondsTimeout - unchecked((int)TimeSpan.FromTicks((DateTime.UtcNow.Ticks - ticksEntered)).TotalMilliseconds);

						if (waitTime <= 0)
						{
							item = default(T);
							return false;
						}
					}

					++_waitingDequeuers;
					bool pulsed = Monitor.Wait(SyncRoot, waitTime, exitContext);
					--_waitingDequeuers;

					if (!pulsed)
					{
						item = default(T);
						return false;
					}
				}

				item = _queue.Dequeue();
				return true;
			}
		}

		/// <summary>
		///	<para>Removes and sets <param name="item"/> to the object
		///	at the beginning of the <see cref="BlockingQueue{T}"/>.
		///	Blocks the invoking thread if no items are available.</para>
		/// </summary>
		/// <param name="item">
		///	<para>The object that is removed from the beginning of the <see cref="BlockingQueue{T}"/>.</para>
		/// </param>
		/// <param name="timeout">
		///	<para>A <see cref="TimeSpan"/> that represents the number of
		///	milliseconds to wait, or a <see cref="TimeSpan"/> that represents
		///	-1 milliseconds to wait indefinitely.</para>
		/// </param>
		/// <param name="exitContext">
		///	<para><see langword="true"/> to exit the synchronization domain
		///	for the context before the wait (if in a synchronized context),
		///	and reacquire it afterward; otherwise, <see langword="false"/>.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> on success; <see langword="false"/>
		///	if <paramref name="timeout"/> elapsed before any items became available.</para>
		/// </returns>
		public bool TryDequeue(out T item, TimeSpan timeout, bool exitContext)
		{
			long totalMilliseconds = (long)timeout.TotalMilliseconds;
			if ((-1L > totalMilliseconds) || (int.MaxValue < totalMilliseconds))
			{
				throw new ArgumentOutOfRangeException(
					"timeout",
					string.Format("timeout must be between -1 and {0} milliseconds", int.MaxValue));
			}
			return TryDequeue(out item, unchecked((int)totalMilliseconds), exitContext);
		}

		/// <summary>
		/// Gets the number of elements contained in the <see cref="BlockingQueue{T}"/>.
		/// </summary>
		/// <value></value>
		/// <returns>The number of elements contained in the <see cref="BlockingQueue{T}"/>.</returns>
		public int Count
		{
			get { lock (SyncRoot) return _queue.Count; }
		}

		private object SyncRoot
		{
			get { return _queue; }
		}

		#region IEnumerable<T> Members

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			T[] result;
			lock (SyncRoot)
			{
				result = new T[_queue.Count];
				_queue.CopyTo(result, 0);
			}
			return ((IEnumerable<T>)result).GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<T>)this).GetEnumerator();
		}

		#endregion

		#region ICollection Members

		void ICollection.CopyTo(Array array, int index)
		{
			lock (SyncRoot)
			{
				((ICollection)_queue).CopyTo(array, index);
			}
		}

		int ICollection.Count
		{
			get
			{
				lock (SyncRoot)
				{
					return ((ICollection)_queue).Count;
				}
			}
		}

		bool ICollection.IsSynchronized
		{
			get { return true; }
		}

		object ICollection.SyncRoot
		{
			get { return SyncRoot; }
		}

		#endregion
	}
}
