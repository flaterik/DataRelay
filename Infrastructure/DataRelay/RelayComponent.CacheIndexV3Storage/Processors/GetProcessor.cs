using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using MySpace.Common.Storage;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class GetProcessor
    {
        /// <summary>
        /// Processes the specified message context.
        /// </summary>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>Raw CacheIndexInternal</returns>
        internal static byte[] Process(MessageContext messageContext, IndexStoreContext storeContext)
        {
            short typeId;
            short messageTypeId = messageContext.TypeId;

            if (storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection.Contains(messageTypeId))
            {
                typeId = messageTypeId;
            }
            else if (!storeContext.RelatedTypeIds.TryGetValue(messageTypeId, out typeId))
            {
                LoggingUtil.Log.InfoFormat("Invalid TypeID for GetMessage {0}", messageTypeId);
                return null;
            }

            return storeContext.IndexStorageComponent.GetBuffer(
                typeId,
                new StorageKey(
                    IndexServerUtils.FormExtendedId(messageContext.ExtendedId, 0),
                    IndexCacheUtils.GeneratePrimaryId(messageContext.ExtendedId)));
        }
    }
}