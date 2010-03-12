using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class FirstLastQueryResult : IVersionSerializable
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

		private List<ResultItem> firstPageResultItemList;
		public List<ResultItem> FirstPageResultItemList
		{
			get
			{
				return firstPageResultItemList;
			}
			set
			{
				firstPageResultItemList = value;
			}
		}

		private List<ResultItem> lastPageResultItemList;
		public List<ResultItem> LastPageResultItemList
		{
			get
			{
				return lastPageResultItemList;
			}
			set
			{
				lastPageResultItemList = value;
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
		public FirstLastQueryResult()
		{
			Init(false, -1, null, null, null, -1, null);
		}

        public FirstLastQueryResult(bool indexExists, int indexSize, byte[] metadata, List<ResultItem> firstPageResultItemList, List<ResultItem> lastPageResultItemList, int virtualCount, string exceptionInfo)
		{
			Init(indexExists, indexSize, metadata, firstPageResultItemList, lastPageResultItemList,  virtualCount, exceptionInfo);
		}

        private void Init(bool indexExists, int indexSize, byte[] metadata, List<ResultItem> firstPageResultItemList, List<ResultItem> lastPageResultItemList, int virtualCount, string exceptionInfo)
		{
			this.indexExists = indexExists;
			this.indexSize = indexSize;
			this.metadata = metadata;
			this.firstPageResultItemList = firstPageResultItemList;
			this.lastPageResultItemList = lastPageResultItemList;
            this.virtualCount = virtualCount;
			this.exceptionInfo = exceptionInfo;
		}
		#endregion

		#region Methods
		#endregion

		#region IVersionSerializable Members
		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			//IndexExists
			writer.Write(indexExists);

			//IndexSize
			writer.Write(indexSize);

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

			//FirstPageResultItemList
			if (firstPageResultItemList == null || firstPageResultItemList.Count == 0)
			{
				writer.Write(0);
			}
			else
			{
				writer.Write(firstPageResultItemList.Count);
				foreach (ResultItem resultItem in firstPageResultItemList)
				{
					resultItem.Serialize(writer);
				}
			}

			//LastPageResultItemList
			if (lastPageResultItemList == null || lastPageResultItemList.Count == 0)
			{
				writer.Write(0);
			}
			else
			{
				writer.Write(lastPageResultItemList.Count);
				foreach (ResultItem resultItem in lastPageResultItemList)
				{
					resultItem.Serialize(writer);
				}
			}

			//ExceptionInfo
			writer.Write(exceptionInfo);

            //VirtualCount
            writer.Write(virtualCount);
		}

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
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

            //FirstPageResultItemList
            int listCount = reader.ReadInt32();
            firstPageResultItemList = new List<ResultItem>(listCount);
            if (listCount > 0)
            {
                ResultItem resultItem;
                for (int i = 0; i < listCount; i++)
                {
                    resultItem = new ResultItem();
                    resultItem.Deserialize(reader);
                    firstPageResultItemList.Add(resultItem);
                }
            }

            //LastPageResultItemList
            listCount = reader.ReadInt32();
            lastPageResultItemList = new List<ResultItem>(listCount);
            if (listCount > 0)
            {
                ResultItem resultItem;
                for (int i = 0; i < listCount; i++)
                {
                    resultItem = new ResultItem();
                    resultItem.Deserialize(reader);
                    lastPageResultItemList.Add(resultItem);
                }
            }

            //ExceptionInfo
            exceptionInfo = reader.ReadString();

            //VirtualCount
            if (version >= 2)
            {
                virtualCount = reader.ReadInt32();
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

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
            reader.Response = SerializationResponse.Unhandled;
		}

		#endregion
	}
}
