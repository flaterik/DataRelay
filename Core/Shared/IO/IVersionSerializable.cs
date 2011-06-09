using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MySpace.Common.IO;

namespace MySpace.Common
{
	/// <summary>
	/// Represents the possible outcomes of deserializing an object.
	/// </summary>
	public enum SerializationResponse
	{
		/// <summary>
		/// Notifies the serializer that the version that was passed was expected and handles correctly.
		/// </summary>
		Success,

		/// <summary>
		/// Notifies the serializer that the object version was handled, but that it was out of date. A new save will be
		/// triggered in this instance.
		/// </summary>
		Handled,

		/// <summary>
		/// Notifies the serializer that the object version passed was unable to be handled. This will result in a database
		/// call and a resave to cache.
		/// </summary>
		Unhandled
	}

	/// <summary>
	/// Provides the ability to serialize and deserialze a class to and from a stream supporting 
	/// multiple data versions of streams.
	/// </summary>
	public interface IVersionSerializable: ICustomSerializable
	{
		/// <summary>
		/// Serialize the class data to a stream.
		/// </summary>
		/// <param name="writer">The <see cref="IPrimitiveWriter"/> that writes to the stream.</param>
		new void Serialize(IPrimitiveWriter writer);

		/// <summary>
		/// Deserialize the class data from a stream.
		/// </summary>
		/// <param name="reader">The <see cref="IPrimitiveReader"/> that extracts used to extra data from a stream.</param>
		/// <param name="version">The value of <see cref="CurrentVersion"/> that was written to the stream when it was originally serialized to a stream;
		/// the version of the <paramref name="reader"/> data.</param>
		void Deserialize(IPrimitiveReader reader, int version);

		/// <summary>
		/// Gets the current serialization data version of your object.  The <see cref="Serialize"/> method
		/// will write to the stream the correct format for this version.
		/// </summary>
		int CurrentVersion { get; }

		/// <summary>
		/// Deprecated. Has no effect.
		/// </summary>
		[Obsolete("Volatile has no effect on serialization.", false)]
		bool Volatile { get; }
	}

	/// <summary>
	/// This exception will trigger a database call and re-insert to cache, and should only be thrown if the version was 
	/// unableable to be successfully handled and deserialized.
	/// </summary>
	[global::System.Serializable]
	public class UnhandledVersionException : Exception
	{
		private int vExpected;
		private int vRecieved;

		public int VersionExpected { get { return vExpected; } }
		public int VersionRecieved { get { return vRecieved; } }

		public UnhandledVersionException(int expected, int recieved) { vExpected = expected; vRecieved = recieved; }

		public UnhandledVersionException() { }
		public UnhandledVersionException(string message) : base(message) { }
		public UnhandledVersionException(string message, Exception inner) : base(message, inner) { }
		
		protected UnhandledVersionException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

	/// <summary>
	/// This exception will trigger a re-insert to cache, and should only be thrown if the version was able to be
	/// successfully handled and deserialized. If the version is unable to be handled, then the UnhandledVersionException
	/// should be thrown instead.
	/// </summary>
	[global::System.Serializable]
	public class HandledVersionException : Exception
	{
		private int vExpected;
		private int vHandled;

		public int VersionExpected { get { return vExpected; } }
		public int VersionHandled { get { return vHandled; } }

		public HandledVersionException(int expected, int handled) { vExpected = expected; vHandled = handled; }

		public HandledVersionException() { }
		public HandledVersionException(string message) : base(message) { }
		public HandledVersionException(string message, Exception inner) : base(message, inner) { }

		protected HandledVersionException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
