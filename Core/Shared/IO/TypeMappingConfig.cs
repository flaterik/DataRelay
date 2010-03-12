using System.Xml.Serialization;

namespace MySpace.Common.IO
{
	/// <summary>
	/// 	<para>Defines a type id / type mapping used by the <see cref="TypeInfo"/> class.</para>
	/// </summary>
	[XmlRoot(_sectionName, Namespace = _namespace)]
	public class TypeMappingConfig
	{
		private const string _sectionName = "typeMapping";
		private const string _namespace = "http://myspace.com/TypeMappingConfig.xsd";

		/// <summary>
		/// Gets the name of the configuration section.
		/// </summary>
		/// <value>The name of the configuration section.</value>
		public static string SectionName
		{
			get { return _sectionName; }
		}

		/// <summary>
		/// 	<para>Gets or sets the types mapped in this config.</para>
		/// </summary>
		/// <value>
		/// 	<para>The types mapped in this config.</para>
		/// </value>
		[XmlArray("types", Namespace = _namespace)]
		[XmlArrayItem(typeof(TypeInfoConfig), ElementName = "type", Namespace = _namespace)]
		public TypeInfoConfigCollection Types { get; set; }
	}
}
