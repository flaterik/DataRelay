using System;
using System.Diagnostics;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>An implementation of <see cref="PerfCounter"/> that 
	///		maintains a counter of type <see cref="PerformanceCounterType.RateOfCountsPerSecond32"/>.</para>
	/// </summary>
	public sealed class RateOfCountsPerSecond32Counter : PerfCounter
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RateOfCountsPerSecond32Counter"/> class.</para>
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
		public RateOfCountsPerSecond32Counter(string name, string description)
			: base(name, description, PerformanceCounterType.RateOfCountsPerSecond32, null, true)
		{
		}

		/// <summary>
		/// 	<para>Increments this counter by the one.</para>
		/// </summary>
		public void Increment()
		{
			base.BaseIncrement(1L, 0L);
		}

		/// <summary>
		/// 	<para>Increments this counter by the specifed amount.</para>
		/// </summary>
		/// <param name="incrementBy">
		/// 	<para>The amount by which to increment the specified instances
		///		of this counter.</para>
		/// </param>
		public void Increment(long incrementBy)
		{
			base.BaseIncrement(incrementBy, 0L);
		}
	}
}