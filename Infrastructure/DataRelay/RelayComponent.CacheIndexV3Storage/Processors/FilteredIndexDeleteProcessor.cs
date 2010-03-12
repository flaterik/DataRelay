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
    internal static class FilteredIndexDeleteProcessor
    {
        /// <summary>
        /// Processes the specified filtered index delete command.
        /// </summary>
        /// <param name="filteredIndexDeleteCommand">The filtered index delete command.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        internal static void Process(
            FilteredIndexDeleteCommand filteredIndexDeleteCommand, 
            MessageContext messageContext, 
            IndexStoreContext storeContext)
        {
            if (filteredIndexDeleteCommand != null)
            {
                IndexTypeMapping indexTypeMapping =
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];
                Index indexInfo = indexTypeMapping.IndexCollection[filteredIndexDeleteCommand.TargetIndexName];

                #region Get CacheIndexInternal for TargetIndexName

                CacheIndexInternal cacheIndexInternal = IndexServerUtils.GetCacheIndexInternal(storeContext,
                    messageContext.TypeId,
                    filteredIndexDeleteCommand.PrimaryId,
                    filteredIndexDeleteCommand.IndexId,
                    indexInfo.ExtendedIdSuffix,
                    filteredIndexDeleteCommand.TargetIndexName,
                    0,
                    filteredIndexDeleteCommand.DeleteFilter,
                    false,
                    null,
                    false,
                    true,
                    indexInfo.PrimarySortInfo,
                    indexInfo.LocalIdentityTagList,
                    indexInfo.StringHashCodeDictionary,
                    null);

                #endregion

                if (cacheIndexInternal != null)
                {
                    #region Increment perf counters' number
                    
                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsInIndexPerFilterDeleteRequest,
                        messageContext.TypeId,
                        cacheIndexInternal.OutDeserializationContext.TotalCount);

                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsReadPerFilterDeleteRequest,
                        messageContext.TypeId,
                        cacheIndexInternal.OutDeserializationContext.ReadItemCount);

                    PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.NumOfItemsFilteredPerFilterDeleteRequest,
                        messageContext.TypeId,
                        (cacheIndexInternal.OutDeserializationContext.TotalCount - cacheIndexInternal.OutDeserializationContext.FilteredInternalItemList.Count));
                    
                    #endregion

                    #region Update VirtualCount
                    
                    cacheIndexInternal.VirtualCount -= (cacheIndexInternal.OutDeserializationContext.TotalCount - cacheIndexInternal.Count);
                    
                    #endregion

                    #region Save CacheIndexInternal to local storage since item which pass delete filter are pruned in it
                   
                    byte[] extendedId = IndexServerUtils.FormExtendedId(filteredIndexDeleteCommand.IndexId,
                        indexTypeMapping.IndexCollection[cacheIndexInternal.InDeserializationContext.IndexName].ExtendedIdSuffix);

                    RelayMessage indexStorageMessage = RelayMessage.GetSaveMessageForObject(messageContext.TypeId,
                        filteredIndexDeleteCommand.PrimaryId,
                        extendedId,
                        DateTime.Now,
                        cacheIndexInternal,
                        storeContext.GetCompressOption(messageContext.TypeId));

                    storeContext.IndexStorageComponent.HandleMessage(indexStorageMessage);

                    #endregion

                    #region Data store deletes

                    if (DataTierUtil.ShouldForwardToDataTier(messageContext.RelayTTL, 
                            messageContext.SourceZone, 
                            storeContext.MyZone, 
                            indexTypeMapping.IndexServerMode) &&
                        cacheIndexInternal.OutDeserializationContext.FilteredInternalItemList != null &&
                        cacheIndexInternal.OutDeserializationContext.FilteredInternalItemList.Count > 0)
                    {
                        List<RelayMessage> dataStorageMessageList = new List<RelayMessage>();

                        short relatedTypeId;
                        if (!storeContext.TryGetRelatedIndexTypeId(messageContext.TypeId, out relatedTypeId))
                        {
                            LoggingUtil.Log.ErrorFormat("Invalid RelatedTypeId for TypeId - {0}", messageContext.TypeId);
                            throw new Exception("Invalid RelatedTypeId for TypeId - " + messageContext.TypeId);
                        }
                        cacheIndexInternal.InternalItemList = cacheIndexInternal.OutDeserializationContext.FilteredInternalItemList;
                        List<byte[]> fullDataIdList = DataTierUtil.GetFullDataIds(cacheIndexInternal.InDeserializationContext.IndexId, 
                            cacheIndexInternal.InternalItemList, 
                            indexTypeMapping.FullDataIdFieldList);

                        foreach (byte[] fullDataId in fullDataIdList)
                        {
                            if (fullDataId != null)
                            {
                                dataStorageMessageList.Add(new RelayMessage(relatedTypeId,
                                                               IndexCacheUtils.GeneratePrimaryId(fullDataId),
                                                               fullDataId, 
                                                               MessageType.Delete));
                            }
                        }

                        if (dataStorageMessageList.Count > 0)
                        {
                            storeContext.ForwarderComponent.HandleMessages(dataStorageMessageList);
                        }
                    }
                    
                    #endregion
                }
            }
        }
    }
}
