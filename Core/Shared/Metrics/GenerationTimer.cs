using System;
using System.Threading;

namespace MySpace.Metrics
{
	internal static class GenerationTimer
	{
		private static int _generation = 1;
		private static readonly Timer _timer = new Timer(_updateGeneration);
		private const int _rolloverValue = Int32.MaxValue - 1000;
		public const int GenerationFrequencyMs = 5000;

		static GenerationTimer()
		{
			_timer.Change(GenerationFrequencyMs, GenerationFrequencyMs);
		}

		private static void _updateGeneration(object obj)
		{
			//when we roll, we could get a bad calc, let's just swallow it
			int newValue = Interlocked.Increment(ref _generation);
			if (newValue > _rolloverValue) Thread.VolatileWrite(ref _generation, 1);
		}

		public static int Generation
		{
			get { return _generation; }
		}

		/// <summary>
		/// Returns a value indicating if the given amount of time has passed.
		/// </summary>
		/// <param name="generation">The generation to check the time for.</param>
		/// <param name="time">How much time you want to see has elapsed.</param>
		/// <returns>Returns if at least that amount of time has elapsed.</returns>
		public static bool HasElapsed(int generation, TimeSpan time)
		{
			int currentGen = _generation;
			if (generation == currentGen)
			{
				return (time.TotalMilliseconds == 0);
			}
			int elapsedGenerations;
			if (generation > currentGen) // we rolled over
			{
				elapsedGenerations = _rolloverValue - generation + currentGen;
			}
			else
			{
				elapsedGenerations = currentGen - generation;
			}
			long elapsedTime = elapsedGenerations*GenerationFrequencyMs;
			long ms = (long) time.TotalMilliseconds;

			return elapsedTime > ms;
		}
	}
}
