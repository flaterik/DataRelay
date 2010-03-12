using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    internal class InternalItemComparer : IComparer<InternalItem>
    {
        private readonly BaseComparer comparer;

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalItemComparer"/> class.
        /// </summary>
        /// <param name="isTagPrimarySort">if set to <c>true</c> [is tag primary sort].</param>
        /// <param name="sortFieldName">Name of the sort field.</param>
        /// <param name="sortOrderList">The sort order list.</param>
        internal InternalItemComparer(bool isTagPrimarySort, string sortFieldName, List<SortOrder> sortOrderList)
        {
            comparer = new BaseComparer(isTagPrimarySort, sortFieldName, sortOrderList);
        }
        
        #region IComparer<InternalItem> Members

        /// <summary>
        /// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <param name="x">The InternalItem to compare.</param>
        /// <param name="y">The InternalItem to compare.</param>
        /// <returns>
        /// Value
        /// Condition
        /// Less than zero
        /// <paramref name="x"/> is less than <paramref name="y"/>.
        /// Zero
        /// <paramref name="x"/> equals <paramref name="y"/>.
        /// Greater than zero
        /// <paramref name="x"/> is greater than <paramref name="y"/>.
        /// </returns>
        public int Compare(InternalItem x, InternalItem y)
        {
            return comparer.Compare(x, y);
        }

        #endregion
    }
}
