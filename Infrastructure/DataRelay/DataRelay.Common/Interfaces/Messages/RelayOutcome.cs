using System;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Enumerations used to indicate the outcome of sending a <see cref="RelayMessage"/>.
	/// </summary>
	public enum RelayOutcome : byte
	{
		/// <summary>
		/// The <see cref="RelayMessage"/> has not been sent over the network.
		/// </summary>
		NotSent = 0,

		/// <summary>
		/// The <see cref="RelayMessage"/> reached its destination and completed its operation
		/// successfully.
		/// </summary>
		Success = 1,

		/// <summary>
		/// The <see cref="RelayMessage"/> successfully reached its destination.
		/// </summary>
		Received = 2,
		
		/// <summary>
		/// An error occured on the client or the server.
		/// </summary>
		Error = 3,

		/// <summary>
		/// The functionality that the <see cref="RelayMessage"/> required wasn't supported
		/// or the server doesn't support outcomes yet.
		/// </summary>
		NotSupported = 4,

		/// <summary>
		/// Relay conditional get, stating that the client data is still fresh.
		/// </summary>
		StillFresh = 5,

		/// <summary>
		/// The result from a canonical data store that the piece of data requested doesn't exist
		/// anywhere.  Not to be confused with a "Cache Miss."
		/// </summary>
		Nonexistent = 6,

		/// <summary>
		/// The <see cref="RelayMessage"/> has been queued to be sent across the network.
		/// </summary>
		Queued = 7,

		/// <summary>
		/// The server didn't respond fast enough so we timed out.
		/// </summary>
		Timeout = 8,

		/// <summary>
		/// Requested item has been persisted to recoverable storage.
		/// </summary>
		Persisted = 9, 

		/// <summary>
		/// The operation couldn't be completed due to a business rule or other non-error constraint.
		/// </summary>
		Denied = 10

	}
}
