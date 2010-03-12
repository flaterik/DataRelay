using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Enumerations used to describe the health status of a data relay server.
	/// </summary>
	public enum ServerStatus
	{
		/// <summary>
		/// Server with a node address that is invalid or cannot be resolved.
		/// </summary>
		unresolvedServer,
		/// <summary>
		/// Server that has server down errors surpass the specified group danger
		/// zone thresholds.
		/// </summary>
		dangerousServer,
		/// <summary>
		/// Server that is not active as specified via configuration.
		/// </summary>
		inactiveServer,
		/// <summary>
		/// Server that is selected in the cluster.
		/// </summary>
		chosenServer,
		/// <summary>
		/// Server that is running smoothly.
		/// </summary>
		happyServer
	}
}
