using System;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.Framework;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class CacheIndex : IVersionSerializable, IExtendedRawCacheParameter
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

		private string targetIndexName;
		public string TargetIndexName
		{
			get
			{
				return targetIndexName;
			}
			//Can be set through Ctor only
		}

		private Dictionary<string /*IndexName*/, List<string> /*TagNameList*/> indexTagMapping;
		public Dictionary<string /*IndexName*/, List<string> /*TagNameList*/> IndexTagMapping
		{
			get
			{
				return indexTagMapping;
			}
			//Can be set through Ctor only
		}

		private List<IndexDataItem> addList;
		public List<IndexDataItem> AddList
		{
			get
			{
				return addList;
			}
			set
			{
				addList = value;
			}
		}

		private List<IndexItem> deleteList;
		public List<IndexItem> DeleteList
		{
			get
			{
				return deleteList;
			}
			set
			{
				deleteList = value;
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

		private bool updateMetadata;
		public bool UpdateMetadata
		{
			get
			{
				return updateMetadata;
			}
			set
			{
				updateMetadata = value;
			}
		}

		private bool replaceFullIndex;
		public bool ReplaceFullIndex
		{
			get
			{
				return replaceFullIndex;
			}
			set
			{
				replaceFullIndex = value;
			}
		}

	    private bool preserveData;
        public bool PreserveData
        {
            get
            {
                return preserveData;
            }
            set
            {
                preserveData = value;
            }
        }

        private Dictionary<string /*IndexName*/, int /*VirtualCount*/> indexVirtualCountMapping;
        public Dictionary<string /*IndexName*/, int /*VirtualCount*/> IndexVirtualCountMapping
        {
            get
            {
                return indexVirtualCountMapping;
            }
            set
            {
                indexVirtualCountMapping = value;
            }
        }

		#endregion

		#region Ctors
		//Parameterless
		public CacheIndex()
		{
			Init(null, null, null, null, null, null, false, false, false, null, 0);
		}

		//With targetIndexName and with NO indexTagMapping
		public CacheIndex(byte[] indexId, string targetIndexName, List<IndexDataItem> addList)
		{
            Init(indexId, targetIndexName, null, addList, null, null, false, false, false, null, 0);
		}

		public CacheIndex(byte[] indexId, string targetIndexName, List<IndexDataItem> addList, List<IndexItem> deleteList)
		{
            Init(indexId, targetIndexName, null, addList, deleteList, null, false, false, false, null, 0);
		}

		public CacheIndex(byte[] indexId, string targetIndexName, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex)
		{
            Init(indexId, targetIndexName, null, addList, deleteList, metadata, updateMetadata, replaceFullIndex, false, null, 0);
		}

        public CacheIndex(byte[] indexId, string targetIndexName, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex, bool preserveData)
        {
            Init(indexId, targetIndexName, null, addList, deleteList, metadata, updateMetadata, replaceFullIndex, preserveData, null, 0);
        }

		//With indexTagMapping and with NO targetIndexName
		public CacheIndex(byte[] indexId, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList)
		{
            Init(indexId, null, indexTagMapping, addList, null, null, false, false, false, null, 0);
		}

		public CacheIndex(byte[] indexId, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList, List<IndexItem> deleteList)
		{
            Init(indexId, null, indexTagMapping, addList, deleteList, null, false, false, false, null, 0);
		}

		public CacheIndex(byte[] indexId, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex)
		{
            Init(indexId, null, indexTagMapping, addList, deleteList, metadata, updateMetadata, replaceFullIndex, false, null, 0);
		}

        public CacheIndex(byte[] indexId, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex, bool preserveData)
        {
            Init(indexId, null, indexTagMapping, addList, deleteList, metadata, updateMetadata, replaceFullIndex, preserveData, null, 0);
        }

        //VirtualCount Update
        public CacheIndex(byte[] indexId, Dictionary<string, int> indexVirtualCountMapping)
        {
            Init(indexId, null, null, null, null, null, false, false, false, indexVirtualCountMapping, 0);
        }

        private void Init(byte[] indexId, string targetIndexName, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex, bool preserveData, Dictionary<string, int> indexVirtualCountMapping, int primaryId)
		{
			this.indexId = indexId;
			this.targetIndexName = targetIndexName;
			this.indexTagMapping = indexTagMapping;
			this.addList = addList;
			this.deleteList = deleteList;
			this.metadata = metadata;
			this.updateMetadata = updateMetadata;
			this.replaceFullIndex = replaceFullIndex;
            this.preserveData = preserveData;
            this.indexVirtualCountMapping = indexVirtualCountMapping;
            this.primaryId = primaryId;
		}

		#endregion

		#region IExtendedRawCacheParameter Members
		public byte[] ExtendedId
		{
			get
			{
				return indexId;
			}
			set
			{
				throw new Exception("Setter for 'CacheIndex.ExtendedId' is not implemented and should not be invoked!");
			}
		}
		private DateTime? lastUpdatedDate;
		public DateTime? LastUpdatedDate
		{
			get
			{
				return lastUpdatedDate;
			}
			set
			{
				lastUpdatedDate = value;
			}
		}
		#endregion

		#region ICacheParameter Members
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

		private DataSource dataSource = DataSource.Unknown;
		public DataSource DataSource
		{
			get
			{
				return dataSource;
			}
			set
			{
				dataSource = value;
			}
		}

		public bool IsEmpty
		{
			get
			{
				return false;
			}
			set
			{
				return;
			}
		}

		public bool IsValid
		{
			get
			{
				return true;
			}
		}

		public bool EditMode
		{
			get
			{
				return false;
			}
			set
			{
				return;
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

			//TargetIndexName
			writer.Write(targetIndexName);

			//IndexTagMapping
			if (indexTagMapping == null || indexTagMapping.Count == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)indexTagMapping.Count);
				foreach (KeyValuePair<string /*IndexName*/, List<string> /*TagNameList*/> kvp in indexTagMapping)
				{
					writer.Write(kvp.Key);
					if (kvp.Value == null || kvp.Value.Count == 0)
					{
						writer.Write((ushort)0);
					}
					else
					{
						writer.Write((ushort)kvp.Value.Count);
						foreach (string str in kvp.Value)
						{
							writer.Write(str);
						}
					}
				}
			}

			//AddList
			if (addList == null || addList.Count == 0)
			{
				writer.Write(0);
			}
			else
			{
				writer.Write(addList.Count);
				foreach (IndexDataItem indexDataItem in addList)
				{
					indexDataItem.Serialize(writer);
				}
			}

			//DeleteList
			if (deleteList == null || deleteList.Count == 0)
			{
				writer.Write(0);
			}
			else
			{
				writer.Write(deleteList.Count);
				foreach (IndexItem indexItem in deleteList)
				{
					indexItem.Serialize(writer);
				}
			}

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

			//UpdateMetadata
			writer.Write(updateMetadata);

			//ReplaceFullIndex
			writer.Write(replaceFullIndex);

            //PreserveData
            writer.Write(preserveData);

            //IndexVirtualCountMapping
            if (indexVirtualCountMapping == null || indexVirtualCountMapping.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)indexVirtualCountMapping.Count);
                foreach (KeyValuePair<string /*IndexName*/, int /*VirtualCount*/> kvp in indexVirtualCountMapping)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            //PrimaryId
            writer.Write(primaryId);
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
			//IndexId
			ushort len = reader.ReadUInt16();
			if (len > 0)
			{
				indexId = reader.ReadBytes(len);
			}

			//TargetIndexName
			targetIndexName = reader.ReadString();

			//IndexTagMapping
			ushort count = reader.ReadUInt16();
			indexTagMapping = new Dictionary<string, List<string>>(count);
			if (count > 0)
			{
				string indexName;
				ushort tagNameListCount;
				List<string> tagNameList;

				for (ushort i = 0; i < count; i++)
				{
					indexName = reader.ReadString();
					tagNameListCount = reader.ReadUInt16();
					tagNameList = new List<string>();
					for (ushort j = 0; j < tagNameListCount; j++)
					{
						tagNameList.Add(reader.ReadString());
					}
					indexTagMapping.Add(indexName, tagNameList);
				}
			}

			//AddList
			int listCount = reader.ReadInt32();
			addList = new List<IndexDataItem>(listCount);
			IndexDataItem indexDataItem;
			for (int i = 0; i < listCount; i++)
			{
				indexDataItem = new IndexDataItem();
				indexDataItem.Deserialize(reader);
				addList.Add(indexDataItem);
			}

			//DeleteList
			listCount = reader.ReadInt32();
			deleteList = new List<IndexItem>(listCount);
			IndexItem indexItem;
			for (int i = 0; i < listCount; i++)
			{
				indexItem = new IndexItem();
				indexItem.Deserialize(reader);
				deleteList.Add(indexItem);
			}

			//Metadata
			len = reader.ReadUInt16();
			if (len > 0)
			{
				metadata = reader.ReadBytes(len);
			}

			//UpdateMetadata
			updateMetadata = reader.ReadBoolean();

			//ReplaceFullIndex
			replaceFullIndex = reader.ReadBoolean();

            if (version >= 2)
            {
                //PreserveData
                preserveData = reader.ReadBoolean();
            }

            if(version >= 3)
            {
                //IndexVirtualCountMapping
                count = reader.ReadUInt16();
                if (count > 0)
                {
                    indexVirtualCountMapping = new Dictionary<string, int>(count);
                    string indexName;
                    int virtualCount;

                    for (ushort i = 0; i < count; i++)
                    {
                        indexName = reader.ReadString();
                        virtualCount = reader.ReadInt32();
                        indexVirtualCountMapping.Add(indexName, virtualCount);
                    }
                }
            }

            if (version >= 4)
            {
                //PrimaryId
                primaryId = reader.ReadInt32();
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
