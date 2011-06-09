using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class DeleteProcessor
    {
        /// <summary>
        /// Processes the specified message context.
        /// </summary>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        internal static void Process(MessageContext messageContext, IndexStoreContext storeContext)
        {
            lock (LockingUtil.Instance.GetLock(messageContext.PrimaryId))
            {
                IndexTypeMapping indexTypeMapping = 
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];
                List<RelayMessage> indexStorageMessageList = new List<RelayMessage>(indexTypeMapping.IndexCollection.Count);
                List<RelayMessage> dataStorageMessageList = new List<RelayMessage>();
                CacheIndexInternal internalIndex;
                List<byte[]> fullDataIdList;

                if (indexTypeMapping.MetadataStoredSeperately)
                {
                    BinaryStorageAdapter.Delete(
                        storeContext.IndexStorageComponent,
                        messageContext.TypeId,
                        messageContext.PrimaryId,
                        messageContext.ExtendedId);
                }

                foreach (Index index in indexTypeMapping.IndexCollection)
                {
                    internalIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                        messageContext.TypeId,
                        messageContext.PrimaryId,
                        messageContext.ExtendedId,
                        index.ExtendedIdSuffix,
                        index.IndexName,
                        0,
                        null,
                        true,
                        null,
                        false,
                        false,
                        index.PrimarySortInfo,
                        index.LocalIdentityTagList,
                        index.StringHashCodeDictionary,
                        null,
                        indexTypeMapping.IndexCollection[index.IndexName].IsMetadataPropertyCollection,
                        null,
                        DomainSpecificProcessingType.None,
                        null,
                        null,
                        null,
                        true);

                    if (internalIndex != null)
                    {
                        #region Deletes messages for data store

                        if (DataTierUtil.ShouldForwardToDataTier(messageContext.RelayTTL, 
                            messageContext.SourceZone, 
                            storeContext.MyZone, 
                            indexTypeMapping.IndexServerMode))
                        {
                            fullDataIdList = DataTierUtil.GetFullDataIds(messageContext.ExtendedId,
                                                                             internalIndex.InternalItemList,
                                                                             indexTypeMapping.FullDataIdFieldList);
                            short relatedTypeId;
                            foreach (byte[] fullDataId in fullDataIdList)
                            {
                                if (fullDataId != null)
                                {
                                    if (storeContext.TryGetRelatedIndexTypeId(messageContext.TypeId, out relatedTypeId))
                                    {
                                        dataStorageMessageList.Add(new RelayMessage(relatedTypeId,
                                                                                    IndexCacheUtils.GeneratePrimaryId(fullDataId), 
                                                                                    fullDataId,
                                                                                    MessageType.Delete));
                                    }
                                    else
                                    {
                                        LoggingUtil.Log.ErrorFormat("Invalid RelatedTypeId for TypeId - {0}", messageContext.TypeId);
                                        throw new Exception("Invalid RelatedTypeId for TypeId - " + messageContext.TypeId);
                                    }
                                }
                            }
                        }
                        
                        #endregion

                        #region Delete messages for index store

                        BinaryStorageAdapter.Delete(
                            storeContext.IndexStorageComponent,
                            messageContext.TypeId,
                            messageContext.PrimaryId,
                            IndexServerUtils.FormExtendedId(messageContext.ExtendedId, index.ExtendedIdSuffix));
                        
                        #endregion
                    }
                }

                if (dataStorageMessageList.Count > 0)
                {
                    storeContext.ForwarderComponent.HandleMessages(dataStorageMessageList);
                }
            }
        }
    }
}