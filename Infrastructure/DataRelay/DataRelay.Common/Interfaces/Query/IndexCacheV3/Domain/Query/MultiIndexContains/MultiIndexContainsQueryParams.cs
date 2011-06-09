using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MultiIndexContainsQueryParams : IVersionSerializable
    {
        #region Ctors

        public MultiIndexContainsQueryParams()
        {
            Init(null, null);
        }

        internal MultiIndexContainsQueryParams(MultiIndexContainsQuery baseQuery)
        {
            Init(null, baseQuery);
        }

        private void Init(Filter filter, MultiIndexContainsQuery baseQuery)
        {
            this.filter = filter;
            BaseQuery = baseQuery;
        }

        #endregion

        #region Data Members

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

        internal MultiIndexContainsQuery BaseQuery { get; set; }

        private int primaryId = -1;
        public int PrimaryId
        {
            get
            {
                if (primaryId == -1 && BaseQuery != null)
                {
                    return BaseQuery.PrimaryId;
                }
                return count;
            }
            set
            {
                count = value;
            }
        }

        private int count = -1;
        public int Count
        {
            get
            {
                if (count == -1 && BaseQuery != null)
                {
                    return BaseQuery.Count;
                }
                return count;
            }
            set
            {
                count = value;
            }
        }

        private IndexCondition indexCondition;
        public IndexCondition IndexCondition
        {            
            get
            {
                if (indexCondition == null && BaseQuery != null)
                {
                    return BaseQuery.IndexCondition;
                }
                return indexCondition;
            }
            set
            {
                indexCondition = value;
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

                //Count
                writer.Write(Count);

                //IndexCondition
                if (IndexCondition == null)
                {
                    writer.Write(false);
                }
                else
                {
                    writer.Write(true);
                    Serializer.Serialize(writer.BaseStream, IndexCondition);
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

                if (version >= 2)
                {
                    //Count
                    Count = reader.ReadInt32();

                    //IndexCondition
                    if (reader.ReadBoolean())
                    {
                        IndexCondition = new IndexCondition();
                        Serializer.Deserialize(reader.BaseStream, IndexCondition);
                    }
                }
            }
        }

        private const int CURRENT_VERSION = 2;
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
