using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IntersectionQueryParams : IVersionSerializable
    {
        #region Ctors
        public IntersectionQueryParams()
        {
            Init(null, null);
        }

        internal IntersectionQueryParams(IntersectionQuery baseQuery)
        {
            Init(null, baseQuery);
        }

        private void Init(Filter filter, IntersectionQuery baseQuery)
        {
            this.filter = filter;
            this.baseQuery = baseQuery;
        }
        #endregion

        #region Data Members
        private Filter filter;
        public Filter Filter
        {
            get
            {
                if (filter == null && baseQuery != null)
                {
                    return baseQuery.Filter;
                }
                return filter;
            }
            set
            {
                filter = value;
            }
        }

        private IntersectionQuery baseQuery;
        internal IntersectionQuery BaseQuery
        {
            get
            {
                return baseQuery;
            }
            set
            {
                baseQuery = value;
            }
        }
        #endregion

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //Filter
                if (filter == null)
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)filter.FilterType);
                    Serializer.Serialize(writer.BaseStream, filter);
                }
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //Filter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    FilterType filterType = (FilterType)b;
                    filter = FilterFactory.CreateFilter(reader, filterType);
                }
            }
        }

        private const int CURRENT_VERSION = 1;
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        public bool Volatile
        {
            get
            {
                return false;
            }
        }
        #endregion

        #region ICustomSerializable Members

        public void Deserialize(IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion
    }
}
