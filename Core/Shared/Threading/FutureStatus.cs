using System;
using System.Linq;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates the state of the future.
	/// </summary>
	public enum FutureStatus
	{
		/// <summary>
		/// The future is incomplete.
		/// </summary>
		Incomplete,
		/// <summary>
		/// The future completed successfully with a value.
		/// </summary>
		Success,
		/// <summary>
		/// The future completed un-successfully with an error.
		/// </summary>
		Failure,
		/// <summary>
		/// The future was canceled by a consumer.
		/// </summary>
		Canceled,
	}
}
