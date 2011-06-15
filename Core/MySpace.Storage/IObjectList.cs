using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage
{
	/// <summary>
	/// List of objects, with associated information for the list.
	/// </summary>
	/// <typeparam name="T">The type of the objects listed.</typeparam>
	/// <typeparam name="THeader">The type of the information associated
	/// with the list itself.</typeparam>
	public interface IObjectList<T, THeader> : IEnumerable<T>, IDisposable
	{
		/// <summary>
		/// Gets the information associated with the list.
		/// </summary>
		THeader Header { get; }

		/// <summary>
		/// Adds a new object to the list.
		/// </summary>
		/// <param name="instance">The object to add.</param>
		void Add(T instance);

		/// <summary>
		/// Adds new objects to the list.
		/// </summary>
		/// <param name="instances">The objects to add.</param>
		void AddRange(IEnumerable<T> instances);

		/// <summary>
		/// Removes all objects from the list.
		/// </summary>
		void Clear();
	}
}
