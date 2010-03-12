using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using System;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class DeleteAllInTypeProcessor
    {
        /// <summary>
        /// Processes the specified message context.
        /// </summary>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        internal static void Process(MessageContext messageContext, IndexStoreContext storeContext)
        {
            // TBD : Support concurrent DeleteAllInType for different types
            lock (LockingUtil.Instance.LockerObjects)
            {
                RelayMessage msg;

                if (DataTierUtil.ShouldForwardToDataTier(messageContext.RelayTTL,
                    messageContext.SourceZone,
                    storeContext.MyZone,
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId].IndexServerMode))
                {
                    // Send DeleteAll to Data Store
                    short relatedTypeId;
                    if(!storeContext.TryGetRelatedIndexTypeId(messageContext.TypeId, out relatedTypeId))
                    {
                        LoggingUtil.Log.ErrorFormat("Invalid RelatedTypeId for TypeId - {0}", messageContext.TypeId);
                        throw new Exception("Invalid RelatedTypeId for TypeId - " + messageContext.TypeId);
                    }
                    msg = new RelayMessage(relatedTypeId, 0, MessageType.DeleteAllInType);
                    storeContext.ForwarderComponent.HandleMessage(msg);
                }

                // Send DeleteAll to local Index storage
                msg = new RelayMessage(messageContext.TypeId, 0, MessageType.DeleteAllInType);
                storeContext.IndexStorageComponent.HandleMessage(msg);

                // Delete hash mapping entries in file
                storeContext.RemoveType(messageContext.TypeId);
            }
        }
    }
}