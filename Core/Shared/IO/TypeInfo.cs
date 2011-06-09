using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MySpace.Common.CompactSerialization.IO;
using MySpace.Logging;

namespace MySpace.Common.IO
{
	/// <summary>
	/// 	<para>Provides a global type id definition.</para>
	/// </summary>
	public sealed class TypeInfo
	{
		private static readonly LogWrapper _log = new LogWrapper();
		private static readonly short _minId;
		private static readonly TypeInfo[] _typeInfoById = new TypeInfo[0];
		private static readonly Factory<Type, TypeInfo> _typeInfoByType;
		private static readonly Factory<string, TypeInfo> _typeInfoByTypeName;

		private static IEnumerable<KeyValuePair<Type, bool>> GetPresets()
		{
			// simple system value types
			yield return new KeyValuePair<Type, bool>(typeof(Byte), true);
			yield return new KeyValuePair<Type, bool>(typeof(SByte), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int16), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt16), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int32), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt32), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int64), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt64), true);
			yield return new KeyValuePair<Type, bool>(typeof(Single), true);
			yield return new KeyValuePair<Type, bool>(typeof(Double), true);
			yield return new KeyValuePair<Type, bool>(typeof(Char), true);
			yield return new KeyValuePair<Type, bool>(typeof(Boolean), true);
			yield return new KeyValuePair<Type, bool>(typeof(Decimal), true);
			yield return new KeyValuePair<Type, bool>(typeof(Guid), true);
			yield return new KeyValuePair<Type, bool>(typeof(DateTime), true);
			yield return new KeyValuePair<Type, bool>(typeof(TimeSpan), true);
			yield return new KeyValuePair<Type, bool>(typeof(IntPtr), true);
			yield return new KeyValuePair<Type, bool>(typeof(UIntPtr), true);

			// simple nullable system value types
			yield return new KeyValuePair<Type, bool>(typeof(Byte?), true);
			yield return new KeyValuePair<Type, bool>(typeof(SByte?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int16?), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt16?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int32?), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt32?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int64?), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt64?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Single?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Double?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Char?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Boolean?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Decimal?), true);
			yield return new KeyValuePair<Type, bool>(typeof(Guid?), true);
			yield return new KeyValuePair<Type, bool>(typeof(DateTime?), true);
			yield return new KeyValuePair<Type, bool>(typeof(TimeSpan?), true);
			yield return new KeyValuePair<Type, bool>(typeof(IntPtr?), true);
			yield return new KeyValuePair<Type, bool>(typeof(UIntPtr?), true);

			// simple system reference types
			yield return new KeyValuePair<Type, bool>(typeof(String), true);
			yield return new KeyValuePair<Type, bool>(typeof(BitArray), true);
			yield return new KeyValuePair<Type, bool>(typeof(DBNull), true);

			// arrays of system value types
			yield return new KeyValuePair<Type, bool>(typeof(Byte[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(SByte[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int16[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt16[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int32[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt32[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Int64[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(UInt64[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Single[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Double[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Char[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Boolean[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Decimal[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(Guid[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(DateTime[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(TimeSpan[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(IntPtr[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(UIntPtr[]), true);

			// arrays of system reference types
			yield return new KeyValuePair<Type, bool>(typeof(String[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(BitArray[]), true);
			yield return new KeyValuePair<Type, bool>(typeof(DBNull[]), true);

			// append new presets here, well established should be
			// false until all code that uses this class has the latest version
		}

		static TypeInfo()
		{
			using (new BenchmarkedRegion("Static constructor"))
			{
				var typeInfoById = new Dictionary<short, TypeInfo>();
				var typeInfoByType = new Dictionary<Type, TypeInfo>();

				foreach (var preset in GetPresets())
				{
					var info = new TypeInfo(preset.Key, --_minId, preset.Value);
					typeInfoById.Add(info.Id, info);
					typeInfoByType.Add(info.Type, info);
				}

				var config = (TypeMappingConfig)ConfigurationManager.GetSection(TypeMappingConfig.SectionName);

				int maxId = -1;
				if (config == null)
				{
					_log.ErrorFormat("Missing configuration section: '{0}'", TypeMappingConfig.SectionName);
				}
				else
				{
					foreach (var typeInfoConfig in config.Types)
					{
						if (string.IsNullOrEmpty(typeInfoConfig.Name))
						{
							_log.ErrorFormat("'name' not defined for type with id: {0}", typeInfoConfig.Id);
							continue;
						}

						Type type;
						try
						{
							type = Type.GetType(typeInfoConfig.Name, false);
						}
						catch (Exception ex)
						{
							_log.Error(string.Format("Failed to resolve type id {0}", typeInfoConfig.Id), ex);
							continue;
						}

						if (type == null)
						{
							_log.WarnFormat("Couldn't resolve type name {0} for type id {1}", typeInfoConfig.Name, typeInfoConfig.Id);
							continue;
						}

						var typeInfo = new TypeInfo(type, typeInfoConfig.Id, typeInfoConfig.WellEstablished);

						if (typeInfoById.ContainsKey(typeInfoConfig.Id))
						{
							_log.ErrorFormat("A type with id {0} is already defined. This type will be ignored.", typeInfoConfig.Id);
							continue;
						}

						if (typeInfoByType.ContainsKey(type))
						{
							_log.ErrorFormat("An id, {0}, is already defined for type {1}; type id {2} will be skipped.", typeInfoByType[type].Id, type, typeInfoConfig.Id);
							continue;
						}

						typeInfoById[typeInfo.Id] = typeInfo;
						typeInfoByType[type] = typeInfo;

						if (maxId < typeInfo.Id) maxId = typeInfo.Id;
					}
				}
				_typeInfoById = new TypeInfo[maxId + 1 - _minId];
				foreach (var pair in typeInfoById)
				{
					_typeInfoById[pair.Key - _minId] = pair.Value;
				}

				_typeInfoByType = Algorithm.LazyIndexer<Type, TypeInfo>(type =>
				{
					TypeInfo result;
					if (typeInfoByType.TryGetValue(type, out result)) return result;
					return new TypeInfo(type);
				});

				_typeInfoByTypeName = Algorithm.LazyIndexer<string, TypeInfo>(typeName =>
					_typeInfoByType(Type.GetType(typeName, true)));

				ThreadPool.QueueUserWorkItem(LogTypeIdGaps, _typeInfoById);
			}
		}

		private static void LogTypeIdGaps(object state)
		{
			var typeInfoById = (TypeInfo[])state;

			int? startGap = null;
			for (int i = 0; i < typeInfoById.Length; ++i)
			{
				if (typeInfoById[i] == null)
				{
					if (!startGap.HasValue) startGap = i;
				}
				else if (startGap.HasValue)
				{
					_log.WarnFormat(
						"Type id's between {0} to {1} are unused. This gap wastes memory; consider re-factoring the '{2}' config secion, if possible, or adding new type mappings to this range.",
						startGap.Value + _minId,
						i + _minId,
						TypeMappingConfig.SectionName);
				}
			}
		}

		/// <summary>
		/// 	<para>Gets the <see cref="TypeInfo"/> for the specified type, <typeparamref name="T"/>.</para>
		/// </summary>
		/// <typeparam name="T">The type to get the <see cref="TypeInfo"/> for.</typeparam>
		/// <returns>
		///	<para>The <see cref="TypeInfo"/> for the specified type, <typeparamref name="T"/>.
		///	<see langword="null"/> if no type info is defined for <typeparamref name="T"/>
		///	in the <see cref="TypeMappingConfig"/> section.</para>
		/// </returns>
		/// <exception cref="NotSupportedException">
		///	<para>No valid <see cref="TypeInfo" /> was found for the specified type.</para>
		/// </exception>
		public static TypeInfo Get<T>()
		{
			return TypeSpecific<T>.TypeInfo;
		}

		/// <summary>
		/// 	<para>Gets the <see cref="TypeInfo"/> for the specified type.</para>
		/// </summary>
		/// <param name="type">The type to get the <see cref="TypeInfo"/> for.</param>
		/// <returns>
		///	<para>The <see cref="TypeInfo"/> for the specified type.
		///	<see langword="null"/> if no type info is defined for <paramref name="type"/>
		///	in the <see cref="TypeMappingConfig"/> section.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		public static TypeInfo Get(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			return _typeInfoByType(type);
		}

		/// <summary>
		/// 	<para>Gets the <see cref="TypeInfo"/> for the specified type.</para>
		/// </summary>
		/// <param name="typeId">The type id to get the <see cref="TypeInfo"/> for.</param>
		/// <returns>
		///	<para>The <see cref="TypeInfo"/> for the specified type.
		///	<see langword="null"/> if no type info is defined for the specified type id
		///	in the <see cref="TypeMappingConfig"/> section.</para>
		/// </returns>
		/// <exception cref="NotSupportedException">
		///	<para>No valid <see cref="TypeInfo" /> was found for the specified type id.</para>
		/// </exception>
		public static TypeInfo Get(short typeId)
		{
			return Get(typeId, true);
		}

		/// <summary>
		/// 	<para>Gets the <see cref="TypeInfo"/> for the specified type.</para>
		/// </summary>
		/// <param name="typeId">The type id to get the <see cref="TypeInfo"/> for.</param>
		/// <param name="throwOnError">
		///	<para>Specify <see langword="true"/> to throw if the <see cref="TypeInfo"/> could not be obtained;
		///	otherwise specify <see langword="false"/>.</para>
		/// </param>
		/// <returns>
		///	<para>The <see cref="TypeInfo"/> for the specified type.
		///	<see langword="null"/> if no type info is defined for the specified type id
		///	in the <see cref="TypeMappingConfig"/> section.</para>
		/// </returns>
		/// <exception cref="NotSupportedException">
		///	<para>No valid <see cref="TypeInfo" /> was found for the specified type id.</para>
		/// </exception>
		public static TypeInfo Get(short typeId, bool throwOnError)
		{
			TypeInfo result = null;

			int index = typeId - _minId;
			if (index < _typeInfoById.Length)
			{
				result = _typeInfoById[index];
			}

			if (result == null && throwOnError)
			{
				throw new NotSupportedException(string.Format("No TypeInfo found for type id '{0}'.", typeId));
			}

			return result;
		}

		private TypeInfo(Type type)
		{
			_type = type;
			_id = null;
			_wellEstablished = false;
		}

		private TypeInfo(Type type, short id, bool wellEstablished)
		{
			_type = type;
			_id = id;
			_wellEstablished = wellEstablished;
		}

		private readonly Type _type;
		private readonly short? _id;
		private readonly bool _wellEstablished;
		private byte[] _headerBytes;

		/// <summary>
		/// 	<para>Gets unsigned type id for the type.</para>
		/// </summary>
		/// <value>
		/// 	<para>The unsigned type id for the type.</para>
		/// </value>
		/// <exception cref="InvalidOperationException">
		///	<para>No type id is defined for this type.</para>
		/// </exception>
		public short Id
		{
			get
			{
				if (!_id.HasValue)
				{
					throw new InvalidOperationException(string.Format("No type id is defined in config section '{0}' for type {1}", TypeMappingConfig.SectionName, _type));
				}
				return _id.Value;
			}
		}

		/// <summary>
		/// 	<para>Gets a value indicating whether this instance has a type id defined.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if this instance has a type id; otherwise, <see langword="false"/>.</para>
		/// </value>
		public bool HasId
		{
			get { return _id.HasValue; }
		}

		/// <summary>
		/// 	<para>Gets or sets a value indicating whether or not this config entry has been
		/// 	well established across all servers in the system. If the entry is not well established
		/// 	the serialization method will use the assembly qualified type name instead of the type
		/// 	id which can be resolved to a real type whether or not the type mapping entry is
		/// 	defined on the remote server.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> to indicate that the entry is well established across
		/// 	all servers in the system; <see langword="false"/> otherwise.</para>
		/// </value>
		public bool WellEstablished
		{
			get { return _wellEstablished; }
		}

		/// <summary>
		/// 	<para>Gets the type.</para>
		/// </summary>
		/// <value>
		/// 	<para>The type.</para>
		/// </value>
		public Type Type
		{
			get { return _type; }
		}

		/// <summary>
		/// Writes this instance to the specified writer so that it can be later read by <see cref="ReadFrom"/>.
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		public void WriteTo(IPrimitiveWriter writer)
		{
			if (_headerBytes == null)
			{
				var header = new MemoryStream();
				var headerWriter = new CompactBinaryWriter(new BinaryWriter(header));
				if (_id.HasValue && _wellEstablished)
				{
					headerWriter.Write((byte)Mode.Id);
					headerWriter.Write(_id.Value);
				}
				else
				{
					headerWriter.Write((byte)Mode.Name);
					string typeName = string.Format("{0},{1}", _type.FullName, _type.Assembly.GetName().Name);
					headerWriter.Write(typeName);
				}

				headerWriter.BaseStream.Flush();
				_headerBytes = header.ToArray();
			}
			writer.BaseStream.Write(_headerBytes, 0, _headerBytes.Length);
		}

		/// <summary>
		/// Reads a <see cref="TypeInfo"/> object from the specified reader.
		/// </summary>
		/// <param name="reader">The reader to read from.</param>
		/// <returns>
		///	<para>The <see cref="TypeInfo"/> read from <paramref name="reader"/>.</para>
		/// </returns>
		/// <exception cref="InvalidDataException">
		///	<para>Unexpected data is encountered in the stream.</para>
		/// </exception>
		public static TypeInfo ReadFrom(IPrimitiveReader reader)
		{
			var mode = (Mode)reader.ReadByte();
			switch (mode)
			{
				case Mode.Id:
					return Get(reader.ReadInt16());
				case Mode.Name:
					return _typeInfoByTypeName(reader.ReadString());
			}
			throw new InvalidDataException(string.Format("Unexpected mode encountered {0}", (byte)mode));
		}

		private enum Mode : byte
		{
			Id = 0,
			Name = 1
		}

		private static class TypeSpecific<T>
		{
			public static readonly TypeInfo TypeInfo;

			static TypeSpecific()
			{
				TypeInfo = _typeInfoByType(typeof(T));
			}
		}

		private class BenchmarkedRegion : IDisposable
		{
			private readonly string _regionName;
			private readonly Stopwatch _watch = new Stopwatch();

			public BenchmarkedRegion(string regionName)
			{
				_regionName = regionName;
				_watch.Start();
			}

			#region IDisposable Members

			public void Dispose()
			{
				_watch.Stop();
				LogResults();
			}

			#endregion

			[Conditional("DEBUG")]
			private void LogResults()
			{
				_log.InfoFormat("Code region '{0}' took {1} seconds to run.", _regionName, _watch.Elapsed.TotalSeconds);
			}
		}
	}
}
