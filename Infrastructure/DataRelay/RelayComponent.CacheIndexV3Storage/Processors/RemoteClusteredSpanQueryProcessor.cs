using MySpace.DataRelay.Client;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal class RemoteClusteredSpanQueryProcessor : BaseRemoteClusteredQueryProcessor
    {
        private static RemoteClusteredSpanQueryProcessor instance = new RemoteClusteredSpanQueryProcessor();

        public static RemoteClusteredSpanQueryProcessor Instance
        {
            get
            {
                return instance;
            }
        }

        private RemoteClusteredSpanQueryProcessor() {}

        internal SpanQueryResult Process(RemoteClusteredSpanQuery remoteClusteredSpanQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            // increment the performance counter
            PerformanceCounters.Instance.SetCounterValue(
                PerformanceCounterEnum.IndexLookupAvgPerRemoteClusteredSpanQuery,
                messageContext.TypeId,
                remoteClusteredSpanQuery.IndexIdList.Count);

            // creating a spanQuery with copy ctor
            VirtualSpanQuery query = new VirtualSpanQuery(remoteClusteredSpanQuery)
            {
                CacheTypeName = storeContext.GetTypeName(messageContext.TypeId),

                // we get only reference at this point
                ExcludeData = true
            };

            SpanQueryResult queryResult = RelayClient.Instance.SubmitQuery<VirtualSpanQuery, SpanQueryResult>(query);

            GetDataItems(remoteClusteredSpanQuery.FullDataIdInfo, remoteClusteredSpanQuery.ExcludeData, messageContext, storeContext, queryResult);

            return queryResult;
        }
    }
}
