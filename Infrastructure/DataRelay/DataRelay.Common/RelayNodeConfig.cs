using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Configuration;
using System.Xml;
using System.Net;
using System.Net.NetworkInformation;
using MySpace.DataRelay.Common.Schemas;
using System.IO;
using System.Web;

using FileStalker = MySpace.Common.ChangeNotification.FileStalker;
using Wintellect.PowerCollections;
using System.Threading;

namespace MySpace.DataRelay.Configuration
{
	class RelayNodeSectionHandler : IConfigurationSectionHandler
	{
		public const string ConfigSectionName = "RelayNodeConfig";
		private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
		private static System.Threading.Timer _reloadTimer;

		static System.Configuration.Configuration GetConfigurationFile()
		{
			System.Configuration.Configuration config;
			config = ConfigurationManager.OpenExeConfiguration("");			
			return config;			
		}
		
		public object Create(object parent, object configContext, XmlNode section)
		{
			
			try
			{
				RelayNodeConfig config = GetRelayNodeConfig(section);

				return config;
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Exception getting relay node config: {0}", ex);
				throw; // we want callers to know there was a problem			
			}
		}

		internal static RelayNodeConfig GetRelayNodeConfig(XmlNode section)
		{
			//XPathNavigator nav = section.CreateNavigator();
			XmlSerializer ser = new XmlSerializer(typeof(RelayNodeConfig));
			object configurationObject;
			configurationObject = ser.Deserialize(new XmlNodeReader(section));
			RelayNodeConfig typedConfig = configurationObject as RelayNodeConfig;

			System.Configuration.Configuration confFile = GetConfigurationFile();

			string basePath = confFile.FilePath;

			if (typedConfig != null)
			{
				#region Get Sub Configs
				foreach (XmlNode node in section.ChildNodes)
				{
					switch (node.Name)
					{
						case "RelayComponents":
							RelayComponents comps = GetSourcedObject<RelayComponents>(basePath, node);
							if (comps != null)
							{
								typedConfig.RelayComponents = comps.RelayComponentCollection;
							}
							else
							{
								if (log.IsErrorEnabled)
									log.Error("No relay component config found.");
							}
							break;
						case "TypeSettings":
							TypeSettings typeSettings = GetSourcedObject<TypeSettings>(basePath, node);
							if (typeSettings.TypeSettingCollection.Count == 0)
							{
								// attempt custom load
								typeSettings = TypeSettingsConfigLoader.Load(basePath, node);
							}
							typedConfig.TypeSettings = typeSettings;
							
							break;
						case "RelayNodeMapping":
							RelayNodeMapping nodeMapping = GetSourcedObject<RelayNodeMapping>(basePath, node);
							typedConfig.RelayNodeMapping = nodeMapping;
							break;
						case "TransportSettings":
							TransportSettings transportSettings = GetSourcedObject<TransportSettings>(basePath, node);
							typedConfig.TransportSettings = transportSettings;
							break;
					}
				}
				#endregion

				if (System.Web.HttpContext.Current == null) //not a web project, doesn't apply					
				{
					WatchConfigFiles();
				}
				else
				{
					SectionInformation info = confFile.GetSection(RelayNodeSectionHandler.ConfigSectionName).SectionInformation;
					if (!info.RestartOnExternalChanges)
					{
						WatchConfigFiles();
					}
				}
			}

			return typedConfig;

		}

		private static T GetSourcedObject<T>(string basePath, XmlNode sectionNode) where T : class
		{

			T sourcedObject = default(T);
			Type objectType = typeof(T);
			try
			{										
				XmlSerializer ser = new XmlSerializer(objectType);

				string configSource = sectionNode.Attributes["configSource"].Value;
				if(!String.IsNullOrEmpty(configSource))				
				{
					XmlReader reader = XmlReader.Create(Path.Combine(Path.GetDirectoryName(basePath), configSource));
					sourcedObject = ser.Deserialize(reader) as T;
					reader.Close();
				}
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error getting sourced config of type {0}: {1}", objectType.FullName, ex);
				// we want callers to know there was a problem, also all of the config file needs to be loaded,
				//for relay to function
				throw; 
			}
			return sourcedObject;
		}

