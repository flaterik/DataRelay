using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class RandomQueryResult : IVersionSerializable
    {
        #region Data Members

        public bool IndexExists { get; set; }

        public int IndexSize { get; set; }

        public byte[] Metadata { get; set; }

        public MetadataPropertyCollection MetadataPropertyCollection { get; set; }

        public List<ResultItem> ResultItemList { get; set; }

        public int VirtualCount { get; set; }

        public int IndexCap { get; set; }

        public string ExceptionInfo { get; set; }

        #endregion

        #region Ctors

        public RandomQueryResult()
        {
            Init(false, -1, null, null, null, -1, 0, null);
        }

        public RandomQueryResult(bool indexExists, 
            int indexSize, 
            byte[] metadata,
            MetadataPropertyCollection metadataPropertyCollection,
            List<ResultItem> resultItemList, 
            int virtualCount,
            int indexCap, 
            string exceptionInfo)
        {
            Init(indexExists, indexSize, metadata, metadataPropertyCollection, resultItemList, virtualCount, indexCap, exceptionInfo);
        }

        private void Init(bool indexExists, 
            int indexSize, 
            byte[] metadata,
            MetadataPropertyCollection metadataPropertyCollection,
            List<ResultItem> resultItemList, 
            int virtualCount,
            int indexCap, 
            string exceptionInfo)
        {
            IndexExists = indexExists;
            IndexSize = indexSize;
            Metadata = metadata;
            MetadataPropertyCollection = metadataPropertyCollection;
            ResultItemList = resultItemList;
            VirtualCount = virtualCount;
            IndexCap = indexCap;
            ExceptionInfo = exceptionInfo;
        }

        #endregion

        #region IVersionSerializable Members
        public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //IndexExists
                writer.Write(IndexExists);

                //IndexSize
                writer.Write(IndexSize);

                //Metadata
                if (Metadata == null || Metadata.Length == 0)
                {
                    writer.Write((ushort) 0);
                }
                else
                {
                    writer.Write((ushort) Metadata.Length);
                    writer.Write(Metadata);
                }

                //ResultItemList
                if (ResultItemList == null || ResultItemList.Count == 0)
                {
                    writer.Write(0);
                }
                else
                {
                    writer.Write(ResultItemList.Count);
                    foreach (ResultItem resultItem in ResultItemList)
                    {
                        resultItem.Serialize(writer);
                    }
                }

                //ExceptionInfo
                writer.Write(ExceptionInfo);

                //VirtualCount
                writer.Write(VirtualCount);

                //IndexCap
                writer.Write(IndexCap);

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
        }

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //IndexExists
                IndexExists = reader.ReadBoolean();

                //IndexSize
                IndexSize = reader.ReadInt32();

                //Metadata
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    Metadata = reader.ReadBytes(len);
                }

                //ResultItemList
                int listCount = reader.ReadInt32();
                ResultItemList = new List<ResultItem>(listCount);
                if (listCount > 0)
                {
                    ResultItem resultItem;
                    for (int i = 0; i < listCount; i++)
                    {
                        resultItem = new ResultItem();
                        resultItem.Deserialize(reader);
                        ResultItemList.Add(resultItem);
                    }
                }

                //ExceptionInfo
                ExceptionInfo = reader.ReadString();

                //VirtualCount
                VirtualCount = reader.ReadInt32();

                //IndexCap
                if (version >= 2)
                {
                    IndexCap = reader.ReadInt32();
                }

                if (version >= 3)
                {
                    //MetadataPropertyCollection
                    if (reader.ReadBoolean())
                    {
                        MetadataPropertyCollection = new MetadataPropertyCollection();
                        Serializer.Deserialize(reader.BaseStream, MetadataPropertyCollection);
                    }
                }
            }
        }

        private const int CURRENT_VERSION = 3;
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

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion
    }
}