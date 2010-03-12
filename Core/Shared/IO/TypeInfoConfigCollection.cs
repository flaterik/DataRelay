using System;
using System.Collections.ObjectModel;

namespace MySpace.Common.IO
{
	/// <summary>
	/// 	<para>A collection of <see cref="TypeInfoConfig"/> keyed by <see cref="TypeInfoConfig.Id"/>.</para>
	/// </summary>
	public class TypeInfoConfigCollection : KeyedCollection<short, TypeInfoConfig>
	{
		/// <summary>
		/// When implemented in a derived class, extracts the key from the specified element.
		/// </summary>
		/// <param name="item">The element from which to extract the key.</param>
		/// <returns>The key for the specified element.</returns>
		protected override short GetKeyForItem(TypeInfoConfig item)
		{
			return item.Id;
		}
	}
}
