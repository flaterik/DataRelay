using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MySpace.Common.CompactSerialization.IO;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Enums;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    internal static class IndexServerUtils
    {
        /// <summary>
        /// Gets the CacheIndexInternal.
        /// </summary>
        /// <param name="storeContext">The store context.</param>
        /// <param name="typeId">The type id.</param>
        /// <param name="primaryId">The primary id.</param>
        /// <param name="indexId">The index id.</param>
        /// <param name="extendedIdSuffix">The extended id suffix.</param>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="count">The count.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="inclusiveFilter">if set to <c>true</c> includes the items that pass the filter; otherwise , <c>false</c>.</param>
        /// <param name="indexCondition">The index condition.</param>
        /// <param name="deserializeHeaderOnly">if set to <c>true</c> if just CacheIndexInternal header is to be deserialized; otherwise, <c>false</c>.</param>
        /// <param name="getFilteredItems">if set to <c>true</c> get filtered items; otherwise, <c>false</c>.</param>
        /// <param name="primarySortInfo">The primary sort info.</param>
        /// <param name="localIdentityTagNames">The local identity tag names.</param>
        /// <param name="stringHashCodeDictionary">The string hash code dictionary.</param>
        /// <param name="capCondition">The cap condition.</param>
        /// <returns>CacheIndexInternal</returns>
        internal static CacheIndexInternal GetCacheIndexInternal(IndexStoreContext storeContext,
            short typeId,
            int primaryId,
            byte[] indexId,
            short extendedIdSuffix,
            string indexName,
            int count,
            Filter filter,
            bool inclusiveFilter,
            IndexCondition indexCondition,
            bool deserializeHeaderOnly,
            bool getFilteredItems,
            PrimarySortInfo primarySortInfo,
            List<string> localIdentityTagNames,
            Dictionary<int, bool> stringHashCodeDictionary,
            CapCondition capCondition)
        {
            CacheIndexInternal cacheIndexInternal = null;
            byte[] extendedId = FormExtendedId(indexId, extendedIdSuffix);
            RelayMessage getMsg = new RelayMessage(typeId, primaryId, extendedId, MessageType.Get);
            storeContext.IndexStorageComponent.HandleMessage(getMsg);

            if (getMsg.Payload != null) //CacheIndex exists
            {
                cacheIndexInternal = new CacheIndexInternal
                                         {
                                             InDeserializationContext = new InDeserializationContext
                                                                            {
                                                                                TypeId = getMsg.TypeId,
                                                                                TagHashCollection = storeContext.TagHashCollection,
                                                                                IndexId = indexId,
                                                                                IndexName = indexName,
                                                                                MaxItemsPerIndex = count,
                                                                                Filter = filter,
                                                                                InclusiveFilter = inclusiveFilter,
                                                                                IndexCondition = indexCondition,
                                                                                DeserializeHeaderOnly = deserializeHeaderOnly,
                                                                                CollectFilteredItems = getFilteredItems,
                                                                                PrimarySortInfo = primarySortInfo,
                                                                                LocalIdentityTagNames = localIdentityTagNames,
                                                                                StringHashCollection = storeContext.StringHashCollection,
                                                                                StringHashCodeDictionary = stringHashCodeDictionary,
                                                                                CapCondition = capCondition
                                                                            }
                                         };

                // This mess is required until Moods 2.0 migrated to have IVersionSerializable version of CacheIndexInternal
                // ** TBD - Should be removed later
                if (LegacySerializationUtil.Instance.IsSupported(getMsg.TypeId))
                {
                    MemoryStream stream = new MemoryStream(getMsg.Payload.ByteArray);
                    cacheIndexInternal.Deserialize(new CompactBinaryReader(stream));
                }
                else
                {
                    getMsg.GetObject(cacheIndexInternal);
                }
            }

            return cacheIndexInternal;
        }

        /// <summary>
        /// Gets the query metadata.
        /// </summary>
        /// <param name="internalCacheIndexList">The internal cache index list.</param>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="typeId">The type id.</param>
        /// <param name="primaryId">The primary id.</param>
        /// <param name="extendedId">The extended id.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns></returns>
        internal static byte[] GetQueryMetadata(List<CacheIndexInternal> internalCacheIndexList,
            IndexTypeMapping indexTypeMapping,
            short typeId,
            int primaryId,
            byte[] extendedId,
            IndexStoreContext storeContext)
        {
            if (indexTypeMapping.MetadataStoredSeperately)
            {
                #region Check if metadata is stored seperately
                //Send a get message to local index storage and fetch index for IndexId
                RelayMessage getMsg = new RelayMessage(typeId, primaryId, extendedId, MessageType.Get);
                storeContext.IndexStorageComponent.HandleMessage(getMsg);

                if (getMsg.Payload != null)
                {
                    return getMsg.Payload.ByteArray;
                }
                #endregion
            }
            else
            {
                #region Check metadata on indexes
                foreach (CacheIndexInternal cacheIndexInternal in internalCacheIndexList)
                {
                    if (indexTypeMapping.IndexCollection[cacheIndexInternal.InDeserializationContext.IndexName].MetadataPresent)
                    {
                        return cacheIndexInternal.Metadata;
                    }
                }
                #endregion
            }
            return null;
        }

        /// <summary>
        /// Gets the tags.
        /// </summary>
        /// <param name="cacheIndexInternal">The cache index internal.</param>
        /// <param name="searchItem">The search item.</param>
        /// <param name="resultItem">The result item.</param>
        internal static void GetTags(CacheIndexInternal cacheIndexInternal, IndexItem searchItem, IndexItem resultItem)
        {
            int searchIndex = cacheIndexInternal.Search(searchItem);
            if (searchIndex > -1)
            {
                IndexItem tempIndexItem = InternalItemAdapter.ConvertToIndexItem(cacheIndexInternal.GetItem(searchIndex), cacheIndexInternal.InDeserializationContext);

                foreach (KeyValuePair<string, byte[]> kvp in tempIndexItem.Tags)
                {
                    if (!resultItem.Tags.ContainsKey(kvp.Key))
                    {
                        resultItem.Tags.Add(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the printable CacheIndexInternalList.
        /// </summary>
        /// <param name="internalIndexList">The internal index list.</param>
        /// <returns></returns>
        internal static string GetPrintableCacheIndexInternalList(List<CacheIndexInternal> internalIndexList, 
            TagHashCollection tagHashCollection, short typeId)
        {
            StringBuilder logStr = new StringBuilder();
            logStr.Append("CacheIndexInternalList Info").Append(Environment.NewLine);

            int itemCount;
            foreach (CacheIndexInternal cii in internalIndexList)
            {
                if (cii.InDeserializationContext != null && cii.InDeserializationContext.IndexId != null)
                {
                    //CacheIndexInternal Name
                    logStr.Append("IndexName : ").Append(cii.InDeserializationContext.IndexName);
                    logStr.Append(", IndexId : ").Append(IndexCacheUtils.GetReadableByteArray(cii.InDeserializationContext.IndexId)).Append(Environment.NewLine);
                }

                if (cii.InternalItemList != null)
                {
                    logStr.Append("CacheIndexInternal.InternalItemList.Count : ").Append(cii.InternalItemList.Count.ToString()).Append(Environment.NewLine);
                    itemCount = 0;
                    foreach (InternalItem internalItem in cii.InternalItemList)
                    {
                        LogItem(logStr, internalItem, itemCount++, tagHashCollection, typeId);                        
                    }
                }
                else
                {
                    logStr.Append("CacheIndexInternal.ItemIdList = null").Append(Environment.NewLine);
                }
            }
            return logStr.ToString();
        }

        /// <summary>
        /// Gets printable CacheIndex.
        /// </summary>
        /// <param name="clientIndex">client index.</param>
        /// <param name="tagHashCollection">tag Hash Collection.</param>
        /// <returns></returns>
        internal static string GetPrintableCacheIndex(CacheIndex clientIndex, TagHashCollection tagHashCollection, short typeId)
        {
            StringBuilder logStr = new StringBuilder();
            logStr.Append(Environment.NewLine).Append("Client Index Info").Append(Environment.NewLine);
            int itemCount;

            logStr.Append("IndexId : ").Append(IndexCacheUtils.GetReadableByteArray(clientIndex.IndexId)).
                Append(Environment.NewLine);

            //AddList
            if (clientIndex.AddList != null)
            {
                logStr.Append("ClientIndex.AddList.Count : ").Append(clientIndex.AddList.Count).Append(Environment.NewLine);

                itemCount = 0;
                foreach (IndexDataItem indexDataItem in clientIndex.AddList)
                {
                    LogItem(logStr, indexDataItem, itemCount, tagHashCollection, typeId);
                }
            }
            else
            {
                logStr.Append("ClientIndex.AddList = null").Append(Environment.NewLine);
            }

            //DeleteList
            if (clientIndex.DeleteList != null)
            {
                logStr.Append("ClientIndex.DeleteList.Count : ").Append(clientIndex.DeleteList.Count).Append(Environment.NewLine);

                itemCount = 0;
                foreach (IndexItem indexItem in clientIndex.DeleteList)
                {
                    LogItem(logStr, indexItem, itemCount, tagHashCollection, typeId);
                }
            }
            else
            {
                logStr.Append("ClientIndex.DeleteList = null").Append(Environment.NewLine);
            }
            return logStr.ToString();
        }
        
        private static void LogItem(StringBuilder logStr, IItem iItem, int itemCount, TagHashCollection tagHashCollection, short typeId)
        {           
            if (iItem.ItemId == null)
            {
                logStr.Append("ItemList[").Append(itemCount.ToString()).Append("].ItemId = null").Append(Environment.NewLine);
            }
            else if (iItem.ItemId.Length == 0)
            {
                logStr.Append("ItemList[").Append(itemCount.ToString()).Append("].ItemId.Length = 0").Append(Environment.NewLine);
            }
            else
            {
                logStr.Append("ItemList[").Append(itemCount.ToString()).Append("].ItemId = ").Append(IndexCacheUtils.GetReadableByteArray(iItem.ItemId)).Append(Environment.NewLine);
            }

            logStr.Append("Tags.Count : ");
            if (iItem is IndexItem)
            {
                var indexItem = (IndexItem)iItem;
                if (indexItem.Tags != null && indexItem.Tags.Count > 0)
                {
                    logStr.Append(indexItem.Tags.Count).Append(" - ");
                    foreach (var tag in indexItem.Tags)
                    {
                        logStr.Append(tag.Key).Append("=").Append(IndexCacheUtils.GetReadableByteArray(tag.Value)).
                            Append(", ");
                    }
                }
                else
                {
                    logStr.Append("0");
                }
            }
            else if (iItem is InternalItem)
            {
                var internalItem = (InternalItem)iItem;
                if (internalItem.TagList != null && internalItem.TagList.Count > 0)
                {
                    logStr.Append(internalItem.TagList.Count).Append(" - ");
                    foreach (var tag in internalItem.TagList)
                    {
                        logStr.Append(tagHashCollection.GetTagName(typeId, tag.Key)).
                            Append("=").Append(IndexCacheUtils.GetReadableByteArray(tag.Value)).
                            Append(", ");
                    }
                }
                else
                {
                    logStr.Append("0");
                }
            }
            logStr.Append(Environment.NewLine);
        }

        /// <summary>
        /// Forms the extended id.
        /// </summary>
        /// <param name="indexId">The index id.</param>
        /// <param name="extendedIdSuffix">The extended id suffix.</param>
        /// <returns></returns>
        internal static byte[] FormExtendedId(byte[] indexId, short extendedIdSuffix)
        {
            byte[] extendedId = new byte[indexId.Length + 1];

            Array.Copy(indexId, 0, extendedId, 0, indexId.Length);
            Array.Copy(BitConverter.GetBytes(extendedIdSuffix), 0, extendedId, indexId.Length, 1);

            return extendedId;
        }
    }
}