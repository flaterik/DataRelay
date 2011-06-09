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
    internal static class DistinctQueryProcessor
    {
        /// <summary>
        /// Processes the specified DistinctQuery.
        /// </summary>
        /// <param name="distinctQuery">The DistinctQuery.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>DistinctQueryResult</returns>
        internal static DistinctQueryResult Process(DistinctQuery distinctQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            DistinctQueryResult distinctQueryResult;
            Dictionary<byte[], int> distinctValueCountMapping = null;
            bool indexExists = false;

            try
            {
                IndexTypeMapping indexTypeMapping = storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];
                Index targetIndexInfo = indexTypeMapping.IndexCollection[distinctQuery.TargetIndexName];

                #region Prepare Result
                
                CacheIndexInternal targetIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                    messageContext.TypeId,
                    messageContext.PrimaryId,
                    distinctQuery.IndexId,
                    targetIndexInfo.ExtendedIdSuffix,
                    distinctQuery.TargetIndexName,
                    (distinctQuery.ItemsToLookUp != null ? (int)distinctQuery.ItemsToLookUp : Int32.MaxValue),
                    null,
                    true,
                    distinctQuery.IndexCondition,
                    false,
                    false,
                    targetIndexInfo.PrimarySortInfo,
                    targetIndexInfo.LocalIdentityTagList,
                    targetIndexInfo.StringHashCodeDictionary,
                    null,
                    targetIndexInfo.IsMetadataPropertyCollection,
                    null,
                    DomainSpecificProcessingType.None,
                    storeContext.DomainSpecificConfig,
                    distinctQuery.FieldName,
                    null,
                    true);
                
                #endregion

                if (targetIndex != null)
                {
                    indexExists = true;

                    // update perf counter
                    PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.NumOfItemsInIndexPerDistinctQuery,
                        messageContext.TypeId,
                        targetIndex.OutDeserializationContext.TotalCount);

                    PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.NumOfItemsReadPerDistinctQuery,
                        messageContext.TypeId,
                        targetIndex.OutDeserializationContext.ReadItemCount);

                    distinctValueCountMapping = targetIndex.OutDeserializationContext.DistinctValueCountMapping;
                }

                distinctQueryResult = new DistinctQueryResult
                                          {
                                              DistinctValueCountMapping = distinctValueCountMapping,
                                              IndexExists = indexExists,
                                          };
            }
            catch (Exception ex)
            {
                distinctQueryResult = new DistinctQueryResult
                                          {
                                              DistinctValueCountMapping = null,
                                              IndexExists = false,
                                              ExceptionInfo = ex.Message
                                          };
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing DistinctQuery : {1}", messageContext.TypeId, ex);
            }
            return distinctQueryResult;
        }
    }
}
