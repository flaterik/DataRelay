using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Interfaces.Query.IndexCacheV3.Domain.Query.Intersection
{
    internal static class IntersectionAlgo
    {
        internal static void Intersect<T>(bool isTagPrimarySort,
            string sortFieldName,
            List<string> localIdentityTagNames,
            List<SortOrder> sortOrderList,
            ItemList<T> resultList,
            ItemList<T> currentList,
            int maxResultItems,
            bool isLastIntersection) where T : IItem
        {
            int i;
            if ((localIdentityTagNames == null || localIdentityTagNames.Count < 1) && !isTagPrimarySort)
            {
                // Traverse both CacheIndexInternal simultaneously
                int j;
                BaseComparer comparer = new BaseComparer(isTagPrimarySort, sortFieldName, sortOrderList);
                for (i = 0, j = 0; i < resultList.Count && j < currentList.Count &&
                    /* Check if resultList has accumalated enough items */ !(maxResultItems > 0 && isLastIntersection && i == maxResultItems); )
                {
                    int retVal = comparer.Compare(resultList.GetItem(i), currentList.GetItem(j));

                    if (retVal == 0)
                    {
                        //Items equal. Move pointers to both lists
                        i++;
                        j++;
                    }
                    else
                    {
                        if (retVal > 0) // resultList item is greater and skip currentList item
                        {
                            j++;
                        }
                        else // currentList item is greater and remove resultList item
                        {
                            resultList.RemoveAt(i);
                        }
                    }
                }
            }
            else
            {
                // Assign smaller list to resultList
                if (resultList.Count > currentList.Count)
                {
                    //Swap resultList and currentList
                    ItemList<T> tempList = resultList;
                    resultList = currentList;
                    currentList = tempList;
                }

                for (i = 0; i < resultList.Count && !(maxResultItems > 0 && i == maxResultItems); )
                {
                    if (currentList.BinarySearchItem(resultList.GetItem(i), isTagPrimarySort, sortFieldName, sortOrderList, localIdentityTagNames) < 0)
                    {
                        //Remove item from resultList
                        resultList.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            //Get rid of uninspected items in resultList
            if (i < resultList.Count)
            {
                resultList.RemoveRange(i, resultList.Count - i);
            }
        }
    }
}