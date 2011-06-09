using System;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IntersectionQueryResult : ItemList<IndexDataItem>, IVersionSerializable
	{
		#region Data Members

        public List<IndexDataItem> ResultItemList
        {
            get; set;
        }

        public Dictionary<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> IndexIdIndexHeaderMapping
        {
            get; set;
        }

        internal List<string> LocalIdentityTagNames
        {
            get; set;
        }

        internal bool IsTagPrimarySort
        {
            get; set;
        }

        internal string SortFieldName
        {
            get; set;
        }

        internal List<SortOrder> SortOrderList
        {
            get; set;
        }

        public string ExceptionInfo
        {
            get; set;
        }

        #endregion

		#region Ctors
		public IntersectionQueryResult()
		{
			Init(null, null, null, false, null, null, null);
		}

        public IntersectionQueryResult(
            List<IndexDataItem> resultItemList, 
            Dictionary<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> indexIdIndexHeaderMapping, 
            List<string> localIdentityTagNames,
            bool isTagPrimarySort, 
            string sortFieldName,
            List<SortOrder> sortOrderList, 
            string exceptionInfo)
		{
            Init(resultItemList, indexIdIndexHeaderMapping, localIdentityTagNames, isTagPrimarySort, sortFieldName, sortOrderList, exceptionInfo);
		}

        private void Init(
            List<IndexDataItem> resultItemList, 
            Dictionary<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> indexIdIndexHeaderMapping,
            List<string> localIdentityTagNames,
            bool isTagPrimarySort, 
            string sortFieldName, 
            List<SortOrder> sortOrderList, 
            string exceptionInfo)
		{
			ResultItemList = resultItemList;
			IndexIdIndexHeaderMapping = indexIdIndexHeaderMapping;
            LocalIdentityTagNames = localIdentityTagNames;
			IsTagPrimarySort = isTagPrimarySort;
			SortFieldName = sortFieldName;
			SortOrderList = sortOrderList;
			ExceptionInfo = exceptionInfo;
		}
		#endregion

		#region IVersionSerializable Members
		public void Serialize(IPrimitiveWriter writer)
		{
			//ResultItemList
			if (ResultItemList == null || ResultItemList.Count == 0)
			{
				writer.Write(0);
			}
			else
			{
				writer.Write(ResultItemList.Count);
				foreach (IndexDataItem resultItem in ResultItemList)
				{
                    resultItem.Serialize(writer);
				}
			}

			//IndexIdIndexHeaderMapping
			if (IndexIdIndexHeaderMapping == null || IndexIdIndexHeaderMapping.Count == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)IndexIdIndexHeaderMapping.Count);
                foreach (KeyValuePair<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> kvp in IndexIdIndexHeaderMapping)
				{
					//IndexId
					if (kvp.Key == null || kvp.Key.Length == 0)
					{
						writer.Write((ushort)0);
					}
					else
					{
						writer.Write((ushort)kvp.Key.Length);
						writer.Write(kvp.Key);
					}

                    //IndexHeader
				    Serializer.Serialize(writer, kvp.Value);
				}
			}

            //LocalIdentityTagNames
            if (LocalIdentityTagNames == null || LocalIdentityTagNames.Count == 0)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(LocalIdentityTagNames.Count);
                foreach (string tagName in LocalIdentityTagNames)
                {
                    writer.Write(tagName);
                }
            }

			//IsTagPrimarySort
			writer.Write(IsTagPrimarySort);

			//SortFieldName
			// Note : this field will only be needed if PrimarySort field is tag
			if (IsTagPrimarySort)
			{
				writer.Write(SortFieldName);
			}
			else
			{
				writer.Write("");
			}

			//SortOrderList
			if (SortOrderList == null || SortOrderList.Count == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)SortOrderList.Count);
				foreach (SortOrder sortOrder in SortOrderList)
				{
					sortOrder.Serialize(writer);
				}
			}

			//ExceptionInfo
			writer.Write(ExceptionInfo);
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
            //ResultItemList
            int listCount = reader.ReadInt32();
            if (listCount > 0)
            {
                ResultItemList = new List<IndexDataItem>(listCount);
                IndexDataItem resultItem;
                for (int i = 0; i < listCount; i++)
                {
                    resultItem = new IndexDataItem();
                    resultItem.Deserialize(reader);
                    ResultItemList.Add(resultItem);
                }
            }

            //IndexIdIndexHeaderMapping
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexIdIndexHeaderMapping = new Dictionary<byte[], IndexHeader>(count, new ByteArrayEqualityComparer());
                byte[] indexId;
                IndexHeader indexHeader;
                ushort len;

                for (ushort i = 0; i < count; i++)
                {
                    len = reader.ReadUInt16();
                    indexId = null;
                    if (len > 0)
                    {
                        indexId = reader.ReadBytes(len);
                    }
                    indexHeader = new IndexHeader();
                    Serializer.Deserialize(reader.BaseStream, indexHeader);

                    IndexIdIndexHeaderMapping.Add(indexId, indexHeader);
                }
            }

            //LocalIdentityTagNames
            listCount = reader.ReadInt32();
            if (listCount > 0)
            {
                LocalIdentityTagNames = new List<string>(listCount);
                for (int i = 0; i < listCount; i++)
                {
                    LocalIdentityTagNames.Add(reader.ReadString());
                }
            }

            //IsTagPrimarySort
            IsTagPrimarySort = reader.ReadBoolean();

            //SortFieldName
            SortFieldName = reader.ReadString();

            //SortOrderList
            count = reader.ReadUInt16();
            SortOrderList = new List<SortOrder>(count);
            SortOrder sortOrder;
            for (int i = 0; i < count; i++)
            {
                sortOrder = new SortOrder();
                sortOrder.Deserialize(reader);
                SortOrderList.Add(sortOrder);
            }

            //ExceptionInfo
            ExceptionInfo = reader.ReadString();
		}

        private const int CURRENT_VERSION = 1;
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

        #region ItemList Members
        public override IndexDataItem GetItem(int pos)
        {
            return ResultItemList[pos];
        }

        public override int Count
        {
            get
            {
                if (ResultItemList != null)
                {
                    return ResultItemList.Count;
                }
                return 0;
            }
        }

        public override void RemoveAt(int pos)
        {
            if (ResultItemList != null)
            {
                ResultItemList.RemoveAt(pos);
            }
        }

        public override void RemoveRange(int startPos, int count)
        {
            if (ResultItemList != null)
            {
                ResultItemList.RemoveRange(startPos, count);
            }
        }

        public override int BinarySearchItem(
            IndexDataItem searchItem, 
            bool isTagPrimarySort, 
            string sortFieldName, 
            List<SortOrder> sortOrderList, 
            List<string> localIdentityTagNames)
        {
            try
            {
                int searchIndex = -1;
                if (ResultItemList != null && ResultItemList.Count > 0)
                {
                    IndexDataItemComparer comparer = new IndexDataItemComparer(isTagPrimarySort, sortFieldName, sortOrderList);
                    searchIndex = ResultItemList.BinarySearch(searchItem, comparer);

                    //Look for localIdentity at searchIndex
                    if (searchIndex > -1 && localIdentityTagNames.Count > 0)
                    {
                        if (EqualsLocalId(ResultItemList[searchIndex], searchItem, localIdentityTagNames))
                        {
                            return searchIndex;
                        }

                        //Search to left of the searchIndex
                        int newIndex = searchIndex - 1;
                        while (newIndex > -1 && comparer.Compare(ResultItemList[newIndex], searchItem) == 0)
                        {
                            if (EqualsLocalId(ResultItemList[newIndex], searchItem, localIdentityTagNames))
                            {
                                return newIndex;
                            }
                            newIndex--;
                        }

                        //Search to right of index
                        newIndex = searchIndex + 1;
                        while (newIndex < Count && comparer.Compare(ResultItemList[newIndex], searchItem) == 0)
                        {
                            if (EqualsLocalId(ResultItemList[newIndex], searchItem, localIdentityTagNames))
                            {
                                return newIndex;
                            }
                            newIndex++;
                        }
                        return -1;
                    }
                }
                return searchIndex;
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Binary Search", ex);
            }
        }
        #endregion
    }
}
