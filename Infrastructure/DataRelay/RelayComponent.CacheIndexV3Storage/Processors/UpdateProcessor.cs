using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class UpdateProcessor
    {
        /// <summary>
        /// Processes the specified cache index update.
        /// </summary>
        /// <param name="cacheIndexUpdate">The cache index update.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        internal static void Process(CacheIndexUpdate cacheIndexUpdate, MessageContext messageContext, IndexStoreContext storeContext)
        {
            switch (cacheIndexUpdate.Command.CommandType)
            {
                case CommandType.FilteredIndexDelete:
                    PerformanceCounters.Instance.IncrementCounter(PerformanceCounterEnum.FilterDelete, messageContext.TypeId,1);
                    FilteredIndexDeleteProcessor.Process(cacheIndexUpdate.Command as FilteredIndexDeleteCommand, messageContext, storeContext);
                    break;

                case CommandType.MetadataProperty:
                    PerformanceCounters.Instance.IncrementCounter(PerformanceCounterEnum.MetadataPropertyUpdate, messageContext.TypeId, 1);
                    MetadataPropertyProcessor.Process(cacheIndexUpdate.Command as MetadataPropertyCommand, messageContext, storeContext);
                    break;
            }
        }
    }
}