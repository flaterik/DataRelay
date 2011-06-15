using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// A policy store that provides information about types of cached objects.
	/// </summary>
	public interface ITypePolicy : IDisposable
	{
		/// <summary>
		/// Initializes the policy store.
		/// </summary>
		/// <param name="config">The configuration of the policy store.</param>
		void Initialize(object config);

		/// <summary>
		/// Gets the description of a type.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of cached objects.</param>
		/// <returns>A <see cref="TypeDescription"/> holding information
		/// about <paramref name="type"/>.</returns>
		TypeDescription GetDescription(Type type);

		/// <summary>
		/// Gets the description of a type.
		/// </summary>
		/// <param name="keySpace">The <see cref="DataBuffer"/> key space
		/// of the cached objects.</param>
		/// <returns>A <see cref="TypeDescription"/> holding information
		/// about the cached type.</returns>
		TypeDescription GetDescription(DataBuffer keySpace);
	}
}
