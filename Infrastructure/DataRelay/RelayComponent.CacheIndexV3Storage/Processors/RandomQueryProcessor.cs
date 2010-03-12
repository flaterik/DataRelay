using System;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class RandomQueryProcessor
    {
        /// <summary>
        /// Processes the specified random query.
        /// </summary>
        /// <param name="randomQuery">The random query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>RandomQueryResult</returns>
        internal static RandomQueryResult Process(RandomQuery randomQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            RandomQueryResult randomQueryResult;
            List<ResultItem> resultItemList = null;
            bool indexExists = false;
            int indexSize = -1;
            int virtualCount = -1;
            byte[] metadata = null;

            try
            {
                IndexTypeMapping indexTypeMapping =
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                #region Validate Query

                ValidateQuery(indexTypeMapping, randomQuery);

                #endregion

                Index targetIndexInfo = indexTypeMapping.IndexCollection[randomQuery.TargetIndexName];

                #region Prepare Result

                #region Extract index and apply criteria

                CacheIndexInternal targetIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                    messageContext.TypeId,
                    messageContext.PrimaryId,
                    randomQuery.IndexId,
                    targetIndexInfo.ExtendedIdSuffix,
                    randomQuery.TargetIndexName,
                    0,
                    randomQuery.Filter,
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

                    // update performance counters
                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsInIndexPerRandomQuery,
                        messageContext.TypeId,
                        indexSize);

                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsReadPerRandomQuery,
                        messageContext.TypeId,
                        targetIndex.OutDeserializationContext.ReadItemCount);

                    #region Populate resultLists

                    int indexItemCount = targetIndex.Count;
                    int resultListCount = Math.Min(randomQuery.Count, indexItemCount);
                    IEnumerable<int> itemPositionList = Algorithm.RandomSubset(new Random(), 0, indexItemCount - 1, resultListCount);
                    resultItemList = CacheIndexInternalAdapter.GetResultItemList(targetIndex, itemPositionList);

                    #endregion

                    #region Get data

                    if (!randomQuery.ExcludeData)
                    {
                        DataTierUtil.GetData(resultItemList, storeContext, messageContext, indexTypeMapping.FullDataIdFieldList, randomQuery.FullDataIdInfo);
                    }

                    #endregion

                    #region Get metadata

                    if (randomQuery.GetMetadata)
                    {
                        metadata = IndexServerUtils.GetQueryMetadata(new List<CacheIndexInternal>(1) { targetIndex },
                            indexTypeMapping,
                            messageContext.TypeId,
                            messageContext.PrimaryId,
                            randomQuery.IndexId,
                            storeContext);
                    }

                    #endregion

                #endregion

                }
                randomQueryResult = new RandomQueryResult(indexExists, indexSize, metadata, resultItemList, virtualCount, null);
            }
            catch (Exception ex)
            {
                randomQueryResult = new RandomQueryResult(false, -1, null, null, -1, ex.Message);
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing RandomQuery : {1}", messageContext.TypeId, ex);
            }
            return randomQueryResult;
        }

        /// <summary>
        /// Validates the query.
        /// </summary>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="randomQuery">The random query.</param>
        private static void ValidateQuery(IndexTypeMapping indexTypeMapping, RandomQuery randomQuery)
        {
            if (!indexTypeMapping.IndexCollection.Contains(randomQuery.TargetIndexName))
            {
                throw new Exception("Invalid TargetIndexName - " + randomQuery.TargetIndexName);
            }
            if (randomQuery.IndexId == null || randomQuery.IndexId.Length == 0)
            {
                throw new Exception("No IndexId present on the RandomQuery");
            }
            if (randomQuery.Count < 1)
            {
                throw new Exception("Both RandomQuery.Count should be greater than 1");
            }
        }
    }
}
