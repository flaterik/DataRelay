using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IndexHeader : IVersionSerializable
    {

        #region Data Members

        public byte[] Metadata { get; set; }

        public MetadataPropertyCollection MetadataPropertyCollection { get; set; }

        public int VirtualCount { get; set; }

        #endregion

        #region Ctors

        public IndexHeader()
        {
            Init(null, -1);
        }

        public IndexHeader(byte[] metadata, int virtualCount)
        {
            Init(metadata, virtualCount);
        }

        private void Init(byte[] metadata, int virtualCount)
        {
            Metadata = metadata;
            VirtualCount = virtualCount;
        }

        #endregion

        #region IVersionSerializable Members

        public void Serialize(IPrimitiveWriter writer)
        {
            //Metadata
            if (Metadata == null || Metadata.Length == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)Metadata.Length);
                writer.Write(Metadata);
            }

            //VirtualCount
            writer.Write(VirtualCount);

            //MetadataPropertyCollection
            if (MetadataPropertyCollection == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, MetadataPropertyCollection);
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //Metadata
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                Metadata = reader.ReadBytes(len);
            }

            //VirtualCount
            VirtualCount = reader.ReadInt32();

            if (version >= 2)
            {
                //MetadataPropertyCollection
                if (reader.ReadBoolean())
                {
                    MetadataPropertyCollection = new MetadataPropertyCollection();
                    Serializer.Deserialize(reader.BaseStream, MetadataPropertyCollection);
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