		#region Config Watching
		static object configWatcherLock = new object();
		static List<FileSystemWatcher> configurationWatchers = null;
		static object configLoadLock = new object();
		static Set<string> pendingConfigReloads = new Set<string>(StringComparer.OrdinalIgnoreCase);
		static Set<string> watchedConfigFiles = new Set<string>(StringComparer.OrdinalIgnoreCase);

		internal static List<EventHandler> ReloadEventHandlers = new List<EventHandler>();
		//internal static EventHandler ReloadEventHandler;

		private static void WatchConfigFiles()
		{
			System.Configuration.Configuration configurationFile = GetConfigurationFile();
			AddWatchedFiles(configurationFile);
			lock (configWatcherLock)
			{
				foreach (FileSystemWatcher configurationWatcher in configurationWatchers)
				{
					configurationWatcher.Changed += new FileSystemEventHandler(ConfigDirChanged);
					configurationWatcher.Deleted += new FileSystemEventHandler(ConfigDirChanged);
					configurationWatcher.Created += new FileSystemEventHandler(ConfigDirChanged);
					configurationWatcher.EnableRaisingEvents = true;
				}
			}
		}

		private static string GetConfigFilePath(System.Configuration.Configuration confFile, ConfigurationSection relayNodeConfigSection)
		{
			string configSource = relayNodeConfigSection.SectionInformation.ConfigSource;
			if (String.IsNullOrEmpty(configSource))
			{
				return Path.GetFullPath(confFile.FilePath);				
			}
			else
			{
				return Path.Combine(Path.GetDirectoryName(confFile.FilePath), configSource);
				
			}
		}

		private static void AddWatchedFiles(System.Configuration.Configuration confFile)
		{
			Set<string> watchedFolders = new Set<string>();
			lock(configWatcherLock)
			{
				if (configurationWatchers != null)
				{
					foreach (FileSystemWatcher watcher in configurationWatchers)
					{
						watcher.EnableRaisingEvents = false;
					}
					configurationWatchers.Clear();
				}
				configurationWatchers = new List<FileSystemWatcher>();
			}

			ConfigurationSection relayNodeConfigSection = confFile.GetSection(RelayNodeSectionHandler.ConfigSectionName);
			string configFilePath = GetConfigFilePath(confFile, relayNodeConfigSection);


			string configSource = relayNodeConfigSection.SectionInformation.ConfigSource;
			XmlDocument confXml = new XmlDocument();
			XmlNodeList sourcedNodes;
			
			if (String.IsNullOrEmpty(configSource))
			{
				if (log.IsInfoEnabled)
					log.InfoFormat("Config is not sourced, watching main configuration file {0}", configFilePath);
				//without the xmlns in the xml this would be much simpler....
				sourcedNodes = confXml.SelectNodes("*/*[local-name()='RelayNodeConfig']/*[@configSource]");
			}
			else
			{
				if (log.IsInfoEnabled)
					log.InfoFormat("Watching sourced config file {0}", configFilePath);			
				sourcedNodes = confXml.SelectNodes("//*[@configSource]");
			}

			watchedFolders.Add(Path.GetDirectoryName(configFilePath));
			watchedConfigFiles.Add(Path.GetFileName(configFilePath));
			confXml.Load(configFilePath);

			if (sourcedNodes != null)
			{
				//This will only pass if the application is a web app and it is running on a unc
				if (HttpRuntime.IsOnUNCShare) 
				{
					List<FileStalker> stalkers = new List<FileStalker>();

					foreach (XmlNode node in sourcedNodes)
					{
						string sourceFile = node.Attributes["configSource"].Value;
						string configPath = null;

						if (Path.IsPathRooted(sourceFile))
						{
							configPath = sourceFile;
						}
						else
						{
							configPath = Path.Combine(HttpRuntime.AppDomainAppPath, sourceFile);
						}
						string configName = Path.GetFileName(configPath);

						FileStalker stalker = new FileStalker(configPath);
						stalker.FileModified += new EventHandler<MySpace.Common.ChangeNotification.FileModifiedEventArgs>(stalker_FileModified);
						stalkers.Add(stalker);
						watchedConfigFiles.Add(configName);
					}

					stalkers = Stalkers;
				}
				else
				{
					foreach (XmlNode node in sourcedNodes)
					{
						string sourceFile = node.Attributes["configSource"].Value;
						string sourcePath = Path.Combine(Path.GetDirectoryName(confFile.FilePath), sourceFile);
						watchedFolders.Add(Path.GetDirectoryName(sourcePath));
						watchedConfigFiles.Add(Path.GetFileName(sourcePath));
						if (log.IsInfoEnabled)
							log.InfoFormat("Watching configuration file {0}", sourcePath);
						AddWatchedFiles(sourcePath, watchedConfigFiles, watchedFolders);
					}
				}

			}

			if (!HttpRuntime.IsOnUNCShare)
			{
				lock (configWatcherLock)
				{
					foreach (string folder in watchedFolders)
					{
						configurationWatchers.Add(new FileSystemWatcher(folder, "*.config"));
					}
				}
			}
		}

