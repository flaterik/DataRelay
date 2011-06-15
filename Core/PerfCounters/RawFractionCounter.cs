using System;
using System.Diagnostics;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>An implementation of <see cref="PerfCounter"/> 
	///		maintaining a counter of type <see cref="PerformanceCounterType.RawFraction"/>, which 
	///		keeps a fractional value, such as the percentage of disk space available.</para>
	/// </summary>
	public sealed class RawFractionCounter : PerfCounter
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RawFractionCounter"/> class.</para>
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
		public RawFractionCounter(string name, string description)
			: base(name, description, PerformanceCounterType.RawFraction, PerformanceCounterType.RawBase, false)
		{
		}

		/// <summary>
		///		Initializes this counter by setting its numerator and denominator.
		/// </summary>
		/// <param name="numerator">The initial value of the numerator.  For a counter tracking available resource as a percentage of total, this value
		///		should be initialized to the same value as the numerator.  Fro a counter tracking consumed resource as a percentage of tatal, this 
		///		value should be initialize to zero.</param>
		/// <param name="denominator">The denominator of this counter.  Must be greater than zero.</param>
		public void Initialize(long numerator, long denominator)
		{
			BaseSetRawValue(numerator, denominator);
		}

		/// <summary>
		///		Increments the numerator portion of the fraction.
		/// </summary>
		/// <param name="numeratorDelta">The amount to add to or subtract from the numerator.</param>
		public void IncrementNumerator(long numeratorDelta)
		{
			BaseIncrement(numeratorDelta, null);
			this.GetValue();
		}

		/// <summary>
		/// 	<para>Overriden.  Gets the current value of this performance counter.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="Single"/> value providing the value of the
		/// 	specified instance; always 0 if the couter has not been installed.</para>
		/// </returns>
		public override float GetValue()
		{
			if (IsInitialized && InnerCounter != null)
			{
				return InnerCounter.NextValue();
			}
			else
			{
				return 0;
			}
		}
	}
}
