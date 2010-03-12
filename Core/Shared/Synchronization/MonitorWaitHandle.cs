using System;
using System.Threading;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>An implementation of <see cref="WaitHandle"/> that uses
	/// 	the <see cref="Monitor"/> class instead of allocating
	/// 	system wait handles. <see cref="Monitor"/> internally uses a combination
	/// 	of spinning and pooled wait handles which offers better performance
	/// 	than manually allocating such handles. The most common use case
	/// 	for this particular class it in custom <see cref="IAsyncResult"/>
	/// 	implementations where the cost of allocating real wait handles is too high.</para>
	/// 	<para>WARNING!!! Do not use this class when calling
	/// 	<see cref="WaitHandle.WaitAll(WaitHandle[])"/>,
	/// 	<see cref="WaitHandle.WaitAny(WaitHandle[])"/>,
	/// 	<see cref="WaitHandle.SignalAndWait(WaitHandle,WaitHandle)"/>
	/// 	or similar methods. Doing so will result in very bad exceptions!!!</para>
	/// </summary>
	/// <remarks>
	///	<para>WARNING!!! Do not use this class when calling
	/// 	<see cref="WaitHandle.WaitAll(WaitHandle[])"/>,
	/// 	<see cref="WaitHandle.WaitAny(WaitHandle[])"/>,
	/// 	<see cref="WaitHandle.SignalAndWait(WaitHandle,WaitHandle)"/>
	/// 	or similar methods. Doing so will result in very bad exceptions!!!</para>
	/// </remarks>
	public class MonitorWaitHandle : WaitHandle
	{
		private bool _isSet;
		private readonly EventResetMode _mode;
		private readonly object _syncRoot = new object();

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="MonitorWaitHandle"/> class.</para>
		/// </summary>
		/// <param name="initialState">
		///	<para><see langword="true"/> to set the initial state to signaled;
		///	<see langword="false"/> to set it to nonsignaled.</para>
		/// </param>
		/// <param name="mode">
		///	<para>One of the <see cref="T:System.Threading.EventResetMode"></see> values
		///	that determines whether the event resets automatically or manually.</para>
		/// </param>
		public MonitorWaitHandle(bool initialState, EventResetMode mode)
		{
			_isSet = initialState;
			_mode = mode;
		}

		/// <summary>
		///	<para>Blocks the current thread until the current <see cref="WaitHandle"/>
		///	receives a signal, using 32-bit signed integer to measure the time interval and
		///	specifying whether to exit the synchronization domain before the wait.</para>
		/// </summary>
		/// <param name="millisecondsTimeout">
		///	<para>The number of milliseconds to wait, or <see cref="Timeout.Infinite"/> (-1) to wait indefinitely.</para>
		/// </param>
		/// <param name="exitContext">
		///	<para><see langword="true"/> to exit the synchronization domain
		///	for the context before the wait (if in a synchronized context),
		///	and reacquire it afterward; otherwise, <see langword="false"/>.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the current instance receives a signal; otherwise, <see langword="false"/>.</para>
		/// </returns>
		/// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed. </exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// 	<paramref name="millisecondsTimeout"/> is a negative number other than -1, which represents an infinite time-out. </exception>
		/// <exception cref="T:System.Threading.AbandonedMutexException">The wait completed because a thread exited without releasing a mutex. This exception is not thrown on Windows 98 or Windows Millennium Edition.</exception>
		/// <exception cref="T:System.InvalidOperationException">The current instance is a transparent proxy for a <see cref="T:System.Threading.WaitHandle"/> in another application domain.</exception>
		public override bool WaitOne(int millisecondsTimeout, bool exitContext)
		{
			if (millisecondsTimeout < -1)
			{
				throw new ArgumentOutOfRangeException("millisecondsTimeout", "millisecondsTimeout cannot be less than -1");
			}

			lock (_syncRoot)
			{
				if (millisecondsTimeout == 0)
				{
					if (_isSet && _mode == EventResetMode.AutoReset)
					{
						_isSet = false;
						return true;
					}
					return _isSet;
				}

				long ticksEntered = millisecondsTimeout > 0 ? DateTime.UtcNow.Ticks : 0L;
				while (!_isSet)
				{
					if (millisecondsTimeout > 0)
					{
						int waitTime;
						unchecked
						{
							waitTime = millisecondsTimeout - (int)TimeSpan.FromTicks((DateTime.UtcNow.Ticks - ticksEntered)).TotalMilliseconds;
						}
						if (waitTime <= 0) return false;
						Monitor.Wait(_syncRoot, waitTime, exitContext);
					}
					else
					{
						Monitor.Wait(_syncRoot);
					}
				}
				if (_mode == EventResetMode.AutoReset)
				{
					_isSet = false;
				}
				return true;
			}
		}

		/// <summary>
		/// Blocks the current thread until the current instance receives a signal, using a <see cref="T:System.TimeSpan"/> to measure the time interval and specifying whether to exit the synchronization domain before the wait.
		/// </summary>
		/// <param name="timeout">A <see cref="T:System.TimeSpan"/> that represents the number of milliseconds to wait, or a <see cref="T:System.TimeSpan"/> that represents -1 milliseconds to wait indefinitely.</param>
		/// <param name="exitContext">true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it afterward; otherwise, false.</param>
		/// <returns>
		/// true if the current instance receives a signal; otherwise, false.
		/// </returns>
		/// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed. </exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// 	<paramref name="timeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out.-or-<paramref name="timeout"/> is greater than <see cref="F:System.Int32.MaxValue"/>. </exception>
		/// <exception cref="T:System.Threading.AbandonedMutexException">The wait completed because a thread exited without releasing a mutex. This exception is not thrown on Windows 98 or Windows Millennium Edition.</exception>
		/// <exception cref="T:System.InvalidOperationException">The current instance is a transparent proxy for a <see cref="T:System.Threading.WaitHandle"/> in another application domain.</exception>
		public override bool WaitOne(TimeSpan timeout, bool exitContext)
		{
			long totalMilliseconds = (long)timeout.TotalMilliseconds;
			if ((-1L > totalMilliseconds) || (int.MaxValue < totalMilliseconds))
			{
				throw new ArgumentOutOfRangeException(
					"timeout",
					string.Format("timeout must be between -1 and {0} milliseconds", int.MaxValue));
			}
			return WaitOne((int)totalMilliseconds, exitContext);
		}

		/// <summary>
		/// Blocks the current thread until the current <see cref="T:System.Threading.WaitHandle"/> receives a signal.
		/// </summary>
		/// <returns>
		/// true if the current instance receives a signal. If the current instance is never signaled, <see cref="M:System.Threading.WaitHandle.WaitOne(System.Int32,System.Boolean)"/> never returns.
		/// </returns>
		/// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed. </exception>
		/// <exception cref="T:System.Threading.AbandonedMutexException">The wait completed because a thread exited without releasing a mutex. This exception is not thrown on Windows 98 or Windows Millennium Edition.</exception>
		/// <exception cref="T:System.InvalidOperationException">The current instance is a transparent proxy for a <see cref="T:System.Threading.WaitHandle"/> in another application domain.</exception>
		public override bool WaitOne()
		{
			return WaitOne(-1, false);
		}

		/// <summary>Sets the state of the event to signaled, allowing one or more waiting threads to proceed.</summary>
		/// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
		/// <filterpriority>2</filterpriority>
		public void Set()
		{
			lock (_syncRoot)
			{
				if (!_isSet)
				{
					_isSet = true;
					if (_mode == EventResetMode.AutoReset)
					{
						Monitor.Pulse(_syncRoot);
					}
					else
					{
						Monitor.PulseAll(_syncRoot);
					}
				}
			}
		}

		/// <summary>Sets the state of the event to nonsignaled, causing threads to block.</summary>
		/// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
		/// <filterpriority>2</filterpriority>
		public void Reset()
		{
			lock (_syncRoot)
			{
				_isSet = false;
			}
		}
	}
}
