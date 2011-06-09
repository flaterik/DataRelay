using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class RandomQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        #region Data Members

        public byte[] IndexId { get; set; }

        public int Count { get; set; }

        public string TargetIndexName { get; set; }

        public bool ExcludeData { get; set; }

        public bool GetMetadata { get; set; }

        public Filter Filter { get; set; }

        public FullDataIdInfo FullDataIdInfo { get; set; }

        public IndexCondition IndexCondition { get; set; }

        public DomainSpecificProcessingType DomainSpecificProcessingType { get; set; }

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
            IndexId = indexId;
            Count = count;
            TargetIndexName = targetIndexName;
            ExcludeData = excludeData;
            GetMetadata = getMetadata;
            Filter = filter;
            FullDataIdInfo = fullDataIdInfo;
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
                return primaryId > 0 ? primaryId : IndexCacheUtils.GeneratePrimaryId(IndexId);
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
                if (IndexId == null || IndexId.Length == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)IndexId.Length);
                    writer.Write(IndexId);
                }

                //Count
                writer.Write(Count);

                //TargetIndexName
                writer.Write(TargetIndexName);

                //ExcludeData
                writer.Write(ExcludeData);

                //GetMetadata
                writer.Write(GetMetadata);

                //Filter
                if (Filter == null)
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)Filter.FilterType);
                    Serializer.Serialize(writer.BaseStream, Filter);
                }

                //FullDataIdInfo
                if (FullDataIdInfo == null)
                {
                    writer.Write(false);
                }
                else
                {
                    writer.Write(true);
                    Serializer.Serialize(writer.BaseStream, FullDataIdInfo);
                }

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

                //DomainSpecificProcessingType
                writer.Write((byte)DomainSpecificProcessingType);
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
                    IndexId = reader.ReadBytes(len);
                }

                //Count
                Count = reader.ReadInt32();

                //TargetIndexName
                TargetIndexName = reader.ReadString();

                //ExcludeData
                ExcludeData = reader.ReadBoolean();

                //GetMetadata
                GetMetadata = reader.ReadBoolean();

                //Filter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    FilterType filterType = (FilterType)b;
                    Filter = FilterFactory.CreateFilter(reader, filterType);
                }

                //FullDataIdInfo
                if (reader.ReadBoolean())
                {
                    FullDataIdInfo = new FullDataIdInfo();
                    Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
                }

                //IndexCondition
                if (reader.ReadBoolean())
                {
                    IndexCondition = new IndexCondition();
                    Serializer.Deserialize(reader.BaseStream, IndexCondition);
                }

                if (version >= 2)
                {
                    //DomainSpecificProcessingType
                    DomainSpecificProcessingType = (DomainSpecificProcessingType)reader.ReadByte();
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
