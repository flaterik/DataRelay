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

        /// <summary>
        /// Gets or sets the index id.
        /// </summary>
        /// <value>The index id.</value>
        public byte[] IndexId { get; set; }

        /// <summary>
        /// Gets or sets the name of the target index.
        /// </summary>
        /// <value>The name of the target index.</value>
        public string TargetIndexName { get; private set; }

        /// <summary>
        /// Gets the mapping of IndexName and TagNameList.
        /// </summary>
        /// <value>The index tag mapping.</value>
        public Dictionary<string /*IndexName*/, List<string> /*TagNameList*/> IndexTagMapping { get; private set; }

        /// <summary>
        /// Gets or sets the add list. The lists are processed in the following order - UpdateList, DeleteList, AddList
        /// </summary>
        /// <value>The add list.</value>
        public List<IndexDataItem> AddList { get; set; }

        /// <summary>
        /// Gets or sets the delete list. The lists are processed in the following order - UpdateList, DeleteList, AddList
        /// </summary>
        /// <value>The delete list.</value>
        public List<IndexItem> DeleteList { get; set; }

        /// <summary>
        /// Gets or sets the metadata.
        /// </summary>
        /// <value>The metadata.</value>
        public byte[] Metadata { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether metadata or the MetadataPropertyCollectionUpdate should be processed.
        /// </summary>
        /// <value><c>true</c> if metadata or MetadataPropertyCollectionUpdate should be processed; otherwise, <c>false</c>.</value>
        public bool UpdateMetadata { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to replace the existing index.
        /// </summary>
        /// <value><c>true</c> if the existing index is to be replaced; otherwise, <c>false</c>.</value>
        public bool ReplaceFullIndex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the existing data should be preserved.
        /// </summary>
        /// <value><c>true</c> if existing data is to be preserved; otherwise, <c>false</c>.</value>
        public bool PreserveData { get; set; }

        /// <summary>
        /// Gets or sets the mapping of IndexName and Virtual Counts.
        /// </summary>
        /// <value>The index virtual count mapping.</value>
        public Dictionary<string /*IndexName*/, int /*VirtualCount*/> IndexVirtualCountMapping { get; set; }

        /// <summary>
        /// Gets or sets the metadata property collection update.
        /// </summary>
        /// <value>The metadata property collection update.</value>
        public MetadataPropertyCollectionUpdate MetadataPropertyCollectionUpdate { get; set; }

        /// <summary>
        /// Gets or sets the update list. The lists are processed in the following order - UpdateList, DeleteList, AddList
        /// </summary>
        /// <value>The update list.</value>
        public List<IndexDataItem> UpdateList { get; set; }

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
            IndexId = indexId;
            TargetIndexName = targetIndexName;
            IndexTagMapping = indexTagMapping;
            AddList = addList;
            DeleteList = deleteList;
            Metadata = metadata;
            UpdateMetadata = updateMetadata;
            ReplaceFullIndex = replaceFullIndex;
            PreserveData = preserveData;
            IndexVirtualCountMapping = indexVirtualCountMapping;
            this.primaryId = primaryId;
        }

        #endregion

        #region IExtendedRawCacheParameter Members

        public byte[] ExtendedId
        {
            get
            {
                return IndexId;
            }
            set
            {
                throw new Exception("Setter for 'CacheIndex.ExtendedId' is not implemented and should not be invoked!");
            }
        }

        public DateTime? LastUpdatedDate { get; set; }

        #endregion

        #region ICacheParameter Members

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
            if (IndexId == null || IndexId.Length == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)IndexId.Length);
                writer.Write(IndexId);
            }

            //TargetIndexName
            writer.Write(TargetIndexName);

            //IndexTagMapping
            if (IndexTagMapping == null || IndexTagMapping.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)IndexTagMapping.Count);
                foreach (KeyValuePair<string /*IndexName*/, List<string> /*TagNameList*/> kvp in IndexTagMapping)
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
            SerializeIndexDataItemList(writer, AddList);

            //DeleteList
            if (DeleteList == null || DeleteList.Count == 0)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(DeleteList.Count);
                foreach (IndexItem indexItem in DeleteList)
                {
                    indexItem.Serialize(writer);
                }
            }

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

            //UpdateMetadata
            writer.Write(UpdateMetadata);

            //ReplaceFullIndex
            writer.Write(ReplaceFullIndex);

            //PreserveData
            writer.Write(PreserveData);

            //IndexVirtualCountMapping
            if (IndexVirtualCountMapping == null || IndexVirtualCountMapping.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)IndexVirtualCountMapping.Count);
                foreach (KeyValuePair<string /*IndexName*/, int /*VirtualCount*/> kvp in IndexVirtualCountMapping)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            //PrimaryId
            writer.Write(primaryId);

            //MetadataPropertyCollectionUpdate
            if (MetadataPropertyCollectionUpdate == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, MetadataPropertyCollectionUpdate);
            }

            //UpdateList
            SerializeIndexDataItemList(writer, UpdateList);
        }

        private static void SerializeIndexDataItemList(IPrimitiveWriter writer, List<IndexDataItem> indexDataItemList)
        {
            if (indexDataItemList == null || indexDataItemList.Count == 0)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(indexDataItemList.Count);
                foreach (IndexDataItem indexDataItem in indexDataItemList)
                {
                    indexDataItem.Serialize(writer);
                }
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //IndexId
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                IndexId = reader.ReadBytes(len);
            }

            //TargetIndexName
            TargetIndexName = reader.ReadString();

            //IndexTagMapping
            ushort count = reader.ReadUInt16();
            IndexTagMapping = new Dictionary<string, List<string>>(count);
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
                    IndexTagMapping.Add(indexName, tagNameList);
                }
            }

            //AddList
            AddList = DeserializeIndexDataItemList(reader);

            //DeleteList
             int listCount = reader.ReadInt32();
            DeleteList = new List<IndexItem>(listCount);
            IndexItem indexItem;
            for (int i = 0; i < listCount; i++)
            {
                indexItem = new IndexItem();
                indexItem.Deserialize(reader);
                DeleteList.Add(indexItem);
            }

            //Metadata
            len = reader.ReadUInt16();
            if (len > 0)
            {
                Metadata = reader.ReadBytes(len);
            }

            //UpdateMetadata
            UpdateMetadata = reader.ReadBoolean();

            //ReplaceFullIndex
            ReplaceFullIndex = reader.ReadBoolean();

            if (version >= 2)
            {
                //PreserveData
                PreserveData = reader.ReadBoolean();
            }

            if (version >= 3)
            {
                //IndexVirtualCountMapping
                count = reader.ReadUInt16();
                if (count > 0)
                {
                    IndexVirtualCountMapping = new Dictionary<string, int>(count);
                    string indexName;
                    int virtualCount;

                    for (ushort i = 0; i < count; i++)
                    {
                        indexName = reader.ReadString();
                        virtualCount = reader.ReadInt32();
                        IndexVirtualCountMapping.Add(indexName, virtualCount);
                    }
                }
            }

            if (version >= 4)
            {
                //PrimaryId
                primaryId = reader.ReadInt32();
            }

            if (version >= 5)
            {
                //MetadataPropertyCollectionUpdate
                if (reader.ReadBoolean())
                {
                    MetadataPropertyCollectionUpdate = new MetadataPropertyCollectionUpdate();
                    Serializer.Deserialize(reader.BaseStream, MetadataPropertyCollectionUpdate);
                }
            }

            if (version >= 6)
            {
                //UpdateList
                UpdateList = DeserializeIndexDataItemList(reader);
            }
        }

        private static List<IndexDataItem> DeserializeIndexDataItemList(IPrimitiveReader reader)
        {
            int listCount = reader.ReadInt32();
            List<IndexDataItem> indexDataItemList = new List<IndexDataItem>(listCount);
            IndexDataItem indexDataItem;
            for (int i = 0; i < listCount; i++)
            {
                indexDataItem = new IndexDataItem();
                indexDataItem.Deserialize(reader);
                indexDataItemList.Add(indexDataItem);
            }
            return indexDataItemList;
        }

        private const int CURRENT_VERSION = 6;
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
