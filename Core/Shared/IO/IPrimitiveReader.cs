using System;
using System.Collections.Generic;
namespace MySpace.Common.IO
{
	/// <summary>
	/// Use this reader's methods to deserialize your object. Remember to read back in the properties
	/// in the same order as they were written.
	/// </summary>
	public interface IPrimitiveReader
	{
		/// <summary>
		/// Disposable. If you dont know what this is. Dont use it :)
		/// </summary>
		void Dispose();
		System.IO.Stream BaseStream { get; }

		/// <summary>
		/// You are given the option to send messages back to the serializer that describe the results
		/// of your deserialization. 
		/// </summary>
		SerializationResponse Response { get; set; }

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
		/// Read in a non-nullable byte array. Use this method if you only wish to read in 
		/// a part of the serialized byte array. Otherwise, use the ReadBytes(int) method.
		/// </summary>
		/// <param name="buffer">The buffer to read the bytes into.</param>
		/// <param name="index">The start index to being reading from.</param>
		/// <param name="count">The number of bytes to read.</param>
		/// <returns>The number of bytes read.</returns>
		int Read(byte[] buffer, int index, int count);

		/// <summary>
		/// Read in a non-nullable char array.
		/// </summary>
		/// <param name="buffer">The buffer to read the chara into.</param>
		/// <param name="index">The start index to being reading from.</param>
		/// <param name="count">The number of chars to read.</param>
		/// <returns>The number of chars read.</returns>
		int Read(char[] buffer, int index, int count);

		/// <summary>
		/// Read a single object from the stream.
		/// </summary>
		/// <returns>A single object</returns>
		object Read();

		/// <summary>
		/// Read a non-nullable single boolean value from the stream.
		/// </summary>
		/// <returns>A single boolean value</returns>
		bool ReadBoolean();

		/// <summary>
		/// Read a non-nullable single byte from the stream.
		/// </summary>
		/// <returns>A single byte</returns>
		byte ReadByte();

		/// <summary>
		/// Read a non-nullable byte[] from the stream. If you wish to only read a partial segment
		/// of the serialized stream, use the Read(byte[], int, int) method.
		/// </summary>
		/// <returns>A byte array the size of the specified count</returns>
		byte[] ReadBytes(int count);

		/// <summary>
		/// Read a non-nullable single char from the stream.
		/// </summary>
		/// <returns>A single char</returns>
		char ReadChar();

		/// <summary>
		/// Read a non-nullable char[] from the stream. If you wish to only read a partial segment
		/// of the serialized stream, use the Read(char[], int, int) method.
		/// </summary>
		/// <returns>A char array the size of the specified count</returns>
		char[] ReadChars(int count);

		/// <summary>
		///		Read a <see cref="DateTime"/> object from the stream 
		///		serialized with the <see cref="IPrimitiveWriter.Write(DateTime)"/> method.
		/// </summary>
		/// <returns>A <see cref="DateTime"/> object read from the stream.</returns>
		DateTime ReadDateTime();

		/// <summary>
		///		<para>Reads a <see cref="DateTime"/> object from the stream
		///		that was serialized with the <see cref="IPrimitiveWriter.WriteRoundTripDateTime"/>
		///		method.</para>
		/// </summary>
		/// <returns>A <see cref="DateTime"/> object read from the stream.</returns>
		DateTime ReadRoundTripDateTime();

		/// <summary>
		/// Read a non-nullable decimal value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A decimal value</returns>
		decimal ReadDecimal();

		/// <summary>
		/// Read a non-nullable double value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A double value</returns>
		double ReadDouble();

		/// <summary>
		/// Read a non-nullable Int16 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An Int16 value</returns>
		short ReadInt16();

		/// <summary>
		/// Read a non-nullable Int32 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An Int32 value</returns>
		int ReadInt32();

		/// <summary>
		/// Read a non-nullable Int64 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An Int64 value</returns>
		long ReadInt64();

		/// <summary>
		/// Read a non-nullable SByte value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An SByte value</returns>
		sbyte ReadSByte();

		/// <summary>
		/// Read a non-nullable floating point value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A floating point value</returns>
		float ReadSingle();

		/// <summary>
		/// Read a non-nullable string object from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A string object</returns>
		string ReadString();

		/// <summary>
		/// Read a non-nullable unsigned Int16 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An unsigned Int16 value</returns>
		ushort ReadUInt16();

		/// <summary>
		/// Read a non-nullable unsigned Int32 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An unsigned Int32 value</returns>
		uint ReadUInt32();

		/// <summary>
		/// Read a non-nullable unsigned Int64 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An unsigned Int64 value</returns>
		ulong ReadUInt64();

		#endregion

		#region Nullable Types

		/// <summary>
		/// Read a nullable boolean value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable boolean value, or null</returns>
		bool? ReadNullableBoolean();

		/// <summary>
		/// Read a nullable byte value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable byte, or null</returns>
		byte? ReadNullableByte();

		/// <summary>
		/// Read an array of nullable bytes value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An array of nullable bytes, or null</returns>
		byte?[] ReadNullableBytes(int count);

		/// <summary>
		/// Read a nullable char value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable char, or null</returns>
		char? ReadNullableChar();

