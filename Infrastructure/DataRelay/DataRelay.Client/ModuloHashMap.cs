using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace MySpace.DataRelay.Client
{
    internal class ModuloHashMap<T> : IHashMap<int, T> where T : IComparable<T>
    {
        private static readonly List<T> _emptyList = new List<T>(0);
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(500);
        private readonly Dictionary<int, Entry<T>> _map = new Dictionary<int, Entry<T>>();
        private int _modulo = 1;

        /// <summary>
        /// Gets or sets the modulo.
        /// </summary>
        /// <value>The modulo.</value>
        public int Modulo 
        {   get { return _modulo; }
            set
            {
                if(value<=0) throw new ArgumentOutOfRangeException("value", "Modulo must be a positive integer value.");
                _modulo = value;
            }
        }

        /// <summary>
        /// Adds an item for the the specified hash point.
        /// </summary>
        /// <param name="hashPoint">The hash point.</param>
        /// <param name="item">The item.</param>
        public void Add(int hashPoint, T item)
        {
            lock (_map)
            {
                Entry<T> entry;
                if (_map.TryGetValue(hashPoint, out entry))
                {
                    entry.Add(item);
                }
                else
                {
                    _map[hashPoint] = new Entry<T>(item);
                }
            }
        }

        /// <summary>
        /// Selects the items that in in the hash range for the key supplied.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>A list of items.  The list may be empty if there are no items available for the has point.</returns>
        public IEnumerable<T> Select(int key)
        {
            lock(_map)
            {
                Entry<T> entry;
                if(_map.TryGetValue(key % _modulo, out entry))
                {
                    return entry.GetList(_refreshInterval);
                }
            }
            return _emptyList;
        }

        /// <summary>
        /// Shuffles all entries in the map.  This is used after initialization to prevent all clients trying the same node first, based on
        /// order listed in config.
        /// </summary>
        public void Shuffle()
        {
            lock(_map)
            {
                foreach(var entry in _map)
                {
                    entry.Value.Shuffle();
                }
            }
        }

        private class Entry<TItem> where TItem : IComparable<TItem>
        {
            private readonly Stopwatch _stopwatch = new Stopwatch();
            private List<TItem> _list;

            public Entry(TItem firstitem)
            {
                Add(firstitem);
            }

            public void Add(TItem item)
            {
                _list = _list != null ? new List<TItem>(_list) : new List<TItem>();
                _list.Add(item);
                _list.Sort();
                _stopwatch.Restart();
            }

            public IEnumerable<TItem> GetList(TimeSpan refreshInterval)
            {
                if (_stopwatch.Elapsed >= refreshInterval) Sort();
                return _list;
            }

            private void Sort()
            {
                _list = new List<TItem>(_list);
                _list.Sort();
                _stopwatch.Restart();
            }

            public void Shuffle()
            {
                var rand = new Random(Guid.NewGuid().GetHashCode());
                var shuffled = new List<TItem>(_list.Count);
                shuffled.AddRange( _list.OrderBy( item => rand.Next()) );
                _list = shuffled;
            }
        }
    }
}