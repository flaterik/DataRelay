using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using MySpace.Logging;

namespace MySpace.Common.IO
{
	[Flags]
	public enum SerializerFlags
	{
		/// <summary>
		/// Use default serialization settings
		/// </summary>
		Default = 0x00000000,
		/// <summary>
		/// Compress/decompress the serialized data
		/// </summary>
		Compress = 0x00000001,
		/// <summary>
		/// Instructs the serializer to use the legacy
		/// ICustomSerialable and IVersionSerializable
		/// mechanisms.
		/// </summary>
		//UseLegacySerialization  = 0x00000002,
	}

	public static class Serializer
	{
		private static readonly LogWrapper _log = new LogWrapper();

		private static readonly Pool<MemoryStream> _memoryPool;

		private static string GetLegacyMessage(Type type, bool includeStackTrace)
		{
			var message = string.Format("Use of BinaryFormatter on object type {0}. Use an alternative type that is supported by the myspace serialization framework or email INFRASTRUCTURE_CORE to add support for this type.", type);
			if (includeStackTrace)
			{
				message += "\r\n" + Environment.StackTrace;
			}
			return message;
		}

		static Serializer()
		{
			SerializationMemoryConfig config = null;
			try
			{
				config = (SerializationMemoryConfig)ConfigurationManager.GetSection("SerializationMemory");
			}
			catch (Exception e)
			{
				_log.Error("Failed to load configuration section 'SerializationMemory'. Using defaults. - " + e);
			}

			if (config == null || !config.EnablePooling)
			{
				_memoryPool = new Pool<MemoryStream>(
					() => new MemoryStream(),
					(s, phase) => false,
					new PoolConfig());
			}
			else
			{
				_memoryPool = new Pool<MemoryStream>(
					() => new MemoryStream(),
					(s, phase) =>
					{
						if (phase == PoolItemPhase.Leaving)
						{
							s.SetLength(0);
						}
						return true;
					},
					config);
			}
		}

#if DEBUG
		[ThreadStatic]
		private static int _suppressAlertCount = 0;
#endif

		private class SuppressAlertContext : IDisposable
		{
			private static readonly SuppressAlertContext _instance = new SuppressAlertContext();

			public static SuppressAlertContext Open()
			{
#if DEBUG
				++_suppressAlertCount;
#endif
				return _instance;
			}

			private SuppressAlertContext()
			{
			}

			public void Dispose()
			{
#if DEBUG
				--_suppressAlertCount;
#endif
			}
		}

		private static readonly Factory<Type, bool> _oneTimeLogger = Algorithm.LazyIndexer<Type, bool>(type =>
		{
			_log.WarnFormat(GetLegacyMessage(type, true));
			return true;
		});

		/// <summary>
		/// Opens a scope that will suppress <see cref="InvalidOperationException"/>s
		/// thrown by the serialization framework. This scope should be closed (disposed) when
		/// it is no longer needed and should only be used for testing reasons.
		/// </summary>
		/// <returns>
		/// An <see cref="IDisposable"/> object that will close the scope.
		/// </returns>
		internal static IDisposable OpenSuppressAlertScope()
		{
			return SuppressAlertContext.Open();
		}

		/// <summary>
		/// Alerts that the specified type is being serialized in a legacy manner.
		/// If DEBUG is defined an <see cref="InvalidOperationException"/> is thrown.
		/// Otherwise the alert is logged the first time this type is encountered.
		/// </summary>
		/// <param name="type">The type being serialized.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///	<para>Thrown if DEBUG is defined and <see cref="SuppressAlertContext"/> is not in effect.
		///	Otherwise the alert is logged the first time this type is encountered.</para>
		/// </exception>
		internal static void AlertLegacySerialization(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

#if DEBUG
			if (_suppressAlertCount == 0)
			{
				throw new InvalidOperationException(GetLegacyMessage(type, false));
			}
#endif
			_oneTimeLogger(type);
		}

