using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class FirstLastQueryResult : IVersionSerializable
	{
		#region Data Members

	    public bool IndexExists { get; set; }

	    public int IndexSize { get; set; }

	    public byte[] Metadata { get; set; }

        public MetadataPropertyCollection MetadataPropertyCollection { get; set; }

	    public List<ResultItem> FirstPageResultItemList { get; set; }

	    public List<ResultItem> LastPageResultItemList { get; set; }

	    public int VirtualCount { get; set; }

        public int IndexCap { get; set; }

	    public string ExceptionInfo { get; set; }

	    #endregion

		#region Ctors

		public FirstLastQueryResult()
		{
            Init(false, -1, null, null, null, null, -1, 0, null);
		}

        public FirstLastQueryResult(bool indexExists, 
            int indexSize,
            byte[] metadata, 
            MetadataPropertyCollection metadataPropertyCollection,
            List<ResultItem> firstPageResultItemList, 
            List<ResultItem> lastPageResultItemList, 
            int virtualCount,
            int indexCap,
            string exceptionInfo)
		{
			Init(indexExists, 
                indexSize,
                metadata,
                metadataPropertyCollection,
                firstPageResultItemList,
                lastPageResultItemList,  
                virtualCount, 
                indexCap, 
                exceptionInfo);
		}

        private void Init(bool indexExists, 
            int indexSize,
            byte[] metadata, 
            MetadataPropertyCollection metadataPropertyCollection,
            List<ResultItem> firstPageResultItemList, 
            List<ResultItem> lastPageResultItemList, 
            int virtualCount, 
            int indexCap,
            string exceptionInfo)
		{
			IndexExists = indexExists;
			IndexSize = indexSize;
			Metadata = metadata;
            MetadataPropertyCollection = metadataPropertyCollection;
            FirstPageResultItemList = firstPageResultItemList;
			LastPageResultItemList = lastPageResultItemList;
            VirtualCount = virtualCount;
            IndexCap = indexCap;
			ExceptionInfo = exceptionInfo;
		}

		#endregion


		#region IVersionSerializable Members

		public void Serialize(IPrimitiveWriter writer)
		{
			//IndexExists
			writer.Write(IndexExists);

			//IndexSize
			writer.Write(IndexSize);

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

			//FirstPageResultItemList
			if (FirstPageResultItemList == null || FirstPageResultItemList.Count == 0)
			{
				writer.Write(0);
			}
			else
			{
				writer.Write(FirstPageResultItemList.Count);
				foreach (ResultItem resultItem in FirstPageResultItemList)
				{
					resultItem.Serialize(writer);
				}
			}

			//LastPageResultItemList
			if (LastPageResultItemList == null || LastPageResultItemList.Count == 0)
			{
				writer.Write(0);
			}
			else
			{
				writer.Write(LastPageResultItemList.Count);
				foreach (ResultItem resultItem in LastPageResultItemList)
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

		public void Deserialize(IPrimitiveReader reader, int version)
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

            //FirstPageResultItemList
            int listCount = reader.ReadInt32();
            FirstPageResultItemList = new List<ResultItem>(listCount);
            if (listCount > 0)
            {
                ResultItem resultItem;
                for (int i = 0; i < listCount; i++)
                {
                    resultItem = new ResultItem();
                    resultItem.Deserialize(reader);
                    FirstPageResultItemList.Add(resultItem);
                }
            }

            //LastPageResultItemList
            listCount = reader.ReadInt32();
            LastPageResultItemList = new List<ResultItem>(listCount);
            if (listCount > 0)
            {
                ResultItem resultItem;
                for (int i = 0; i < listCount; i++)
                {
                    resultItem = new ResultItem();
                    resultItem.Deserialize(reader);
                    LastPageResultItemList.Add(resultItem);
                }
            }

            //ExceptionInfo
            ExceptionInfo = reader.ReadString();

            //VirtualCount
            if (version >= 2)
            {
                VirtualCount = reader.ReadInt32();
            }

            //IndexCap
            if (version >= 3)
            {
                IndexCap = reader.ReadInt32();
            }

            if (version >= 4)
            {
                //MetadataPropertyCollection
                if (reader.ReadBoolean())
                {
                    MetadataPropertyCollection = new MetadataPropertyCollection();
                    Serializer.Deserialize(reader.BaseStream, MetadataPropertyCollection);
                }
            }
		}

        private const int CURRENT_VERSION = 4;
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
