using MySpace.DataRelay.Client;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    /// <summary>
    /// Remote clustered paged index query processor 
    /// </summary>Process a remote clustered paged index query 
    internal class RemoteClusteredPagedIndexQueryProcessor : BaseRemoteClusteredQueryProcessor
    {
        private static RemoteClusteredPagedIndexQueryProcessor instance = new RemoteClusteredPagedIndexQueryProcessor();

        public static RemoteClusteredPagedIndexQueryProcessor Instance
        {
            get
            {
                return instance;
            }
        }

        private RemoteClusteredPagedIndexQueryProcessor() { }

        /// <summary>
        /// Process a RemoteClusteredIntersectionQuery, composing a VirtualClusteredPagedIndexQuery
        /// and send query messages
        /// </summary>
        /// <param name="remoteClusteredPagedIndexQuery">incoming RemoteClusteredIntersectionQuery</param>
        /// <param name="messageContext">message context</param>
        /// <param name="storeContext">store context</param>
        /// <returns>query result</returns>
        internal PagedIndexQueryResult Process(RemoteClusteredPagedIndexQuery remoteClusteredPagedIndexQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            // increment the performance counter
            PerformanceCounters.Instance.SetCounterValue(
                PerformanceCounterEnum.IndexLookupAvgPerRemoteClusteredPagedIndexQuery,
                messageContext.TypeId,
                remoteClusteredPagedIndexQuery.IndexIdList.Count);

            // calling pagedIndexQuery copy ctor
            VirtualPagedIndexQuery query = new VirtualPagedIndexQuery(remoteClusteredPagedIndexQuery)
            {
                CacheTypeName = storeContext.GetTypeName(messageContext.TypeId),

                // we only get reference at this point
                ExcludeData = true
             };

            PagedIndexQueryResult queryResult = RelayClient.Instance.SubmitQuery<VirtualPagedIndexQuery, PagedIndexQueryResult>(query);

            // retrieve the data
            GetDataItems(remoteClusteredPagedIndexQuery.FullDataIdInfo, remoteClusteredPagedIndexQuery.ExcludeData, messageContext, storeContext, queryResult);

            return queryResult;
        }
    }
}
