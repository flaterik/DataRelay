using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySpace.Common.Barf;

namespace MySpace.Common.IO
{
	public static class PrimitiveExtensions
	{
		const int _guidSize = 16;

		public static void WriteUri(this IPrimitiveWriter writer, Uri value)
		{
			ArgumentAssert.IsNotNull(writer, "writer");

			if (value == null)
			{
				writer.Write((string)null);
			}
			else
			{
				writer.Write(value.ToString());
			}
		}

		public static Uri ReadUri(this IPrimitiveReader reader)
		{
			ArgumentAssert.IsNotNull(reader, "reader");

			var stringValue = reader.ReadString();

			if (stringValue == null) return null;

			return new Uri(stringValue);
		}

		public static Uri FillUri(this FillArgs args)
		{
			ArgumentAssert.IsNotNull(args, "args");

			if (args.NextIsNull) return null;

			var b = new UriBuilder();

			b.Scheme = args.NextBoolean ? "http" : "https";
			b.Host = Guid.NewGuid().ToString("N");
			b.Port = args.NextByte + 20;

			return b.Uri;
		}

		public static void AssertAreEqual(this AssertArgs args, Uri expected, Uri actual)
		{
			if (expected == null)
			{
				Assert.AreEqual(null, actual);
			}
			else
			{
				Assert.AreNotEqual(null, actual);
				Assert.AreEqual(expected.ToString(), actual.ToString());
			}
		}

		public static void RaiseInvalidDataException(this IPrimitiveReader reader)
		{
			throw new InvalidDataException(string.Format(
				"Invalid data encountered while de-serializing. Length=\"{0}\", Position=\"{1}\"",
				reader.BaseStream.Length,
				reader.BaseStream.Position));
		}

		public static void Write(this IPrimitiveWriter writer, Guid value)
		{
			writer.BaseStream.Write(value.ToByteArray(), 0, _guidSize);
		}

		public static BitArray ReadBitArray(this IPrimitiveReader reader)
		{
			var count = reader.ReadVarInt32();
			if (count == -1)
			{
				return null;
			}
			if (count < 0)
			{
				reader.RaiseInvalidDataException();
			}
			var buffer = SafeMemoryAllocator.CreateArray<byte>(RoundToNearest8(count));
			reader.BaseStream.Read(buffer, 0, buffer.Length);
			var result = new BitArray(buffer) { Length = count };
			return result;
		}

		public static void WriteBitArray(this IPrimitiveWriter writer, BitArray value)
		{
			if (value == null)
			{
				writer.WriteVarInt32(-1);
			}
			else
			{
				writer.WriteVarInt32(value.Count);
				var buffer = new byte[RoundToNearest8(value.Count)];
				value.CopyTo(buffer, 0);
				writer.BaseStream.Write(buffer, 0, buffer.Length);
			}
		}

		public static BitArray FillBitArray(this FillArgs args)
		{
			if (args.NextIsNull) return null;
			var result = new BitArray(args.NextCollectionSize);
			for (int i = 0; i < result.Count; ++i)
			{
				result.Set(i, args.NextBoolean);
			}
			return result;
		}

		public static void AssertAreEqual(this AssertArgs args, BitArray expected, BitArray actual)
		{
			CollectionAssert.AreEqual(expected, actual);
		}

		private static int RoundToNearest8(int value)
		{
			return (value + 7) >> 3;
		}

		public static Guid ReadGuid(this IPrimitiveReader reader)
		{
			return new Guid(ReadBytes(reader, _guidSize));
		}

		public static Guid FillGuid(this FillArgs args)
		{
			return Guid.NewGuid();
		}

		public static void AssertAreEqual(this AssertArgs args, Guid expected, Guid actual)
		{
			Assert.AreEqual(expected, actual);
		}

		public static void WriteIPEndPoint(this IPrimitiveWriter writer, IPEndPoint value)
		{
			if (value == null)
			{
				writer.Write(false);
			}
			else
			{
				writer.Write(true);
				WriteIPAddress(writer, value.Address);
				writer.WriteVarInt32(value.Port);
			}
		}

