using System;
using System.Diagnostics;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>An implementation of <see cref="PerfCounter"/> that 
	///		maintains a counter of type <see cref="PerformanceCounterType.SampleFraction"/>.</para>
	/// </summary>
	public sealed class RatioCounter : PerfCounter
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RatioCounter"/> class.</para>
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
		public RatioCounter(string name, string description)
			: base(name, description, PerformanceCounterType.SampleFraction, PerformanceCounterType.SampleBase, false)
		{
		}

		/// <summary>
		///	<para>Records an that an attempt to perform an operation failed or succeeded.</para>
		/// </summary>
		/// <param name="success">
		///	<para><see langword="ture"/> if the attempted operation should be recorded as
		///	successful; <see langword="false"/> otherwise.</para>
		/// </param>
		public void RecordAttempt(bool success)
		{
			BaseIncrement(success ? 1L : 0, 1L);
		}

		/// <summary>
		///	<para>Records that <paramref name="numSuccessful"/> attempts were made
		///	out of <paramref name="numTotal"/> total attempts.</para>
		/// </summary>
		/// <param name="numSuccessful">The number of successful attempts.</param>
		/// <param name="numTotal">The total number of attempts.</param>
		public void RecordAttempts(long numSuccessful, long numTotal)
		{
			BaseIncrement(numSuccessful, numTotal);
		}
	}
}
