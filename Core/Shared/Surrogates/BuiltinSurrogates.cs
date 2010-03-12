/*

Compact Serialization Framework
Copyright (C) 2006 Shoaib Ali

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA

for bug-reports and suggestions alleey@gmail.com

*/
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

using MySpace.Common.CompactSerialization.IO;
using MySpace.Common.IO;

namespace MySpace.Common.CompactSerialization.Surrogates
{
	#region /       Default surrogates       /

	/// <summary>
	/// Surrogate for null values. 
	/// </summary>
	sealed class NullSerializationSurrogate : SerializationSurrogate
	{
		public NullSerializationSurrogate() : base(typeof(NullSerializationSurrogate)) { }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Object"/> objects. Also serves the
	/// purpose of default surrogate. It uses .NET native serialization
	/// </summary>
	sealed class ObjectSerializationSurrogate : SerializationSurrogate
	{
		static BinaryFormatter formatter = new BinaryFormatter();

		public ObjectSerializationSurrogate(Type t) : base(t) { }

		/// <summary>
		/// Uses a <see cref="BinaryFormatter"/> to read an object of 
		/// type <see cref="ActualType"/> from the underlying stream.
		/// </summary>
		/// <param name="reader">stream reader</param>
		/// <returns>object read from the stream reader</returns>
		public override object Read(CompactBinaryReader reader)
		{
			object result = formatter.Deserialize(reader.BaseStream);
			if (result != null)
			{
				Serializer.AlertLegacySerialization(result.GetType());
			}
			return result;
		}

		/// <summary>
		/// Uses a <see cref="BinaryFormatter"/> to write an object of 
		/// type <see cref="ActualType"/> to the underlying stream
		/// </summary>
		/// <param name="writer">stream writer</param>
		/// <param name="graph">object to be written to the stream reader</param>
		public override void Write(CompactBinaryWriter writer, object graph)
		{
			if (graph != null)
			{
				Serializer.AlertLegacySerialization(graph.GetType());
			}
			formatter.Serialize(writer.BaseWriter.BaseStream, graph);
		}
	}


