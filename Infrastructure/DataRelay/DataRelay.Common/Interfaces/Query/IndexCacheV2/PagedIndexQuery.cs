using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class PagedIndexQuery<TQueryResult, TItem> : IMergeableQueryResult<TQueryResult>, IRelayMessageQuery
		where TQueryResult : PagedIndexQueryResult<TItem>, new()
		where TItem : CacheDataReference, new()
	{
		#region Data Members
		private int pageSize;
		public int PageSize
		{
			get
			{
				return pageSize;
			}
			set
			{
				pageSize = value;
			}
		}

		private int pageNum;
		public int PageNum
		{
			get
			{
				return pageNum;
			}
			set
			{
				pageNum = value;
			}
		}

		private byte[] preferredIndexMinValue;
		public byte[] PreferredIndexMinValue
		{
			get
			{
				return preferredIndexMinValue;
			}
			set
			{
				preferredIndexMinValue = value;
			}
		}

		private byte[] preferredIndexMaxValue;
		public byte[] PreferredIndexMaxValue
		{
			get
			{
				return preferredIndexMaxValue;
			}
			set
			{
				preferredIndexMaxValue = value;
			}
		}

		private List<byte[]> indexIdList;
		public List<byte[]> IndexIdList
		{
			get
			{
				return indexIdList;
			}
			set
			{
				indexIdList = value;
			}
		}

		private List<byte[]> cacheTypeList;
		public List<byte[]> CacheTypeList
		{
			get
			{
				return cacheTypeList;
			}
			set
			{
				cacheTypeList = value;
			}
		}

		private bool cacheTypeListIsInclusionList;
		public bool CacheTypeListIsInclusionList
		{
			get
			{
				return cacheTypeListIsInclusionList;
			}
			set
			{
				cacheTypeListIsInclusionList = value;
			}
		}

		private bool metadataRequested;
		public bool MetadataRequested
		{
			get
			{
				return metadataRequested;
			}
			set
			{
				metadataRequested = value;
			}
		}

		private string preferredIndexName;
		public string PreferredIndexName
		{
			get
			{
				return preferredIndexName;
			}
			set
			{
				preferredIndexName = value;
			}
		}

		private CacheDataReferenceTypes cacheDataReferenceType;
		public CacheDataReferenceTypes CacheDataReferenceType
		{
		  get 
		  {
			  return cacheDataReferenceType;
		  }
		  set 
		  { 
			  cacheDataReferenceType = value; 
		  }
		}

		private bool returnAllSortFields;
		public bool ReturnAllSortFields
		{
			get
			{
				return returnAllSortFields;
			}
			set
			{
				returnAllSortFields = value;
			}
		}

		#endregion

		#region Ctors
		public PagedIndexQuery()
		{
			Init();
		}

		public PagedIndexQuery(CacheDataReferenceTypes cacheDataReferenceType)
		{
			Init();
			this.cacheDataReferenceType = cacheDataReferenceType;			
		}

		public PagedIndexQuery(int pageSize, int pageNum, byte[] minValue, byte[] maxValue, List<byte[]> indexIdList, List<byte[]> cacheTypeList, bool cacheTypeListIsInclusionList, bool metadataRequested, bool returnAllSortFields, string preferredIndexName, CacheDataReferenceTypes cacheDataReferenceType)
		{
			this.pageSize = pageSize;
			this.pageNum = pageNum;
			this.preferredIndexMinValue = minValue;
			this.preferredIndexMaxValue = maxValue;
			this.indexIdList = indexIdList;
			this.cacheTypeList = cacheTypeList;
			this.cacheTypeListIsInclusionList = cacheTypeListIsInclusionList;
			this.metadataRequested = metadataRequested;
			this.preferredIndexName = preferredIndexName;
			this.cacheDataReferenceType = cacheDataReferenceType;
			this.returnAllSortFields = returnAllSortFields;
		}

		private void Init()
		{
			this.pageSize = -1;
			this.pageNum = -1;
			this.preferredIndexMinValue = null;
			this.preferredIndexMaxValue = null;
			this.indexIdList = new List<byte[]>();
			this.cacheTypeList = new List<byte[]>();
			this.cacheTypeListIsInclusionList = true;
			this.metadataRequested = false;
			this.preferredIndexName = null;
			this.returnAllSortFields = false;
		}
		#endregion

		#region IMergeableQueryResult<TQueryResult> Members
		public TQueryResult MergeResults(IList<TQueryResult> partialResults)
		{
			TQueryResult finalResult = default(TQueryResult);

			if (partialResults != null && partialResults.Count > 0)
			{
				if (partialResults.Count == 1)
				{
					// No need to merge anything
					finalResult = partialResults[0];
				}
				else
				{
					#region Merge partialResults into completeResultList
					List<TItem> completeResultList = new List<TItem>();
					ByteArrayEqualityComparer byteArrayEqualityComparer = new ByteArrayEqualityComparer();
					Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/> completeMetadata = 
						new Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/>(byteArrayEqualityComparer);
					int totalCount = 0;

					foreach (TQueryResult partialResult in partialResults)
					{
						if (partialResult != null)
						{
							totalCount += partialResult.TotalCount;
							Merge(
								ref completeResultList, 
								partialResult.ResultList, 
								partialResult.SortDescriptor, 
								PageSize, 
								partialResult.CacheDataReferenceType == CacheDataReferenceTypes.CacheDataReference ? true : false);

							if (metadataRequested)
							{
								foreach (KeyValuePair<byte[], byte[]> kvp in partialResult.ResultMetadata)
								{
									if (!completeMetadata.ContainsKey(kvp.Key))
									{
										completeMetadata.Add(kvp.Key, kvp.Value);
									}
								}
							}
						}
					}
					#endregion

					#region Use page logic
					// Paging required only in multicluster configuration on client side
					// Paging for single clustered configuration performed on server side
					if (pageNum != 0)
					{
						List<TItem> filteredResultList = new List<TItem>();
						int pageSize = (completeResultList.Count < PageSize ? completeResultList.Count : PageSize);
						filteredResultList = completeResultList.GetRange(0, pageSize);
					}
					#endregion

					#region Create final result
					finalResult = new TQueryResult();
					finalResult.ResultList = completeResultList;
					finalResult.TotalCount = totalCount;

					if (metadataRequested)
					{
						Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/> filteredMetadata = 
							new Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/>(byteArrayEqualityComparer);
						foreach (TItem cdr in finalResult.ResultList)
						{
							if (!filteredMetadata.ContainsKey(cdr.IndexId))
							{
								filteredMetadata.Add(cdr.IndexId, completeMetadata[cdr.IndexId]);
							}
						}
						finalResult.ResultMetadata = filteredMetadata;
					}
					#endregion
				}
			}
			return finalResult;
		}

		private void Merge(ref List<TItem> list1, List<TItem> list2, string sortDescriptor, int pageSize, bool isIndexPrimary)
		{
			#region Merge until one list ends
			IndexCacheComparer indexCacheComparer = new IndexCacheComparer(sortDescriptor);
			List<TItem> newList = new List<TItem>();
			int count1 = 0;
			int count2 = 0;

			for (int i = 0; i < list1.Count + list2.Count && count1 != list1.Count && count2 != list2.Count && newList.Count < pageSize; i++)
			{
				Encoding enc = new UTF8Encoding(false, true);

				if (indexCacheComparer.Compare(
					isIndexPrimary ? list1[count1].Id : (list1[count1] as SortableCacheDataReference).SortFields[preferredIndexName],
					isIndexPrimary ? list2[count2].Id : (list2[count2] as SortableCacheDataReference).SortFields[preferredIndexName]) 
					<= 0)
				{
					newList.Add(list1[count1++]);	// list1 item is greater
				}
				else
				{
					newList.Add(list2[count2++]); // list2 item is greater
				}
	      }
			#endregion

			#region Append rest of the list1/list2 to newList
			if (count1 != list1.Count && newList.Count < pageSize)
			{
				int count = list1.Count - count1;
				for(int i = 0; i < count; i++)
				{
					newList.Add(list1[count1++]);
				}
			}
			else if (count2 != list2.Count && newList.Count < pageSize)
			{
				int count = list2.Count - count2;
				for (int i = 0; i < count; i++)
				{
					newList.Add(list2[count2++]);
				}
			}
			#endregion

			#region Update reference
			list1 = newList;
			#endregion
		}

		#endregion

		#region IRelayMessageQuery Members
		public byte QueryId
		{
			get
			{
                return (byte)QueryTypes.PagedTaggedIndexQuery;
			}
		}
		#endregion

		#region IVersionSerializable Members

		public void Serialize(IPrimitiveWriter writer)
		{
			writer.Write((byte)cacheDataReferenceType);
			writer.Write(pageSize);
			writer.Write(pageNum);

			if (preferredIndexMinValue == null || preferredIndexMinValue.Length == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)preferredIndexMinValue.Length);
				writer.Write(preferredIndexMinValue);
			}

			if (preferredIndexMaxValue == null || preferredIndexMaxValue.Length == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)preferredIndexMaxValue.Length);
				writer.Write(preferredIndexMaxValue);
			}

			ushort count = 0;
			if (indexIdList != null && indexIdList.Count > 0)
			{
				count = (ushort)indexIdList.Count;
			}
			writer.Write(count);

			if (count > 0)
			{
				writer.Write((ushort)indexIdList[0].Length);
				foreach (byte[] indexId in indexIdList)
				{
					writer.Write(indexId);
				}
			}

			count = 0;
			if (cacheTypeList != null && cacheTypeList.Count > 0)
			{
				count = (ushort)cacheTypeList.Count;
			}
			writer.Write(count);

			if (count > 0)
			{
				foreach (byte[] cacheType in cacheTypeList)
				{
					writer.Write((ushort)cacheType.Length);
					writer.Write(cacheType);
				}
			}

			writer.Write(cacheTypeListIsInclusionList);
			writer.Write(metadataRequested);
			writer.Write(returnAllSortFields);
			writer.Write(preferredIndexName);
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
			Deserialize(reader);
		}

		public int CurrentVersion
		{
			get
			{
				return 1;
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
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			cacheDataReferenceType = (CacheDataReferenceTypes)reader.ReadByte();
			pageSize = reader.ReadInt32();
			pageNum = reader.ReadInt32();
			
			ushort valueCount = reader.ReadUInt16();
			if (valueCount > 0)
			{
				preferredIndexMinValue = reader.ReadBytes(valueCount);
			}
			
			valueCount = reader.ReadUInt16();
			if (valueCount > 0)
			{
				preferredIndexMaxValue = reader.ReadBytes(valueCount);
			}

			ushort count = reader.ReadUInt16();
			if (count > 0)
			{
				ushort indexIdLength = reader.ReadUInt16();
				for (int i = 0; i < count; i++)
				{
					indexIdList.Add(reader.ReadBytes(indexIdLength));
				}
			}

			count = reader.ReadUInt16();
			if (count > 0)
			{
				for (int i = 0; i < count; i++)
				{
					cacheTypeList.Add(reader.ReadBytes(reader.ReadUInt16()));
				}
			}

			cacheTypeListIsInclusionList = reader.ReadBoolean();
			metadataRequested = reader.ReadBoolean();
			returnAllSortFields = reader.ReadBoolean();
			preferredIndexName = reader.ReadString();
		}

		#endregion

		#region Methods
		public static int GeneratePrimaryId(byte[] bytes)
		{
			// TBD
			if (bytes == null || bytes.Length == 0)
			{
				return 1;
			}
			else
			{
				if (bytes.Length >= 4)
				{
					return Math.Abs(BitConverter.ToInt32(bytes, 0));
				}
				else
				{
					return Math.Abs((int)bytes[0]);
				}
			}
		}
		#endregion
	}
}
