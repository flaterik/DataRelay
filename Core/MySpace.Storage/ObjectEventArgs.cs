using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;

namespace MySpace.Storage
{
	/// <summary>
	/// Holds information about an expired entry. Used by handlers of
	/// <see cref="IObjectStorage.Dropped"/>.
	/// </summary>
	public class ObjectEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the reference to the expired item.
		/// </summary>
		/// <value>Unique to type of expired entry.</value>
		public ObjectReference Reference { get; private set;}

		/// <summary>
		/// Gets whether the item is an <see cref="IObjectList{T, THeader}"/>.
		/// </summary>
		/// <value><see langword="true"/> if the item is a list; otherwise
		/// <see langword="false"/>.</value>
		public bool IsList { get; private set; }

		/// <summary>
		/// Gets the entry object that was expired from storage.
		/// </summary>
		/// <value>This <see cref="Object"/> is no longer in storage.</value>
		public object Instance { get; private set; }

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectEventArgs"/> class.</para>
		/// </summary>
		/// <param name="reference">
		/// 	<para>The reference to the entry.</para>
		/// </param>
		/// <param name="isList">
		/// 	<para>Whether or not the entry is an <see cref="IObjectList{T, THeader}"/>.</para>
		/// </param>
		/// <param name="instance">
		/// 	<para>The entry.</para>
		/// </param>
		public ObjectEventArgs(ObjectReference reference, bool isList, object instance)
		{
			Reference = reference;
			IsList = isList;
			Instance = instance;
		}
	}
}
