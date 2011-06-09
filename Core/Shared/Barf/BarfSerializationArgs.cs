using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Encapsulates arguments needed by <see cref="IBarfSerializer{T}.Serialize(T,BarfSerializationArgs)"/> and <see cref="IBarfSerializer.Serialize(object,BarfSerializationArgs)"/>.
	/// </summary>
	public class BarfSerializationArgs
	{
		public struct TypeContext
		{
			internal long HeaderPosition { get; set; }
			internal byte[] FutureData { get; set; }
		}

		private BarfTypeTable _typeTable;
		private BinaryFormatter _binaryFormatter;
		private long _streamHeaderPosition = -1L;
		private int _recursionCount;

		internal BarfSerializationArgs(IPrimitiveWriter writer)
		{
			Writer = writer;
		}

		public void WriteNullObject()
		{
			EnsureOpenStreamHeader();
			BarfObjectHeader.WriteNull(Writer);
			EnsureCloseStreamHeader();
		}

		private void EnsureOpenStreamHeader()
		{
			if (_streamHeaderPosition == -1L)
			{
				Writer.Write(SerializerHeaders.Barf);
				_streamHeaderPosition = BarfStreamHeader.BeginWrite(Writer, BarfFormatter.CurrentFrameworkVersion);
			}
		}

		private void EnsureCloseStreamHeader()
		{
			if (_recursionCount == 0)
			{
				var flags = HeaderFlags.None;

				if (_typeTable != null && _typeTable.Count > 0)
				{
					flags |= HeaderFlags.HasNameTable;
				}

				BarfStreamHeader.EndWrite(Writer, _streamHeaderPosition, flags);

				if ((flags & HeaderFlags.HasNameTable) == HeaderFlags.HasNameTable)
				{
					_typeTable.WriteTo(Writer);
				}
				_streamHeaderPosition = -1L;
				_typeTable = null;
			}
		}

		private long BeginObject(int version, int minVersion)
		{
			EnsureOpenStreamHeader();

			var headerPosition = BarfObjectHeader.BeginWrite(Writer, version, minVersion);

			++_recursionCount;

			return headerPosition;
		}

		public long BeginObject<T>()
		{
			var def = BarfTypeDefinition.Get<T>(true);

			return BeginObject(def.CurrentVersion, def.MinVersion);
		}

		public TypeContext BeginObject<T>(T instance)
			where T : ISerializationInfo
		{
			var info = instance.SerializationInfo as BarfSerializationInfo;

			if (info == null)
			{
				return new TypeContext
				{
					HeaderPosition = BeginObject<T>(),
					FutureData = null
				};
			}

			BarfSerializationTypeInfo typeInfo;
			if (!info.TryGet(typeof(T), out typeInfo))
			{
				return new TypeContext
				{
					HeaderPosition = BeginObject<T>(),
					FutureData = null
				};
			}

			return new TypeContext
			{
				HeaderPosition = BeginObject(typeInfo.OriginalHeader.Version, typeInfo.OriginalHeader.MinVersion),
				FutureData = typeInfo.FutureData
			};
		}

		public void EndObject(TypeContext context)
		{
			if (context.FutureData != null)
			{
				Writer.BaseStream.Write(context.FutureData, 0, context.FutureData.Length);
			}

			EndObject(context.HeaderPosition);
		}

		public void EndObject(long headerPosition)
		{
			--_recursionCount;

			BarfObjectHeader.EndWrite(Writer, headerPosition);

			EnsureCloseStreamHeader();
		}

		/// <summary>
		/// Gets the writer to write binary object data to.
		/// </summary>
		/// <value>The writer to write binary object data to.</value>
		public IPrimitiveWriter Writer { get; private set; }

		/// <summary>
		/// Gets a type table containing type information that can be serialized.
		/// </summary>
		/// <value>The type table containing type information that can be serialized.</value>
		public BarfTypeTable TypeTable
		{
			get
			{
				if (_typeTable == null)
				{
					_typeTable = new BarfTypeTable();
				}

				return _typeTable;
			}
		}

		/// <summary>
		/// Gets the binary formatter to use on objects that are otherwise un-supported by the barf framework.
		/// </summary>
		/// <value>The binary formatter.</value>
		public BinaryFormatter BinaryFormatter
		{
			get
			{
				if (_binaryFormatter == null)
				{
					_binaryFormatter = new BinaryFormatter();
				}

				return _binaryFormatter;
			}
		}
	}
}
