using MySpace.Common;
using System.Collections.Generic;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MultiItemResult : List<IndexDataItem>, IVersionSerializable
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
        #endregion

        #region Ctors
        public MultiItemResult()
        {
            Init(null);
        }

        public MultiItemResult(byte[] indexId)
        {
            Init(indexId);
        }

        private void Init(byte[] indexId)
        {
            this.indexId = indexId;
        }
        #endregion

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            //IndexDataItem List
            if (Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)Count);
                foreach (IndexDataItem indexDataItem in this)
                {
                    indexDataItem.Serialize(writer);
                }

                //IndexId
                if (indexId == null || indexId.Length == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)indexId.Length);
                    writer.Write(indexId);
                }
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            Deserialize(reader);
        }

        public int CurrentVersion
        {
            get
            {
                return 1;
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
            //IndexDataItem List
            //List
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexDataItem indexDataItem;
                for (ushort i = 0; i < count; i++)
                {
                    indexDataItem = new IndexDataItem();
                    indexDataItem.Deserialize(reader);
                    Add(indexDataItem);
                }

                //IndexId
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    indexId = reader.ReadBytes(len);
                }
            }

        }

        #endregion

    }
}
