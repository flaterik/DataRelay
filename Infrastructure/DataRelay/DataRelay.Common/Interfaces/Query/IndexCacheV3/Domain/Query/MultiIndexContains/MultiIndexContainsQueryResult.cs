using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MultiIndexContainsQueryResult : IVersionSerializable
    {
        #region Data Members

        public List<Pair<MultiIndexContainsQueryResultItem, IndexHeader>> MultiIndexContainsQueryResultItemIndexHeaderList { get; internal set; }

        public string ExceptionInfo { get; set; }

        #endregion

        #region Ctors

        public MultiIndexContainsQueryResult()
        {
            Init(null, null);
        }

        public MultiIndexContainsQueryResult(List<Pair<MultiIndexContainsQueryResultItem, IndexHeader>> multiIndexContainsQueryResultItemIndexHeaderList, string exceptionInfo = null)
        {
            Init(multiIndexContainsQueryResultItemIndexHeaderList, exceptionInfo);
        }

        private void Init(List<Pair<MultiIndexContainsQueryResultItem, IndexHeader>> multiIndexContainsQueryResultItemIndexHeaderList, string exceptionInfo)
        {
            MultiIndexContainsQueryResultItemIndexHeaderList = multiIndexContainsQueryResultItemIndexHeaderList;
            ExceptionInfo = exceptionInfo;
        }

        #endregion

        #region IVersionSerializable Members

        public void Serialize(IPrimitiveWriter writer)
        {
            //MultiIndexContainsQueryResultItemIndexHeaderList
            if (MultiIndexContainsQueryResultItemIndexHeaderList == null || MultiIndexContainsQueryResultItemIndexHeaderList.Count <= 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)MultiIndexContainsQueryResultItemIndexHeaderList.Count);
                foreach (var multiIndexContainsQueryResultItemIndexHeader in MultiIndexContainsQueryResultItemIndexHeaderList)
                {
                    //MultiIndexContainsQueryResultItem
                    if (multiIndexContainsQueryResultItemIndexHeader.First == null)
                    {
                        writer.Write(false);
                    }
                    else
                    {
                        writer.Write(true);
                        Serializer.Serialize(writer.BaseStream, multiIndexContainsQueryResultItemIndexHeader.First);
                    }

                    //IndexHeader
                    if (multiIndexContainsQueryResultItemIndexHeader.Second == null)
                    {
                        writer.Write(false);
                    }
                    else
                    {
                        writer.Write(true);
                        Serializer.Serialize(writer.BaseStream, multiIndexContainsQueryResultItemIndexHeader.Second);
                    }
                }
            }

            //ExceptionInfo
            writer.Write(ExceptionInfo);
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //MultiIndexContainsQueryResultItemIndexHeaderList
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                MultiIndexContainsQueryResultItemIndexHeaderList = new List<Pair<MultiIndexContainsQueryResultItem, IndexHeader>>(count);
                MultiIndexContainsQueryResultItem multiIndexContainsQueryResultItem;
                IndexHeader indexHeader;

                for (ushort i = 0; i < count; i++)
                {
                    multiIndexContainsQueryResultItem = null;
                    indexHeader = null;

                    //MultiIndexContainsQueryResultItem
                    if (reader.ReadBoolean())
                    {
                         multiIndexContainsQueryResultItem = new MultiIndexContainsQueryResultItem();
                        Serializer.Deserialize(reader.BaseStream, multiIndexContainsQueryResultItem);
                    }

                    //IndexHeader
                    if (reader.ReadBoolean())
                    {
                        indexHeader = new IndexHeader();
                        Serializer.Deserialize(reader.BaseStream, indexHeader);
                    }

                    MultiIndexContainsQueryResultItemIndexHeaderList.Add(new Pair<MultiIndexContainsQueryResultItem, IndexHeader>(multiIndexContainsQueryResultItem, indexHeader));
                }

            }

            //ExceptionInfo
            ExceptionInfo = reader.ReadString();
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
