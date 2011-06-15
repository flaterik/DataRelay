using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>An implementation of <see cref="PerfCounter"/> that 
    ///		keeps the number of occurrences of an event over a specified interval.</para>
	/// </summary>
	public sealed class PerIntervalCounter : PerfCounter
	{
		private static readonly List<WeakReference> _counters = new List<WeakReference>();
		private static readonly Timer _timer = new Timer(AdvanceCounters, null, -1, -1);

		private static void AddCounter(PerIntervalCounter counter)
		{
			lock (_counters)
			{
				_counters.Add(new WeakReference(counter));

				if (_counters.Count == 1)
				{
					_timer.Change(1000, 1000);
				}
			}
		}

		private static void AdvanceCounters(object state)
		{
			lock (_counters)
			{
				for (int i = 0; i < _counters.Count; ++i)
				{
					var counter = (PerIntervalCounter)_counters[i].Target;

					if (counter == null)
					{
						_counters.RemoveAt(i);
						--i;
					}
					else
					{
						counter.AdvanceSecond();
					}
				}
				if (_counters.Count == 0)
				{
					_timer.Change(-1, -1);
				}
			}
		}

		private int _currentIndex;
		private readonly long[] _perSecNumbers;

		/// <summary>
		/// 	<para>Initializes an instance of the <see cref="PerIntervalCounter"/> class.</para>
		/// </summary>
		/// <param name="name">
		/// 	<para>The name of this counter.</para>
		/// </param>
		/// <param name="intervalLength">
		/// 	<para>The length of the time interval, in seconds, with which to
		///		calculate the sum value.</para>
		/// </param>
		/// <param name="description">
		/// 	<para>The optional description of this counter.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="name"/> is <see langword="null"/>.</para>
		/// </exception>
		public PerIntervalCounter(string name, int intervalLength, string description)
			: base(name, description, PerformanceCounterType.NumberOfItems32, null, false)
		{
			if (intervalLength < 1) throw new ArgumentOutOfRangeException("intervalLength", "intervalLength must be greater than or equal to 1");

			_currentIndex = 0;
			_perSecNumbers = new long[intervalLength];
		}

		/// <summary>
		/// Called when the instance is initialized via <see cref="PerfCounter.Initialize"/>.
		/// </summary>
		protected override void OnInitialize()
		{
			AddCounter(this);
		}

		private void AdvanceSecond()
		{
			long sum = 0;
			foreach (long number in _perSecNumbers)
			{
				sum += number;
			}

			if (_currentIndex >= (_perSecNumbers.Length - 1))
			{
				_currentIndex = 0;
			}
			else
			{
				_currentIndex++;
			}

			_perSecNumbers[_currentIndex] = 0;

			BaseSetRawValue(sum, null);
		}

		/// <summary>
		/// 	<para>Increments this counter by the specified amount.</para>
		/// </summary>
		/// <param name="incrementBy">
		/// 	<para>The amount by which to increment this counter.</para>
		/// </param>
		public void Increment(long incrementBy)
		{
            Interlocked.Add(ref _perSecNumbers[_currentIndex], incrementBy);
		}
	}
}
