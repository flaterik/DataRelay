using System;
using System.IO;
using System.Runtime;
using MySpace.Common.CompactSerialization.Formatters;
using System.Collections.Generic;
using MySpace.Common.IO;
using System.Reflection;

namespace MySpace.Common.CompactSerialization.IO
{
	/// <summary>
	/// This class encapsulates a <see cref="BinaryReader"/> object. It also provides an extra
	/// Read method for <see cref="System.Object"/> types. 
	/// </summary>
	public class CompactBinaryReader : IDisposable, MySpace.Common.IO.IPrimitiveReader
	{
		public SerializationResponse Response { get { return response; } set { response = value; } }
		private SerializationResponse response = SerializationResponse.Success;

		private byte[] readBuffer;

		private System.IO.BinaryReader reader;
		private Stream stream;

		/// <summary>
		/// Constructs the object over a <see cref="BinaryReader"/> object.
		/// </summary>
		/// <param name="binaryReader"><see cref="BinaryReader"/> object</param>
		public CompactBinaryReader(System.IO.BinaryReader binaryReader)
		{
			reader = binaryReader;
			stream = binaryReader.BaseStream;
		}

		public CompactBinaryReader(Stream readStream)
		{
			//reader = binaryReader;
			stream = readStream;
		}

		public Stream BaseStream
		{
			get
			{
				return stream;
			}
		}

		private readonly Stack<long> regionStack = new Stack<long>();
		private RegionCloser regionCloser;
		private RegionCloser GetRegionCloser()
		{
			if (regionCloser == null) //don't bother with thread saftey here
			{
				regionCloser = new RegionCloser() { Reader = this };
			}
			return regionCloser;
		}

		/// <summary>
		/// Creates a Region that enables older versions to read new streams and keep the correct
		/// stream position.
		/// </summary>
		/// <remarks>
		/// <para>
		/// When older code reads a region from new code that contains new fields at the end of the stream
		/// it will be able to read the first portion which it understands and skip the remaining fields,
		/// thus preserving the stream position at the end of reading.
		/// </para>
		/// <code>
		/// public void Serialize(IPrimitiveWriter writer)
		///	{
		///		using(writer.CreateRegion())
		///		{
		///			writer.Write(FirstName);
		///			writer.Write(LastName);
		///		}
		///	}
		/// </code>
		/// </remarks>
		/// <returns>Returns a <see cref="IDisposable"/> instance that must be disposed at the end of the region.</returns>
		public IDisposable CreateRegion()
		{
			byte headerLength = ReadByte(); //read header length
			Int32 length = ReadInt32();
			if (headerLength > sizeof(Int32)) //skip extra header we don't understand
			{
				reader.BaseStream.Seek(headerLength - sizeof(Int32), SeekOrigin.Current);
			}
			long endPosition = BaseStream.Position + length;
			regionStack.Push(endPosition);

			return GetRegionCloser();
		}

		private void CloseRegion()
		{
			long endPosition = regionStack.Pop();
			if (BaseStream.Position != endPosition)
			{
				BaseStream.Seek(endPosition, SeekOrigin.Begin);
			}
		}

		private class RegionCloser : IDisposable
		{
			public CompactBinaryReader Reader;

			#region IDisposable Members

			public void Dispose()
			{
				Reader.CloseRegion();
			}

			#endregion
		}


		/// <summary> Returns the underlying <see cref="BinaryReader"/> object. </summary>
		public System.IO.BinaryReader BaseReader
		{
			get
			{
				if (reader == null)
				{
					reader = new BinaryReader(stream);
				}
				return reader;
			}

		}

		/// <summary>
		/// Close the underlying <see cref="BinaryWriter"/>.
		/// </summary>
		public void Dispose()
		{
			if (reader != null) reader.Close();
		}

		#region Basic Routines

		/// <summary>
		/// Reads an object of type <see cref="object"/> from the current stream 
		/// and advances the stream position. 
		/// </summary>
		/// <returns>object read from the stream</returns>
		public object Read()
		{
			//bool customWritten = this.ReadBoolean();
			//that value is always written for "object" writes, but we don't need it here.
			return CompactBinaryFormatter.Deserialize(this);
		}

