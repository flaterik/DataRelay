using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MySpace.Common.CompactSerialization.IO;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class SaveProcessor
    {
        /// <summary>
        /// Processes the specified cache index.
        /// </summary>
        /// <param name="cacheIndex">Index of the cache.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        internal static void Process(CacheIndex cacheIndex, MessageContext messageContext, IndexStoreContext storeContext)
        {
            lock (LockingUtil.Instance.GetLock(messageContext.PrimaryId))
            {
                try
                {
                    IndexTypeMapping indexTypeMapping =
                        storeContext.StorageConfiguration.CacheIndexV3StorageConfig.IndexTypeMappingCollection[messageContext.TypeId];

                    #region Extract CacheIndex and Validate from incoming message

                    ValidateSave(cacheIndex);

                    #endregion

                    #region Log CacheIndex before processing
                    StringBuilder dbgIndexInfo = null;
                    if (LoggingUtil.Log.IsDebugEnabled)
                    {
                        dbgIndexInfo = new StringBuilder();
                        dbgIndexInfo.Append("TypeId=").Append(messageContext.TypeId).Append(Environment.NewLine);
                        dbgIndexInfo.Append(IndexServerUtils.GetPrintableCacheIndex(cacheIndex,
                            storeContext.TagHashCollection,
                            messageContext.TypeId));
                    }
                    #endregion

                    #region Init vars

                    List<RelayMessage> indexStorageMessageList = new List<RelayMessage>();
                    List<RelayMessage> dataStorageMessageList = new List<RelayMessage>();
                    List<CacheIndexInternal> internalIndexList = new List<CacheIndexInternal>();
                    List<IndexItem> cappedDeleteItemList = new List<IndexItem>();
                    CacheIndexInternal internalIndex;

                    #endregion

                    if (cacheIndex.IndexVirtualCountMapping == null)
                    {
                        #region Save Items

                        #region Extract CacheIndexInternal from index storage

                        if (cacheIndex.TargetIndexName == null) //Save to multiple indexes
                        {
                            #region Get CacheIndexInternal for multiple indexes

                            foreach (KeyValuePair<string /*IndexName*/, List<string> /*TagNameList*/> kvp in cacheIndex.IndexTagMapping)
                            {
                                Index indexInfo = indexTypeMapping.IndexCollection[kvp.Key];
                                internalIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                                    messageContext.TypeId,
                                    cacheIndex.PrimaryId,
                                    cacheIndex.IndexId,
                                    indexInfo.ExtendedIdSuffix,
                                    kvp.Key,
                                    0,
                                    null,
                                    true,
                                    null,
                                    false,
                                    false,
                                    indexInfo.PrimarySortInfo,
                                    indexInfo.LocalIdentityTagList,
                                    indexInfo.StringHashCodeDictionary,
                                    null);

                                if (internalIndex != null)
                                {
                                    // update performance counter
                                    PerformanceCounters.Instance.SetCounterValue(
                                        PerformanceCounterEnum.NumberOfItemsInIndexPerSave,
                                        messageContext.TypeId,
                                        internalIndex.OutDeserializationContext.TotalCount);
                                }

                                if (internalIndex == null || cacheIndex.ReplaceFullIndex) //CacheIndexInternal does not exists or is to be discarded
                                {
                                    internalIndex = new CacheIndexInternal
                                                        {
                                                            InDeserializationContext = new InDeserializationContext
                                                                                           {
                                                                                               TypeId = messageContext.TypeId,
                                                                                               TagHashCollection = storeContext.TagHashCollection,
                                                                                               IndexId = cacheIndex.IndexId,
                                                                                               IndexName = kvp.Key,
                                                                                               InclusiveFilter = true,
                                                                                               PrimarySortInfo = indexInfo.PrimarySortInfo,
                                                                                               LocalIdentityTagNames = indexInfo.LocalIdentityTagList,
                                                                                               StringHashCollection = storeContext.StringHashCollection,
                                                                                               StringHashCodeDictionary = indexInfo.StringHashCodeDictionary
                                                                                           }
                                                        };
                                }

                                internalIndexList.Add(internalIndex);
                            }

                            #endregion
                        }
                        else //Save to single index
                        {
                            #region Get CacheIndexInternal for TargetIndexName

                            Index indexInfo = indexTypeMapping.IndexCollection[cacheIndex.TargetIndexName];

                            internalIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                                messageContext.TypeId,
                                cacheIndex.PrimaryId,
                                cacheIndex.IndexId,
                                indexInfo.ExtendedIdSuffix,
                                cacheIndex.TargetIndexName,
                                0,
                                null,
                                true,
                                null,
                                false,
                                false,
                                indexInfo.PrimarySortInfo,
                                indexInfo.LocalIdentityTagList,
                                indexInfo.StringHashCodeDictionary,
                                null);

                            if (internalIndex != null)
                            {
                                // update performance counter
                                PerformanceCounters.Instance.SetCounterValue(
                                    PerformanceCounterEnum.NumberOfItemsInIndexPerSave,
                                    messageContext.TypeId,
                                    internalIndex.OutDeserializationContext.TotalCount);
                            }

                            if (internalIndex == null || cacheIndex.ReplaceFullIndex) //CacheIndexInternal does not exists or is to be discarded
                            {
                                internalIndex = new CacheIndexInternal
                                {
                                    InDeserializationContext = new InDeserializationContext
                                                                             {
                                                                                 TypeId = messageContext.TypeId,
                                                                                 TagHashCollection = storeContext.TagHashCollection,
                                                                                 IndexId = cacheIndex.IndexId,
                                                                                 IndexName = cacheIndex.TargetIndexName,
                                                                                 InclusiveFilter = true,
                                                                                 PrimarySortInfo = indexInfo.PrimarySortInfo,
                                                                                 LocalIdentityTagNames = indexInfo.LocalIdentityTagList,
                                                                                 StringHashCollection = storeContext.StringHashCollection,
                                                                                 StringHashCodeDictionary = indexInfo.StringHashCodeDictionary
                                                                             }
                                };
                            }

                            internalIndexList.Add(internalIndex);

                            #endregion

                        }
                        #endregion

                        #region Log CacheIndexInternals before save
                        if (LoggingUtil.Log.IsDebugEnabled && dbgIndexInfo != null)
                        {
                            dbgIndexInfo.Append(Environment.NewLine).Append(string.Format("BEFORE SAVE {0}",
                                                        IndexServerUtils.GetPrintableCacheIndexInternalList(
                                                            internalIndexList,
                                                            storeContext.TagHashCollection,
                                                            messageContext.TypeId)));
                        }
                        #endregion

                        #region Process Delete and Add List
                        try
                        {
                            #region Process Delete List

                            if (cacheIndex.DeleteList.Count > 0 && !cacheIndex.ReplaceFullIndex)
                            {
                                ProcessDeleteList(internalIndexList, cacheIndex.DeleteList, messageContext.TypeId);
                            }

                            #endregion

                            #region Process Add List

                            if (cacheIndex.AddList.Count > 0 || cacheIndex.UpdateMetadata)
                            {
                                ProcessAddList(internalIndexList, cappedDeleteItemList, cacheIndex, storeContext, indexTypeMapping);
                            }

                            #endregion
                        }
                        catch
                        {
                            LoggingUtil.Log.Debug(IndexServerUtils.GetPrintableCacheIndexInternalList(internalIndexList, storeContext.TagHashCollection, messageContext.TypeId));
                            throw;
                        }
                        #endregion

                        #region Log CacheIndexInternals after save
                        if (LoggingUtil.Log.IsDebugEnabled && dbgIndexInfo != null)
                        {
                            dbgIndexInfo.Append(Environment.NewLine).Append(string.Format("AFTER SAVE {0}",
                                                        IndexServerUtils.GetPrintableCacheIndexInternalList(internalIndexList,
                                                            storeContext.TagHashCollection,
                                                            messageContext.TypeId)));
                        }
                        #endregion

                        #region Data store relay messages for deletes and saves

                        if (DataTierUtil.ShouldForwardToDataTier(messageContext.RelayTTL,
                            messageContext.SourceZone,
                            storeContext.MyZone,
                            indexTypeMapping.IndexServerMode) && !cacheIndex.PreserveData)
                        {
                            byte[] fullDataId;
                            short relatedTypeId;
                            if (!storeContext.TryGetRelatedIndexTypeId(messageContext.TypeId, out relatedTypeId))
                            {
                                LoggingUtil.Log.ErrorFormat("Invalid RelatedTypeId for TypeId - {0}", messageContext.TypeId);
                                throw new Exception("Invalid RelatedTypeId for TypeId - " + messageContext.TypeId);
                            }

                            #region Delete Messages

                            foreach (IndexItem indexItem in cacheIndex.DeleteList)
                            {
                                fullDataId = DataTierUtil.GetFullDataId(cacheIndex.IndexId, indexItem, indexTypeMapping.FullDataIdFieldList);
                                if (fullDataId != null)
                                {
                                    dataStorageMessageList.Add(new RelayMessage(relatedTypeId,
                                                                   IndexCacheUtils.GeneratePrimaryId(fullDataId),
                                                                   fullDataId,
                                                                   MessageType.Delete));
                                }
                            }

                            #endregion

                            #region Save Messages

                            foreach (IndexDataItem indexDataItem in cacheIndex.AddList)
                            {
                                fullDataId = DataTierUtil.GetFullDataId(cacheIndex.IndexId, indexDataItem, indexTypeMapping.FullDataIdFieldList);

                                if (fullDataId != null)
                                {
                                    dataStorageMessageList.Add(new RelayMessage(relatedTypeId,
                                                                    IndexCacheUtils.GeneratePrimaryId(fullDataId),
                                                                    fullDataId,
                                                                    DateTime.Now,
                                                                    indexDataItem.Data ?? new byte[0],
                                                                    storeContext.GetCompressOption(messageContext.TypeId),
                                                                    MessageType.Save));

                                    if (indexDataItem.Data == null || indexDataItem.Data.Length == 0)
                                    {

                                        LoggingUtil.Log.WarnFormat("Saving null data for TypeId: {0}, IndexId: {1}, ItemId: {2}, FullDataId: {3}, PrimaryId: {4}",
                                                                    relatedTypeId,
                                                                    IndexCacheUtils.GetReadableByteArray(cacheIndex.IndexId),
                                                                    IndexCacheUtils.GetReadableByteArray(indexDataItem.ItemId),
                                                                    IndexCacheUtils.GetReadableByteArray(fullDataId),
                                                                    IndexCacheUtils.GeneratePrimaryId(fullDataId));
                                    }

                                }
                            }

                            #endregion

                            #region Capped Item Delete Messages

                            foreach (IndexItem indexItem in cappedDeleteItemList)
                            {
                                fullDataId = DataTierUtil.GetFullDataId(cacheIndex.IndexId, indexItem, indexTypeMapping.FullDataIdFieldList);
                                if (fullDataId != null)
                                {
                                    dataStorageMessageList.Add(new RelayMessage(relatedTypeId,
                                                                   IndexCacheUtils.GeneratePrimaryId(fullDataId),
                                                                   fullDataId,
                                                                   MessageType.Delete));
                                }
                            }

                            #endregion

                            #region Send relay mesaages to data store

                            if (dataStorageMessageList.Count > 0)
                            {
                                storeContext.ForwarderComponent.HandleMessages(dataStorageMessageList);
                            }

                            #endregion
                        }

                        #endregion

                        #endregion

                        if (dbgIndexInfo != null)
                        {
                            LoggingUtil.Log.Debug(dbgIndexInfo.ToString());
                        }
                    }
                    else
                    {
                        #region Update Virtual Count

                        foreach (KeyValuePair<string /*IndexName*/, int /*VirtualCount*/> kvp in cacheIndex.IndexVirtualCountMapping)
                        {
                            Index indexInfo = indexTypeMapping.IndexCollection[kvp.Key];
                            internalIndex = IndexServerUtils.GetCacheIndexInternal(storeContext,
                                                                                   messageContext.TypeId,
                                                                                   cacheIndex.PrimaryId,
                                                                                   cacheIndex.IndexId,
                                                                                   indexInfo.ExtendedIdSuffix,
                                                                                   kvp.Key,
                                                                                   0,
                                                                                   null,
                                                                                   true,
                                                                                   null,
                                                                                   true,
                                                                                   false,
                                                                                   indexInfo.PrimarySortInfo,
                                                                                   indexInfo.LocalIdentityTagList,
                                                                                   indexInfo.StringHashCodeDictionary,
                                                                                   null);

                            if (internalIndex == null)
                            {
                                internalIndex = new CacheIndexInternal
                                                    {
                                                        InDeserializationContext = new InDeserializationContext
                                                                                            {
                                                                                                TypeId = messageContext.TypeId,
                                                                                                TagHashCollection = storeContext.TagHashCollection,
                                                                                                IndexId = cacheIndex.IndexId,
                                                                                                IndexName = kvp.Key,
                                                                                                InclusiveFilter = true,
                                                                                                DeserializeHeaderOnly = true,
                                                                                                PrimarySortInfo = indexInfo.PrimarySortInfo,
                                                                                                LocalIdentityTagNames = indexInfo.LocalIdentityTagList,
                                                                                                StringHashCollection = storeContext.StringHashCollection,
                                                                                                StringHashCodeDictionary = indexInfo.StringHashCodeDictionary
                                                                                            }
                                                    };
                            }
                            else
                            {
                                // update performance counter
                                PerformanceCounters.Instance.SetCounterValue(
                                    PerformanceCounterEnum.NumberOfItemsInIndexPerSave,
                                    messageContext.TypeId,
                                    internalIndex.OutDeserializationContext.TotalCount);
                            }

                            internalIndex.VirtualCount = kvp.Value;
                            internalIndexList.Add(internalIndex);
                        }
                        #endregion
                    }

                    #region Index storage relay messages for each CacheIndexInternal

                    #region Metadata

                    if (indexTypeMapping.MetadataStoredSeperately && cacheIndex.UpdateMetadata)
                    {
                        indexStorageMessageList.Add(new RelayMessage(messageContext.TypeId,
                                                         cacheIndex.PrimaryId,
                                                         cacheIndex.IndexId,
                                                         DateTime.Now,
                                                         cacheIndex.Metadata ?? new byte[0],
                                                         storeContext.GetCompressOption(messageContext.TypeId),
                                                         MessageType.Save));
                    }

                    #endregion

                    #region Index(es)

                    byte[] payload;
                    CompactBinaryWriter writer;
                    RelayMessage indexStorageMessage;
                    byte[] extendedId;

                    foreach (CacheIndexInternal cacheIndexInternal in internalIndexList)
                    {
                        extendedId = IndexServerUtils.FormExtendedId(
                            cacheIndex.IndexId,
                            indexTypeMapping.IndexCollection[cacheIndexInternal.InDeserializationContext.IndexName].ExtendedIdSuffix);

                        // This mess is required until Moods 2.0 migrated to have IVersionSerializable version of CacheIndexInternal
                        // ** TBD - Should be removed later
                        if (LegacySerializationUtil.Instance.IsSupported(messageContext.TypeId))
                        {
                            writer = new CompactBinaryWriter(new BinaryWriter(new MemoryStream()));
                            cacheIndexInternal.Serialize(writer);
                            payload = new byte[writer.BaseStream.Length];
                            writer.BaseStream.Position = 0;
                            writer.BaseStream.Read(payload, 0, payload.Length);

                            indexStorageMessage = new RelayMessage(messageContext.TypeId,
                                 cacheIndex.PrimaryId,
                                 extendedId,
                                 DateTime.Now,
                                 payload,
                                 storeContext.GetCompressOption(messageContext.TypeId),
                                 MessageType.Save);
                        }
                        else
                        {
                            indexStorageMessage = RelayMessage.GetSaveMessageForObject(messageContext.TypeId,
                                cacheIndex.PrimaryId,
                                extendedId,
                                DateTime.Now,
                                cacheIndexInternal,
                                storeContext.GetCompressOption(messageContext.TypeId));
                        }

                        indexStorageMessageList.Add(indexStorageMessage);
                    }

                    #endregion

                    #region Send relay mesaages to index storage

                    storeContext.IndexStorageComponent.HandleMessages(indexStorageMessageList);

                    #endregion

                    #endregion
                }
                catch (Exception ex)
                {
                    LoggingUtil.Log.DebugFormat("CacheIndex: {0}", IndexServerUtils.GetPrintableCacheIndex(cacheIndex, storeContext.TagHashCollection, messageContext.TypeId));
                    throw new Exception("TypeId " + messageContext.TypeId + " -- Error processing save message.", ex);
                }
            }

        }

        private static void ProcessAddList(List<CacheIndexInternal> internalIndexList,
            List<IndexItem> cappedDeleteItemList,
            CacheIndex clientIndex,
            IndexStoreContext storeContext,
            IndexTypeMapping indexTypeMapping)
        {
            #region Add new items to each internalIndex

            Index indexInfo;
            InternalItemComparer comparer;
            int searchIndex;

            PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.AddList,
                        indexTypeMapping.TypeId,
                        clientIndex.AddList.Count);

            foreach (CacheIndexInternal cacheIndexInternal in internalIndexList)
            {
                indexInfo = indexTypeMapping.IndexCollection[cacheIndexInternal.InDeserializationContext.IndexName];
                comparer = new InternalItemComparer(indexInfo.PrimarySortInfo.IsTag, indexInfo.PrimarySortInfo.FieldName, indexInfo.PrimarySortInfo.SortOrderList);
                if (clientIndex.AddList.Count > 0)
                {
                    foreach (IndexDataItem addItem in clientIndex.AddList)
                    {
                        searchIndex = cacheIndexInternal.Search(addItem);
                        if (searchIndex > -1)
                        {
                            UpdateExistingIndexItem(clientIndex, cacheIndexInternal, indexInfo, addItem, searchIndex, storeContext, comparer);
                        }
                        else
                        {
                            AddNewItem(clientIndex, cacheIndexInternal, indexInfo, addItem, storeContext, comparer);

                            #region IndexSize Capping
                            if (indexInfo.MaxIndexSize > 0 && cacheIndexInternal.Count > indexInfo.MaxIndexSize)
                            {
                                int deleteIndex = indexInfo.TrimFromTail ? cacheIndexInternal.Count - 1 : 0;
                                //Add item to a list to delete it from the Data Store
                                cappedDeleteItemList.Add(InternalItemAdapter.ConvertToIndexItem(cacheIndexInternal.GetItem(deleteIndex),
                                    cacheIndexInternal.InDeserializationContext));

                                //Delete from Index Store
                                cacheIndexInternal.DeleteItem(deleteIndex, false);
                            }
                            #endregion
                        }
                    }
                }

                //Update metadata
                if (clientIndex.UpdateMetadata && indexTypeMapping.IndexCollection[cacheIndexInternal.InDeserializationContext.IndexName].MetadataPresent)
                {
                    cacheIndexInternal.Metadata = clientIndex.Metadata;
                }
            }

            #endregion
        }

        /// <summary>
        /// Processes the delete list.
        /// </summary>
        /// <param name="internalIndexList">The internal index list.</param>
        /// <param name="deleteList">The delete list.</param>
        /// <param name="typeId">The type id.</param>
        private static void ProcessDeleteList(List<CacheIndexInternal> internalIndexList, List<IndexItem> deleteList, short typeId)
        {
            int deleteIndex;

            PerformanceCounters.Instance.SetCounterValue(
                        PerformanceCounterEnum.DeleteList,
                        typeId,
                        deleteList.Count);

            foreach (CacheIndexInternal cacheIndexInternal in internalIndexList)
            {
                if (cacheIndexInternal.Count > 0)
                {
                    foreach (IndexItem deleteItem in deleteList)
                    {
                        deleteIndex = cacheIndexInternal.Search(deleteItem);
                        if (deleteIndex > -1)
                        {
                            cacheIndexInternal.DeleteItem(deleteIndex, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validates the save.
        /// </summary>
        /// <param name="clientIndex">Index of the client.</param>
        private static void ValidateSave(CacheIndex clientIndex)
        {
            if (clientIndex.IndexVirtualCountMapping != null &&
                clientIndex.AddList != null && clientIndex.AddList.Count > 0 &&
                clientIndex.DeleteList != null && clientIndex.DeleteList.Count > 0)
            {
                LoggingUtil.Log.ErrorFormat("VirtualCount cannot be set with non-empty AddList and DeleteList");
                throw new Exception("VirtualCount cannot be set with non-empty AddList and DeleteList");
            }
        }

        /// <summary>
        /// Adds the new item.
        /// </summary>
        /// <param name="clientIndex">Index from the client.</param>
        /// <param name="cacheIndexInternal">The cache index internal.</param>
        /// <param name="indexInfo">The index info.</param>
        /// <param name="addItem">The add item.</param>
        /// <param name="storeContext">The store context.</param>
        /// <param name="comparer">The comparer.</param>
        private static void AddNewItem(CacheIndex clientIndex,
            CacheIndexInternal cacheIndexInternal,
            Index indexInfo, IndexDataItem addItem,
            IndexStoreContext storeContext,
            InternalItemComparer comparer)
        {
            int searchIndex;
            List<KeyValuePair<int, byte[]>> kvpList;
            searchIndex = cacheIndexInternal.GetInsertPosition(addItem, indexInfo.PrimarySortInfo.SortOrderList[0].SortBy, comparer);

            //Add item to CacheIndexInternal
            kvpList = null;
            if (addItem.Tags != null && addItem.Tags.Count > 0)
            {
                kvpList = new List<KeyValuePair<int, byte[]>>();

                if (clientIndex.TargetIndexName != null) //Save to single index
                {
                    foreach (KeyValuePair<string, byte[]> kvp in addItem.Tags)
                    {
                        //Add to tagHashCollection
                        storeContext.TagHashCollection.AddTag(cacheIndexInternal.InDeserializationContext.TypeId, kvp.Key);
                        kvpList.Add(new KeyValuePair<int, byte[]>(TagHashCollection.GetTagHashCode(kvp.Key), kvp.Value));
                    }
                }
                else //Save to multiple indexes
                {
                    List<string> tagNameList;
                    clientIndex.IndexTagMapping.TryGetValue(cacheIndexInternal.InDeserializationContext.IndexName, out tagNameList);
                    foreach (string tagName in tagNameList)
                    {
                        //Add to tagHashCollection
                        storeContext.TagHashCollection.AddTag(cacheIndexInternal.InDeserializationContext.TypeId, tagName);
                        kvpList.Add(new KeyValuePair<int, byte[]>(TagHashCollection.GetTagHashCode(tagName), addItem.Tags[tagName]));
                    }
                }
            }
            cacheIndexInternal.InsertItem(new InternalItem { ItemId = addItem.ItemId, TagList = kvpList }, searchIndex, true);
        }

        /// <summary>
        /// Updates the existing index item.
        /// </summary>
        /// <param name="clientIndex">Index from the client.</param>
        /// <param name="cacheIndexInternal">The cache index internal.</param>
        /// <param name="indexInfo">The index info.</param>
        /// <param name="addItem">The add item.</param>
        /// <param name="searchIndex">Index to search.</param>
        /// <param name="storeContext">The store context.</param>
        /// <param name="comparer">The comparer.</param>
        private static void UpdateExistingIndexItem(CacheIndex clientIndex, CacheIndexInternal cacheIndexInternal, Index indexInfo, IndexDataItem addItem, int searchIndex, IndexStoreContext storeContext, InternalItemComparer comparer)
        {
            InternalItem internalItem = cacheIndexInternal.InternalItemList[searchIndex];
            if (clientIndex.TargetIndexName != null) //Save to single index
            {
                if (addItem.Tags != null && addItem.Tags.Count > 0)
                {
                    bool reposition = IsRepositioningOfIndexItemRequired(indexInfo, addItem, internalItem);

                    // Update all tags on the internal item
                    foreach (KeyValuePair<string, byte[]> kvp in addItem.Tags)
                    {
                        storeContext.TagHashCollection.AddTag(cacheIndexInternal.InDeserializationContext.TypeId, kvp.Key);
                        internalItem.UpdateTag(TagHashCollection.GetTagHashCode(kvp.Key), kvp.Value);
                    }

                    // Reposition index item if required
                    if (reposition)
                    {
                        RepositionIndexItem(cacheIndexInternal, indexInfo, addItem, searchIndex, internalItem, comparer);
                    }
                }
            }
            else //Save to multiple indexes
            {
                if (addItem.Tags != null && addItem.Tags.Count > 0)
                {
                    List<string> tagNameList;
                    byte[] tagValue;
                    clientIndex.IndexTagMapping.TryGetValue(cacheIndexInternal.InDeserializationContext.IndexName, out tagNameList);

                    bool reposition = IsRepositioningOfIndexItemRequired(indexInfo, addItem, internalItem);

                    // Update all tags on the internal item
                    foreach (string tagName in tagNameList)
                    {
                        //Add to tagHashCollection
                        storeContext.TagHashCollection.AddTag(cacheIndexInternal.InDeserializationContext.TypeId, tagName);
                        addItem.TryGetTagValue(tagName, out tagValue);
                        internalItem.UpdateTag(TagHashCollection.GetTagHashCode(tagName), tagValue);
                    }

                    // Reposition index item if required
                    if (reposition)
                    {
                        RepositionIndexItem(cacheIndexInternal, indexInfo, addItem, searchIndex, internalItem, comparer);
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether repositioning of index item is required or not.
        /// </summary>
        /// <param name="indexInfo">The index info.</param>
        /// <param name="addItem">The add item.</param>
        /// <param name="internalItem">The internal item.</param>
        /// <returns>
        /// 	<c>true</c> if repositioning of index item is required; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsRepositioningOfIndexItemRequired(Index indexInfo, IndexDataItem addItem, InternalItem internalItem)
        {
            byte[] sortTagValueInAddItem;
            if (indexInfo.PrimarySortInfo.IsTag && addItem.Tags.TryGetValue(indexInfo.PrimarySortInfo.FieldName, out sortTagValueInAddItem))
            {
                // Index is sorted by tag and tag might have been updated
                byte[] sortTagValueInIndex;
                internalItem.TryGetTagValue(indexInfo.PrimarySortInfo.FieldName, out sortTagValueInIndex);

                if (!ByteArrayComparerUtil.CompareByteArrays(sortTagValueInAddItem, sortTagValueInIndex))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Repositions the index item.
        /// </summary>
        /// <param name="cacheIndexInternal">The cache index internal.</param>
        /// <param name="indexInfo">The index info.</param>
        /// <param name="addItem">The add item.</param>
        /// <param name="searchIndex">Index of the search.</param>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="comparer">The comparer.</param>
        private static void RepositionIndexItem(CacheIndexInternal cacheIndexInternal, Index indexInfo, IndexDataItem addItem, int searchIndex, InternalItem internalItem, InternalItemComparer comparer)
        {
            // Remove the Item from current position
            cacheIndexInternal.DeleteItem(searchIndex, false);

            // Determine where to insert the Item
            int newSearchIndex = cacheIndexInternal.GetInsertPosition(addItem, indexInfo.PrimarySortInfo.SortOrderList[0].SortBy, comparer);

            // insert the item at new position
            cacheIndexInternal.InsertItem(internalItem, newSearchIndex, false);
        }
    }
}
