using System.Collections.Generic;
using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class ContainsIndexQuery : IRelayMessageQuery, IPrimaryQueryId
	{
		#region Data Members

	    public byte[] IndexId { get; set; }

	    public List<IndexItem> IndexItemList { get; set; }

	    public string TargetIndexName { get; set; }

	    public List<string> TagsFromIndexes { get; set; }

	    public bool ExcludeData { get; set; }

	    public bool GetMetadata { get; set; }

	    public FullDataIdInfo FullDataIdInfo { get; set; }

	    public DomainSpecificProcessingType DomainSpecificProcessingType { get; set; }

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
			IndexId = indexId;
            IndexItemList = indexItemList;
			TargetIndexName = targetIndexName;
			TagsFromIndexes = tagsFromIndexes;
			ExcludeData = excludeData;
			GetMetadata = getMetadata;
            FullDataIdInfo = fullDataIdInfo;
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
                return primaryId > 0 ? primaryId : IndexCacheUtils.GeneratePrimaryId(IndexId);
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
			if (IndexId == null || IndexId.Length == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)IndexId.Length);
				writer.Write(IndexId);
			}

			//IndexItemList
            if(IndexItemList == null || IndexItemList.Count == 0)
            {
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)IndexItemList.Count);
                foreach(IndexItem indexItem in IndexItemList)
                {
                    indexItem.Serialize(writer);
                }
			}

			//TargetIndexName
			writer.Write(TargetIndexName);

			//TagsFromIndexes
			if (TagsFromIndexes == null || TagsFromIndexes.Count == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)TagsFromIndexes.Count);
				foreach (string str in TagsFromIndexes)
				{
					writer.Write(str);
				}
			}

			//ExcludeData
			writer.Write(ExcludeData);

			//GetMetadata
			writer.Write(GetMetadata);

            //FullDataIdInfo
            if (FullDataIdInfo == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, FullDataIdInfo);
            }

            //DomainSpecificProcessingType
            writer.Write((byte)DomainSpecificProcessingType);
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
            //IndexId
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                IndexId = reader.ReadBytes(len);
            }

            //IndexItemList
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexItem indexItem;
                IndexItemList = new List<IndexItem>(count);
                for (ushort i = 0; i < count; i++)
                {
                    indexItem = new IndexItem();
                    indexItem.Deserialize(reader);
                    IndexItemList.Add(indexItem);
                }
            }

            //TargetIndexName
            TargetIndexName = reader.ReadString();

            //TagsFromIndexes
            count = reader.ReadUInt16();
            TagsFromIndexes = new List<string>(count);
            if (count > 0)
            {
                for (ushort i = 0; i < count; i++)
                {
                    TagsFromIndexes.Add(reader.ReadString());
                }
            }

            //ExcludeData
            ExcludeData = reader.ReadBoolean();

            //GetMetadata
            GetMetadata = reader.ReadBoolean();

            if(version == 2)
            {
                //FullDataIdInfo
                FullDataIdInfo = new FullDataIdInfo();
                Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
            }

            if (version >= 3)
            {
                //FullDataIdInfo
                if (reader.ReadBoolean())
                {
                    FullDataIdInfo = new FullDataIdInfo();
                    Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
                }

                //DomainSpecificProcessingType
                DomainSpecificProcessingType = (DomainSpecificProcessingType)reader.ReadByte();
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
		public void Deserialize(IPrimitiveReader reader)
		{
            reader.Response = SerializationResponse.Unhandled;
		}
		#endregion
	}
}