		#region Serialize Methods
		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="writer">The target stream</param>
		/// <param name="instance">The object to serialize. This can be null.</param>
		/// <param name="flags">One or more <see cref="SerializedFlags"/> options</param>
		public static void Serialize<T>(IPrimitiveWriter writer, T instance, SerializerFlags flags)
		{
			TypeSerializationInfo typeInfo = TypeSerializationInfo.GetTypeInfo<T>(instance);
			TypeSerializationArgs args = new TypeSerializationArgs();

			args.Writer = writer;
			args.Flags = flags;

			typeInfo.Serialize(instance, args);

		}

		/// <summary>
		/// Internal use only
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="instance"></param>
		/// <param name="args"></param>
		public static void Serialize<T>(T instance, TypeSerializationArgs args)
		{
			TypeSerializationInfo typeInfo = null;

			if (args.IsBaseClass)
			{
				//  For base class handling, make sure we explicitly get the type
				//  info for the template parameter
				typeInfo = TypeSerializationInfo.GetTypeInfo(typeof(T));
			}
			else
			{
				//  Otherwise use the default type detection
				typeInfo = TypeSerializationInfo.GetTypeInfo<T>(instance);
			}

			typeInfo.Serialize(instance, args);
		}

		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="stream">The target stream</param>
		/// <param name="instance">The object to serialize</param>
		public static void Serialize<T>(IPrimitiveWriter stream, T instance)
		{
			Serialize<T>(stream, instance, SerializerFlags.Default);
		}

		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="instance">The object to serialize</param>
		/// <param name="useCompression">True to compress the serialized object</param>
		/// <returns>The serialized object</returns>
		public static byte[] Serialize<T>(T instance, bool useCompression)
		{
			return Serialize(
						instance,
						useCompression ? SerializerFlags.Compress : SerializerFlags.Default,
						Compressor.DefaultCompressionImplementation
						);
		}

		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="instance">The object to serialize</param>
		/// <param name="useCompression">True to compress the serialized object</param>
		/// <param name="compression">Compression method to use</param>
		/// <returns>The serialized object</returns>
		public static byte[] Serialize<T>(T instance, bool useCompression, CompressionImplementation compression)
		{
			return Serialize(
						instance,
						useCompression ? SerializerFlags.Compress : SerializerFlags.Default,
						compression
						);
		}

		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="instance">The object to serialize</param>
		/// <param name="flags">One or more <see cref="SerializedFlags"/> options</param>
		/// <param name="compression">Compression method to use</param>
		/// <returns>The serialized object</returns>
		public static byte[] Serialize<T>(T instance, SerializerFlags flags, CompressionImplementation compression)
		{
			using (var item = _memoryPool.Borrow())
			{
				var stream = item.Item;
				Serialize(stream, instance, flags, compression);
				return stream.ToArray();
			}
		}

		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="instance">The object to serialize</param>
		/// <param name="flags">One or more <see cref="SerializedFlags"/> options</param>
		/// <returns>The serialized object</returns>
		public static byte[] Serialize<T>(T instance, SerializerFlags flags)
		{
			using (var item = _memoryPool.Borrow())
			{
				var stream = item.Item;
				Serialize(stream, instance, flags, Compressor.DefaultCompressionImplementation);
				return stream.ToArray();
			}
		}

		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="stream">The target stream</param>
		/// <param name="instance">The object to serialize</param>
		public static void Serialize<T>(Stream stream, T instance)
		{
			Serialize(stream, instance, SerializerFlags.Default);
		}

		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="stream">The target stream</param>
		/// <param name="instance">The object to serialize</param>
		/// <param name="useCompression">True to compress the serialized object</param>
		public static void Serialize<T>(Stream stream, T instance, bool useCompression)
		{
			Serialize(
				stream,
				instance,
				useCompression ? SerializerFlags.Compress : SerializerFlags.Default,
				Compressor.DefaultCompressionImplementation
				);
		}

		public static void Serialize<T>(Stream stream, T instance, bool useCompression, CompressionImplementation compression)
		{
			Serialize(
				stream,
				instance,
				useCompression ? SerializerFlags.Compress : SerializerFlags.Default,
				compression
				);
		}

		public static void Serialize<T>(Stream stream, T instance, SerializerFlags flags)
		{
			Serialize(
				stream,
				instance,
				flags,
				Compressor.DefaultCompressionImplementation
				);
		}

