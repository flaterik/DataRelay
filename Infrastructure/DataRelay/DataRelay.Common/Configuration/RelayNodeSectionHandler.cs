using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml;
using MySpace.ConfigurationSystem;
using System.Xml.Serialization;
using System.Web;
using MySpace.DataRelay.Common.Schemas;

namespace MySpace.DataRelay.Configuration
{
	class RelayNodeSectionHandler : IConfigurationSectionHandler
	{
		public const string ConfigSectionName = "RelayNodeConfig";

		private static string BasePath;
		private static System.Configuration.Configuration ConfigurationFile;
		private static readonly Logging.LogWrapper log = new Logging.LogWrapper();

		private static readonly List<EventHandler> ReloadEventHandlers = new List<EventHandler>();

		static RelayNodeSectionHandler()
		{
			SetConfigurationFile();
		}

		private static void SetConfigurationFile()
		{
			System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration("");
			ConfigurationFile = config;
			BasePath = Path.GetDirectoryName(config.FilePath);
		}

		public object Create(object parent, object configContext, XmlNode section)
		{
			try
			{
				RelayNodeConfig config = GetRelayNodeConfig(section);

				return config;
			}
			catch (ConfigurationSystemException) //already been logged
			{
				throw;
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Exception getting relay node config: {0}", ex);
				throw; // we want callers to know there was a problem			
			}
		}

		internal static void AddReloadEventHandler(EventHandler reloadEventHandler)
		{
			lock (ReloadEventHandlers)
			{
				if (reloadEventHandler != null && !ReloadEventHandlers.Contains(reloadEventHandler))
					ReloadEventHandlers.Add(reloadEventHandler);
			}
		}

		internal static RelayNodeConfig GetRelayNodeConfig(XmlNode section)
		{
			RelayNodeConfig mainConfig;
			if (string.IsNullOrWhiteSpace(section.InnerXml))
			{
				mainConfig = new RelayNodeConfig();
				mainConfig.UseConfigurationServer = true;
				mainConfig.SectionXml = section;
			}
			else
			{
				mainConfig = DeserializeMainConfig(section);	
			}

			if (mainConfig.UseConfigurationServer) //override with remotely hosted section
				mainConfig = GetMainConfigFromServer(mainConfig.ConfigurationServerSectionName);

			WatchConfig(mainConfig);

			FillSubConfigs(mainConfig);

			return mainConfig;
		}

		private static void ReloadConfig(string name)
		{
			try
			{
				lock (ReloadEventHandlers)
				{
					SetConfigurationFile();
					ConfigurationSection relayNodeConfigSection = ConfigurationFile.GetSection(ConfigSectionName);

					XmlDocument configDoc = new XmlDocument();

					XmlNode configNode;

					if (relayNodeConfigSection != null)
					{
						string configSource = relayNodeConfigSection.SectionInformation.ConfigSource;

						if (configSource == String.Empty)
						{
							configDoc.Load(ConfigurationFile.FilePath);
							configNode = configDoc.SelectSingleNode("*/*[local-name()='RelayNodeConfig']");
						}
						else
						{
							configDoc.Load(GetFilePath(configSource));
							configNode = configDoc.DocumentElement;
						}
					}
					else
					{
						configNode = RelayNodeConfig.GetDefaultConfigNode();
					}

					RelayNodeConfig newConfig = GetRelayNodeConfig(configNode);

					foreach (EventHandler handler in ReloadEventHandlers)
					{
						handler(newConfig, EventArgs.Empty);
					}
				}
			}
			catch (Exception e)
			{   //since this happens on a background thread we really want to swallow it, because otherwise without legacy exception handling 
				//enabled it'll tank the app pool.
				log.ErrorFormat("Exception processing config reload: {0}", e);
			}

		}

		private static RelayNodeConfig GetMainConfigFromServer(string sectionName)
		{
			XmlNode remoteSectionNode = ConfigurationClient.GetSectionXml(sectionName);
			RelayNodeConfig remoteConfig = DeserializeMainConfig(remoteSectionNode);
			//no matter what config server says we need to keep these two properties the same
			remoteConfig.UseConfigurationServer = true;
			remoteConfig.ConfigurationServerSectionName = sectionName;
			return remoteConfig;
		}

		private static RelayNodeConfig DeserializeMainConfig(XmlNode section)
		{
			XmlSerializer ser = new XmlSerializer(typeof(RelayNodeConfig));
			object configurationObject = ser.Deserialize(new XmlNodeReader(section));
			if (!(configurationObject is RelayNodeConfig))
				throw new ConfigurationErrorsException("Relay Node config with xml " + section.OuterXml + " could not be deserialzed");

			RelayNodeConfig mainConfig = configurationObject as RelayNodeConfig;
			mainConfig.SectionXml = section;
			return mainConfig;
		}

