using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Interfaces.Query.IndexCacheV3.Domain.Query.Intersection
{
    internal static class IntersectionAlgo
    {
        internal static void Intersect<T>(
            bool isTagPrimarySort,
            string sortFieldName,
            List<string> localIdentityTagNames,
            List<SortOrder> sortOrderList,
            ItemList<T> resultList,
            ItemList<T> currentList) where T:IItem
        {
            if (localIdentityTagNames == null || localIdentityTagNames.Count < 1)
            {
                // Traverse both CacheIndexInternal simultaneously
                int i, j;
                BaseComparer comparer = new BaseComparer(isTagPrimarySort, sortFieldName, sortOrderList);
                for (i = resultList.Count - 1, j = currentList.Count - 1; i > -1 && j > -1; )
                {
                    int retVal = comparer.Compare(resultList.GetItem(i), currentList.GetItem(j));
                 
                    if (retVal == 0)
                    {
                        //Items equal. Move pointers to both lists
                        i--;
                        j--;
                    }
                    else
                    {
                        if (retVal < 0) // resultList item is greater
                        {
                            j--;
                        }
                        else
                        {
                            resultList.RemoveAt(i);
                            i--;
                        }
                    }
                }
                //Get rid of uninspected items in resultList
                if (i > -1)
                {
                    resultList.RemoveRange(0, i + 1);
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
                for (int i = resultList.Count - 1; i > -1; i--)
                {
                    if (currentList.BinarySearchItem(resultList.GetItem(i), isTagPrimarySort, sortFieldName, sortOrderList, localIdentityTagNames) < 0)
                    {
                        //Remove item from resultList
                        resultList.RemoveAt(i);
                    }
                }
            }
        }    
    }
}
