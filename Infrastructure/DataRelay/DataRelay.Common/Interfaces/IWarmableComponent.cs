using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Implement on an <see cref="IRelayComponent"/> to be called
	/// prior to the handling of traffic and after initialization.
	/// </summary>
	public interface IWarmableComponent
	{
		/// <summary>
		/// Executes code required to warm the server up prior to handling traffic.
		/// </summary>
		void WarmUp();
	}
}