		public static IPEndPoint ReadIPEndPoint(this IPrimitiveReader reader)
		{
			if (reader.ReadBoolean())
			{
				var address = reader.ReadIPAddress();
				var port = reader.ReadVarInt32();
				return new IPEndPoint(address, port);
			}
			return null;
		}

		public static IPEndPoint FillIPEndPoint(this FillArgs args)
		{
			var address = args.FillIPAddress();
			if (address == null) return null;
			return new IPEndPoint(address, args.NextByte + 20);
		}

		public static void AssertAreEqual(this AssertArgs args, IPEndPoint expected, IPEndPoint actual)
		{
			if (expected == null)
			{
				Assert.IsNull(actual);
			}
			else
			{
				Assert.IsNotNull(actual);
				args.AssertAreEqual(expected.Address, actual.Address);
				Assert.AreEqual(expected.Port, actual.Port);
			}
		}

		public static void WriteIPAddress(this IPrimitiveWriter writer, IPAddress value)
		{
			if (value == null)
			{
				writer.WriteVarInt32(-1);
			}
			else
			{
				var buffer = value.GetAddressBytes();
				writer.WriteVarInt32(buffer.Length);
				writer.BaseStream.Write(buffer, 0, buffer.Length);
			}
		}

		[Obsolete("Use WriteIPAddress")]
		public static void Write(this IPrimitiveWriter writer, IPAddress value)
		{
			WriteIPAddress(writer, value);
		}

		public static IPAddress ReadIPAddress(this IPrimitiveReader reader)
		{
			int length = reader.ReadVarInt32();
			if (length >= 0)
			{
				return new IPAddress(ReadBytes(reader, length));
			}
			if (length == -1)
			{
				return null;
			}
			throw new InvalidDataException();
		}

		public static IPAddress FillIPAddress(this FillArgs args)
		{
			if (args.NextIsNull) return null;
			var bytes = new byte[4];
			args.Random.NextBytes(bytes);
			return new IPAddress(bytes);
		}

		public static void AssertAreEqual(this AssertArgs args, IPAddress expected, IPAddress actual)
		{
			if (expected == null)
			{
				Assert.IsNull(actual);
			}
			else
			{
				Assert.IsNotNull(actual);
				Assert.AreEqual(expected.AddressFamily, actual.AddressFamily);
				CollectionAssert.AreEqual(expected.GetAddressBytes(), actual.GetAddressBytes());
			}
		}

		public static byte[] ReadByteArray(this IPrimitiveReader reader)
		{
			var count = reader.ReadVarInt32();
			if (count < -1)
			{
				throw new InvalidDataException();
			}
			if (count == -1)
			{
				return null;
			}
			return ReadBytes(reader, count);
		}

		public static void WriteByteArray(this IPrimitiveWriter writer, byte[] value)
		{
			if (value == null)
			{
				writer.WriteVarInt32(-1);
			}
			else
			{
				writer.WriteVarInt32(value.Length);
				writer.BaseStream.Write(value, 0, value.Length);
			}
		}

		public static byte[] FillByteArray(this FillArgs args)
		{
			if (args.NextIsNull)
			{
				return null;
			}
			var result = new byte[args.NextCollectionSize];
			args.Random.NextBytes(result);
			return result;
		}

		public static void AssertAreEqual(this AssertArgs args, byte[] expected, byte[] actual)
		{
			CollectionAssert.AreEqual(expected, actual);
		}

		private static byte[] ReadBytes(IPrimitiveReader reader, int count)
		{
			if (count < 0)
			{
				throw new InvalidDataException();
			}

			var result = SafeMemoryAllocator.CreateArray<byte>(count);
			int remaining = count;
			while (remaining > 0)
			{
				int read = reader.BaseStream.Read(result, count - remaining, remaining);
				if (read <= 0)
				{
					throw new InvalidDataException("Unexpected end of stream");
				}
				remaining -= read;
			}
			return result;
		}
	}
}
