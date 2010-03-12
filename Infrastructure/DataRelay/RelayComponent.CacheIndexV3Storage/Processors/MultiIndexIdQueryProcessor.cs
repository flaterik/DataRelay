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
            StringBuilder exceptionInfo = new StringBuilder();
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

                    #endregion

                    #region Prepare ResultList

                    CacheIndexInternal targetIndex;
                    IndexIdParams indexIdParam;
                    int maxExtractCount;
                    Dictionary<KeyValuePair<byte[], string>, CacheIndexInternal> internalIndexDictionary =
                        new Dictionary<KeyValuePair<byte[], string>, CacheIndexInternal>();

                    for (int i = 0; i < query.IndexIdList.Count; i++)
                    {
                        #region Extract index and apply criteria

                        indexIdParam = query.GetParamsForIndexId(query.IndexIdList[i]);
                        maxExtractCount = ComputeMaxExtractCount(indexIdParam.MaxItems,
                            query.GetAdditionalAvailableItemCount,
                            indexIdParam.Filter,
                            query.MaxMergeCount);
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
                            query.IndexCondition,
                            false,
                            false,
                            targetIndexInfo.PrimarySortInfo,
                            targetIndexInfo.LocalIdentityTagList,
                            targetIndexInfo.StringHashCodeDictionary,
                            query.CapCondition);

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

                            MergeAlgo.MergeItemLists(ref resultItemList,
                                CacheIndexInternalAdapter.GetResultItemList(targetIndex, 1, int.MaxValue),
                                query.MaxMergeCount,
                                baseComparer);

                            #endregion
                        }
                    }

                    #endregion

                    #region Subset Processing

                    ProcessSubsets(query, ref resultItemList);

                    #endregion

                    #region Get Extra Tags for IndexIds in the list

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
                                        null);

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
                                internalIndexDictionary.TryGetValue(new KeyValuePair<byte[], string>(indexId, query.TargetIndexName),out targetIndexCacheIndexInternal))
                            {
                                indexIdIndexHeaderMapping.Add(indexId, GetIndexHeader(internalIndexDictionary, 
                                    targetIndexCacheIndexInternal, 
                                    indexId, 
                                    query, 
                                    indexTypeMapping, 
                                    messageContext.TypeId, 
                                    storeContext));
                            }
                        }
                    }
                    else if (query.GetIndexHeaderType == GetIndexHeaderType.ResultItemsIndexIds)
                    {
                        //Get IndexHeader just for IndexIds present in the result
                        indexIdIndexHeaderMapping = new Dictionary<byte[], IndexHeader>(new ByteArrayEqualityComparer());

                        for (int i = 0; i < resultItemList.Count; i++)
                        {
                            ResultItem resultItem = resultItemList[i];
                            if (!indexIdIndexHeaderMapping.ContainsKey(resultItem.IndexId))
                            {
                                CacheIndexInternal targetIndexCacheIndexInternal;
                                internalIndexDictionary.TryGetValue(new KeyValuePair<byte[], string>(resultItem.IndexId, query.TargetIndexName), 
                                    out targetIndexCacheIndexInternal);
                                indexIdIndexHeaderMapping.Add(resultItem.IndexId, GetIndexHeader(internalIndexDictionary, 
                                    targetIndexCacheIndexInternal, 
                                    resultItem.IndexId, 
                                    query, 
                                    indexTypeMapping, 
                                    messageContext.TypeId, 
                                    storeContext));
                            }
                        }
                    }

                    #endregion

                    #region Get data

                    if (!query.ExcludeData)
                    {
                        DataTierUtil.GetData(resultItemList, storeContext, messageContext, indexTypeMapping.FullDataIdFieldList, query.FullDataIdInfo);
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
                                                FormatAddressHistory(messageContext.AddressHistory),
                                                FormatQueryInfo(query));
                }

                LoggingUtil.Log.DebugFormat("QueryInfo: {0}, AddressHistory: {1}", FormatQueryInfo(query), FormatAddressHistory(messageContext.AddressHistory));

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

        /// <summary>
        /// Formats the address history.
        /// </summary>
        /// <param name="addressHistory">The address history.</param>
        /// <returns>String containing formatted address history</returns>
        protected static string FormatAddressHistory(List<IPAddress> addressHistory)
        {
            string retVal = "";
            if (addressHistory != null && addressHistory.Count > 0)
            {
                if (addressHistory.Count == 1)
                {
                    retVal = addressHistory[0].ToString();
                }
                else
                {
                    var stb = new StringBuilder();
                    for (int i = 0; i < addressHistory.Count; i++)
                    {
                        stb.Append(addressHistory[i]).Append(", ");
                    }
                    retVal = stb.ToString();
                }
            }
            return retVal;
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
        /// <param name="indexId">The index id.</param>
        /// <param name="query">The query.</param>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="typeId">The type id.</param>
        /// <param name="primaryId">The primary id.</param>
        /// <param name="extendedId">The extended id.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>IndexHeader</returns>
        private static IndexHeader GetIndexHeader(Dictionary<KeyValuePair<byte[], string>, CacheIndexInternal> internalIndexDictionary,
            CacheIndexInternal targetIndexCacheIndexInternal,
            byte[] indexId,
            BaseMultiIndexIdQuery<TQueryResult> query,
            IndexTypeMapping indexTypeMapping,
            short typeId,
            IndexStoreContext storeContext)
        {
            IndexHeader indexHeader = new IndexHeader();
            KeyValuePair<byte[] /*IndexId */, string /*IndexName*/> kvp;

            #region Metadata

            if (CheckMetaData(internalIndexDictionary, indexTypeMapping))
            {
                if (indexTypeMapping.MetadataStoredSeperately)
                {
                    #region Check if metadata is stored seperately

                    //Send a get message to local index storage and fetch seperately stored metadata
                    RelayMessage getMsg = new RelayMessage(typeId, IndexCacheUtils.GeneratePrimaryId(indexId), indexId, MessageType.Get);
                    storeContext.IndexStorageComponent.HandleMessage(getMsg);

                    if (getMsg.Payload != null)
                    {
                        indexHeader.Metadata = getMsg.Payload.ByteArray;
                    }

                    #endregion
                }
                else
                {
                    #region Check metadata on targetIndex

                    if (indexTypeMapping.IndexCollection[query.TargetIndexName].MetadataPresent)
                    {
                        indexHeader.Metadata = targetIndexCacheIndexInternal.Metadata;
                    }

                    #endregion

                    #region Check metadata on other extracted indexes

                    if (query.TagsFromIndexes != null)
                    {
                        foreach (string indexName in query.TagsFromIndexes)
                        {
                            if (indexTypeMapping.IndexCollection[indexName].MetadataPresent)
                            {
                                indexHeader.Metadata = internalIndexDictionary[new KeyValuePair<byte[], string>(indexId, indexName)].Metadata;
                            }
                        }
                    }

                    #endregion
                }
            }

            #endregion

            #region VirtualCount

            indexHeader.VirtualCount = targetIndexCacheIndexInternal.VirtualCount;

            #endregion

            return indexHeader;
        }

        /// <summary>
        /// Formats the query info.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>String containing formatted QueryInfo</returns>
        protected virtual string FormatQueryInfo(BaseMultiIndexIdQuery<TQueryResult> query)
        {
            StringBuilder stb = new StringBuilder();
            stb.Append("NumOfIndex: ").Append(query.IndexIdList == null ? 0 : query.IndexIdList.Count).Append(", ");
            stb.Append("MaxItemsPerIndex: ").Append(query.MaxItems).Append(", ");
            if (query.Filter == null)
            {
                stb.Append("Total Filter Count: ").Append(0).Append(", ");
            }
            else
            {
                stb.Append("Total Filter Count: ").Append(query.Filter.FilterCount).Append(", ");
                stb.Append("Filter Info - ").Append(Environment.NewLine).Append(query.Filter.FilterInfo).Append(Environment.NewLine);
            }
            stb.Append("ExcludeData: ").Append(query.ExcludeData).Append(", ");
            return stb.ToString();
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
        /// Processes the subsets.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="resultItemList">The result item list.</param>
        protected abstract void ProcessSubsets(BaseMultiIndexIdQuery<TQueryResult> query, ref List<ResultItem> resultItemList);

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