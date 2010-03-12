using System;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// The runtime information object for the forwarding component. Contains the Html Status.
	/// </summary>
	[Serializable]
	public class ForwarderRuntimeInfo : ComponentRuntimeInfo
	{
		/// <summary>
		/// The Html formatted status information for the forwarder. Contains information about each defined node.
		/// </summary>
		public string HtmlStatus;

		/// <summary>
		/// Create an empty ForwarderRuntimeInfo object.
		/// </summary>
		public ForwarderRuntimeInfo()
			: base(Forwarder.ComponentName)
		{
			
		}

		/// <summary>
		/// Returns the html status of the forwarder.
		/// </summary>
		public override string GetRuntimeInfoAsString()
		{
			return HtmlStatus;
		}
	}
}