		public void Read<T>(T instance, bool useCompression) where T : ICustomSerializable
		{
			Serializer.Deserialize(stream, instance, useCompression);
		}

		public void Read<T>(T instance, SerializerFlags flags) where T : ICustomSerializable
		{
			Serializer.Deserialize(stream, instance, flags);
		}

		public T Read<T>(bool useCompression) where T : ICustomSerializable, new()
		{
			return Serializer.Deserialize<T>(stream, useCompression);
		}

		public T Read<T>(SerializerFlags flags) where T : ICustomSerializable, new()
		{
			return Serializer.Deserialize<T>(stream, flags);
		}

		/// <summary>
		/// Do not Use this!  Actually.  DO NOT use ICustomSerializable.
		/// Use Read(bool useCompression), and convert your domain object to ICustomSerializable.
		/// </summary>
		/// <typeparam name="T">object to be deserialized</typeparam>
		/// <returns>object deserialized</returns>
		public T Read<T>() where T : new()
		{
			if (Serializer.IsSerializable(typeof(T)) == true)
			{
				return Serializer.Deserialize<T>(this);
			}
			else
			{
				return (T)CompactBinaryFormatter.Deserialize(this);
			}
		}

		/// <summary>
		/// Reads an object of type <see cref="bool"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public bool ReadBoolean()
		{
			return stream.ReadByte() != 0;
			//return reader.ReadBoolean(); 
		}
		/// <summary>
		/// Reads an object of type <see cref="byte"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public byte ReadByte()
		{
			return (byte)stream.ReadByte();
			//return reader.ReadByte(); 
		}
		/// <summary>
		/// Reads an object of type <see cref="byte[]"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <param name="count">number of bytes read</param>
		/// <returns>object read from the stream</returns>
		public byte[] ReadBytes(int count)
		{
			byte[] buffer = SafeMemoryAllocator.CreateArray<byte>(count);
			int offset = 0;
			do
			{
				int read = stream.Read(buffer, offset, count);
				if (read == 0)
				{
					break;
				}
				offset += read;
				count -= read;
			}
			while (count > 0);

			if (offset != buffer.Length)
			{
				string length = "unknown";
				string position = "unknown";
				if (stream.CanSeek)
				{
					length = stream.Length.ToString();
					position = stream.Position.ToString();
				}

				//this will happen when the stream is shorter than count
				throw new ApplicationException(
			  string.Format("CompactBinaryReader: Tried to read past end of stream, failed to read bytes from stream. count: {0}, offset {1}, buffer.Length {2}, stream.Length {3}, stream.Position {4}",
			  count, offset, buffer.Length, length, position));
			}

			return buffer;
			//return reader.ReadBytes(count); 
		}
		/// <summary>
		/// Reads an object of type <see cref="char"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public char ReadChar()
		{
			if (reader == null)
			{
				reader = new BinaryReader(stream);
			}
			return reader.ReadChar();
		}
		/// <summary>
		/// Reads an object of type <see cref="char[]"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public char[] ReadChars(int count)
		{
			if (reader == null)
			{
				reader = new BinaryReader(stream);
			}
			return reader.ReadChars(count);
		}
		/// <summary>
		/// Reads an object of type <see cref="decimal"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public decimal ReadDecimal()
		{
			if (reader == null)
			{
				reader = new BinaryReader(stream);
			}
			return reader.ReadDecimal();
		}

