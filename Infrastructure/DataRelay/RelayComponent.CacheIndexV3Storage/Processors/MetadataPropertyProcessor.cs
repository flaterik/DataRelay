using System;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using MySpace.Common.IO;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class MetadataPropertyProcessor
    {
        /// <summary>
        /// Processes the specified MetadataPropertyCommand.
        /// </summary>
        /// <param name="metadataPropertyCommand">The MetadataPropertyCommand.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        internal static void Process(MetadataPropertyCommand metadataPropertyCommand, MessageContext messageContext, IndexStoreContext storeContext)
        {
            if (metadataPropertyCommand != null)
            {
                MetadataPropertyCollection metadataPropertyCollection = null;
                byte[] metadata;
                IndexTypeMapping indexTypeMapping = storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];
                CacheIndexInternal cacheIndexInternal = null;

                #region Fetch MetadataPropertyCollection

                if (indexTypeMapping.MetadataStoredSeperately)
                {
                    IndexServerUtils.GetMetadataStoredSeperately(indexTypeMapping, 
                        messageContext.TypeId, 
                        metadataPropertyCommand.PrimaryId, 
                        metadataPropertyCommand.IndexId, 
                        storeContext,
                        out metadata,
                        out metadataPropertyCollection);
                }
                else
                {
                    Index indexInfo = indexTypeMapping.IndexCollection[metadataPropertyCommand.TargetIndexName];

                    // Get CacheIndexInternal
                    cacheIndexInternal = IndexServerUtils.GetCacheIndexInternal(storeContext,
                        messageContext.TypeId,
                        metadataPropertyCommand.PrimaryId,
                        metadataPropertyCommand.IndexId,
                        indexInfo.ExtendedIdSuffix,
                        metadataPropertyCommand.TargetIndexName,
                        0,
                        null,
                        false,
                        null,
                        true,
                        false,
                        null,
                        null,
                        null,
                        null,
                        true,
                        null,
                        DomainSpecificProcessingType.None,
                        null,
                        null,
                        null,
                        true);

                    if (cacheIndexInternal != null && cacheIndexInternal.MetadataPropertyCollection != null)
                    {
                        metadataPropertyCollection = cacheIndexInternal.MetadataPropertyCollection;
                    }
                }

                #endregion

                #region Process MetadataPropertyCollection add/deletes

                if (metadataPropertyCollection == null)
                {
                    metadataPropertyCollection = new MetadataPropertyCollection();
                }
                metadataPropertyCollection.Process(metadataPropertyCommand.MetadataPropertyCollectionUpdate);

                #endregion

                #region Restore MetadataPropertyCollection back to storage
                
                bool isCompress = storeContext.GetCompressOption(messageContext.TypeId);
                byte[] extId = null;
                byte[] byteArray = null;

                if (indexTypeMapping.MetadataStoredSeperately)
                {
                    extId = metadataPropertyCommand.ExtendedId;
                    byteArray = Serializer.Serialize(metadataPropertyCollection, isCompress);
                }
                else
                {
                    if (cacheIndexInternal == null)
                    {
                        cacheIndexInternal = new CacheIndexInternal
                                                 {
                                                     InDeserializationContext = new InDeserializationContext(0,
                                                         metadataPropertyCommand.TargetIndexName,
                                                         metadataPropertyCommand.IndexId,
                                                         messageContext.TypeId,
                                                         null,
                                                         false,
                                                         null,
                                                         true,
                                                         false,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         true,
                                                         null,
                                                         DomainSpecificProcessingType.None,
                                                         null,
                                                         null,
                                                         null)
                                                 };
                    }

                    //Restore CacheIndexInternal
                    cacheIndexInternal.MetadataPropertyCollection = metadataPropertyCollection;

                    extId = IndexServerUtils.FormExtendedId(metadataPropertyCommand.IndexId,
                                                            indexTypeMapping.IndexCollection[
                                                                cacheIndexInternal.InDeserializationContext.IndexName].
                                                                ExtendedIdSuffix);

                    byteArray = Serializer.Serialize(cacheIndexInternal, isCompress);
                }

                PayloadStorage bdbEntryHeader = new PayloadStorage
                {
                    Compressed = isCompress,
                    TTL = -1,
                    LastUpdatedTicks = DateTime.Now.Ticks,
                    ExpirationTicks = -1,
                    Deactivated = false
                };

                BinaryStorageAdapter.Save(
                    storeContext.MemoryPool,
                    storeContext.IndexStorageComponent,
                    messageContext.TypeId,
                    metadataPropertyCommand.PrimaryId,
                    extId,
                    bdbEntryHeader,
                    byteArray);

                #endregion
            }
        }
    }
}
