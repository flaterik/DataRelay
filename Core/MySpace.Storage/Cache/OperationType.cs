using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// The type of <see cref="LocalCache"/> operation being performed.
	/// </summary>
	public enum OperationType
	{
		/// <summary>
		/// Save being performed.
		/// </summary>
		Save = 0,

		/// <summary>
		/// Deletion being performed.
		/// </summary>
		Delete = 1,
	}
}