		/// <summary>
		/// Reads an object of type <see cref="float"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public float ReadSingle()
		{
			//have to move to unsafe first
			//this.FillBuffer(4);
			//uint num = (uint)(((readBuffer[0] | (readBuffer[1] << 8)) | (readBuffer[2] << 0x10)) | (readBuffer[3] << 0x18));
			//return *(((float*)&num));

			if (reader == null)
			{
				reader = new BinaryReader(stream);
			}
			return reader.ReadSingle();
		}
		/// <summary>
		/// Reads an object of type <see cref="double"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public double ReadDouble()
		{
			//have to move to unsafe for this
			//FillBuffer(8);
			//uint num = (uint)(((readBuffer[0] | (readBuffer[1] << 8)) | (readBuffer[2] << 0x10)) | (readBuffer[3] << 0x18));
			//uint num2 = (uint)(((readBuffer[4] | (readBuffer[5] << 8)) | (readBuffer[6] << 0x10)) | (readBuffer[7] << 0x18));
			//ulong num3 = (num2 << 0x20) | num;
			//return *(((double*)&num3));
			if (reader == null)
			{
				reader = new BinaryReader(stream);
			}
			return reader.ReadDouble();
		}
		/// <summary>
		/// Reads an object of type <see cref="short"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public short ReadInt16()
		{
			FillBuffer(2);
			return (short)(this.readBuffer[0] | (this.readBuffer[1] << 8));
		}
		/// <summary>
		/// Reads an object of type <see cref="int"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public int ReadInt32()
		{
			this.FillBuffer(4);
			return (((this.readBuffer[0] |
				 (this.readBuffer[1] << 8)) |
				 (this.readBuffer[2] << 0x10)) |
				 (this.readBuffer[3] << 0x18));
		}

		/// <summary>
		/// Reads an object of type <see cref="long"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public long ReadInt64()
		{
			FillBuffer(8);
			uint num = (uint)(((this.readBuffer[0] | (this.readBuffer[1] << 8)) | (this.readBuffer[2] << 0x10)) | (this.readBuffer[3] << 0x18));
			uint num2 = (uint)(((this.readBuffer[4] | (this.readBuffer[5] << 8)) | (this.readBuffer[6] << 0x10)) | (this.readBuffer[7] << 0x18));
			return (long)((((long)num2) << 0x20) | num);
		}

		/// <summary>
		/// Reads an object of type <see cref="string"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public string ReadString()
		{
			if (reader == null)
			{
				reader = new BinaryReader(stream);
			}
			string str = reader.ReadString();
			if (str == "\0")
			{
				str = null;
			}
			return str;
		}
		/// <summary>
		/// Reads an object of type <see cref="DateTime"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public DateTime ReadDateTime()
		{
			return new DateTime(ReadInt64());
		}
		/// <summary>
		/// Reads the specifies number of bytes into <paramref name="buffer"/>.
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <param name="buffer">buffer to read into</param>
		/// <param name="index">starting position in the buffer</param>
		/// <param name="count">number of bytes to write</param>
		/// <returns>number of buffer read</returns>
		public int Read(byte[] buffer, int index, int count)
		{
			return stream.Read(buffer, index, count);
		}
		/// <summary>
		/// Reads the specifies number of bytes into <paramref name="buffer"/>.
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <param name="buffer">buffer to read into</param>
		/// <param name="index">starting position in the buffer</param>
		/// <param name="count">number of bytes to write</param>
		/// <returns>number of chars read</returns>
		public int Read(char[] buffer, int index, int count)
		{
			if (reader == null)
			{
				reader = new BinaryReader(stream);
			}
			return reader.Read(buffer, index, count);
		}
		/// <summary>
		/// Reads an object of type <see cref="sbyte"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>
		public sbyte ReadSByte()
		{
			this.FillBuffer(1);
			return (sbyte)readBuffer[0];
		}

		/// <summary>
		/// Reads an object of type <see cref="ushort"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>		
		public ushort ReadUInt16()
		{
			this.FillBuffer(2);
			return (ushort)(this.readBuffer[0] | (this.readBuffer[1] << 8));
		}
		/// <summary>
		/// Reads an object of type <see cref="uint"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>		
		public uint ReadUInt32()
		{
			this.FillBuffer(4);
			return (uint)(((this.readBuffer[0] | (this.readBuffer[1] << 8)) | (this.readBuffer[2] << 0x10)) | (this.readBuffer[3] << 0x18));
		}

