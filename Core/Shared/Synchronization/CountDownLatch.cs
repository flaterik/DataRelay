using System;
using System.Threading;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>Provides a mechanism to synchronize a one or more operations.</para>
	/// </summary>
	public class CountDownLatch
	{
		private readonly object _syncRoot = new object();
		private readonly bool _runSignaledAsync;
		private EventHandler<AsyncEventArgs> _signaled;
		private volatile int _count;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="CountDownLatch"/> class.</para>
		/// </summary>
		/// <param name="count">
		///	<para>The number of times the latch must be signaled before transitioning to a set state
		///	and releasing any waiting threads.</para>
		/// </param>
		/// <param name="runSignaledAsync">
		///	<para>If <see langword="false"/> is specified the thread that signals the latch will invoke
		///	<see cref="Signaled"/>, otherwise <see cref="Signaled"/> will be queued onto the
		///	<see cref="ThreadPool"/>.</para>
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="count"/> is less than zero.</para>
		/// </exception>
		public CountDownLatch(int count, bool runSignaledAsync)
		{
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count is less than zero.");

			_count = count;
			_runSignaledAsync = runSignaledAsync;
		}

		/// <summary>
		///	<para>Occurs when the count reaches zero. If the count is already
		///	zero the delegate will be invoked immediately on the current thread.</para>
		/// </summary>
		/// <exception cref="ArgumentNullException">
		///	<para>The provided event handler is <see langword="null"/>.</para>
		/// </exception>
		public event EventHandler<AsyncEventArgs> Signaled
		{
			add
			{
				if (value == null) throw new ArgumentNullException("value");

				if(_count <= 0)
				{
					value(this, AsyncEventArgs.Synchronous);
					return;
				}

				bool runSynchronously = false;
				lock (_syncRoot)
				{
					if (_count <= 0)
					{
						runSynchronously = true;
					}
					else
					{
						_signaled = (EventHandler<AsyncEventArgs>)Delegate.Combine(_signaled, value);
					}
				}

				if (runSynchronously)
				{
					value(this, AsyncEventArgs.Synchronous);
				}
			}
			remove
			{
				if (value == null) throw new ArgumentNullException("value");

				if (_signaled == null) return;
				lock (_syncRoot)
				{
					if (_signaled == null) return;
					_signaled = (EventHandler<AsyncEventArgs>)Delegate.Remove(_signaled, value);
				}
			}
		}

		/// <summary>
		/// 	<para>Decrements the count, signals the latch instance if the resulting count is zero.</para>
		/// </summary>
		/// <exception cref="InvalidOperationException">
		///	<para>The count is decremented below zero.</para>
		/// </exception>
		public void Decrement()
		{
#pragma warning disable 420
			int count = Interlocked.Decrement(ref _count);
#pragma warning restore 420
			if (count == 0)
			{
				EventHandler<AsyncEventArgs> signaled;
				lock (_syncRoot)
				{
					signaled = _signaled;
					Monitor.PulseAll(_syncRoot);
				}
				if (_signaled != null)
				{
					if (_runSignaledAsync)
					{
						ThreadPool.QueueUserWorkItem(o => signaled(o, AsyncEventArgs.Asynchronous), this);
					}
					else
					{
						signaled(this, AsyncEventArgs.Asynchronous);
					}
				}
			}
			else if (count < 0)
			{
				throw new InvalidOperationException("CountDownLatch may only be signaled as many times as its count (specified during construction)");
			}
		}

		/// <summary>
		/// 	<para>Waits until the count reaches zero. If the count is already zero the invoking thread will not block.</para>
		/// </summary>
		/// <exception cref="ThreadInterruptedException">
		///	<para>The thread that invokes Wait is later interrupted from the waiting state.
		///	This happens when another thread calls this thread's <see cref="Thread.Interrupt"/> method.</para>
		/// </exception>
		public void Wait()
		{
			Wait(Timeout.Infinite, false);
		}

		/// <summary>
		/// 	<para>Waits until the count reaches zero. If the count is already zero the invoking thread will not block.</para>
		/// </summary>
		/// <param name="timeout">
		///	<para>A <see cref="TimeSpan" /> representing the amount of time to wait before the thread gives up and un-blocks.
		///	If the timeout elapses before the latch's count reaches zero this method will return <see langword="false"/>.</para>
		/// </param>
		/// <param name="exitContext">
		///	<para><see langword="true"/> to exit and reacquire the synchronization domain for the context
		///	(if in a synchronized context) before the wait; otherwise, <see langword="false"/>.</para>
		/// </param>
		/// <exception cref="ThreadInterruptedException">
		///	<para>The thread that invokes Wait is later interrupted from the waiting state.
		///	This happens when another thread calls this thread's <see cref="Thread.Interrupt"/> method.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para>The value of <paramref name="timeout"/> in milliseconds is negative and does not represent
		///	<see cref="Timeout.Infinite" /> (–1 millisecond), or is greater than <see cref="Int32.MaxValue"/>.</para>
		/// </exception>
		/// <returns>
		///	<para><see langword="true"/> if the latch's count reached zero before the specified time elapsed;
		///	<see langword="false"/> if the latch's count did not reach zero after the specified time elapsed.</para>
		/// </returns>
		public bool Wait(TimeSpan timeout, bool exitContext)
		{
			long totalMilliseconds = (long)timeout.TotalMilliseconds;
			if ((totalMilliseconds < Timeout.Infinite) || (totalMilliseconds > int.MaxValue))
			{
				throw new ArgumentOutOfRangeException("timeout", "The value of timeout in milliseconds is negative and does not represent Timeout.Infinite (–1 millisecond), or is greater than Int32.MaxValue");
			}

			return Wait(unchecked((int)totalMilliseconds), exitContext);
		}

		/// <summary>
		/// 	<para>Waits until the count reaches zero. If the count is already zero the invoking thread will not block.</para>
		/// </summary>
		/// <param name="millisecondsTimeout">
		///	<para>An <see cref="Int32" /> representing the amount of time, in milliseconds to wait before the thread gives
		///	up and un-blocks. Specify <see cref="Timeout.Infinite"/> to wait indefinitely. If millisecondsTimeout elapses
		///	before the latch's count reaches zero this method will return <see langword="false"/>.</para>
		/// </param>
		/// <param name="exitContext">
		///	<para><see langword="true"/> to exit and reacquire the synchronization domain for the context
		///	(if in a synchronized context) before the wait; otherwise, <see langword="false"/>.</para>
		/// </param>
		/// <exception cref="ThreadInterruptedException">
		///	<para>The thread that invokes Wait is later interrupted from the waiting state.
		///	This happens when another thread calls this thread's <see cref="Thread.Interrupt"/> method.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para>The value of <paramref name="millisecondsTimeout"/> is negative, and is not equal to <see cref="Timeout.Infinite" />.</para>
		/// </exception>
		/// <returns>
		///	<para><see langword="true"/> if the latch's count reached zero before the specified time elapsed;
		///	<see langword="false"/> if the latch's count did not reach zero after the specified time elapsed.</para>
		/// </returns>
		public bool Wait(int millisecondsTimeout, bool exitContext)
		{
			if (_count <= 0) return true;
			lock (_syncRoot)
			{
				if (_count <= 0) return true;
				if (millisecondsTimeout == 0) return false;
				return Monitor.Wait(_syncRoot, millisecondsTimeout, exitContext);
			}
		}
	}
}
