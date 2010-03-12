using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IndexItem : IVersionSerializable, IItem
    {
        #region Data Members
        private byte[] itemId;
        public byte[] ItemId
        {
            get
            {
                return itemId;
            }
            set
            {
                itemId = value;
            }
        }

        private Dictionary<string /*TagName*/, byte[] /*TagValue*/> tags;
        public Dictionary<string /*TagName*/, byte[] /*TagValue*/> Tags
        {
            get
            {
                return tags;
            }
            set
            {
                tags = value;
            }
        }

        #endregion

        #region Ctors
        public IndexItem()
        {
            Init(null, null);
        }

        public IndexItem(byte[] itemId)
        {
            Init(itemId, null);
        }

        public IndexItem(byte[] itemId, Dictionary<string /*TagName*/, byte[] /*TagValue*/> tags)
        {
            Init(itemId, tags);
        }

        private void Init(byte[] itemId, Dictionary<string /*TagName*/, byte[] /*TagValue*/> tags)
        {
            this.itemId = itemId;
            this.tags = tags;
        }

        #endregion

        #region IVersionSerializable Members
        public virtual void Serialize(IPrimitiveWriter writer)
        {
            //ItemId
            if (itemId == null || itemId.Length == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)itemId.Length);
                writer.Write(itemId);
            }

            //Tags
            if (tags == null || tags.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)tags.Count);
                foreach (KeyValuePair<string /*TagName*/, byte[] /*TagValue*/> kvp in tags)
                {
                    writer.Write(kvp.Key);
                    if (kvp.Value == null || kvp.Value.Length == 0)
                    {
                        writer.Write((ushort)0);
                    }
                    else
                    {
                        writer.Write((ushort)kvp.Value.Length);
                        writer.Write(kvp.Value);
                    }
                }
            }

        }

        public virtual void Deserialize(IPrimitiveReader reader, int version)
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

        public virtual void Deserialize(IPrimitiveReader reader)
        {
            //ItemId
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                itemId = reader.ReadBytes(len);
            }

            //Tags
            ushort count = reader.ReadUInt16();
            tags = new Dictionary<string, byte[]>(count);
            if (count > 0)
            {
                string tagName;
                byte[] tagValue;
                ushort tagValueLen;

                for (ushort i = 0; i < count; i++)
                {
                    tagName = reader.ReadString();
                    tagValueLen = reader.ReadUInt16();
                    tagValue = null;
                    if (tagValueLen > 0)
                    {
                        tagValue = reader.ReadBytes(tagValueLen);
                    }
                    tags.Add(tagName, tagValue);
                }
            }
        }

        #endregion

        #region IItem Members

        public bool TryGetTagValue(string tagName, out byte[] tagValue)
        {
            tagValue = null;
            if (tags != null && tags.Count > 0 && tagName != null)
            {
                if(tags.TryGetValue(tagName, out tagValue))
                    return true;
            }
            return false;
        }

        #endregion
    }
}