		private static List<FileStalker> Stalkers = new List<FileStalker>();

		static void stalker_FileModified(object sender, MySpace.Common.ChangeNotification.FileModifiedEventArgs e)
		{
			if (!string.IsNullOrEmpty(e.FilePath))
			{
				string configName = Path.GetFileName(e.FilePath);
				QueueConfigReload(configName);
			}
		}

		private static void AddWatchedFiles(string confFilePath, Set<string> watchedConfigFiles, Set<string> watchedFolders)
		{
			XmlDocument sourceDoc = new XmlDocument();
			sourceDoc.Load(confFilePath);
			List<XmlNode> nodes = new List<XmlNode>();

			foreach (XmlNode node in sourceDoc.ChildNodes)
			{
				AddNodesToList(node, nodes);
			}

			foreach (XmlNode node in nodes)
			{
				if (node != null)
				{
					if (node.Attributes != null)
					{
						if (node.Attributes["configSource"] != null)
						{
							string sourceFile = node.Attributes["configSource"].Value;
							string sourcePath = Path.Combine(Path.GetDirectoryName(confFilePath), sourceFile);
							watchedFolders.Add(Path.GetDirectoryName(sourcePath));
							watchedConfigFiles.Add(Path.GetFileName(sourcePath));
							if (log.IsInfoEnabled)
								log.InfoFormat("Watching configuration file {0}", sourcePath);
							AddWatchedFiles(sourcePath, watchedConfigFiles, watchedFolders);
						}
					}
				}
			}
		}

		private static void AddNodesToList(XmlNode node, List<XmlNode> nodes)
		{
			nodes.Add(node);
			foreach (XmlNode child in node.ChildNodes)
			{
				AddNodesToList(child, nodes);
			}
		}

		static void QueueConfigReload(string name)
		{
			lock (configLoadLock)
			{
				if (pendingConfigReloads.Contains(name) || !watchedConfigFiles.Contains(name))
				{
					return;
				}

				if (pendingConfigReloads.Count == 0)
				{
					if (log.IsInfoEnabled)
						log.InfoFormat("Config file {0} changed. Processing in five seconds.", name);
					pendingConfigReloads.Add(name);

					//assigning to a static variable because you need to keep a reference to timers to keep them from being GC'd and breaking
					_reloadTimer = new Timer(DelayProcessConfigChange, name.ToLower(), 5000, Timeout.Infinite); 
					
				}
				else
				{
					if(log.IsInfoEnabled)
						log.InfoFormat("Config file {0} changed. Processing with current unprocessed changes.", name);
					pendingConfigReloads.Add(name);
					return;
				}
			}
		}

		static void ConfigDirChanged(object sender, FileSystemEventArgs e)
		{
			QueueConfigReload(e.Name);
		}

		static void DelayProcessConfigChange(object ar)
		{
			lock (configLoadLock)
			{				
				pendingConfigReloads.Clear();
				try
				{
					System.Configuration.Configuration conf = GetConfigurationFile();
					ConfigurationSection confSection = conf.GetSection(ConfigSectionName);
					string configFilePath = GetConfigFilePath(conf, confSection);
					XmlDocument configDoc = new XmlDocument();
					configDoc.Load(configFilePath);
					XmlNode configNode;
					if (confSection.SectionInformation.ConfigSource == String.Empty)
					{
						configNode = configDoc.SelectSingleNode("*/*[local-name()='RelayNodeConfig']");
					}
					else
					{
						configNode = configDoc.DocumentElement;
					}
					RelayNodeConfig relayNodeConfig = GetRelayNodeConfig(configNode);
					//object configObject = System.Configuration.ConfigurationManager.GetSection(RelayNodeSectionHandler.ConfigSectionName); 
					System.Configuration.ConfigurationManager.RefreshSection(RelayNodeSectionHandler.ConfigSectionName);//this will retrigger the watch
					foreach (EventHandler handler in ReloadEventHandlers)
					{
						handler(relayNodeConfig, EventArgs.Empty);
					}                    
				}
				catch (Exception ex)
				{
					if (log.IsErrorEnabled)
						log.ErrorFormat("Exception processing relay system config reload: {0}", ex);
				}
			}	
		}

