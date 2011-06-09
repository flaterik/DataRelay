using System;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.Framework;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.Common.IO;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using Filter = MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3.Filter;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store
{
    internal class CacheIndexInternal : IVersionSerializable, IExtendedRawCacheParameter
    {
        #region Data Members

        /// <summary>
        /// Gets or sets the metadata.
        /// </summary>
        /// <value>The metadata.</value>
        internal byte[] Metadata
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the metadata property collection.
        /// </summary>
        /// <value>The metadata property collection.</value>
        public MetadataPropertyCollection MetadataPropertyCollection
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the internal item list.
        /// </summary>
        /// <value>The internal item list.</value>
        internal InternalItemList InternalItemList
        {
            get;
            set;
        }

        private int virtualCount;
        /// <summary>
        /// Gets or sets the virtual count.
        /// </summary>
        /// <value>The virtual count.</value>
        internal int VirtualCount
        {
            get
            {
                return virtualCount;
            }
            set
            {
                virtualCount = ((outDeserializationContext != null) && (value < outDeserializationContext.TotalCount)) ? 
                    outDeserializationContext.TotalCount : 
                    value;
            }
        }

        /// <summary>
        /// Gets or sets the in deserialization context.
        /// </summary>
        /// <value>The InDeserializationContext.</value>
        internal InDeserializationContext InDeserializationContext
        {
            get;
            set;
        }

        private OutDeserializationContext outDeserializationContext;
        /// <summary>
        /// Gets the out deserialization context.
        /// </summary>
        /// <value>The OutDeserializationContext.</value>
        internal OutDeserializationContext OutDeserializationContext
        {
            get
            {
                return outDeserializationContext;
            }
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The InternalItemList Count.</value>
        internal int Count
        {
            get
            {
                return InternalItemList != null ? InternalItemList.Count : 0;
            }
        }

        internal GroupByResult GroupByResult
        {
            get;
            set;
        }

        #endregion

        #region Ctors

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheIndexInternal"/> class.
        /// </summary>
        internal CacheIndexInternal()
        {
            InternalItemList = new InternalItemList();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the item.
        /// </summary>
        /// <param name="pos">The pos.</param>
        /// <returns>InternalItem</returns>
        internal InternalItem GetItem(int pos)
        {
            return InternalItemList[pos];
        }

        /// <summary>
        /// Deletes the item.
        /// </summary>
        /// <param name="pos">The pos.</param>
        /// <param name="decrementVirtualCount">if set to <c>true</c> [decrement virtual count].</param>
        internal void DeleteItem(int pos, bool decrementVirtualCount)
        {
            InternalItemList.RemoveAt(pos);
            if (decrementVirtualCount)
            {
                virtualCount--;
            }
        }

        /// <summary>
        /// Deletes the item range.
        /// </summary>
        /// <param name="startPos">The start pos.</param>
        /// <param name="count">The count.</param>
        /// <param name="decrementVirtualCount">if set to <c>true</c> decrements virtual count.</param>
        internal void DeleteItemRange(int startPos, int count, bool decrementVirtualCount)
        {
            InternalItemList.RemoveRange(startPos, count);
            if (decrementVirtualCount)
            {
                virtualCount -= count;
            }
        }

        /// <summary>
        /// Adds the item.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="incrementVirtualCount">if set to <c>true</c> increments virtual count.</param>
        internal void AddItem(InternalItem internalItem, bool incrementVirtualCount)
        {
            InternalItemList.Add(internalItem);
            if (incrementVirtualCount)
            {
                virtualCount++;
            }
        }

        /// <summary>
        /// Inserts the item.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="pos">The pos.</param>
        /// <param name="incrementVirtualCount">if set to <c>true</c> increments virtual count.</param>
        internal void InsertItem(InternalItem internalItem, int pos, bool incrementVirtualCount)
        {
            InternalItemList.Insert(pos, internalItem);
            if (incrementVirtualCount)
            {
                virtualCount++;
            }
        }

        /// <summary>
        /// Sorts according to the specified tag sort.
        /// </summary>
        /// <param name="tagSort">The tag sort.</param>
        internal void Sort(TagSort tagSort)
        {
            InternalItemList.Sort(tagSort);
        }

        /// <summary>
        /// Gets the tag value.
        /// </summary>
        /// <param name="pos">The pos.</param>
        /// <param name="tagName">Name of the tag.</param>
        /// <returns>Byte Array tag value</returns>
        internal byte[] GetTagValue(int pos, string tagName)
        {
            byte[] tagValue;
            InternalItemList[pos].TryGetTagValue(tagName, out tagValue);
            return tagValue;
        }

        /// <summary>
        /// Searches the specified search item.
        /// </summary>
        /// <param name="searchItem">The search item.</param>
        /// <returns></returns>
        internal int Search(IndexItem searchItem)
        {
            return InDeserializationContext.PrimarySortInfo.IsTag ?
                InternalItemList.LinearSearch(InternalItemAdapter.ConvertToInternalItem(searchItem), InDeserializationContext.LocalIdentityTagNames) :
                InternalItemList.BinarySearchItem(InternalItemAdapter.ConvertToInternalItem(searchItem),
                    InDeserializationContext.PrimarySortInfo.IsTag,
                    InDeserializationContext.PrimarySortInfo.FieldName,
                    InDeserializationContext.PrimarySortInfo.SortOrderList, InDeserializationContext.LocalIdentityTagNames);
        }

        /// <summary>
        /// Gets the insert position.
        /// </summary>
        /// <param name="searchItem">The search item.</param>
        /// <param name="sortBy">The sort by.</param>
        /// <param name="comparer">The comparer.</param>
        /// <returns>InsertPosition</returns>
        internal int GetInsertPosition(IndexItem searchItem, SortBy sortBy, InternalItemComparer comparer)
        {
            return InternalItemList.GetInsertPosition(InternalItemAdapter.ConvertToInternalItem(searchItem), comparer, sortBy);
        }

        #endregion

        #region IVersionSerializable Members

        /// <summary>
        /// Serialize the class data to a stream.
        /// </summary>
        /// <param name="writer">The <see cref="IPrimitiveWriter"/> that writes to the stream.</param>
        public void Serialize(IPrimitiveWriter writer)
        {
            //Metadata or MetadataPropertyCollection
            if (InDeserializationContext.IsMetadataPropertyCollection)
            {
                //MetadataPropertyCollection
                if (MetadataPropertyCollection == null || MetadataPropertyCollection.Count == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)MetadataPropertyCollection.Count);
                    foreach (KeyValuePair<string /*PropertyName*/, byte[] /*PropertyValue*/> kvp in MetadataPropertyCollection)
                    {
                        writer.Write(kvp.Key);
                        if (kvp.Value == null || kvp.Value.Length == 0)
                        {
                            writer.Write((ushort)0);
                        }
                        else
                        {
                            writer.Write((ushort)kvp.Value.Length);
                            writer.Write(kvp.Value);
                        }
                    }
                }
            }
            else
            {
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
            }

            if (!LegacySerializationUtil.Instance.IsSupported(InDeserializationContext.TypeId))
            {
                //VirtualCount
                writer.Write(virtualCount);
            }

            // Note: If InDeserializationContext.DeserializeHeaderOnly property is set then InDeserializationContext.UnserializedCacheIndexInternal shall hold all CacheIndexInternal 
            // payload except metadata and virtual count. This code path will only be used if just header info like 
            // virtual count needs to be updated keeping rest of the index untouched
            if (InDeserializationContext.DeserializeHeaderOnly &&
                outDeserializationContext != null &&
                outDeserializationContext.UnserializedCacheIndexInternal != null &&
                outDeserializationContext.UnserializedCacheIndexInternal.Length != 0)
            {
                //Count
                writer.Write(outDeserializationContext.TotalCount);

                // UnserializedCacheIndexInternal
                writer.BaseStream.Write(outDeserializationContext.UnserializedCacheIndexInternal, 0, outDeserializationContext.UnserializedCacheIndexInternal.Length);
            }
            else
            {
                //Count
                if (InternalItemList == null || InternalItemList.Count == 0)
                {
                    writer.Write(0);
                }
                else
                {
                    writer.Write(InternalItemList.Count);

                    for (int i = 0; i < InternalItemList.Count; i++)
                    {
                        //Id
                        if (InternalItemList[i].ItemId == null || InternalItemList[i].ItemId.Length == 0)
                        {
                            throw new Exception("Invalid ItemId - is null or length is zero for IndexId : " +
                                                IndexCacheUtils.GetReadableByteArray(InDeserializationContext.IndexId));
                        }
                        writer.Write((ushort)InternalItemList[i].ItemId.Length);
                        writer.Write(InternalItemList[i].ItemId);

                        //(byte)KvpListCount
                        if (InternalItemList[i].TagList == null || InternalItemList[i].TagList.Count == 0)
                        {
                            writer.Write((byte)0);
                        }
                        else
                        {
                            writer.Write((byte)InternalItemList[i].TagList.Count);

                            //KvpList
                            byte[] stringHashValue;
                            foreach (KeyValuePair<int /*TagHashCode*/, byte[] /*TagValue*/> kvp in InternalItemList[i].TagList)
                            {
                                writer.Write(kvp.Key);
                                if (kvp.Value == null || kvp.Value.Length == 0)
                                {
                                    writer.Write((ushort)0);
                                }
                                else
                                {
                                    if (InDeserializationContext.StringHashCodeDictionary != null &&
                                        InDeserializationContext.StringHashCodeDictionary.Count > 0 &&
                                        InDeserializationContext.StringHashCodeDictionary.ContainsKey(kvp.Key))
                                    {
                                        InDeserializationContext.StringHashCollection.AddStringArray(InDeserializationContext.TypeId, kvp.Value);
                                        stringHashValue = StringHashCollection.GetHashCodeByteArray(kvp.Value);
                                        writer.Write((ushort)stringHashValue.Length);
                                        writer.Write(stringHashValue);
                                    }
                                    else
                                    {
                                        writer.Write((ushort)kvp.Value.Length);
                                        writer.Write(kvp.Value);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Deserialize the class data from a stream.
        /// </summary>
        /// <param name="reader">The <see cref="IPrimitiveReader"/> that extracts used to extra data from a stream.</param>
        /// <param name="version">The value of <see cref="CurrentVersion"/> that was written to the stream when it was originally serialized to a stream;
        /// the version of the <paramref name="reader"/> data.</param>
        public void Deserialize(IPrimitiveReader reader, int version)
        {
            ushort len;

            //Metadata or MetadataPropertyCollection
            if (InDeserializationContext.IsMetadataPropertyCollection)
            {
                //MetadataPropertyCollection
                len = reader.ReadUInt16();
                if (len > 0)
                {
                    MetadataPropertyCollection = new MetadataPropertyCollection();
                    string propertyName;
                    byte[] propertyValue;
                    ushort propertyValueLen;

                    for (ushort i = 0; i < len; i++)
                    {
                        propertyName = reader.ReadString();
                        propertyValueLen = reader.ReadUInt16();
                        propertyValue = null;
                        if (propertyValueLen > 0)
                        {
                            propertyValue = reader.ReadBytes(propertyValueLen);
                        }
                        MetadataPropertyCollection.Add(propertyName, propertyValue);
                    }
                }
            }
            else
            {
                //Metadata
                len = reader.ReadUInt16();
                if (len > 0)
                {
                    Metadata = reader.ReadBytes(len);
                }
            }

            //VirtualCount
            if (version >= 2)
            {
                virtualCount = reader.ReadInt32();
            }

            //Count
            outDeserializationContext = new OutDeserializationContext { TotalCount = reader.ReadInt32() };

            if (InDeserializationContext.DeserializeHeaderOnly)
            {
                //Note: If InDeserializationContext.DeserializeHeaderOnly property is set then InDeserializationContext.PartialByteArray shall hold all CacheIndexInternal 
                //payload except metadata and header (just virtual count for now). This code path will only be used if just
                //header info like virtual count needs to be updated keeping rest of the index untouched.
                //InDeserializationContext.PartialByteArray shall be used in Serialize code
                outDeserializationContext.UnserializedCacheIndexInternal =
                    new byte[(int)reader.BaseStream.Length - (int)reader.BaseStream.Position];
                reader.BaseStream.Read(outDeserializationContext.UnserializedCacheIndexInternal, 0, outDeserializationContext.UnserializedCacheIndexInternal.Length);
            }
            else
            {
                int actualItemCount = outDeserializationContext.TotalCount;

                //this.InDeserializationContext.MaxItemsPerIndex = 0 indicates need to extract all items
                //this.InDeserializationContext.MaxItemsPerIndex > 0 indicates need to extract only number of items indicated by InDeserializationContext.MaxItemsPerIndex
                if (InDeserializationContext.MaxItemsPerIndex > 0)
                {
                    if (InDeserializationContext.MaxItemsPerIndex < outDeserializationContext.TotalCount)
                    {
                        actualItemCount = InDeserializationContext.MaxItemsPerIndex;
                    }
                }

                #region Populate InternalItemList

                InternalItem internalItem;
                bool enterConditionPassed = false;

                InternalItemList = new InternalItemList();
                GroupByResult = new GroupByResult(new BaseComparer(InDeserializationContext.PrimarySortInfo.IsTag, InDeserializationContext.PrimarySortInfo.FieldName, InDeserializationContext.PrimarySortInfo.SortOrderList));

                // Note: ---- Termination condition of the loop
                // For full index extraction loop shall terminate because of condition : internalItemList.Count + GroupByResult.Count < actualItemCount
                // For partial index extraction loop shall terminate because of following conditions 
                //				a)  i < InDeserializationContext.TotalCount (when no sufficient items are found) OR
                //				b)  internalItemList.Count < actualItemCount (Item extraction cap is reached)																					
                int i = 0;
                while (GroupByResult.Count + InternalItemList.Count < actualItemCount && i < outDeserializationContext.TotalCount)
                {
                    i++;

                    #region Deserialize ItemId

                    len = reader.ReadUInt16();
                    if (len > 0)
                    {
                        internalItem = new InternalItem
                                           {
                                               ItemId = reader.ReadBytes(len)
                                           };
                    }
                    else
                    {
                        throw new Exception("Invalid ItemId - is null or length is zero for IndexId : " +
                                            IndexCacheUtils.GetReadableByteArray(InDeserializationContext.IndexId));
                    }

                    #endregion

                    #region Process IndexCondition
                    if (InDeserializationContext.EnterCondition != null || InDeserializationContext.ExitCondition != null)
                    {
                        #region Have Enter/Exit Condition

                        if (InDeserializationContext.PrimarySortInfo.IsTag == false)
                        {
                            #region Sort by ItemId

                            if (InDeserializationContext.EnterCondition != null && enterConditionPassed == false)
                            {
                                #region enter condition processing

                                if (FilterPassed(internalItem, InDeserializationContext.EnterCondition))
                                {
                                    if (InDeserializationContext.ExitCondition != null && !FilterPassed(internalItem, InDeserializationContext.ExitCondition))
                                    {
                                        // no need to search beyond this point
                                        break;
                                    }

                                    enterConditionPassed = true;
                                    DeserializeTags(internalItem, InDeserializationContext, OutDeserializationContext, reader);
                                    ApplyFilterAndAddItem(internalItem);
                                }
                                else
                                {
                                    SkipDeserializeInternalItem(reader);
                                    // no filter processing required
                                }

                                #endregion
                            }
                            else if (InDeserializationContext.ExitCondition != null)
                            {
                                #region exit condition processing

                                if (FilterPassed(internalItem, InDeserializationContext.ExitCondition))
                                {
                                    // since item passed exit filter, we keep it.
                                    DeserializeTags(internalItem, InDeserializationContext, OutDeserializationContext, reader);
                                    ApplyFilterAndAddItem(internalItem);
                                }
                                else
                                {
                                    // no need to search beyond this point
                                    break;
                                }

                                #endregion
                            }
                            else if (InDeserializationContext.EnterCondition != null && enterConditionPassed && InDeserializationContext.ExitCondition == null)
                            {
                                #region enter condition processing when no exit condition exists

                                DeserializeTags(internalItem, InDeserializationContext, OutDeserializationContext, reader);
                                ApplyFilterAndAddItem(internalItem);

                                #endregion
                            }

                            #endregion
                        }
                        else
                        {
                            #region Sort by Tag

                            #region Deserialize InternalItem and fetch PrimarySortTag value

                            byte[] tagValue;
                            DeserializeTags(internalItem, InDeserializationContext, OutDeserializationContext, reader);
                            if (!internalItem.TryGetTagValue(InDeserializationContext.PrimarySortInfo.FieldName, out tagValue))
                            {
                                throw new Exception("PrimarySortTag Not found:  " + InDeserializationContext.PrimarySortInfo.FieldName);
                            }

                            #endregion

                            if (InDeserializationContext.EnterCondition != null && enterConditionPassed == false)
                            {
                                #region enter condition processing

                                if (FilterPassed(internalItem, InDeserializationContext.EnterCondition))
                                {
                                    if (InDeserializationContext.ExitCondition != null && !FilterPassed(internalItem, InDeserializationContext.ExitCondition))
                                    {
                                        // no need to search beyond this point
                                        break;
                                    }

                                    enterConditionPassed = true;
                                    ApplyFilterAndAddItem(internalItem);
                                }                               

                                #endregion
                            }
                            else if (InDeserializationContext.ExitCondition != null)
                            {
                                #region exit condition processing

                                if (FilterPassed(internalItem, InDeserializationContext.ExitCondition))
                                {                                
                                    // since item passed exit filter, we keep it.
                                    ApplyFilterAndAddItem(internalItem);
                                }
                                else
                                {
                                    // no need to search beyond this point
                                    break;

                                }

                                #endregion
                            }
                            else if (InDeserializationContext.EnterCondition != null && enterConditionPassed && InDeserializationContext.ExitCondition == null)
                            {
                                #region enter condition processing when no exit condition exists

                                ApplyFilterAndAddItem(internalItem);

                                #endregion
                            }

                            #endregion
                        }

                        #endregion
                    }
                    else
                    {
                        #region No Enter/Exit Condition

                        DeserializeTags(internalItem, InDeserializationContext, OutDeserializationContext, reader);
                        ApplyFilterAndAddItem(internalItem);

                        #endregion
                    }

                    #endregion
                }

                //Set ReadItemCount on OutDeserializationContext
                outDeserializationContext.ReadItemCount = i;

                #endregion
            }
        }

        /// <summary>
        /// Applies the filter and adds the item.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        private void ApplyFilterAndAddItem(InternalItem internalItem)
        {
            if (InDeserializationContext.CapCondition != null &&
                InDeserializationContext.CapCondition.FilterCaps != null &&
                InDeserializationContext.CapCondition.FilterCaps.Count > 0)
            {
                #region CapCondition Exists

                byte[] tagValue;
                if (internalItem.TryGetTagValue(InDeserializationContext.CapCondition.FieldName, out tagValue))
                {
                    FilterCap filterCap;
                    if (InDeserializationContext.CapCondition.FilterCaps.TryGetValue(tagValue, out filterCap))
                    {
                        #region  Filter Cap found for tagValue

                        if (filterCap.Cap > 0 && FilterPassed(internalItem, GetCappedOrParentFilter(filterCap)))
                        {
                            filterCap.Cap--;
                            ProcessAdditionalConstraintsAndAddItem(internalItem);
                        }

                        #endregion
                    }
                    else if (!InDeserializationContext.CapCondition.IgnoreNonCappedItems && FilterPassed(internalItem, InDeserializationContext.Filter))
                    {
                        #region Filter Cap not found for tagValue

                        ProcessAdditionalConstraintsAndAddItem(internalItem);

                        #endregion
                    }
                }
                else if (!InDeserializationContext.CapCondition.IgnoreNonCappedItems && FilterPassed(internalItem, InDeserializationContext.Filter))
                {
                    #region Apply parent filter

                    ProcessAdditionalConstraintsAndAddItem(internalItem);

                    #endregion
                }

                #endregion
            }
            else if (FilterPassed(internalItem, InDeserializationContext.Filter))
            {
                #region CapCondition Doesn't  Exist

                ProcessAdditionalConstraintsAndAddItem(internalItem);

                #endregion
            }
        }

        /// <summary>
        /// Processes the additional constraints and add item.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        private void ProcessAdditionalConstraintsAndAddItem(InternalItem internalItem)
        {
            // Domain specific processing
            if (InDeserializationContext.DomainSpecificProcessingType != DomainSpecificProcessingType.None)
            {
                DomainSpecificProcssorUtil.Process(internalItem,
                    InDeserializationContext.TagHashCollection,
                    InDeserializationContext.TypeId,
                    InDeserializationContext.DomainSpecificProcessingType,
                    InDeserializationContext.DomainSpecificConfig);
            }

            //GroupBy
            if (InDeserializationContext.GroupBy != null)
            {
                ApplyGroupBy(internalItem);
            }
            else
            {
                AddItem(internalItem, false);
            }
        }

        /// <summary>
        /// Applies the grouping.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        internal void ApplyGroupBy(InternalItem internalItem)
        {
            ResultItem resultItem = new ResultItem(InDeserializationContext.IndexId,
                internalItem.ItemId,
                null,
                InternalItemAdapter.ConvertToTagDictionary(internalItem.TagList, InDeserializationContext));

            byte[] compositeKey;
            //Check if GroupBy needs to be applied
            if (String.IsNullOrEmpty(InDeserializationContext.GroupBy.FieldName))
            {
                compositeKey = GetCompositeKey(internalItem,
                                               InDeserializationContext.GroupBy.GroupByFieldNameList);
            }
            else
            {
                byte[] fieldValue;
                if (String.Compare(InDeserializationContext.GroupBy.FieldName, "ItemId", true) == 0)
                {
                    fieldValue = internalItem.ItemId;
                }
                else
                {
                    internalItem.TryGetTagValue(InDeserializationContext.GroupBy.FieldName, out fieldValue);
                }

                compositeKey = fieldValue != null &&
                                      InDeserializationContext.GroupBy.FieldValueSet.Contains(fieldValue)
                                          ? GetCompositeKey(internalItem,
                                                            InDeserializationContext.GroupBy.GroupByFieldNameList)
                                          : GetCompositeKey(internalItem,
                                                            InDeserializationContext.GroupBy.NonGroupByFieldNameList);
            }
            GroupByResult.Add(compositeKey, resultItem);
        }

        /// <summary>
        /// Gets the composite key.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="fieldNameList">The field name list.</param>
        /// <returns></returns>
        private static byte[] GetCompositeKey(InternalItem internalItem, List<string> fieldNameList)
        {
            byte[] compositeKey;
            List<byte[]> compositeKeyPartList = new List<byte[]>(fieldNameList.Count);
            int totalByteLength = 0;
            byte[] compositeKeyPart;
            string fieldName;
            for (int i = 0; i < fieldNameList.Count; i++)
            {
                fieldName = fieldNameList[i];

                if (!string.IsNullOrEmpty(fieldName))
                {
                    if (String.Compare(fieldName, "ItemId", true) == 0)
                    {
                        compositeKeyPart = internalItem.ItemId;
                    }
                    else
                    {
                        internalItem.TryGetTagValue(fieldName, out compositeKeyPart);
                    }

                    if (compositeKeyPart != null && compositeKeyPart.Length > 0)
                    {
                        totalByteLength += compositeKeyPart.Length;
                        compositeKeyPartList.Add(compositeKeyPart);
                    }
                }
            }

            compositeKey = new byte[totalByteLength];
            int offset = 0;

            for (int i = 0; i < compositeKeyPartList.Count; i++)
            {
                compositeKeyPart = compositeKeyPartList[i];
                Buffer.BlockCopy(compositeKeyPart, 0, compositeKey, offset, compositeKeyPart.Length);
                offset += compositeKeyPart.Length;
            }
            return compositeKey;
        }

        /// <summary>
        /// Gets the capped or parent filter.
        /// </summary>
        /// <param name="filterCap">The filter cap.</param>
        /// <returns></returns>
        private Filter GetCappedOrParentFilter(FilterCap filterCap)
        {
            return filterCap.UseParentFilter ? InDeserializationContext.Filter : filterCap.Filter;
        }

        /// <summary>
        /// Deserializes the internal item.
        /// </summary>
        /// <param name="internalItem">The internal item</param>
        /// <param name="inDeserializationContext">The in deserialization context.</param>
        /// <param name="outDeserializationContext">The out deserialization context.</param>
        /// <param name="reader">The reader.</param>
        private static void DeserializeTags(InternalItem internalItem,
            InDeserializationContext inDeserializationContext,
            OutDeserializationContext outDeserializationContext,
            IPrimitiveReader reader)
        {
            byte kvpListCount = reader.ReadByte();

            if (kvpListCount > 0)
            {
                internalItem.TagList = new List<KeyValuePair<int, byte[]>>(kvpListCount);
                for (byte j = 0; j < kvpListCount; j++)
                {
                    int tagHashCode = reader.ReadInt32();
                    ushort tagValueLen = reader.ReadUInt16();
                    byte[] tagValue = null;
                    if (tagValueLen > 0)
                    {
                        tagValue = reader.ReadBytes(tagValueLen);
                        if (inDeserializationContext.StringHashCodeDictionary != null &&
                            inDeserializationContext.StringHashCodeDictionary.Count > 0 &&
                            inDeserializationContext.StringHashCodeDictionary.ContainsKey(tagHashCode))
                        {
                            tagValue = inDeserializationContext.StringHashCollection.GetStringByteArray(inDeserializationContext.TypeId, tagValue);
                        }
                    }
                    internalItem.TagList.Add(new KeyValuePair<int, byte[]>(tagHashCode, tagValue));
                }
            }

            //Get Distinct Values
            if (!String.IsNullOrEmpty(inDeserializationContext.GetDistinctValuesFieldName))
            {
                byte[] distinctValue;
                if (String.Equals(inDeserializationContext.GetDistinctValuesFieldName, "ItemId", StringComparison.OrdinalIgnoreCase))
                {
                    distinctValue = internalItem.ItemId;
                }
                else
                {
                    internalItem.TryGetTagValue(inDeserializationContext.GetDistinctValuesFieldName, out distinctValue);
                }

                if (distinctValue != null)
                {
                    if (outDeserializationContext.DistinctValueCountMapping.ContainsKey(distinctValue))
                    {
                        outDeserializationContext.DistinctValueCountMapping[distinctValue] += 1;
                    }
                    else
                    {
                        outDeserializationContext.DistinctValueCountMapping.Add(distinctValue, 1);
                    }
                }
            }
        }

        /// <summary>
        /// Skips the deserialization of the internal item.
        /// </summary>
        /// <param name="reader">The reader.</param>
        private static void SkipDeserializeInternalItem(IPrimitiveReader reader)
        {
            var kvpListCount = reader.ReadByte();

            //kvpList          
            if (kvpListCount > 0)
            {
                for (byte j = 0; j < kvpListCount; j++)
                {
                    //tagHashCode 
                    reader.ReadBytes(4);

                    //tagValueLen + value
                    reader.ReadBytes(reader.ReadUInt16());
                }
            }
        }

        /// <summary>
        /// Processes the filters.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>if true Filter passed successfully; otherwise, false</returns>
        private bool FilterPassed(InternalItem internalItem, Filter filter)
        {
            bool retVal = true;
            if (filter != null)
            {
                if (!FilterUtil.ProcessFilter(internalItem,
                    filter,
                    InDeserializationContext.InclusiveFilter,
                    InDeserializationContext.TagHashCollection,
                    InDeserializationContext.IsMetadataPropertyCollection ? MetadataPropertyCollection : InDeserializationContext.MetadataPropertyCollection))
                {
                    retVal = false;
                    if (InDeserializationContext.CollectFilteredItems)
                    {
                        outDeserializationContext.FilteredInternalItemList.Add(internalItem);
                    }
                }
            }
            return retVal;
        }

        private const int CURRENT_VERSION = 2;
        /// <summary>
        /// Gets the current serialization data version of your object.  The <see cref="Serialize"/> method
        /// will write to the stream the correct format for this version.
        /// </summary>
        /// <value></value>
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        /// <summary>
        /// Deprecated. Has no effect.
        /// </summary>
        /// <value></value>
        public bool Volatile
        {
            get
            {
                return false;
            }
        }
        #endregion

        #region ICustomSerializable Members

        /// <summary>
        /// Deserialize data from a stream
        /// </summary>
        /// <param name="reader"></param>
        public void Deserialize(IPrimitiveReader reader)
        {
            //Note: This is called by legacy code which is using Non-IVersionserializable version of CacheIndexInternal
            // In future it should be replaced with following code

            //reader.Response = SerializationResponse.Unhandled;

            Deserialize(reader, 1);
        }

        #endregion

        #region IExtendedRawCacheParameter Members

        /// <summary>
        /// A byte array used to identiy the object when an integer is insufficient.
        /// </summary>
        /// <value></value>
        public byte[] ExtendedId
        {
            get
            {
                return InDeserializationContext.IndexId;
            }
            set
            {
                throw new Exception("Setter for 'CacheIndex.ExtendedId' is not implemented and should not be invoked!");
            }
        }

        private DateTime? lastUpdatedDate;
        /// <summary>
        /// If this is not null, on input it will be used in place of DateTime.Now. On output, it will be populated by the server's recorded LastUpdatedDate.
        /// </summary>
        /// <value></value>
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

        /// <summary>
        /// Gets or sets the primary id.
        /// </summary>
        /// <value>The primary id.</value>
        public int PrimaryId
        {
            get
            {
                return IndexCacheUtils.GeneratePrimaryId(InDeserializationContext.IndexId);
            }
            set
            {
                throw new Exception("Setter for 'CacheIndexInternal.PrimaryId' is not implemented and should not be invoked!");
            }
        }

        private DataSource dataSource = DataSource.Unknown;
        /// <summary>
        /// Source of the object (Cache vs. Database).
        /// </summary>
        /// <value></value>
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

        /// <summary>
        /// If shared is empty.
        /// </summary>
        /// <value></value>
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

        /// <summary>
        /// Gets a value indicating whether this instance is valid.
        /// </summary>
        /// <value><c>true</c> if this instance is valid; otherwise, <c>false</c>.</value>
        public bool IsValid
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [edit mode].
        /// </summary>
        /// <value><c>true</c> if [edit mode]; otherwise, <c>false</c>.</value>
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
    }
}