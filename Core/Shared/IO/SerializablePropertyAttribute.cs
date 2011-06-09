using System;
using System.Reflection;

namespace MySpace.Common
{
	/// <summary>
	/// Indicates that the property can be serialized and what version
	/// of the class it was added in. This can also be applied to collection-based
	/// classes to specify options for serializing the collection items.
	/// </summary>
	/// <example>
	/// [SerializableClass]
	/// [SerializableProperty(1)]
	/// class MyClass : List&lt;MyItem&gt;
	/// {
	///     [SerializableProperty(2)]
	///     public int MyProperty
	///     {
	///         get { return this.property; }
	///         set { this.property = value; }
	///     }
	/// }
	/// </example>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class SerializablePropertyAttribute : Attribute
	{
		int order = -1;

		/// <summary>
		/// Indicates that the property can be serialized and which version of the class
		/// the property was added in.
		/// </summary>
		/// <param name="version">Version of the class in which the property was added</param>
		public SerializablePropertyAttribute(int version)
		{
			Version = version;
		}

		/// <summary>
		/// Indicates that the property can be serialized and which version of the class
		/// the property was added in.
		/// </summary>
		/// <param name="memberName">The name of the member. If specified, this will be used to determine the order
		/// in which this field or property is serialized within its version number. This field
		/// should be case-insensitive unique and should not change once defined.</param>
		/// <param name="version">Version of the class in which the property was added.</param>
		public SerializablePropertyAttribute(string memberName, int version)
		{
			MemberName = memberName;
			Version = version;
		}

		#region Properties

		/// <summary>
		/// Gets the name of the member. If specified, this will be used to determine the order
		/// in which this field or property is serialized within its version number. This field
		/// should be case-insensitive unique and should not change once defined.
		/// </summary>
		/// <value>The name of the member. If specified, this will be used to determine the order
		/// in which this field or property is serialized within its version number. This field
		/// should be case-insensitive unique and should not change once defined.</value>
		public string MemberName { get; private set; }

		/// <summary>
		/// Returns the version of the class in which this property was added
		/// </summary>
		public int Version { get; private set; }

		/// <summary>
		/// Determines what order the property is serialized relative to other properties from the same version.
		/// This is intended for backwards compatibility only and should not be used for new properties.
		/// By default properties are serialized in alphabetical order.
		/// </summary>
		public int Order
		{
			get { return order; }
			set { order = value; }
		}

		/// <summary>
		/// Specifies if the property will be used as an index to look up the serialized object in the cache.
		/// The default value is <see langword="false"/>.
		/// </summary>
		public bool Index { get; set; }

		public string ReadMethod { get; set; }

		public string WriteMethod { get; set; }

		/// <summary>
		/// Indicates that the type to be serialized should be determined at runtime
		/// instead of using the compiled type information of the property.
		/// </summary>
		/// <remarks>
		/// This can be used to serialize properties where the actual value may be used to
		/// serialize properties/collections where the type is an interface or base
		/// class where the actual value may be one or more derived types.
		/// 
		/// Note that the serializer assumes that the resulting type uses the automatic
		/// serialization mechanism. Other objects are not supported.
		/// </remarks>
		public bool Dynamic { get; set; }

		/// <summary>
		/// Indicates that the property has become obsolete. The property will be omitted
		/// when serializing or deserializing classes whose current version is greater than
		/// or equal to the obsolete version.
		/// </summary>
		public int ObsoleteVersion { get; set; }

		#endregion

		#region Methods
		/// <summary>
		/// Returns <see langword="true"/> if the property has the SerializableProperty attribute
		/// </summary>
		/// <param name="prop">Property to check</param>
		/// <returns>True if the property has the attribute</returns>
		public static bool HasAttribute(MemberInfo prop)
		{
			return prop.IsDefined(typeof(SerializablePropertyAttribute), true);
		}

		/// <summary>
		/// Returns the SerializableProperty attribute for a property.
		/// </summary>
		/// <param name="prop">The property to check</param>
		/// <returns>The attribute if it exists. Null is returned if it does not.</returns>
		public static SerializablePropertyAttribute GetAttribute(MemberInfo prop)
		{
			object[] attributes = prop.GetCustomAttributes(typeof(SerializablePropertyAttribute), true);

			if (attributes.Length > 0)
				return attributes[0] as SerializablePropertyAttribute;
			return null;
		}
		#endregion
	}
}
