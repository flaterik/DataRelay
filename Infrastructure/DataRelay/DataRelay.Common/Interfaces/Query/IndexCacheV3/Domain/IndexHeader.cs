using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IndexHeader : IVersionSerializable
    {
        #region Data Members

        private byte[] metadata;
        public byte[] Metadata
        {
            get
            {
                return metadata;
            }
            set
            {
                metadata = value;
            }
        }

        private int virtualCount;
        public int VirtualCount
        {
            get
            {
                return virtualCount;
            }
            set
            {
                virtualCount = value;
            }
        }
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
            this.metadata = metadata;
            this.virtualCount = virtualCount;
        }
        #endregion

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            //Metadata
            if (metadata == null || metadata.Length == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)metadata.Length);
                writer.Write(metadata);
            }

            //VirtualCount
            writer.Write(virtualCount);
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //Metadata
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                metadata = reader.ReadBytes(len);
            }

            //VirtualCount
            virtualCount = reader.ReadInt32();
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
