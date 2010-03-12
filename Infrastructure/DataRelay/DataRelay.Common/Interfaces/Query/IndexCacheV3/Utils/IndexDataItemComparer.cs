using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Interfaces.Query.IndexCacheV3
{
    internal class IndexDataItemComparer : IComparer<IndexDataItem>
    {
        private readonly BaseComparer comparer;
        internal IndexDataItemComparer(bool isTagPrimarySort, string sortFieldName, List<SortOrder> sortOrderList)
        {
            comparer = new BaseComparer(isTagPrimarySort, sortFieldName, sortOrderList);
        }

        #region IComparer<IndexDataItem> Members
        public int Compare(IndexDataItem x, IndexDataItem y)
        {
            return comparer.Compare(x, y);
        }
        #endregion
    }
}
