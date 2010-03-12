using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Xml;

using MySpace.Logging;

namespace MySpace.BerkeleyDb.Configuration
{
	class BerkeleyDbSectionHandler : IConfigurationSectionHandler
	{
		static readonly LogWrapper Log = new LogWrapper();

		public object Create(object parent, object configContext, XmlNode section)
		{
			XmlSerializer ser = new XmlSerializer(typeof(BerkeleyDbSectionConfig));
			try
			{	
				object configurationObject = ser.Deserialize(new XmlNodeReader(section));
				BerkeleyDbSectionConfig typedConfig = configurationObject as BerkeleyDbSectionConfig;
				System.Configuration.Configuration confFile = ConfigurationManager.OpenExeConfiguration("");
				string basePath = confFile.FilePath;
				if (typedConfig != null)
				{
					foreach (XmlNode node in section.ChildNodes)
					{
						switch (node.Name)
						{
							case "BerkeleyDbConfig":
								BerkeleyDbConfig berkeleyDbConfig = GetSourcedObject<BerkeleyDbConfig>(basePath, node);
								typedConfig.BerkeleyDbConfig = berkeleyDbConfig;
								break;
						}
					}					
					configurationObject = typedConfig;
				}

				return configurationObject;
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error(null, ex);
				}
				return null;				
			}
		}

		private static T GetSourcedObject<T>(string basePath, XmlNode sectionNode) where T : class
		{
			
			T sourcedObject = default(T);
			Type objectType = typeof(T);
			try
			{
				if (Log.IsInfoEnabled)
				{
					Log.Info("Getting sourced config of type " + objectType.FullName);
				}
				XmlSerializer ser = new XmlSerializer(objectType);
				
				string configSource = sectionNode.Attributes["configSource"].Value;
				if (configSource != String.Empty)
				{
					XmlReader reader = XmlReader.Create(Path.Combine(Path.GetDirectoryName(basePath), configSource));
					sourcedObject = ser.Deserialize(reader) as T;
					reader.Close();
				}				
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					StringBuilder sb = new StringBuilder();
					sb.AppendFormat("Error getting sourced config of type {0}: {1}", objectType.FullName, ex);
					Log.Error(sb.ToString(), ex);
				}
			}
			return sourcedObject;
		}
		

	}


	[XmlRoot("BerkeleyDbSectionConfig", Namespace = "http://myspace.com/BerkeleyDbSectionConfig.xsd")]
	public class BerkeleyDbSectionConfig
	{
		static readonly LogWrapper Log = new LogWrapper();

		public static BerkeleyDbSectionConfig GetRelayNodeConfig()
		{
			BerkeleyDbSectionConfig config = null;
			try
			{
				config = ConfigurationManager.GetSection("BerkeleyDbSectionConfig") as BerkeleyDbSectionConfig;
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					StringBuilder sb = new StringBuilder();
					sb.AppendFormat("Exception loading BerkeleyDb Section Config Section: {0}", ex);
					Log.Error(sb.ToString(), ex);
				}
			}
			return config;

		}

		

		[XmlIgnore]
		public BerkeleyDbConfig BerkeleyDbConfig;		
	}
	
}
