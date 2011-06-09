using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using MySpace.Common.IO;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public abstract class BaseMultiIndexIdQuery<TQueryResult> :
        IIndexIdParam,
        IPrimaryRelayMessageQuery,
        ICloneable,
        ISplitable<TQueryResult>
        where TQueryResult : BaseMultiIndexIdQueryResult, new()
    {
        #region Data Members
        /// <summary>
        /// Index name for the query
        /// </summary>
        public string TargetIndexName
        {
            get;
            set;
        }

        /// <summary>
        /// List of the index names other than TargetIndexName to get extra tags
        /// </summary>
        public List<string> TagsFromIndexes
        {
            get;
            set;
        }

        /// <summary>
        /// If set index is dynamically before selecting items
        /// </summary>
        public TagSort TagSort
        {
            get;
            set;
        }

        /// <summary>
        /// List of index ids to lookup
        /// </summary>
        public List<byte[]> IndexIdList
        {
            get;
            set;
        }

        /// <summary>
        /// If set only that many number of items are returned from each IndexId within the IndexIdList
        /// </summary>
        public int MaxItems
        {
            get;
            set;
        }

        /// <summary>
        /// If true no data is to be fetched from data tier
        /// </summary>
        public bool ExcludeData
        {
            get;
            set;
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
            get;
            set;
        }

        /// <summary>
        /// If set default PrimaryIds of IndexIds within the IndexIdList are overriden
        /// </summary>
        public List<int> PrimaryIdList
        {
            get;
            set;
        }

        /// <summary>
        /// If set Filter conditions are applied to every item within the index
        /// </summary>
        public Filter Filter
        {
            get;
            set;
        }

        /// <summary>
        /// If set it is applied on index's sort field. IndexCondition is applied before Filter
        /// </summary>
        public IndexCondition IndexCondition
        {
            get;
            set;
        }

        /// <summary>
        /// If set it overrides MaxItems and Filter properties
        /// </summary>
        public Dictionary<byte[] /*IndexId*/, IndexIdParams /*IndexIdParams*/> IndexIdParamsMapping
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set the items get a different data tier object rather than the pre-configured one
        /// </summary>
        public FullDataIdInfo FullDataIdInfo
        {
            get;
            set;
        }

        /// <summary>
        /// If set individual caps can be set on the query conditions
        /// </summary>
        public CapCondition CapCondition
        {
            get;
            set;
        }

        /// <summary>
        /// Specifies the the manner in which IndexHeaders should be returned
        /// </summary>
        public GetIndexHeaderType GetIndexHeaderType
        {
            get;
            set;
        }

        /// <summary>
        /// If set subset processing is performed on the client
        /// </summary>
        internal bool ClientSideSubsetProcessingRequired
        {
            get;
            set;
        }

        /// <summary>
        /// Set on the server and used by the client to optimize merging
        /// </summary>
        internal abstract int MaxMergeCount
        {
            get;
        }

        /// <summary>
        /// Gets or sets the type of the domain specific processing.
        /// </summary>
        /// <value>The type of the domain specific processing.</value>
        public DomainSpecificProcessingType DomainSpecificProcessingType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the GroupBy clause.
        /// </summary>
        /// <value>The GroupBy clause.</value>
        public GroupBy GroupBy
        {
            get;
            set;
        }

        #endregion

        #region Ctors

        protected BaseMultiIndexIdQuery()
        {
            Init(null, null, null, null, null, -1, false, false, null, false, null, null, null, false, null, GetIndexHeaderType.None, DomainSpecificProcessingType.None, null);
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
                baseMultiIndexIdQuery.GetIndexHeaderType,
                baseMultiIndexIdQuery.DomainSpecificProcessingType,
                baseMultiIndexIdQuery.GroupBy);
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
            GetIndexHeaderType getIndexHeaderType,
            DomainSpecificProcessingType domainSpecificProcessingType,
            GroupBy groupBy)
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
            DomainSpecificProcessingType = domainSpecificProcessingType;
            GroupBy = groupBy;
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

        public override string ToString()
        {
            var stb = new StringBuilder();
            
            stb.Append("(").Append("TargetIndexName: ").Append(TargetIndexName).Append("),");
            
            stb.Append("(").Append("TagsFromIndexes Count: ").Append(TagsFromIndexes == null ? "Null" : TagsFromIndexes.Count.ToString());
            if (TagsFromIndexes != null && TagsFromIndexes.Count > 0)
            {
                foreach (var indexName in TagsFromIndexes)
                {
                    stb.Append("(").Append(" IndexName: ").Append(indexName).Append("),");
                }
            }
            stb.Append("),");
            
            stb.Append("(").Append("TagSort: ").Append(TagSort == null ? "Null" : TagSort.ToString()).Append("),");
            
            stb.Append("(").Append("IndexIdList Count: ").Append(IndexIdList == null ? "Null" : IndexIdList.Count.ToString());
            if (IndexIdList != null && IndexIdList.Count > 0)
            {
                foreach (var indexId in IndexIdList)
                {
                    stb.Append("(").Append(" IndexId: ").Append(IndexCacheUtils.GetReadableByteArray(indexId)).Append("),");
                }
            }
            stb.Append("),");
            
            stb.Append("(").Append("MaxItems: ").Append(MaxItems).Append("),");
            
            stb.Append("(").Append("ExcludeData: ").Append(ExcludeData).Append("),");
           
            stb.Append("(").Append("GetIndexHeader: ").Append(GetIndexHeader).Append("),");
            
            stb.Append("(").Append("GetAdditionalAvailableItemCount: ").Append(GetAdditionalAvailableItemCount).Append("),");
            
            stb.Append("(").Append("PrimaryIdList").Append(PrimaryIdList == null ? "Null" : PrimaryIdList.Count.ToString());
            if (PrimaryIdList != null && PrimaryIdList.Count > 0)
            {
                foreach (var primaryId in PrimaryIdList)
                {
                    stb.Append("(").Append("PrimaryId: ").Append(primaryId).Append("),");
                }
            }
            stb.Append("),");

            if (Filter == null)
            {
                stb.Append("(").Append("Total Filter Count: ").Append(0);
            }
            else
            {
                stb.Append("(").Append("Total Filter Count: ").Append(Filter.FilterCount).Append(" ").Append("Filter Info - ").Append(Filter.ToString());
            }
            stb.Append("),");
            stb.Append("(").Append("IndexCondition: ").Append(IndexCondition == null ? "Null" : IndexCondition.ToString()).Append("),");
            stb.Append("(").Append("IndexIdParamsMapping Count: ").Append(IndexIdParamsMapping == null ? "Null" : IndexIdParamsMapping.Count.ToString());
            if (IndexIdParamsMapping != null && IndexIdParamsMapping.Count > 0)
            {
                foreach (var indexIdParam in IndexIdParamsMapping)
                {
                    stb.Append("(");
                    stb.Append("(").Append(" IndexId: ").Append(IndexCacheUtils.GetReadableByteArray(indexIdParam.Key)).Append("),");
                    stb.Append("(").Append("IndexIdParams: ").Append(indexIdParam.Value).Append("),");
                    stb.Append("),");
                }
            }
            stb.Append("),");
            stb.Append("(").Append("CapCondition: ").Append(CapCondition == null ? "Null" : CapCondition.ToString()).Append("),");
            stb.Append("(").Append("GetIndexHeaderType: ").Append(GetIndexHeaderType.ToString()).Append("),");
            stb.Append("(").Append("ClientSideSubsetProcessingRequired: ").Append(ClientSideSubsetProcessingRequired).Append("),");
            stb.Append("(").Append("MaxMergeCount: ").Append(MaxMergeCount).Append("),");
            stb.Append("(").Append("DomainSpecificProcessingType: ").Append(DomainSpecificProcessingType).Append("),");
            stb.Append("(").Append("GroupBy: ").Append(GroupBy == null ? "Null" : GroupBy.ToString()).Append("),");

            return stb.ToString();
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
        protected int primaryId = IndexCacheUtils.MULTIINDEXQUERY_DEFAULT_PRIMARYID;

        public virtual int PrimaryId
        {
            get
            {
                return primaryId;
            }

            set
            {
                primaryId = value;
            }
        }

        #endregion

        #region ISplitable<TQueryResult> Members

        public virtual List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup)
        {
            BaseMultiIndexIdQuery<TQueryResult> query;

            List<IPrimaryRelayMessageQuery> queryList = new List<IPrimaryRelayMessageQuery>();
            Dictionary<int /*cluster number*/, Triple<List<byte[]>, List<int>, Dictionary<byte[], IndexIdParams>>> clusterParamsMapping;

            IndexCacheUtils.SplitIndexIdsByCluster(IndexIdList,
                PrimaryIdList,
                IndexIdParamsMapping,
                numClustersInGroup,
                out clusterParamsMapping);

            int mappingClusterCount = clusterParamsMapping.Count;

            ClientSideSubsetProcessingRequired =
                (numClustersInGroup > 1 && IndexIdList.Count > 1 && mappingClusterCount > 1);

            Triple<List<byte[]> /*index id list*/ , List<int> /*Primary id list*/, Dictionary<byte[], IndexIdParams> /*IndexId Params Mapping*/ > value;

            for (int i = 0; i < numClustersInGroup && queryList.Count < mappingClusterCount; i++)
            {
                if (clusterParamsMapping.TryGetValue(i, out value) == false)
                    continue;

                query = (BaseMultiIndexIdQuery<TQueryResult>)Clone();

                query.PrimaryId = i;
                query.IndexIdList = value.First;
                query.PrimaryIdList = value.Second;
                query.IndexIdParamsMapping = value.Third;

                queryList.Add(query);
            }

            return queryList;
        }

        public virtual List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup,
            int localClusterPosition,
            out IPrimaryRelayMessageQuery localQuery)
        {
            BaseMultiIndexIdQuery<TQueryResult> query;

            List<IPrimaryRelayMessageQuery> queryList = new List<IPrimaryRelayMessageQuery>();
            Dictionary<int /*cluster number*/, Triple<List<byte[]>, List<int>, Dictionary<byte[], IndexIdParams>>> clusterParamsMapping;

            localQuery = null;

            IndexCacheUtils.SplitIndexIdsByCluster(
                IndexIdList,
                PrimaryIdList,
                IndexIdParamsMapping,
                numClustersInGroup,
                out clusterParamsMapping);

            int mappingClusterCount = clusterParamsMapping.Count;
            int queryCount = 0;

            ClientSideSubsetProcessingRequired =
                (numClustersInGroup > 1 && IndexIdList.Count > 1 && mappingClusterCount > 1);

            Triple<List<byte[]> /*index id list*/, List<int> /*primary id list*/, Dictionary<byte[], IndexIdParams> /*index id parms*/ > value;

            for (int i = 0; i < numClustersInGroup; i++)
            {
                if (clusterParamsMapping.TryGetValue(i, out value) == false)
                    continue;

                query = (BaseMultiIndexIdQuery<TQueryResult>)Clone();

                query.PrimaryId = i;
                query.IndexIdList = value.First;
                query.PrimaryIdList = value.Second;
                query.IndexIdParamsMapping = value.Third;
                query.ExcludeData = true;               // dont retrieve data in these split queries for now

                if (query.PrimaryId == localClusterPosition)
                {
                    localQuery = query;
                }
                else
                {
                    queryList.Add(query);
                }

                if (++queryCount == mappingClusterCount)
                    break;
            }

            return queryList;
        }

        #endregion

        #region IMergeableQueryResult<TQueryResult> Members

        public abstract TQueryResult MergeResults(IList<TQueryResult> QueryResults);

        #endregion

        #region ICloneable Members

        public abstract object Clone();

        #endregion
    }
}
