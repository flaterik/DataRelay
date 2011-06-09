using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Configuration;
using System.Xml;
using System.Net;
using System.Net.NetworkInformation;
using MySpace.DataRelay.Common.Schemas;
using System.Net.Sockets;


namespace MySpace.DataRelay.Configuration
{
	[XmlRoot("RelayNodeConfig", Namespace = "http://myspace.com/RelayNodeConfig.xsd")]
	public class RelayNodeConfig
	{
		private static readonly Logging.LogWrapper log = new Logging.LogWrapper();
		
		const string DefaultConfigurationString = "<RelayNodeConfig xmlns=\"http://myspace.com/RelayNodeConfig.xsd\"><UseConfigurationServer>true</UseConfigurationServer></RelayNodeConfig>";
		private static RelayNodeConfig DefaultConfiguration;

		public static RelayNodeConfig GetRelayNodeConfig()
		{
			return GetRelayNodeConfig(null);
		}

		public static RelayNodeConfig GetRelayNodeConfig(EventHandler reloadEventHandler)
		{
			AddReloadEventHandler(reloadEventHandler);

			RelayNodeConfig config = ConfigurationManager.GetSection(RelayNodeSectionHandler.ConfigSectionName) as RelayNodeConfig;

			if (config == null)
				config = GetDefaultConfig();
			
			return config;
		}

		internal static XmlNode GetDefaultConfigNode()
		{
			XmlDocument configDoc = new XmlDocument();
			configDoc.LoadXml(DefaultConfigurationString);
			return configDoc.DocumentElement;
		}
		
		private static RelayNodeConfig GetDefaultConfig()
		{
			if (DefaultConfiguration == null)
			{
				log.Info("No Data Relay configuration found. Getting default client configuration from configuration server.");
				DefaultConfiguration = RelayNodeSectionHandler.GetRelayNodeConfig(GetDefaultConfigNode());
				AddReloadEventHandler(ReloadDefaultConfig);
			}

			return DefaultConfiguration;
		}

		private static void AddReloadEventHandler(EventHandler reloadEventHandler)
		{
			RelayNodeSectionHandler.AddReloadEventHandler(reloadEventHandler);
		}
		
		private static void ReloadDefaultConfig(object state, EventArgs args)
		{
			RelayNodeConfig newConfig = state as RelayNodeConfig;
			if (newConfig != null)
			{
				DefaultConfiguration = newConfig;
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
					RelayNodeDefinition node = GetMyNode();
					if (node != null)
					{
						instanceName = "Port " + node.Port;
					}
					else
					{
						instanceName = "Client";
					}
				}

				return instanceName;

			}
		}
				
		[XmlIgnore]
		public RelayComponentCollection RelayComponents;		

		[XmlIgnore]
		public TypeSettings TypeSettings;

		[XmlIgnore]
		public RelayNodeMapping RelayNodeMapping;

		[XmlIgnore]
		public TransportSettings TransportSettings;

		[XmlIgnore]
		internal XmlNode SectionXml;

		/// <summary>
		/// If "true", use the RelayNodeConfig from configuration server
		/// </summary>
		[XmlElement("UseConfigurationServer")] 
		public bool UseConfigurationServer;

		[XmlElement("ConfigurationServerSectionName")]
		public string ConfigurationServerSectionName = RelayNodeSectionHandler.ConfigSectionName;

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

		[XmlElement("RedirectMessages")]
		public bool RedirectMessages;

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

