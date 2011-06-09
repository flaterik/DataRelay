using System;
using System.Collections.Generic;

namespace MySpace.DataRelay.Client
{
    internal interface IHashMap<in THash, TItem> where TItem : IComparable<TItem>
    {
        void Add(THash hashPoint, TItem item);
        IEnumerable<TItem> Select(THash key);
    }
}