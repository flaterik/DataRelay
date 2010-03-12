using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.Logging;

namespace MySpace.DataRelay.Interfaces.Query.IndexCacheV3
{
    public class BaseComparer : IComparer<IItem>, IComparer<byte[]>
    {
        public readonly bool IsTagPrimarySort;
        public readonly string SortFieldName;
        public readonly List<SortOrder> SortOrderList;

        private static readonly LogWrapper Log = new LogWrapper();
        private int startIndex1;
        private int startIndex2;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseComparer"/> class.
        /// </summary>
        /// <param name="isTagPrimarySort">if set to <c>true</c> indicates that sort field is a tag.</param>
        /// <param name="sortFieldName">Name of the sort field.</param>
        /// <param name="sortOrderList">The sort order list.</param>
        public BaseComparer(bool isTagPrimarySort, string sortFieldName, List<SortOrder> sortOrderList)
        {
            IsTagPrimarySort = isTagPrimarySort;
            SortFieldName = sortFieldName;
            SortOrderList = sortOrderList;
        }

        /// <summary>
        /// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
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
        public int Compare(IItem x, IItem y)
        {
            byte[] val1, val2;
            if (IsTagPrimarySort)
            {
                x.TryGetTagValue(SortFieldName, out val1);
                y.TryGetTagValue(SortFieldName, out val2);
            }
            else
            {
                val1 = x.ItemId;
                val2 = y.ItemId;
            }
            return Compare(val1, val2);
        }

        /// <summary>
        /// Compares the specified arr1 to arr2.
        /// </summary>
        /// <param name="arr1">The arr1.</param>
        /// <param name="arr2">The arr2.</param>
        /// <returns></returns>
        public int Compare(byte[] arr1, byte[] arr2)
        {
            if (SortOrderList == null || SortOrderList.Count < 1)
            {
                if (Log.IsErrorEnabled)
                    Log.Error("Empty SortOrderList in BaseComparer");
                throw new Exception("Empty SortOrderList in BaseComparer");
            }

            #region Null check for arrays

            if (arr1 == null || arr2 == null)
            {
                if (arr1 == null && arr2 == null)   //Both arrays are null
                {
                    return 0;
                }

                if (SortOrderList[0].SortBy == SortBy.ASC)   //One of the arrays is null and order is ASC
                {
                    if (arr1 == null)
                    {
                        return -1;
                    }
                    return 1;
                }

                if (arr1 == null)    //One of the arrays is null and order is DESC
                {
                    return 1;
                }
                return -1;
            }

            #endregion

            #region Length check for arrays

            if (arr1.Length != arr2.Length)
            {
                if (arr1.Length > arr2.Length)
                {
                    return 1;
                }
                return -1;
            }

            #endregion

            int retVal = 0;
            DataType dataType;
            startIndex1 = startIndex2 = 0;
            for (int i = 0; i < SortOrderList.Count && retVal == 0; i++)
            {
                dataType = SortOrderList[i].DataType;
                retVal = (SortOrderList[i].SortBy == SortBy.ASC) ?
                    ByteArrayComparerUtil.CompareByteArrayBasedOnDataType(arr1, arr2, ref startIndex1, ref startIndex2, 0, 0, dataType) :
                    ByteArrayComparerUtil.CompareByteArrayBasedOnDataType(arr2, arr1, ref startIndex2, ref startIndex1, 0, 0, dataType);
            }
            return retVal;
        }
    }
}