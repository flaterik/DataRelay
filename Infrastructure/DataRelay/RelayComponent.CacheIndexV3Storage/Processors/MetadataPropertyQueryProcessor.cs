using System;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class MetadataPropertyQueryProcessor
    {
        /// <summary>
        /// Processes the specified MetadataPropertyQuery.
        /// </summary>
        /// <param name="metadataPropertyQuery">The MetadataPropertyQuery.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns></returns>
        internal static MetadataPropertyQueryResult Process(MetadataPropertyQuery metadataPropertyQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            MetadataPropertyQueryResult metadataPropertyQueryResult;
            MetadataPropertyCollection metadataPropertyCollection = null;
            try
            {
                IndexTypeMapping indexTypeMapping =
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                if (indexTypeMapping.IsMetadataPropertyCollection || indexTypeMapping.IndexCollection[metadataPropertyQuery.TargetIndexName].IsMetadataPropertyCollection)
                {
                    #region Fetch MetadataPropertyCollection

                    if (indexTypeMapping.MetadataStoredSeperately)
                    {
                        byte[] metadata;
                        IndexServerUtils.GetMetadataStoredSeperately(indexTypeMapping,
                                                                     messageContext.TypeId,
                                                                     metadataPropertyQuery.PrimaryId,
                                                                     metadataPropertyQuery.IndexId,
                                                                     storeContext,
                                                                     out metadata,
                                                                     out metadataPropertyCollection);
                    }
                    else
                    {
                        // Get CacheIndexInternal
                        Index indexInfo = indexTypeMapping.IndexCollection[metadataPropertyQuery.TargetIndexName];
                        CacheIndexInternal cacheIndexInternal = IndexServerUtils.GetCacheIndexInternal(storeContext,
                                                                                                       messageContext.
                                                                                                           TypeId,
                                                                                                       metadataPropertyQuery
                                                                                                           .PrimaryId,
                                                                                                       metadataPropertyQuery
                                                                                                           .IndexId,
                                                                                                       indexInfo.
                                                                                                           ExtendedIdSuffix,
                                                                                                       metadataPropertyQuery
                                                                                                           .
                                                                                                           TargetIndexName,
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
                                                                                                       DomainSpecificProcessingType
                                                                                                           .None,
                                                                                                       null,
                                                                                                       null,
                                                                                                       null,
                                                                                                       true);

                        if (cacheIndexInternal != null)
                        {
                            metadataPropertyCollection = cacheIndexInternal.MetadataPropertyCollection;
                        }
                    }

                    #endregion
                }

                metadataPropertyQueryResult = new MetadataPropertyQueryResult
                                     {
                                         MetadataPropertyCollection = metadataPropertyCollection
                                     };
            }
            catch (Exception ex)
            {
                metadataPropertyQueryResult = new MetadataPropertyQueryResult
                {
                    MetadataPropertyCollection = metadataPropertyCollection,
                    ExceptionInfo = ex.Message
                };
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing MetadataPropertyQuery : {1} for IndexId : {2} and TargetIndexname : {3}", 
                    messageContext.TypeId, 
                    ex,
                    metadataPropertyQuery.IndexId != null ? IndexCacheUtils.GetReadableByteArray(metadataPropertyQuery.IndexId) : "Null",
                    metadataPropertyQuery.TargetIndexName ?? "Null");
            }
            return metadataPropertyQueryResult;
        }
    }
}
