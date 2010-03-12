using System;
using System.Collections.Generic;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Represents a component that replicates <see cref="RelayMessage"/>.
	/// </summary>
	public interface IReplicationComponent : IRelayComponent
	{
		/// <summary>
		/// Replicates a message if the message can be replicated.
		/// </summary>
		/// <param name="message">The message to replicate.</param>
		/// <returns>Returns true if replicated.</returns>
		bool Replicate(RelayMessage message);
		
		/// <summary>
		///  Replicates a list of messages.
		/// </summary>
		/// <param name="messages">The list of messages.</param>
		/// <returns>The number of messages replicated.</returns>
		int Replicate(IList<RelayMessage> messages);
	}
	
}
