using System;
using System.IO;
using System.Linq;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	public class BarfObjectHeader
	{
		internal static BarfObjectHeader ReadFrom(IPrimitiveReader reader)
		{
			var value = reader.ReadVarInt32();
			if (value == -1)
			{
				return NullBarfObjectHeader.Instance;
			}

			if (value < 0) reader.RaiseInvalidDataException();
			var result = new BarfObjectHeader { Version = value };

			value = reader.ReadVarInt32();
			if (value < 0) reader.RaiseInvalidDataException();
			result.MinVersion = value;

			value = reader.ReadInt32();
			if (value < 0) reader.RaiseInvalidDataException();
			result.Length = value;

			result.StartPosition = reader.BaseStream.Position;

			return result;
		}

		internal static long BeginWrite(IPrimitiveWriter writer, int version, int minVersion)
		{
			writer.WriteVarInt32(version);
			writer.WriteVarInt32(minVersion);
			writer.Write(-1); // reserve
			return writer.BaseStream.Position;
		}

		internal static void EndWrite(IPrimitiveWriter writer, long originalPosition)
		{
			long currentPosition = writer.BaseStream.Position;
			writer.BaseStream.Seek(originalPosition - sizeof(int), SeekOrigin.Begin);
			writer.Write((int)(currentPosition - originalPosition));
			writer.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
		}

		internal static void WriteNull(IPrimitiveWriter writer)
		{
			writer.WriteVarInt32(-1);
		}

		private BarfObjectHeader() { }

		public virtual bool IsNull { get; protected set; }
		public virtual int Version { get; protected set; }
		public virtual int MinVersion { get; protected set; }
		public virtual int Length { get; protected set; }
		public virtual long StartPosition { get; protected set; }
		public virtual long EndPosition
		{
			get { return StartPosition + Length; }
		}

		private sealed class NullBarfObjectHeader : BarfObjectHeader
		{
			public static readonly NullBarfObjectHeader Instance = new NullBarfObjectHeader();

			public override bool IsNull
			{
				get { return true; }
				protected set { throw new InvalidOperationException(); }
			}

			public override int Length
			{
				get { throw GetInvalidOperationException("Length"); }
				protected set { throw new InvalidOperationException(); }
			}

			public override int MinVersion
			{
				get { throw GetInvalidOperationException("MinVersion"); }
				protected set { throw new InvalidOperationException(); }
			}

			public override int Version
			{
				get { throw GetInvalidOperationException("Version"); }
				protected set { throw new InvalidOperationException(); }
			}

			public override long StartPosition
			{
				get { throw GetInvalidOperationException("StartPosition"); }
				protected set { throw new InvalidOperationException(); }
			}

			public override long EndPosition
			{
				get { throw GetInvalidOperationException("EndPosition"); }
			}

			private Exception GetInvalidOperationException(string propertyName)
			{
				string typeName = GetType().BaseType.Name;
				string message = string.Format("{0}.{1} is inaccessible when {0}.IsNull is true.", typeName, propertyName);
				return new InvalidOperationException(message);
			}
		}
	}
}