		#endregion

		
	}
	
	
	[XmlRoot("RelayNodeConfig", Namespace = "http://myspace.com/RelayNodeConfig.xsd")]
	public class RelayNodeConfig
	{
		private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
		public static RelayNodeConfig GetRelayNodeConfig()
		{
			return GetRelayNodeConfig(null);
		}
		
		public static RelayNodeConfig GetRelayNodeConfig(EventHandler reloadEventHandler)
		{
			RelayNodeConfig config = null;
			try
			{
				AddReloadEventHandler(reloadEventHandler);               

				config = System.Configuration.ConfigurationManager.GetSection(RelayNodeSectionHandler.ConfigSectionName) as RelayNodeConfig;				
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Exception loading Relay Node Config Section: {0}", ex);
				throw;  // we want callers to know something went wrong.
			}
			return config;

		}

		private static void AddReloadEventHandler(EventHandler reloadEventHandler)
		{   
			if (reloadEventHandler != null && 
				!RelayNodeSectionHandler.ReloadEventHandlers.Contains(reloadEventHandler))
			{
				RelayNodeSectionHandler.ReloadEventHandlers.Add(reloadEventHandler);
			}
			
		}
		
		public RelayNodeConfig()
		{
			OutMessagesOnRelayThreads = false; //default value
		}

		private string instanceName;		
		public string InstanceName
		{
			get
			{
				if (instanceName == null)
				{
					RelayNodeDefinition myNode = GetMyNode();
					if (myNode != null)
					{
						instanceName = "Port " + myNode.Port.ToString();
					}
					else
					{
						instanceName = "Client";
					}
				}

				return instanceName;

			}
		}
				
		[XmlIgnore()]
		public RelayComponentCollection RelayComponents;		

		[XmlIgnore()]
		public TypeSettings TypeSettings;

		[XmlIgnore()]
		public RelayNodeMapping RelayNodeMapping;

		[XmlIgnore()]
		public TransportSettings TransportSettings;

		[XmlElement("OutputTraceInfo")]
		public bool OutputTraceInfo;

		[XmlElement("TraceSettings")]
		public TraceSettings TraceSettings;

		[XmlElement("OutMessagesOnRelayThreads")]
		public bool OutMessagesOnRelayThreads;
		
		/// <summary>
		/// The number of RelayNode threads dedicated to in messages.
		/// </summary>
		[XmlElement("NumberOfThreads")]
		public int NumberOfThreads = 1;

		/// <summary>
		/// The number of RelayNode threads dedicated to out messages.
		/// </summary>
		[XmlElement("NumberOfOutMessageThreads")]
		public int NumberOfOutMessageThreads = 1;

		[XmlElement("MaximumMessageQueueDepth")]
		public int MaximumMessageQueueDepth = 100000;

		/// <summary>
		/// The maximum number of messages queued in RelayNode before throttling.
		/// </summary>
		[XmlElement("MaximumOutMessageQueueDepth")]
		public int MaximumOutMessageQueueDepth = 50000;

		/// <summary>
		///	<para>The timeout, in seconds, to wait after a fatal shutdown
		///	has been signalled before killing the app domain.</para>
		/// </summary>
		[XmlElement("FatalShutdownTimeout")]
		public int FatalShutdownTimeout = 300;

		/// <summary>
		/// When a expired object is detected, the node will process a delete message for that object if this is true.
		/// </summary>
		[XmlElement("SendExpirationDeletes")]
		public bool SendExpirationDeletes;

		[XmlArray("IgnoredMessageTypes")]
		[XmlArrayItem("MessageType")]
		public string[] IgnoredMessageTypes;

