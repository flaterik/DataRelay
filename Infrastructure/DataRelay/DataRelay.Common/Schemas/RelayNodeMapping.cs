using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using MySpace.Logging;
using MySpace.Configuration;

namespace MySpace.DataRelay.Common.Schemas
{
	[XmlRoot("RelayNodeMapping", Namespace = "http://myspace.com/RelayNodeMapping.xsd")]
	public class RelayNodeMapping
	{
		internal static readonly LogWrapper log = new LogWrapper();

		[XmlArray("ZoneDefinitions")]
		[XmlArrayItem("Zone")]
		public ZoneDefinitionCollection ZoneDefinitions;

		[XmlArray("RelayNodeGroups")]
		[XmlArrayItem("RelayNodeGroup")]
		public RelayNodeGroupDefinitionCollection RelayNodeGroups;

		private bool checkedValidity;
		private bool isValid;
		private readonly object validityLock = new object();
		public bool Validate()
		{
			if (!checkedValidity)
			{
				lock (validityLock)
				{
					if (!checkedValidity)
					{
						isValid = DoValidityCheck();
						checkedValidity = true;
					}
				}
			}

			return isValid;
		}

		private bool DoValidityCheck()
		{
			Dictionary<string, bool> definedNodes = new Dictionary<string, bool>();
			foreach (RelayNodeGroupDefinition groupDefinition in RelayNodeGroups)
			{
				foreach (RelayNodeClusterDefinition clusterDefinition in groupDefinition.RelayNodeClusters)
				{
					foreach (RelayNodeDefinition nodeDefinition in clusterDefinition.RelayNodes)
					{
						if (definedNodes.ContainsKey(nodeDefinition.ToString()))
						{
							if (log.IsErrorEnabled)
								log.ErrorFormat("Node mapping failed validation because of duplicate node {0} in group {1}", nodeDefinition, groupDefinition.Name);
							return false;
						}
						else
						{
							definedNodes.Add(nodeDefinition.ToString(), true);
						}
					}
				}
			}
			return true;
		}
	}

	/// <summary>
	/// Represents those configuration values specific to a given environment.
	/// </summary>
	public class EnvironmentDefinition
	{
		private string _environmentNames;
		private bool _environmentNamesClean;

		/// <summary>
		/// The names of the environments this definition applies to delimited by commas.
		/// Use <see cref="EnvironmentNames"/> to access them seperately.
		/// </summary>
		[XmlAttribute("names")]
		public string EnvironmentNamesRaw
		{
			get
			{
				if (_environmentNamesClean == false)
				{
					_environmentNames = (_environmentNames ?? string.Empty).Replace(" ", "").ToLower();
					_environmentNamesClean = true;
				}
				return _environmentNames;
			}
			set { _environmentNames = value; }
		}

		private string[] _environments;

		/// <summary>
		/// Gets a list of the environment names.
		/// </summary>
		public string[] EnvironmentNames
		{
			get
			{
				if (_environments == null)
				{
					string[] pieces = EnvironmentNamesRaw.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
					_environments = pieces;
				}
				return _environments;
			}
		}