	/// <summary>
	/// Surrogate for <see cref="System.object[]"/> type.
	/// </summary>
	sealed class ObjectArraySerializationSurrogate : SerializationSurrogate
	{
		public ObjectArraySerializationSurrogate() : base(typeof(object[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			object[] array = SafeMemoryAllocator.CreateArray<object>(length);
			for (int i = 0; i < length; i++) array[i] = reader.Read();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			object[] array = (object[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	#endregion

	#region /       .NET Primitive Types' surrogates       /

	/// <summary>
	/// Surrogate for <see cref="System.Boolean"/> type.
	/// </summary>
	sealed class BooleanSerializationSurrogate : SerializationSurrogate
	{
		public BooleanSerializationSurrogate() : base(typeof(Boolean)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadBoolean(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((Boolean)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Boolean[]"/> type.
	/// </summary>
	sealed class BooleanArraySerializationSurrogate : SerializationSurrogate
	{
		public BooleanArraySerializationSurrogate() : base(typeof(Boolean[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			Boolean[] array = SafeMemoryAllocator.CreateArray<Boolean>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadBoolean();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Boolean[] array = (Boolean[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.Byte"/> type.
	/// </summary>
	sealed class ByteSerializationSurrogate : SerializationSurrogate
	{
		public ByteSerializationSurrogate() : base(typeof(Byte)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadByte(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((Byte)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Byte[]"/> type.
	/// </summary>
	sealed class ByteArraySerializationSurrogate : SerializationSurrogate
	{
		public ByteArraySerializationSurrogate() : base(typeof(Byte[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			return reader.ReadBytes(length);
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Byte[] array = (Byte[])graph;
			writer.Write(array.Length);
			writer.Write(array);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.Char"/> type.
	/// </summary>
	sealed class CharSerializationSurrogate : SerializationSurrogate
	{
		public CharSerializationSurrogate() : base(typeof(Char)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadChar(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((Char)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Char[]"/> type.
	/// </summary>
	sealed class CharArraySerializationSurrogate : SerializationSurrogate
	{
		public CharArraySerializationSurrogate() : base(typeof(Char[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			return reader.ReadChars(length);
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Char[] array = (Char[])graph;
			writer.Write(array.Length);
			writer.Write(array);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.Single"/> type.
	/// </summary>
	sealed class SingleSerializationSurrogate : SerializationSurrogate
	{
		public SingleSerializationSurrogate() : base(typeof(Single)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadSingle(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((Single)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Single[]"/> type.
	/// </summary>
	sealed class SingleArraySerializationSurrogate : SerializationSurrogate
	{
		public SingleArraySerializationSurrogate() : base(typeof(Single[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			Single[] array = SafeMemoryAllocator.CreateArray<Single>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadSingle();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Single[] array = (Single[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.Double"/> type.
	/// </summary>
	sealed class DoubleSerializationSurrogate : SerializationSurrogate
	{
		public DoubleSerializationSurrogate() : base(typeof(Double)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadDouble(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((Double)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Double[]"/> type.
	/// </summary>
	sealed class DoubleArraySerializationSurrogate : SerializationSurrogate
	{
		public DoubleArraySerializationSurrogate() : base(typeof(Double[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			Double[] array = SafeMemoryAllocator.CreateArray<Double>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadDouble();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Double[] array = (Double[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.Decimal"/> type.
	/// </summary>
	sealed class DecimalSerializationSurrogate : SerializationSurrogate
	{
		public DecimalSerializationSurrogate() : base(typeof(Decimal)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadDecimal(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((Decimal)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Decimal[]"/> type.
	/// </summary>
	sealed class DecimalArraySerializationSurrogate : SerializationSurrogate
	{
		public DecimalArraySerializationSurrogate() : base(typeof(Decimal)) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			Decimal[] array = SafeMemoryAllocator.CreateArray<Decimal>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadDecimal();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Decimal[] array = (Decimal[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.Int16"/> type.
	/// </summary>
	sealed class Int16SerializationSurrogate : SerializationSurrogate
	{
		public Int16SerializationSurrogate() : base(typeof(Int16)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadInt16(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((Int16)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Int16[]"/> type.
	/// </summary>
	sealed class Int16ArraySerializationSurrogate : SerializationSurrogate
	{
		public Int16ArraySerializationSurrogate() : base(typeof(Int16[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			Int16[] array = SafeMemoryAllocator.CreateArray<Int16>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadInt16();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Int16[] array = (Int16[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.Int32"/> type.
	/// </summary>
	sealed class Int32SerializationSurrogate : SerializationSurrogate
	{
		public Int32SerializationSurrogate() : base(typeof(Int32)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadInt32(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((int)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Int32[]"/> type.
	/// </summary>
	sealed class Int32ArraySerializationSurrogate : SerializationSurrogate
	{
		public Int32ArraySerializationSurrogate() : base(typeof(Int32[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			Int32[] array = SafeMemoryAllocator.CreateArray<Int32>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadInt32();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Int32[] array = (Int32[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.Int64"/> type.
	/// </summary>
	sealed class Int64SerializationSurrogate : SerializationSurrogate
	{
		public Int64SerializationSurrogate() : base(typeof(Int64)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadInt64(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((Int64)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.Int64[]"/> type.
	/// </summary>
	sealed class Int64ArraySerializationSurrogate : SerializationSurrogate
	{
		public Int64ArraySerializationSurrogate() : base(typeof(Int64)) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			Int64[] array = SafeMemoryAllocator.CreateArray<Int64>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadInt64();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Int64[] array = (Int64[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.DateTime"/> type.
	/// </summary>
	sealed class DateTimeSerializationSurrogate : SerializationSurrogate
	{
		public DateTimeSerializationSurrogate() : base(typeof(DateTime)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadDateTime(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((DateTime)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.DateTime[]"/> type.
	/// </summary>
	sealed class DateTimeArraySerializationSurrogate : SerializationSurrogate
	{
		public DateTimeArraySerializationSurrogate() : base(typeof(DateTime[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			DateTime[] array = SafeMemoryAllocator.CreateArray<DateTime>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadDateTime();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			DateTime[] array = (DateTime[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.String"/> type.
	/// </summary>
	sealed class StringSerializationSurrogate : SerializationSurrogate
	{
		public StringSerializationSurrogate() : base(typeof(String)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadString(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((string)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.String[]"/> type.
	/// </summary>
	sealed class StringArraySerializationSurrogate : SerializationSurrogate
	{
		public StringArraySerializationSurrogate() : base(typeof(String[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			String[] array = SafeMemoryAllocator.CreateArray<String>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadString();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			String[] array = (String[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.SByte"/> type.
	/// </summary>
	sealed class SByteSerializationSurrogate : SerializationSurrogate
	{
		public SByteSerializationSurrogate() : base(typeof(SByte)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadSByte(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((SByte)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.SByte[]"/> type.
	/// </summary>
	sealed class SByteArraySerializationSurrogate : SerializationSurrogate
	{
		public SByteArraySerializationSurrogate() : base(typeof(SByte[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			SByte[] array = SafeMemoryAllocator.CreateArray<SByte>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadSByte();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			SByte[] array = (SByte[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.UInt16"/> type.
	/// </summary>
	sealed class UInt16SerializationSurrogate : SerializationSurrogate
	{
		public UInt16SerializationSurrogate() : base(typeof(UInt16)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadUInt16(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((UInt16)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.UInt16[]"/> type.
	/// </summary>
	sealed class UInt16ArraySerializationSurrogate : SerializationSurrogate
	{
		public UInt16ArraySerializationSurrogate() : base(typeof(UInt16[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			UInt16[] array = SafeMemoryAllocator.CreateArray<UInt16>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadUInt16();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			UInt16[] array = (UInt16[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.UInt32"/> type.
	/// </summary>
	sealed class UInt32SerializationSurrogate : SerializationSurrogate
	{
		public UInt32SerializationSurrogate() : base(typeof(UInt32)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadUInt32(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((UInt32)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.UInt32[]"/> type.
	/// </summary>
	sealed class UInt32ArraySerializationSurrogate : SerializationSurrogate
	{
		public UInt32ArraySerializationSurrogate() : base(typeof(UInt32[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			UInt32[] array = SafeMemoryAllocator.CreateArray<UInt32>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadUInt32();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			UInt32[] array = (UInt32[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	/// <summary>
	/// Surrogate for <see cref="System.UInt64"/> type.
	/// </summary>
	sealed class UInt64SerializationSurrogate : SerializationSurrogate
	{
		public UInt64SerializationSurrogate() : base(typeof(UInt64)) { }
		public override object Read(CompactBinaryReader reader) { return reader.ReadUInt64(); }
		public override void Write(CompactBinaryWriter writer, object graph) { writer.Write((UInt64)graph); }
	}

	/// <summary>
	/// Surrogate for <see cref="System.UInt64[]"/> type.
	/// </summary>
	sealed class UInt64ArraySerializationSurrogate : SerializationSurrogate
	{
		public UInt64ArraySerializationSurrogate() : base(typeof(UInt64[])) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			UInt64[] array = SafeMemoryAllocator.CreateArray<UInt64>(length);
			for (int i = 0; i < length; i++) array[i] = reader.ReadUInt64();
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			UInt64[] array = (UInt64[])graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array[i]);
		}
	}

	#endregion

	#region /       Generic surrogates for containers      /

	/// <summary>
	/// Surrogate for generic <see cref="System.Array"/> types.
	/// </summary>
	sealed class ArraySerializationSurrogate : SerializationSurrogate
	{
		public ArraySerializationSurrogate(Type t) : base(t) { }

		public Array CreateInstance(int len)
		{
			return Array.CreateInstance(ActualType.GetElementType(), len);
		}

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			Array array = (Array)CreateInstance(length);
			for (int i = 0; i < length; i++)
				array.SetValue(reader.Read(), i);
			return array;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			Array array = (Array)graph;
			writer.Write(array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write(array.GetValue(i));
		}
	}

	/// <summary>
	/// Surrogate for types that inherit from <see cref="System.IList"/>.
	/// </summary>
	sealed class IListSerializationSurrogate : SerializationSurrogate
	{
		public IListSerializationSurrogate(Type t) : base(t) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			IList list = (IList)CreateInstance();
			for (int i = 0; i < length; i++)
				list.Add(reader.Read());
			return list;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			IList list = (IList)graph;
			writer.Write(list.Count);
			for (int i = 0; i < list.Count; i++)
				writer.Write(list[i]);
		}
	}

	/// <summary>
	/// Surrogate for types that inherit from <see cref="System.IDictionary"/>.
	/// </summary>
	sealed class IDictionarySerializationSurrogate : SerializationSurrogate
	{
		public IDictionarySerializationSurrogate(Type t) : base(t) { }

		public override object Read(CompactBinaryReader reader)
		{
			int length = reader.ReadInt32();
			IDictionary dict = (IDictionary)CreateInstance();
			for (int i = 0; i < length; i++)
			{
				object key = reader.Read();
				object value = reader.Read();
				dict.Add(key, value);
			}
			return dict;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			IDictionary dict = (IDictionary)graph;
			writer.Write(dict.Count);
			for (IDictionaryEnumerator i = dict.GetEnumerator(); i.MoveNext(); )
			{
				writer.Write(i.Key);
				writer.Write(i.Value);
			}
		}
	}


	#endregion

	/// <summary>
	/// Surrogate for types that inherit from <see cref="ICompactSerializable"/>.
	/// </summary>
	sealed class ICompactSerializableSerializationSurrogate : SerializationSurrogate
	{
		public ICompactSerializableSerializationSurrogate(Type t) : base(t) { }

		/// <summary>
		/// Non default object construction. The idea is to circumvent constructor calls
		/// and populate the object in <see cref="ICompactSerializable.Deserialize"/> method.
		/// </summary>
		/// <returns></returns>
		public override object CreateInstance()
		{
			return FormatterServices.GetUninitializedObject(ActualType);
		}

		public override object Read(CompactBinaryReader reader)
		{
			ICompactSerializable custom = (ICompactSerializable)CreateInstance();
			custom.Deserialize(reader);
			return custom;
		}

		public override void Write(CompactBinaryWriter writer, object graph)
		{
			((ICompactSerializable)graph).Serialize(writer);
		}
	}

}
