using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IndexIdParams : IVersionSerializable
    {      
        #region Ctors
        public IndexIdParams()
        {
            Init(-1, null, null);    
        }

        internal IndexIdParams(IIndexIdParam baseQuery)
        {
            Init(-1, null, baseQuery);
        }

        private void Init(int maxItems, Filter filter, IIndexIdParam baseQuery)
        {
            MaxItems = maxItems;
            Filter = filter;
            BaseQuery = baseQuery;
        }
        #endregion

        #region Data Members
        private int maxItems;
        public int MaxItems
        {
            get
            {
                if (maxItems <= 0 && BaseQuery != null)
                {
                    return BaseQuery.MaxItems;
                }
                return maxItems;
            }
            set
            {
                maxItems = value;
            }
        }

        private Filter filter;
        public Filter Filter
        {
            get
            {
                if (filter == null && BaseQuery != null)
                {
                    return BaseQuery.Filter;
                }
                return filter;
            }
            set
            {
                filter = value;
            }
        }

        internal IIndexIdParam BaseQuery
        {
            get;
            set;
        }
        #endregion

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //MaxItems
                writer.Write(maxItems);

                //Filter
                if (filter == null)
                {
                    writer.Write((byte) 0);
                }
                else
                {
                    writer.Write((byte) filter.FilterType);
                    Serializer.Serialize(writer.BaseStream, filter);
                }
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //MaxItems
                maxItems = reader.ReadInt32();

                //Filter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    FilterType filterType = (FilterType) b;
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
