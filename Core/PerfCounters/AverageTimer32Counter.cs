using System;
using System.Diagnostics;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>An implementation of <see cref="PerfCounter"/> that 
	///		maintains a counter of type <see cref="PerformanceCounterType.AverageTimer32"/>.</para>
	/// </summary>
	public sealed class AverageTimer32Counter : PerfCounter
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="AverageTimer32Counter"/> class.</para>
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
		public AverageTimer32Counter(string name, string description)
			: base(name, description, PerformanceCounterType.AverageTimer32, PerformanceCounterType.AverageBase, false)
		{
		}

		/// <summary>
		/// 	<para>Increments this counter by the specifed amount.</para>
		/// </summary>
		/// <param name="ticks">
		/// 	<para>The number of ticks that it took to process the number of items	
		///		indicated by <paramref name="itemCount"/>.</para>
		/// </param>
		/// <param name="itemCount">
		/// 	<para>The number of items processed during the time span indicated
		///		by <paramref name="ticks"/>.</para>
		/// </param>
		public void Increment(long ticks, long itemCount)
		{
			base.BaseIncrement(ticks, itemCount);
		}
	}
}