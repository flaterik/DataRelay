using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using System.Text;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal class PagedQueryProcessor : MultiIndexIdQueryProcessor<PagedIndexQueryResult>
    {
        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="PagedQueryProcessor"/> class.
        /// </summary>
        private PagedQueryProcessor() { }

        #endregion

        private static readonly PagedQueryProcessor instance = new PagedQueryProcessor();
        /// <summary>
        /// Gets the PagedQueryProcessor instance.
        /// </summary>
        /// <value>The instance.</value>
        internal static PagedQueryProcessor Instance
        {
            get
            {
                return instance;
            }
        }


        /// <summary>
        /// Validates the query.
        /// </summary>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="query">The query.</param>
        /// <param name="messageContext">The message context.</param>
        protected override void ValidateQuery(IndexTypeMapping indexTypeMapping, BaseMultiIndexIdQuery<PagedIndexQueryResult> query, MessageContext messageContext)
        {
            if (!indexTypeMapping.IndexCollection.Contains(query.TargetIndexName))
            {
                throw new Exception("Invalid TargetIndexName - " + query.TargetIndexName);
            }

            if (query.IndexIdList == null || query.IndexIdList.Count == 0)
            {
                throw new Exception("No IndexIdList present on the query");
            }

            if (query.PrimaryIdList != null && query.PrimaryIdList.Count != query.IndexIdList.Count)
            {
                throw new Exception("PrimaryIdList.Count does not match with IndexIdList.Count");
            }

            PerformQueryOverride(indexTypeMapping, query, messageContext);
        }

        /// <summary>
        /// Performs the query override.
        /// </summary>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="query">The query.</param>
        /// <param name="messageContext">The message context.</param>
        private void PerformQueryOverride(IndexTypeMapping indexTypeMapping, BaseMultiIndexIdQuery<PagedIndexQueryResult> query, MessageContext messageContext)
        {
            PagedIndexQuery pagedQuery = query as PagedIndexQuery;
            if (indexTypeMapping.QueryOverrideSettings != null)
            {
                // override MaxItemsPerIndexThreshold if required
                if (indexTypeMapping.QueryOverrideSettings.MaxItemsPerIndexThreshold != 0 &&
                    pagedQuery.IndexIdList.Count > 1 &&
                    pagedQuery.PageNum == 0 &&
                    (pagedQuery.IndexIdParamsMapping == null || pagedQuery.IndexIdParamsMapping.Count == 0) &&
                    pagedQuery.MaxItemsPerIndex == 0 &&
                    pagedQuery.Filter == null)
                {
                    LoggingUtil.Log.ErrorFormat(
                        "Encountered Potentially Bad Paged Query.  Overriding MaxItemsPerIndex to {0}.  AddressHistory {1}.  Original Query Info: {2}",
                        indexTypeMapping.QueryOverrideSettings.MaxItemsPerIndexThreshold,
                        FormatAddressHistory(messageContext.AddressHistory),
                        FormatQueryInfo(pagedQuery));
                    pagedQuery.MaxItemsPerIndex = indexTypeMapping.QueryOverrideSettings.MaxItemsPerIndexThreshold;
                }

                // override PageNum if required
                if (indexTypeMapping.QueryOverrideSettings.DisableFullPageQuery && pagedQuery.PageNum == 0 && pagedQuery.PageSize != 0)
                {
                    LoggingUtil.Log.InfoFormat(
                        "Configuration rules require overriding PageNum to 1.  AddressHistory {0}.  Original Query Info: {1}",
                        FormatAddressHistory(messageContext.AddressHistory),
                        FormatQueryInfo(pagedQuery));
                    pagedQuery.PageNum = 1;
                }
            }
        }

        /// <summary>
        /// Processes the subsets.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="resultItemList">The result item list.</param>
        protected override void ProcessSubsets(BaseMultiIndexIdQuery<PagedIndexQueryResult> query, ref List<ResultItem> resultItemList)
        {
            PagedIndexQuery pagedQuery = query as PagedIndexQuery;
            if (!pagedQuery.ClientSideSubsetProcessingRequired && pagedQuery.PageNum != 0)
            {
                List<ResultItem> pageFilteredResultItemList = new List<ResultItem>();
                int pageSize = pagedQuery.PageSize;
                int start = (pagedQuery.PageNum - 1) * pageSize;
                int end = pagedQuery.PageNum * pageSize;
                for (int i = start; i < end && i < resultItemList.Count; i++)
                {
                    pageFilteredResultItemList.Add(resultItemList[i]);
                }
                resultItemList = pageFilteredResultItemList;
            }
        }

        /// <summary>
        /// Formats the query info.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>String containing formatted QueryInfo</returns>
        protected override string FormatQueryInfo(BaseMultiIndexIdQuery<PagedIndexQueryResult> query)
        {
            PagedIndexQuery pagedQuery = query as PagedIndexQuery;
            StringBuilder stb = new StringBuilder(base.FormatQueryInfo(query));
            stb.Append("PageNum: ").Append(pagedQuery.PageNum).Append(", ");
            stb.Append("PageSize: ").Append(pagedQuery.PageSize);
            return stb.ToString();
        }

        /// <summary>
        /// Sets the item counter.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="outDeserializationContext">The OutDeserializationContext.</param>
        protected override void SetItemCounter(short typeId, OutDeserializationContext outDeserializationContext)
        {
            PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.NumOfItemsInIndexPerPagedIndexQuery, 
                typeId,
                outDeserializationContext.TotalCount);

            PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.NumOfItemsReadPerPagedIndexQuery,
                typeId,
                outDeserializationContext.ReadItemCount);
        }

        /// <summary>
        /// Sets the index id list counter.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="query">The query.</param>
        protected override void SetIndexIdListCounter(short typeId, BaseMultiIndexIdQuery<PagedIndexQueryResult> query)
        {
            PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.IndexLookupAvgPerPagedIndexQuery,
                typeId,
                query.IndexIdList.Count);
        }
    }
}