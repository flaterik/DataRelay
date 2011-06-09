using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;


namespace MySpace.BerkeleyDb.Configuration
{
	/// <summary>
	/// A descendent of <see cref="KeyedCollection{TKey, TItem}"/> that implements
	/// <see cref="IXmlSerializable"/> to work around XML serializer issues.
	/// </summary>
	/// <typeparam name="TKey">The type of key.</typeparam>
	/// <typeparam name="TItem">The type of item.</typeparam>
	[Serializable]
	public abstract class XmlSerializableKeyedCollection<TKey, TItem> :
		KeyedCollection<TKey, TItem>, IXmlSerializable
	{
		/// <summary>
		/// Gets the namespace of the collection
		/// </summary>
		/// <returns>The <see cref="String"/> namespace.</returns>
		/// <remarks>There might be some way to automatically determine the proper namespace
		/// via reflection, but this works given development time constraints.</remarks>
		protected abstract string GetNamespace();

		private string _namespace;
		private string Namespace
		{
			get
			{
				if (_namespace == null)
				{
					_namespace = GetNamespace();
				}
				return _namespace;
			}
		}

		private XmlSerializer _itemSerializer;
		private XmlSerializer ItemSerializer
		{
			get
			{
				if (_itemSerializer == null)
				{
					_itemSerializer = new XmlSerializer(typeof(TItem),
						Namespace);
				}
				return _itemSerializer;
			}
		}

		XmlSchema IXmlSerializable.GetSchema()
		{
			return null;
		}

		void IXmlSerializable.ReadXml(XmlReader reader)
		{
			using (var subReader = reader.ReadSubtree())
			{
				subReader.Read();
				while (subReader.Read())
				{
					if (subReader.NodeType == XmlNodeType.Element)
					{
						Add((TItem)ItemSerializer.Deserialize(subReader));
					}
				}
			}
		}

		void IXmlSerializable.WriteXml(XmlWriter writer)
		{
			foreach (var item in Items)
			{
				_itemSerializer.Serialize(writer, item);
			}
		}
	}
}
