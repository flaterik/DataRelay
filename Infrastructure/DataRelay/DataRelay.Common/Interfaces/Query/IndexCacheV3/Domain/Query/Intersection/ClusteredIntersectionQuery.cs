using System;
using System.Collections.Generic;
using System.Text;
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

        public ClusteredIntersectionQuery(IntersectionQuery query)
            : base(query)
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

        public BaseClusteredIntersectionQuery(IntersectionQuery query) 
            : base(query)
        {
        }
        #endregion

        #region ISplitable Members
        public List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup)
        {
            IntersectionQuery intersectionQuery;
            List<IPrimaryRelayMessageQuery> queryList = new List<IPrimaryRelayMessageQuery>();
            Dictionary<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>> clusterParamsMapping;

            IndexCacheUtils.SplitIndexIdsByCluster(IndexIdList, PrimaryIdList, intersectionQueryParamsMapping, numClustersInGroup, out clusterParamsMapping);

            if (clusterParamsMapping.Count == 1)
            {
                //This means that the query is not spilt across more than multiple clusters and the MaxResultItems criteria can be applied on the server
                IsSingleClusterQuery = true;
            }

            foreach (KeyValuePair<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>> clusterParam in clusterParamsMapping)
            {
                intersectionQuery = new IntersectionQuery(this)
                {
                    primaryId = clusterParam.Key,
                    IndexIdList = clusterParam.Value.First,
                    PrimaryIdList = clusterParam.Value.Second,
                    intersectionQueryParamsMapping = clusterParam.Value.Third
                };
                queryList.Add(intersectionQuery);
            }
            return queryList;
        }

        public List<IPrimaryRelayMessageQuery> SplitQuery(
            int numClustersInGroup,
            int localClusterPosition,
            out IPrimaryRelayMessageQuery localQuery)
        {
            IntersectionQuery intersectionQuery;
            List<IPrimaryRelayMessageQuery> queryList = new List<IPrimaryRelayMessageQuery>();
            Dictionary<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>> clusterParamsMapping;
            localQuery = null;

            IndexCacheUtils.SplitIndexIdsByCluster(IndexIdList, PrimaryIdList, intersectionQueryParamsMapping, numClustersInGroup, out clusterParamsMapping);
            
            if(clusterParamsMapping.Count == 1)
            {
                //This means that the query is not spilt across more than multiple clusters and the MaxResultItems criteria can be applied on the server
                IsSingleClusterQuery = true;
            }

            foreach (KeyValuePair<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>> clusterParam in clusterParamsMapping)
            {
                intersectionQuery = new IntersectionQuery(this)
                {
                    primaryId = clusterParam.Key,
                    IndexIdList = clusterParam.Value.First,
                    PrimaryIdList = clusterParam.Value.Second,
                    intersectionQueryParamsMapping = clusterParam.Value.Third
                };

                if (clusterParam.Key == localClusterPosition)
                {
                    localQuery = intersectionQuery;
                }
                else
                {
                    queryList.Add(intersectionQuery);
                }
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
                    completeResult = new TQueryResult();
                    StringBuilder exceptionStringBuilder = new StringBuilder();

                    #region Merge partialResults into completeResultList
                    ByteArrayEqualityComparer byteArrayEqualityComparer = new ByteArrayEqualityComparer();
                    Dictionary<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> completeIndexIdIndexHeaderMapping =
                        new Dictionary<byte[], IndexHeader>(byteArrayEqualityComparer);

                    for (int i = 0; i < partialResults.Count; i++)
                    {
                        TQueryResult partialResult = partialResults[i];
                        if (partialResult != null && partialResult.ResultItemList != null &&
                            partialResult.ResultItemList.Count > 0)
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
                                    partialResult,
                                    MaxResultItems,
                                    i == partialResults.Count - 1 ? true : false);

                                if (completeResult.ResultItemList == null || completeResult.ResultItemList.Count < 1)
                                {
                                    completeIndexIdIndexHeaderMapping = null;
                                    break;
                                }
                            }
                            if (GetIndexHeader && partialResult.IndexIdIndexHeaderMapping != null)
                            {
                                foreach (
                                    KeyValuePair<byte[], IndexHeader> kvp in partialResult.IndexIdIndexHeaderMapping)
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

                            if ((partialResult != null) && (!String.IsNullOrEmpty(partialResult.ExceptionInfo)))
                            {
                                exceptionStringBuilder.Append(partialResult.ExceptionInfo);
                                exceptionStringBuilder.Append(" ");
                            }

                            break;
                        }
                    }

                    #region Create final result
                    completeResult.IndexIdIndexHeaderMapping = completeIndexIdIndexHeaderMapping;

                    if (exceptionStringBuilder.Length > 0)
                    {
                        completeResult.ExceptionInfo = exceptionStringBuilder.ToString();
                    }

                    #endregion

                    #endregion
                }
            }
            return completeResult;
        }
        #endregion
    }
}