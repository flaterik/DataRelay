using System;
using System.Threading;

namespace MySpace.Metrics
{
	/// <summary>
	/// Represents a <see cref="SloppyMovingAverage"/> that captures the Min and Max averages and Min Max values ever encountered.
	/// </summary>
	public class MinMaxAverage : SloppyMovingAverage
	{
		private long _minCurrent = long.MaxValue;
		private long _maxCurrent = long.MinValue;
		private long _minEncountered = long.MaxValue;
		private long _maxEncountered = long.MinValue;

		private readonly SloppyMovingAverage _minAverage = new SloppyMovingAverage(MinimumNumberOfSamples);
		private readonly SloppyMovingAverage _maxAverage = new SloppyMovingAverage(MinimumNumberOfSamples);

		/// <summary>
		/// Initializes a new instance of <see cref="MinMaxAverage"/>.
		/// </summary>
		/// <param name="numberOfSamplesToAverage">The number of samples to take before taking an average.  This number times
		/// <see cref="SamplesToAverageMinMax"/> are required to get values for <see cref="MinAverage"/> and <see cref="MaxAverage"/>.</param>
		public MinMaxAverage(int numberOfSamplesToAverage) : base(numberOfSamplesToAverage)
		{
			_resetSampleSet();
		}

		private void _resetSampleSet()
		{
			Interlocked.Exchange(ref _minCurrent, long.MaxValue);
			Interlocked.Exchange(ref _maxCurrent, long.MinValue);
		}

		/// <summary>
		/// Overridden. Updates the Min Max Samples.
		/// </summary>
		/// <param name="sample">The current sample.</param>
		protected override void OnUpdate(long sample)
		{
			if (sample > _maxCurrent) Thread.VolatileWrite(ref _maxCurrent, sample);
			if (sample < _minCurrent) Thread.VolatileWrite(ref _minCurrent, sample);
			if (sample > _maxEncountered) Thread.VolatileWrite(ref _maxEncountered, sample);
			if (sample < _minEncountered) Thread.VolatileWrite(ref _minEncountered, sample);
			base.OnUpdate(sample);
		}

		/// <summary>
		/// Overridden. Updates the Average Min and Max values. 
		/// </summary>
		protected override void OnAverage()
		{
			if (_minCurrent != Int32.MaxValue) _minAverage.UpdateAverage(_minCurrent);
			if(_maxCurrent != Int32.MinValue) _maxAverage.UpdateAverage(_maxCurrent);
			_resetSampleSet();
			base.OnAverage();
		}

		/// <summary>
		/// Gets the number of sample sets required to obtain an average Min and an average Max.
		/// </summary>
		public int SamplesToAverageMinMax
		{
			get { return MinimumNumberOfSamples; }
		}

		/// <summary>
		/// Gets the average maximum value encountered. 
		/// </summary>
		/// <remarks>This value is only available of after <see cref="SamplesToAverageMinMax"/> average sets have occured.</remarks>
		public long MaxAverage
		{
			get { return _maxAverage.CurrentAverage; }
		}

		/// <summary>
		/// Gets the average minimum value encountered. 
		/// </summary>
		/// <remarks>This value is only available of after <see cref="SamplesToAverageMinMax"/> average sets have occured.</remarks>
		public long MinAverage
		{
			get { return _minAverage.CurrentAverage; }
		}
		
		/// <summary>
		/// Gets the Maximum Value ever encountered.
		/// </summary>
		public long MaxEncountered
		{
			get { return _maxEncountered; }
		}

		/// <summary>
		/// Gets the Minimum Value ever encountered. 
		/// </summary>
		public long MinEncountered
		{
			get { return _minEncountered; }
		}

		/// <summary>
		/// Gets the Maximum Value ever encountered.  If no samples have been taken then return the <param name="defaultValue"/> instead.
		/// </summary>
		public long GetMaxEncountered(long defaultValue)
		{
			return _maxEncountered == long.MinValue ? defaultValue : _maxEncountered; 
		}

		/// <summary>
		/// Gets the Minimum Value ever encountered. If no samples have been taken then return the <param name="defaultValue"/> instead.
		/// </summary>
		public long GetMinEncountered(long defaultValue)
		{
			return _minEncountered == long.MaxValue ? defaultValue : _minEncountered; 
		}
	}
}
