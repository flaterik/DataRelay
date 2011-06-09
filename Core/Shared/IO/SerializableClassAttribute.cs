using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common
{
	public enum LegacySerializationType
	{
		CustomSerializable,
		VersionSerializable
	}

	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public class SerializableClassAttribute : Attribute
	{
		#region Constants

		[Flags]
		enum Options
		{
			None = 0,
			Volatile = 0x0001,
			Inline = 0x0002,
			SerializeBase = 0x0004,
		}

		#endregion // Constants

		#region Members

		Options options = Options.None;
		#endregion

		#region Construction
		/// <summary>
		/// Indicates that the class supports version-safe serialization.
		/// </summary>
		public SerializableClassAttribute()
		{
		}
		#endregion

		#region Properties
		/// <summary>
		/// By setting this value to true, exceptions will be thrown to the client if anything
		/// other than a successful result is returned. Leaf object will most likely not want
		/// to throw exceptions, but just return an unpopulated object, and set the upstream
		/// return results to Unhandled. The default value is false.
		/// </summary>
		[Obsolete("This attribute has no effect on serialization.", false)]
		public bool Volatile
		{
			get { return true; }
			set { }
		}

		/// <summary>
		/// By setting this value, the serializer will not allow the following: 
		/// deserialization by a class whose current version is less than a serialized stream's min verion
		/// or deserialization by a class whose minimum version is greater than the stream's current version.
		/// The default value is 1.
		/// </summary>
		/// <remarks>
		/// This value will get written to the serialized stream.  
		/// </remarks>
		public int MinVersion { get; set; }

		/// <summary>
		/// Gets or sets the minimum version a serialized stream's current version must be.  If the
		/// serialized stream's current version is less than the <see cref="MinDeserializeVersion"/> deserialization
		/// will fail.
		/// </summary>
		/// <remarks>
		/// This value will not get written to the serialized stream. This value may never be greater 
		/// than the current version and if it's less than or equal to the <see cref="MinVersion"/> it is ignored.
		/// </remarks>
		public int MinDeserializeVersion { get; set; }

		/// <summary>
		/// Gets the minimum framework version to use when writing this type.
		/// E.g. if the global write version is 0 and this value is 1 then
		/// framework version 1 will be used to encode binary data.
		/// If the global write version is 2 and this value is 1 then
		/// framework version 2 will be used to encode binary data.
		/// The first version of the auto serialization framework is considered
		/// framework version 0. The first version of Barf is considered
		/// framework version 1.
		/// </summary>
		/// <value>The minimum framework version to use when writing this type.</value>
		public int MinWriteFrameworkVersion { get; set; }

		/// <summary>
		/// <para>Indicates that this is a simple, struct-like class that can be serialized inline with its
		/// container class. Inline classes have the following limitations:</para>
		/// <para>1. They cannot be serialized standalone</para>
		/// <para>2. They do not support versioning (all properties must be set to version 1)</para>
		/// <para>3. They have a maximum serialized data length 240 bytes</para>
		/// </summary>
		public bool Inline
		{
			get { return (this.options & Options.Inline) != 0; }
			set { SetOption(Options.Inline, value); }
		}

		/// <summary>
		/// Informs the serializer that it should automatically serialize
		/// members from base classes where the base class is also has the
		/// SerializableClass attribute. The default value is false.
		/// </summary>
		public bool SerializeBaseClass
		{
			get { return (this.options & Options.SerializeBase) != 0; }
			set { SetOption(Options.SerializeBase, value); }

		}

		/// <summary>
		/// <para>
		/// Informs the serializer that previous versions of this class
		/// used IVersionSerializable. If the input data to a Deserialize
		/// method detects this version or lower then the serializer will
		/// look for a method with signature 
		/// Deserialize(IPrimitiveReader reader, int version) and call
		/// it instead of the standard deserialization logic.
		/// </para>
		/// </summary>
		/// <remarks>
		/// <para>
		/// All serializable properties in the class must have a version that
		/// is higher than the value of LegacyVersion. If a lower version property
		/// is detected then the serializer will throw a <see cref="InvalidOperationException"/>
		/// when attempting to serialize or deserialize the class for the first time.
		/// </para>
		/// <para>
		/// When serializing the class, the minimum version of the data will be
		/// set to the higher of the MinVersion field and LegacyVersion+1. This is to
		/// prevent older versions of the class from attempting to deserialize the new data using
		/// <see cref="MySpace.Common.IVersionSerializable"/>
		/// </para>
		/// <para>
		/// Make sure you remove legacy serialization interfaces (<see cref="ICustomSerializable"/>
		/// or <see cref="IVersionSerializable"/>) when converting your objects to
		/// use attributes.
		/// </para>
		/// <para>
		/// For more information go to http://mywiki.corp.myspace.com/index.php/Serialization
		/// </para>
		/// </remarks>
		public int LegacyVersion{ get; set; }

		/// <summary>
		/// 	<para>Gets or sets a value indicating whether or not to suppress exceptions that are thrown
		/// 	in debug builds to warn consumers about bad serialization practices when serializing this type.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> to suppress exceptions that are thrown in debug builds to warn
		/// 	consumers about bad serialization practices when serializing this type; otherwise, <see langword="false"/>.</para>
		/// </value>
		public bool SuppressWarningExceptions { get; set; }

		/// <summary>
		/// 	<para>Gets or sets a value indicating whether dynamic auto-serialization code should only be
		/// 	generated at run time. This is only intended to be used to prevent class with intentional invalid
		/// 	serialization attributes from failing the build. This option should only be used for test-purposes.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> to only allow code generation at run-time for this type; otherwise, <see langword="false"/>.</para>
		/// </value>
		public bool RuntimeOnly { get; set; }

		#endregion

		#region Methods
		public static bool HasAttribute(Type t)
		{
			return t.IsDefined(typeof(SerializableClassAttribute), true);
		}

		public static bool HasAttribute(object o)
		{
			return HasAttribute(o.GetType());
		}

		void SetOption(Options op, bool value)
		{
			this.options &= ~op;
			if (value) this.options |= op;
		}

		/// <summary>
		/// Retrieves the SerializableClass attribute for the given type.
		/// </summary>
		/// <param name="t">The type to check</param>
		/// <param name="inherit">True to check for inherited attributes</param>
		/// <returns>The SerializableClass attibute for the type, or null if it was not found</returns>
		public static SerializableClassAttribute GetAttribute(Type t, bool inherit)
		{
			object[] attributes = t.GetCustomAttributes(typeof(SerializableClassAttribute), inherit);

			if (attributes.Length > 0)
			{
				return (SerializableClassAttribute)attributes[0];
			}

			return null;
		}
		#endregion
	}
}