		[XmlIgnore]
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
		/// Returns the first ipv4 non-loopback address in the list of local addresses. Addresses that don't start with 169 will be favored, because
		/// that's the family for "failed dhcp request". Such addresses are not likely to be the address in use, and are frequently non-routable.
		/// </summary>
		/// <returns></returns>
		public IPAddress GetAddressToUse()
		{
			IPAddress backupAddress = null;//if we find a 169 address but nothing else, use it.
			if (MyAddresses != null && MyAddresses.Count > 0)
			{
				for (int i = 0; i < MyAddresses.Count; i++)
				{
					IPAddress addressToCheck = MyAddresses[i];
					AddressFamily family = addressToCheck.AddressFamily;
					if (family == AddressFamily.InterNetwork &&
						!IPAddress.Loopback.Equals(addressToCheck)
						)
					{
						if (addressToCheck.GetAddressBytes()[0] != 169)
						{
							return addressToCheck;
						}
						//else... 
						backupAddress = addressToCheck;
					}
				}
			}
			
			return backupAddress;
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
				IPAddress environmentIP;
				if (IPAddress.TryParse(environmentIPstring, out environmentIP))
				{
					if (log.IsInfoEnabled)
					{
						log.InfoFormat("Got IPAddress {0} from environment variable \"IPADDRESS\"", environmentIP);
					}
					return environmentIP;
				}
				if (log.IsWarnEnabled)
				{
					log.WarnFormat("Could not parse address {0} from environment variable \"IPADDRESS\"", environmentIPstring);
				}
				return null;
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

		private ushort localZone;
		private bool lookedForZone;
		private object localZoneLock = new object();
		
		public ushort GetLocalZone()
		{
			if (!lookedForZone)
			{
				lock (localZoneLock)
				{
					if (!lookedForZone)
					{
						RelayNodeDefinition myDefinition = GetMyNode();
						if (myDefinition != null && myDefinition.Zone > 0)
						{
							localZone = myDefinition.Zone;
							log.InfoFormat("This server is using the zone override {0} as its local zone", localZone);
						}
						else
						{
							List<IPAddress> addresses;
							int portNumber;
							GetMyNetworkInfo(out addresses, out portNumber);
							localZone = 1;
							if (addresses != null)
							{
								localZone = FindLocalZone(addresses);
								if (localZone != 0)
								{
									log.InfoFormat("This server is using {0} as its local zone", localZone);
								}
								else
								{
									log.Warn("This server was not found in any defined zones and is defaulting to 0");
								}
							}
							else
							{	
								log.Warn("This server was not found in any defined zones and is defaulting to 0");
							}
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
			ushort newlocalZone = 0;
			foreach (IPAddress address in addresses)
			{
				newlocalZone = RelayNodeMapping.ZoneDefinitions.GetZoneForAddress(address);
				
				if (newlocalZone != 0)
				{
					return newlocalZone;
				}
			}
			if (newlocalZone == 0)
			{
				newlocalZone = RelayNodeMapping.ZoneDefinitions.GetZoneForName(Environment.MachineName);
			}
			return newlocalZone;
		}

		private RelayNodeClusterDefinition myCluster;
		private int myClusterIndex;

		private bool lookedForCluster;
		private readonly object myClusterLock = new object();
		public RelayNodeClusterDefinition GetMyCluster()
		{
			if (!lookedForCluster)
			{
				lock (myClusterLock)
				{
					if (!lookedForCluster)
					{
						RelayNodeGroupDefinition group = GetMyGroup();
						if (group != null)
						{
							int portNumber;
							List<IPAddress> addresses;
							GetMyNetworkInfo(out addresses, out portNumber);
							if (portNumber != 0 && addresses != null)
							{
								foreach (IPAddress address in addresses)
								{
									myCluster = group.GetClusterFor(address, portNumber, out myClusterIndex);
									if (myClusterIndex >= 0) break;
								}
							}
						}
						else
						{
							myClusterIndex = -1;
						}
						lookedForCluster = true;
					}
				}
			}
			return myCluster;
		}

		public int GetMyClusterIndex()
		{
			if (!lookedForCluster)
				GetMyCluster();
			return myClusterIndex;
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
						RelayNodeGroupDefinition group = GetMyGroup();
						if (group != null)
						{
							int portNumber;
							List<IPAddress> addresses;
							GetMyNetworkInfo(out addresses, out portNumber);
							if (portNumber != 0 && addresses != null)
							{
								foreach (IPAddress address in addresses)
								{
									myNode = group.GetNodeFor(address, portNumber);
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
		private static readonly Logging.LogWrapper log = new Logging.LogWrapper();

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

		[XmlArray("DecodeExtendedIdTypeIds")]
		[XmlArrayItem("MessageTypeId")]
		public short[] DecodeExtendedIdTypeIds;

		[XmlElement("SampleSeconds")]
		public int SampleSeconds;
	}
}
