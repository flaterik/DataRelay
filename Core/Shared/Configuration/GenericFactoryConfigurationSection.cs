using System;
using MySpace.Common.HelperObjects;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MySpace.Common.Configuration
{
	/// <summary>
	/// Generic, freely locatable configuration for obtaining instances of
	/// any class.
	/// </summary>
	/// <typeparam name="T">Type of the class whose instance is obtained.</typeparam>
	public class GenericFactoryConfigurationSection<T> : ConfigurationSection, IXmlSerializable
		where T : class
	{
		private string _factoryType;
		/// <summary>
		/// Gets or sets the name of the factory class that provides instances.
		/// </summary>
		/// <value>The <see cref="String"/> name of a class that implements
		/// <see cref="GenericFactory{T}"/> with a parameterless constructor.</value>
		/// <remarks>Invokes <see cref="Type.GetType(String)"/> if value is not null.</remarks>
		/// <exception cref="InvalidOperationException">
		/// <para><para>Attempt to set with name of a class that does not have
		/// a public parameterless constructor.</para></para>
		/// </exception>
		public string FactoryType
		{
			get { return _factoryType; }
			set
			{
				_factoryType = value;
				if (string.IsNullOrEmpty(_factoryType))
				{
					_factory = null;
				}
				else
				{
					var factoryType = Type.GetType(FactoryType, true);
					var constructor = factoryType.GetConstructor(Type.EmptyTypes);
					if (constructor == null)
					{
						throw new InvalidOperationException(string.Format(
                			"Type '{0}' does not have available parameterless constructor.",
                			value));
					}
					_factory = (GenericFactory<T>)constructor.Invoke(null);
				}
			}
		}

		/// <summary>
		/// Override in descendent classes to provide a default factory type that is used
		/// if no factory type is specified in the configuration.
		/// </summary>
		/// <returns>The <see cref="String"/> specifying the default factory class;
		/// <see langword="null"/> if there is no default.</returns>
		protected virtual string GetDefaultFactoryType()
		{
			return null;
		}

		private string _elementName = "factoryConfig";
		private string _ns = null;
		private const string FactoryTypeAttributeName = "factoryType";

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
			// record the element name supplied during deserialization
			_elementName = reader.Name;
			_ns = reader.NamespaceURI;
			if (reader.MoveToAttribute(FactoryTypeAttributeName))
			{
				FactoryType = reader.ReadContentAsString();
				reader.MoveToElement();
			} else
			{
				FactoryType = GetDefaultFactoryType();
			}

			if (_factory != null)
			{
				// make subtree for safety
				using (reader = reader.ReadSubtree())
				{
					reader.Read();

					_factory.ReadXml(reader);
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
			if (string.IsNullOrEmpty(_ns))
			{
				writer.WriteStartElement(_elementName);
			}
			else
			{
				writer.WriteStartElement(_elementName, _ns);
			}
			writer.WriteAttributeString(FactoryTypeAttributeName, FactoryType);
			if (_factory != null)
			{
				_factory.WriteXml(writer);
			}
			writer.WriteEndElement();
			return true;
		}

		private GenericFactory<T> _factory;

		/// <summary>
		/// Obtains an instance of <typeparamref name="T"/> from the factory
		/// class specified in <see cref="FactoryType"/>.
		/// </summary>
		/// <returns>An instance of <typeparamref name="T"/>.</returns>
		public T ObtainInstance()
		{
			if (_factory == null)
			{
				throw new ApplicationException("Factory not specified");
			}
			return _factory.ObtainInstance();
		}

		/// <summary>
		/// Obtains an instance of the factory class specified in
		/// <see cref="FactoryType"/>.
		/// </summary>
		/// <returns>An instance of <see name="GenericFactory{T}"/>.</returns>
		public GenericFactory<T> ObtainFactory()
		{
			if (_factory == null)
			{
				throw new ApplicationException("Factory not specified");
			}
			return _factory;
		}

		/// <summary>
		/// Obtains an instance of the section configured from an xml stream.
		/// </summary>
		/// <param name="reader">The <see cref="XmlReader"/> read for configuration.</param>
		/// <returns>A configured instance of
		/// <see name="GenericFactoryConfigurationSection{T}"/>.</returns>
		public static GenericFactoryConfigurationSection<T> FromXml(XmlReader reader)
		{
			var section = new GenericFactoryConfigurationSection<T>();
			using (var subReader = reader.ReadSubtree())
			{
				subReader.Read(); // to move to original element
				((IXmlSerializable)section).ReadXml(reader);
			}
			return section;
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

		/// <summary>
		/// 	<para>Converts an object into its XML representation.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="System.Xml.XmlWriter"/> stream to which the object is serialized.</para>
		/// </param>
		public void WriteXml(XmlWriter writer)
		{
			SerializeElement(writer, false);
		}

		#endregion
	}

	/// <summary>
	/// Generic, freely locatable configuration for obtaining instances of
	/// any class using a default factory type.
	/// </summary>
	/// <typeparam name="T">Type of the class whose instance is obtained.</typeparam>
	/// <typeparam name="TFactory">The default factory type.</typeparam>
	public class GenericFactoryConfigurationSection<T, TFactory> :
		GenericFactoryConfigurationSection<T>
		where T : class
		where TFactory : GenericFactory<T>, new()
	{
		/// <summary>
		/// 	<para>Overriden. Provides a default factory type that is used if no factory type is specified in the configuration.</para>
		/// </summary>
		/// <returns>
		/// 	<para>The <see cref="Type.AssemblyQualifiedName"/> of <typeparamref name="TFactory"/>.</para>
		/// </returns>
		protected override string GetDefaultFactoryType()
		{
			return typeof(TFactory).AssemblyQualifiedName;
		}

		/// <summary>
		/// Obtains an instance of the section configured from an xml stream.
		/// </summary>
		/// <param name="reader">The <see cref="XmlReader"/> read for configuration.</param>
		/// <returns>A configured instance of
		/// <see name="GenericFactoryConfigurationSection{T}"/>.</returns>
		public static new GenericFactoryConfigurationSection<T, TFactory> FromXml(XmlReader reader)
		{
			var section = new GenericFactoryConfigurationSection<T, TFactory>();
			using (var subReader = reader.ReadSubtree())
			{
				subReader.Read(); // to move to original element
				((IXmlSerializable)section).ReadXml(reader);
			}
			return section;
		}
	}
		
}
