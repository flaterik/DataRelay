using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.IO;
using System.Xml;

namespace MySpace.Common.Storage
{
	/// <summary>
	/// Specifies extended storage key.
	/// </summary>
	public struct StorageKey : IVersionSerializable, IEquatable<StorageKey>
	{
		/// <summary>
		/// Gets the key to be used for storage in a partition.
		/// </summary>
		/// <value>The <see cref="DataBuffer"/> specifying the key within
		/// the partition specified by <see cref="PartitionId"/>.</value>
		public DataBuffer Key { get; private set; }

		/// <summary>
		/// Gets the identifier of the partition to be used for storage, for stores
		/// that use partitions.
		/// </summary>
		/// <value>The <see cref="Int32"/> specifying the partition to use.</value>
		public int PartitionId { get; private set; }

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="StorageKey"/> structure.</para>
		/// </summary>
		/// <param name="key">
		/// 	<para>The <see cref="DataBuffer"/> storage key.</para>
		/// </param>
		/// <param name="partitionId">
		/// 	<para>The <see cref="Int32"/> partition id.</para>
		/// </param>
		public StorageKey(DataBuffer key, int partitionId) : this()
		{
			Key = key;
			PartitionId = partitionId;
		}

		/// <summary>
		/// Casts a data buffer to a storage key.
		/// </summary>
		/// <param name="key">The <see cref="DataBuffer"/> storage key.</param>
		/// <returns>The new <see cref="StorageKey"/>, with <see cref="DataBuffer.GetObjectId"/> of
		/// <paramref name="key"/> used as the <see cref="PartitionId"/> as well.</returns>
		public static implicit operator StorageKey(DataBuffer key)
		{
			return new StorageKey(key, key.GetObjectId());
		}

		/// <summary>
		/// Casts an integer to a storage key.
		/// </summary>
		/// <param name="key">The <see cref="Int32"/> storage key.</param>
		/// <returns>The new <see cref="StorageKey"/>, with <see cref="DataBuffer.GetObjectId"/> of
		/// <paramref name="key"/> used as the <see cref="PartitionId"/> as well.</returns>
		public static implicit operator StorageKey(int key)
		{
			return (DataBuffer)key;
		}

		/// <summary>
		/// Casts a string to a storage key.
		/// </summary>
		/// <param name="key">The <see cref="String"/> storage key.</param>
		/// <returns>The new <see cref="StorageKey"/>, with <see cref="DataBuffer.GetObjectId"/> of
		/// <paramref name="key"/> used as the <see cref="PartitionId"/> as well.</returns>
		public static implicit operator StorageKey(string key)
		{
			return (DataBuffer)key;
		}

		/// <summary>
		/// Casts a byte array to a storage key.
		/// </summary>
		/// <param name="key">The <see cref="Byte"/> array storage key.</param>
		/// <returns>The new <see cref="StorageKey"/>, with <see cref="DataBuffer.GetObjectId"/> of
		/// <paramref name="key"/> used as the <see cref="PartitionId"/> as well.</returns>
		public static implicit operator StorageKey(byte[] key)
		{
			return (DataBuffer)key;
		}

		/// <summary>
		/// 	<para>Overriden. Returns a string representation of this instance.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="String"/> representing this instance.</para>
		/// </returns>
		/// <filterpriority>
		/// 	<para>2</para>
		/// </filterpriority>
		public override string ToString()
		{
			return string.Format("{0};{1}", Key, PartitionId);
		}

		/// <summary>
		/// 	<para>Indicates whether the current object is equal to another object of the same type.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.</para>
		/// </returns>
		/// <param name="other">
		/// 	<para>A <see cref="StorageKey"/> to compare with this object.</para>
		/// </param>
		public bool Equals(StorageKey other)
		{
			return PartitionId == other.PartitionId && Key.Equals(other.Key);
		}

		/// <summary>
		/// 	<para>Overriden. Indicates whether this instance and a specified object are equal.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.</para>
		/// </returns>
		/// <param name="obj">
		/// 	<para>Another object to compare to.</para>
		/// </param>
		/// <filterpriority>
		/// 	<para>2</para>
		/// </filterpriority>
		public override bool Equals(object obj)
		{
			if (obj == null) return false;
			if (!obj.GetType().Equals(typeof(StorageKey))) return false;
			return Equals((StorageKey) obj);
		}

		/// <summary>
		/// 	<para>Overriden. Returns the hash code for this instance.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A 32-bit signed integer that is the hash code for this instance.</para>
		/// </returns>
		/// <filterpriority>
		/// 	<para>2</para>
		/// </filterpriority>
		public override int GetHashCode()
		{
			var hash = Key.GetHashCode();
			if (hash < 0)
			{
				hash = (hash << 1) + 1;
			} else
			{
				hash <<= 1;
			}
			return hash ^ PartitionId;
		}

		/// <summary>
		/// 	<para>Serialize the class data to a stream.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="IPrimitiveWriter"/> that writes to the stream.</para>
		/// </param>
		public void Serialize(IPrimitiveWriter writer)
		{
			Key.SerializeValue(writer);
			writer.Write(PartitionId);
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
				Key = DataBuffer.DeserializeValue(reader);
				PartitionId = reader.ReadInt32();
			} else
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

		/// <summary>
		/// Writes the format version number to a stream then serializes the
		/// instance to the stream.
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="IPrimitiveWriter"/> that writes to the stream.</para>
		/// </param>
		public void SerializeValue(IPrimitiveWriter writer)
		{
			writer.Write(CurrentVersion);
			Serialize(writer);
		}

		/// <summary>
		/// Returns a data buffer instance deserialized from a stream using the
		/// format version number read from the stream.
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="IPrimitiveReader"/> that extracts used to extra data from a stream.</para>
		/// </param>
		/// <returns>The <see cref="StorageKey"/> deserialized from
		/// <paramref name="reader"/>.</returns>
		/// <exception cref="ApplicationException">
		/// <para>The deserialization failed.</para>
		/// </exception>
		public static StorageKey DeserializeValue(IPrimitiveReader reader)
		{
			var ret = new StorageKey();
			var version = reader.ReadInt32();
			ret.Deserialize(reader, version);
			if (reader.Response != SerializationResponse.Success)
			{
				throw new ApplicationException("Could not deserialize");
			}
			return ret;
		}
	}
}
