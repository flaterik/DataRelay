using System;
using System.Linq;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// Represents the manner in which to load the <see cref="IVariable"/> or <see cref="IValueShortcut" />.
	/// </summary>
	[Flags]
	public enum LoadOptions
	{
		/// <summary>
		///	<para>Value Types - Loads the value onto the evaluation stack.</para>
		///	<para>- or -</para>
		///	<para>Reference Types - Loads a reference onto the evaluation stack.</para>
		/// </summary>
		Default = 0,
		/// <summary>
		/// If enabled and the type of object to load is a value type, and address to its value will be loaded onto the evaluation stack.
		/// </summary>
		ValueAsAddress = 1 << 1,
		/// <summary>
		/// If enabled and the type of object to load is a reference type then an address to the reference will be loaded onto the evaluation stack.
		/// </summary>
		ReferenceAsAddress = 1 << 2,
		/// <summary>
		/// If enabled value types will be boxed.
		/// </summary>
		BoxValues = 1 << 3,
		/// <summary>
		/// Both <see cref="ValueAsAddress"/> and <see cref="ReferenceAsAddress"/> flags are set.
		/// </summary>
		AnyAsAddress = ValueAsAddress | ReferenceAsAddress
	}
}
