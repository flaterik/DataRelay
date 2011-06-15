using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;
using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// Manages the dependency of a dependent object on another,
	/// independent object.
	/// </summary>
	public class ObjectDependency : IVersionSerializable, IEquatable<ObjectDependency>
	{
		/// <summary>
		/// Gets the reference to the dependent object.
		/// </summary>
		/// <value><see cref="ObjectReference"/> that specifies the dependent
		/// object.</value>
		public ObjectReference Reference { get; private set; }

		/// <summary>
		/// Gets the last updated date of the dependent object.
		/// </summary>
		/// <value><see cref="DateTime"/> when the object was updated.</value>
		public DateTime LastUpdatedDate { get; private set; }

		/// <summary>
		/// Gets the type of dependency.
		/// </summary>
		/// <value><see cref="DependencyType"/> that specifies the type
		/// of dependency.</value>
		public DependencyType Type { get; private set; }

		/// <summary>
		/// Gets the custom object dependency object, if any.
		/// </summary>
		/// <value>The custom <see cref="IObjectDependency"/>,
		/// if used. Can be <see langword="null"/>.</value>
		public IObjectDependency CustomDependency { get; private set; }

		/// <summary>
		/// 	<para>Initializes a new instance of the
		///		<see cref="ObjectDependency"/> class for a custom dependency.</para>
		/// </summary>
		/// <param name="customDependency">
		/// 	<para>The <see cref="IObjectDependency"/> to place in the
		///		<see cref="ObjectDependency"/>.</para>
		/// </param>
		internal ObjectDependency(IObjectDependency customDependency)
		{
			CustomDependency = customDependency;
			Reference = null;
			LastUpdatedDate = new DateTime();
			Type = new DependencyType();
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectDependency"/>
		///		class for expiration of other objects.</para>
		/// </summary>
		/// <param name="reference">
		/// 	<para>Reference to the dependent object.</para>
		/// </param>
		/// <param name="lastUpdatedDate">
		/// 	<para>Last updated date of the dependent object.</para>
		/// </param>
		/// <param name="type">
		/// 	<para>Type of dependency.</para>
		/// </param>
		internal ObjectDependency(ObjectReference reference, DateTime lastUpdatedDate,
			DependencyType type)
		{
			CustomDependency = null;
			Reference = reference;
			LastUpdatedDate = lastUpdatedDate;
			Type = type;
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ObjectDependency"/> class
		///		for deserialization.</para>
		/// </summary>
		internal ObjectDependency()
		{
			CustomDependency = null;
			Reference = null;
			LastUpdatedDate = new DateTime();
			Type = new DependencyType();
		}

		/// <summary>
		/// 	<para>Indicates whether the current object is equal to another object of the same type.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.</para>
		/// </returns>
		/// <param name="other">
		/// 	<para>An object to compare with this object.</para>
		/// </param>
		public bool Equals(ObjectDependency other)
		{
			if (other == null) return false;
			return Utility.AreEqualled(CustomDependency, other.CustomDependency) &&
			       Utility.AreEqual(Reference, other.Reference) &&
			       LastUpdatedDate.Equals(other.LastUpdatedDate) &&
			       Type.Equals(other.Type);
		}

		/// <summary>
		/// 	<para>Overriden. Determines whether the specified <see cref="ObjectDependency"/> is equal to the current <see cref="System.Object"/>.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the specified <see cref="System.Object"/> is equal to the current <see cref="ObjectDependency"/>; otherwise, false.</para>
		/// </returns>
		/// <param name="obj">
		/// 	<para>The <see cref="System.Object"/> to compare with the current <see cref="ObjectDependency"/>.</para>
		/// </param>
		/// <exception cref="System.NullReferenceException">
		/// 	<para>The <paramref name="obj"/> parameter is null.</para>
		/// </exception>
		public override bool Equals(object obj)
		{
			return Equals(obj as ObjectDependency);
		}

		/// <summary>
		/// 	<para>Overriden. Serves as a hash function for a particular type.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A hash code for the current <see cref="ObjectDependency"/>.</para>
		/// </returns>
		public override int GetHashCode()
		{
			if (CustomDependency != null)
			{
				return 0x3f12d876 ^ CustomDependency.GetHashCode();
			}
			return Utility.CombineHashCodes(Reference.GetHashCode(),
				LastUpdatedDate.GetHashCode(), Type.GetHashCode());
		}

		/// <summary>
		/// 	<para>Called when a change is made to the cached object.</para>
		/// </summary>
		/// <param name="changed">
		/// 	<para>
		/// 		<see cref="ObjectReference"/> that refers to the changed cached object.</para>
		/// </param>
		/// <param name="op">
		/// 	<para>
		/// 		<see cref="OperationType"/> that specifies the type of operation performed.</para>
		/// </param>
		/// <returns>
		/// 	<para>
		/// 		<see langword="true"/> if the dependency should be retained, otherwise <see langword="false"/>.</para>
		/// </returns>
		public bool Notify(ObjectReference changed, OperationType op)
		{
			if (CustomDependency != null)
			{
				return CustomDependency.Notify(changed, op);
			}
			switch(op)
			{
				case OperationType.Delete:
					break;
				case OperationType.Save:
					if (Type == DependencyType.Existence) return true;
					break;
				default:
					throw new NotImplementedException(string.Format(
						op + " not implemented"));
			}
			LocalCache.DeleteVersion(Reference.TypeId, Reference.ObjectId, LastUpdatedDate);
			return false;
		}

		/// <summary>
		/// 	<para>Serialize the class data to a stream.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="IPrimitiveWriter"/> that writes to the stream.</para>
		/// </param>
		public void Serialize(IPrimitiveWriter writer)
		{
			if (CustomDependency != null)
			{
				writer.Write(true);
				var descr = LocalCache.Policy.GetDescription(CustomDependency.GetType());
				descr.KeySpace.SerializeValue(writer);
				writer.Write(CustomDependency.CurrentVersion);
				CustomDependency.Serialize(writer);
			}
			else
			{
				writer.Write(false);
				writer.Write((int)Type);
				writer.Write(Reference, false);
				writer.Write(LastUpdatedDate);
			}
		}

		/// <summary>
		/// 	<para>Overriden. Returns a <see cref="System.String"/> that represents the current <see cref="System.Object"/>.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="System.String"/> that represents the current <see cref="System.Object"/>.</para>
		/// </returns>
		public override string ToString()
		{
			if (CustomDependency != null)
			{
				return CustomDependency.ToString();
			}
			return string.Format("Type={0}, Reference={1}, Update={2}",
				Type, Reference, LastUpdatedDate);
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
				if (reader.ReadBoolean())
				{
					var keySpace = DataBuffer.DeserializeValue(reader);
					var descr = LocalCache.Policy.GetDescription(keySpace);
					var dependencyVersion = reader.ReadInt32();
					CustomDependency = (IObjectDependency)descr.Creator();
					CustomDependency.Deserialize(reader, dependencyVersion);
					Type = new DependencyType();
					Reference = null;
					LastUpdatedDate = new DateTime();
				} else
				{
					CustomDependency = null;
					Type = (DependencyType)reader.ReadInt32();
					Reference = reader.Read<ObjectReference>(false);
					LastUpdatedDate = reader.ReadDateTime();					
				}
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
	}
}
