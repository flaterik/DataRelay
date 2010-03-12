using System.Xml.Serialization;

namespace MySpace.Common.IO
{
	/// <summary>
	/// 	<para>Defines a relationship between a unique type id and a type.</para>
	/// </summary>
	public class TypeInfoConfig
	{
		/// <summary>
		/// 	<para>Gets or sets a unique id for the type.</para>
		/// </summary>
		/// <value>
		/// 	<para>A unique id for the type.</para>
		/// </value>
		[XmlAttribute("id")]
		public short Id { get; set; }

		/// <summary>
		/// 	<para>Gets or sets the type name; this should be the assembly qualified type name.</para>
		/// </summary>
		/// <value>
		/// 	<para>The type name; this should be the assembly qualified type name.</para>
		/// </value>
		[XmlAttribute("name")]
		public string Name { get; set; }

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
		[XmlAttribute("wellEstablished")]
		public bool WellEstablished { get; set; }
	}
}
