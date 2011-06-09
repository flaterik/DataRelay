using System;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using MySpace.BerkeleyDb.Configuration;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.RelayComponent.BerkeleyDb;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.DomainSpecificConfigs;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using MySpace.DataRelay.RelayComponent.Forwarding;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Enums;
using MySpace.BinaryStorage.Store;
using MySpace.Storage;
using MySpace.ResourcePool;
using System.IO;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context
{
    internal class IndexStoreContext
    {
        #region Data Members

        private static readonly IndexStoreContext instance = new IndexStoreContext();
        /// <summary>
        /// Gets the IndexStoreContext instance.
        /// </summary>
        /// <value>The IndexStoreContext instance.</value>
        internal static IndexStoreContext Instance
        {
            get
            {
                return instance;
            }
        }

        internal const string COMPONENT_NAME = "CacheIndexV3Storage";

        private RelayNodeConfig nodeConfig;
        /// <summary>
        /// Gets or sets the node config.
        /// </summary>
        /// <value>The node config.</value>
        public RelayNodeConfig NodeConfig
        {
            get
            {
                return nodeConfig;
            }
            set
            {
                nodeConfig = value;
            }
        }

        private CacheIndexV3StorageConfiguration storageConfiguration;
        /// <summary>
        /// Gets or sets the Index Cache storage configuration.
        /// </summary>
        /// <value>The Index Cache storage configuration.</value>
        public CacheIndexV3StorageConfiguration StorageConfiguration
        {
            get
            {
                return storageConfiguration;
            }
            set
            {
                storageConfiguration = value;
            }
        }

        /// <summary>
        /// Gets or sets the domain specific config.
        /// </summary>
        /// <value>The domain specific config.</value>
        public DomainSpecificConfig DomainSpecificConfig
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the index storage component.
        /// </summary>
        /// <value>The index storage component.</value>
        public IBinaryStorage IndexStorageComponent
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the forwarder component.
        /// </summary>
        /// <value>The forwarder component.</value>
        public IRelayComponent ForwarderComponent
        {
            get; set;
        }

        private Dictionary<short, short> relatedTypeIds;
        /// <summary>
        /// Gets or sets the related type ids.
        /// </summary>
        /// <value>The related type ids.</value>
        public Dictionary<short, short> RelatedTypeIds
        {
            get
            {
                return relatedTypeIds;
            }
            set
            {
                relatedTypeIds = value;
            }
        }

        private Dictionary<short, bool> compressOptions;
        /// <summary>
        /// Gets or sets the compress options.
        /// </summary>
        /// <value>The compress options.</value>
        public Dictionary<short, bool> CompressOptions
        {
            get
            {
                return compressOptions;
            }
            set
            {
                compressOptions = value;
            }
        }

        private TagHashCollection tagHashCollection;
        /// <summary>
        /// Gets or sets the tag hash collection.
        /// </summary>
        /// <value>The tag hash collection.</value>
        public TagHashCollection TagHashCollection
        {
            get
            {
                return tagHashCollection;
            }
            set
            {
                tagHashCollection = value;
            }
        }

        private StringHashCollection stringHashCollection;
        /// <summary>
        /// Gets or sets the string hash collection.
        /// </summary>
        /// <value>The string hash collection.</value>
        public StringHashCollection StringHashCollection
        {
            get
            {
                return stringHashCollection;
            }
            set
            {
                stringHashCollection = value;
            }
        }

        /// <summary>
        /// Gets or sets the remote query call timeout
        /// </summary>
        public int RemoteClusteredQueryTimeOut
        {
            get
            {
                return storageConfiguration.CacheIndexV3StorageConfig.RemoteClusterQueryTimeOut;
            }
        }

        private int myClusterPosition;
        
        /// <summary>
        /// Gets or sets my cluster position.
        /// </summary>
        /// <value>My cluster position.</value>
        public int MyClusterPosition
        {
            get
            {
                return myClusterPosition;
            }
            set
            {
                myClusterPosition = value;
            }
        }

        /// <summary>
        /// gets the partial get bytes length
        /// </summary>
        public int PartialGetLength
        {
            get
            {
                return storageConfiguration.CacheIndexV3StorageConfig.PartialGetLength;
            }
        }

        private int numClustersInGroup;
        /// <summary>
        /// Gets or sets the num clusters in group.
        /// </summary>
        /// <value>The num clusters in group.</value>
        public int NumClustersInGroup
        {
            get
            {
                return numClustersInGroup;
            }
            set
            {
                numClustersInGroup = value;
            }
        }

        private ushort myZone;
        /// <summary>
        /// Gets or sets my zone.
        /// </summary>
        /// <value>My zone.</value>
        public ushort MyZone
        {
            get
            {
                return myZone;
            }
            set
            {
                myZone = value;
            }
        }

        private MemoryStreamPool myMemoryPool;

        /// <summary>
        /// Gets or sets memory pool
        /// </summary>
        public MemoryStreamPool MemoryPool
        {
            get
            {
                return this.myMemoryPool;
            }
        }

        #endregion

        #region Ctors

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexStoreContext"/> class.
        /// </summary>
        private IndexStoreContext() { }

        #endregion

        /// <summary>
        /// Initializes the reload config.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="isInit">if set to <c>true</c> [is init].</param>
        internal void InitializeReloadConfig(RelayNodeConfig config, bool isInit)
        {
            #region Set Component Configs
            
            if (config == null)
            {
                Exception ex = new Exception("Unable to Initialize/Reload Config for CacheIndexV3Store because RelayNodeConfig is null");
                LoggingUtil.Log.Error(ex.Message);
                throw ex;
            }

            Interlocked.Exchange(ref nodeConfig, config);
            Interlocked.Exchange(ref storageConfiguration, InitializeConfig(nodeConfig));
            
            #endregion

            #region Setup Forwarder
           
            if (ForwarderComponent == null)
            {
                ForwarderComponent = new Forwarder();
                ForwarderComponent.Initialize(config, null);
            }
            else
            {
                ForwarderComponent.ReloadConfig(config);
            }
            
            #endregion

            #region Init/Reload Component
            
            if (IndexStorageComponent == null)
            {
                IndexStorageComponent = InitializeStorageComponent(storageConfiguration.BerkeleyDbConfig);
            }
            else
            {
                ReloadStorageComponent(storageConfiguration.BerkeleyDbConfig, IndexStorageComponent);
            }
            
            #endregion

            #region init memoryPool
            if (this.myMemoryPool == null)
            {
                this.myMemoryPool = new MemoryStreamPool(
                    storageConfiguration.CacheIndexV3StorageConfig.MemPoolItemInitialSizeInBytes,
                    ResourcePool.ResourcePool<MemoryStream>.InfiniteReuse,
                    storageConfiguration.CacheIndexV3StorageConfig.MemPoolMinItemNumber);
            }

            #region init DataMembers

            Interlocked.Exchange(ref relatedTypeIds, InitializeRelatedTypeIds(nodeConfig));

            Interlocked.Exchange(ref compressOptions, InitializeCompressOptions(nodeConfig));

            InitializeClusterInfo(out myClusterPosition, out numClustersInGroup, out myZone);

            LockingUtil.Instance.InitializeLockerObjects(storageConfiguration.CacheIndexV3StorageConfig.LockMultiplier, numClustersInGroup);

            LegacySerializationUtil.Instance.InitializeLegacySerializtionTypes(nodeConfig.TypeSettings, storageConfiguration.CacheIndexV3StorageConfig.SupportLegacySerialization);


            #region init performance counters

            List<short> typeIdList = new List<short>();
            foreach (IndexTypeMapping indexTypeMapping in storageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection)
            {
                typeIdList.Add(indexTypeMapping.TypeId);
            }

            // get the max type id
            short maxTypeId = config.TypeSettings.MaxTypeId;

            // initialize or re-initialize performance counters, 
            // counter category will also be created if it is not there
            PerformanceCounters.Instance.InitializeCounters(
                config.TransportSettings.ListenPort,
                typeIdList,
                maxTypeId,
                isInit);

            #endregion

            #region Set HashCollections
            
            Interlocked.Exchange(ref tagHashCollection, InitializeTagHashCollection(storageConfiguration));
            Interlocked.Exchange(ref stringHashCollection, InitializeStringHashCollection(storageConfiguration));
            
            #endregion

            #endregion

            #region Init Domain Specific Config

            DomainSpecificConfig = new DomainSpecificConfig
                                       {
                                           StreamRecencyConfig = ConfigurationManager.GetSection("StreamRecencyConfig") as StreamRecencyConfig
                                       };

            #endregion
        }

        /// <summary>
        /// Removes the type from collections.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        internal void RemoveType(short typeId)
        {
            tagHashCollection.RemoveType(typeId);
            stringHashCollection.RemoveType(typeId);
        }

        /// <summary>
        /// Initializes the Index Cache config.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <returns>Index Cache Configuration</returns>
        private static CacheIndexV3StorageConfiguration InitializeConfig(RelayNodeConfig config)
        {
            CacheIndexV3StorageConfiguration configObj = config.RelayComponents.GetConfigFor(COMPONENT_NAME) as CacheIndexV3StorageConfiguration;
            if (configObj != null)
            {
                configObj.SanityCheckAndInitializeCustomFields();
            }
            return configObj;
        }

        /// <summary>
        /// Initializes the cluster info.
        /// </summary>
        /// <param name="myClusterPos">My cluster pos.</param>
        /// <param name="numClustersInGroup">The num clusters in group.</param>
        /// <param name="myZone">My zone.</param>
        private static void InitializeClusterInfo(out int myClusterPos, out int numClustersInGroup, out ushort myZone)
        {
            RelayNodeGroupDefinition myGroup = RelayNodeConfig.GetRelayNodeConfig().GetMyGroup();

            RelayNodeClusterDefinition myCluster = RelayNodeConfig.GetRelayNodeConfig().GetMyCluster();

            RelayNodeDefinition myNode = RelayNodeConfig.GetRelayNodeConfig().GetMyNode();

            myZone = myNode.Zone;
            numClustersInGroup = myGroup.RelayNodeClusters.Length;
            myClusterPos = 0;

            foreach (RelayNodeClusterDefinition cluster in myGroup.RelayNodeClusters)
            {
                if (cluster.RelayNodes.Length == myCluster.RelayNodes.Length &&
                    cluster.ContainsNode(myNode.IPAddress, myNode.Port))
                {
                    // this cluster contains my Node
                    break;
                }
                myClusterPos++;
            }
        }

        /// <summary>
        /// Initializes the storage component.
        /// </summary>
        /// <param name="berkeleyDbConfig">The berkeley db config.</param>
        /// <returns>IBinaryStorage Component</returns>
        private static IBinaryStorage InitializeStorageComponent(BerkeleyDbConfig berkeleyDbConfig)
        {
            // create and init bdb component
            IBinaryStorage bdbComponent = new BerkeleyBinaryStore();

            try
            {
                bdbComponent.Initialize(berkeleyDbConfig);
            }
            catch (Exception ex)
            {
                LoggingUtil.Log.ErrorFormat("Failed to Initialize BerkeleyDbComponent : {0}", ex);
                throw ex;
            }

            return bdbComponent;
        }

        /// <summary>
        /// Reloads the storage component.
        /// </summary>
        /// <param name="berkeleyDbConfig">The berkeley db config.</param>
        /// <param name="relayComponent">The relay component.</param>
        private static void ReloadStorageComponent(BerkeleyDbConfig berkeleyDbConfig, IBinaryStorage binaryStorage)
        {
            binaryStorage.Reinitialize(berkeleyDbConfig);
        }

        /// <summary>
        /// Initializes the related type ids.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <returns>RelatedTypeIds</returns>
        private static Dictionary<short, short> InitializeRelatedTypeIds(RelayNodeConfig config)
        {
            Dictionary<short, short> relatedTypeIds = new Dictionary<short, short>();
            if (config != null)
            {
                foreach (TypeSetting typeSetting in config.TypeSettings.TypeSettingCollection)
                {
                    if (typeSetting.RelatedIndexTypeId > 0)
                    {
                        relatedTypeIds.Add(typeSetting.TypeId, typeSetting.RelatedIndexTypeId);
                    }
                }
            }
            return relatedTypeIds;
        }

        /// <summary>
        /// Initializes the compress options.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <returns>CompressOptions</returns>
        private static Dictionary<short, bool> InitializeCompressOptions(RelayNodeConfig config)
        {
            Dictionary<short, bool> compressOptions = new Dictionary<short, bool>();
            if (config != null)
            {
                foreach (TypeSetting typeSetting in config.TypeSettings.TypeSettingCollection)
                {
                    compressOptions.Add(typeSetting.TypeId, typeSetting.Compress);
                }
            }
            return compressOptions;
        }

        /// <summary>
        /// Initializes the tag hash collection.
        /// </summary>
        /// <param name="storageConfiguration">The storage configuration.</param>
        /// <returns>TagHashCollection</returns>
        private static TagHashCollection InitializeTagHashCollection(CacheIndexV3StorageConfiguration storageConfiguration)
        {
            if (String.IsNullOrEmpty(storageConfiguration.CacheIndexV3StorageConfig.TagHashFile))
            {
                storageConfiguration.CacheIndexV3StorageConfig.TagHashFile = storageConfiguration.BerkeleyDbConfig.EnvironmentConfig.HomeDirectory + "/TagHashCollection.Tags";
            }

            return new TagHashCollection(storageConfiguration.CacheIndexV3StorageConfig.TagHashFile);
        }

        /// <summary>
        /// Initializes the string hash collection.
        /// </summary>
        /// <param name="storageConfiguration">The storage configuration.</param>
        /// <returns>StringHashCollection</returns>
        private static StringHashCollection InitializeStringHashCollection(CacheIndexV3StorageConfiguration storageConfiguration)
        {
            if (String.IsNullOrEmpty(storageConfiguration.CacheIndexV3StorageConfig.StringHashFile))
            {
                storageConfiguration.CacheIndexV3StorageConfig.StringHashFile = storageConfiguration.BerkeleyDbConfig.EnvironmentConfig.HomeDirectory + "/StringHashCollection.Strings";
            }

            return new StringHashCollection(storageConfiguration.CacheIndexV3StorageConfig.StringHashFile);
        }
        
        #endregion

        #region Member methods
        
        /// <summary>
        /// Gets the name of the type.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <returns>TypeName</returns>
        internal string GetTypeName(short typeId)
        {
            return nodeConfig.TypeSettings.TypeSettingCollection[typeId].TypeName;
        }

        /// <summary>
        /// Tries the get type id.
        /// </summary>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="typeId">The type id.</param>
        /// <returns>true if get is successful; otherwise, false</returns>
        internal bool TryGetTypeId(string typeName, out short typeId)
        {
            typeId = -1;
            if(nodeConfig.TypeSettings.TypeSettingCollection.Contains(typeName))
            {
                typeId = nodeConfig.TypeSettings.TypeSettingCollection[typeName].TypeId;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries the get related index type id.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="relatedTypeId">The related type id.</param>
        /// <returns>true if get is successful; otherwise, false</returns>
        internal bool TryGetRelatedIndexTypeId(short typeId, out short relatedTypeId)
        {
            if (!relatedTypeIds.TryGetValue(typeId, out relatedTypeId))
            {
                lock (relatedTypeIds)
                {
                    if (!relatedTypeIds.TryGetValue(typeId, out relatedTypeId))
                    {
                        relatedTypeId = nodeConfig.TypeSettings.TypeSettingCollection[typeId].RelatedIndexTypeId;
                        if (relatedTypeId < 1)
                            return false;
                        relatedTypeIds.Add(typeId, relatedTypeId);
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the compress option.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <returns>CompressOption</returns>
        internal bool GetCompressOption(short typeId)
        {
            bool compress;
            if (!compressOptions.TryGetValue(typeId, out compress))
            {
                lock (compressOptions)
                {
                    if (!compressOptions.TryGetValue(typeId, out compress))
                    {
                        compress = nodeConfig.TypeSettings.TypeSettingCollection[typeId].Compress;
                        compressOptions.Add(typeId, compress);
                    }
                }
            }
            return compress;
        }

        /// <summary>
        /// Shuts down the component.
        /// </summary>
        internal void ShutDown()
        {
            IndexStorageComponent.Dispose();

            StringBuilder exceptionString = new StringBuilder();
            try
            {
                tagHashCollection.PersistState();
                LoggingUtil.Log.InfoFormat("Saved TagHashCollection to file : {0}", storageConfiguration.CacheIndexV3StorageConfig.TagHashFile);
            }
            catch (Exception ex)
            {
                LoggingUtil.Log.ErrorFormat("Unable to save TagHashCollection to file : {0} - {1}", storageConfiguration.CacheIndexV3StorageConfig.TagHashFile, ex);
                exceptionString.AppendFormat("Unable to save TagHashCollection to file : {0} - {1}",
                                             storageConfiguration.CacheIndexV3StorageConfig.TagHashFile, ex);
            }
            try
            {
                stringHashCollection.PersistState();
                LoggingUtil.Log.InfoFormat("Saved StringHashCollection to file : {0}", storageConfiguration.CacheIndexV3StorageConfig.StringHashFile);
            }
            catch (Exception ex)
            {
                LoggingUtil.Log.ErrorFormat("Unable to save StringHashCollection to file : {0} - {1}", storageConfiguration.CacheIndexV3StorageConfig.StringHashFile, ex);
                exceptionString.AppendFormat(". Unable to save StringHashCollection to file : {0} - {1}",
                             storageConfiguration.CacheIndexV3StorageConfig.StringHashFile, ex);

            }
            if(exceptionString.Length> 0)
            {
                throw new Exception(exceptionString.ToString());
            }

            // dispose all the performance counters
            PerformanceCounters.Instance.DisposeCounters();
        }
        
        #endregion
    }
}
