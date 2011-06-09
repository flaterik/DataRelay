using MySpace.Common;
using MySpace.Common.IO;
using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MultiIndexContainsQueryResultItem :  List<IndexDataItem>, IVersionSerializable
    {
        #region Data Members

        public byte[] IndexId { get; set; }

        public int IndexSize { get; set; }

        public bool IndexExists { get; set; }

        public int IndexCap { get; set; }

        #endregion

        #region Ctors

        public MultiIndexContainsQueryResultItem()
        {
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
                    if (indexDataItem == null)
                    {
                        writer.Write(false);
                    }
                    else
                    {
                        writer.Write(true);
                        Serializer.Serialize(writer.BaseStream, indexDataItem);
                    }
                }

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

                //IndexSize
                writer.Write(IndexSize);

                //IndexExists
                writer.Write(IndexExists);

                //IndexCap
                writer.Write(IndexCap);
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //IndexDataItem List
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexDataItem indexDataItem;
                for (ushort i = 0; i < count; i++)
                {
                    if (reader.ReadBoolean())
                    {
                        indexDataItem = new IndexDataItem();
                        Serializer.Deserialize(reader.BaseStream, indexDataItem);
                        Add(indexDataItem);
                    }
                }

                //IndexId
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    IndexId = reader.ReadBytes(len);
                }

                //IndexSize
                IndexSize = reader.ReadInt32();

                //IndexExists
                IndexExists = reader.ReadBoolean();

                //IndexCap
                IndexCap = reader.ReadInt32();
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