		public RelayNodeGroupDefinition GetNodeGroupForTypeId(short typeId)
		{
			RelayNodeGroupDefinition group = null;
			if (RelayNodeMapping != null && TypeSettings != null)
			{
				string groupName = TypeSettings.TypeSettingCollection.GetGroupNameForId(typeId);
				if (groupName != null)
				{
					if (RelayNodeMapping.RelayNodeGroups.Contains(groupName))
					{
						group = RelayNodeMapping.RelayNodeGroups[groupName];
					}
				}
				
			}

			return group;

		}

		[XmlIgnore()]
		public List<IPAddress> MyAddresses
		{
			get
			{
				return myAddresses;
			}
		}

		List<IPAddress> myAddresses;
		object addressLock = new object();
		public void GetMyNetworkInfo(out List<IPAddress> addresses, out int portNumber)
		{
			if (TransportSettings != null)
			{
				portNumber = TransportSettings.ListenPort;
			}
			else
			{
				portNumber = 0;
			}
			if (myAddresses == null)
			{
				lock (addressLock)
				{
					if (myAddresses == null)
					{
						myAddresses = new List<IPAddress>();

						IPAddress environmentDefinedAddress = GetEnvironmentDefinedAddress();
						if (environmentDefinedAddress != null)
						{
							myAddresses.Add(environmentDefinedAddress);
						}

						NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
						foreach (NetworkInterface networkInterface in interfaces)
						{
							IPInterfaceProperties props = networkInterface.GetIPProperties();
							
							foreach (UnicastIPAddressInformation addressInfo in props.UnicastAddresses)
							{
								myAddresses.Add(addressInfo.Address);
							}
						}
					}
				}
			}
			addresses = myAddresses;
			
		}

		/// <summary>
		/// Our Xen hosted machines all think they have the same IP address at the NIC level. When 
		/// they boot, an Environment variable "IPADDRESS" is set to give us a locally visible
		/// copy of their external IP address.
		/// </summary>
		/// <returns></returns>
		private IPAddress GetEnvironmentDefinedAddress()
		{
			try
			{
				string environmentIPstring = Environment.GetEnvironmentVariable("IPADDRESS", EnvironmentVariableTarget.Machine);
				if (String.IsNullOrEmpty(environmentIPstring))
				{
					return null;
				}
				else
				{
					IPAddress environmentIP;
					if (IPAddress.TryParse(environmentIPstring, out environmentIP))
					{
						if (log.IsInfoEnabled)
						{
							log.InfoFormat("Got IPAddress {0} from environment variable \"IPADDRESS\"", environmentIP);
						}
						return environmentIP;
					}
					else
					{
						if (log.IsWarnEnabled)
						{
							log.WarnFormat("Could not parse address {0} from environment variable \"IPADDRESS\"", environmentIPstring);
						}
						return null;
					}
				}
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled)
				{
					log.ErrorFormat("Exception getting IP address from environment variable \"IPAddress\": {0}", e); 
				}
				return null;
			}
		}

		private ushort localZone = 0;
		private bool lookedForZone = false;
		private object localZoneLock = new object();
		public ushort GetLocalZone()
		{
			if (!lookedForZone)
			{
				lock (localZoneLock)
				{
					if (!lookedForZone)
					{
						int portNumber;
						List<IPAddress> addresses;
						GetMyNetworkInfo(out addresses, out portNumber);
						localZone = 1;
						if (addresses != null)
						{
							localZone = FindLocalZone(addresses);
							if (localZone != 0)
							{
								if (log.IsInfoEnabled)
									log.InfoFormat("This server is using {0} as its local zone", localZone);
							}
							else
							{
								if (log.IsWarnEnabled)
									log.Warn("This server was not found in any defined zones and is defaulting to 0");
							}
						}
						else
						{
							if (log.IsWarnEnabled)
								log.Warn("This server was not found in any defined zones and is defaulting to 0");
						}
						lookedForZone = true;
					}
				}
			}
			return localZone;
		}
		
