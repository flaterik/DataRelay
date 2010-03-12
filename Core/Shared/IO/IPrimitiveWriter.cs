using System;
using System.Collections.Generic;
namespace MySpace.Common.IO
{
	public interface IPrimitiveWriter
	{
		/// <summary>
		/// Disposeable... if you dont know, dont use it :)
		/// </summary>
		void Dispose();
		System.IO.Stream BaseStream { get; }

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
		IDisposable CreateRegion();

		#region Basic Types

		/// <summary>
		///		<para>Writes a <see cref="DateTime"/> object to the stream by only recording
		///		its tick value, this method is more space-efficient than <see cref="WriteRoundTripDateTime"/>
		///		but cannot reproduce exactly the same <see cref="DateTime"/> value when
		///		deserialized using the <see cref="IPrimitiveReader.ReadDateTime"/> method.</para>
		/// </summary>
		/// <param name="value">
		///		<para>The <see cref="DateTime"/> value to write to the stream.</para>
		/// </param>
		void Write(DateTime value);
		/// <summary>
		///		<para>Writes a <see cref="DateTime"/> object to the stream that can
		///		be deserialized in its entirety, including its <see cref="DateTime.Kind"/>
		///		property, using the <see cref="IPrimitiveReader.ReadRoundTripDateTime"/>
		///		method.</para>
		/// </summary>
		/// <param name="value">
		///		<para>The <see cref="DateTime"/> value to write to the stream.</para>
		/// </param>
		void WriteRoundTripDateTime(DateTime value);
		void Write(byte[] buffer, int index, int count);
		void Write(string value);
		void Write(float value);
		void Write(double value);
		void Write(uint value);
		void Write(ulong value);
		void Write(ushort value);
		void Write(char[] chars, int index, int count);
		void Write(sbyte value);
		void Write(byte[] buffer);
		void Write(char ch);
		void Write(byte value);
		void Write(bool value);
		void Write(char[] chars);
		void Write(decimal value);
		void Write(long value);
		void Write(short value);
		void Write(int value);

		#endregion

		#region Nullable Types

		void Write(DateTime? value);
		void Write(byte?[] buffer, int index, int count);
		void Write(float? value);
		void Write(double? value);
		void Write(uint? value);
		void Write(ulong? value);
		void Write(ushort? value);
		void Write(char?[] chars, int index, int count);
		void Write(sbyte? value);
		void Write(byte?[] buffer);
		void Write(char? ch);
		void Write(byte? value);
		void Write(bool? value);
		void Write(char?[] chars);
		void Write(decimal? value);
		void Write(long? value);
		void Write(short? value);
		void Write(int? value);

		#endregion

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
		void WriteVarInt32(int value);

		/// <summary>
		/// Do not Use this!  Actually.  No NOT use ICustomSerializable.
		/// Use <see cref="Write{T}(T, bool)"/>, and convert your domain object to IVersionSerializable.
		/// </summary>
		[Obsolete("Use Write<T>(T, bool)")]
		void Write<T>(T graph);
		void WriteList<T>(List<T> collection) where T : ICustomSerializable;
		void WriteArray<T>(T[] collection) where T : ICustomSerializable;
		void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary) where TValue : ICustomSerializable;

		void Write<T>(T obj, bool useCompression) where T : ICustomSerializable;
	}
}
