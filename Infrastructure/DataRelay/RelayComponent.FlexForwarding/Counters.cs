using MySpace.PerfCounters;

namespace MySpace.DataRelay.RelayComponent.FlexForwarding
{
	/// <summary>
	/// The performance counters for the component.
	/// </summary>
	public sealed class Counters : PerfCounterCategory<Counters>
	{
		private Counters(string instanceName)
			: base("MySpace FlexForwarder", instanceName)
		{
		}

		/// <summary>
		/// The messages per second for all flex cache groups that are flowing through the FlexForwarding component.
		/// </summary>
		public readonly RateOfCountsPerSecond32Counter TotalMessagesPerSecond = new RateOfCountsPerSecond32Counter("Msg/sec", "The messages per second for all flex cache groups that are flowing through the FlexForwarding component.");

		/// <summary>
		/// The errors per second for all flex cache groups that are flowing through the FlexForwarding component.
		/// </summary>
		public readonly RateOfCountsPerSecond32Counter TotalErrorsPerSecond = new RateOfCountsPerSecond32Counter("Errors/sec", "The errors per second for all flex cache groups that are flowing through the FlexForwarding component.");

		/// <summary>
		/// The average size of messages that are flowing through the FlexForwarding component.
		/// </summary>
		public readonly AverageCount64Counter AverageMessageSize = new AverageCount64Counter("Avg Message Size", "The average size of messages that are flowing through the FlexForwarding component.");
		
		/// <summary>
		/// The PUT messages per second for all flex cache groups that are flowing through the FlexForwarding component.
		/// </summary>
		public readonly RateOfCountsPerSecond32Counter TotalPutMessagesPerSecond = new RateOfCountsPerSecond32Counter("PUT Msg/sec", "The PUT messages per second for all flex cache groups that are flowing through the FlexForwarding component.");

		/// <summary>
		/// The GET messages per second for all flex cache groups that are flowing through the FlexForwarding component.
		/// </summary>
		public readonly RateOfCountsPerSecond32Counter TotalGetMessagesPerSecond = new RateOfCountsPerSecond32Counter("GET Msg/sec", "The GET messages per second for all flex cache groups that are flowing through the FlexForwarding component.");

		/// <summary>
		/// The hit ratio of all GET messages.
		/// </summary>
		public readonly RatioCounter TotalHitRatio = new RatioCounter("Hit Ratio", "The hit ratio of all GET messages.");

		/// <summary>
		/// The DELETE messages per second for all flex cache groups that are flowing through the FlexForwarding component.
		/// </summary>
		public readonly RateOfCountsPerSecond32Counter TotalDeleteMessagesPerSecond = new RateOfCountsPerSecond32Counter("DELETE Msg/sec", "The DELETE messages per second for all flex cache groups that are flowing through the FlexForwarding component.");

	}
}
