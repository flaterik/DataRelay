using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class RandomQueryResult : IVersionSerializable
    {
        #region Data Members
        private bool indexExists;
        public bool IndexExists
        {
            get
            {
                return indexExists;
            }
            set
            {
                indexExists = value;
            }
        }

        private int indexSize;
        public int IndexSize
        {
            get
            {
                return indexSize;
            }
            set
            {
                indexSize = value;
            }
        }

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

        private List<ResultItem> resultItemList;
        public List<ResultItem> ResultItemList
        {
            get
            {
                return resultItemList;
            }
            set
            {
                resultItemList = value;
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

        private string exceptionInfo;
        public string ExceptionInfo
        {
            get
            {
                return exceptionInfo;
            }
            set
            {
                exceptionInfo = value;
            }
        }
        #endregion

        #region Ctors
        public RandomQueryResult()
        {
            Init(false, -1, null, null, -1, null);
        }

        public RandomQueryResult(bool indexExists, int indexSize, byte[] metadata, List<ResultItem> resultItemList, int virtualCount, string exceptionInfo)
        {
            Init(indexExists, indexSize, metadata, resultItemList, virtualCount, exceptionInfo);
        }

        private void Init(bool indexExists, int indexSize, byte[] metadata, List<ResultItem> resultItemList, int virtualCount, string exceptionInfo)
        {
            this.indexExists = indexExists;
            this.indexSize = indexSize;
            this.metadata = metadata;
            this.resultItemList = resultItemList;
            this.virtualCount = virtualCount;
            this.exceptionInfo = exceptionInfo;
        }
        #endregion

        #region IVersionSerializable Members
        public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //IndexExists
                writer.Write(indexExists);

                //IndexSize
                writer.Write(indexSize);

                //Metadata
                if (metadata == null || metadata.Length == 0)
                {
                    writer.Write((ushort) 0);
                }
                else
                {
                    writer.Write((ushort) metadata.Length);
                    writer.Write(metadata);
                }

                //ResultItemList
                if (resultItemList == null || resultItemList.Count == 0)
                {
                    writer.Write(0);
                }
                else
                {
                    writer.Write(resultItemList.Count);
                    foreach (ResultItem resultItem in resultItemList)
                    {
                        resultItem.Serialize(writer);
                    }
                }

                //ExceptionInfo
                writer.Write(exceptionInfo);

                //VirtualCount
                writer.Write(virtualCount);
            }
        }

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //IndexExists
                indexExists = reader.ReadBoolean();

                //IndexSize
                indexSize = reader.ReadInt32();

                //Metadata
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    metadata = reader.ReadBytes(len);
                }

                //ResultItemList
                int listCount = reader.ReadInt32();
                resultItemList = new List<ResultItem>(listCount);
                if (listCount > 0)
                {
                    ResultItem resultItem;
                    for (int i = 0; i < listCount; i++)
                    {
                        resultItem = new ResultItem();
                        resultItem.Deserialize(reader);
                        resultItemList.Add(resultItem);
                    }
                }

                //ExceptionInfo
                exceptionInfo = reader.ReadString();

                //VirtualCount
                virtualCount = reader.ReadInt32();
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

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion
    }
}