		/// <summary>
		/// Read an array of nullable chars value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>An array of nullable chars, or null</returns>
		char?[] ReadNullableChars(int count);

		/// <summary>
		/// Read a nullable DateTime object from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable DateTime object, or null</returns>
		DateTime? ReadNullableDateTime();

		/// <summary>
		/// Read a nullable decimal value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable decimal value</returns>
		decimal? ReadNullableDecimal();

		/// <summary>
		/// Read a nullable double value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable double value</returns>
		double? ReadNullableDouble();

		/// <summary>
		/// Read a nullable Int16 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable Int16 value</returns>
		short? ReadNullableInt16();

		/// <summary>
		/// Read a nullable Int132 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable Int32 value</returns>
		int? ReadNullableInt32();

		/// <summary>
		/// Read a nullable Int64 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable Int64 value</returns>
		long? ReadNullableInt64();

		/// <summary>
		/// Read a nullable sbyte value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable sbyte value</returns>
		sbyte? ReadNullableSByte();

		/// <summary>
		/// Read a nullable floating point value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable floating point value</returns>
		float? ReadNullableSingle();

		/// <summary>
		/// Read a nullable unsigned Int16 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable unsigned Int16 value</returns>
		ushort? ReadNullableUInt16();

		/// <summary>
		/// Read a nullable unsigned Int32 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable unsigned Int32 value</returns>
		uint? ReadNullableUInt32();

		/// <summary>
		/// Read a nullable unsigned Int64 value from the stream. Please refrain from implicit conversion. Always
		/// read in the type that it was serialized at, and perform conversion afterward.
		/// </summary>
		/// <returns>A nullable unsigned Int64 value</returns>
		ulong? ReadNullableUInt64();


		#endregion

		#region ICustomSerializable Methods ( soon to leave )

		/// <summary>
		/// Use this method to read in serialized objects that are either ICustomSerializable or just
		/// newable. If the objects are not ICustomSerializable, they will be serialized using the binary
		/// formatter wich will result in an exponentially larger memory stream. 
		/// 
		/// NOTE: If your object is IVersionSerializable, please use the  T Read<T>(bool useCompression)
		/// method to correctly deserialize your object, but only if it was serialized with the
		/// T Read<T>(bool useCompression) method on the writer.
		/// </summary>
		/// <typeparam name="T">Type of object to deserialize</typeparam>
		/// <returns>Deserialized object</returns>
		T Read<T>() where T : new();

		/// <summary>
		/// Use this object to read in a stronly typed list of ICustomSerialized objects. If your object
		/// list is an array, rather than a List, please use the ReadArray<T>() method.
		/// </summary>
		/// <typeparam name="T">The type of objects in the list</typeparam>
		/// <returns>A strongly typed List of ICsutomSerializable objects</returns>
		List<T> ReadList<T>() where T : ICustomSerializable, new();

		/// <summary>
		/// Use this to read in a stronly typed array of ICustomSerialized objects. If your object
		/// is a List, rather than an array, please use the ReadList<T>() method.
		/// </summary>
		/// <typeparam name="T">Type of ICustomSerializable objects in the array</typeparam>
		/// <returns>An array of ICustomSerializable objects</returns>
		T[] ReadArray<T>() where T : ICustomSerializable, new();

		/// <summary>
		/// Use this to read in a dictionary of key value pairs where the Value is ICustomSerializable
		/// </summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <returns>The dictionary</returns>
		Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>() where TValue : ICustomSerializable, new();

		#endregion

		/// <summary>
		/// Reads an <see cref="Int32"/> in a format that allows smaller values to occupy less space
		/// (as little as 1 byte). The trade off is that bigger values occupy more space (as much as 5 bytes).
		/// Use <see cref="IPrimitiveWriter.WriteVarInt32"/> and <see cref="ReadVarInt32"/> instead of
		/// <see cref="IPrimitiveWriter.Write(Int32)"/> and <see cref="ReadInt32"/> when you expect
		/// the value to be less than 134,217,728 or greater than -134,217,728 more
		/// often than not.
		/// </summary>
		/// <remarks>
		/// Exact space consumption.
		/// -63 to 63										1 byte
		/// -8,192 to 8,192							2 bytes
		/// -1,048,576 to 1,048,576			3 bytes
		/// -134,217,728 to 134,217,728	4 bytes
		/// Everything else							5 bytes
		/// </remarks>
		int ReadVarInt32();

		/// <summary>
		/// Use this method to deserialize objects that are IVersionSerializable. This will preserve the 
		/// versioning information within the stream. This is the only method that can correctly
		/// deserialize objects that have been serialized as IVersionSerializable.
		/// </summary>
		/// <typeparam name="T">The type of the object</typeparam>
		/// <param name="useCompression">Option to compress this portion of the stream. If the parent
		/// object is set to use compression, using compression here would be un needed overhead.</param>
		/// <returns>The IVersionSerializable object of the type requested</returns>
		T Read<T>(bool useCompression) where T : ICustomSerializable, new();
		T Read<T>(SerializerFlags flags) where T : ICustomSerializable, new();
		void Read<T>(T instance, bool useCompression) where T : ICustomSerializable;
		void Read<T>(T instance, SerializerFlags flags) where T : ICustomSerializable;
	}
}
