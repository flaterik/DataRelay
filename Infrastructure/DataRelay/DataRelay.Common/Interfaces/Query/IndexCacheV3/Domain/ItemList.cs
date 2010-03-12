using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Interfaces.Query.IndexCacheV3
{   
    public abstract class ItemList<T> where T :IItem
    {
        /// <summary>
        /// Gets the item at the specified pos.
        /// </summary>
        /// <param name="pos">The pos.</param>
        /// <returns></returns>
        public abstract T GetItem(int pos);

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The count.</value>
        public abstract int Count
        {
            get;
        }

        /// <summary>
        /// Removes item at the specified pos.
        /// </summary>
        /// <param name="pos">The pos.</param>
        public abstract void RemoveAt(int pos);

        /// <summary>
        /// Removes the range of items from the start pos to the end pos.
        /// </summary>
        /// <param name="startPos">The start pos.</param>
        /// <param name="count">The count.</param>
        public abstract void RemoveRange(int startPos, int count);

        /// <summary>
        /// Binary searches item.
        /// </summary>
        /// <param name="searchItem">The search item.</param>
        /// <param name="isTagPrimarySort">if set to <c>true</c> [is tag primary sort].</param>
        /// <param name="sortFieldName">Name of the sort field.</param>
        /// <param name="sortOrderList">The sort order list.</param>
        /// <param name="localIdentityTagNames">The local identity tag names.</param>
        /// <returns></returns>
        public abstract int BinarySearchItem(T searchItem, 
            bool isTagPrimarySort, 
            string sortFieldName,
            List<SortOrder> sortOrderList,
            List<string> localIdentityTagNames);
       
        public bool EqualsLocalId(T item1, T item2, List<string> localIdentityTagNames)
        {
            return IndexCacheUtils.EqualsLocalId(item1, item2, localIdentityTagNames);
        }
    }
}