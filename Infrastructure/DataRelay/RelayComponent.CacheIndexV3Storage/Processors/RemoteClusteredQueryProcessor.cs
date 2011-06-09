using System;
using System.Collections.Generic;
using System.Threading;
using MySpace.DataRelay.Common.Interfaces.Query;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using MySpace.DataRelay.RelayComponent.Forwarding;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal class RemoteClusteredQueryProcessor
    {
        private static RemoteClusteredQueryProcessor instance = new RemoteClusteredQueryProcessor();

        public static RemoteClusteredQueryProcessor Instance
        {
            get
            {
                return instance;
            }
        }

        public TQueryResult Process <TQueryResult, TQuery>(
            PerformanceCounterEnum indexNumberCounterName,
            TQuery query,
            MessageContext messageContext,
            IndexStoreContext storeContext,
            MultiIndexIdQueryProcessor<TQueryResult> processor)
            where  TQueryResult : BaseMultiIndexIdQueryResult, new()
            where TQuery : BaseMultiIndexIdQuery<TQueryResult>
        {
            if ((query.IndexIdList == null) || (query.IndexIdList.Count == 0))
            {
                throw new Exception("Remote query index list is null or count is 0, type id is " + messageContext.TypeId);
            }

            // increment the performance counter
            PerformanceCounters.Instance.SetCounterValue(
                    indexNumberCounterName,
                    messageContext.TypeId, 
                    query.IndexIdList.Count);

            bool compressOption = storeContext.GetCompressOption(messageContext.TypeId);
            int numberOfClusters = storeContext.NumClustersInGroup;
            int myClusterPosition = storeContext.MyClusterPosition;

            List<TQueryResult> resultList = new List<TQueryResult>();

            IPrimaryRelayMessageQuery localQuery;
            TQueryResult finalResult = null;
            TQueryResult localResult = null;
            TQuery localIndexQuery;

            List<IPrimaryRelayMessageQuery> queryList = query.SplitQuery(
                numberOfClusters,
                myClusterPosition,
                out localQuery);

            // send remote messages async, then run the local queries
            int remoteQueryCount = queryList.Count;

            if (remoteQueryCount == 0)    // when there is no remote query
            {
                if (localQuery != null)
                {
                    localIndexQuery = (TQuery)localQuery;

                    finalResult = processor.Process(localIndexQuery, messageContext, storeContext);
                }
            }
            else
            {
                long endCount = 0; 

                RelayMessage[] remoteQueryMessages = new RelayMessage[numberOfClusters];
                TQueryResult[] queryResultArray = new TQueryResult[numberOfClusters];

                Forwarder forwardingComponent = (Forwarder)storeContext.ForwarderComponent;

                using (AutoResetEvent evt = new AutoResetEvent(false))
                {
                    AsyncCallback callback = asyncResult =>
                    {
                        try
                        {
                            forwardingComponent.EndHandleMessage(asyncResult);

                            TQueryResult remoteResult = new TQueryResult();
                            int index = (int)asyncResult.AsyncState;
                            remoteQueryMessages[index].GetObject<TQueryResult>(remoteResult);
                            queryResultArray[index] = remoteResult;
                        }
                        catch (Exception ex)
                        {
                            LoggingUtil.Log.ErrorFormat(
                                "Failed to get inter-cluster query result : {0}", ex);
                        }
                        finally
                        {
                            if (Interlocked.Increment(ref endCount) == remoteQueryCount)
                            {
                                evt.Set();
                            }
                        }
                    };

                    for (int i = 0; i < remoteQueryCount; i++)
                    {
                        try
                        {
                            TQuery myRemoteQuery = (TQuery)queryList[i];
                            myRemoteQuery.ExcludeData = true;

                            // compose query message
                            RelayMessage queryMsg = RelayMessage.GetQueryMessageForQuery(
                                messageContext.TypeId,
                                compressOption,
                                myRemoteQuery);

                            queryMsg.IsInterClusterMsg = true;

                            remoteQueryMessages[myRemoteQuery.PrimaryId] = queryMsg;

                            forwardingComponent.BeginHandleMessage(queryMsg, myRemoteQuery.PrimaryId, callback);
                        }
                        catch (Exception ex)
                        {
                            LoggingUtil.Log.ErrorFormat("Exception in Calling BeginHandleMessage : {0}", ex);

                            // increment the end count since the exception caught, the async call is not successful
                            if (Interlocked.Increment(ref endCount) == remoteQueryCount)
                            {
                                evt.Set();
                            }
                        }
                    }

                    try
                    {
                        // handle local query using the local process
                        if (localQuery != null)
                        {
                            localIndexQuery = (TQuery)localQuery;

                            localResult = processor.Process(localIndexQuery, messageContext, storeContext);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingUtil.Log.ErrorFormat("Exception in getting local query result : {0}", ex);
                    }

                    // using infinite as timeout here, the timeout is already handled at the forwarder layer
                    if (evt.WaitOne(IndexStoreContext.Instance.RemoteClusteredQueryTimeOut, true) == false)
                    {
                        LoggingUtil.Log.Error("Wait handler in remote clustered query didnt get signaled within the timeout period");
                    }
                    
                    if (localResult != null)
                    {
                        queryResultArray[storeContext.MyClusterPosition] = localResult;
                    }
                } // end of using

                // convert the array to list for the merge processing
                for (int i = 0; i < numberOfClusters; i++)
                {
                    if (queryResultArray[i] != null)
                    {
                        resultList.Add(queryResultArray[i]);
                    }
                }

                // merge query results
                finalResult = query.MergeResults(resultList);

            }  // end of else

            // retrieve the data
            GetDataItems(query.FullDataIdInfo, query.ExcludeData, messageContext, storeContext, finalResult);

            return finalResult;
        }

        public void GetDataItems(FullDataIdInfo info, bool excludeData, MessageContext messageContext, IndexStoreContext storeContext, BaseMultiIndexIdQueryResult queryResult)
        {
            if ((excludeData == false) && (queryResult != null))
            {
                IndexTypeMapping indexTypeMapping =
                storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                DataTierUtil.GetData(queryResult.ResultItemList, 
                    queryResult.GroupByResult, 
                    storeContext, 
                    messageContext, 
                    indexTypeMapping.FullDataIdFieldList, 
                    info);
            }
        }
    }
}
