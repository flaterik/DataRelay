using MySpace.Common.IO;
using MySpace.Common;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class RandomQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        #region Data Members
        private byte[] indexId;
        public byte[] IndexId
        {
            get
            {
                return indexId;
            }
            set
            {
                indexId = value;
            }
        }

        private int count;
        public int Count
        {
            get
            {
                return count;
            }
            set
            {
                count = value;
            }
        }

        private string targetIndexName;
        public string TargetIndexName
        {
            get
            {
                return targetIndexName;
            }
            set
            {
                targetIndexName = value;
            }
        }

        private bool excludeData;
        public bool ExcludeData
        {
            get
            {
                return excludeData;
            }
            set
            {
                excludeData = value;
            }
        }

        private bool getMetadata;
        public bool GetMetadata
        {
            get
            {
                return getMetadata;
            }
            set
            {
                getMetadata = value;
            }
        }

        private Filter filter;
        public Filter Filter
        {
            get
            {
                return filter;
            }
            set
            {
                filter = value;
            }
        }

        private FullDataIdInfo fullDataIdInfo;
        public FullDataIdInfo FullDataIdInfo
        {
            get
            {
                return fullDataIdInfo;
            }
            set
            {
                fullDataIdInfo = value;
            }
        }
        #endregion

        #region Ctors
        public RandomQuery()
        {
            Init(null, -1, null, false, false, null, null);
        }

        public RandomQuery(byte[] indexId, int count, string targetIndexName)
        {
            Init(indexId, count, targetIndexName, false, false, null, null);
        }

        public RandomQuery(byte[] indexId, int count, string targetIndexName, bool excludeData, bool getMetadata, Filter filter)
        {
            Init(indexId, count, targetIndexName, excludeData, getMetadata, filter, null);
        }

        private void Init(byte[] indexId, int count, string targetIndexName, bool excludeData, bool getMetadata, Filter filter, FullDataIdInfo fullDataIdInfo)
        {
            this.indexId = indexId;
            this.count = count;
            this.targetIndexName = targetIndexName;
            this.excludeData = excludeData;
            this.getMetadata = getMetadata;
            this.filter = filter;
            this.fullDataIdInfo = fullDataIdInfo;
        }
        #endregion

        #region IRelayMessageQuery Members
        public byte QueryId
        {
            get
            {
                return (byte)QueryTypes.RandomQuery;
            }
        }
        #endregion

        #region IPrimaryQueryId Members
        private int primaryId;
        public int PrimaryId
        {
            get
            {
                return primaryId > 0 ? primaryId : IndexCacheUtils.GeneratePrimaryId(indexId);
            }
            set
            {
                primaryId = value;
            }
        }
        #endregion

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //IndexId
                if (indexId == null || indexId.Length == 0)
                {
                    writer.Write((ushort) 0);
                }
                else
                {
                    writer.Write((ushort) indexId.Length);
                    writer.Write(indexId);
                }

                //Count
                writer.Write(count);

                //TargetIndexName
                writer.Write(targetIndexName);

                //ExcludeData
                writer.Write(excludeData);

                //GetMetadata
                writer.Write(getMetadata);

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

                //FullDataIdInfo
                Serializer.Serialize(writer.BaseStream, fullDataIdInfo);
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //IndexId
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    indexId = reader.ReadBytes(len);
                }

                //Count
                count = reader.ReadInt32();

                //TargetIndexName
                targetIndexName = reader.ReadString();

                //ExcludeData
                excludeData = reader.ReadBoolean();

                //GetMetadata
                getMetadata = reader.ReadBoolean();

                //Filter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    FilterType filterType = (FilterType) b;
                    filter = FilterFactory.CreateFilter(reader, filterType);
                }

                if (version >= 2)
                {
                    //FullDataIdInfo
                    fullDataIdInfo = new FullDataIdInfo();
                    Serializer.Deserialize(reader.BaseStream, fullDataIdInfo);
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
