using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage
{
	/// <summary>
	/// Describes how the <see cref="IObjectStorage"/> behaves as available space
	/// runs out.
	/// </summary>
	public enum OutOfSpacePolicy
	{
		/// <summary>
		/// Throws exceptions when storage runs out of space.
		/// </summary>
		Exception = 0,
		/// <summary>
		/// Drops entries when storage runs out of space.
		/// </summary>
		DropEntries = 1
	}
}
