using System;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Represents a type of error that occurred in the relay system.
	/// </summary>
	public enum RelayErrorType
	{
		/// <summary>
		/// No error was encountered.
		/// </summary>
		None,
		/// <summary>
		/// A connection could not be opened against a relay node within a reasonable amount of time.
		/// Possible reasons are: service was down, DNS problem, connection refused by node.
		/// </summary>
		NodeUnreachable,
		/// <summary>
		/// The destination node was in the danger zone.
		/// </summary>
		NodeInDanagerZone,
		/// <summary>
		/// No nodes are available. This could happen if all nodes are in the danger zone,
		/// or no nodes are mapped in the config.
		/// </summary>
		NoNodesAvailable,
		/// <summary>
		/// The destination node did not respond within the allowed time.
		/// </summary>
		TimedOut,
		/// <summary>
		/// An error occurred that was specific to the
		/// <see cref="IRelayComponent"/> that handled the message.
		/// </summary>
		ComponentSpecific,
		/// <summary>
		/// An unknown error was encountered.
		/// </summary>
		Unknown
	}
}
