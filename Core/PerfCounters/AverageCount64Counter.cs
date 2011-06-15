using System;
using System.Diagnostics;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>An implementation of <see cref="PerfCounter"/> that 
	///		maintains a counter of type <see cref="PerformanceCounterType.AverageCount64"/>.</para>
	/// </summary>
	public sealed class AverageCount64Counter : PerfCounter
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="AverageCount64Counter"/> class.</para>
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
		public AverageCount64Counter(string name, string description)
			: base(name, description, PerformanceCounterType.AverageCount64, PerformanceCounterType.AverageBase, false)
		{
		}

		/// <summary>
		/// 	<para>Records that an item with <paramref name="count"/> was processed.</para>
		/// </summary>
		/// <param name="count">
		/// 	<para>The count associated with each item processed. e.g. Bytes / Item.</para>
		/// </param>
		public void RecordCount(long count)
		{
			base.BaseIncrement(count, 1L);
		}

		/// <summary>
		///	<para>Records that a number of items with an aggregate count
		///	of <paramref name="totalCount"/> were processed.</para>
		/// </summary>
		/// <param name="totalCount">The sum of all item counts to record.</param>
		/// <param name="numItems">The number of items.</param>
		public void RecordCounts(long totalCount, long numItems)
		{
			base.BaseIncrement(totalCount, numItems);
		}
	}
}
