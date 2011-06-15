using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common;
using MySpace.Common.Storage;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// A dependency on a cached object.
	/// </summary>
	public interface IObjectDependency : IVersionSerializable
	{
		/// <summary>
		/// Called when a change is made to the cached object.
		/// </summary>
		/// <param name="changed"><see cref="ObjectReference"/>
		/// that refers to the changed cached object.</param>
		/// <param name="op"><see cref="OperationType"/> that specifies
		/// the type of operation performed.</param>
		/// <returns><see langword="true"/> if the dependency should be retained,
		/// otherwise <see langword="false"/>.</returns>
		bool Notify(ObjectReference changed, OperationType op);
	}
}
