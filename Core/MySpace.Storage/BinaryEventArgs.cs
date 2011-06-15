using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;

namespace MySpace.Storage
{
	/// <summary>
	/// Holds information about an expired entry. Used by handlers of
	/// <see cref="IBinaryStorage.Dropped"/>.
	/// </summary>	
	public class BinaryEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the key space of the entry.
		/// </summary>
		/// <value>The <see cref="DataBuffer"/> corresponding to the entry's
		/// key space.</value>
		public DataBuffer KeySpace { get; private set; }

		/// <summary>
		/// Gets the key of the entry.
		/// </summary>
		/// <value>The <see cref="StorageKey"/> corresponding to the entry's
		/// key.</value>
		public StorageKey Key { get; private set; }

		/// <summary>
		/// Gets the dat of the entry.
		/// </summary>
		/// <value>A <see cref="DataBuffer"/> containing the entry data.</value>
		public DataBuffer Data { get; private set; }

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="BinaryEventArgs"/> class.</para>
		/// </summary>
		/// <param name="keySpace">
		/// 	<para>The <see cref="DataBuffer"/> of the key space.</para>
		/// </param>
		/// <param name="key">
		/// 	<para>The <see cref="StorageKey"/> of the key.</para>
		/// </param>
		/// <param name="data">
		///		<para>The <see cref="DataBuffer"/> containing the entry data.</para>
		/// </param>
		public BinaryEventArgs(DataBuffer keySpace, StorageKey key, DataBuffer data)
		{
			KeySpace = keySpace;
			Key = key;
			Data = data;
		}
	}
}
