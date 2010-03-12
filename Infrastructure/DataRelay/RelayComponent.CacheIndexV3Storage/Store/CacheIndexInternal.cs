using System;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.Framework;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.Common.IO;
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
                virtualCount = value < outDeserializationContext.TotalCount ? outDeserializationContext.TotalCount : value;
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

            if (!LegacySerializationUtil.Instance.IsSupported(InDeserializationContext.TypeId))
            {
                //VirtualCount
                writer.Write(virtualCount);
            }

            // Note: If InDeserializationContext.DeserializeHeaderOnly property is set then InDeserializationContext.UnserializedCacheIndexInternal shall hold all CacheIndexInternal 
            // payload except metadata and virtual count. This code path will only be used if just header info like 
            // virtual count needs to be updated keeping rest of the index untouched
            if (InDeserializationContext.DeserializeHeaderOnly &&
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
            //Metadata
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                Metadata = reader.ReadBytes(len);
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
                    new byte[(int)reader.BaseStream.Length - (int)reader.BaseStream.Position + 1];
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

                byte[] itemId;
                InternalItem internalItem;
                bool enterConditionPassed = false;

                InternalItemList = new InternalItemList();

                // Note: ---- Termination condition of the loop
                // For full index extraction loop shall terminate because of condition : internalItemList.Count < actualItemCount
                // For partial index extraction loop shall terminate because of following conditions 
                //				a)  i < InDeserializationContext.TotalCount (when no sufficient items are found) OR
                //				b)  internalItemList.Count < actualItemCount (Item extraction cap is reached)																					
                int i = 0;
                while (InternalItemList.Count < actualItemCount && i < outDeserializationContext.TotalCount)
                {
                    i++;

                    #region Deserialize ItemId

                    len = reader.ReadUInt16();
                    if (len > 0)
                    {
                        itemId = reader.ReadBytes(len);
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

                                if (InDeserializationContext.EnterCondition.Process(itemId))
                                {
                                    enterConditionPassed = true;
                                    internalItem = DeserializeInternalItem(itemId, InDeserializationContext, reader);
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

                                if (InDeserializationContext.ExitCondition.Process(itemId))
                                {
                                    // since item passed exit filter, we keep it.
                                    internalItem = DeserializeInternalItem(itemId, InDeserializationContext, reader);
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

                                internalItem = DeserializeInternalItem(itemId, InDeserializationContext, reader);
                                ApplyFilterAndAddItem(internalItem);

                                #endregion
                            }

                            #endregion
                        }
                        else
                        {
                            byte[] tagValue;

                            #region Deserialize InternalItem and fetch PrimarySortTag value

                            internalItem = DeserializeInternalItem(itemId, InDeserializationContext, reader);
                            if (!internalItem.TryGetTagValue(InDeserializationContext.PrimarySortInfo.FieldName, out tagValue))
                            {
                                throw new Exception("PrimarySortTag Not found:  " + InDeserializationContext.PrimarySortInfo.FieldName);
                            }

                            #endregion

                            #region Sort by Tag

                            if (InDeserializationContext.EnterCondition != null && enterConditionPassed == false)
                            {
                                #region enter condition processing

                                if (InDeserializationContext.EnterCondition.Process(tagValue))
                                {
                                    enterConditionPassed = true;
                                    ApplyFilterAndAddItem(internalItem);
                                }

                                #endregion
                            }
                            else if (InDeserializationContext.ExitCondition != null)
                            {
                                #region exit condition processing

                                if (InDeserializationContext.ExitCondition.Process(tagValue))
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

                        internalItem = DeserializeInternalItem(itemId, InDeserializationContext, reader);
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
                    #region  Filter Cap found for tagValue

                    FilterCap filterCap;
                    if (InDeserializationContext.CapCondition.FilterCaps.TryGetValue(tagValue, out filterCap))
                    {
                        if (filterCap.Cap > 0 && FilterPassed(internalItem, GetCappedOrParentFilter(filterCap)))
                        {
                            filterCap.Cap--;
                            AddItem(internalItem, false);
                        }
                    }

                    #endregion

                    #region Filter Cap not found for tagValue

                    else if (FilterPassed(internalItem, InDeserializationContext.Filter))
                    {
                        AddItem(internalItem, false);
                    }

                    #endregion
                }

                #region Apply parent filter

                else if (FilterPassed(internalItem, InDeserializationContext.Filter))
                {
                    AddItem(internalItem, false);
                }

                #endregion

                #endregion
            }

            #region CapCondition Doesn't  Exist

            else if (FilterPassed(internalItem, InDeserializationContext.Filter))
            {
                AddItem(internalItem, false);
            }

            #endregion
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
        /// <param name="itemId">The item id.</param>
        /// <param name="inDeserializationContext">The in deserialization context.</param>
        /// <param name="reader">The reader.</param>
        /// <returns>InternalItem</returns>
        private static InternalItem DeserializeInternalItem(byte[] itemId, InDeserializationContext inDeserializationContext, IPrimitiveReader reader)
        {
            byte kvpListCount = reader.ReadByte();

            List<KeyValuePair<int, byte[]>> kvpList = null;
            if (kvpListCount > 0)
            {
                kvpList = new List<KeyValuePair<int, byte[]>>(kvpListCount);
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
                            tagValue =
                                inDeserializationContext.StringHashCollection.GetStringByteArray(
                                    inDeserializationContext.TypeId, tagValue);
                        }
                    }
                    kvpList.Add(new KeyValuePair<int, byte[]>(tagHashCode, tagValue));
                }
            }
            return new InternalItem { ItemId = itemId, TagList = kvpList };
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
                    InDeserializationContext.TagHashCollection))
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