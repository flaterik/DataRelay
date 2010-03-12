using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store
{
    internal static class CacheIndexInternalAdapter
    {
        //itemNum = 0 indicates get all items
        /// <summary>
        /// Gets the result item list.
        /// </summary>
        /// <param name="cacheIndexInternal">The cache index internal.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="itemNum">If 0 gets all items.</param>
        /// <returns>List of ResultItems</returns>
        internal static List<ResultItem> GetResultItemList(CacheIndexInternal cacheIndexInternal, int offset, int itemNum)
        {
            if (itemNum == Int32.MaxValue)
            {
                itemNum = cacheIndexInternal.Count;
            }

            List<ResultItem> resultItemList = new List<ResultItem>(itemNum);

            if (cacheIndexInternal.Count >= offset)
            {
                for (int i = offset - 1; i < cacheIndexInternal.Count && resultItemList.Count < itemNum; i++)
                {
                    resultItemList.Add(new ResultItem(cacheIndexInternal.InDeserializationContext.IndexId,
                         cacheIndexInternal.GetItem(i).ItemId,
                        null,
                        InternalItemAdapter.ConvertToTagDictionary(cacheIndexInternal.GetItem(i).TagList, cacheIndexInternal.InDeserializationContext)));
                }
            }
            return resultItemList;
        }

        /// <summary>
        /// Gets the index data item list.
        /// </summary>
        /// <param name="cacheIndexInternal">The cache index internal.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="itemNum">If 0 gets all items.</param>
        /// <returns>List of IndexDataItems</returns>
        internal static List<IndexDataItem> GetIndexDataItemList(CacheIndexInternal cacheIndexInternal, int offset, int itemNum)
        {
            if (itemNum == Int32.MaxValue)
            {
                itemNum = cacheIndexInternal.Count;
            }

            List<IndexDataItem> resultItemList = new List<IndexDataItem>(itemNum);

            if (cacheIndexInternal.Count >= offset)
            {
                for (int i = offset - 1; i < cacheIndexInternal.Count && resultItemList.Count < itemNum; i++)
                {
                    resultItemList.Add(new IndexDataItem(cacheIndexInternal.GetItem(i).ItemId,
                        InternalItemAdapter.ConvertToTagDictionary(cacheIndexInternal.GetItem(i).TagList, cacheIndexInternal.InDeserializationContext)));
                }
            }
            return resultItemList;
        }

        /// <summary>
        /// Gets the result item list.
        /// </summary>
        /// <param name="cacheIndexInternal">The cache index internal.</param>
        /// <param name="itemPositionList">The item position list.</param>
        /// <returns>List of ResultItems</returns>
        internal static List<ResultItem> GetResultItemList(CacheIndexInternal cacheIndexInternal, IEnumerable<int> itemPositionList)
        {
            List<ResultItem> resultItemList = new List<ResultItem>();

            foreach (int itemPosition in itemPositionList)
            {
                resultItemList.Add(new ResultItem(cacheIndexInternal.InDeserializationContext.IndexId,
                    cacheIndexInternal.GetItem(itemPosition).ItemId,
                    null,
                    InternalItemAdapter.ConvertToTagDictionary(cacheIndexInternal.GetItem(itemPosition).TagList, cacheIndexInternal.InDeserializationContext)));
            }

            return resultItemList;
        }
    }
}