		/// <summary>
		/// Reads an object of type <see cref="ulong"/> from the current stream 
		/// and advances the stream position. 
		/// This method reads directly from the underlying stream.
		/// </summary>
		/// <returns>object read from the stream</returns>		
		public ulong ReadUInt64()
		{
			this.FillBuffer(8);
			ulong num = this.readBuffer[0] | ((ulong)this.readBuffer[1] << 8) | ((ulong)this.readBuffer[2] << 0x10) | ((ulong)this.readBuffer[3] << 0x18);
			ulong num2 = this.readBuffer[4] | ((ulong)this.readBuffer[5] << 8) | ((ulong)this.readBuffer[6] << 0x10) | ((ulong)this.readBuffer[7] << 0x18);
			return ((num2 << 0x20) | num);
		}

		/// <summary>
		/// Reads an <see cref="Int32"/> in a format that allows smaller values to occupy less space
		/// (as little as 1 byte). The trade off is that bigger values occupy more space (as much as 5 bytes).
		/// Use <see cref="IPrimitiveWriter.WriteVarInt32"/> and <see cref="ReadVarInt32"/> instead of
		/// <see cref="IPrimitiveWriter.Write(Int32)"/> and <see cref="ReadInt32"/> when you expect
		/// the value to be less than 134,217,728 or greater than -134,217,728 more
		/// often than not.
		/// </summary>
		/// <returns>The de-serialized <see cref="Int32"/> value.</returns>
		/// <remarks>
		/// Exact space consumption.
		/// -63 to 63										1 byte
		/// -8,192 to 8,192							2 bytes
		/// -1,048,576 to 1,048,576			3 bytes
		/// -134,217,728 to 134,217,728	4 bytes
		/// Everything else							5 bytes
		/// </remarks>
		public int ReadVarInt32()
		{
			return BaseStream.ReadVarInt32();
		}

		/// <summary>
		/// Reads a collection of objects which implement ICustomSerializable from the current stream.
		/// </summary>
		public List<T> ReadList<T>() where T : ICustomSerializable, new()
		{
			int count = ReadInt32();

			if (count == -1)
			{
				return null;
			}
			else
			{
				List<T> list = SafeMemoryAllocator.CreateList<T>(count);

				for (int i = 0; i < count; i++)
				{
					if (ReadBoolean())
					{
						T item = new T();

						item.Deserialize(this);

						list.Add(item);
					}
					else
					{
						list.Add(default(T));
					}
				}
				return list;
			}
		}

		public int[] ReadInt32Array()
		{
			int count = ReadInt32();

			if (count == -1)
			{
				return null;
			}
			else
			{
				int[] array = SafeMemoryAllocator.CreateArray<int>(count);

				for (int i = 0; i < count; i++)
				{
					int value = ReadInt32();
					array[i] = value;
				}
				return array;
			}
		}

		/// <summary>
		/// Reads an array of generic objects which implement ICustomSerializable from the current stream.
		/// </summary>
		public T[] ReadArray<T>() where T : ICustomSerializable, new()
		{
			int count = ReadInt32();

			if (count == -1)
			{
				return null;
			}
			else
			{
				T[] array = SafeMemoryAllocator.CreateArray<T>(count);

				for (int i = 0; i < count; i++)
				{
					T item = new T();

					item.Deserialize(this);

					array[i] = item;
				}
				return array;
			}
		}

		public Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>() where TValue : ICustomSerializable, new()
		{
			int count = ReadInt32();

			if (count == -1)
			{
				return null;
			}
			else
			{
				Dictionary<TKey, TValue> dict = new Dictionary<TKey, TValue>(count);
				for (int i = 0; i < count; i++)
				{
					TKey key = (TKey)this.Read();
					TValue value = new TValue();
					value.Deserialize(this);
					dict.Add(key, value);
				}
				return dict;
			}
		}
		#endregion

		#region Nullable Routines

		//public int ReadNullable(byte[] buffer, int index, int count)
		//{
		//   if (ReadBoolean())
		//      return 0;
		//   else
		//   {
		//      int len = ReadInt32();
		//   }
		//}

