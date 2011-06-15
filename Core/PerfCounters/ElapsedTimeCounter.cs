using System;
using System.Diagnostics;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>An implementation of <see cref="PerfCounter"/> that 
	///		maintains a counter of type <see cref="PerformanceCounterType.ElapsedTime"/>.</para>
	/// </summary>
	public class ElapsedTimeCounter : PerfCounter
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="NumberOfItems32Counter"/> class.</para>
		/// </summary>
		/// <param name="name">
		/// 	<para>The name of this counter.</para>
		/// </param>
		/// <param name="description">
		/// 	<para>The optional description of this counter.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="name"/> is <see langword="null"/>.</para>
		/// </exception>
		public ElapsedTimeCounter(string name, string description)
			: base(name, description, PerformanceCounterType.ElapsedTime, null, true)
		{
		}

		/// <summary>
		/// Gets the current value of this performance counter.
		/// </summary>
		/// <returns>
		/// A <see cref="Single"/> value providing the value of the
		/// specified instance; always 0 if the couter has not been installed.
		/// </returns>
		public override float GetValue()
		{
			var counter = InnerCounter;
			if (counter == null) return 0f;

			counter.NextValue(); // necessary to clear old sample value
			return counter.NextValue();
		}

		/// <summary>
		/// Sets the counter's start time to the current time.
		/// </summary>
		protected override void OnInitialize()
		{
			BaseSetRawValue(Stopwatch.GetTimestamp(), null);
		}

		/// <summary>
		/// 	<para>Resets the elapsed time value to zero.</para>
		/// </summary>
		public void Reset()
		{
			BaseSetRawValue(Stopwatch.GetTimestamp(), null);
		}
	}
}
