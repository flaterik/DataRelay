using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>Installs and uninstalls performance counters.</para>
	/// </summary>
	internal static class PerfCounterInstaller
	{
		/// <summary>
		/// 	<para>Installs the specified performance counters onto the system.</para>
		/// </summary>
		/// <param name="counters">
		/// 	<para>The <see cref="PerfCounter"/> instances to install;
		///		ignored if <see langword="null"/>, and null elements are ignored.</para>
		/// </param>
		public static void Install(params PerfCounter[] counters)
		{
			Install((IEnumerable<PerfCounter>)counters);
		}

		/// <summary>
		/// 	<para>Installs the specified performance counters onto the system.</para>
		/// </summary>
		/// <param name="counters">
		/// 	<para>The <see cref="PerfCounter"/> instances to install;
		///		ignored if <see langword="null"/>, and null elements are ignored.</para>
		/// </param>
		public static void Install(IEnumerable<PerfCounter> counters)
		{
			if (counters == null) return;

			// Organize counters into categories
			Dictionary<string, CounterCreationDataCollection> table = new Dictionary<string, CounterCreationDataCollection>();

			foreach (PerfCounter counter in counters)
			{
				if (counter == null) continue;

				CounterCreationDataCollection dataList;

				if (!table.TryGetValue(counter.CategoryName, out dataList))
				{
					dataList = new CounterCreationDataCollection();
					table.Add(counter.CategoryName, dataList);
				}

				foreach (CounterCreationData data in counter.GetCounterCreationData())
				{
					dataList.Add(data);
				}
			}

			// Install categories
			foreach (KeyValuePair<string, CounterCreationDataCollection> category in table)
			{
				PerformanceCounterCategory.Create(category.Key, String.Empty, PerformanceCounterCategoryType.MultiInstance, category.Value);
			}
		}

		/// <summary>
		/// 	<para>Uninstalls the specified categories of counters from the system.</para>
		/// </summary>
		/// <param name="categories">
		/// 	<para>The categories of counters to install;
		///		ignored if <see langword="null"/>, and null elements are ignored.</para>
		/// </param>
		public static void Uninstall(params string[] categories)
		{
			if (categories == null) return;

			foreach (string category in categories)
			{
				if (category == null) continue;

				if (PerformanceCounterCategory.Exists(category))
				{
					PerformanceCounterCategory.Delete(category);
				}
			}
		}
	}
}
