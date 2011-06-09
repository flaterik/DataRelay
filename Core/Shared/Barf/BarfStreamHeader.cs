using System;
using System.IO;
using System.Linq;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	public sealed class BarfStreamHeader
	{
		internal static BarfStreamHeader ReadFrom(IPrimitiveReader reader)
		{
			var result = new BarfStreamHeader();
			var value = reader.ReadVarInt32();
			if (value < 0) reader.RaiseInvalidDataException();
			result.FrameworkVersion = value;

			result.Flags = (HeaderFlags)reader.ReadByte();

			return result;
		}

		internal static long BeginWrite(IPrimitiveWriter writer, int frameworkVersion)
		{
			writer.WriteVarInt32(frameworkVersion);
			writer.Write((byte)0); // reserve
			return writer.BaseStream.Position;
		}

		internal static void EndWrite(IPrimitiveWriter writer, long originalPosition, HeaderFlags flags)
		{
			var currentPosition = writer.BaseStream.Position;
			writer.BaseStream.Seek(originalPosition - 1, SeekOrigin.Begin);
			writer.Write((byte)flags);
			writer.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
		}

		internal int FrameworkVersion { get; private set; }
		internal HeaderFlags Flags { get; private set; }
		internal int Length { get; private set; }
	}
}
