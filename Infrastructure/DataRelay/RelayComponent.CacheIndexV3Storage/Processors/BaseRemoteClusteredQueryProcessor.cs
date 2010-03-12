using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal abstract class BaseRemoteClusteredQueryProcessor
    {
        public void GetDataItems(FullDataIdInfo info, bool excludeData, MessageContext messageContext, IndexStoreContext storeContext, BaseMultiIndexIdQueryResult queryResult)
        {
            if (excludeData == false)
            {
                IndexTypeMapping indexTypeMapping =
                storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                DataTierUtil.GetData(queryResult.ResultItemList, storeContext, messageContext, indexTypeMapping.FullDataIdFieldList, info);
            }
        }
    }
}
