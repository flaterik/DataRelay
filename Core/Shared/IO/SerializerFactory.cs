using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MySpace.Common.CompactSerialization.IO;

namespace MySpace.Common.IO
{
	/// <summary>
	/// Provides readers and writers for custom serialization framework.
	/// </summary>
	public class SerializerFactory
	{
		public static IPrimitiveReader GetReader(Stream stream)
		{
			//BinaryReader br = new BinaryReader(stream);
			CompactBinaryReader cbr = new CompactBinaryReader(stream);
			return cbr;
		}

		public static IPrimitiveWriter GetWriter(Stream stream)
		{
			BinaryWriter bw = new BinaryWriter(stream);
			CompactBinaryWriter cbw = new CompactBinaryWriter(bw);
			return cbw;
		}
	}
}
