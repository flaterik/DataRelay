using System;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>Encapsulates a handle to a task monitored by the <see cref="TaskMonitor"/> class.</para>
	/// </summary>
	public interface ITaskHandle
	{
		/// <summary>
		///	<para>Tries the set the task to a complete state.</para>
		/// </summary>
		/// <returns>
		///	<para><see langword="true"/> if the task was set to complete
		///	successfully. <see langword="false"/> if the task timed out before
		///	this method was called; in this case the timeout handler you
		///	specified to <see cref="TaskMonitor.RegisterMonitor"/> will or
		///	has already been invoked.</para>
		/// </returns>
		bool TrySetComplete();
	}
}
