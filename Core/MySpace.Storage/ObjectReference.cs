using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.Common.Storage;

namespace MySpace.Storage
{
	/// <summary>
	/// Holds information to refer to an object in <see cref="IObjectStorage"/>.
	/// </summary>
	public sealed class ObjectReference : IEquatable<ObjectReference>, IVersionSerializable
	{
		/// <summary>
		/// Gets the identifier of the key space associated with the type
		/// of the object being referred to.
		/// </summary>
		/// <value>The <see cref="DataBuffer"/> of the associated key space.</value>
		public DataBuffer TypeId { get; private set; }

		/// <summary>
		/// Gets the key used as identifier of the object.
		/// </summary>
		/// <value>The <see cref="StorageKey"/> identifier of the object
		/// used as key.</value>
		public StorageKey ObjectId { get; private set; }

		/// <summary>
		/// 	<para>Overriden. Indicates whether the current object is equal to another object of the same type.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.</para>
		/// </returns>
		/// <param name="other">
		/// 	<para>An object to compare with this object.</para>
		/// </param>
		public bool Equals(ObjectReference other)
		{
			if (ReferenceEquals(other, null)) return false;
			return TypeId.Equals(other.TypeId) && ObjectId.Equals(other.ObjectId);
		}

		/// <summary>
		/// 	<para>Overriden. Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="System.Object"/>.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the specified <see cref="System.Object"/> is equal to the current <see cref="System.Object"/>; otherwise, false.</para>
		/// </returns>
		/// <param name="obj">
		/// 	<para>The <see cref="System.Object"/> to compare with the current <see cref="System.Object"/>.</para>
		/// </param>
		/// <exception cref="System.NullReferenceException">
		/// 	<para>The <paramref name="obj"/> parameter is null.</para>
		/// </exception>
		public override bool Equals(object obj)
		{
			return Equals(obj as ObjectReference);
		}

		/// <summary>
		/// 	<para>Overriden. Serves as a hash function for a particular type.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A hash code for the current <see cref="ObjectReference"/>.</para>
		/// </returns>
		public override int GetHashCode()
		{
			return Utility.CombineHashCodes(TypeId.GetHashCode(), ObjectId.GetHashCode());
		}

		/// <summary>
		/// 	<para>Overriden. Returns a <see cref="System.String"/> that represents the current <see cref="ObjectReference"/>.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="System.String"/> that represents the current <see cref="System.Object"/>.</para>
		/// </returns>
		public override string ToString()
		{
			return string.Format("{0}:{1}", TypeId, ObjectId);
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectReference"/> class
		///		for deserialization.</para>
		/// </summary>
		public ObjectReference()
		{
			TypeId = DataBuffer.Empty;
			ObjectId = new StorageKey();
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectReference"/> class
		///		when the type key space and object id key are already determined.</para>
		/// </summary>
		/// <param name="typeId">
		/// 	<para>The type key space.</para>
		/// </param>
		/// <param name="objectId">
		/// 	<para>The object id key.</para>
		/// </param>
		public ObjectReference(DataBuffer typeId, StorageKey objectId)
		{
			TypeId = typeId;
			ObjectId = objectId;
		}

		/// <summary>
		/// 	<para>Serialize the class data to a stream.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="IPrimitiveWriter"/> that writes to the
		///		stream.</para>
		/// </param>
		public void Serialize(IPrimitiveWriter writer)
		{
			TypeId.SerializeValue(writer);
			ObjectId.SerializeValue(writer);
		}

		/// <summary>
		/// 	<para>Deserialize the class data from a stream.</para>
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="IPrimitiveReader"/> that extracts used to extra data from a stream.</para>
		/// </param>
		/// <param name="version">
		/// 	<para>The value of <see cref="CurrentVersion"/> that was written to the stream when it was originally serialized to a stream; the version of the <paramref name="reader"/> data.</para>
		/// </param>
		public void Deserialize(IPrimitiveReader reader, int version)
		{
			if (version == 1)
			{
				TypeId = DataBuffer.DeserializeValue(reader);
				ObjectId = StorageKey.DeserializeValue(reader);
			}
			else
			{
				reader.Response = SerializationResponse.Unhandled;
			}
		}

		/// <summary>
		/// 	<para>Gets the current serialization data version of your object.  The <see cref="Serialize"/> method will write to the stream the correct format for this version.</para>
		/// </summary>
		public int CurrentVersion
		{
			get { return 1; }
		}

		/// <summary>
		/// 	<para>Deprecated. Has no effect.</para>
		/// </summary>
		public bool Volatile
		{
			get { return false; }
		}

		void ICustomSerializable.Deserialize(IPrimitiveReader reader)
		{
			throw new NotImplementedException();
		}
	}
}