		/// <summary>
		/// Serializes an object
		/// </summary>
		/// <param name="stream">The target stream</param>
		/// <param name="instance">The object to serialize</param>
		/// <param name="flags">One or more <see cref="SerializedFlags"/> options</param>
		public static void Serialize<T>(
							Stream stream,
							T instance,
							SerializerFlags flags,
							CompressionImplementation compression
							)
		{
			IPrimitiveWriter writer = SerializerFactory.GetWriter(stream);

			//  Check parameters
			Debug.Assert(stream != null, "Input stream is null");
			if (stream == null) throw new ArgumentNullException("stream");

			if (instance == null)
			{
				return; // Nothing to do
			}

			Serialize<T>(writer, instance, flags);

			//  Compress result if requested
			if ((flags & SerializerFlags.Compress) != 0)
			{
				byte[] bytes = stream.ToArray();

				bytes = Compressor.GetInstance().Compress(bytes, compression);

				stream.Seek(0, SeekOrigin.Begin);
				stream.SetLength(bytes.Length);
				stream.Write(bytes, 0, bytes.Length);
			}
		}

		#endregion // Serialize Overloads

		#region Deserialize Methods
		/// <summary>
		/// Deserializes an object from a stream and returns the result
		/// </summary>
		/// <param name="stream">The source stream</param>
		/// <param name="instance">The instance to receive the deserialized properties</param>
		/// <param name="flags">One or more <see cref="SerializedFlags"/> options</param>
		/// <returns>The deserialized object or NULL if the version is invalid and the object is non-volatile</returns>
		public static T Deserialize<T>(IPrimitiveReader reader, SerializerFlags flags)
		{
			object instance = default(T);
			if (Deserialize<T>(reader, ref instance, flags))
			{
				return (T)instance;
			}
			return default(T);
		}

		/// <summary>
		/// Deserializes an object from a stream into a pre-created empty object
		/// </summary>
		/// <typeparam name="T">The type of the object to deserialize</typeparam>
		/// <param name="reader">Source stream</param>
		/// <param name="instance">An empty instance of the type to deserialize</param>
		/// <param name="flags"></param>
		/// <returns>Returns true if the object was deserialized</returns>
		public static bool Deserialize<T>(IPrimitiveReader reader, T instance, SerializerFlags flags)
		{
			object oi = instance;
			return Deserialize<T>(reader, ref oi, flags);
		}

		/// <summary>
		/// Deserializes an object from a stream into a pre-created empty object
		/// </summary>
		/// <param name="reader">Source stream</param>
		/// <param name="instance">An empty instance of the type to deserialize or null to create the object automatically</param>
		/// <param name="flags"></param>
		/// <returns>Returns true if the object was deserialized</returns>
		static bool Deserialize<T>(IPrimitiveReader reader, ref object instance, SerializerFlags flags)
		{
			TypeSerializationArgs args = new TypeSerializationArgs();

			args.Reader = reader;
			args.Flags = flags;
			return Deserialize<T>(ref instance, args);
		}

		/// <summary>
		/// Internal use only
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="instance"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public static bool Deserialize<T>(ref object instance, TypeSerializationArgs args)
		{
			TypeSerializationInfo typeInfo = null;

			//  Check arguments
			if (args.Reader == null)
			{
				Debug.Fail("Input stream is null");
				throw new ArgumentNullException("reader");
			}

			if (args.IsBaseClass)
			{
				//  For base class handling, make sure we explicitly get the type
				//  info for the template parameter
				typeInfo = TypeSerializationInfo.GetTypeInfo(typeof(T));
			}
			else
			{
				//  Otherwise use the default type detection
				typeInfo = TypeSerializationInfo.GetTypeInfo<T>(instance);
			}
			return typeInfo.Deserialize(ref instance, args);
		}

		/// <summary>
		/// Deserializes an object
		/// </summary>
		/// <typeparam name="T">The type of object to deserialize</typeparam>
		/// <param name="stream">The source stream</param>
		/// <returns>The deserialized object</returns>
		public static T Deserialize<T>(Stream stream)
		{
			return Deserialize<T>(stream, SerializerFlags.Default);
		}

		/// <summary>
		/// Deserializes an object
		/// </summary>
		/// <typeparam name="T">The type of object to deserialize</typeparam>
		/// <param name="stream">The source stream</param>
		/// <returns>The deserialized object</returns>
		internal static T Deserialize<T>(IPrimitiveReader stream) where T : new()
		{
			return Deserialize<T>(stream, SerializerFlags.Default);
		}

