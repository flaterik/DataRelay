using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public abstract class AggregateFilter : Filter
    {
        #region Data Members
        private int totalCount;

        protected List<Filter> FilterList
        {
            get;
            set;
        }

        #endregion

        #region Ctors
        private AggregateFilter()
        {
        }

        protected AggregateFilter(params Filter[] filters)
        {
            if (filters == null)
            {
                FilterList = new List<Filter>();
                return;
            }

            FilterList = new List<Filter>(filters.Length);
            foreach (Filter filter in filters)
            {
                if (filter != null)
                {
                    FilterList.Add(filter);
                }
            }
        }

        #endregion

        #region Methods
        internal override int FilterCount
        {
            get
            {
                return totalCount + 1;
            }
        }

        public override string ToString()
        {
            var stb = new StringBuilder();
            stb.Append("(");
            for (int i = 0; i < FilterList.Count; i++)
            {
                stb.Append(FilterList[i].ToString());
                if (i < FilterList.Count - 1)
                {
                    stb.Append(" ").Append(FilterType).Append(" ");
                }
            }
            stb.Append(")");
            return stb.ToString();
        }

        internal abstract bool ShortCircuitHint
        {
            get;
        }

        public Filter this[int index]
        {
            get
            {
                return FilterList[index];
            }
        }

        public int Count
        {
            get
            {
                return FilterList.Count;
            }
        }
        #endregion

        #region IVersionSerializable Members
        private const int CURRENT_VERSION = 1;
        public override int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        public override bool Volatile
        {
            get
            {
                return false;
            }
        }

        public override void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                writer.Write((ushort)FilterList.Count);

                foreach (Filter filter in FilterList)
                {
                    writer.Write((byte)filter.FilterType);
                    Serializer.Serialize(writer.BaseStream, filter);
                }
            }
        }

        public override void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                FilterType filterType;
                ushort count = reader.ReadUInt16();
                FilterList = new List<Filter>(count);
                Filter childFilter;
                for (ushort i = 0; i < count; i++)
                {
                    filterType = (FilterType)reader.ReadByte();
                    childFilter = FilterFactory.CreateFilter(reader, filterType);
                    FilterList.Add(childFilter);
                    totalCount += childFilter.FilterCount;
                }
            }
        }
        #endregion
    }
}
