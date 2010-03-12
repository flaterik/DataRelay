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
    internal static class ContainsQueryProcessor
    {
        /// <summary>
        /// Processes the specified contains index query.
        /// </summary>
        /// <param name="containsIndexQuery">The contains index query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>ContainsIndexQueryResult</returns>
        internal static ContainsIndexQueryResult Process(ContainsIndexQuery containsIndexQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            ContainsIndexQueryResult containsIndexQueryResult;
            MultiItemResult multiItemResult = null;
            byte[] metadata = null;
            bool indexExists = false;
            int indexSize = -1;
            int virtualCount = -1;

            try
            {
                IndexTypeMapping indexTypeMapping =
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                #region Check TargetIndexName
                
                if (!indexTypeMapping.IndexCollection.Contains(containsIndexQuery.TargetIndexName))
                {
                    throw new Exception("Invalid TargetIndexName - " + containsIndexQuery.TargetIndexName);
                }
                
                #endregion

                Index targetIndexInfo = indexTypeMapping.IndexCollection[containsIndexQuery.TargetIndexName];
                List<CacheIndexInternal> internalCacheIndexList = new List<CacheIndexInternal>();

                #region Get TargetIndex
                
                CacheIndexInternal cacheIndexInternal = IndexServerUtils.GetCacheIndexInternal(storeContext,
                    messageContext.TypeId,
                    messageContext.PrimaryId,
                    containsIndexQuery.IndexId,
                    targetIndexInfo.ExtendedIdSuffix,
                    containsIndexQuery.TargetIndexName,
                    0,
                    null,
                    true,
                    null,
                    false,
                    false,
                    targetIndexInfo.PrimarySortInfo,
                    targetIndexInfo.LocalIdentityTagList,
                    targetIndexInfo.StringHashCodeDictionary,
                    null);
                
                #endregion

                if (cacheIndexInternal != null)
                {
                    internalCacheIndexList.Add(cacheIndexInternal);
                    indexExists = true;
                    indexSize = cacheIndexInternal.OutDeserializationContext.TotalCount;
                    virtualCount = cacheIndexInternal.VirtualCount;

                    // update the performance counter
                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsInIndexPerContainsIndexQuery,
                        messageContext.TypeId,
                        indexSize);

                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsReadPerContainsIndexQuery,
                        messageContext.TypeId,
                        cacheIndexInternal.OutDeserializationContext.ReadItemCount);

                    int searchIndex;
                    IndexDataItem indexDataItem;

                    foreach (IndexItem queryIndexItem in containsIndexQuery.IndexItemList)
                    {
                        #region Search item in index
                        
                        searchIndex = internalCacheIndexList[0].Search(queryIndexItem);
                        
                        #endregion

                        if (searchIndex > -1)
                        {
                            if (multiItemResult == null)
                            {
                                multiItemResult = new MultiItemResult(containsIndexQuery.IndexId);
                            }
                            indexDataItem = new IndexDataItem(InternalItemAdapter.ConvertToIndexItem(internalCacheIndexList[0].GetItem(searchIndex),
                                internalCacheIndexList[0].InDeserializationContext));

                            #region Get extra tags
                            
                            if (containsIndexQuery.TagsFromIndexes != null && containsIndexQuery.TagsFromIndexes.Count != 0)
                            {
                                foreach (string indexName in containsIndexQuery.TagsFromIndexes)
                                {
                                    Index indexInfo = indexTypeMapping.IndexCollection[indexName];

                                    CacheIndexInternal indexInternal =
                                        IndexServerUtils.GetCacheIndexInternal(storeContext,
                                                                               messageContext.TypeId,
                                                                               messageContext.PrimaryId,
                                                                               containsIndexQuery.IndexId,
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

                                    if (indexInternal != null)
                                    {
                                        // update the performance counter
                                        PerformanceCounters.Instance.SetCounterValue(
                                            PerformanceCounterEnum.NumOfItemsInIndexPerContainsIndexQuery,
                                            messageContext.TypeId,
                                            indexInternal.OutDeserializationContext.TotalCount);

                                        PerformanceCounters.Instance.SetCounterValue(
                                            PerformanceCounterEnum.NumOfItemsReadPerContainsIndexQuery,
                                            messageContext.TypeId,
                                            indexInternal.OutDeserializationContext.ReadItemCount);

                                        internalCacheIndexList.Add(indexInternal);

                                        IndexServerUtils.GetTags(indexInternal, queryIndexItem, indexDataItem);
                                    }
                                }
                            }
                            
                            #endregion

                            multiItemResult.Add(indexDataItem);
                        }
                    }

                    #region Get data
                    
                    if (!containsIndexQuery.ExcludeData && multiItemResult != null)
                    {
                        byte[] extendedId;
                        List<RelayMessage> dataStoreMessages = new List<RelayMessage>(multiItemResult.Count);
                        short relatedTypeId;
                        if (containsIndexQuery.FullDataIdInfo != null && containsIndexQuery.FullDataIdInfo.RelatedTypeName != null)
                        {
                            if (!storeContext.TryGetTypeId(containsIndexQuery.FullDataIdInfo.RelatedTypeName, out relatedTypeId))
                            {
                                LoggingUtil.Log.ErrorFormat("Invalid RelatedCacheTypeName - {0}", containsIndexQuery.FullDataIdInfo.RelatedTypeName);
                                throw new Exception("Invalid RelatedTypeId for TypeId - " + containsIndexQuery.FullDataIdInfo.RelatedTypeName);
                            }
                        }
                        else if (!storeContext.TryGetRelatedIndexTypeId(messageContext.TypeId, out relatedTypeId))
                        {
                            LoggingUtil.Log.ErrorFormat("Invalid RelatedTypeId for TypeId - {0}", messageContext.TypeId);
                            throw new Exception("Invalid RelatedTypeId for TypeId - " + messageContext.TypeId);
                        }

                        foreach (IndexDataItem resultItem in multiItemResult)
                        {
                            extendedId = DataTierUtil.GetFullDataId(containsIndexQuery.IndexId,
                                resultItem,
                                    containsIndexQuery.FullDataIdInfo != null && containsIndexQuery.FullDataIdInfo.RelatedTypeName != null ?
                                    containsIndexQuery.FullDataIdInfo.FullDataIdFieldList :
                                    indexTypeMapping.FullDataIdFieldList);
                            dataStoreMessages.Add(new RelayMessage(relatedTypeId, IndexCacheUtils.GeneratePrimaryId(extendedId), extendedId, MessageType.Get));
                        }

                        storeContext.ForwarderComponent.HandleMessages(dataStoreMessages);

                        int i = 0;
                        foreach (IndexDataItem resultItem in multiItemResult)
                        {
                            if (dataStoreMessages[i].Payload != null)
                            {
                                resultItem.Data = dataStoreMessages[i].Payload.ByteArray;
                            }
                            i++;
                        }
                    }
                    
                    #endregion

                    #region Get metadata
                    
                    if (containsIndexQuery.GetMetadata)
                    {
                        metadata = IndexServerUtils.GetQueryMetadata(internalCacheIndexList,
                            indexTypeMapping,
                            messageContext.TypeId,
                            messageContext.PrimaryId,
                            containsIndexQuery.IndexId,
                            storeContext);
                    }
                    
                    #endregion
                }
                containsIndexQueryResult = new ContainsIndexQueryResult(multiItemResult, metadata, indexSize, indexExists, virtualCount, null);
            }
            catch (Exception ex)
            {
                containsIndexQueryResult = new ContainsIndexQueryResult(null, null, -1, false, -1, ex.Message);
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing ContainsIndexQuery : {1}", messageContext.TypeId, ex);
            }
            return containsIndexQueryResult;
        }
    }
}
