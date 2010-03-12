using System;

namespace MySpace.DataRelay.RelayComponent.Forwarding 
{
	/// <summary>
	/// When relay messages operate on a type configure with
	///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"/> with 
	///		SyncInMessages=true and 
	///		ThrowOnSyncFailure=true
	///	failed executions will throw this exception	
	/// </summary>
	public class SyncRelayOperationException : ApplicationException 
	{
		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="message"></param>
		public SyncRelayOperationException(String message) : base(message) 
		{
		}
	}
}
