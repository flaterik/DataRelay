using System.Diagnostics;
using System.Threading;

namespace MySpace.Metrics
{
	/// <summary>
	/// Responsible for adding rate information to <see cref="MinMaxAverage"/>.
	/// </summary>
	public class RateMinMaxAverage : MinMaxAverage
	{
		private long _timestamp;
		private int _rate;
		private int _generation;

		/// <summary>
		/// Initializes a new instance of <see cref="RateMinMaxAverage"/>.
		/// </summary>
		/// <param name="numberOfSamplesToAverage">The number of samples to take before performing an average.</param>
		public RateMinMaxAverage(int numberOfSamplesToAverage)
			: base(numberOfSamplesToAverage)
		{
		}

		/// <summary>
		/// Gets the number of samples per second.
		/// </summary>
		public int SamplesPerSecond
		{
			get
			{
				if (GenerationTimer.HasElapsed(_generation, RateCount.MaxTimeForRateCalculation))
				{
					if (SampleCount >= MinimumNumberOfSamples)
					{
						_calculateRate(SampleCount);
					}
					else
					{
						return 0;
					}
				}

				return _rate;
			}
		}

		protected override void OnUpdate(long sample)
		{
			//it's possible that two threads can cause this change and get a slightly inaccurate value for the first time, it's ok that this happens.
			if (_timestamp == 0) Thread.VolatileWrite(ref _timestamp, Stopwatch.GetTimestamp());
			base.OnUpdate(sample);
		}

		protected override void OnAverage()
		{
			_calculateRate(NumberOfSamplesToAverage); 
			base.OnAverage();
		}

		private void _calculateRate(int numberOfSamples)
		{
			long start = Interlocked.Exchange(ref _timestamp, Stopwatch.GetTimestamp());
			double diff = _timestamp - start;
			if (diff != 0) //prevent divide by zero
			{
				double timeInSeconds = (diff / Stopwatch.Frequency);
				int rate = (int)(numberOfSamples / timeInSeconds);
				Thread.VolatileWrite(ref _rate, rate);
				_generation = GenerationTimer.Generation;
			}
		}
	}
}
