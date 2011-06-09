using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	/// <summary>
	///	A container used for serializing type names into a <see cref="Stream"/> instance.
	/// </summary>
	public class BarfTypeTable
	{
		private static readonly Factory<string, Type> _typesByName = Algorithm.LazyIndexer<string, Type>(name => Type.GetType(name, true));

		private static readonly Factory<Type, string> _reducedFullNames = Algorithm.LazyIndexer<Type, string>(type =>
		{
			if (!type.IsGenericType)
			{
				Debug.Fail("Non generic types shouldn't get here.");
				return type.FullName;
			}

			var typeParams = type.GetGenericArguments();

			var output = new StringBuilder(type.Namespace);
			output.Append('.');
			output.Append(type.Name);
			output.Append('[');
			for (int i = 0; i < typeParams.Length; ++i)
			{
				if (i > 0)
				{
					output.Append(',');
				}
				output.Append('[');
				output.Append(GetReducedFullName(typeParams[i]));
				output.Append(',');
				output.Append(typeParams[i].Assembly.GetName().Name);
				output.Append("]");
			}
			output.Append("]");
			return output.ToString();
		});

		private static string GetReducedFullName(Type type)
		{
			if (!type.IsGenericType) return type.FullName;

			return _reducedFullNames(type);
		}

		private static readonly UTF8Encoding _encoding = new UTF8Encoding(false, true);

		private static void WriteString(IPrimitiveWriter writer, string value)
		{
			if (value == null)
			{
				writer.WriteVarInt32(-1);
			}
			else if (value.Length == 0)
			{
				writer.WriteVarInt32(0);
			}
			else
			{
				var bytes = _encoding.GetBytes(value);
				writer.WriteVarInt32(bytes.Length);
				writer.BaseStream.Write(bytes, 0, bytes.Length);
			}
		}

		private static string ReadString(IPrimitiveReader reader)
		{
			int length = reader.ReadVarInt32();

			if (length < -1)
			{
				throw new InvalidDataException();
			}

			if (length == -1)
			{
				return null;
			}
			if (length == 0)
			{
				return string.Empty;
			}

			var ms = reader.BaseStream as MemoryStream;
			if (ms != null && ms.IsPubliclyVisible())
			{
				var buffer = ms.GetBuffer();
				var result = _encoding.GetString(buffer, (int)ms.Position, length);
				ms.Seek(length, SeekOrigin.Current);
				return result;
			}

			var bytes = SafeMemoryAllocator.CreateArray<byte>(length);
			reader.BaseStream.Read(bytes, 0, length);
			return _encoding.GetString(bytes);
		}

		private static void WriteSegments(
			IPrimitiveWriter writer,
			IEnumerable<string> items,
			char segmentDelimiter)
		{
			int nextSegmentId = 1;
			var itemReferences = new List<byte[]>();
			var idsBySegment = new Dictionary<string,int>();
			var orderedSegments = new List<string>();
			var splitter = new [] { segmentDelimiter };
			var stream = new MemoryStream();

			foreach (var item in items)
			{
				var segments = item.Split(splitter, StringSplitOptions.None);
				for (int i = 0; i < segments.Length; ++i)
				{
					int id;
					if (!idsBySegment.TryGetValue(segments[i], out id))
					{
						id = nextSegmentId++;
						idsBySegment.Add(segments[i], id);
						orderedSegments.Add(segments[i]);
					}
					if (i < segments.Length - 1)
					{
						stream.WriteVarInt32(id);
					}
					else
					{
						// the last one is negative so we know when to stop
						stream.WriteVarInt32(-id);
					}
				}
				itemReferences.Add(stream.ToArray());
				stream.SetLength(0);
			}

			writer.WriteVarInt32(orderedSegments.Count);
			for (int i = 0; i < orderedSegments.Count; ++i)
			{
				WriteString(writer, orderedSegments[i]);
			}

			writer.WriteVarInt32(itemReferences.Count);
			for (int i = 0; i < itemReferences.Count; ++i)
			{
				writer.BaseStream.Write(itemReferences[i], 0, itemReferences[i].Length);
			}
		}

		private static IEnumerable<string> ReadSegments(
			IPrimitiveReader reader,
			char segmentDelimiter)
		{
			int count = reader.ReadVarInt32();

			if (count == 0)
			{
				yield break;
			}

			var segments = SafeMemoryAllocator.CreateList<string>(count);
			for (; count > 0; --count)
			{
				segments.Add(ReadString(reader));
			}

			var builder = new StringBuilder();
			count = reader.ReadVarInt32();
			for (; count > 0; --count)
			{
				while(true)
				{
					int segmentId = reader.ReadVarInt32();
					if (segmentId < 0)
					{
						builder.Append(segments[-segmentId - 1]);
						break;
					}
					builder.Append(segments[segmentId - 1]);
					builder.Append(segmentDelimiter);
				}
				yield return builder.ToString();
				builder.Length = 0;
			}
		}

		/// <summary>
		/// Reads a <see cref="BarfTypeTable"/> instance from the specified <see cref="IPrimitiveReader"/>.
		/// </summary>
		/// <param name="reader">The reader to read from.</param>
		/// <returns>
		/// The deserialized <see cref="BarfTypeTable"/> instance.
		/// </returns>
		public static BarfTypeTable ReadFrom(IPrimitiveReader reader)
		{
			var assemblyNames = new List<string>(ReadSegments(reader, '.'));

			if (assemblyNames.Count == 0) return new BarfTypeTable(new List<TypeData>());

			var typeNames = new List<string>(ReadSegments(reader, '.'));

			var typeData = new List<TypeData>(typeNames.Count);

			for (int i = 0; i < typeNames.Count; ++i)
			{
				int assemblyIndex = reader.ReadVarInt32();
				if (assemblyIndex < 0 || assemblyIndex >= assemblyNames.Count)
				{
					throw new InvalidDataException();
				}

				typeData.Add(new TypeData(typeNames[i], assemblyNames[assemblyIndex]));
			}

			return new BarfTypeTable(typeData);
		}

		private readonly List<TypeData> _types;
		private Dictionary<Type, int> _indexesByType;

		internal BarfTypeTable()
		{
			_types = new List<TypeData>();
		}

		private BarfTypeTable(List<TypeData> types)
		{
			_types = types;
		}

		public void WriteTo(IPrimitiveWriter writer)
		{
			if (_types.Count == 0)
			{
				writer.WriteVarInt32(0);
				return;
			}

			int nextAssemblyId = 0;
			var orderedAssemblyNames = new List<string>();
			var indexedAssemblyNames = new Dictionary<string, int>();
			var typesToWrite = new string [_types.Count];
			var assemblyReferencesToWrite = new int [_types.Count];

			for (int i = 0; i < _types.Count; ++i)
			{
				int id;
				if (!indexedAssemblyNames.TryGetValue(_types[i].AssemblyName, out id))
				{
					id = nextAssemblyId++;
					orderedAssemblyNames.Add(_types[i].AssemblyName);
					indexedAssemblyNames.Add(_types[i].AssemblyName, id);
				}
				typesToWrite[i] = _types[i].FullTypeName;
				assemblyReferencesToWrite[i] = id;
			}

			WriteSegments(writer, orderedAssemblyNames, '.');
			WriteSegments(writer, typesToWrite, '.');

			for (int i = 0; i < assemblyReferencesToWrite.Length; ++i)
			{
				writer.WriteVarInt32(assemblyReferencesToWrite[i]);
			}
		}

		public int Include(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			int index;

			if (_indexesByType == null)
			{
				_indexesByType = new Dictionary<Type, int>();
				for (int i = 0; i < _types.Count; ++i)
				{
					_indexesByType.Add(_types[i].Type, i);
				}
			}

			if (_indexesByType.TryGetValue(type, out index))
			{
				return index;
			}

			index = _types.Count;
			_types.Add(new TypeData(type));
			_indexesByType.Add(type, index);
			return index;
		}

		public Type GetType(int index)
		{
			if (index < 0 || index >= _types.Count)
			{
				throw new ArgumentOutOfRangeException("index", string.Format("TypeIndex=\"{0}\" is outside the range of 0-{1}", index, _types.Count));
			}

			return _types[index].Type;
		}

		public object CreateNew(int index)
		{
			if (index < 0 || index >= _types.Count)
			{
				throw new ArgumentOutOfRangeException("index", string.Format("TypeIndex=\"{0}\" is outside the range of 0-{1}", index, _types.Count));
			}

			var info = _types[index];

			return info.Ctor();
		}

		public int Count
		{
			[DebuggerStepThrough]
			get { return _types.Count; }
		}

		private class TypeData
		{
			private string _fullTypeName;
			private string _assemblyName;
			private Type _type;
			private Factory<object> _ctor;

			public TypeData(string fullTypeName, string assemblyName)
			{
				_fullTypeName = fullTypeName;
				_assemblyName = assemblyName;
			}

			public TypeData(Type type)
			{
				_type = type;
			}

			public string FullTypeName
			{
				get
				{
					if (_fullTypeName == null)
					{
						_fullTypeName = GetReducedFullName(Type);
					}
					return _fullTypeName;
				}
			}

			public string AssemblyName
			{
				get
				{
					if (_assemblyName == null)
					{
						_assemblyName = Type.Assembly.GetName().Name;
					}
					return _assemblyName;
				}
			}

			public Type Type
			{
				get
				{
					if (_type == null && _fullTypeName != null)
					{
						_type = _typesByName(_fullTypeName + "," + _assemblyName);
					}
					return _type;
				}
			}

			public Factory<object> Ctor
			{
				get
				{
					if (_ctor == null)
					{
						_ctor = DynamicMethods.GetCtor<object>(Type);
					}
					return _ctor;
				}
			}
		}
	}
}
