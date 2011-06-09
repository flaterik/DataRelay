using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MySpace.Common.Storage;

namespace BerkeleyDbWrapper
{
	///<summary>
	///Holds the lengths of key and value in a Berkeley Db entry.
	///</summary>
	public struct Lengths
	{
		/// <summary>
		/// 	<para>Constant the represents an entry not found.</para>
		/// </summary>		
		public const int NotFound = -1;
		/// <summary>
		/// 	<para>Constant the represents an entry deleted.</para>
		/// </summary>		
		public const int Deleted = -2;
		/// <summary>
		/// 	<para>Constant the represents an entry created but not populated.</para>
		/// </summary>		
		public const int KeyExists = -3;
		///<summary>
		///Gets the key length.
		///</summary>
		///<value>
		///The <see cref="Int32" /> length of the key.
		///</value>
		public int KeyLength { get; private set; }
		///<summary>
		///Gets the value length.
		///</summary>
		///<value>
		///The <see cref="Int32" /> value of the key.
		///</value>
		public int ValueLength { get; private set; }
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Lengths"/> structure.</para>
		/// </summary>
		/// <param name="keyLength">
		/// 	<para>The <see cref="Int32" /> length of the key.</para>
		/// </param>
		/// <param name="valueLength">
		/// 	<para>The <see cref="Int32" /> value of the key.</para>
		/// </param>
		public Lengths(int keyLength, int valueLength)
			: this()
		{
			KeyLength = keyLength;
			ValueLength = valueLength;
		}
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Lengths"/> structure.</para>
		/// </summary>
		/// <param name="key">
		/// 	<para>The <see cref="DataBuffer" /> key.</para>
		/// </param>
		/// <param name="value">
		/// 	<para>The <see cref="DataBuffer" /> key.</para>
		/// </param>
		public Lengths(DataBuffer key, DataBuffer value) :
			this(key.ByteLength, value.ByteLength)
		{
		}
	}
}
