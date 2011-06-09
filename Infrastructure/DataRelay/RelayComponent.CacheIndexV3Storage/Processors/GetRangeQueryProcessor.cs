using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class GetRangeQueryProcessor
    {
        /// <summary>
        /// Processes the specified get range query.
        /// </summary>
        /// <param name="getRangeQuery">The get range query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>GetRangeQueryResult</returns>
        internal static GetRangeQueryResult Process(GetRangeQuery getRangeQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            GetRangeQueryResult getRangeQueryResult;
            List<ResultItem> resultItemList = null;
            bool indexExists = false;
            int indexSize = -1;
            int virtualCount = -1;
            byte[] metadata = null;
            MetadataPropertyCollection metadataPropertyCollection = null;

            try
            {
                IndexTypeMapping indexTypeMapping =
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                #region Validate Query
                
                ValidateQuery(indexTypeMapping, getRangeQuery);
                
                #endregion

                int maxItemsPerIndex = getRangeQuery.Offset - 1 + getRangeQuery.ItemNum;

                Index targetIndexInfo = indexTypeMapping.IndexCollection[getRangeQuery.TargetIndexName];
                int indexCap = targetIndexInfo.MaxIndexSize;

                #region Prepare Result
                
                #region Extract index and apply criteria

                if (indexTypeMapping.MetadataStoredSeperately)
                {
                    IndexServerUtils.GetMetadataStoredSeperately(indexTypeMapping,
                        messageContext.TypeId,
                        messageContext.PrimaryId,
                        getRangeQuery.IndexId,
                        storeContext,
                        out metadata,
                        out metadataPropertyCollection);
                }

                CacheIndexInternal targetIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                    messageContext.TypeId,
                    messageContext.PrimaryId,
                    getRangeQuery.IndexId,
                    targetIndexInfo.ExtendedIdSuffix,
                    getRangeQuery.TargetIndexName,
                    maxItemsPerIndex,
                    getRangeQuery.Filter,
                    true,
                    getRangeQuery.IndexCondition,
                    false,
                    false,
                    targetIndexInfo.PrimarySortInfo,
                    targetIndexInfo.LocalIdentityTagList,
                    targetIndexInfo.StringHashCodeDictionary,
                    null,
                    targetIndexInfo.IsMetadataPropertyCollection,
                    metadataPropertyCollection,
                    getRangeQuery.DomainSpecificProcessingType,
                    storeContext.DomainSpecificConfig,
                    null,
                    null,
                    false);
                
                #endregion

                if (targetIndex != null)
                {
                    indexSize = targetIndex.OutDeserializationContext.TotalCount;
                    virtualCount = targetIndex.VirtualCount;
                    indexExists = true;

                    #region Dynamic tag sort

                    if (getRangeQuery.TagSort != null)
                    {
                        targetIndex.Sort(getRangeQuery.TagSort);
                    }

                    #endregion

                    // update perf counter
                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsInIndexPerGetRangeQuery,
                        messageContext.TypeId,
                        indexSize);

                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsReadPerGetRangeQuery,
                        messageContext.TypeId,
                        targetIndex.OutDeserializationContext.ReadItemCount);

                    #region Populate resultLists
                    
                    resultItemList = CacheIndexInternalAdapter.GetResultItemList(targetIndex, getRangeQuery.Offset, getRangeQuery.ItemNum);
                    
                    #endregion

                    #region Get data
                    
                    if (!getRangeQuery.ExcludeData)
                    {
                        DataTierUtil.GetData(resultItemList,
                            null,
                            storeContext, 
                            messageContext, 
                            indexTypeMapping.FullDataIdFieldList, 
                            getRangeQuery.FullDataIdInfo);
                    }
                    
                    #endregion

                    #region Get metadata
                    
                    if (getRangeQuery.GetMetadata && !indexTypeMapping.MetadataStoredSeperately)
                    {
                        IndexServerUtils.GetMetadataStoredWithIndex(indexTypeMapping,
                            new List<CacheIndexInternal>(1) { targetIndex },
                            out metadata,
                            out metadataPropertyCollection);
                    }
                    
                    #endregion

                #endregion
                }

                getRangeQueryResult = new GetRangeQueryResult(indexExists, indexSize, metadata, metadataPropertyCollection, resultItemList, virtualCount, indexCap, null);
            }
            catch (Exception ex)
            {
                getRangeQueryResult = new GetRangeQueryResult(false, -1, null, null, null, -1, 0, ex.Message);
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing GetRangeQuery : {1}", messageContext.TypeId, ex);
            }
            return getRangeQueryResult;
        }

        /// <summary>
        /// Validates the query.
        /// </summary>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="getRangeQuery">The get range query.</param>
        private static void ValidateQuery(IndexTypeMapping indexTypeMapping, GetRangeQuery getRangeQuery)
        {
            if (string.IsNullOrEmpty(getRangeQuery.TargetIndexName))
            {
                getRangeQuery.TargetIndexName = IndexServerUtils.CheckQueryTargetIndexName(indexTypeMapping);
            }

            if (!indexTypeMapping.IndexCollection.Contains(getRangeQuery.TargetIndexName))
            {
                throw new Exception("Invalid TargetIndexName - " + getRangeQuery.TargetIndexName);
            }
            if (getRangeQuery.IndexId == null || getRangeQuery.IndexId.Length == 0)
            {
                throw new Exception("No IndexId present on the GetRangeQuery");
            }
            if ((getRangeQuery.Offset < 1) || (getRangeQuery.ItemNum < 1))
            {
                throw new Exception("Both GetRangeQuery.Offset and GetRangeQuery.ItemNum should be greater than 1");
            }
        }
    }
}
