using System;
using System.Text;
using System.Collections.Generic;
using MySpace.Common.IO;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class PagedIndexQuery : BaseMultiIndexIdQuery<PagedIndexQueryResult>
    {
        #region Data Members

        public int PageSize { get; set; }

        /// <summary>
        /// Set to zero if all items are required
        /// </summary>
        public int PageNum { get; set;}

        internal override int MaxMergeCount
        {
            get
            {
                return (PageNum == 0) ? Int32.MaxValue : PageNum * PageSize;
            }
        }

        /// <summary>
        /// Set this to true to get total number of items that satisfy Filter(s)
        /// </summary>
        public bool GetPageableItemCount
        {
            get
            {
                return GetAdditionalAvailableItemCount;
            }
            set
            {
                GetAdditionalAvailableItemCount = value;
            }
        }

        internal bool ClientSidePaging
        {
            get
            {
                return ClientSideSubsetProcessingRequired;
            }
            set
            {
                ClientSideSubsetProcessingRequired = value;
            }
        }

        public int MaxItemsPerIndex
        {
            get
            {
                return MaxItems;
            }
            set
            {
                MaxItems = value;
            }
        }

        private int numClustersInGroup;

        #endregion

        #region Methods

        public override string ToString()
        {
            var stb = new StringBuilder();
            stb.Append("--- Paged Query ---");
            stb.Append("(").Append(" PageNum: ").Append(PageNum).Append("),");
            stb.Append("(").Append("PageSize: ").Append(PageSize).Append("),");
            stb.Append(base.ToString());
            return stb.ToString();
        }

        #endregion

        #region Ctors

        public PagedIndexQuery()
        {
            Init(null, null, -1, -1, null, null, null, -1, false, false, null, false, null, null, null, false);
        }

        public PagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName)
        {
            Init(indexIdList, null, pageSize, pageNum, targetIndexName, null, null, -1, false, false, null, false, null, null, null, false);
        }

        public PagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName, int maxItemsPerIndex)
        {
            Init(indexIdList, null, pageSize, pageNum, targetIndexName, null, null, maxItemsPerIndex, false, false, null, false, null, null, null, false);
        }

        [Obsolete("This constructor is obsolete; use object initializer instead")]
        public PagedIndexQuery(List<byte[]> indexIdList,
            int pageSize,
            int pageNum,
            string targetIndexName,
            List<string> tagsFromIndexes,
            TagSort tagSort,
            CriterionList criterionList,
            int maxItemsPerIndex,
            bool excludeData,
            bool getIndexHeader)
        {
            Init(indexIdList,
               null,
               pageSize,
               pageNum,
               targetIndexName,
               tagsFromIndexes,
               tagSort,
               maxItemsPerIndex,
               excludeData,
               getIndexHeader,
               null,
               false,
               null,
               null,
               null,
               false);
        }

        public PagedIndexQuery(PagedIndexQuery query)
            : base(query)
        {
            PageNum = query.PageNum;
            PageSize = query.PageSize;
        }

        private void Init(List<byte[]> indexIdList,
            List<int> primaryIdList,
            int pageSize,
            int pageNum,
            string targetIndexName,
            List<string> tagsFromIndexes,
            TagSort tagSort,
            int maxItemsPerIndex,
            bool excludeData,
            bool getIndexHeader,
            Dictionary<byte[], IndexIdParams> indexIdParamsMapping,
            bool getPageableItemCount,
            Filter filter,
            FullDataIdInfo fullDataIdInfo,
            IndexCondition indexCondition,
            bool clientSidePaging)
        {
            IndexIdList = indexIdList;
            PrimaryIdList = primaryIdList;
            PageSize = pageSize;
            PageNum = pageNum;
            TargetIndexName = targetIndexName;
            TagsFromIndexes = tagsFromIndexes;
            TagSort = tagSort;
            MaxItemsPerIndex = maxItemsPerIndex;
            ExcludeData = excludeData;
            GetIndexHeader = getIndexHeader;
            IndexIdParamsMapping = indexIdParamsMapping;
            GetPageableItemCount = getPageableItemCount;
            Filter = filter;
            FullDataIdInfo = fullDataIdInfo;
            IndexCondition = indexCondition;
            ClientSidePaging = clientSidePaging;
        }

        #endregion

        #region ISplitable<TQueryResult> Members

        public override List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup)
        {
            this.numClustersInGroup = numClustersInGroup;

            return base.SplitQuery(numClustersInGroup);
        }

        public override List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup,
            int localClusterPosition,
            out IPrimaryRelayMessageQuery localQuery)
        {
            this.numClustersInGroup = numClustersInGroup;

            return base.SplitQuery(numClustersInGroup, localClusterPosition, out localQuery);
        }

        #endregion

        #region IMergeableQueryResult<TQueryResult> Members
        public override PagedIndexQueryResult MergeResults(IList<PagedIndexQueryResult> partialResults)
        {
            PagedIndexQueryResult finalResult = default(PagedIndexQueryResult);

            if (partialResults == null || partialResults.Count == 0)
            {
                return finalResult;
            }

            // We have partialResults to process
            BaseComparer baseComparer = null;
            ByteArrayEqualityComparer byteArrayEqualityComparer = new ByteArrayEqualityComparer();
            Dictionary<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> completeIndexIdIndexHeaderMapping =
                new Dictionary<byte[], IndexHeader>(byteArrayEqualityComparer);

            // resultVer required to determine if server is correctly performing paging logic
            int resultVer = 0;

            if (partialResults.Count == 1)
            {
                #region  Just one cluster was targeted, so no need to merge anything

                finalResult = partialResults[0];
                if (finalResult != null)
                {
                    resultVer = finalResult.CurrentVersion;
                }

                #endregion
            }
            else
            {
                #region  More than one clusters was targeted

                List<ResultItem> completeResultItemList = new List<ResultItem>();
                GroupByResult completeGroupByResult = new GroupByResult(null);
                int totalCount = 0;
                int pageableItemCount = 0;
                StringBuilder exceptionStringBuilder = new StringBuilder();
                bool indexCapSet = false;
                int indexCap = 0;

                foreach (PagedIndexQueryResult partialResult in partialResults)
                {
                    if (partialResult != null)
                    {
                        #region Update resultVer

                        if (resultVer == 0)
                        {
                            resultVer = partialResult.CurrentVersion;
                        }

                        #endregion

                        #region Assign IndexCap

                        if (!indexCapSet)
                        {
                            indexCap = partialResult.IndexCap;
                            indexCapSet = true;
                        }

                        #endregion

                        #region Compute TotalCount

                        totalCount += partialResult.TotalCount;

                        #endregion

                        #region Compute PageableItemCount

                        if (GetPageableItemCount)
                        {
                            pageableItemCount += partialResult.AdditionalAvailableItemCount;
                        }

                        #endregion

                        #region Merge Results

                        if ((partialResult.ResultItemList != null && partialResult.ResultItemList.Count > 0) || 
                            (partialResult.GroupByResult != null && partialResult.GroupByResult.Count > 0))
                        {
                            if (baseComparer == null)
                            {
                                baseComparer = new BaseComparer(partialResult.IsTagPrimarySort,
                                                                partialResult.SortFieldName, 
                                                                partialResult.SortOrderList);
                                completeGroupByResult = new GroupByResult(baseComparer);
                            }

                            if (GroupBy == null)
                            {
                                MergeAlgo.MergeItemLists(ref completeResultItemList,
                                                         partialResult.ResultItemList,
                                                         MaxMergeCount,
                                                         baseComparer);
                            }
                            else
                            {
                                MergeAlgo.MergeGroupResult(ref completeGroupByResult,
                                                           partialResult.GroupByResult,
                                                           MaxMergeCount,
                                                           baseComparer);
                            }
                        }
                        #endregion

                        #region Update IndexIdIndexHeaderMapping

                        if (GetIndexHeaderType != GetIndexHeaderType.None &&
                            partialResult.IndexIdIndexHeaderMapping != null &&
                            partialResult.IndexIdIndexHeaderMapping.Count > 0)
                        {
                            foreach (KeyValuePair<byte[], IndexHeader> kvp in partialResult.IndexIdIndexHeaderMapping)
                            {
                                if (!completeIndexIdIndexHeaderMapping.ContainsKey(kvp.Key))
                                {
                                    completeIndexIdIndexHeaderMapping.Add(kvp.Key, kvp.Value);
                                }
                            }
                        }

                        #endregion

                        #region Update exceptionInfo

                        if (!String.IsNullOrEmpty(partialResult.ExceptionInfo))
                        {
                            exceptionStringBuilder.Append(partialResult.ExceptionInfo);
                            exceptionStringBuilder.Append(" ");
                        }

                        #endregion
                    }
                }

                #region Create FinalResult

                finalResult = new PagedIndexQueryResult
                {
                    ResultItemList = completeResultItemList,
                    GroupByResult = completeGroupByResult,
                    TotalCount = totalCount,
                    AdditionalAvailableItemCount = pageableItemCount,
                    IndexCap = indexCap,
                };

                //Assign sort fields for use in GroupBy remote queries
                if(baseComparer != null)
                {
                    finalResult.IsTagPrimarySort = baseComparer.IsTagPrimarySort;
                    finalResult.SortFieldName = baseComparer.SortFieldName;
                    finalResult.SortOrderList = baseComparer.SortOrderList;
                }

                if (GetIndexHeaderType != GetIndexHeaderType.None && completeIndexIdIndexHeaderMapping.Count > 0)
                {
                    finalResult.IndexIdIndexHeaderMapping = completeIndexIdIndexHeaderMapping;
                }

                if (exceptionStringBuilder.Length > 0)
                {
                    finalResult.ExceptionInfo = exceptionStringBuilder.ToString();
                }

                #endregion

                #endregion
            }

            #region Determine whether client side paging is required

            bool performClientSidePaging;
            if (resultVer < PagedIndexQueryResult.CORRECT_SERVERSIDE_PAGING_LOGIC_VERSION)
            {
                // this.ClientSidePaging cannot be trusted
                performClientSidePaging = numClustersInGroup > 1 && PageNum != 0;
            }
            else
            {
                // this.ClientSidePaging can be trusted
                performClientSidePaging = ClientSidePaging && PageNum != 0;
            }

            #endregion

            #region Perform Paging and Update IndexIdIndexHeaderMapping if required

            if (performClientSidePaging && finalResult != null)
            {
                #region Paging Logic

                int start = (PageNum - 1) * PageSize;
                if (GroupBy == null)
                {
                    int end = (PageNum*PageSize) < finalResult.ResultItemList.Count ? (PageNum*PageSize) : finalResult.ResultItemList.Count;
                    List<ResultItem> filteredResultItemList = new List<ResultItem>();
                    for (int i = start; i < end; i++)
                    {
                        filteredResultItemList.Add(finalResult.ResultItemList[i]);
                    }
                    finalResult.ResultItemList = filteredResultItemList;
                }
                else if (finalResult.GroupByResult != null && finalResult.GroupByResult.Count > 0)
                {
                    int end = (PageNum * PageSize) < finalResult.GroupByResult.Count ? (PageNum * PageSize) : finalResult.GroupByResult.Count;
                    GroupByResult filteredGroupByResult = new GroupByResult(baseComparer);
                    for (int i = start; i < end; i++)
                    {
                        filteredGroupByResult.Add(finalResult.GroupByResult[i].CompositeKey, finalResult.GroupByResult[i]);
                    }
                    finalResult.GroupByResult = filteredGroupByResult;
                }

                #endregion

                #region Update IndexIdIndexHeaderMapping to only include metadata relevant after paging

                if (partialResults.Count != 1 && GetIndexHeaderType == GetIndexHeaderType.ResultItemsIndexIds && completeIndexIdIndexHeaderMapping.Count > 0)
                {
                    Dictionary<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> filteredIndexIdIndexHeaderMapping =
                        new Dictionary<byte[], IndexHeader>(byteArrayEqualityComparer);
                    if (GroupBy == null)
                    {
                        foreach (ResultItem resultItem in finalResult.ResultItemList)
                        {
                            if (!filteredIndexIdIndexHeaderMapping.ContainsKey(resultItem.IndexId))
                            {
                                filteredIndexIdIndexHeaderMapping.Add(resultItem.IndexId, completeIndexIdIndexHeaderMapping[resultItem.IndexId]);
                            }
                        }
                    }
                    else if (finalResult.GroupByResult != null && finalResult.GroupByResult.Count > 0)
                    {
                        foreach (ResultItemBag resultItemBag in finalResult.GroupByResult)
                        {
                            for (int i = 0; i < resultItemBag.Count; i++)
                            {
                                ResultItem resultItem = resultItemBag[i];
                                if (!filteredIndexIdIndexHeaderMapping.ContainsKey(resultItem.IndexId))
                                {
                                    filteredIndexIdIndexHeaderMapping.Add(resultItem.IndexId, completeIndexIdIndexHeaderMapping[resultItem.IndexId]);
                                }
                            }
                        }
                    }
                    finalResult.IndexIdIndexHeaderMapping = filteredIndexIdIndexHeaderMapping.Count > 0 ? filteredIndexIdIndexHeaderMapping : null;
                }

                #endregion
            }

            #endregion

            return finalResult;
        }

        #endregion

        #region IRelayMessageQuery Members

        public override byte QueryId
        {
            get
            {
                return (byte)QueryTypes.PagedTaggedIndexQuery;
            }
        }

        #endregion

        #region IVersionSerializable Members

        public override void Serialize(IPrimitiveWriter writer)
        {
            //PageSize
            writer.Write(PageSize);

            //PageNum
            writer.Write(PageNum);

            //TargetIndexName
            writer.Write(TargetIndexName);

            //TagsFromIndexes
            if (TagsFromIndexes == null || TagsFromIndexes.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)TagsFromIndexes.Count);
                foreach (string str in TagsFromIndexes)
                {
                    writer.Write(str);
                }
            }

            //TagSort
            if (TagSort == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                Serializer.Serialize(writer.BaseStream, TagSort);
            }

            //IndexIdList
            if (IndexIdList == null || IndexIdList.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)IndexIdList.Count);
                foreach (byte[] indexId in IndexIdList)
                {
                    if (indexId == null || indexId.Length == 0)
                    {
                        writer.Write((ushort)0);
                    }
                    else
                    {
                        writer.Write((ushort)indexId.Length);
                        writer.Write(indexId);
                    }
                }
            }

            //Write a byte to account for deprecated CriterionList
            writer.Write((byte)0);

            //MaxItemsPerIndex
            writer.Write(MaxItemsPerIndex);

            //ExcludeData
            writer.Write(ExcludeData);

            //GetIndexHeader
            writer.Write(GetIndexHeader);

            //GetPageableItemCount
            writer.Write(GetPageableItemCount);

            //PrimaryIdList
            if (PrimaryIdList == null || PrimaryIdList.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)PrimaryIdList.Count);
                foreach (int primaryId in PrimaryIdList)
                {
                    writer.Write(primaryId);
                }
            }

            //Filter
            if (Filter == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)Filter.FilterType);
                Serializer.Serialize(writer.BaseStream, Filter);
            }

            //IndexIdParamsMapping
            if (IndexIdParamsMapping == null || IndexIdParamsMapping.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)IndexIdParamsMapping.Count);
                foreach (KeyValuePair<byte[] /*IndexId*/, IndexIdParams /*IndexIdParams*/> kvp in IndexIdParamsMapping)
                {
                    //IndexId
                    if (kvp.Key == null || kvp.Key.Length == 0)
                    {
                        writer.Write((ushort)0);

                        //No need to serialize IndexIdParams
                    }
                    else
                    {
                        writer.Write((ushort)kvp.Key.Length);
                        writer.Write(kvp.Key);

                        //IndexIdParams
                        Serializer.Serialize(writer, kvp.Value);
                    }
                }
            }

            //FullDataIdInfo
            if (FullDataIdInfo == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, FullDataIdInfo);
            }

            //ClientSidePaging
            writer.Write(ClientSidePaging);

            //IndexCondition
            if (IndexCondition == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, IndexCondition);
            }

            //CapCondition
            if (CapCondition == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, CapCondition);
            }

            //GetIndexHeaderType
            writer.Write((byte)GetIndexHeaderType);

            //DomainSpecificProcessingType
            writer.Write((byte)DomainSpecificProcessingType);

            //GroupBy
            if (GroupBy == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, GroupBy);
            }
        }

        public override void Deserialize(IPrimitiveReader reader, int version)
        {
            //PageSize
            PageSize = reader.ReadInt32();

            //PageNum
            PageNum = reader.ReadInt32();

            //TargetIndexName
            TargetIndexName = reader.ReadString();

            //TagsFromIndexes
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                TagsFromIndexes = new List<string>(count);
                for (ushort i = 0; i < count; i++)
                {
                    TagsFromIndexes.Add(reader.ReadString());
                }
            }

            //TagSort
            if (reader.ReadByte() != 0)
            {
                TagSort = new TagSort();
                Serializer.Deserialize(reader.BaseStream, TagSort);
            }

            //IndexIdList
            count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexIdList = new List<byte[]>(count);
                ushort len;
                for (ushort i = 0; i < count; i++)
                {
                    len = reader.ReadUInt16();
                    if (len > 0)
                    {
                        IndexIdList.Add(reader.ReadBytes(len));
                    }
                }
            }

            //Read a byte to account for deprecated CriterionList
            reader.ReadByte();

            //MaxItemsPerIndex
            MaxItemsPerIndex = reader.ReadInt32();

            //ExcludeData
            ExcludeData = reader.ReadBoolean();

            //GetIndexHeader
            GetIndexHeader = reader.ReadBoolean();

            if (version >= 2)
            {
                //GetPageableItemCount
                GetPageableItemCount = reader.ReadBoolean();
            }

            if (version >= 3)
            {
                //PrimaryIdList
                count = reader.ReadUInt16();
                if (count > 0)
                {
                    PrimaryIdList = new List<int>(count);
                    for (ushort i = 0; i < count; i++)
                    {
                        PrimaryIdList.Add(reader.ReadInt32());
                    }
                }
            }

            if (version >= 4)
            {
                //Filter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    FilterType filterType = (FilterType)b;
                    Filter = FilterFactory.CreateFilter(reader, filterType);
                }
            }

            if (version >= 5)
            {
                //IndexIdParamsMapping
                count = reader.ReadUInt16();
                if (count > 0)
                {
                    IndexIdParamsMapping = new Dictionary<byte[], IndexIdParams>(count, new ByteArrayEqualityComparer());
                    byte[] indexId;
                    IndexIdParams indexIdParam;
                    ushort len;

                    for (ushort i = 0; i < count; i++)
                    {
                        len = reader.ReadUInt16();
                        indexId = null;
                        if (len > 0)
                        {
                            indexId = reader.ReadBytes(len);

                            indexIdParam = new IndexIdParams();
                            Serializer.Deserialize(reader.BaseStream, indexIdParam);

                            IndexIdParamsMapping.Add(indexId, indexIdParam);
                        }
                    }
                }
            }

            if (version >= 6)
            {
                //FullDataIdInfo
                if (reader.ReadBoolean())
                {
                    FullDataIdInfo = new FullDataIdInfo();
                    Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
                }
            }

            if (version >= 7)
            {
                //ClientSidePaging
                ClientSidePaging = reader.ReadBoolean();
            }

            if (version >= 8)
            {
                //IndexCondition
                if (reader.ReadBoolean())
                {
                    IndexCondition = new IndexCondition();
                    Serializer.Deserialize(reader.BaseStream, IndexCondition);
                }
            }

            if (version >= 9)
            {
                //CapCondition
                if (reader.ReadBoolean())
                {
                    CapCondition = new CapCondition();
                    Serializer.Deserialize(reader.BaseStream, CapCondition);
                }
            }

            if (version >= 10)
            {
                //GetIndexHeaderType
                GetIndexHeaderType = (GetIndexHeaderType)reader.ReadByte();
            }

            if (version >= 11)
            {
                //DomainSpecificProcessingType
                DomainSpecificProcessingType = (DomainSpecificProcessingType)reader.ReadByte();
            }

            if (version >= 12)
            {
                //GroupBy
                if (reader.ReadBoolean())
                {
                    GroupBy = new GroupBy();
                    Serializer.Deserialize(reader.BaseStream, GroupBy);
                }
            }
        }

        private const int CURRENT_VERSION = 12;
        public override int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        #endregion

        #region ICloneable Members

        public sealed override object Clone()
        {
            return new PagedIndexQuery(this);
        }

        #endregion
    }
}
