using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Interfaces.Query.IndexCacheV3
{
    internal static class MergeAlgo
    {
        internal static void MergeItemLists(ref List<ResultItem> list1,
            List<ResultItem> list2,
            int maxMergeCount,
            BaseComparer baseComparer)
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

        internal static void MergeGroupResult(ref GroupByResult groupByResult1,
            GroupByResult groupByResult2,
            int maxMergeCount,
            BaseComparer baseComparer)
        {
            int mergedResultCount = groupByResult1.Count + groupByResult2.Count;
            int count1 = 0;
            int count2 = 0;
            if (mergedResultCount > maxMergeCount)
            {
                mergedResultCount = maxMergeCount;
            }
            GroupByResult newGroupResult = new GroupByResult(baseComparer);
            ResultItemBag resultItemBag;

            #region Merge until one GroupByResult ends

            for (int i = 0; newGroupResult.Count < mergedResultCount && count1 != groupByResult1.Count && count2 != groupByResult2.Count; i++)
            {
                resultItemBag = baseComparer.Compare(groupByResult1[count1], groupByResult2[count2]) <= 0 ? groupByResult1[count1++] : groupByResult2[count2++];
                newGroupResult.Add(resultItemBag.CompositeKey, resultItemBag);
            }

            #endregion

            #region Append rest of the groupByResult1/groupByResult2 to newGroupResult

            if (count1 != groupByResult1.Count && newGroupResult.Count < mergedResultCount)
            {
                int count = groupByResult1.Count - count1;
                for (int i = 0; i < count && newGroupResult.Count < mergedResultCount; i++)
                {
                    newGroupResult.Add(groupByResult1[count1].CompositeKey, groupByResult1[count1]);
                    count1++;
                }
            }
            else if (count2 != groupByResult2.Count && newGroupResult.Count < mergedResultCount)
            {
                int count = groupByResult2.Count - count2;
                for (int i = 0; i < count && newGroupResult.Count < mergedResultCount; i++)
                {
                    newGroupResult.Add(groupByResult2[count2].CompositeKey, groupByResult2[count2]);
                    count2++;
                }
            }

            #endregion

            #region Update reference

            groupByResult1 = newGroupResult;

            #endregion
        }
    }
}
