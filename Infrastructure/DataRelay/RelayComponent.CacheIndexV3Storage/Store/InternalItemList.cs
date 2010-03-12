using System;
using System.Collections;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store
{
    internal class InternalItemList : ItemList<InternalItem>, IEnumerable
    {
        #region Data Members

        private readonly List<InternalItem> itemList = new List<InternalItem>();

        #endregion

        #region Methods

        /// <summary>
        /// Gets or sets the <see cref="MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store.InternalItem"/> with the specified pos.
        /// </summary>
        /// <value></value>
        internal InternalItem this[int pos]
        {
            get
            {
                return itemList[pos];
            }
            set
            {
                itemList[pos] = value;
            }
        }

        /// <summary>
        /// Adds the specified internal item.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        internal void Add(InternalItem internalItem)
        {
            itemList.Add(internalItem);
        }

        /// <summary>
        /// Inserts the internalItem at the specified pos.
        /// </summary>
        /// <param name="pos">The pos.</param>
        /// <param name="internalItem">The internal item.</param>
        public void Insert(int pos, InternalItem internalItem)
        {
            itemList.Insert(pos, internalItem);
        }

        /// <summary>
        /// Linear searches the searchItem within the InternalItemList
        /// </summary>
        /// <param name="searchItem">The search item.</param>
        /// <param name="localIdentityTagNames">The local identity tag names.</param>
        /// <returns></returns>
        internal int LinearSearch(InternalItem searchItem, List<string> localIdentityTagNames)
        {
            try
            {
                for (int i = 0; i < Count; i++)
                {
                    if (ByteArrayComparerUtil.CompareByteArrays(this[i].ItemId, searchItem.ItemId))
                    {
                        if (EqualsLocalId(this[i], searchItem, localIdentityTagNames))
                        {
                            return i;
                        }
                    }
                }
                return -1;
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Linear Search.", ex);
            }
        }

        /// <summary>
        /// Gets the insert position.
        /// </summary>
        /// <param name="searchItem">The search item.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="sortby">The sortby.</param>
        /// <returns></returns>
        internal int GetInsertPosition(InternalItem searchItem, IComparer<InternalItem> comparer, SortBy sortby)
        {
            try
            {
                int searchIndex = -1;
                if (Count > 0)
                {
                    searchIndex = itemList.BinarySearch(searchItem, comparer);
                    if (searchIndex > -1)
                    {
                        if (sortby == SortBy.DESC)
                        {
                            //Search to left of searchIndex
                            searchIndex--;
                            while (searchIndex > -1 && comparer.Compare(this[searchIndex], searchItem) == 0)
                            {
                                //Keep moving left until there is a match
                                searchIndex--;
                            }
                            searchIndex++;
                        }
                        else
                        {
                            //Search to right of searchIndex
                            searchIndex++;
                            while (searchIndex < Count && comparer.Compare(this[searchIndex], searchItem) == 0)
                            {
                                //Keep moving right until there is a match
                                searchIndex++;
                            }
                        }
                    }
                }
                if (searchIndex < 0)
                {
                    //Note: Return value of List.BinarySearch is -
                    //The zero-based index of item in the sorted List, if item is found; 
                    //otherwise, a negative number that is the bitwise complement of the index 
                    //of the next element that is larger than item or, if there is no larger element, 
                    //the bitwise complement of List.Count. 
                    searchIndex = ~searchIndex;
                }
                return searchIndex;
            }
            catch
            {
                LoggingUtil.Log.Error("Error while getting insert position");
                throw new Exception("Error while getting insert position");
            }
        }

        /// <summary>
        /// Sorts the InternalItemList based on the specified tag sort.
        /// </summary>
        /// <param name="tagSort">The tag sort.</param>
        internal void Sort(TagSort tagSort)
        {
            InternalItemComparer internalItemComparer = new InternalItemComparer(tagSort.IsTag, tagSort.TagName, new List<SortOrder>(1) { tagSort.SortOrder });
            itemList.Sort(internalItemComparer);
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator GetEnumerator()
        {
            return (itemList as IEnumerable).GetEnumerator();
        }

        #endregion

        #region ItemList Members

        /// <summary>
        /// Binary searches an item.
        /// </summary>
        /// <param name="searchItem">The search item.</param>
        /// <param name="isTagPrimarySort">if set to <c>true</c> [is tag primary sort].</param>
        /// <param name="sortFieldName">Name of the sort field.</param>
        /// <param name="sortOrderList">The sort order list.</param>
        /// <param name="localIdentityTagNames">The local identity tag names.</param>
        /// <returns></returns>
        public override int BinarySearchItem(InternalItem searchItem, 
            bool isTagPrimarySort, 
            string sortFieldName, 
            List<SortOrder> sortOrderList, 
            List<string> localIdentityTagNames)
        {
            try
            {
                int searchIndex = -1;
                if (Count > 0)
                {
                    InternalItemComparer comparer = new InternalItemComparer(isTagPrimarySort, sortFieldName, sortOrderList);
                    searchIndex = itemList.BinarySearch(searchItem, comparer);

                    //Look for localIdentity at searchIndex
                    if (searchIndex > -1 && localIdentityTagNames.Count > 0)
                    {
                        if (EqualsLocalId(this[searchIndex], searchItem, localIdentityTagNames))
                        {
                            return searchIndex;
                        }

                        //Search to left of the searchIndex
                        int newIndex = searchIndex - 1;
                        while (newIndex > -1 && comparer.Compare(this[newIndex], searchItem) == 0)
                        {
                            if (EqualsLocalId(this[newIndex], searchItem, localIdentityTagNames))
                            {
                                return newIndex;
                            }
                            newIndex--;
                        }

                        //Search to right of index
                        newIndex = searchIndex + 1;
                        while (newIndex < Count && comparer.Compare(this[newIndex], searchItem) == 0)
                        {
                            if (EqualsLocalId(this[newIndex], searchItem, localIdentityTagNames))
                            {
                                return newIndex;
                            }
                            newIndex++;
                        }
                        return -1;
                    }
                }
                return searchIndex;
            }
            catch (Exception ex)
            {                
                throw new Exception("Error in Binary Search", ex);
            }

        }

        /// <summary>
        /// Gets the item at the specified pos.
        /// </summary>
        /// <param name="pos">The pos.</param>
        /// <returns></returns>
        public override InternalItem GetItem(int pos)
        {
            if (Count > pos)
            {
                return this[pos];
            }
            return null;
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The count.</value>
        public override int Count
        {
            get
            {
                return itemList.Count;
            }
        }

        /// <summary>
        /// Removes item at the specified pos.
        /// </summary>
        /// <param name="pos">The pos.</param>
        public override void RemoveAt(int pos)
        {
            itemList.RemoveAt(pos);
        }

        /// <summary>
        /// Removes the range of items from the start pos to the end pos.
        /// </summary>
        /// <param name="startPos">The start pos.</param>
        /// <param name="count">The count.</param>
        public override void RemoveRange(int startPos, int count)
        {
            itemList.RemoveRange(startPos, count);
        }

        #endregion
    }
}
