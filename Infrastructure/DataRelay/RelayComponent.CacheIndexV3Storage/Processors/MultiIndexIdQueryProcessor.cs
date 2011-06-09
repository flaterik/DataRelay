using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal abstract class MultiIndexIdQueryProcessor<TQueryResult> where TQueryResult : BaseMultiIndexIdQueryResult, new()
    {
        /// <summary>
        /// Processes the specified query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>Query Result</returns>
        internal TQueryResult Process(BaseMultiIndexIdQuery<TQueryResult> query,
            MessageContext messageContext,
            IndexStoreContext storeContext)
        {
            TQueryResult result;
            List<ResultItem> resultItemList = new List<ResultItem>();
            Dictionary<byte[], IndexHeader> indexIdIndexHeaderMapping = null;
            bool isTagPrimarySort = false;
            string sortFieldName = null;
            List<SortOrder> sortOrderList = null;
            int totalCount = 0;
            int additionalAvailableItemCount = 0;
            GroupByResult groupByResult = null;
            StringBuilder exceptionInfo = new StringBuilder();
            int indexCap = 0;
            IndexTypeMapping indexTypeMapping =
                storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

            try
            {
                if (query.IndexIdList.Count > 0)
                {
                    #region Validate Query

                    ValidateQuery(indexTypeMapping, query, messageContext);

                    #endregion

                    #region Set sort vars

                    Index targetIndexInfo = indexTypeMapping.IndexCollection[query.TargetIndexName];
                    indexCap = targetIndexInfo.MaxIndexSize;

                    if (query.TagSort != null)
                    {
                        isTagPrimarySort = query.TagSort.IsTag;
                        sortFieldName = query.TagSort.TagName;
                        sortOrderList = new List<SortOrder>(1) { query.TagSort.SortOrder };
                    }
                    else
                    {
                        isTagPrimarySort = targetIndexInfo.PrimarySortInfo.IsTag;
                        sortFieldName = targetIndexInfo.PrimarySortInfo.FieldName;
                        sortOrderList = targetIndexInfo.PrimarySortInfo.SortOrderList;
                    }
                    BaseComparer baseComparer = new BaseComparer(isTagPrimarySort, sortFieldName, sortOrderList);
                    groupByResult = new GroupByResult(baseComparer);
                    #endregion

                    #region Prepare ResultList

                    CacheIndexInternal targetIndex;
                    IndexIdParams indexIdParam;
                    int maxExtractCount;
                    byte[] metadata;
                    MetadataPropertyCollection metadataPropertyCollection;
                    Dictionary<KeyValuePair<byte[], string>, CacheIndexInternal> internalIndexDictionary = new Dictionary<KeyValuePair<byte[], string>, CacheIndexInternal>();

                    int maxMergeCount = query.MaxMergeCount;

                    IndexCondition queryIndexCondition = query.IndexCondition;

                    for (int i = 0; i < query.IndexIdList.Count; i++)
                    {
                        #region Extract index and apply criteria

                        indexIdParam = query.GetParamsForIndexId(query.IndexIdList[i]);
                        maxExtractCount = ComputeMaxExtractCount(indexIdParam.MaxItems,
                            query.GetAdditionalAvailableItemCount,
                            indexIdParam.Filter,
                            query.MaxMergeCount);

                        // Note: This should be changed later and just extracted once if it is also requested in GetIndexHeader
                        metadata = null;
                        metadataPropertyCollection = null;
                        if (indexTypeMapping.MetadataStoredSeperately)
                        {
                            IndexServerUtils.GetMetadataStoredSeperately(indexTypeMapping,
                                messageContext.TypeId,
                                messageContext.PrimaryId,
                                query.IndexIdList[i],
                                storeContext,
                                out metadata,
                                out metadataPropertyCollection);
                        }

                        targetIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                            messageContext.TypeId,
                            (query.PrimaryIdList != null && i < query.PrimaryIdList.Count) ?
                                query.PrimaryIdList[i] :
                                IndexCacheUtils.GeneratePrimaryId(query.IndexIdList[i]),
                            query.IndexIdList[i],
                            targetIndexInfo.ExtendedIdSuffix,
                            query.TargetIndexName,
                            maxExtractCount,
                            indexIdParam.Filter,
                            true,
                            queryIndexCondition,
                            false,
                            false,
                            targetIndexInfo.PrimarySortInfo,
                            targetIndexInfo.LocalIdentityTagList,
                            targetIndexInfo.StringHashCodeDictionary,
                            query.CapCondition,
                            targetIndexInfo.IsMetadataPropertyCollection,
                            metadataPropertyCollection,
                            query.DomainSpecificProcessingType,
                            storeContext.DomainSpecificConfig,
                            null,
                            query.GroupBy,
                            false);

                        #endregion

                        if (targetIndex != null)
                        {
                            totalCount += targetIndex.OutDeserializationContext.TotalCount;
                            additionalAvailableItemCount += targetIndex.Count;
                            internalIndexDictionary.Add(new KeyValuePair<byte[], string>(query.IndexIdList[i], query.TargetIndexName),
                                targetIndex);

                            SetItemCounter(messageContext.TypeId, targetIndex.OutDeserializationContext);

                            #region Dynamic tag sort

                            if (query.TagSort != null)
                            {
                                targetIndex.Sort(query.TagSort);
                            }

                            #endregion

                            #region Get items from index and merge

                            if (query.GroupBy == null)
                            {
                                MergeAlgo.MergeItemLists(ref resultItemList,
                                    CacheIndexInternalAdapter.GetResultItemList(targetIndex, 1, int.MaxValue),
                                    query.MaxMergeCount,
                                    baseComparer);
                            }
                            else
                            {
                                MergeAlgo.MergeGroupResult(ref groupByResult,
                                    targetIndex.GroupByResult,
                                    query.MaxMergeCount,
                                    baseComparer);
                            }

                            if ((i != query.IndexIdList.Count - 1) && (resultItemList.Count == maxMergeCount))
                            {
                                AdjustIndexCondition(GetConditionBoundaryBytes(resultItemList, groupByResult, query.GroupBy, isTagPrimarySort, sortFieldName), 
                                    ref queryIndexCondition, 
                                    baseComparer);
                            }

                            #endregion
                        }
                    }

                    #endregion

                    #region Subset Processing

                    ProcessSubsets(query, ref resultItemList, ref groupByResult, baseComparer);

                    #endregion

                    #region Get Extra Tags for IndexIds in the list

                    //Note: Getting extra tags from GroupByResult not supported for now

                    if (query.TagsFromIndexes != null && query.TagsFromIndexes.Count != 0)
                    {
                        KeyValuePair<byte[] /*IndexId */, string /*IndexName*/> kvp;
                        CacheIndexInternal additionalCacheIndexInternal;

                        #region Form IndexId - PrimaryId Mapping

                        Dictionary<byte[] /*IndexId */, int /*PrimaryId*/> indexIdPrimaryIdMapping =
                            new Dictionary<byte[] /*IndexId */, int /*PrimaryId*/>(query.IndexIdList.Count, new ByteArrayEqualityComparer());
                        if (query.PrimaryIdList != null && query.PrimaryIdList.Count > 0)
                        {
                            //Form dictionary of IndexIdPrimaryIdMapping
                            for (int i = 0; i < query.IndexIdList.Count && i < query.PrimaryIdList.Count; i++)
                            {
                                indexIdPrimaryIdMapping.Add(query.IndexIdList[i], query.PrimaryIdList[i]);
                            }
                        }

                        #endregion

                        int indexPrimaryId;
                        foreach (ResultItem resultItem in resultItemList)
                        {
                            foreach (string indexName in query.TagsFromIndexes)
                            {
                                Index indexInfo = indexTypeMapping.IndexCollection[indexName];
                                kvp = new KeyValuePair<byte[], string>(resultItem.IndexId, indexName);
                                if (!internalIndexDictionary.ContainsKey(kvp))
                                {
                                    additionalCacheIndexInternal = IndexServerUtils.GetCacheIndexInternal(storeContext,
                                        messageContext.TypeId,
                                        indexIdPrimaryIdMapping.TryGetValue(resultItem.IndexId, out indexPrimaryId) ?
                                            indexPrimaryId :
                                            IndexCacheUtils.GeneratePrimaryId(resultItem.IndexId),
                                        resultItem.IndexId,
                                        indexInfo.ExtendedIdSuffix,
                                        indexName,
                                        0,
                                        null,
                                        true,
                                        null,
                                        false,
                                        false,
                                        indexInfo.PrimarySortInfo,
                                        indexInfo.LocalIdentityTagList,
                                        indexInfo.StringHashCodeDictionary,
                                        null,
                                        indexInfo.IsMetadataPropertyCollection,
                                        null,
                                        query.DomainSpecificProcessingType,
                                        storeContext.DomainSpecificConfig,
                                        null,
                                        null,
                                        false);

                                    if (additionalCacheIndexInternal != null)
                                    {
                                        SetItemCounter(messageContext.TypeId, additionalCacheIndexInternal.OutDeserializationContext);

                                        internalIndexDictionary.Add(kvp, additionalCacheIndexInternal);
                                        try
                                        {
                                            IndexServerUtils.GetTags(additionalCacheIndexInternal, resultItem, resultItem);
                                        }
                                        catch (Exception ex)
                                        {
                                            LoggingUtil.Log.Error(ex.ToString());
                                            exceptionInfo.Append(" | " + ex.Message);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    #endregion

                    #region Get IndexHeader

                    if (query.GetIndexHeaderType == GetIndexHeaderType.AllIndexIds)
                    {
                        //Get IndexHeader for all IndexIds
                        indexIdIndexHeaderMapping = new Dictionary<byte[], IndexHeader>(new ByteArrayEqualityComparer());

                        for (int i = 0; i < query.IndexIdList.Count; i++)
                        {
                            byte[] indexId = query.IndexIdList[i];
                            CacheIndexInternal targetIndexCacheIndexInternal;

                            if (!indexIdIndexHeaderMapping.ContainsKey(indexId) &&
                                internalIndexDictionary.TryGetValue(new KeyValuePair<byte[], string>(indexId, query.TargetIndexName), out targetIndexCacheIndexInternal))
                            {
                                indexIdIndexHeaderMapping.Add(indexId, GetIndexHeader(internalIndexDictionary,
                                    targetIndexCacheIndexInternal,
                                    indexId,
                                    query,
                                    indexTypeMapping,
                                    messageContext.TypeId,
                                    storeContext,
                                    i));
                            }
                        }
                    }
                    else if (query.GetIndexHeaderType == GetIndexHeaderType.ResultItemsIndexIds)
                    {
                        //Get IndexHeader just for IndexIds present in the result
                        indexIdIndexHeaderMapping = new Dictionary<byte[], IndexHeader>(new ByteArrayEqualityComparer());

                        if (query.GroupBy == null)
                        {
                            for (int i = 0; i < resultItemList.Count; i++)
                            {
                                ResultItem resultItem = resultItemList[i];
                                if (!indexIdIndexHeaderMapping.ContainsKey(resultItem.IndexId))
                                {
                                    CacheIndexInternal targetIndexCacheIndexInternal;
                                    internalIndexDictionary.TryGetValue(new KeyValuePair<byte[], string>(resultItem.IndexId, query.TargetIndexName),
                                        out targetIndexCacheIndexInternal);
                                    indexIdIndexHeaderMapping.Add(resultItem.IndexId,
                                        GetIndexHeader(internalIndexDictionary,
                                        targetIndexCacheIndexInternal,
                                        resultItem.IndexId,
                                        query,
                                        indexTypeMapping,
                                        messageContext.TypeId,
                                        storeContext,
                                        i));
                                }
                            }
                        }
                        else
                        {
                            foreach (ResultItemBag resultItemBag in groupByResult)
                            {
                                for (int i = 0; i < resultItemBag.Count; i++)
                                {
                                    ResultItem resultItem = resultItemBag[i];
                                    if (!indexIdIndexHeaderMapping.ContainsKey(resultItem.IndexId))
                                    {
                                        CacheIndexInternal targetIndexCacheIndexInternal;
                                        internalIndexDictionary.TryGetValue(new KeyValuePair<byte[], string>(resultItem.IndexId, query.TargetIndexName),
                                            out targetIndexCacheIndexInternal);
                                        indexIdIndexHeaderMapping.Add(resultItem.IndexId,
                                            GetIndexHeader(internalIndexDictionary,
                                            targetIndexCacheIndexInternal,
                                            resultItem.IndexId,
                                            query,
                                            indexTypeMapping,
                                            messageContext.TypeId,
                                            storeContext,
                                            i));
                                    }
                                }
                            }
                        }
                    }

                    #endregion

                    #region Get data

                    if (!query.ExcludeData)
                    {
                        DataTierUtil.GetData(resultItemList, 
                            groupByResult,
                            storeContext, messageContext, 
                            indexTypeMapping.FullDataIdFieldList,
                            query.FullDataIdInfo);
                    }

                    #endregion
                }

                result = new TQueryResult
                             {
                                 ResultItemList = resultItemList,
                                 IndexIdIndexHeaderMapping = indexIdIndexHeaderMapping,
                                 TotalCount = totalCount,
                                 AdditionalAvailableItemCount = additionalAvailableItemCount,
                                 IsTagPrimarySort = isTagPrimarySort,
                                 SortFieldName = sortFieldName,
                                 SortOrderList = sortOrderList,
                                 IndexCap = indexCap,
                                 GroupByResult = groupByResult,
                                 ExceptionInfo = exceptionInfo.ToString()
                             };

                #region Log Potentially Bad Queries

                if (indexTypeMapping.QueryOverrideSettings != null &&
                    indexTypeMapping.QueryOverrideSettings.MaxResultItemsThresholdLog > 0 &&
                    resultItemList != null &&
                    resultItemList.Count > indexTypeMapping.QueryOverrideSettings.MaxResultItemsThresholdLog)
                {
                    LoggingUtil.Log.ErrorFormat("Encountered potentially Bad Paged Query with Large Result Set of {0}.  AddressHistory: {1}.  Query Info: {2}",
                                                resultItemList.Count,
                                                IndexServerUtils.FormatAddressHistory(messageContext.AddressHistory),
                                                FormatQueryInfo(query));
                }

                LoggingUtil.Log.DebugFormat("QueryInfo: {0}, AddressHistory: {1}", FormatQueryInfo(query), IndexServerUtils.FormatAddressHistory(messageContext.AddressHistory));

                #endregion

                SetIndexIdListCounter(messageContext.TypeId, query);
            }
            catch (Exception ex)
            {
                exceptionInfo.Append(" | " + ex.Message);
                result = new TQueryResult
                             {
                                 ExceptionInfo = exceptionInfo.ToString()
                             };
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing PagedIndexQuery : {1}", messageContext.TypeId, ex);
            }
            return result;
        }

        private static byte[] GetConditionBoundaryBytes(List<ResultItem> resultItemList, 
            GroupByResult groupByResult, 
            GroupBy groupBy,
            bool isTagPrimarySort,
            string sortFieldName)
        {
            ResultItem resultItem = groupBy == null
                                        ? resultItemList[resultItemList.Count - 1]
                                        : groupByResult.First.First;

            byte[] conditionBoundry;
            if (!isTagPrimarySort) // based on item id
            {
                conditionBoundry = resultItem.ItemId;
            }
            else // based on tag
            {
                resultItem.TryGetTagValue(sortFieldName, out conditionBoundry);
            }
            return conditionBoundry;
        }

        /// <summary>
        /// Adjust current index condition based on the result item list 
        /// </summary>
        /// <param name="conditionBoundry">condition boundry bytes</param>
        /// <param name="queryIndexCondition">query index condition</param>
        /// <param name="comparer">comparer the sort is based on</param>
        private static void AdjustIndexCondition(byte[] conditionBoundry, ref IndexCondition queryIndexCondition, BaseComparer comparer)
        {
            if (conditionBoundry != null)
            {
                // check the condiftion boundry and put it into the queryIndexCondition
                if (comparer.SortOrderList[0].SortBy == SortBy.DESC) // if desc
                {
                    if (queryIndexCondition != null)
                    {
                        if (queryIndexCondition.InclusiveMinValue == null)
                        {
                            queryIndexCondition.InclusiveMinValue = conditionBoundry;
                        }
                        else if (comparer.Compare(conditionBoundry, queryIndexCondition.InclusiveMinValue) == -1)
                        {
                            queryIndexCondition.InclusiveMinValue = conditionBoundry;
                        }
                    }
                    else
                    {
                        queryIndexCondition = new IndexCondition
                                                  {
                                                      InclusiveMinValue = conditionBoundry
                                                  };
                    }
                }
                else // if asc
                {
                    if (queryIndexCondition != null)
                    {
                        if (queryIndexCondition.InclusiveMaxValue == null)
                        {
                            queryIndexCondition.InclusiveMaxValue = conditionBoundry;
                        }
                        else if (comparer.Compare(conditionBoundry, queryIndexCondition.InclusiveMaxValue) == 1)
                        {
                            queryIndexCondition.InclusiveMaxValue = conditionBoundry;
                        }
                    }
                    else
                    {
                        queryIndexCondition = new IndexCondition
                                                  {
                                                      InclusiveMaxValue = conditionBoundry
                                                  };
                    }
                }
            }
        }
       

        /// <summary>
        /// Computes the max extract count.
        /// </summary>
        /// <param name="maxItemsPerIndex">Index of the max items per.</param>
        /// <param name="getAdditionalAvailableItemCount">if set to <c>true</c> [get additional available item count].</param>
        /// <param name="filter">The filter.</param>
        /// <param name="maxMergeCount">The max merge count.</param>
        /// <returns>max extract count</returns>
        private static int ComputeMaxExtractCount(int maxItemsPerIndex, bool getAdditionalAvailableItemCount, Filter filter, int maxMergeCount)
        {
            int maxExtractCount;
            if (maxItemsPerIndex > 0)
            {
                maxExtractCount = maxItemsPerIndex;
            }
            else if (getAdditionalAvailableItemCount && filter != null)
            {
                maxExtractCount = Int32.MaxValue;
            }
            else
            {
                maxExtractCount = maxMergeCount;
            }
            return maxExtractCount;
        }

        /// <summary>
        /// Checks the meta data.
        /// </summary>
        /// <param name="internalIndexDictionary">The internal index dictionary.</param>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <returns>true if index is configured to store metadata; otherwise, false</returns>
        private static bool CheckMetaData(Dictionary<KeyValuePair<byte[], string>, CacheIndexInternal> internalIndexDictionary, IndexTypeMapping indexTypeMapping)
        {
            if (indexTypeMapping.MetadataStoredSeperately)
            {
                return true;
            }
            foreach (KeyValuePair<KeyValuePair<byte[] /*IndexId */, string /*IndexName*/>, CacheIndexInternal> kvp in internalIndexDictionary)
            {
                if (indexTypeMapping.IndexCollection[kvp.Value.InDeserializationContext.IndexName].MetadataPresent)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the index header.
        /// </summary>
        /// <param name="internalIndexDictionary">The internal index dictionary.</param>
        /// <param name="targetIndexCacheIndexInternal">The targetindex CacheIndexInternal.</param>
        /// <param name="indexId">The index id.</param>
        /// <param name="query">The query.</param>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="typeId">The type id.</param>
        /// <param name="storeContext">The store context.</param>
        /// <param name="indexInIndexIdList">The indexInIndexIdList.</param>
        /// <returns>IndexHeader</returns>
        private static IndexHeader GetIndexHeader(Dictionary<KeyValuePair<byte[], string>, 
            CacheIndexInternal> internalIndexDictionary,
            CacheIndexInternal targetIndexCacheIndexInternal,
            byte[] indexId,
            BaseMultiIndexIdQuery<TQueryResult> query,
            IndexTypeMapping indexTypeMapping,
            short typeId,
            IndexStoreContext storeContext,
            int indexInIndexIdList)
        {
            byte[] metadata = null;
            MetadataPropertyCollection metadataPropertyCollection = null;

            if(CheckMetaData(internalIndexDictionary, indexTypeMapping))
            {
                if (indexTypeMapping.MetadataStoredSeperately)
                {
                    #region Check if MetadataPropertyCollection is stored seperately

                    IndexServerUtils.GetMetadataStoredSeperately(indexTypeMapping, 
                        typeId,
                        (query.PrimaryIdList != null &&indexInIndexIdList < query.PrimaryIdList.Count) ? 
                            query.PrimaryIdList[indexInIndexIdList]:
                            IndexCacheUtils.GeneratePrimaryId(indexId),
                        indexId,
                        storeContext,
                        out metadata,
                        out metadataPropertyCollection);

                    #endregion
                }
                else
                {
                    #region Check metadata on targetIndex

                    if (indexTypeMapping.IndexCollection[query.TargetIndexName].MetadataPresent)
                    {
                        if (indexTypeMapping.IndexCollection[query.TargetIndexName].IsMetadataPropertyCollection)
                        {
                            metadataPropertyCollection = targetIndexCacheIndexInternal.MetadataPropertyCollection;
                        }
                        else
                        {
                            metadata = targetIndexCacheIndexInternal.Metadata;
                        }
                    }

                    #endregion

                    #region Check metadata on other extracted indexes

                    if (query.TagsFromIndexes != null)
                    {
                        foreach (string indexName in query.TagsFromIndexes)
                        {
                            if (indexTypeMapping.IndexCollection[indexName].MetadataPresent)
                            {
                                if (indexTypeMapping.IndexCollection[indexName].IsMetadataPropertyCollection)
                                {
                                    metadataPropertyCollection =
                                        internalIndexDictionary[new KeyValuePair<byte[], string>(indexId, indexName)].
                                            MetadataPropertyCollection;
                                }
                                else
                                {
                                    metadata =
                                        internalIndexDictionary[new KeyValuePair<byte[], string>(indexId, indexName)].
                                            Metadata;
                                }
                            }
                        }
                    }

                    #endregion
                }
            }

            return new IndexHeader
            {
                Metadata = metadata,
                MetadataPropertyCollection = metadataPropertyCollection,
                VirtualCount = targetIndexCacheIndexInternal.VirtualCount
            };
        }

        /// <summary>
        /// Validates the query.
        /// </summary>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="query">The query.</param>
        /// <param name="messageContext">The message context.</param>
        protected abstract void ValidateQuery(IndexTypeMapping indexTypeMapping,
            BaseMultiIndexIdQuery<TQueryResult> query,
            MessageContext messageContext);

        /// <summary>
        /// Formats the query info.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>String containing formatted QueryInfo</returns>
        protected abstract string FormatQueryInfo(BaseMultiIndexIdQuery<TQueryResult> query);

        /// <summary>
        /// Processes the subsets.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="resultItemList">The result item list.</param>
        ///  <param name="groupByResult">The GroupByResult</param>
        ///  <param name="baseComparer">The BaseComparer</param>       
        protected abstract void ProcessSubsets(BaseMultiIndexIdQuery<TQueryResult> query, 
            ref List<ResultItem> resultItemList, 
            ref GroupByResult groupByResult,
            BaseComparer baseComparer);

        /// <summary>
        /// Sets the item counter.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="outDeserializationContext">The OutDeserializationContext.</param>
        protected abstract void SetItemCounter(short typeId, OutDeserializationContext outDeserializationContext);

        /// <summary>
        /// Sets the index id list counter.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="query">The query.</param>
        protected abstract void SetIndexIdListCounter(short typeId, BaseMultiIndexIdQuery<TQueryResult> query);

    }
}