		/// <summary>
		/// Deserializes an object
		/// </summary>
		/// <typeparam name="T">The type of object to deserialize</typeparam>
		/// <param name="stream">The source stream</param>
		/// <param name="useCompression">If true then the source stream is compressed</param>
		/// <returns>The deserialized object</returns>
		public static bool Deserialize<T>(Stream stream, T instance, bool useCompression)
		{
			return Deserialize<T>(stream, instance, useCompression ? SerializerFlags.Compress : SerializerFlags.Default);
		}

		public static T Deserialize<T>(Stream stream, bool useCompression)
		{
			return Deserialize<T>(stream, useCompression ? SerializerFlags.Compress : SerializerFlags.Default);
		}

		/// <summary>
		/// Deserializes an object
		/// </summary>
		/// <param name="stream">The source stream</param>
		/// <param name="instance">The instance to receive the deserialized properties</param>
		/// <param name="flags">One or more <see cref="SerializedFlags"/> options</param>
		public static bool Deserialize<T>(Stream stream, T instance, SerializerFlags flags)
		{
			return Deserialize<T>(stream, instance, flags, Compressor.DefaultCompressionImplementation);
		}

		/// <summary>
		/// Deserializes an object
		/// </summary>
		/// <param name="stream">The source stream</param>
		/// <param name="instance">The instance to receive the deserialized properties</param>
		public static bool Deserialize<T>(Stream stream, T instance)
		{
			return Deserialize<T>(stream, instance, SerializerFlags.Default, Compressor.DefaultCompressionImplementation);
		}

		/// <summary>
		/// Deserializes an object
		/// </summary>
		/// <param name="stream">The source stream</param>
		/// <param name="instance">The instance to receive the deserialized properties</param>
		/// <param name="flags">One or more <see cref="SerializedFlags"/> options</param>
		/// <param name="compression">Compression method to use</param>
		public static bool Deserialize<T>(Stream stream, T instance, SerializerFlags flags, CompressionImplementation compression)
		{
			//  Check args
			Debug.Assert(stream != null, "Input stream is null");
			if (stream == null)
				throw new ArgumentNullException("stream");

			return Deserialize<T>(
							GetReader(stream, flags, compression),
							instance,
							flags
							);
		}

		public static T Deserialize<T>(Stream stream, SerializerFlags flags)
		{
			return Deserialize<T>(
							GetReader(stream, flags, Compressor.DefaultCompressionImplementation),
							flags
							);
		}

		public static T Deserialize<T>(Stream stream, SerializerFlags flags, CompressionImplementation compression) where T : new()
		{
			return Deserialize<T>(
							GetReader(stream, flags, compression),
							flags
							);
		}

		public static object Deserialize(Stream stream, SerializerFlags flags, CompressionImplementation compression, Type instanceType)
		{
			TypeSerializationInfo typeInfo = null;
			TypeSerializationArgs args = new TypeSerializationArgs();
			object instance = Activator.CreateInstance(instanceType, true);

			args.Flags = flags;
			args.Reader = GetReader(stream, flags, compression);

			typeInfo = TypeSerializationInfo.GetTypeInfo(instanceType);
			typeInfo.Deserialize(ref instance, args);

			return instance;
		}

		#endregion // Deserialize overloads

		#region Helper Methods
		public static bool IsSerializable(Type t)
		{
			return (t.GetInterface(typeof(ICustomSerializable).Name) != null)
					|| SerializableClassAttribute.HasAttribute(t);
		}

		/// <summary>
		/// Method used for performance testing.
		/// </summary>
		[Conditional("DEBUG")]
		public static void CallGetTypeInfo<T>(object instance)
		{
			TypeSerializationInfo.GetTypeInfo<T>(instance);
		}

		public static bool Compare<T>(T x, T y)
		{
			Type t = ((object)x).GetType();

			if (t != ((object)y).GetType()) return false;

			return TypeSerializationInfo.GetTypeInfo<T>(x).Compare(x, y);
		}

