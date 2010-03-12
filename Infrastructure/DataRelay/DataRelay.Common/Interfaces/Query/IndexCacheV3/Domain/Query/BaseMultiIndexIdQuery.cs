using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public abstract class BaseMultiIndexIdQuery<TQueryResult> : 
        IIndexIdParam,
        IPrimaryRelayMessageQuery,
        ISplitable<TQueryResult> where TQueryResult : BaseMultiIndexIdQueryResult, new()
    {
        #region Data Members
        /// <summary>
        /// Index name for the query
        /// </summary>
        public string TargetIndexName
        {
            get; set;
        }

        /// <summary>
        /// List of the index names other than TargetIndexName to get extra tags
        /// </summary>
        public List<string> TagsFromIndexes
        {
            get; set;
        }

        /// <summary>
        /// If set index is dynamically before selecting items
        /// </summary>
        public TagSort TagSort
        {
            get; set;
        }

        /// <summary>
        /// List of index ids to lookup
        /// </summary>
        public List<byte[]> IndexIdList
        {
            get; set;
        }

        /// <summary>
        /// If set only that many number of items are returned from each IndexId within the IndexIdList
        /// </summary>
        public int MaxItems
        {
            get; set;
        }

        /// <summary>
        /// If true no data is to be fetched from data tier
        /// </summary>
        public bool ExcludeData
        {
            get; set;
        }

        /// <summary>
        /// If true gets the header information like Metadata and Virtual Count
        /// </summary>
        public bool GetIndexHeader
        {
            get
            {
                return GetIndexHeaderType == GetIndexHeaderType.ResultItemsIndexIds;
            }
            set
            {
                GetIndexHeaderType = value ? GetIndexHeaderType.ResultItemsIndexIds : GetIndexHeaderType.None;
            }
        }

        /// <summary>
        /// If true gets the total number of items that satisfy Filter(s)
        /// </summary>
        internal bool GetAdditionalAvailableItemCount
        {
            get; set;
        }

        /// <summary>
        /// If set default PrimaryIds of IndexIds within the IndexIdList are overriden
        /// </summary>
        public List<int> PrimaryIdList
        {
            get; set;
        }

        /// <summary>
        /// If set Filter conditions are applied to every item within the index
        /// </summary>
        public Filter Filter
        {
            get; set;
        }

        /// <summary>
        /// If set it is applied on index's sort field. IndexCondition is applied before Filter
        /// </summary>
        public IndexCondition IndexCondition
        {
            get; set;
        }

        /// <summary>
        /// If set it overrides MaxItems and Filter properties
        /// </summary>
        public Dictionary<byte[] /*IndexId*/, IndexIdParams /*IndexIdParams*/> IndexIdParamsMapping
        {
            get; internal set;
        }

        /// <summary>
        /// If set the items get a different data tier object rather than the pre-configured one
        /// </summary>
        public FullDataIdInfo FullDataIdInfo
        {
            get; set;
        }

        /// <summary>
        /// If set individual caps can be set on the query conditions
        /// </summary>
        public CapCondition CapCondition
        { 
            get; set;
        }

        /// <summary>
        /// Specifies the the manner in which IndexHeaders should be returned
        /// </summary>
        public GetIndexHeaderType GetIndexHeaderType
        {
            get; set;
        }

        /// <summary>
        /// If set subset processing is performed on the client
        /// </summary>
        internal bool ClientSideSubsetProcessingRequired
        {
            get; set;
        }

        /// <summary>
        /// Set on the server and used by the client to optimize merging
        /// </summary>
        internal abstract int MaxMergeCount
        {
            get;
        }
        
        #endregion

        #region Ctors

        protected BaseMultiIndexIdQuery()
        {
            Init(null, null, null, null, null, -1, false, false, null, false, null, null, null, false, null, GetIndexHeaderType.None);
        }

        protected BaseMultiIndexIdQuery(BaseMultiIndexIdQuery<TQueryResult> baseMultiIndexIdQuery)
        {
            Init(baseMultiIndexIdQuery.IndexIdList, 
                baseMultiIndexIdQuery.PrimaryIdList,
                baseMultiIndexIdQuery.TargetIndexName,
                baseMultiIndexIdQuery.TagsFromIndexes,
                baseMultiIndexIdQuery.TagSort,
                baseMultiIndexIdQuery.MaxItems,
                baseMultiIndexIdQuery.ExcludeData,
                baseMultiIndexIdQuery.GetIndexHeader,
                baseMultiIndexIdQuery.IndexIdParamsMapping,
                baseMultiIndexIdQuery.GetAdditionalAvailableItemCount,
                baseMultiIndexIdQuery.Filter,
                baseMultiIndexIdQuery.FullDataIdInfo,
                baseMultiIndexIdQuery.IndexCondition,
                baseMultiIndexIdQuery.ClientSideSubsetProcessingRequired,
                baseMultiIndexIdQuery.CapCondition,
                baseMultiIndexIdQuery.GetIndexHeaderType);
        }

        private void Init(List<byte[]> indexIdList, 
            List<int> primaryIdList, 
            string targetIndexName,
            List<string> tagsFromIndexes,
            TagSort tagSort,
            int maxItems,
            bool excludeData,
            bool getIndexHeader,
            Dictionary<byte[], IndexIdParams> indexIdParamsMapping,
            bool getAdditionalAvailableItemCount,
            Filter filter,
            FullDataIdInfo fullDataIdInfo,
            IndexCondition indexCondition,
            bool clientSideSubsetProcessingRequired,
            CapCondition capCondition,
            GetIndexHeaderType getIndexHeaderType)
        {
            IndexIdList = indexIdList;
            PrimaryIdList = primaryIdList;
            TargetIndexName = targetIndexName;
            TagsFromIndexes = tagsFromIndexes;
            TagSort = tagSort;
            MaxItems = maxItems;
            ExcludeData = excludeData;
            GetIndexHeader = getIndexHeader;
            IndexIdParamsMapping = indexIdParamsMapping;
            GetAdditionalAvailableItemCount = getAdditionalAvailableItemCount;
            Filter = filter;
            FullDataIdInfo = fullDataIdInfo;
            IndexCondition = indexCondition;
            ClientSideSubsetProcessingRequired = clientSideSubsetProcessingRequired;
            CapCondition = capCondition;
            GetIndexHeaderType = getIndexHeaderType;
        }

        #endregion

        #region Methods
        /// <summary>
        /// Gets IndexIdParams for the specified IndexId from IndexIdParamsMapping
        /// </summary>
        /// <param name="indexId"></param>
        /// <returns></returns>
        internal IndexIdParams GetParamsForIndexId(byte[] indexId)
        {
            IndexIdParams retVal;

            if ((IndexIdParamsMapping == null) || !IndexIdParamsMapping.TryGetValue(indexId, out retVal))
            {
                retVal = new IndexIdParams(this);
            }
            return retVal;
        }

        /// <summary>
        /// Add IndexIdParams for the specified IndexId to IndexIdParamsMapping
        /// </summary>
        /// <param name="indexId"></param>
        /// <param name="indexIdParam"></param>
        public void AddIndexIdParam(byte[] indexId, IndexIdParams indexIdParam)
        {
            if (IndexIdParamsMapping == null)
            {
                IndexIdParamsMapping = new Dictionary<byte[], IndexIdParams>(new ByteArrayEqualityComparer());
            }
            indexIdParam.BaseQuery = this;
            IndexIdParamsMapping.Add(indexId, indexIdParam);
        }

        /// <summary>
        /// Delete IndexIdParams for the specified IndexId from IndexIdParamsMapping
        /// </summary>
        /// <param name="indexId"></param>
        public void DeleteIndexIdParam(byte[] indexId)
        {
            if (IndexIdParamsMapping != null)
            {
                IndexIdParamsMapping.Remove(indexId);
            }
        }
        #endregion

        #region IRelayMessageQuery Members
        
        public abstract byte QueryId
        {
            get;
        }
        
        #endregion

        #region IVersionSerializable Members
        
        public abstract int CurrentVersion
        {
            get;
        }

        public abstract void Deserialize(IPrimitiveReader reader, int version);

        public abstract void Serialize(IPrimitiveWriter writer);

        public bool Volatile
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region ICustomSerializable Members
        
        public void Deserialize(IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }
        
        #endregion

        #region IPrimaryQueryId Members

        /// <summary>
        /// assign -1 to primaryId field as the initial value
        /// </summary>
        protected int primaryId = IndexCacheUtils.MUTILEINDEXQUERYDEFAULTPRIMARYID;

        public virtual int PrimaryId
        {
            get
            {
                return this.primaryId;
            } 
            
            set
            {
                this.primaryId = value;
            }
        }
        
        #endregion

        #region ISplitable<TQueryResult> Members
        
        public abstract List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup);

        #endregion

        #region IMergeableQueryResult<TQueryResult> Members

        public abstract TQueryResult MergeResults(IList<TQueryResult> QueryResults);

        #endregion
    }
}
