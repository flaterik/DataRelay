using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class MultiIndexContainsQueryProcessor
    {
        internal static MultiIndexContainsQueryResult Process(MultiIndexContainsQuery multiIndexContainsQuery,
            MessageContext messageContext,
            IndexStoreContext storeContext)
        {
            MultiIndexContainsQueryResult multiIndexContainsQueryResult;
            List<Pair<MultiIndexContainsQueryResultItem, IndexHeader>> multiIndexContainsQueryResultItemIndexHeaderList =
                new List<Pair<MultiIndexContainsQueryResultItem, IndexHeader>>(multiIndexContainsQuery.IndexIdList.Count);
            int indexSize = -1;
            int indexCap = 0;
            StringBuilder exceptionInfo = new StringBuilder();

            try
            {
                if (multiIndexContainsQuery.IndexIdList.Count > 0)
                {
                    IndexTypeMapping indexTypeMapping = storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                    short relatedTypeId;
                    ValidateQuery(multiIndexContainsQuery, indexTypeMapping, storeContext, messageContext.TypeId, out relatedTypeId);

                    Index targetIndexInfo = indexTypeMapping.IndexCollection[multiIndexContainsQuery.TargetIndexName];
                    indexCap = targetIndexInfo.MaxIndexSize;
                    MultiIndexContainsQueryParams multiIndexContainsQueryParam;
                    int searchIndex;
                    IndexDataItem indexDataItem;
                    MultiIndexContainsQueryResultItem multiIndexContainsQueryResultItem;
                    IndexHeader indexHeader;
                    List<RelayMessage> dataStoreMessages = new List<RelayMessage>();
                    byte[] extendedId;
                    byte[] metadata;
                    MetadataPropertyCollection metadataPropertyCollection;

                    foreach (byte[] indexId in multiIndexContainsQuery.IndexIdList)
                    {
                        #region Get TargetIndex

                        // Note: This should be changed later and just extracted once if it is also requested in GetIndexHeader
                        metadata = null;
                        metadataPropertyCollection = null;
                        if (indexTypeMapping.MetadataStoredSeperately)
                        {
                            IndexServerUtils.GetMetadataStoredSeperately(indexTypeMapping,
                                messageContext.TypeId,
                                IndexCacheUtils.GeneratePrimaryId(indexId),
                                indexId,
                                storeContext,
                                out metadata,
                                out metadataPropertyCollection);
                        }

                        multiIndexContainsQueryParam =
                            multiIndexContainsQuery.GetMultiIndexContainsQueryParamForIndexId(indexId);
                        CacheIndexInternal cacheIndexInternal = IndexServerUtils.GetCacheIndexInternal(storeContext,
                                                                                                       messageContext.TypeId,
                                                                                                       IndexCacheUtils.GeneratePrimaryId(indexId),
                                                                                                       indexId,
                                                                                                       targetIndexInfo.ExtendedIdSuffix,
                                                                                                       multiIndexContainsQuery.TargetIndexName,
                                                                                                       multiIndexContainsQueryParam.Count,
                                                                                                       multiIndexContainsQueryParam.Filter,
                                                                                                       true,
                                                                                                       multiIndexContainsQueryParam.IndexCondition,
                                                                                                       false,
                                                                                                       false,
                                                                                                       targetIndexInfo.PrimarySortInfo,
                                                                                                       targetIndexInfo.LocalIdentityTagList,
                                                                                                       targetIndexInfo.StringHashCodeDictionary,
                                                                                                       null,
                                                                                                       targetIndexInfo.IsMetadataPropertyCollection,
                                                                                                       metadataPropertyCollection,
                                                                                                       multiIndexContainsQuery.DomainSpecificProcessingType,
                                                                                                       storeContext.DomainSpecificConfig,
                                                                                                       null,
                                                                                                       null,
                                                                                                       false);

                        #endregion

                        if (cacheIndexInternal != null)
                        {
                            indexSize = cacheIndexInternal.OutDeserializationContext.TotalCount;
                            multiIndexContainsQueryResultItem = null;
                            indexHeader = null;

                            // update the performance counter
                            PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.NumOfItemsInIndexPerMultiIndexContainsQuery,
                                messageContext.TypeId,
                                cacheIndexInternal.OutDeserializationContext.TotalCount);

                            PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.NumOfItemsReadPerMultiIndexContainsQuery,
                                messageContext.TypeId,
                                cacheIndexInternal.OutDeserializationContext.ReadItemCount);

                            #region MultiIndexContainsQueryResultItem

                            foreach (IndexItem queryIndexItem in multiIndexContainsQuery.IndexItemList)
                            {
                                #region Search item in index

                                searchIndex = cacheIndexInternal.Search(queryIndexItem);

                                #endregion

                                if (searchIndex > -1)
                                {
                                    if (multiIndexContainsQueryResultItem == null)
                                    {
                                        multiIndexContainsQueryResultItem = new MultiIndexContainsQueryResultItem
                                                                {
                                                                    IndexId = indexId,
                                                                    IndexCap = indexCap,
                                                                    IndexExists = true,
                                                                    IndexSize = indexSize
                                                                };
                                    }
                                    indexDataItem =
                                        new IndexDataItem(
                                            InternalItemAdapter.ConvertToIndexItem(cacheIndexInternal.GetItem(searchIndex),
                                            cacheIndexInternal.InDeserializationContext));

                                    multiIndexContainsQueryResultItem.Add(indexDataItem);

                                    // Data
                                    if (!multiIndexContainsQuery.ExcludeData)
                                    {
                                        extendedId = DataTierUtil.GetFullDataId(indexId,
                                            indexDataItem,
                                            multiIndexContainsQuery.FullDataIdInfo != null &&
                                            multiIndexContainsQuery.FullDataIdInfo.RelatedTypeName != null ?
                                            multiIndexContainsQuery.FullDataIdInfo.FullDataIdFieldList :
                                            indexTypeMapping.FullDataIdFieldList);

                                        dataStoreMessages.Add(new RelayMessage(relatedTypeId, IndexCacheUtils.GeneratePrimaryId(extendedId), extendedId, MessageType.Get));

                                    }
                                }
                            }

                            #endregion

                            #region Add indexHeader to Result
                            if (multiIndexContainsQueryResultItem != null && multiIndexContainsQueryResultItem.Count > 0)
                            {
                                if (multiIndexContainsQuery.GetIndexHeader)
                                {

                                    indexHeader = IndexServerUtils.GetIndexHeader(cacheIndexInternal, indexTypeMapping,
                                                                              messageContext.TypeId,
                                                                              IndexCacheUtils.GeneratePrimaryId(indexId),
                                                                              storeContext);
                                }


                                multiIndexContainsQueryResultItemIndexHeaderList.Add(new Pair<MultiIndexContainsQueryResultItem, IndexHeader>
                                {
                                    First = multiIndexContainsQueryResultItem,
                                    Second = indexHeader
                                });
                            }

                            #endregion
                        }
                    }

                    #region Get data

                    if (!multiIndexContainsQuery.ExcludeData)
                    {
                        storeContext.ForwarderComponent.HandleMessages(dataStoreMessages);

                        int i = 0;
                        foreach (
                            Pair<MultiIndexContainsQueryResultItem, IndexHeader> pair in
                                multiIndexContainsQueryResultItemIndexHeaderList)
                        {
                            foreach (IndexDataItem resultItem in pair.First)
                            {
                                if (dataStoreMessages[i].Payload != null)
                                {
                                    resultItem.Data = dataStoreMessages[i].Payload.ByteArray;
                                }
                                i++;
                            }
                        }
                    }

                    #endregion

                }
                multiIndexContainsQueryResult = new MultiIndexContainsQueryResult(multiIndexContainsQueryResultItemIndexHeaderList, exceptionInfo.ToString());

                // update performance counter
                PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.IndexLookupAvgPerMultiIndexContainsQuery,
                    messageContext.TypeId,
                    multiIndexContainsQuery.IndexIdList.Count);
            }
            catch (Exception ex)
            {
                multiIndexContainsQueryResult = new MultiIndexContainsQueryResult(null, ex.Message);
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing MultiIndexContainsQuery : {1}", messageContext.TypeId, ex);
            }
            return multiIndexContainsQueryResult;
        }

        private static void ValidateQuery(MultiIndexContainsQuery multiIndexContainsQuery, 
            IndexTypeMapping indexTypeMapping, 
            IndexStoreContext storeContext, 
            short typeId,
            out short relatedTypeId)
        {
            relatedTypeId = -1;
            if (string.IsNullOrEmpty(multiIndexContainsQuery.TargetIndexName))
            {
                multiIndexContainsQuery.TargetIndexName = IndexServerUtils.CheckQueryTargetIndexName(indexTypeMapping);
            }

            if (!indexTypeMapping.IndexCollection.Contains(multiIndexContainsQuery.TargetIndexName))
            {
                throw new Exception("Invalid TargetIndexName - " + multiIndexContainsQuery.TargetIndexName);
            }

            if (!multiIndexContainsQuery.ExcludeData)
            {
                if (multiIndexContainsQuery.FullDataIdInfo != null && multiIndexContainsQuery.FullDataIdInfo.RelatedTypeName != null)
                {
                    if (!storeContext.TryGetTypeId(multiIndexContainsQuery.FullDataIdInfo.RelatedTypeName, out relatedTypeId))
                    {
                        LoggingUtil.Log.ErrorFormat("Invalid RelatedCacheTypeName - {0}", multiIndexContainsQuery.FullDataIdInfo.RelatedTypeName);
                        throw new Exception("Invalid RelatedTypeId for TypeId - " + multiIndexContainsQuery.FullDataIdInfo.RelatedTypeName);
                    }
                }
                else if (!storeContext.TryGetRelatedIndexTypeId(typeId, out relatedTypeId))
                {
                    LoggingUtil.Log.ErrorFormat("Invalid RelatedTypeId for TypeId - {0}", typeId);
                    throw new Exception("Invalid RelatedTypeId for TypeId - " + typeId);
                }
            }
        }
    }
}
