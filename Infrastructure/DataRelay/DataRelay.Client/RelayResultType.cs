using System;
using MySpace.Common;

namespace MySpace.DataRelay.Client
{
	/// <summary>
	/// Enumerations used in <see cref="RelayResult{T}"/> for the status of finding and loading a given 
	/// <see cref="ICacheParameter"/> object or list of <see cref="ICacheParameter"/> objects from the transport.
	/// </summary>
	public enum RelayResultType
	{
		/// <summary>
		/// <see cref="RelayResultType.Success"/> is returned if the <see cref="ICacheParameter"/> object was found with no errors or a <see cref="HandledVersionException"/> is encountered.
		/// </summary>
		Success,
		/// <summary>
		/// <see cref="RelayResultType.NotFound"/> is returned if the <see cref="ICacheParameter"/> object is not found.
		/// </summary>
		NotFound,
		/// <summary>
		/// <see cref="RelayResultType.Error"/> is returned if an <see cref="Exception"/> other than <see cref="HandledVersionException"/> is encountered.
		/// </summary>
		Error
	}
}