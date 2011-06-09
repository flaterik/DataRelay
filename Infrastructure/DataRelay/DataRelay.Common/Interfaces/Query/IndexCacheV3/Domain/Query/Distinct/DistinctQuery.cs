using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class DistinctQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        #region Data Members

        public byte[] IndexId { get; set; }

        public int? ItemsToLookUp { get; set; }

        public string FieldName { get; set; }

        public string TargetIndexName { get; set; }

        public IndexCondition IndexCondition { get; set; }

        #endregion

        #region IRelayMessageQuery Members

        public byte QueryId
        {
            get
            {
                return (byte)QueryTypes.DistinctQuery;
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

            //FieldName
            writer.Write(FieldName);

            //ItemsToLookUp
            writer.Write(ItemsToLookUp);

            //TargetIndexName
            writer.Write(TargetIndexName);

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

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //IndexId
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                IndexId = reader.ReadBytes(len);
            }

            //FieldName
            FieldName = reader.ReadString();

            //ItemsToLookUp
            ItemsToLookUp = reader.ReadNullableInt32();

            //TargetIndexName
            TargetIndexName = reader.ReadString();

            //IndexCondition
            if (reader.ReadBoolean())
            {
                IndexCondition = new IndexCondition();
                Serializer.Deserialize(reader.BaseStream, IndexCondition);
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