		static IPrimitiveReader GetReader(Stream stream, SerializerFlags flags, CompressionImplementation compression)
		{
			//  Decompress source stream if necessary
			if ((flags & SerializerFlags.Compress) != 0)
			{
				byte[] bytes = stream.ToArray();

				bytes = Compressor.GetInstance().Decompress(bytes, compression);
				MemoryStream ms = new MemoryStream(bytes);

				stream = ms;
			}

			return SerializerFactory.GetReader(stream);
		}

		#endregion
	}

	/// <summary>
	/// Contains static methods that can convert primitive types
	/// </summary>
	public static class ConversionUtility
	{
		/// <summary>
		/// Converts the int array to a byte array.
		/// </summary>
		/// <param name="integers">The int array to convert.</param>
		/// <returns>the byte array representation of the specified int array</returns>
		public static byte[] ConvertIntArrayToByteArray(int[] integers)
		{
			if (integers == null || integers.Length == 0) return new byte[0];

			int sizeInBytes = integers.Length * 4;
			byte[] bytes = SafeMemoryAllocator.CreateArray<byte>(sizeInBytes);

			Buffer.BlockCopy(integers, 0, bytes, 0, sizeInBytes);

			return bytes;
		}

		/// <summary>
		/// Converts the byte array to an int array.
		/// </summary>
		/// <param name="bytes">The byte array to convert.</param>
		/// <returns>the int array representation of the specified byte array</returns>
		/// <remarks>This method expects the specified byte array's length to be divisible by 4 (the number of bytes for a single int)</remarks>
		/// <exception cref="ArgumentOutOfRangeException">thrown if the specified byte array is not divisible by 4</exception>
		public static int[] ConvertByteArrayToIntArray(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0) return new int[0];

			if (bytes.Length % 4 != 0)
				throw new ArgumentOutOfRangeException("bytes", "This method expects the specified byte array's length to be divisible by 4 (the number of bytes for a single int)");

			int sizeInIntegers = bytes.Length / 4;
			int[] integers = SafeMemoryAllocator.CreateArray<int>(sizeInIntegers);

			Buffer.BlockCopy(bytes, 0, integers, 0, bytes.Length);

			return integers;
		}

		/// <summary>
		/// This method will basically stuff two <see cref="int"/>s (passed in the <see cref="IntegerPair"/> 
		/// <paramref name="pair"/>) together into a single <see cref="ulong"/> and then finally converted 
		/// to a byte <see cref="byte"/> <see cref="Array"/>
		/// </summary>
		/// <param name="pair"></param>
		/// <remarks>Designed to assist in implementing <see cref="IExtendedRawCacheParameter"/></remarks>
		/// <seealso cref="ConvertToIntegerPair"/>
		public static byte[] ConvertToByteArray(IntegerPair pair)
		{
			ulong value = ((ulong)pair.First << 32) | (uint)pair.Second;
			return BitConverter.GetBytes(value);
		}

		/// <summary>
		/// This method will convert a <see cref="byte"/> <see cref="Array"/> back into two separate <see cref="int"/>s 
		/// and return them in a <see cref="IntegerPair"/>.
		/// </summary>
		/// <param name="value"></param>
		/// <remarks>Designed to assist in implementing <see cref="IExtendedRawCacheParameter"/></remarks>
		/// <seealso cref="ConvertToByteArray"/>
		/// <exception cref="ArgumentNullException">If the specified <paramref name="value"/> is <code>null</code></exception>
		/// <exception cref="ArgumentOutOfRangeException">If the specified <paramref name="value"/> has a <see cref="Array.Length"/> not equal to 8</exception>
		public static IntegerPair ConvertToIntegerPair(byte[] value)
		{
			if (value == null) throw new ArgumentNullException("value", "value cannot be null");
			if (value.Length != 8) throw new ArgumentOutOfRangeException("value", value.Length, "value must be a byte array with a length of 8.");

			ulong result = BitConverter.ToUInt64(value, 0);
			int first = (int)(result >> 32);
			int second = (int)(result);

			return new IntegerPair { First = first, Second = second };
		}
	}

	/// <summary>
	/// Simple data structure to store the values of two <see cref="int"/>s
	/// </summary>
	public struct IntegerPair
	{
		public int First, Second;
	}

}
