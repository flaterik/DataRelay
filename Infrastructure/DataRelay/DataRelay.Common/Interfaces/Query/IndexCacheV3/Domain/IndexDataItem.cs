using System.Collections.Generic;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IndexDataItem : IndexItem
    {
        #region Data Members
        private byte[] data;
        public byte[] Data
        {
            get
            {
                return data;
            }
            set
            {
                data = value;
            }
        }
        #endregion

        #region Ctors
        public IndexDataItem()
        {
            Init(null);
        }

        public IndexDataItem(IndexItem indexItem)
            : base(indexItem.ItemId, indexItem.Tags)
        {
            Init(null);
        }

        public IndexDataItem(byte[] itemId, byte[] data)
            : base(itemId)
        {
            Init(data);
        }

        public IndexDataItem(byte[] itemId, Dictionary<string /*TagName*/, byte[] /*TagValue*/> tags)
            : base(itemId, tags)
        {
            Init(null);
        }

        public IndexDataItem(byte[] itemId, byte[] data, Dictionary<string /*TagName*/, byte[] /*TagValue*/> tags)
            : base(itemId, tags)
        {
            Init(data);
        }

        private void Init(byte[] data)
        {
            this.data = data;
        }
        #endregion

        #region IVersionSerializable Members
        public override void Serialize(IPrimitiveWriter writer)
        {
            base.Serialize(writer);

            //Data
            if (data == null || data.Length == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)data.Length);
                writer.Write(data);
            }
        }

        public override void Deserialize(IPrimitiveReader reader, int version)
        {
            Deserialize(reader);
        }
        #endregion

        #region ICustomSerializable Members

        public override void Deserialize(IPrimitiveReader reader)
        {
            base.Deserialize(reader);

            //Data
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                data = reader.ReadBytes(len);
            }
        }

        #endregion
    }
}
