using MySpace.DataRelay.Client;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class RemoteClusteredIntersectionQueryProcessor
    {
        /// <summary>
        /// Processes the specified remote clustered intersection query.
        /// </summary>
        /// <param name="remoteClusteredIntersectionQuery">The remote clustered intersection query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>IntersectionQueryResult</returns>
        internal static IntersectionQueryResult Process(RemoteClusteredIntersectionQuery remoteClusteredIntersectionQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            // increment performance counter
            PerformanceCounters.Instance.SetCounterValue(
                PerformanceCounterEnum.IndexLookupAvgPerRemoteClusteredIntersectionQuery,
                messageContext.TypeId,
                remoteClusteredIntersectionQuery.IndexIdList.Count);
            
            VirtualClusteredIntersectionQuery query = new VirtualClusteredIntersectionQuery(
                 remoteClusteredIntersectionQuery.IndexIdList,
                 remoteClusteredIntersectionQuery.TargetIndexName,
                 storeContext.GetTypeName(messageContext.TypeId))
                                                          {
                                                              ExcludeData = remoteClusteredIntersectionQuery.ExcludeData,
                                                              GetIndexHeader = remoteClusteredIntersectionQuery.GetIndexHeader,
                                                              Filter = remoteClusteredIntersectionQuery.Filter,
                                                              intersectionQueryParamsMapping = remoteClusteredIntersectionQuery.IntersectionQueryParamsMapping,
                                                              PrimaryId = remoteClusteredIntersectionQuery.PrimaryId,
                                                              PrimaryIdList = remoteClusteredIntersectionQuery.PrimaryIdList
                                                          };

            return RelayClient.Instance.SubmitQuery<VirtualClusteredIntersectionQuery, IntersectionQueryResult>(query);
        }
    }
}