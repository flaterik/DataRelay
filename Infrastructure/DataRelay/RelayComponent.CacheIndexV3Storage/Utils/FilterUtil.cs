using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    internal static class FilterUtil
    {
        /// <summary>
        /// Processes the filter.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="inclusiveFilter">if set to <c>true</c> includes the items that pass the filter; otherwise , <c>false</c>.</param>
        /// <param name="tagHashCollection">The tag hash collection.</param>
        /// <returns><c>true</c> if item passes the filter; otherwise, <c>false</c></returns>
        internal static bool ProcessFilter(InternalItem internalItem, Filter filter, bool inclusiveFilter, TagHashCollection tagHashCollection)
        {
            bool retVal = DoProcessFilter(internalItem, filter, tagHashCollection);

            if (inclusiveFilter)
            {
                return retVal;
            }
            return !retVal;
        }

        /// <summary>
        /// Processes the aggregate filter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="tagHashCollection">The tag hash collection.</param>
        /// <returns><c>true</c> if item passes the filter; otherwise, <c>false</c></returns>
        private static bool ProcessAggregateFilter<T>(InternalItem internalItem, T filter, TagHashCollection tagHashCollection)
            where T : AggregateFilter
        {
            bool retVal = !filter.ShortCircuitHint;
            List<Filter> later = new List<Filter>();

            // evaluate root level items first
            for (ushort i = 0; i < filter.Count; i++)
            {
                if (filter[i] is Condition)
                {
                    // evaluate now
                    retVal = DoProcessFilter(internalItem, filter[i], tagHashCollection);
                    if (retVal == filter.ShortCircuitHint)
                        break;
                }
                else
                {
                    // evaluate later
                    later.Add(filter[i]);
                }
            }

            // No need to evaluate aggreate filters if result already obtained.
            if (retVal != filter.ShortCircuitHint)
            {
                foreach (Filter f in later)
                {
                    retVal = DoProcessFilter(internalItem, f, tagHashCollection);
                    if (retVal == filter.ShortCircuitHint)
                        break;
                }
            }
            return retVal;
        }

        /// <summary>
        /// Does the process filter.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="tagHashCollection">The tag hash collection.</param>
        /// <returns><c>true</c> if item passes the filter; otherwise, <c>false</c></returns>
        private static bool DoProcessFilter(InternalItem internalItem, Filter filter, TagHashCollection tagHashCollection)
        {
            bool retVal = false;

            switch (filter.FilterType)
            {
                case FilterType.Condition:
                    retVal = ProcessCondition(internalItem, filter as Condition);
                    break;

                case FilterType.And:
                    retVal = ProcessAggregateFilter(internalItem, filter as AndFilter, tagHashCollection);
                    break;

                case FilterType.Or:
                    retVal = ProcessAggregateFilter(internalItem, filter as OrFilter, tagHashCollection);
                    break;
            }

            return retVal;
        }

        /// <summary>
        /// Processes the condition.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="condition">The condition.</param>
        /// <returns><c>true</c> if item passes the condition; otherwise, <c>false</c></returns>
        private static bool ProcessCondition(InternalItem internalItem, Condition condition)
        {
            if (condition.IsTag)
            {
                byte[] tagValue;
                internalItem.TryGetTagValue(condition.FieldName, out tagValue);
                return condition.Process(tagValue);
            }
            return condition.Process(internalItem.ItemId);
        }
    }
}