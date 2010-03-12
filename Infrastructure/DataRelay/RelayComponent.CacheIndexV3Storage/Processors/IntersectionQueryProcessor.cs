using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3.Domain.Query.Intersection;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class IntersectionQueryProcessor
    {
        /// <summary>
        /// Processes the specified intersection query.
        /// </summary>
        /// <param name="intersectionQuery">The intersection query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>IntersectionQueryResult</returns>
        internal static IntersectionQueryResult Process(IntersectionQuery intersectionQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            //Fetch each index (assume all indexes are local) and perform intersection and return the results
            IntersectionQueryResult intersectionQueryResult;
            List<IndexDataItem> resultItemList = null;
            Dictionary<byte[], IndexHeader> indexIdIndexHeaderMapping = new Dictionary<byte[], IndexHeader>(new ByteArrayEqualityComparer());
            List<string> localIdentityTagNames = null;
            bool isTagPrimarySort = false;
            string sortFieldName = null;
            List<SortOrder> sortOrderList = null;
            StringBuilder exceptionInfo = new StringBuilder();

            try
            {
                if (intersectionQuery.IndexIdList.Count > 0)
                {
                    IndexTypeMapping indexTypeMapping =
                        storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];
                    ValidateQuery(indexTypeMapping, intersectionQuery);

                    #region Set sort vars
                    
                    Index targetIndexInfo = indexTypeMapping.IndexCollection[intersectionQuery.TargetIndexName];
                    localIdentityTagNames = indexTypeMapping.IndexCollection[intersectionQuery.TargetIndexName].LocalIdentityTagList;
                    bool sortFieldPartOfLocalId = IsSortFieldPartOfLocalId(localIdentityTagNames, targetIndexInfo.PrimarySortInfo);
                    TagSort itemIdTagSort = new TagSort("ItemId", false, new SortOrder(DataType.Int32, SortBy.ASC));
                    
                    if (!sortFieldPartOfLocalId)
                    {
                        //Set sort vars
                        isTagPrimarySort = itemIdTagSort.IsTag;
                        sortFieldName = itemIdTagSort.TagName;
                        sortOrderList = new List<SortOrder>(1) { itemIdTagSort.SortOrder };
                    }
                    else
                    {
                        isTagPrimarySort = targetIndexInfo.PrimarySortInfo.IsTag;
                        sortFieldName = targetIndexInfo.PrimarySortInfo.FieldName;
                        sortOrderList = targetIndexInfo.PrimarySortInfo.SortOrderList;
                    }
                    
                    #endregion

                    #region Fetch CacheIndexInternal and Intersect
                    
                    CacheIndexInternal targetIndex;
                    CacheIndexInternal resultCacheIndexInternal = null;
                    IntersectionQueryParams indexIdParam;
                    byte[] indexId;

                    for (int i = 0; i < intersectionQuery.IndexIdList.Count; i++)
                    {
                        #region Extract index and apply criteria
                        
                        indexId = intersectionQuery.IndexIdList[i];
                        indexIdParam = intersectionQuery.GetIntersectionQueryParamForIndexId(indexId);
                        targetIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                            messageContext.TypeId,
                            (intersectionQuery.PrimaryIdList != null && i < intersectionQuery.PrimaryIdList.Count) ?
                                    intersectionQuery.PrimaryIdList[i] :
                                    IndexCacheUtils.GeneratePrimaryId(indexId),
                            indexId,
                            targetIndexInfo.ExtendedIdSuffix,
                            intersectionQuery.TargetIndexName,
                            0,
                            indexIdParam.Filter,
                            true,
                            null,
                            false,
                            false,
                            targetIndexInfo.PrimarySortInfo,
                            targetIndexInfo.LocalIdentityTagList,
                            targetIndexInfo.StringHashCodeDictionary,
                            null);
                        
                        #endregion

                        if (targetIndex != null)
                        {
                            if (targetIndex.Count < 0)
                            {
                                // No items in one of the indexes. Stop Interestion !!
                                resultCacheIndexInternal = null;
                                break;
                            }

                            PerformanceCounters.Instance.SetCounterValue(
                                PerformanceCounterEnum.NumOfItemsInIndexPerIntersectionQuery,
                                messageContext.TypeId,
                                targetIndex.OutDeserializationContext.TotalCount);

                            PerformanceCounters.Instance.SetCounterValue(
                                PerformanceCounterEnum.NumOfItemsReadPerIntersectionQuery,
                                messageContext.TypeId,
                                targetIndex.OutDeserializationContext.ReadItemCount);

                            if (!sortFieldPartOfLocalId)
                            {
                                //Need to sort indexes by item id
                                targetIndex.Sort(itemIdTagSort);
                            }

                            #region Intersection
                            
                            if (resultCacheIndexInternal == null)
                            {
                                // No need to perform intersection for first index
                                resultCacheIndexInternal = targetIndex;
                            }
                            else
                            {
                                IntersectionAlgo.Intersect(isTagPrimarySort,
                                                           sortFieldName,
                                                           localIdentityTagNames,
                                                           sortOrderList,
                                                           resultCacheIndexInternal.InternalItemList,
                                                           targetIndex.InternalItemList);
                                
                                if (resultCacheIndexInternal == null || resultCacheIndexInternal.Count < 1)
                                {
                                    // Unable to fetch one of the indexes. Stop Interestion !!
                                    resultCacheIndexInternal = null;
                                    indexIdIndexHeaderMapping = null;
                                    break;
                                }
                            }
                            
                            #endregion
                        }
                        else
                        {
                            // Unable to fetch one of the indexes. Stop Interestion !!
                            resultCacheIndexInternal = null;
                            indexIdIndexHeaderMapping = null;
                            break;
                        }

                        #region Get MetaData
                        
                        if (intersectionQuery.GetIndexHeader)
                        {
                            if (!indexIdIndexHeaderMapping.ContainsKey(indexId))
                            {
                                indexIdIndexHeaderMapping.Add(indexId,
                                    GetIndexHeader(targetIndex, indexTypeMapping, messageContext.TypeId, IndexCacheUtils.GeneratePrimaryId(indexId), storeContext));
                            }
                        }
                        
                        #endregion
                    }
                    if (resultCacheIndexInternal != null && resultCacheIndexInternal.Count > 0)
                    {
                        resultItemList = CacheIndexInternalAdapter.GetIndexDataItemList(resultCacheIndexInternal, 1, int.MaxValue);
                    }
                    
                    #endregion

                    #region Get data
                    
                    if (!intersectionQuery.ExcludeData && resultItemList != null && resultItemList.Count > 0)
                    {
                        DataTierUtil.GetData(resultItemList, 
                            storeContext, 
                            messageContext, 
                            indexTypeMapping.FullDataIdFieldList, 
                            intersectionQuery.FullDataIdInfo);
                    }
                    
                    #endregion
                }

                intersectionQueryResult = new IntersectionQueryResult(resultItemList,
                         indexIdIndexHeaderMapping,
                         localIdentityTagNames,
                         isTagPrimarySort,
                         sortFieldName,
                         sortOrderList,
                         exceptionInfo.ToString());

                // update performance counter
                PerformanceCounters.Instance.SetCounterValue(
                    PerformanceCounterEnum.IndexLookupAvgPerIntersectionQuery,
                    messageContext.TypeId,
                    intersectionQuery.IndexIdList.Count);
            }
            catch (Exception ex)
            {
                exceptionInfo.Append(" | " + ex.Message);
                intersectionQueryResult = new IntersectionQueryResult(null, null, null, false, null, null, exceptionInfo.ToString());
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing IntersectionQuery : {1}", messageContext.TypeId, ex);
            }
            return intersectionQueryResult;
        }

        /// <summary>
        /// Gets the index header.
        /// </summary>
        /// <param name="targetIndex">Index of the target.</param>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="typeId">The type id.</param>
        /// <param name="primaryId">The primary id.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>IndexHeader</returns>
        private static IndexHeader GetIndexHeader(CacheIndexInternal targetIndex,
            IndexTypeMapping indexTypeMapping,
            short typeId,
            int primaryId,
            IndexStoreContext storeContext)
        {
            IndexHeader indexHeader = new IndexHeader();

            #region Metadata
            
            if (indexTypeMapping.MetadataStoredSeperately)
            {
                #region Check if metadata is stored seperately
                
                //Send a get message to local index storage and fetch seperately stored metadata
                RelayMessage getMsg = new RelayMessage(typeId, primaryId, targetIndex.InDeserializationContext.IndexId, MessageType.Get);
                storeContext.IndexStorageComponent.HandleMessage(getMsg);
                if (getMsg.Payload != null)
                {
                    indexHeader.Metadata = getMsg.Payload.ByteArray;
                }
                
                #endregion
            }
            else
            {
                #region Check metadata on targetIndex
                
                if (indexTypeMapping.IndexCollection[targetIndex.InDeserializationContext.IndexName].MetadataPresent)
                {
                    indexHeader.Metadata = targetIndex.Metadata;
                }
                
                #endregion
            }
            
            #endregion

            #region VirtualCount
            
            indexHeader.VirtualCount = targetIndex.VirtualCount;
            
            #endregion

            return indexHeader;
        }

        /// <summary>
        /// Validates the query.
        /// </summary>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="intersectionQuery">The intersection query.</param>
        private static void ValidateQuery(IndexTypeMapping indexTypeMapping, IntersectionQuery intersectionQuery)
        {
            if (!indexTypeMapping.IndexCollection.Contains(intersectionQuery.TargetIndexName))
            {
                throw new Exception("Invalid TargetIndexName - " + intersectionQuery.TargetIndexName);
            }

            if (intersectionQuery.PrimaryIdList != null && intersectionQuery.PrimaryIdList.Count != intersectionQuery.IndexIdList.Count)
            {
                throw new Exception("PrimaryIdList.Count does not match with IndexIdList.Count");
            }

            if (!intersectionQuery.ExcludeData && FullDataIdContainsIndexId(indexTypeMapping.FullDataIdFieldList))
            {
                throw new Exception("IntersectionQuery.ExcludeData cannot be set to true since FullDataId contains IndexId");
            }
        }

        /// <summary>
        /// Determines whether sort field is a part of local id from the specified local identity tag list.
        /// </summary>
        /// <param name="localIdentityTagList">The local identity tag list.</param>
        /// <param name="primarySortInfo">The primary sort info.</param>
        /// <returns>
        /// 	<c>true</c> if [is sort field part of local id] [the specified local identity tag list]; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsSortFieldPartOfLocalId(List<string> localIdentityTagList,
            PrimarySortInfo primarySortInfo)
        {
            if (!primarySortInfo.IsTag)
            {
                return true;
            }
            //Search sortField in localIdentityTagList
            foreach (string tagName in localIdentityTagList)
            {
                if (string.Compare(tagName, primarySortInfo.FieldName) == 0)
                {
                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// Determines whether FullDataId contains IndexId.
        /// </summary>
        /// <param name="fullDataIdFieldList">The full data id field list.</param>
        /// <returns></returns>
        private static bool FullDataIdContainsIndexId(IEnumerable<FullDataIdField> fullDataIdFieldList)
        {
            foreach (FullDataIdField fullDataIdField in fullDataIdFieldList)
            {
                if (fullDataIdField.FullDataIdType == FullDataIdType.IndexId)
                    return true;
            }
            return false;
        }
    }
}
