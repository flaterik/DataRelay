using System;
using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class FirstLastQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        #region Data Members

        public byte[] IndexId { get; set; }

        public int FirstPageSize { get; set; }

        public int LastPageSize { get; set; }

        public string TargetIndexName { get; set; }

        public bool ExcludeData { get; set; }

        public bool GetMetadata { get; set; }

        public Filter Filter { get; set; }

        public FullDataIdInfo FullDataIdInfo { get; set; }

        public TagSort TagSort { get; set; }

        public IndexCondition IndexCondition { get; set; }

        public DomainSpecificProcessingType DomainSpecificProcessingType { get; set; }

        #endregion

        #region Ctors

        public FirstLastQuery()
        {
            Init(null, -1, -1, null, false, false, null);
        }

        public FirstLastQuery(byte[] indexId, int firstPageSize, int lastPageSize, string targetIndexName)
        {
            Init(indexId, firstPageSize, lastPageSize, targetIndexName, false, false, null);
        }

        [Obsolete("This constructor is obsolete; use object initializer instead", true)]
        public FirstLastQuery(byte[] indexId, int firstPageSize, int lastPageSize, string targetIndexName, CriterionList criterionList)
        {
            Init(indexId, firstPageSize, lastPageSize, targetIndexName, false, false, null);
        }

        [Obsolete("This constructor is obsolete; use object initializer instead", true)]
        public FirstLastQuery(byte[] indexId, int firstPageSize, int lastPageSize, string targetIndexName, CriterionList criterionList, bool excludeData, bool getMetadata)
        {
            Init(indexId, firstPageSize, lastPageSize, targetIndexName, excludeData, getMetadata, null);
        }

        private void Init(byte[] indexId, int firstPageSize, int lastPageSize, string targetIndexName, bool excludeData, bool getMetadata, FullDataIdInfo fullDataIdInfo)
        {
            IndexId = indexId;
            FirstPageSize = firstPageSize;
            LastPageSize = lastPageSize;
            TargetIndexName = targetIndexName;
            ExcludeData = excludeData;
            GetMetadata = getMetadata;
            FullDataIdInfo = fullDataIdInfo;
        }

        #endregion

        #region IRelayMessageQuery Members

        public byte QueryId
        {
            get
            {
                return (byte)QueryTypes.FirstLastQuery;
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

            //FirstPageSize
            writer.Write(FirstPageSize);

            //LastPageSize
            writer.Write(LastPageSize);

            //TargetIndexName
            writer.Write(TargetIndexName);

            //Write a byte to account for deprecated CriterionList
            writer.Write((byte)0);

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

            //TagSort
            if (TagSort == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, TagSort);
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

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //IndexId
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                IndexId = reader.ReadBytes(len);
            }

            //FirstPageSize
            FirstPageSize = reader.ReadInt32();

            //LastPageSize
            LastPageSize = reader.ReadInt32();

            //TargetIndexName
            TargetIndexName = reader.ReadString();

            //Read a byte to account for deprecated CriterionList
            reader.ReadByte();

            //ExcludeData
            ExcludeData = reader.ReadBoolean();

            //GetMetadata
            GetMetadata = reader.ReadBoolean();

            if (version >= 2)
            {
                //Filter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    FilterType filterType = (FilterType)b;
                    Filter = FilterFactory.CreateFilter(reader, filterType);
                }
            }

            if (version == 3)
            {
                //FullDataIdInfo
                FullDataIdInfo = new FullDataIdInfo();
                Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
            }
            
            if (version >= 4)
            {
                //FullDataIdInfo
                if (reader.ReadBoolean())
                {
                    FullDataIdInfo = new FullDataIdInfo();
                    Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
                }

                //TagSort
                if (reader.ReadBoolean())
                {
                    TagSort = new TagSort();
                    Serializer.Deserialize(reader.BaseStream, TagSort);
                }
            }

            if (version >= 5)
            {
                //IndexCondition
                if (reader.ReadBoolean())
                {
                    IndexCondition = new IndexCondition();
                    Serializer.Deserialize(reader.BaseStream, IndexCondition);
                }
            }

            if (version >= 6)
            {
                //DomainSpecificProcessingType
                DomainSpecificProcessingType = (DomainSpecificProcessingType)reader.ReadByte();
            }
        }

        private const int CURRENT_VERSION = 6;
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