		/// <summary>
		/// Gets a value indicating if the given environment matches this <see cref="EnvironmentDefinition"/>.
		/// </summary>
		/// <param name="environment">The environment to check for.</param>
		/// <returns>True if this instance matches the given environment; otherwise, false.</returns>
		public bool IsMatch(string environment)
		{
			string[] environments = EnvironmentNames;
			string checkFor = (environment ?? string.Empty).ToLower().Trim();
			foreach (string item in environments)
			{
				if (checkFor == item)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Gets or set the clusters for these environments.
		/// </summary>
		[XmlArray("RelayNodeClusters")]
		[XmlArrayItem("RelayNodeCluster")]
		public RelayNodeClusterDefinition[] RelayNodeClusters { get; set; }

		/// <summary>
		/// Gets or sets the socket settings.
		/// </summary>
		[XmlElement("SocketSettings")]
		public SocketSettings SocketSettings { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="QueueConfig"/>.
		/// </summary>
		[XmlElement("QueueConfig")]
		public QueueConfig QueueConfig { get; set; }
	}

	/// <summary>
	/// Represents a list of Zone definitions
	/// </summary>
	public class ZoneDefinitionCollection : List<Zone>
	{
		/// <summary>
		/// Returns the Zone Id for the supplied address, or -1 if it is not found.
		/// </summary>
		/// <param name="address"></param>
		/// <returns></returns>
		public ushort GetZoneForAddress(IPAddress address)
		{
			foreach (Zone zone in this)
			{
				if (zone.ContainsAddress(address))
				{
					return zone.Id;
				}
			}
			return 0;
		}

		public ushort GetZoneForName(string machineName)
		{
			foreach (Zone zone in this)
			{
				if (zone.ContainsName(machineName))
				{
					return zone.Id;
				}
			}
			return 0;
		}
		public override bool Equals(object obj)
		{
			ZoneDefinitionCollection comparor = obj as ZoneDefinitionCollection;

			if (comparor == null)
			{
				return false;
			}

			if (comparor.Count != Count)
			{
				return false;
			}

			for (int i = 0; i < Count; i++)
			{
				if (!this[i].Equals(comparor[i]))
				{
					return false;
				}
			}
			return true;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

	}

	/// <summary>
	/// Represents a single zone definition, which is an Id and zero or more subnets as strings.
	/// </summary>
	public class Zone
	{
		[XmlAttribute("Id")]
		public ushort Id;

		[XmlArray("Subnets")]
		[XmlArrayItem("Subnet")]
		public string[] Subnets;

		[XmlArray("Prefixes")]
		[XmlArrayItem("Prefix")]
		public string[] Prefixes;

		public bool ContainsAddress(IPAddress address)
		{
			if (Subnets != null)
			{
				foreach (string subnet in Subnets)
				{
					if (AddressInSubnet(address, subnet))
					{
						return true;
					}
				}
			}
			return false;
		}

		public bool ContainsName(string machineName)
		{
			if (Prefixes != null)
			{
				foreach (string prefix in Prefixes)
				{
					if (prefix != null)
					{
						if (machineName.ToLower().StartsWith(prefix.ToLower()))
						{
							return true;
						}
					}
				}

			}
			return false;
		}

		private static bool AddressInSubnet(IPAddress address, string subnet)
		{
			if (address.AddressFamily == AddressFamily.InterNetwork)
			{
				string[] addyOctets = address.ToString().Split('.');
				string[] zoneOctets = subnet.Split('.');
				for (int i = 0; i < zoneOctets.Length; i++)
				{
					if (!string.Equals(zoneOctets[i], "*") && //use * or 0 for wildcards, if this zone octet is a wildcard accept it
						 !string.Equals(zoneOctets[i], "0") &&
						 !string.Equals(addyOctets[i], zoneOctets[i]))
					{
						return false;
					}
				}
				return true;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			Zone comparor = obj as Zone;
			if (comparor == null)
			{
				return false;
			}
			if (Id != comparor.Id)
			{
				return false;
			}
			if ((Subnets == null && comparor.Subnets != null)
				 || (comparor.Subnets == null && Subnets != null))
			{
				return false;
			}
			if (Subnets == null)
			{
				return true;
			}
			for (int i = 0; i < Subnets.Length; i++)
			{
				if (!Subnets[i].Equals(comparor.Subnets[i]))
				{
					return false;
				}
			}
			return true;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

	}

	public class RelayNodeGroupDefinitionCollection : KeyedCollection<string, RelayNodeGroupDefinition>
	{
		protected override string GetKeyForItem(RelayNodeGroupDefinition item)
		{
			return item.Name;
		}

		public RelayNodeGroupDefinition GetGroupContaining(List<IPAddress> addressList, int listenPort)
		{
			RelayNodeGroupDefinition myGroup = null;
			foreach (IPAddress address in addressList)
			{
				foreach (RelayNodeGroupDefinition group in this)
				{
					if (group.ContainsNode(address, listenPort))
					{
						myGroup = group;
						break;
					}
				}
				if (myGroup != null)
				{
					break;
				}
			}
			return myGroup;
		}
	}

	public class RelayNodeGroupDefinition
	{
		[XmlAttribute("Activated")]
		public bool Activated;
		[XmlAttribute("Name")]
		public string Name;
		[XmlAttribute("DangerZoneSeconds")]
		public int DangerZoneSeconds;
		[XmlAttribute("DangerZoneThreshold")]
		public int DangerZoneThreshold = 5;
		/// <summary>
		/// The maximum number of times that forwarder will attempt to retry
		/// round-trip messages in response to <see cref="RelayErrorType.NodeUnreachable"/> errors.
		/// </summary>
		[XmlAttribute("RetryCount")]
		public int RetryCount;
		[XmlAttribute("NodeReselectMinutes")]
		public int NodeReselectMinutes;
		[XmlAttribute("UseIdRanges")]
		public bool UseIdRanges;

		[XmlAttribute("StartupRepopulateDuration")]
		public int StartupRepopulateDuration;

	    [XmlAttribute("RetryPolicy")] 
        public RelayRetryPolicy RetryPolicy;

	    private RelayNodeClusterDefinition[] _relayNodeClusters;
		/// <summary>
		/// Gets the RelayNodeClusters for the given environment.
		/// </summary>
		public RelayNodeClusterDefinition[] RelayNodeClusters
		{
			get
			{
				if (_relayNodeClusters == null)
				{
					EnvironmentDefinition env = GetEnvironment();
					_relayNodeClusters = env != null ? env.RelayNodeClusters : DefaultRelayNodeClusters;
				}

				return _relayNodeClusters;
			}
		}

		/// <summary>
		/// Indicates to clients whether or not to serialize <see cref="RelayMessage"/> instances
		/// using the older, non-forward compatible format that older code can read.
		/// <see langword="true"/> by default.
		/// </summary>
		[XmlAttribute("LegacySerialization")]
		public bool LegacySerialization = true;

		[XmlArray("RelayNodeClusters")]
		[XmlArrayItem("RelayNodeCluster")]
		public RelayNodeClusterDefinition[] DefaultRelayNodeClusters { get; set; }

		private EnvironmentDefinition GetEnvironment()
		{
			if (Environments == null) return null;

			string environment = EnvironmentManager.CurrentEnvironment;
			foreach (var env in Environments)
			{
				if (env.IsMatch(environment)) return env;
			}
			return null;
		}

		/// <summary>
		/// Gets the <see cref="SocketSettings"/> for the current environment
		/// </summary>
		public SocketSettings SocketSettings
		{
			get
			{
				var defaultSettings = DefaultSocketSettings;

				EnvironmentDefinition env = GetEnvironment();
				if (env != null) return env.SocketSettings;

				return defaultSettings;
			}
		}

		[XmlElement("SocketSettings")]
		public SocketSettings DefaultSocketSettings { get; set; }

		[XmlElement("QueueConfig")]
		public QueueConfig DefaultQueueConfig { get; set; }

		/// <summary>
		/// Gets the <see cref="QueueConfig"/> for the current environment.
		/// </summary>
		public QueueConfig QueueConfig
		{
			get
			{
				var defaultSettings = DefaultQueueConfig;

				EnvironmentDefinition env = GetEnvironment();
				if (env != null) return env.QueueConfig;

				return defaultSettings;
			}
		}

		/// <summary>
		/// Gets or sets the list of Environments.
		/// </summary>
		[XmlArray("Environments")]
		[XmlArrayItem("Environment")]
		public List<EnvironmentDefinition> Environments { get; set; }

		/// <summary>
		/// Determines if there is defined a node configured to listen on a given address and port 
		/// contained in any cluster accross all environments.
		/// </summary>
		/// <param name="address">The <see cref="IPAddress"/> to look for.</param>
		/// <param name="listenPort">The port that the node is listening on.</param>
		/// <returns>Returns true if the address is contained in this group; otherwise, false.</returns>
		public bool ContainsNode(IPAddress address, int listenPort)
		{
			RelayNodeDefinition node = GetNodeFor(address, listenPort);
			return (node != null);
		}

		/// <summary>
		/// Returns a <see cref="RelayNodeDefinition"/> of a node configured to listen 
		/// on a given address and port contained in any cluster for the current environment.
		/// </summary>
		/// <param name="address">The <see cref="IPAddress"/> to look for.</param>
		/// <param name="listenPort">The port that the node is listening on.</param>
		/// <returns>Returns a definition if found; otherwise, returns null.</returns>
		public RelayNodeDefinition GetNodeFor(IPAddress address, int listenPort)
		{
			RelayNodeDefinition nodeDefinition = null;
			var defaultSettings = DefaultRelayNodeClusters;

			RelayNodeClusterDefinition[] clusters = defaultSettings;
			EnvironmentDefinition env = GetEnvironment();

			if (env != null)
			{
				clusters = env.RelayNodeClusters;
			}

			foreach (RelayNodeClusterDefinition cluster in clusters)
			{
				nodeDefinition = cluster.GetNodeFor(address, listenPort);
				if (nodeDefinition != null)
				{
					break;
				}
			}

			return nodeDefinition;
		}

		/// <summary>
		/// Returns a <see cref="RelayNodeClusterDefinition"/> for the current environment that contains
		/// the a node configured to listen on the
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="portNumber">The port number.</param>
		/// <param name="clusterIndex">Index of the cluster.</param>
		/// <returns></returns>
		internal RelayNodeClusterDefinition GetClusterFor(IPAddress address, int portNumber, out int clusterIndex)
		{
			RelayNodeClusterDefinition[] clusters = null;
			EnvironmentDefinition env = GetEnvironment();
			clusterIndex = -1;
			if (env != null)
			{
				clusters = env.RelayNodeClusters;
			}
			else
			{
				clusters = DefaultRelayNodeClusters;
			}

			RelayNodeClusterDefinition clusterDefinition = null;
			if (clusters != null)
			{
				for (int i = 0; i < clusters.Length; i++)
				{
					if (clusters[i].ContainsNode(address, portNumber))
					{
						clusterDefinition = clusters[i];
						clusterIndex = i;
						break;
					}
				}
			}

			return clusterDefinition;
		}

		public int GetClusterIndexFor(int objectId)
		{
			if (UseIdRanges)
			{
				return GetRangedIndex(objectId);
			}
			else
			{
				return GetModdedIndex(objectId);
			}
		}		
		
		private int GetRangedIndex(int objectId)
		{
			if (_relayNodeClusters == null)
				return 0;
			for (int i = 0; i < RelayNodeClusters.Length; i++)
			{
				if (RelayNodeClusters[i].ObjectInRange(objectId))
				{
					return i;
				}
			}
			return -1;
		}

		private int GetModdedIndex(int objectId)
		{
			RelayNodeClusterDefinition[] clusters = RelayNodeClusters;
			if(clusters == null || objectId == Int32.MinValue) return 0; //cause Math.Abs(Int32.MinValue) throws
			return Math.Abs(objectId) % clusters.Length;
		}
	}

	public class RelayNodeClusterDefinition
	{
		[XmlArray("RelayNodes")]
		[XmlArrayItem("RelayNode")]
		public RelayNodeDefinition[] RelayNodes;

		[XmlAttribute("MinId")]
		public int MinId;
		[XmlAttribute("MaxId")]
		public int MaxId;

		[XmlAttribute("StartupRepopulateDuration")]
		public int StartupRepopulateDuration;

		public bool ContainsNode(IPAddress address, int listenPort)
		{
			foreach (RelayNodeDefinition node in RelayNodes)
			{
				if (node.IPAddress != null
					&& node.IPAddress.Equals(address)
					&& node.Port == listenPort)
				{
					return true;
				}
			}
			return false;
		}

		public bool TryFindNode(IPAddress address, int listenPort, out RelayNodeDefinition nodeDefintion)
		{
			foreach (RelayNodeDefinition node in RelayNodes)
			{
				if (node.IPAddress != null
					&& node.IPAddress.Equals(address)
					&& node.Port == listenPort)
				{
					nodeDefintion = node;
					return true;
				}
			}
			nodeDefintion = null;
			return false;
		}

		internal RelayNodeDefinition GetNodeFor(IPAddress address, int listenPort)
		{
			RelayNodeDefinition nodeDefinition = null;
			foreach (RelayNodeDefinition node in RelayNodes)
			{
				if (node.IPAddress != null
					&& node.IPAddress.Equals(address)
					&& node.Port == listenPort)
				{
					nodeDefinition = node;
					break;
				}
			}
			return nodeDefinition;
		}

		public bool ObjectInRange(int objectId)
		{
			return (objectId >= MinId && objectId <= MaxId);
		}
	}


	public class RelayNodeDefinition
	{
		readonly AsyncCallback getHostEntryCallBack = GetHostEntryCallBack;

		[XmlAttribute("GatherStatistics")]
		public bool GatherStatistics;
		[XmlAttribute("Activated")]
		public bool Activated;

		private string _host;

		[XmlAttribute("Host")]
		public string Host
		{
			get { return _host; }
			set
			{
				_host = value != null ? value.Trim() : null;
			}
		}

		[XmlAttribute("Port")]
		public int Port;
        [XmlAttribute("PipelinePort")]
        public int PipelinePort;
		[XmlAttribute("ServiceType")]
		public string ServiceType;
		[XmlAttribute("Zone")]
		public ushort Zone;

		[XmlAttribute("StartupRepopulateDuration")]
		public int StartupRepopulateDuration;

		private IPAddress ipAddress;
		private readonly object ipLock = new object();
		private bool lookedForIp;
		[XmlIgnore]
		public IPAddress IPAddress
		{
			get
			{
				if (!lookedForIp)
				{
					lock (ipLock)
					{
						if (!lookedForIp)
						{
							if (!IPAddress.TryParse(Host, out ipAddress))
							{
								ipAddress = null;
								IPHostEntry hostEntry = GetHostEntry();

								if (hostEntry != null && hostEntry.AddressList.Length > 0)
								{
									foreach (IPAddress addy in hostEntry.AddressList)
									{
										if (addy.AddressFamily == AddressFamily.InterNetwork)
										{
											ipAddress = addy;
											break;
										}
									}

								}
							}
							lookedForIp = true;
						}
					}
				}
				return ipAddress;
			}
		}

		private IPEndPoint ipEndPoint;
		private readonly object ipEndPointLock = new object();
		private bool lookedForEndpoint;
		[XmlIgnore]
		public IPEndPoint IPEndPoint
		{
			get
			{
				if (!lookedForEndpoint)
				{
					lock (ipEndPointLock)
					{
						if (!lookedForEndpoint)
						{
							if (IPAddress != null)
							{
								ipEndPoint = new IPEndPoint(IPAddress, Port);
							}
							lookedForEndpoint = true;
						}
					}
				}
				return ipEndPoint;
			}
		}


		private IPHostEntry GetHostEntry()
		{
			GetHostEntryState state = new GetHostEntryState();
			IPHostEntry hostEntry = null;
			try
			{
				Dns.BeginGetHostEntry(Host, getHostEntryCallBack, state);
				if (!state.resetEvent.WaitOne(2000, false))
				{
					state.resetEvent.Reset();
					throw new SocketException((int)SocketError.HostNotFound);
				}
				else
				{
					if (state.exception != null)
					{
						//done this way because the CallBack is on another thread, 
						//so any exceptions it throws will not be caught by calling code
						throw state.exception;
					}
					else
					{
						state.resetEvent.Reset();
					}
					hostEntry = state.resolvedIPs;
				}
			}
			catch (Exception ex)
			{
				if (RelayNodeMapping.log.IsErrorEnabled)
					RelayNodeMapping.log.ErrorFormat("Error getting IP address for host '{0}': {1}", Host, ex.Message);
			}
			return hostEntry;
		}

		private static void GetHostEntryCallBack(IAsyncResult ar)
		{
			GetHostEntryState state = ar.AsyncState as GetHostEntryState;
			if (state != null)
			{
				try
				{
					state.resolvedIPs = Dns.EndGetHostEntry(ar);
					state.resetEvent.Set();
				}
				catch (SocketException sex)
				{
					state.exception = sex;
					state.resetEvent.Set();
				}
			}
			else
			{
				state.exception = new SocketException((int)SocketError.HostNotFound);
				state.resetEvent.Set();
			}
		}

		internal class GetHostEntryState
		{
			public ManualResetEvent resetEvent = new ManualResetEvent(false);
			public SocketException exception;
			public IPHostEntry resolvedIPs;
		}

		public override string ToString()
		{
            if (PipelinePort == 0) return Host + ":" + Port;
            return Host + ":" + PipelinePort;
		}
	}

	public class SocketSettings
	{
		[XmlAttribute("ReceiveTimeout")]
		public int ReceiveTimeout;
		[XmlAttribute("ReceiveBufferSize")]
		public int ReceiveBufferSize;
		[XmlAttribute("SendTimeout")]
		public int SendTimeout;
		[XmlAttribute("SendBufferSize")]
		public int SendBufferSize;
		[XmlAttribute("MaximumMessageSize")]
		public int MaximumMessageSize;
		[XmlAttribute("ConnectTimeout")]
		public int ConnectTimeout;
		[XmlAttribute("SocketPoolSize")]
		public int PoolSize;

	}

	public class QueueConfig
	{
		[XmlElement("Enabled")]
		public bool Enabled = true;
		[XmlElement("MaxCount")]
		public int MaxCount = 1000;
		[XmlElement("ItemsPerDequeue")]
		public int ItemsPerDequeue = 100;
		[XmlElement("DequeueIntervalSeconds")]
		public int DequeueIntervalSeconds = 10;
		[XmlElement("MaxQueuePolicy")]
		public string MaxQueuePolicy = "Block";
		[XmlElement("PersistenceFolder")] 
		public string PersistenceFolder = string.Empty;
		[XmlElement("PersistenceFileSize")]
		public int PersistenceFileSize = 2 * 1024 * 1024;
		[XmlElement("MaxPersistedMB")]
		public int MaxPersistedMB = 0;
	}
}
