using System;
using System.Diagnostics;
using System.IO;

using MySpace.Common.CompactSerialization.Formatters;
using System.Collections.Generic;
using MySpace.Common.IO;

namespace MySpace.Common.CompactSerialization.IO
{
	/// <summary>
	/// This class encapsulates a <see cref="BinaryWriter"/> object. It also provides an extra
	/// Write method for <see cref="System.Object"/> types. 
	/// </summary>
	public class CompactBinaryWriter : IDisposable, MySpace.Common.IO.IPrimitiveWriter
	{
		private System.IO.BinaryWriter writer;

		/// <summary>
		/// Constructs the object over a <see cref="BinaryWriter"/> object.
		/// </summary>
		/// <param name="binaryWriter"><see cref="BinaryWriter"/> object</param>
		public CompactBinaryWriter(System.IO.BinaryWriter binaryWriter)
		{
			writer = binaryWriter;
		}

		/// <summary> Returns the underlying <see cref="BinaryWriter"/> object. </summary>
		public System.IO.BinaryWriter BaseWriter { get { return writer; } }

		private readonly Stack<long> regionStack = new Stack<long>();
		private RegionCloser regionCloser;
		private RegionCloser GetRegionCloser()
		{
			if (regionCloser == null) //don't bother with thread saftey here
			{
				regionCloser = new RegionCloser() { Writer = this };
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
			Int32 length = 0;
			const byte headerLength = sizeof(Int32);
			Write(headerLength); // write header length
			long lengthPosition = writer.BaseStream.Position;
			Write(length);
			regionStack.Push(lengthPosition);

			return GetRegionCloser();
		}

		private void CloseRegion()
		{
			Int32 length = 0;
			long lengthPosition = regionStack.Pop();
			long endPosition = BaseStream.Position;
			BaseStream.Seek(lengthPosition, SeekOrigin.Begin);
			length = (Int32)(endPosition - lengthPosition - sizeof(Int32)/*length*/);
			Write(length);
			BaseStream.Seek(endPosition, SeekOrigin.Begin);
		}

		private class RegionCloser : IDisposable
		{
			public CompactBinaryWriter Writer;

			#region IDisposable Members

			public void Dispose()
			{
				Writer.CloseRegion();
			}

			#endregion
		}

		/// <summary>
		/// Close the underlying <see cref="BinaryWriter"/>.
		/// </summary>
		public void Dispose()
		{
			if (writer != null) writer.Close();
		}

		/// <summary>
		/// Returns the underlying <see cref="Stream"/> object.
		/// </summary>
		public Stream BaseStream
		{
			get { return this.writer.BaseStream; }
		}

		#region Basic Routines

		public void Write<T>(T obj, bool useCompression) where T : ICustomSerializable
		{
			Serializer.Serialize(this.BaseWriter.BaseStream, obj, useCompression);
		}

		/// <summary>
		/// Writes <paramref name="graph"/> to the current stream and advances the stream position.
		/// If T implements ICustomSerializable, it will be used.
		/// 
		/// Do not Use this!  Actually.  No NOT use ICustomSerializable.
		/// Use Write&lt;T&gt;(T graph, bool useCompression), and convert your domain object to IVersionSerializable.
		/// </summary>
		/// <typeparam name="T">A newable type</typeparam>
		/// <param name="graph">Object to write</param>
		public void Write<T>(T graph)
		{
			if (graph is ICustomSerializable)
			{
				Serializer.Serialize(this, graph, SerializerFlags.Default);
			}
			else
			{
				CompactBinaryFormatter.Serialize(this, graph);
			}
		}

		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(bool value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(byte value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="buffer"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="buffer">Object to write</param>
		public void Write(byte[] buffer) { writer.Write(buffer); }
		/// <summary>
		/// Writes <paramref name="ch"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="ch">Object to write</param>
		public void Write(char ch) { writer.Write(ch); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(short value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(int value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(long value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="chars"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="chars">Object to write</param>
		public void Write(char[] chars) { writer.Write(chars); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(decimal value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(float value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(double value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(string value)
		{
			if (value == null)
			{
				value = "\0";
			}
			writer.Write(value);
		}
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(DateTime value) { writer.Write(value.Ticks); }
		/// <summary>
		/// Writes <paramref name="buffer"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="buffer">buffer to write</param>
		/// <param name="index">starting position in the buffer</param>
		/// <param name="count">number of bytes to write</param>
		public void Write(byte[] buffer, int index, int count) { writer.Write(buffer, index, count); }
		/// <summary>
		/// Writes <paramref name="chars"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="chars">buffer to write</param>
		/// <param name="index">starting position in the buffer</param>
		/// <param name="count">number of bytes to write</param>
		public void Write(char[] chars, int index, int count) { writer.Write(chars, index, count); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(sbyte value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(ushort value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(uint value) { writer.Write(value); }
		/// <summary>
		/// Writes <paramref name="value"/> to the current stream and advances the stream position. 
		/// This method writes directly to the underlying stream.
		/// </summary>
		/// <param name="value">Object to write</param>
		public void Write(ulong value) { writer.Write(value); }

		/// <summary>
		/// Writes an <see cref="Int32"/> in a format that allows smaller values to occupy less space.
		/// The trade off is that bigger values occupy more space (as much as 5 bytes).
		/// Use <see cref="WriteVarInt32"/> and <see cref="IPrimitiveReader.ReadVarInt32"/> instead of
		/// <see cref="Write(Int32)"/> and <see cref="IPrimitiveReader.ReadInt32"/> when you expect
		/// <paramref name="value"/> to be less than 134,217,728 or greater than -134,217,728 more
		/// often than not.
		/// </summary>
		/// <param name="value">The <see cref="Int32"/> value to write.</param>
		/// <remarks>
		/// Exact space consumption.
		/// -63 to 63										1 byte
		/// -8,192 to 8,192							2 bytes
		/// -1,048,576 to 1,048,576			3 bytes
		/// -134,217,728 to 134,217,728	4 bytes
		/// Everything else							5 bytes
		/// </remarks>
		public void WriteVarInt32(int value)
		{
			BaseStream.WriteVarInt32(value);
		}

		/// <summary>
		/// Writes <paramref name="collection"/> to the current stream and advances the stream position. 
		/// </summary>
		/// <param name="collection">The collection to write.</param>
		public void WriteList<T>(List<T> collection) where T : ICustomSerializable
		{
			if (collection == null)
			{
				writer.Write(-1);
			}
			else
			{
				writer.Write(collection.Count);
				foreach (T t in collection)
				{
					if (t != null)
					{
						writer.Write(true);
						t.Serialize(this);
					}
					else
					{
						writer.Write(false);
					}
				}
			}
		}

		/// <summary>
		/// Writes <paramref name="array"/> to the current stream and advances the stream position. 
		/// </summary>
		/// <param name="array">The array to write.</param>
		public void WriteArray<T>(T[] array) where T : ICustomSerializable
		{
			if (array == null)
			{
				writer.Write(-1);
			}
			else
			{
				writer.Write(array.Length);
				foreach (T t in array)
				{
					t.Serialize(this);
				}
			}
		}

		public void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary) where TValue : ICustomSerializable
		{
			if (dictionary == null)
			{
				writer.Write(-1);
			}
			else
			{
				writer.Write(dictionary.Count);
				foreach (TKey key in dictionary.Keys)
				{
					this.Write((object)key);
					dictionary[key].Serialize(this);
				}
			}
		}

		#endregion

		#region Nullable Routines


		public void Write(DateTime? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(byte?[] buffer, int index, int count)
		{
			if (WriteNullHeader(buffer))
			{
				Write(count);
				int len = index + count;
				for (int i = index; i < len; i++)
				{
					byte? tByte = buffer[i];
					if (WriteNullHeader(tByte))
						Write(tByte.Value);
				}
			}
		}

		public void Write(float? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(double? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(uint? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(ulong? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(ushort? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(char?[] chars, int index, int count)
		{
			if (WriteNullHeader(chars))
			{
				Write(count);
				int len = index + count;
				for (int i = index; i < len; i++)
				{
					char? tChar = chars[i];
					if (WriteNullHeader(tChar))
						Write(tChar.Value);
				}
			}
		}

		public void Write(sbyte? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(byte?[] buffer)
		{
			if (WriteNullHeader(buffer))
			{
				Write(buffer.Length);
				for (int i = 0; i < buffer.Length; i++)
				{
					byte? tByte = buffer[i];
					if (WriteNullHeader(tByte))
						Write(tByte.Value);
				}
			}
		}

		public void Write(char? ch)
		{
			if (WriteNullHeader(ch))
				Write(ch.Value);
		}

		public void Write(byte? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(bool? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(char?[] chars)
		{
			if (WriteNullHeader(chars))
			{
				Write(chars.Length);
				for (int i = 0; i < chars.Length; i++)
				{
					char? tChar = chars[i];
					if (WriteNullHeader(tChar))
						Write(tChar.Value);
				}
			}
		}

		public void Write(decimal? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(long? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(short? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}

		public void Write(int? value)
		{
			if (WriteNullHeader(value))
				Write(value.Value);
		}


		private bool WriteNullHeader(object obj)
		{
			bool isnull = (obj == null);
			Write(isnull);
			return !isnull;
		}


		#endregion

		/// <summary>
		/// 	<para>Writes a <see cref="DateTime"/> object to the stream that can
		/// 	be deserialized in its entirety, including its <see cref="DateTime.Kind"/>
		/// 	property, using the <see cref="CompactBinaryReader.ReadRoundTripDateTime"/>
		/// 	method.</para>
		/// </summary>
		/// <param name="value">
		/// 	<para>The <see cref="DateTime"/> value to write to the stream.</para>
		/// </param>
		public void WriteRoundTripDateTime(DateTime value)
		{
			int intKind = (int)value.Kind;

			// We need to reduce the Kind value to a byte,
			// but need to first ensure that it can be.
			// [tchow 09/20/2007]
			if (intKind > Byte.MaxValue || intKind < Byte.MinValue)
			{
				// This shouldn't happen.  But if it does in a future version
				// of .NET, we cannot let it pass silently. [tchow 09/20/2007]
				throw new ApplicationException("Unexpected DateTime.Kind value.");
			}

			byte byteKind = (byte)intKind;

			this.BaseWriter.Write(byteKind);
			this.BaseWriter.Write(value.Ticks);
		}
	}
}
