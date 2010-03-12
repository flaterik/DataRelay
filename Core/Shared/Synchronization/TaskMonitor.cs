using System;
using System.Collections.Generic;
using System.Security;
using System.Threading;
using MySpace.Logging;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates methods for monitoring tasks with timeouts. The methods in the class
	/// are designed to perform better than traditional methods such as
	/// <see cref="ThreadPool.RegisterWaitForSingleObject(WaitHandle,WaitOrTimerCallback,object,int,bool)"/> or
	/// instantiating a new <see cref="Timer"/> class for every task. The increased performance comes
	/// at the cost of reduced accuracy.
	/// </summary>
	public static class TaskMonitor
	{
		private const int _timeoutFactorPower = 7; // 2 ^ 7 == 128
		private const int _timeoutFactor = 1 << _timeoutFactorPower; // 2 ^ c_timeoutFactorPower
		private const int _timeoutMask = ~(_timeoutFactor - 1); // masks insignificant bits

		private static readonly LogWrapper _log = new LogWrapper();
		private static readonly object _handleQueuesRoot = new object();
		private static Dictionary<RoundTimeoutValue, TaskQueue> _handleQueues = new Dictionary<RoundTimeoutValue, TaskQueue>();

		private static TaskQueue GetQueue(RoundTimeoutValue timeout)
		{
			TaskQueue result;
			if (_handleQueues.TryGetValue(timeout, out result)) return result;
			lock (_handleQueuesRoot)
			{
				if (_handleQueues.TryGetValue(timeout, out result)) return result;

				result = new TaskQueue(timeout);
				var handleQueues = new Dictionary<RoundTimeoutValue, TaskQueue>(_handleQueues);
				handleQueues.Add(timeout, result);
				Thread.MemoryBarrier();
				_handleQueues = handleQueues;
				return result;
			}
		}

		/// <summary>
		///	<para>Timeouts registered will be rounded up to the nearest multiple of this value.</para>
		/// </summary>
		public static readonly int TimeoutFactor = _timeoutFactor;

		/// <summary>
		///	<para>Registers a monitor that will invoke <paramref name="timeoutHandler"/> with argument
		///	<paramref name="state"/> if the consumer does not successfully invoke
		///	<see cref="ITaskHandle.TrySetComplete"/> on the return value before the specified timeout
		///	elapses. <paramref name="timeout"/> will be rounded up to the nearest multiple of
		///	<see cref="TimeoutFactor"/> for performance reasons.</para>
		/// </summary>
		/// <param name="timeout">
		///	<para>The time, in milliseconds, allowed to elapse before the timeout handler will be invoked.
		///	This value will be rounded up to the nearest multiple of <see cref="TimeoutFactor"/>
		///	for performance reasons. If <see cref="Timeout.Infinite"/> (-1) is specified <paramref name="timeoutHandler"/>
		///	will never be called. 0 or values less than -1 are not valid.</para>
		/// </param>
		/// <param name="timeoutHandler">The timeout handler that will run in the event of a timeout.</param>
		/// <param name="state">The user-defined state object that will be passed to <paramref name="timeoutHandler"/>.</param>
		/// <returns>
		///	<para>A <see cref="ITaskHandle"/> implementation that must be set to a complete state
		///	before <paramref name="timeout"/> elapses. If not set <paramref name="timeoutHandler"/>
		///	will be invoked.</para>
		/// </returns>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="timeout"/> timeout is not a positive integer greater than zero or <see cref="Timeout.Infinite"/> (-1).</para>
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="timeoutHandler"/> is <see langword="null"/>.</para>
		/// </exception>
		public static ITaskHandle RegisterMonitor(int timeout, WaitCallback timeoutHandler, object state)
		{
			if (timeout != Timeout.Infinite && timeout <= 0) throw new ArgumentOutOfRangeException("timeout", "timeout must be a positive integer greater than zero or Timeout.Infinite (-1).");
			if (timeoutHandler == null) throw new ArgumentNullException("timeoutHandler");

			var handle = new TaskHandle
			{
				TimeoutHandler = timeoutHandler,
				UserState = state
			};
			handle.Start();

			if (timeout != Timeout.Infinite)
			{
				var queue = GetQueue(timeout);
				lock (queue)
				{
					queue.Enqueue(handle);
				}
			}

			return handle;
		}

		private class TaskQueue
		{
			private const int _maxIterations = 1 << 8;

			private static bool _fullTrust = true;

			private readonly long _timeoutTicks;
			private readonly Queue<TaskHandle> _queue = new Queue<TaskHandle>();

			private readonly Timer _timer;
			private readonly Queue<TaskHandle> _expiredQueue = new Queue<TaskHandle>(_maxIterations / 4);

			public TaskQueue(RoundTimeoutValue timeout)
			{
				_timeoutTicks = timeout.Ticks;
				_timer = new Timer(HandleTimerElapsed);
			}

			public void Enqueue(TaskHandle handle)
			{
				lock (_queue)
				{
					_queue.Enqueue(handle);
					if (_queue.Count == 1)
					{
						ScheduleTimer(handle);
					}
				}
			}

			private void HandleTimerElapsed(object state)
			{
				bool running = true;
				int iteration = 0;
				try
				{
					while (true)
					{
						lock (_queue)
						{
							for (int i = 0; i < _maxIterations; ++i)
							{
								if (_queue.Count == 0)
								{
									running = false;
									break;
								}
								var handle = _queue.Peek();
								if (handle.IsComplete)
								{
									_queue.Dequeue();
									continue;
								}

								if (IsExpired(handle))
								{
									_queue.Dequeue();
									_expiredQueue.Enqueue(handle);
								}
								else
								{
									running = false;
									break;
								}
							}
						}

						if (!running) break;

						if (_expiredQueue.Count == 0)
						{
							Thread.Sleep(iteration & 1);
							++iteration;
						}
						else
						{
							ClearExpiredQueue();
						}
					}
				}
				finally
				{
					ClearExpiredQueue();

					TaskHandle nextHandle = null;
					lock (_queue)
					{
						if (_queue.Count > 0)
						{
							nextHandle = _queue.Peek();
						}
					}
					if (nextHandle != null)
					{
						ScheduleTimer(nextHandle);
					}
				}
			}

			private void ClearExpiredQueue()
			{
				while (_expiredQueue.Count > 0)
				{
					var handle = _expiredQueue.Dequeue();
					if (handle.TrySetComplete())
					{
						RunExpirationHandler(handle);
					}
				}
			}

			private static void RunExpirationHandler(TaskHandle handle)
			{
				bool succeeded = false;
				do
				{
					if (_fullTrust)
					{
						try
						{
							ThreadPool.UnsafeQueueUserWorkItem(handle.TimeoutHandler, handle.UserState);
							succeeded = true;
						}
						catch (SecurityException ex)
						{
							_fullTrust = false;
							_log.WarnFormat("Failed ThreadPool.UnsafeQueueUserWorkItem for security reasons. Switching to use ThreadPool.QueueUserWorkItem: {0}", ex);
						}
					}
					else
					{
						ThreadPool.QueueUserWorkItem(handle.TimeoutHandler, handle.UserState);
						succeeded = true;
					}
				}
				while (!succeeded);
			}

			private void ScheduleTimer(TaskHandle handle)
			{
				int dueTime = (int)TimeSpan.FromTicks(handle.TicksStarted + _timeoutTicks - DateTime.UtcNow.Ticks).TotalMilliseconds + 1;
				if (dueTime < 0) dueTime = 0;
				_timer.Change(dueTime, Timeout.Infinite);
			}

			private bool IsExpired(TaskHandle handle)
			{
				return handle.TicksStarted + _timeoutTicks <= DateTime.UtcNow.Ticks;
			}
		}

		/// <summary>
		///	<para>Encapsulates a time out value rounded to the nearest <see cref="TimeoutFactor"/>.</para>
		/// </summary>
		private struct RoundTimeoutValue : IComparable<RoundTimeoutValue>, IEquatable<RoundTimeoutValue>, IComparable
		{
			private readonly int _value;

			private RoundTimeoutValue(int value)
			{
				if (value > 0) value -= 1;
				_value = (value & _timeoutMask) + _timeoutFactor;
			}

			public static implicit operator int(RoundTimeoutValue timeSpan)
			{
				return timeSpan._value;
			}

			public static implicit operator RoundTimeoutValue(int milliseconds)
			{
				return new RoundTimeoutValue(milliseconds);
			}

			public long Ticks
			{
				get { return TimeSpan.FromMilliseconds(_value).Ticks; }
			}

			public override int GetHashCode()
			{
				return _value >> _timeoutFactorPower;
			}

			public override bool Equals(object obj)
			{
				if (obj == null) return false;
				if (!(obj is RoundTimeoutValue)) throw new ArgumentException("obj must be a RoundTimeoutValue", "obj");
				return Equals((RoundTimeoutValue)obj);
			}

			#region IComparable Members

			public int CompareTo(object obj)
			{
				if (obj == null) return 1;
				if (!(obj is RoundTimeoutValue)) throw new ArgumentException("obj must be a RoundTimeoutValue", "obj");
				return CompareTo((RoundTimeoutValue)obj);
			}

			#endregion

			#region IEquatable<RoundTimeoutValue> Members

			public bool Equals(RoundTimeoutValue other)
			{
				return _value == other._value;
			}

			#endregion

			#region IComparable<RoundTimeoutValue> Members

			public int CompareTo(RoundTimeoutValue other)
			{
				return _value.CompareTo(other._value);
			}

			#endregion
		}

		private class TaskHandle : ITaskHandle
		{
			private const int _incomplete = 0;
			private const int _complete = 1;

			private volatile int _isComplete;

			public object UserState { get; set; }
			public WaitCallback TimeoutHandler { get; set; }
			public long TicksStarted { get; private set; }

			public bool IsComplete
			{
				get { return _isComplete == _complete; }
			}

			public void Start()
			{
				TicksStarted = DateTime.UtcNow.Ticks;
				_isComplete = _incomplete;
			}

			#region ITaskHandle Members

			public bool TrySetComplete()
			{
#pragma warning disable 420
				return _incomplete == _isComplete && _incomplete == Interlocked.Exchange(ref _isComplete, _complete);
#pragma warning restore 420
			}

			#endregion
		}
	}
}