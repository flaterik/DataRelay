using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MySpace.BinaryStorage;
using MySpace.BinaryStorage.Store.BerkeleyStore.PerfCounter;
using MySpace.Common.CompactSerialization.IO;
using MySpace.Common.IO;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.DomainSpecificConfigs;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using System.Net;
using MySpace.ResourcePool;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    internal static class IndexServerUtils
    {
        private static readonly unsafe int BdbHeaderSize = sizeof(PayloadStorage);

        /// <summary>
        /// Gets the CacheIndexInternal.
        /// </summary>
        /// <param name="storeContext">The store context.</param>
        /// <param name="typeId">The type id.</param>
        /// <param name="primaryId">The primary id.</param>
        /// <param name="indexId">The index id.</param>
        /// <param name="extendedIdSuffix">The extended id suffix.</param>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="maxItemsPerIndex">The maxItemsPerIndex.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="inclusiveFilter">if set to <c>true</c> includes the items that pass the filter; otherwise , <c>false</c>.</param>
        /// <param name="indexCondition">The index condition.</param>
        /// <param name="deserializeHeaderOnly">if set to <c>true</c> if just CacheIndexInternal header is to be deserialized; otherwise, <c>false</c>.</param>
        /// <param name="getFilteredItems">if set to <c>true</c> get filtered items; otherwise, <c>false</c>.</param>
        /// <param name="primarySortInfo">The primary sort info.</param>
        /// <param name="localIdentityTagNames">The local identity tag names.</param>
        /// <param name="stringHashCodeDictionary">The string hash code dictionary.</param>
        /// <param name="capCondition">The cap condition.</param>
        /// <param name="isMetadataPropertyCollection">if set to <c>true</c> metadata represents a property collection; otherwise , <c>false</c>.</param>
        /// <param name="metadataPropertyCollection">The MetadataPropertyCollection.</param>
        /// <param name="domainSpecificProcessingType">The DomainSpecificProcessingType.</param>
        /// <param name="domainSpecificConfig">The DomainSpecificConfig.</param>
        /// <param name="getDistinctValuesFieldName">The distinct value field name.</param>
        /// <param name="groupBy">The GroupBy clause.</param>
        /// <param name="forceFullGet">indicate whether or not use full get.</param>
        /// <returns>CacheIndexInternal</returns>
        internal static CacheIndexInternal GetCacheIndexInternal(IndexStoreContext storeContext,
            short typeId,
            int primaryId,
            byte[] indexId,
            short extendedIdSuffix,
            string indexName,
            int maxItemsPerIndex,
            Filter filter,
            bool inclusiveFilter,
            IndexCondition indexCondition,
            bool deserializeHeaderOnly,
            bool getFilteredItems,
            PrimarySortInfo primarySortInfo,
            List<string> localIdentityTagNames,
            Dictionary<int, bool> stringHashCodeDictionary,
            CapCondition capCondition,
            bool isMetadataPropertyCollection,
            MetadataPropertyCollection metadataPropertyCollection,
            DomainSpecificProcessingType domainSpecificProcessingType,
            DomainSpecificConfig domainSpecificConfig,
            string getDistinctValuesFieldName,
            GroupBy groupBy,
            bool forceFullGet)
        {
            CacheIndexInternal cacheIndexInternal = null;
            byte[] extendedId = FormExtendedId(indexId, extendedIdSuffix);
            //RelayMessage getMsg = new RelayMessage(typeId, primaryId, extendedId, MessageType.Get);
            //storeContext.IndexStorageComponent.HandleMessage(getMsg);

            Stream myStream;

            ResourcePoolItem<MemoryStream> pooledStreamItem = null;
            MemoryStream pooledStream;

            try
            {

                int indexLevelGetSize =
                    storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[typeId].
                        IndexCollection[indexName].PartialGetSizeInBytes;

                bool isFullGet = forceFullGet ? true : (indexLevelGetSize <= 0 && storeContext.PartialGetLength <= 0);

                if (isFullGet)
                {
                    byte[] cacheBytes = BinaryStorageAdapter.Get(
                        storeContext.IndexStorageComponent,
                        typeId,
                        primaryId,
                        extendedId);

                    if (cacheBytes != null)
                    {
                        myStream = new MemoryStream(cacheBytes);
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    int partialGetSize = indexLevelGetSize == 0 ? storeContext.PartialGetLength : indexLevelGetSize;

                    // get a memory stream from the memory pool
                    pooledStreamItem = storeContext.MemoryPool.GetItem();
                    pooledStream = pooledStreamItem.Item;

                    myStream = new SmartStream(
                        storeContext.IndexStorageComponent,
                        typeId,
                        primaryId,
                        extendedId,
                        partialGetSize,
                        pooledStream);

                    BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                        BerkeleyBinaryStorePerformanceCounterEnum.PartialGetPerSec,
                        1);
                }

                if (myStream.Length > 0) //CacheIndex exists, this check is just for SmartStream
                {
                    cacheIndexInternal = new CacheIndexInternal
                                             {
                                                 InDeserializationContext =
                                                     new InDeserializationContext(maxItemsPerIndex,
                                                                                  indexName,
                                                                                  indexId,
                                                                                  typeId,
                                                                                  filter,
                                                                                  inclusiveFilter,
                                                                                  storeContext.TagHashCollection,
                                                                                  deserializeHeaderOnly,
                                                                                  getFilteredItems,
                                                                                  primarySortInfo,
                                                                                  localIdentityTagNames,
                                                                                  storeContext.StringHashCollection,
                                                                                  stringHashCodeDictionary,
                                                                                  indexCondition,
                                                                                  capCondition,
                                                                                  isMetadataPropertyCollection,
                                                                                  metadataPropertyCollection,
                                                                                  domainSpecificProcessingType,
                                                                                  domainSpecificConfig,
                                                                                  getDistinctValuesFieldName,
                                                                                  groupBy)
                    };

                    if (!isFullGet)
                    {
                        // skip the bdb entry header
                        myStream.Read(new byte[BdbHeaderSize], 0, BdbHeaderSize);
                    }

                    // This mess is required until Moods 2.0 migrated to have IVersionSerializable version of CacheIndexInternal
                    // ** TBD - Should be removed later
                    if (LegacySerializationUtil.Instance.IsSupported(typeId))
                    {
                        cacheIndexInternal.Deserialize(new CompactBinaryReader(myStream));
                    }
                    else
                    {
                        int version = myStream.ReadByte();

                        try
                        {
                            cacheIndexInternal.Deserialize(new CompactBinaryReader(myStream), version);
                        }
                        catch (Exception ex)
                        {
                            LoggingUtil.Log.ErrorFormat(
                                "The deserialization has an exception: primary id : {0}, index id : {1}, extendedid : {2}, extendedIdSuffix : {3}, version : {4} info : {5}",
                                primaryId,
                                ByteArrayToString(indexId, 0),
                                ByteArrayToString(extendedId, 0),
                                extendedIdSuffix,
                                version,
                                ex.ToString());

                            if (myStream.Length > 0)
                            {
                                myStream.Seek(0, SeekOrigin.Begin);
                                byte[] ba = new byte[10];
                                myStream.Read(ba, 0, 10);

                                LoggingUtil.Log.ErrorFormat("The first 10 bytes of the stream are {0}",
                                                            ByteArrayToString(ba, 0));
                            }

                            throw;
                        }
                    }

                    // update SmartStream perf counters
                    if (!isFullGet)
                    {
                        SmartStream mySmartStream = (SmartStream)myStream;

                        BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                            BerkeleyBinaryStorePerformanceCounterEnum.AvgDbGetPerPartialGet,
                            mySmartStream.DatabaseAccessTime);

                        BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                            BerkeleyBinaryStorePerformanceCounterEnum.AvgDbGetPerPartialGetBase,
                            1);

                        BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                            BerkeleyBinaryStorePerformanceCounterEnum.AvgBytesPerPartialGet,
                            mySmartStream.Position);

                        BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                            BerkeleyBinaryStorePerformanceCounterEnum.AvgBytesPerPartialGetBase,
                            1);
                    }
                }
            }
            finally
            {
                // release the pooled stream
                if (storeContext.MemoryPool != null && pooledStreamItem != null)
                {
                    storeContext.MemoryPool.ReleaseItem(pooledStreamItem);
                }
            }

            return cacheIndexInternal;
        }

        /// <summary>
        /// Gets the MetadataPropertyCollection from raw byte array
        /// </summary>
        /// <param name="msg">The RelayMessage.</param>
        /// <returns></returns>
        internal static MetadataPropertyCollection GetMetadataPropertyCollection(RelayMessage msg)
        {
            MetadataPropertyCollection metadataPropertyCollection = null;
            if (msg.Payload != null)
            {
                metadataPropertyCollection = new MetadataPropertyCollection();
                msg.GetObject(metadataPropertyCollection);
            }
            return metadataPropertyCollection;
        }

        // This is a help function for debuigging purpose
        internal static string ByteArrayToString(byte[] myByteArray, int count)
        {
            int printLength = count <= 0 ? myByteArray.Length : count;

            if (printLength > myByteArray.Length)
            {
                printLength = myByteArray.Length;
            }

            StringBuilder resultBuilder = new StringBuilder();

            for (int i = 0; i < printLength; i++)
            {
                // output the byte array in hex
                resultBuilder.Append(myByteArray[i].ToString("x2"));
            }

            return resultBuilder.ToString();
        }

        /// <summary>
        /// Gets the MetadataPropertyCollection from raw byte array
        /// </summary>
        /// <param name="payload">The RelayMessage.</param>
        /// <returns></returns>
        internal static MetadataPropertyCollection GetMetadataPropertyCollection(byte[] payload)
        {
            MetadataPropertyCollection metadataPropertyCollection = new MetadataPropertyCollection();

            if (payload != null)
            {
                MemoryStream stream = new MemoryStream(payload);
                Serializer.Deserialize(stream, metadataPropertyCollection);
            }

            return metadataPropertyCollection;
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
        /// get the printable index list string
        /// </summary>
        /// <param name="internalIndexList">index list</param>
        /// <param name="tagHashCollection">tag hash collection</param>
        /// <param name="typeId">type id</param>
        /// <returns>string represent internal list</returns>
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
                    logStr.Append("CacheIndexInternal.VirtualCount : ").Append(cii.VirtualCount.ToString()).Append(Environment.NewLine);
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
        /// <param name="typeId">type id.</param>
        /// <returns></returns>
        internal static string GetPrintableCacheIndex(CacheIndex clientIndex, TagHashCollection tagHashCollection, short typeId)
        {
            StringBuilder logStr = new StringBuilder();
            logStr.Append(Environment.NewLine).Append("Client Index Info").Append(Environment.NewLine);
            int itemCount;

            logStr.Append("IndexId : ").Append(IndexCacheUtils.GetReadableByteArray(clientIndex.IndexId)).Append(Environment.NewLine);
            logStr.Append("Metadata : ").Append(clientIndex.Metadata == null ? "Null" : IndexCacheUtils.GetReadableByteArray(clientIndex.Metadata)).Append(Environment.NewLine);
            logStr.Append("TargetIndexName : ").Append(clientIndex.TargetIndexName).Append(Environment.NewLine);
            logStr.Append("IndexTagmapping : ").Append(clientIndex.IndexTagMapping == null ? "Null" : clientIndex.IndexTagMapping.Count.ToString()).Append(Environment.NewLine);
            if(clientIndex.IndexTagMapping != null && clientIndex.IndexTagMapping.Count > 0)
            {
                logStr.Append("IndexTagmapping Items : ");
                foreach (var indexTag in clientIndex.IndexTagMapping)
                {
                    logStr.Append("IndexName : ").Append(indexTag.Key).Append(" Tags : ");
                    foreach (var tag in indexTag.Value)
                    {
                        logStr.Append(tag).Append(" , ");
                    }
                }
                logStr.Append(Environment.NewLine);
            }

            //AddList))
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

        /// <summary>
        /// If the target index name is null, if there is only 1 index for this type id,
        /// then the index name will be assigned. Otherwise, exception will be thrown.
        /// </summary>
        /// <param name="indexTypeMapping">index type mapping</param>
        internal static string CheckQueryTargetIndexName(IndexTypeMapping indexTypeMapping)
        {
            if (indexTypeMapping.IndexCollection.Count != 1)
            {
                throw new Exception(string.Format("Null or Empty TargetIndexName, and Type {0} has more than 1 index names ", indexTypeMapping.TypeId));
            }

            return indexTypeMapping.IndexCollection[0].IndexName;
        }

        /// <summary>
        /// Gets the metadata when its stored seperately.
        /// </summary>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="typeId">The type id.</param>
        /// <param name="primaryId">The primary id.</param>
        /// <param name="extendedId">The extended id.</param>
        /// <param name="storeContext">The store context.</param>
        /// <param name="metadata">The Metadata.</param>
        /// <param name="metadataPropertyCollection">The MetadataPropertyCollection.</param>
        internal static void GetMetadataStoredSeperately(IndexTypeMapping indexTypeMapping,
            short typeId,
            int primaryId,
            byte[] extendedId,
            IndexStoreContext storeContext,
            out byte[] metadata,
            out MetadataPropertyCollection metadataPropertyCollection)
        {
            metadata = null;
            metadataPropertyCollection = null;

            byte[] metadataBytes = BinaryStorageAdapter.Get(storeContext.IndexStorageComponent, typeId, primaryId, extendedId);

            if (metadataBytes != null)
            {
                if (indexTypeMapping.IsMetadataPropertyCollection)
                {
                    metadataPropertyCollection = GetMetadataPropertyCollection(metadataBytes);
                }
                else
                {
                    metadata = metadataBytes;
                }
            }
        }

        /// <summary>
        /// Gets the metadata when its stored along with the index.
        /// </summary>
        /// <param name="internalCacheIndexList">The internal cache index list.</param>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="metadata">The Metadata.</param>
        /// <param name="metadataPropertyCollection">The MetadataPropertyCollection.</param>
        internal static void GetMetadataStoredWithIndex(IndexTypeMapping indexTypeMapping,
            List<CacheIndexInternal> internalCacheIndexList,
            out byte[] metadata,
            out MetadataPropertyCollection metadataPropertyCollection)
        {
            metadata = null;
            metadataPropertyCollection = null;
            foreach (CacheIndexInternal cacheIndexInternal in internalCacheIndexList)
            {
                Index indexInfo = indexTypeMapping.IndexCollection[cacheIndexInternal.InDeserializationContext.IndexName];
                if (indexInfo.MetadataPresent)
                {
                    if (indexInfo.IsMetadataPropertyCollection)
                    {
                        metadataPropertyCollection = cacheIndexInternal.MetadataPropertyCollection;
                    }
                    else
                    {
                        metadata = cacheIndexInternal.Metadata;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the index header.
        /// </summary>
        /// <param name="targetIndex">Index of the target.</param>
        /// <param name="indexTypeMapping">The index type mapping.</param>
        /// <param name="typeId">The type id.</param>
        /// <param name="primaryId">The primary id.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>IndexHeader</returns>
        internal static IndexHeader GetIndexHeader(CacheIndexInternal targetIndex,
            IndexTypeMapping indexTypeMapping,
            short typeId,
            int primaryId,
            IndexStoreContext storeContext)
        {
            MetadataPropertyCollection metadataPropertyCollection;
            byte[] metadata;

            if (indexTypeMapping.MetadataStoredSeperately)
            {
                GetMetadataStoredSeperately(indexTypeMapping,
                                            typeId,
                                            primaryId,
                                            targetIndex.InDeserializationContext.IndexId,
                                            storeContext,
                                            out metadata,
                                            out metadataPropertyCollection);
            }
            else
            {
                GetMetadataStoredWithIndex(indexTypeMapping,
                    new List<CacheIndexInternal> {targetIndex},
                    out metadata,
                    out metadataPropertyCollection);
            }

            return new IndexHeader
            {
                Metadata = metadata,         
                MetadataPropertyCollection = metadataPropertyCollection,
                VirtualCount = targetIndex.VirtualCount
            };
        }

        /// <summary>
        /// Formats the address history.
        /// </summary>
        /// <param name="addressHistory">The address history.</param>
        /// <returns>String containing formatted address history</returns>
        internal static string FormatAddressHistory(List<IPAddress> addressHistory)
        {
            string retVal = "";
            if (addressHistory != null && addressHistory.Count > 0)
            {
                if (addressHistory.Count == 1)
                {
                    retVal = addressHistory[0].ToString();
                }
                else
                {
                    var stb = new StringBuilder();
                    for (int i = 0; i < addressHistory.Count; i++)
                    {
                        stb.Append(addressHistory[i]).Append(", ");
                    }
                    retVal = stb.ToString();
                }
            }
            return retVal;
        }


        /// <summary>
        /// Formats the index ids.
        /// </summary>
        /// <param name="indexIds">The index ids.</param>
        /// <returns>formatted index ids</returns>
        internal static string FormatIndexIds(ICollection<byte[]> indexIds)
        {
            var stb = new StringBuilder();
            if (indexIds == null)
            {
                stb.Append("null");
            }
            else
            {
                stb.Append("Count: ").Append(indexIds.Count).Append(" IndexIds: ");
                foreach (var indexId in indexIds)
                {
                    stb.Append(IndexCacheUtils.GetReadableByteArray(indexId)).Append(",  ");
                }
            }

            return stb.ToString();
        }
    }
}
