using System.Collections;
using System.Collections.Generic;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class SortedResultItemBagList : IEnumerable
    {
        #region Data Members

        private List<ResultItemBag> SortedBag { get; set; }
        private BaseComparer BaseComparer { get; set; }

        #endregion

        #region Ctor

        public SortedResultItemBagList()
        {
        }

        public SortedResultItemBagList(BaseComparer baseComparer)
        {
            BaseComparer = baseComparer;
            SortedBag = new List<ResultItemBag>();
        }

        #endregion

        #region Methods

        public ResultItemBag this[int index]
        {
            get
            {
                return SortedBag[index];
            }
        }

        public void Add(ResultItemBag resultItemBag)
        {
            // add based on sort order
            int pos = SortedBag.BinarySearch(resultItemBag, BaseComparer);
            if (pos < 0)
            {
                pos = ~pos;
            }

            SortedBag.Insert(pos, resultItemBag);  
        }

        public void Remove(ResultItemBag resultItemBag)
        {
            // remove based on obj reference
            for (int i = 0; i < SortedBag.Count; i++)
            {
                if (SortedBag[i] == resultItemBag)
                {
                    SortedBag.RemoveAt(i);
                    break;
                }
            }
        }

        public ResultItemBag First
        {
            get { return SortedBag.Count > 0 ? SortedBag[0] : null; }
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return SortedBag.GetEnumerator();
        }

        #endregion
    }
}