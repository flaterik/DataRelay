using System;
using System.Threading;

namespace MySpace.Metrics
{
	/// <summary>
	/// Provides the ability to capture a moving average with a very low performance foot print.
	/// </summary>
	/// <remarks>The reason the name is "sloppy" is that the class may allow extra samples in heavy
	/// multi-threaded situations, in favor of less thread locking, and as long as the <see cref="NumberOfSamplesToAverage"/> is not
	/// small the extra samples should not squew the average very far.</remarks>
	public class SloppyMovingAverage
	{
		private long _samplesAggregate;
		private int _sampleCounter = 0;
		private readonly int _numberOfSamplesToAverage = 300;
		private long _average = 0;
		public const int MinimumNumberOfSamples = 3;

		protected readonly object SyncRoot = new object();

		/// <summary>
		/// 	<para>Initialize the current instance.</para>
		/// </summary>
		/// <param name="numberOfSamplesToAverage">
		/// 	<para>The number of samples to obtain before averaging.</para>
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="_numberOfSamplesToAverage"/> 
		/// is less than 3.
		/// </exception>
		public SloppyMovingAverage(int numberOfSamplesToAverage)
		{
			if (numberOfSamplesToAverage < MinimumNumberOfSamples)
			{
				throw new ArgumentOutOfRangeException("numberOfSamplesToAverage", "numberOfSamplesToAverage must be at least " + MinimumNumberOfSamples);
			}
			_numberOfSamplesToAverage = numberOfSamplesToAverage;
		}

		/// <summary>
		/// Gets the value indicating the number of samples before averaging.
		/// </summary>
		public int NumberOfSamplesToAverage
		{
			get { return _numberOfSamplesToAverage; }
		}

		/// <summary>
		/// Gets the current count.
		/// </summary>
		protected int SampleCount
		{
			get { return _sampleCounter; }
		}

		/// <summary>
		/// Gets an average given the current number of samples. Requires at least 3 samples.
		/// </summary>
		/// <remarks>This value should only be used while the <see cref="CurrentAverage"/> is not yet updated.</remarks>
		public long PrematureAverage
		{
			get 
			{ 
				long currentAggregate = Interlocked.Read(ref _samplesAggregate);
				int count = _sampleCounter;
				if (count <= 2)
				{
					if (CurrentAverage != 0) return CurrentAverage;
					return 0;
				}
				return currentAggregate/count;
			}
		}

		/// <summary>
		/// Updates the moving average.
		/// </summary>
		/// <param name="sample">The sample to include in the average.</param>
		public void UpdateAverage(long sample)
		{
			OnUpdate(sample);

			//perfect averages aren't required, so if an extra sample sneaks in due to multi-threading
			//scheduling, it's ok.
			if (Interlocked.Increment(ref _sampleCounter) >= _numberOfSamplesToAverage)
			{
				lock (SyncRoot)
				{
					//do sloppy _average
					long currentAggregate =  _samplesAggregate;
					_sampleCounter= 0;
					_samplesAggregate = 0;
					currentAggregate += sample;
					long newAverage = currentAggregate/_numberOfSamplesToAverage;
					 _average = newAverage;
					OnAverage();
				}
			}
			else
			{
				Interlocked.Add(ref _samplesAggregate, sample);
			}
		}

		/// <summary>
		/// Override this method if the inheritor wants to do extra processing on the sample.
		/// </summary>
		/// <param name="sample">The new sample</param>
		protected virtual void OnUpdate(long sample)
		{
		}

		/// <summary>
		/// Override this method to know when to perform an average.
		/// </summary>
		protected virtual void OnAverage()
		{
		}

		/// <summary>
		/// Gets the current average.
		/// </summary>
		/// <value>An <see cref="Int64"/> from 0 to <see cref="long.MaxValue"/>.</value>
		public long CurrentAverage
		{
			get { return _average; }
		}
	}
}
