using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Encapsulates methods for serializing and deserializing
	/// objects marked with <see cref="SerializableClassAttribute"/>
	/// and <see cref="SerializablePropertyAttribute"/> attributes.
	/// </summary>
    public static class BarfFormatter
    {
		private const int _maxFrameworkVersion = 1;
		private const int _minFrameworkVersion = 1;
		[ThreadStatic]
		private const int _currentFrameworkVersion = _minFrameworkVersion;

		/// <summary>
		/// Serializes <see cref="value"/> into <see cref="output"/>.
		/// </summary>
		/// <typeparam name="T">The type of object to serialize.</typeparam>
		/// <param name="value">The object to serialize.</param>
		/// <param name="output">The stream to write to.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="output"/> is <see langword="null"/>.</para>
		/// </exception>
        public static void Serialize<T>(T value, Stream output)
        {
			if (output == null) throw new ArgumentNullException("output");

			var serializer = BarfSerializers.Get<T>(PartFlags.None);
			var stream = output.CanSeek ? output : new MemoryStream();
			var args = new BarfSerializationArgs(SerializerFactory.GetWriter(stream));
			serializer.Serialize(value, args);

			if (!output.CanSeek)
			{
				output.Write(((MemoryStream)stream).GetBuffer(), 0, (int)stream.Length);
			}
		}

		/// <summary>
		/// Deserializes an object of the specified type from <paramref name="input"/>.
		/// </summary>
		/// <typeparam name="T">The type of object to deserialize.</typeparam>
		/// <param name="input">The stream to read from.</param>
		/// <returns>The deserialized object. <see langword="null"/> values are possible
		/// if <see langword="null"/> was originally serialized into the stream.</returns>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="input"/> is <see langword="null"/>.</para>
		/// </exception>	
		/// <exception cref="ArgumentException">
		///		<para><paramref name="input.CanSeek"/> is <see langword="false"/>.</para>
		/// </exception>
        public static T Deserialize<T>(Stream input)
        {
			ArgumentAssert.IsNotNull(input, "input");
			if (!input.CanSeek) throw new ArgumentException("input.CanSeek must be true", "input");

			var serializer = BarfSerializers.Get<T>(PartFlags.None);
			var args = new BarfDeserializationArgs(SerializerFactory.GetReader(input));
			return serializer.Deserialize(args);
        }

		/// <summary>
		/// Determines whether the specified type can be
		/// serialized and deserialized with this class.
		/// </summary>
		/// <param name="type">The type to serialize/deserialize.</param>
		/// <returns>
		/// 	<see langword="true"/> if the specified type is serializable; otherwise, <see langword="false"/>.
		/// </returns>
        public static bool IsSerializable(Type type)
        {
            return SerializableClassAttribute.HasAttribute(type);
        }

		/// <summary>
		/// Gets the current maximum framework version that can currently be read.
		/// </summary>
		/// <value>The current maximum framework version that can currently be read.</value>
		public static int MaxFrameworkVersion
		{
			[DebuggerStepThrough]
			get { return _maxFrameworkVersion; }
		}

		/// <summary>
		/// Gets the current minimum framework version expected in production.
		/// This is the framework version that will be used to write data by default.
		/// </summary>
		/// <value>The current minimum framework version expected in production.</value>
		public static int MinFrameworkVersion
		{
			[DebuggerStepThrough]
			get { return _minFrameworkVersion; }
		}

		/// <summary>
		/// Gets the current framework version.
		/// This is the version that is currently being written.
		/// </summary>
		/// <value>The current minimum framework version expected in production.</value>
		public static int CurrentFrameworkVersion
		{
			[DebuggerStepThrough]
			get { return _currentFrameworkVersion; }
		}
    }
}
