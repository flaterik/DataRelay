using System;
using System.Diagnostics;
using System.Threading;

namespace MySpace.Metrics
{
	/// <summary>
	/// Captures a Sample Rate and total count.
	/// </summary>
	public class RateCount
	{
		private long _timestamp;
		private long _count;
		private long _lastRateCount;
		private int _generation;
		private int _rate;
		internal static readonly TimeSpan MaxTimeForRateCalculation = TimeSpan.FromSeconds(30);

		protected const int MinimumNumberOfSamples = 3;

		/// <summary>
		/// Initializes a new instance of <see cref="RateCount"/>.
		/// </summary>
		public RateCount(int numberOfSamplesToCalculateRate)
		{
			NumberOfSamplesToCalculateRate = numberOfSamplesToCalculateRate;
		}

		/// <summary>
		/// Gets the number of samples to collect before 
		/// </summary>
		public int NumberOfSamplesToCalculateRate
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the number of samples per second.
		/// </summary>
		public int IncrementsPerSecond
		{
			get
			{
				if (GenerationTimer.HasElapsed(_generation, RateCount.MaxTimeForRateCalculation))
				{
					long diff = _count - _lastRateCount;
					if ((diff) >= MinimumNumberOfSamples)
					{
						_calculateRate(diff);
					}
					else
					{
						return 0;
					}
				}

				return _rate;
			}
		}
		
		/// <summary>
		/// Gets the current count.
		/// </summary>
		public long Count
		{
			get { return _count; }
		}

		/// <summary>
		/// Increments the count.
		/// </summary>
		public void Increment()
		{
			//it's possible that two threads can cause this change and get a slightly inaccurate value for the first time, it's ok that this happens.
			if (_timestamp == 0) Thread.VolatileWrite(ref _timestamp, Stopwatch.GetTimestamp());
			if (Interlocked.Increment(ref _count) >= NumberOfSamplesToCalculateRate)
			{
				_calculateRate(NumberOfSamplesToCalculateRate);
			}
		}

		private void _calculateRate(long numberOfSamples)
		{
			lock (this)  //not ideal, but ok
			{
				_lastRateCount = _count;
				long start = _timestamp;
				_timestamp = Stopwatch.GetTimestamp();
				double diff = _timestamp - start;
				if (diff != 0) //prevent divide by zero
				{
					double timeInSeconds = (diff/Stopwatch.Frequency);
					int rate = (int) (numberOfSamples/timeInSeconds);
					 _rate = rate;
					_generation = GenerationTimer.Generation;
				}
			}
		}
	}
}
