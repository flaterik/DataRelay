using System;

namespace MySpace.DataRelay.Client 
{
	/// <summary>
	/// When relay messages operate on a type configure with
	///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"/> with 
	///		LocalCacheTTLSeconds > 0 but with corresponding
	///		<see cref="MySpace.DataRelay.Common.Schemas.RelayNodeGroupDefinition"/> with 
	///		LegacySerialization=true
	///	failed executions will throw this exception	
	/// </summary>
	public class CacheConfigurationException : ApplicationException 
	{
		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="typeId"></param>
		public CacheConfigurationException(short typeId) :
			base(string.Format("Message of type id {0} is using legacy serialization", typeId)) 
		{
		}
	}
}
