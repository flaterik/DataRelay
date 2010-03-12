using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Interfaces.Query.IndexCacheV3
{
    internal static class MergeAlgo
    {
        internal static void MergeItemLists(ref List<ResultItem> list1, List<ResultItem> list2, int maxMergeCount, BaseComparer baseComparer)
        {
            int mergedListCount = list1.Count + list2.Count;
            int count1 = 0;
            int count2 = 0;
            if (mergedListCount > maxMergeCount)
            {
                mergedListCount = maxMergeCount;
            }
            List<ResultItem> newList = new List<ResultItem>(mergedListCount);

            #region Merge until one list ends
            for (int i = 0; i < mergedListCount && count1 != list1.Count && count2 != list2.Count; i++)
            {
                newList.Add((baseComparer.Compare(list1[count1], list2[count2]) <= 0) ?
                                    list1[count1++] : // list1 item is greater
                                    list2[count2++]); // list2 item is greater
            }
            #endregion

            #region Append rest of the list1/list2 to newList
            if (count1 != list1.Count && newList.Count < mergedListCount)
            {
                int count = list1.Count - count1;
                for (int i = 0; i < count && newList.Count < mergedListCount; i++)
                {
                    newList.Add(list1[count1++]);
                }
            }
            else if (count2 != list2.Count && newList.Count < mergedListCount)
            {
                int count = list2.Count - count2;
                for (int i = 0; i < count && newList.Count < mergedListCount; i++)
                {
                    newList.Add(list2[count2++]);
                }
            }
            #endregion

            #region Update reference
            list1 = newList;
            #endregion
        }
    }
}
