using System;
using System.Diagnostics;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>An implementation of <see cref="PerfCounter"/> that 
	///		maintains a counter of type <see cref="PerformanceCounterType.NumberOfItems32"/>.</para>
	/// </summary>
	public sealed class NumberOfItems32Counter : PerfCounter
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
		public NumberOfItems32Counter(string name, string description)
			: base(name, description, PerformanceCounterType.NumberOfItems32, null, false)
		{
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

		/// <summary>
		/// 	<para>Sets the raw value of this counter to the specified number.</para>
		/// </summary>
		/// <param name="rawValue">
		/// 	<para>The raw value to set the specified instances of this counter to.</para>
		/// </param>
		public void SetRawValue(long rawValue)
		{
			base.BaseSetRawValue(rawValue, 0L);
		}
	}
}