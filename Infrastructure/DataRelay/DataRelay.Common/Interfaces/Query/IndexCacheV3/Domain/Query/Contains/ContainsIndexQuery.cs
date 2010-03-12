using System.Collections.Generic;
using MySpace.Common.IO;
using MySpace.Common;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class ContainsIndexQuery : IRelayMessageQuery, IPrimaryQueryId
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

		private List<IndexItem> indexItemList;
        public List<IndexItem> IndexItemList
		{
			get
			{
                return indexItemList;
			}
			set
			{
                indexItemList = value;
			}
		}

		private string targetIndexName;
		public string TargetIndexName
		{
			get
			{
				return targetIndexName;
			}
			set
			{
				targetIndexName = value;
			}
		}

		private List<string> tagsFromIndexes;
		public List<string> TagsFromIndexes
		{
			get
			{
				return tagsFromIndexes;
			}
			set
			{
				tagsFromIndexes = value;
			}
		}

		private bool excludeData;
		public bool ExcludeData
		{
			get
			{
				return excludeData;
			}
			set
			{
				excludeData = value;
			}
		}

		private bool getMetadata;
		public bool GetMetadata
		{
			get
			{
				return getMetadata;
			}
			set
			{
				getMetadata = value;
			}
		}

        private FullDataIdInfo fullDataIdInfo;
        public FullDataIdInfo FullDataIdInfo
	    {
	        get
	        {
                return fullDataIdInfo;
	        }
            set
            {
                fullDataIdInfo = value;
            }
	    }
		#endregion

		#region Ctors
		public ContainsIndexQuery()
		{
			Init(null, null, null, null, false, false, null);
		}

        public ContainsIndexQuery(byte[] indexId, IndexItem indexItem, string targetIndexName)
        {
            Init(indexId, new List<IndexItem>(1) { indexItem }, targetIndexName, null, false, false, null);
        }

        public ContainsIndexQuery(byte[] indexId, IndexItem indexItem, string targetIndexName, List<string> tagsFromIndexes, bool excludeData, bool getMetadata)
        {
            Init(indexId, new List<IndexItem>(1) {indexItem}, targetIndexName, tagsFromIndexes, excludeData, getMetadata, null);
        }

        public ContainsIndexQuery(byte[] indexId, List<IndexItem> indexItemList, string targetIndexName)
		{
            Init(indexId, indexItemList, targetIndexName, null, false, false, null);
		}

        public ContainsIndexQuery(byte[] indexId, 
            List<IndexItem> indexItemList, 
            string targetIndexName, 
            List<string> tagsFromIndexes,
            bool excludeData, 
            bool getMetadata)
		{
            Init(indexId, indexItemList, targetIndexName, tagsFromIndexes, excludeData, getMetadata, null);
		}

        private void Init(byte[] indexId, 
            List<IndexItem> indexItemList, 
            string targetIndexName, 
            List<string> tagsFromIndexes, 
            bool excludeData, 
            bool getMetadata,
            FullDataIdInfo fullDataIdInfo)
		{
			this.indexId = indexId;
            this.indexItemList = indexItemList;
			this.targetIndexName = targetIndexName;
			this.tagsFromIndexes = tagsFromIndexes;
			this.excludeData = excludeData;
			this.getMetadata = getMetadata;
            this.fullDataIdInfo = fullDataIdInfo;
		}
		#endregion

		#region IRelayMessageQuery Members
		public byte QueryId
		{
			get
			{
				return (byte)QueryTypes.ContainsIndexQuery;
			}
		}
		#endregion

		#region IPrimaryQueryId Members
        private int primaryId;
		public int PrimaryId
		{
            get
            {
                return primaryId > 0 ? primaryId : IndexCacheUtils.GeneratePrimaryId(indexId);
            }
            set
            {
                primaryId = value;
            }
		}
		#endregion

		#region IVersionSerializable Members
		public void Serialize(IPrimitiveWriter writer)
		{
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

			//IndexItemList
            if(indexItemList == null || indexItemList.Count == 0)
            {
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)indexItemList.Count);
                foreach(IndexItem indexItem in indexItemList)
                {
                    indexItem.Serialize(writer);
                }
			}

			//TargetIndexName
			writer.Write(targetIndexName);

			//TagsFromIndexes
			if (tagsFromIndexes == null || tagsFromIndexes.Count == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)tagsFromIndexes.Count);
				foreach (string str in tagsFromIndexes)
				{
					writer.Write(str);
				}
			}

			//ExcludeData
			writer.Write(excludeData);

			//GetMetadata
			writer.Write(getMetadata);

            //FullDataIdInfo
		    Serializer.Serialize(writer.BaseStream, fullDataIdInfo);
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
            //IndexId
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                indexId = reader.ReadBytes(len);
            }

            //IndexItemList
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexItem indexItem;
                indexItemList = new List<IndexItem>(count);
                for (ushort i = 0; i < count; i++)
                {
                    indexItem = new IndexItem();
                    indexItem.Deserialize(reader);
                    indexItemList.Add(indexItem);
                }
            }

            //TargetIndexName
            targetIndexName = reader.ReadString();

            //TagsFromIndexes
            count = reader.ReadUInt16();
            tagsFromIndexes = new List<string>(count);
            if (count > 0)
            {
                for (ushort i = 0; i < count; i++)
                {
                    tagsFromIndexes.Add(reader.ReadString());
                }
            }

            //ExcludeData
            excludeData = reader.ReadBoolean();

            //GetMetadata
            getMetadata = reader.ReadBoolean();

            if(version >= 2)
            {
                //FullDataIdInfo
                fullDataIdInfo = new FullDataIdInfo();
                Serializer.Deserialize(reader.BaseStream, fullDataIdInfo);
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
		public void Deserialize(IPrimitiveReader reader)
		{
            reader.Response = SerializationResponse.Unhandled;
		}
		#endregion
	}
}