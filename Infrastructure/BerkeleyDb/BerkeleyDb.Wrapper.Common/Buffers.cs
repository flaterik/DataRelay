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
	///Holds the key and value buffers of a Berkeley Db entry.
	///</summary>
	public struct Buffers
	{
		///<summary>
		///Gets the key buffer.
		///</summary>
		///<value>
		///The key <see cref="Byte" /> array.
		///</value>
		public byte[] KeyBuffer { get; private set; }
		///<summary>
		///Gets the value buffer.
		///</summary>
		///<value>
		///The value <see cref="Byte" /> array.
		///</value>
		public byte[] ValueBuffer { get; private set; }
		///<summary>
		///Gets the return code associated with the operation.
		///</summary>
		///<value>
		///The <see cref="Int32" /> return code.
		///</value>
		public int ReturnCode { get; private set; }
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Buffers"/> structure.</para>
		/// </summary>
		/// <param name="keyBuffer">
		/// 	<para>The key <see cref="Byte" /> array.</para>
		/// </param>
		/// <param name="valueBuffer">
		/// 	<para>The value <see cref="Byte" /> array.</para>
		/// </param>
		/// <param name="returnCode">
		/// 	<para>The <see cref="Int32" /> return code associated with the
		///		operation.</para>
		/// </param>
		public Buffers(byte[] keyBuffer, byte[] valueBuffer, int returnCode)
			: this()
		{
			KeyBuffer = keyBuffer;
			ValueBuffer = valueBuffer;
			ReturnCode = returnCode;
		}
	}


}