		//public int ReadNullable(char[] buffer, int index, int count)
		//{
		//   if (ReadBoolean())
		//      return 0;
		//   else
		//   {
		//      int len = ReadInt32();
		//   }
		//}

		public bool? ReadNullableBoolean()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadBoolean();
		}

		public byte? ReadNullableByte()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadByte();
		}

		public byte?[] ReadNullableBytes(int count)
		{
			if (ReadBoolean())
				return null;
			else
			{
				int len = ReadInt32();
				byte?[] bytes = SafeMemoryAllocator.CreateArray<byte?>(len);

				for (int i = 0; i < len; i++)
				{
					if (i < count)
						bytes[i] = ReadNullableByte();
					else
						ReadNullableByte();
				}

				return bytes;
			}
		}

		public char? ReadNullableChar()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadChar();
		}

		public char?[] ReadNullableChars(int count)
		{
			if (ReadBoolean())
				return null;
			else
			{
				int len = ReadInt32();
				char?[] chars = SafeMemoryAllocator.CreateArray<char?>(len);

				for (int i = 0; i < len; i++)
				{
					if (i < count)
						chars[i] = ReadNullableChar();
					else
						ReadNullableChar();
				}

				return chars;
			}
		}

		public DateTime? ReadNullableDateTime()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadDateTime();
		}

		public decimal? ReadNullableDecimal()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadDecimal();
		}

		public double? ReadNullableDouble()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadDouble();
		}

		public short? ReadNullableInt16()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadInt16();
		}

		public int? ReadNullableInt32()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadInt32();
		}

		public long? ReadNullableInt64()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadInt64();
		}

		public sbyte? ReadNullableSByte()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadSByte();
		}

		public float? ReadNullableSingle()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadSingle();
		}

		public ushort? ReadNullableUInt16()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadUInt16();
		}

		public uint? ReadNullableUInt32()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadUInt32();
		}

		public ulong? ReadNullableUInt64()
		{
			if (ReadBoolean())
				return null;
			else
				return ReadUInt64();
		}

		#endregion

		//T MySpace.Common.IO.IPrimitiveReader.Read<T>()
		//{
		//   throw new Exception("The method or operation is not implemented.");
		//}

		//List<T> MySpace.Common.IO.IPrimitiveReader.ReadList<T>()
		//{
		//   throw new Exception("The method or operation is not implemented.");
		//}

		//T[] MySpace.Common.IO.IPrimitiveReader.ReadArray<T>()
		//{
		//   throw new Exception("The method or operation is not implemented.");
		//}

		//Dictionary<TKey, TValue> MySpace.Common.IO.IPrimitiveReader.ReadDictionary<TKey, TValue>()
		//{
		//   throw new Exception("The method or operation is not implemented.");
		//}

		//yes this is blatantly stolen from binaryreader
		private void FillBuffer(int numBytes)
		{

			if (readBuffer == null)
			{
				readBuffer = new byte[16];
			}

			int offset = 0;
			int read = 0;

			if (numBytes == 1)
			{
				read = stream.ReadByte();
				readBuffer[0] = (byte)read;
			}
			else
			{
				do
				{
					read = stream.Read(readBuffer, offset, numBytes - offset);
					if (read == 0)
					{
						throw new ApplicationException(String.Format("Got a zero byte read when attempting to read {0} bytes from a stream of length {1} that is at position {2}", numBytes, stream.Length, stream.Position));
					}
					offset += read;
				}
				while (offset < numBytes);
			}
		}

		/// <summary>
		/// 	<para>Reads a <see cref="DateTime"/> object from the stream
		/// 	that was serialized with the <see cref="CompactBinaryWriter.WriteRoundTripDateTime"/>
		/// 	method.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="DateTime"/> object read from the stream.</para>
		/// </returns>
		public DateTime ReadRoundTripDateTime()
		{
			byte byteKind = this.BaseReader.ReadByte();
			DateTimeKind kind = (DateTimeKind)byteKind;
			long ticks = this.BaseReader.ReadInt64();

			DateTime value = new DateTime(ticks, kind);
			return value;
		}
	}
}
