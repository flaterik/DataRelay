using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using System.Text;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal class SpanQueryProcessor : MultiIndexIdQueryProcessor<SpanQueryResult>
    {
        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanQueryProcessor"/> class.
        /// </summary>
        private SpanQueryProcessor(){ }

        #endregion

        private static readonly SpanQueryProcessor instance = new SpanQueryProcessor();
        /// <summary>
        /// Gets the SpanQueryProcessor instance.
        /// </summary>
        /// <value>The instance.</value>
        internal static SpanQueryProcessor Instance
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
        protected override void ValidateQuery(IndexTypeMapping indexTypeMapping, BaseMultiIndexIdQuery<SpanQueryResult> query, MessageContext messageContext)
        {
            SpanQuery spanQuery = query as SpanQuery;
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

            if (spanQuery.Offset < 1 && spanQuery.Span != 0)
            {
                throw new Exception("SpanQuery.Offset should be greater than zero except when SpanQuery.Span is zero");
            }
        }

        /// <summary>
        /// Processes the subsets.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="resultItemList">The result item list.</param>
        protected override void ProcessSubsets(BaseMultiIndexIdQuery<SpanQueryResult> query, ref List<ResultItem> resultItemList)
        {
            SpanQuery spanQuery = query as SpanQuery;
            if (!spanQuery.ClientSideSubsetProcessingRequired && spanQuery.Span != 0)
            {
                List<ResultItem> spanFilteredResultItemList = new List<ResultItem>();

                if (resultItemList.Count >= spanQuery.Offset)
                {
                    for (int i = spanQuery.Offset - 1; i < resultItemList.Count && spanFilteredResultItemList.Count < spanQuery.Span; i++)
                    {
                        spanFilteredResultItemList.Add(resultItemList[i]);
                    }
                }
                resultItemList = spanFilteredResultItemList;
            }
        }

        /// <summary>
        /// Formats the query info.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>String containing formatted QueryInfo</returns>
        protected override string FormatQueryInfo(BaseMultiIndexIdQuery<SpanQueryResult> query)
        {
            SpanQuery spanQuery = query as SpanQuery;
            StringBuilder stb = new StringBuilder(base.FormatQueryInfo(query));
            stb.Append("Span: ").Append(spanQuery.Span).Append(", ");
            stb.Append("Offset: ").Append(spanQuery.Offset);

            return stb.ToString();
        }

        /// <summary>
        /// Sets the item counter.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="outDeserializationContext">The OutDeserializationContext.</param>
        protected override void SetItemCounter(short typeId, OutDeserializationContext outDeserializationContext)
        {
            PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.NumOfItemsInIndexPerSpanQuery,
                typeId,
                outDeserializationContext.TotalCount);

            PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.NumOfItemsReadPerSpanQuery,
                typeId,
                outDeserializationContext.ReadItemCount);
        }

        /// <summary>
        /// Sets the index id list counter.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="query">The query.</param>
        protected override void SetIndexIdListCounter(short typeId, BaseMultiIndexIdQuery<SpanQueryResult> query)
        {
            PerformanceCounters.Instance.SetCounterValue(PerformanceCounterEnum.IndexLookupAvgPerSpanQuery,
                typeId,
                query.IndexIdList.Count);
        }
    }
}