		private RelayNodeGroupDefinition myGroup;
		private bool lookedForGroup;
		private object myGroupLock = new object();
		public RelayNodeGroupDefinition GetMyGroup()
		{
			if(!lookedForGroup)
			{
				lock(myGroupLock)
				{
					if (!lookedForGroup)
					{						
						int portNumber;						
						List<IPAddress> addresses;
						GetMyNetworkInfo(out addresses, out portNumber);
						if (portNumber == 0)
						{
							if (log.IsInfoEnabled)
								log.Info("This server is not listening and will act as a client.");                            
						}
						else if (addresses != null)
						{
							if(log.IsInfoEnabled)
								log.Info("The Relay Node Mapping is looking for group containing this server.");
							myGroup = RelayNodeMapping.RelayNodeGroups.GetGroupContaining(addresses, portNumber);
							if (log.IsInfoEnabled)
							{
								if (myGroup != null)
								{
									log.InfoFormat("This server is in group {0}", myGroup.Name);
								}
								else
								{
									log.InfoFormat("This server is not in any defined groups and will act as a client.");
								}
							}
						}
					   
						lookedForGroup = true;
					}
				}
			}
			return myGroup;
		}

		private ushort FindLocalZone(List<IPAddress> addresses)
		{   
			ushort localZone = 0;
			foreach (IPAddress address in addresses)
			{
				localZone = RelayNodeMapping.ZoneDefinitions.GetZoneForAddress(address);
				
				if (localZone != 0)
				{
					return localZone;
				}
			}
			if (localZone == 0)
			{
				localZone = RelayNodeMapping.ZoneDefinitions.GetZoneForName(Environment.MachineName);
			}
			return localZone;
		}

		private RelayNodeClusterDefinition myCluster;
		private bool lookedForCluster;
		private object myClusterLock = new object();
		public RelayNodeClusterDefinition GetMyCluster()
		{
			if (!lookedForCluster)
			{
				lock (myClusterLock)
				{
					if (!lookedForCluster)
					{
						RelayNodeGroupDefinition myGroup = GetMyGroup();
						if (myGroup != null)
						{
							int portNumber;
							List<IPAddress> addresses;
							GetMyNetworkInfo(out addresses, out portNumber);
							if (portNumber != 0 && addresses != null)
							{
								foreach (IPAddress address in addresses)
								{
									myCluster = myGroup.GetClusterFor(address, portNumber);									
									if (myCluster != null)
									{
										break;
									}
								}
							}
						}
						lookedForCluster = true;
					}
				}
			}
			return myCluster;
		}

		private RelayNodeDefinition myNode;
		private bool lookedForNode;
		private object myNodeLock = new object();
		public RelayNodeDefinition GetMyNode()
		{
			if (!lookedForNode)
			{
				lock (myNodeLock)
				{
					if (!lookedForNode)
					{
						RelayNodeGroupDefinition myGroup = GetMyGroup();
						if (myGroup != null)
						{
							int portNumber;
							List<IPAddress> addresses = new List<IPAddress>();
							GetMyNetworkInfo(out addresses, out portNumber);
							if (portNumber != 0 && addresses != null)
							{
								foreach (IPAddress address in addresses)
								{
									myNode = myGroup.GetNodeFor(address, portNumber);
									if (myNode != null)
									{
										break;
									}
								}
							}							
						}
						lookedForNode = true;
					}
				}
			}
			return myNode;
		}
	}

	public class TraceSettings
	{
		private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();

		[XmlElement("WriteToDiagnostic")] 
		public bool WriteToDiagnostic = true;

		[XmlElement("TraceFilename")]
		public string TraceFilename;
		
		[XmlArray("TracedMessageTypes")]
		[XmlArrayItem("MessageType")]
		public string[] TracedMessageTypes;

		public MessageType[] GetTracedMessageTypeEnums()
		{
			if (TracedMessageTypes == null)
				return null;

			MessageType[] tracedTypes = new MessageType[TracedMessageTypes.Length];
			for (int i = 0; i < TracedMessageTypes.Length; i++)
			{
				try
				{
					tracedTypes[i] = (MessageType)Enum.Parse(typeof(MessageType), TracedMessageTypes[i], true);
				}
				catch (Exception e)
				{
					log.WarnFormat("Exception parsing traced message type '{0}': {1}", TracedMessageTypes[i], e.Message);
				}
			}

			return tracedTypes;
		}

		[XmlArray("TracedMessageTypeIds")]
		[XmlArrayItem("MessageTypeId")]
		public short[] TracedMessageTypeIds;

		[XmlElement("SampleSeconds")]
		public int SampleSeconds;
	}
}
