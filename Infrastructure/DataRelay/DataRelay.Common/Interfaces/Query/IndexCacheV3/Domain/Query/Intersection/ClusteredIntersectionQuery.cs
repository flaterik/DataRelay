using System;
using System.Collections.Generic;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3.Domain.Query.Intersection;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class ClusteredIntersectionQuery : BaseClusteredIntersectionQuery<IntersectionQueryResult>
    {
        #region Ctors
        public ClusteredIntersectionQuery()
        {
        }

        public ClusteredIntersectionQuery(List<byte[]> indexIdList, string targetIndexName)
            : base(indexIdList, targetIndexName)
        {
        }
        #endregion
    }

    public class BaseClusteredIntersectionQuery<TQueryResult> : IntersectionQuery, ISplitable<TQueryResult>
        where TQueryResult : IntersectionQueryResult, new()
    {
        #region Ctors
        public BaseClusteredIntersectionQuery()
        {
        }

        public BaseClusteredIntersectionQuery(List<byte[]> indexIdList, string targetIndexName)
            : base(indexIdList, targetIndexName)
        {
        }
        #endregion

        #region ISplitable Members
        public List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup)
        {
            IntersectionQuery intersectionQuery;
            List<IPrimaryRelayMessageQuery> queryList = new List<IPrimaryRelayMessageQuery>();
            Dictionary<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>> clusterParamsMapping;

            IndexCacheUtils.SplitIndexIdsByCluster(indexIdList, primaryIdList, intersectionQueryParamsMapping, numClustersInGroup, out clusterParamsMapping);

            foreach (KeyValuePair<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>> clusterParam in clusterParamsMapping)
            {
                intersectionQuery = new IntersectionQuery(this)
                                        {
                                            primaryId = clusterParam.Key,
                                            indexIdList = clusterParam.Value.First,
                                            primaryIdList = clusterParam.Value.Second,
                                            intersectionQueryParamsMapping = clusterParam.Value.Third
                                        };
                queryList.Add(intersectionQuery);
            }
            return queryList;
        }
        #endregion

        #region IPrimaryQueryId Members
        public override int PrimaryId
        {
            get
            {
                throw new Exception("ClusteredIntersectionQuery is routed to one or more destinations. No single PrimaryId value can be retrived for this query");
            }
        }
        #endregion

        #region IMergeableQueryResult<TQueryResult> Members
        public TQueryResult MergeResults(IList<TQueryResult> partialResults)
        {
            TQueryResult completeResult = null;

            if (partialResults != null && partialResults.Count > 0)
            {
                if (partialResults.Count == 1)
                {
                    // No need to merge anything
                    completeResult = partialResults[0];
                }
                else
                {
                    completeResult =  new TQueryResult();
                    #region Merge partialResults into completeResultList
                    ByteArrayEqualityComparer byteArrayEqualityComparer = new ByteArrayEqualityComparer();
                    Dictionary<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> completeIndexIdIndexHeaderMapping =
                        new Dictionary<byte[], IndexHeader>(byteArrayEqualityComparer);

                    foreach (TQueryResult partialResult in partialResults)
                    {
                        if (partialResult != null && partialResult.ResultItemList != null && partialResult.ResultItemList.Count > 0)
                        {
                            if (completeResult.ResultItemList == null || completeResult.ResultItemList.Count == 0)
                            {
                                completeResult = partialResult;
                            }
                            else
                            {
                                IntersectionAlgo.Intersect(
                                    partialResult.IsTagPrimarySort,
                                    partialResult.SortFieldName,
                                    partialResult.LocalIdentityTagNames,
                                    partialResult.SortOrderList,
                                    completeResult,
                                    partialResult);

                                if (completeResult.ResultItemList == null || completeResult.ResultItemList.Count < 1)
                                {
                                    completeIndexIdIndexHeaderMapping = null;
                                    break;
                                }
                            }
                            if (getIndexHeader && partialResult.IndexIdIndexHeaderMapping != null)
                            {
                                foreach (KeyValuePair<byte[], IndexHeader> kvp in partialResult.IndexIdIndexHeaderMapping)
                                {
                                    completeIndexIdIndexHeaderMapping.Add(kvp.Key, kvp.Value);
                                }
                            }
                        }
                        else
                        {
                            // Unable to fetch one of the indexes. Stop Interestion !!
                            completeResult.ResultItemList = null;
                            completeIndexIdIndexHeaderMapping = null;
                            break;
                        }
                    }

                    #region Create final result
                    completeResult.IndexIdIndexHeaderMapping = completeIndexIdIndexHeaderMapping;
                    #endregion

                    #endregion
                }
            }
            return completeResult;
        }
        #endregion
    }
}