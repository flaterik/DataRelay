using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage
{
	/// <summary>
	/// Describes what sort of transaction commit is supported for <see cref="IObjectStorage"/>s
	/// that support transactions.
	/// </summary>
	public enum TransactionCommitType
	{
		/// <summary>
		/// Supports 2 phase commits.
		/// </summary>
		TwoPhase = 0,

		/// <summary>
		/// Supports both 1 and 2 phase commits.
		/// </summary>
		SinglePhaseAndTwoPhase = 1
	}
}
