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
    internal static class FirstLastQueryProcessor
    {
        /// <summary>
        /// Processes the specified first last query.
        /// </summary>
        /// <param name="firstLastQuery">The first last query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>FirstLastQueryResult</returns>
        internal static FirstLastQueryResult Process(FirstLastQuery firstLastQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            FirstLastQueryResult firstLastQueryResult;
            List<ResultItem> firstPageResultItemList = null;
            List<ResultItem> lastPageResultItemList = null;
            bool indexExists = false;
            int indexSize = -1;
            int virtualCount = -1;
            byte[] metadata = null;

            try
            {
                IndexTypeMapping indexTypeMapping = 
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                #region Validate Query
                
                ValidateQuery(indexTypeMapping, firstLastQuery);
                
                #endregion

                int maxItemsPerIndex = ((firstLastQuery.FirstPageSize > 0) && (firstLastQuery.LastPageSize < 1))
                                           ? firstLastQuery.FirstPageSize
                                           : int.MaxValue;

                Index targetIndexInfo = indexTypeMapping.IndexCollection[firstLastQuery.TargetIndexName];

                #region Prepare Result

                #region Extract index and apply criteria
                
                CacheIndexInternal targetIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                    messageContext.TypeId,
                    messageContext.PrimaryId,
                    firstLastQuery.IndexId,
                    targetIndexInfo.ExtendedIdSuffix,
                    firstLastQuery.TargetIndexName,
                    maxItemsPerIndex,
                    firstLastQuery.Filter,
                    true,
                    null,
                    false,
                    false,
                    targetIndexInfo.PrimarySortInfo,
                    targetIndexInfo.LocalIdentityTagList,
                    targetIndexInfo.StringHashCodeDictionary,
                    null);

                #endregion

                if (targetIndex != null)
                {
                    indexSize = targetIndex.OutDeserializationContext.TotalCount;
                    virtualCount = targetIndex.VirtualCount;
                    indexExists = true;

                    // update perf counters
                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsInIndexPerFirstLastQuery,
                        messageContext.TypeId,
                        indexSize);

                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsReadPerFirstLastQuery,
                        messageContext.TypeId,
                        targetIndex.OutDeserializationContext.ReadItemCount);

                    #region Populate resultLists
                    
                    if (firstLastQuery.FirstPageSize + firstLastQuery.LastPageSize <= targetIndex.Count)
                    {
                        firstPageResultItemList = CacheIndexInternalAdapter.GetResultItemList(targetIndex, 1, firstLastQuery.FirstPageSize);
                        lastPageResultItemList = CacheIndexInternalAdapter.GetResultItemList(targetIndex, 
                            targetIndex.Count - firstLastQuery.LastPageSize + 1,
                            firstLastQuery.LastPageSize);
                    }
                    else
                    {
                        //Populate everything in firstPageResultItemList
                        firstPageResultItemList = CacheIndexInternalAdapter.GetResultItemList(targetIndex, 1, targetIndex.Count);
                    }
                    
                    #endregion

                    #region Get data
                    
                    if (!firstLastQuery.ExcludeData)
                    {
                        //First Page
                        if (firstPageResultItemList != null)
                        {
                            DataTierUtil.GetData(firstPageResultItemList, 
                                storeContext, 
                                messageContext, 
                                indexTypeMapping.FullDataIdFieldList, 
                                firstLastQuery.FullDataIdInfo);
                        }

                        //Last Page
                        if (lastPageResultItemList != null)
                        {
                            DataTierUtil.GetData(lastPageResultItemList, 
                                storeContext, 
                                messageContext, 
                                indexTypeMapping.FullDataIdFieldList, 
                                firstLastQuery.FullDataIdInfo);
                        }
                    }
                    
                    #endregion

                    #region Get metadata
                    
                    if (firstLastQuery.GetMetadata)
                    {
                        metadata = IndexServerUtils.GetQueryMetadata(new List<CacheIndexInternal>(1) { targetIndex },
                            indexTypeMapping, 
                            messageContext.TypeId,
                            messageContext.PrimaryId,
                            firstLastQuery.IndexId,
                            storeContext);
                    }
                    
                    #endregion
                }

                #endregion

                firstLastQueryResult = new FirstLastQueryResult(indexExists, indexSize, metadata, firstPageResultItemList, lastPageResultItemList, virtualCount, null);
            }
            catch (Exception ex)
            {
                firstLastQueryResult = new FirstLastQueryResult(false, -1, null, null, null, -1, ex.Message);
                LoggingUtil.Log.ErrorFormat("TypeID {0} -- Error processing FirstLastQuery : {1}", messageContext.TypeId, ex);
            }
            return firstLastQueryResult;
        }

        /// <summary>
        /// Validates the query.
        /// </summary>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="firstLastQuery">The first last query.</param>
        private static void ValidateQuery(IndexTypeMapping indexTypeMapping, FirstLastQuery firstLastQuery)
        {
            if (!indexTypeMapping.IndexCollection.Contains(firstLastQuery.TargetIndexName))
            {
                throw new Exception("Invalid TargetIndexName - " + firstLastQuery.TargetIndexName);
            }
            if (firstLastQuery.IndexId == null || firstLastQuery.IndexId.Length == 0)
            {
                throw new Exception("No IndexId present on the FirstLastQuery");
            }
            if ((firstLastQuery.FirstPageSize < 1) && (firstLastQuery.LastPageSize < 1))
            {
                throw new Exception("Atleast one of FirstLastQuery.FirstPageSize and FirstLastQuery.LastPageSize should be greater than 1");
            }
        }
    }
}
