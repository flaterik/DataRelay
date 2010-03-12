using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

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
            byte[] payloadByteArray = null;
            RelayMessage getMsg = new RelayMessage
                                      {
                                          MessageType = MessageType.Get,
                                          Id = IndexCacheUtils.GeneratePrimaryId(messageContext.ExtendedId),
                                          ExtendedId = IndexServerUtils.FormExtendedId(messageContext.ExtendedId, 0)  // Pull first of the multiple indexes
                                      };

            if (storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection.Contains(messageContext.TypeId))
            {
                getMsg.TypeId = messageContext.TypeId;
            }
            else if (storeContext.RelatedTypeIds.TryGetValue(messageContext.TypeId, out getMsg.TypeId))
            {
            }
            else
            {
                LoggingUtil.Log.InfoFormat("Invalid TypeID for GetMessage {0}", messageContext.TypeId);
                return payloadByteArray;
            }

            storeContext.IndexStorageComponent.HandleMessage(getMsg);

            if (getMsg.Payload != null)
            {
                payloadByteArray = getMsg.Payload.ByteArray;
            }
            return payloadByteArray;
        }
    }
}