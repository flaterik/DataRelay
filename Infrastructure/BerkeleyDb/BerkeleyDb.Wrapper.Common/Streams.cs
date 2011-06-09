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
	///Holds the key and value streams of a Berkeley Db entry.
	///</summary>
	public struct Streams
	{
		///<summary>
		///Gets the key stream.
		///</summary>
		///<value>
		///The key <see cref="Stream" />.
		///</value>
		public Stream KeyStream { get; private set; }
		///<summary>
		///Gets the value stream.
		///</summary>
		///<value>
		///The value <see cref="Stream" />.
		///</value>
		public Stream ValueStream { get; private set; }
		///<summary>
		///Gets the return code associated with the operation.
		///</summary>
		///<value>
		///The <see cref="Int32" /> return code.
		///</value>
		public int ReturnCode { get; private set; }
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Streams"/> structure.</para>
		/// </summary>
		/// <param name="keyStream">
		/// 	<para>The key <see cref="Stream" />.</para>
		/// </param>
		/// <param name="valueStream">
		/// 	<para>The value <see cref="Stream" />.</para>
		/// </param>
		/// <param name="returnCode">
		/// 	<para>The <see cref="Int32" /> return code associated with the
		///		operation.</para>
		/// </param>
		public Streams(Stream keyStream, Stream valueStream, int returnCode)
			: this()
		{
			KeyStream = keyStream;
			ValueStream = valueStream;
			ReturnCode = returnCode;
		}
	}
}