		private static void WatchConfig(RelayNodeConfig mainConfig)
		{
			//either way, watch the file that represents it, since it might switch between local and not
			ConfigurationSection relayNodeConfigSection = ConfigurationFile.GetSection(ConfigSectionName);
			string configSource = String.Empty;

			if (relayNodeConfigSection != null)
				configSource = relayNodeConfigSection.SectionInformation.ConfigSource;

			string configPath = String.Empty;
			if (!String.IsNullOrEmpty((configSource)))
			{
				//if there's no config source, then the info is embedded in the app config, and that's the file we need to watch
				if (HttpContext.Current == null)
				{
					//but if there's an httpcontext, then we're in a web context, and you can't update an IIS config file without bouncing
					//the appdomain, so there's no point in watching it.
					configPath = Path.Combine(BasePath, configSource);
				}
			}
			else
			{
				configPath = Path.GetFullPath(ConfigurationFile.FilePath);
			}

			if (!string.IsNullOrEmpty(configPath))
				ConfigurationWatcher.WatchFile(configPath, ReloadConfig);

			//if it came from config server, then register reload notification there
			if (mainConfig.UseConfigurationServer)
			{
				ConfigurationWatcher.WatchRemoteSection(mainConfig.ConfigurationServerSectionName, ReloadConfig);
			}
		}

		

		private static void FillSubConfigs(RelayNodeConfig mainConfig)
		{
			if (mainConfig.SectionXml == null)
				throw new ConfigurationErrorsException("Section Xml on RelayNodeConfig was not available; can't fill sub configs!");

			//TODO: implement defaults for subsections.
			foreach (XmlNode node in mainConfig.SectionXml.ChildNodes)
			{
				switch (node.Name)
				{
					case "RelayComponents":

						RelayComponents comps = GetSubConfig<RelayComponents>(node);
						if (comps != null)
						{
							mainConfig.RelayComponents = comps.RelayComponentCollection;
						}
						else
						{
							if (log.IsErrorEnabled)
								log.Error("No relay component config found.");
						}
						break;
					case "TypeSettings":
						mainConfig.TypeSettings = GetSubConfig<TypeSettings>(node);
						break;
					case "RelayNodeMapping":
						mainConfig.RelayNodeMapping = GetSubConfig<RelayNodeMapping>(node);
						break;
					case "TransportSettings":
						mainConfig.TransportSettings = GetSubConfig<TransportSettings>(node);
						break;
				}
			}
		}

		private static T GetSubConfig<T>(XmlNode sectionNode) where T : class
		{
			if (sectionNode == null || sectionNode.Attributes == null)
				throw new ArgumentNullException("sectionNode");

			string configSource = sectionNode.Attributes["configSource"].Value;

			if (IsLocalConfigSource(configSource))
			{
				return GetSourcedObject<T>(configSource);
			}

			return GetServerSourcedObject<T>(configSource);

		}

		private static bool IsLocalConfigSource(string configSource)
		{
			return configSource.Contains(".config") && File.Exists(GetFilePath(configSource));
		}

		private static string GetFilePath(string configSource)
		{
			return Path.Combine(BasePath, configSource);
		}

		private static T GetServerSourcedObject<T>(string configSource) where T : class
		{
			Type objectType = typeof(T);

			ConfigurationWatcher.WatchRemoteSection(configSource, ReloadConfig);

			XmlNode objectNode = ConfigurationClient.GetSectionXml(configSource);
			XmlSerializer ser = new XmlSerializer(objectType);
			T config = ser.Deserialize(new XmlNodeReader(objectNode)) as T;

			return config;
		}

		private static T GetSourcedObject<T>(string configSource) where T : class
		{
			T sourcedObject = default(T);
			Type objectType = typeof(T);
			try
			{
				XmlSerializer ser = new XmlSerializer(objectType);

				if (!String.IsNullOrEmpty(configSource))
				{
					string path = GetFilePath(configSource);
					XmlReader reader = XmlReader.Create(path);
					sourcedObject = ser.Deserialize(reader) as T;
					reader.Close();
					ConfigurationWatcher.WatchFile(path, ReloadConfig);
				}
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error getting sourced config of type {0}: {1}", objectType.FullName, ex);
				//we want callers to know there was a problem, also all of the config file needs to be loaded,
				//for relay to function
				throw;
			}
			return sourcedObject;
		}
	}
}
