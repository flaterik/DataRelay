using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// The type of dependency on a cached object.
	/// </summary>
	public enum DependencyType
	{
		/// <summary>
		/// Dependency is on the state contained in the cached object.
		/// </summary>
		Content = 0,

		/// <summary>
		/// Dependency in on whether or not the object exists in cache.
		/// </summary>
		Existence = 1
	}
}
