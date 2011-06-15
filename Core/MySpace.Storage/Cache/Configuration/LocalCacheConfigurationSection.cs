using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using MySpace.Common.Configuration;

namespace MySpace.Storage.Cache.Configuration
{
	/// <summary>
	/// Configuration section for <see cref="LocalCache"/>.
	/// </summary>
	public class LocalCacheConfigurationSection : ConfigurationSection, IXmlSerializable
	{
		/// <summary>
		/// Gets or sets the configuration of the type policy.
		/// </summary>
		/// <value>A <see cref="GenericFactoryConfigurationSection{ITypePolicy}"/>.</value>
		public GenericFactoryConfigurationSection<ITypePolicy> TypePolicy { get; set; }

		/// <summary>
		/// Gets or sets the configuration of the object storage.
		/// </summary>
		/// <value>A <see cref="GenericFactoryConfigurationSection{IObjectStorage}"/>.</value>
		public GenericFactoryConfigurationSection<IObjectStorage> Storage { get; set; }
	
		/// <summary>
		/// Gets the specified instance of the local cache configuration.
		/// </summary>
		/// <returns>The <see cref="LocalCacheConfigurationSection"/> configured
		/// under "localCache".</returns>
		public static LocalCacheConfigurationSection GetInstance()
		{
			return (LocalCacheConfigurationSection) ConfigurationManager.GetSection(
				"localCache");
		}

		/// <summary>
		/// 	<para>Overriden. Reads XML from the configuration file.</para>
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="System.Xml.XmlReader"/> that reads from the configuration file.</para>
		/// </param>
		/// <param name="serializeCollectionKey">
		/// 	<para>true to serialize only the collection key properties; otherwise, false.</para>
		/// </param>
		/// <exception cref="System.Configuration.ConfigurationErrorsException">
		/// 	<para>The element to read is locked.                     - or -                     An attribute of the current node is not recognized.                     - or -                     The lock status of the current node cannot be determined.</para>
		/// </exception>
		protected override void DeserializeElement(XmlReader reader, bool serializeCollectionKey)
		{
			using (reader = reader.ReadSubtree())
			{
				while (reader.Read())
				{
					if (reader.NodeType == XmlNodeType.Element)
					{
						switch(reader.Name)
						{
							case "typePolicy":
								TypePolicy = new GenericFactoryConfigurationSection<ITypePolicy>();
								((IXmlSerializable) TypePolicy).ReadXml(reader);
								break;
							case "storage":
								Storage = new GenericFactoryConfigurationSection<IObjectStorage>();
								((IXmlSerializable) Storage).ReadXml(reader);
								break;
						}
					}
				}
			}
		}

		/// <summary>
		/// 	<para>Overriden. Writes the contents of this configuration element to the configuration file when implemented in a derived class.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if any data was actually serialized; otherwise, false.</para>
		/// </returns>
		/// <param name="writer">
		/// 	<para>The <see cref="System.Xml.XmlWriter"/> that writes to the configuration file.</para>
		/// </param>
		/// <param name="serializeCollectionKey">
		/// 	<para>true to serialize only the collection key properties; otherwise, false.</para>
		/// </param>
		/// <exception cref="System.Configuration.ConfigurationErrorsException">
		/// 	<para>The current attribute is locked at a higher configuration level.</para>
		/// </exception>
		protected override bool SerializeElement(XmlWriter writer, bool serializeCollectionKey)
		{
			writer.WriteStartElement("localCache");
			if (TypePolicy != null)
			{
				((IXmlSerializable) TypePolicy).WriteXml(writer);
			}
			if (Storage != null)
			{
				((IXmlSerializable)Storage).WriteXml(writer);
			}
			writer.WriteEndElement();
			return true;
		}

		#region IXmlSerializable Members

		System.Xml.Schema.XmlSchema IXmlSerializable.GetSchema()
		{
			return null;
		}

		void IXmlSerializable.ReadXml(XmlReader reader)
		{
			DeserializeElement(reader, false);
		}

		void IXmlSerializable.WriteXml(XmlWriter writer)
		{
			SerializeElement(writer, false);
		}

		#endregion
	}
}
