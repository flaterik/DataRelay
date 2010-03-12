using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using System.Xml;

namespace MySpace.DataRelay.Common.Schemas
{

	[XmlRoot("RelayComponents", Namespace = "http://myspace.com/RelayComponents.xsd")]
	public class RelayComponents
	{
		[XmlArray("RelayComponentCollection")]
		[XmlArrayItem("RelayComponent")]
		public RelayComponentCollection RelayComponentCollection;

		public int Count
		{
			get
			{
				if (RelayComponentCollection != null)
				{
					return RelayComponentCollection.Count;
				}
				return 0;
			}
		}

		public RelayComponent this[int i]
		{
			get
			{
				if (RelayComponentCollection != null)
				{
					return RelayComponentCollection[i];
				}
				return null;
			}
		}
	}
	
	
	public class RelayComponentCollection : KeyedCollection<string, RelayComponent>
	{
		private Dictionary<string, object> loadedConfigs = new Dictionary<string, object>();
		private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
		/// <summary>
		/// default to false, because existing code depends on this.
		/// </summary>
		private bool cachedConfigsEnabled = false;

		/// <summary>
		/// Gets or sets a value indicating if the configs that are loaded from disk are cached.
		/// If true and then changed to false, <see cref="FlushLoaded"/> will be called.
		/// </summary>
		/// <remarks>
		///		<para>One use of caching configs is that enables test code to modify the config in memory.
		///		So that components can use different configuration settings.
		///		</para>
		/// </remarks>
		public bool CachedConfigsEnabled
		{
			get { return this.cachedConfigsEnabled; }
			set
			{
				bool oldValue = this.cachedConfigsEnabled;
				cachedConfigsEnabled = value;

				if (this.cachedConfigsEnabled == false && oldValue == true)
				{
					FlushLoaded();
				}
			}
		}

		protected override string GetKeyForItem(RelayComponent item)
		{
			return item.Name;
		}

		/// <summary>
		/// Flushes all loaded configs such that on a call to <see cref="GetConfigFor"/>,
		/// the config will be reloaded from disk.
		/// </summary>
		public void FlushLoaded()
		{
			loadedConfigs.Clear();
		}

		/// <summary>
		/// Gets the config for the given component name and caches the loaded config.  Multiple calls to the same
		/// <paramref name="componentName"/> will return the same data, unless <see cref="FlushLoaded"/> is
		/// called.
		/// </summary>
		/// <remarks>
		///		<para>If a specific type of ConfigHandler is declared in the configuration file, then
		///		the data will be converted into this type, otherwise, data will be returned as a
		///		<see cref="XmlNode"/> array. </para>
		/// </remarks>
		/// <param name="componentName">The name of the component the retrieve the config for.</param>
		/// <returns>Returns an <see cref="object"/> representing the config.</returns>
		public object GetConfigFor(string componentName)
		{
			object alreadyLoaded;
			if (CachedConfigsEnabled && loadedConfigs.TryGetValue(componentName, out alreadyLoaded))
			{
				return alreadyLoaded;
			}

			RelayComponentConfig config = null;
			object componentConfig = null;
			if (this.Contains(componentName))
			{
				config = this[componentName].Config;
			}
			if (config != null && config.ComponentConfigNodes != null && config.ComponentConfigNodes.Length > 0)
			{
				try
				{
					Type configType = Type.GetType(config.ConfigHandlerType);
					if (configType != null)
					{
						XmlSerializer ser = new XmlSerializer(configType);
						componentConfig = ser.Deserialize(new XmlNodeReader(config.ComponentConfigNodes[0]));
					}
					else
					{
						if(log.IsErrorEnabled)
						{
							log.ErrorFormat("Could not load configuration type {0} for component {1}", config.ConfigHandlerType, componentName);
						}

					}
				}
				catch (Exception ex)
				{
					if (log.IsErrorEnabled)
						log.ErrorFormat("Exception reading config for component {0}: {1}", componentName, ex);
					componentConfig = null;
				}
			}
			if (componentConfig == null && config != null && config.ComponentConfigNodes != null && config.ComponentConfigNodes.Length > 0)
			{
				componentConfig = config.ComponentConfigNodes;
			}

			if (CachedConfigsEnabled)
			{
				loadedConfigs[componentName] = componentConfig;
			}
			return componentConfig;
		}
	}

	public class RelayComponent
	{
		[XmlAttribute("replicator")]
		public bool Replicator;

		[XmlElement("Name")]
		public string Name;
		[XmlElement("Type")]
		public string Type;
		[XmlElement("Version")]
		public string Version = "*.*.*.*";
		[XmlElement("RelayComponentConfig")]
		public RelayComponentConfig Config;

		[XmlElement("InTypeIds")]
		public TypeList InTypeIds = TypeList.Default;
		[XmlElement("OutTypeIds")]
		public TypeList OutTypeIds = TypeList.Default;


	}

	public class TypeList : List<int>, IXmlSerializable
	{
		public static readonly TypeList Default;

		static TypeList()
		{
			Default = new TypeList();
			Default.Add(-1);
		}

		#region IXmlSerializable Members

		public System.Xml.Schema.XmlSchema GetSchema()
		{
			return null;
		}

		public void ReadXml(XmlReader reader)
		{
			string typeListString = null; 
			
			if (reader.IsEmptyElement == false)
			{
				typeListString = reader.ReadString();
				reader.ReadEndElement();
			}
			if(String.IsNullOrEmpty(typeListString))			
			{
				return;
			}
			if (typeListString == "*")
			{
				this.Add(-1);
			}
			else
			{
				string[] typeList = typeListString.Split(',');
				int typeId = 0;
				foreach (string typeString in typeList)
				{
					if (Int32.TryParse(typeString, out typeId))
					{
						this.Add(typeId);
					}
				}
			}
		}

		public void WriteXml(XmlWriter writer)
		{
			if (this.Count > 0 && this[0] == -1)
			{
				writer.WriteString("*");
			}
			else
			{
				for (int i = 0; i < this.Count; i++)
				{
					writer.WriteString(this[i].ToString());
					if (i != (this.Count - 1))
					{
						writer.WriteString(",");
					}
				}
			}
		}

		#endregion

	}

	public class RelayComponentConfig
	{
		[XmlAttribute("ConfigHandlerType")]
		public string ConfigHandlerType;
		[XmlAnyElement]
		public XmlNode[] ComponentConfigNodes;
	}

	
}
