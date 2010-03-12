using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class PageIndexDataReferenceQueryResult : PagedIndexQueryResult<CacheDataReference>
	{
		public PageIndexDataReferenceQueryResult()
			: base()
		{
		}

		public PageIndexDataReferenceQueryResult(
			List<CacheDataReference> resultList,
			Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/> resultMetadata, 
			int totalCount, 
			string sortDescriptor,
			CacheDataReferenceTypes cacheDataReferenceType)
			: base(resultList, resultMetadata, totalCount, sortDescriptor, cacheDataReferenceType)
		{
		}
	}

	public class PageIndexDataQueryResult : PagedIndexQueryResult<CacheData>
	{
		public PageIndexDataQueryResult()
			: base()
		{
		}

		public PageIndexDataQueryResult(
			List<CacheData> resultList,
			Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/> resultMetadata, 
			int totalCount, 
			string sortDescriptor,
			CacheDataReferenceTypes cacheDataReferenceType)
			: base(resultList, resultMetadata, totalCount, sortDescriptor, cacheDataReferenceType)
		{
		}
	}

	public class PageIndexSortableDataReferenceQueryResult : PagedIndexQueryResult<SortableCacheDataReference>
	{
		public PageIndexSortableDataReferenceQueryResult()
			: base()
		{
		}

		public PageIndexSortableDataReferenceQueryResult(
			List<SortableCacheDataReference> resultList,
			Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/> resultMetadata, 
			int totalCount, 
			string sortDescriptor,
			CacheDataReferenceTypes cacheDataReferenceType)
			: base(resultList, resultMetadata, totalCount, sortDescriptor, cacheDataReferenceType)
		{
		}
	}

	public class PagedIndexQueryResult<TItem> :IVersionSerializable where TItem:CacheDataReference, new()
	{
		#region Data Members
		private List<TItem> resultList;
		public List<TItem> ResultList
		{
			get 
			{ 
				return resultList; 
			}
			set
			{ 
				resultList = value; 
			}
		}

		private Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/> resultMetadata;
		public Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/> ResultMetadata
		{
			get 
			{ 
				return resultMetadata; 
			}
			set
			{ 
				resultMetadata = value;
			}
		}

		private int totalCount;
		public int TotalCount
		{
			get
			{
				return totalCount;
			}
			set
			{
				totalCount = value;
			}
		}

		private string sortDescriptor;
		internal string SortDescriptor
		{
			get 
			{ 
				return sortDescriptor; 
			}
			set 
			{ 
				sortDescriptor = value; 
			}
		}

		private CacheDataReferenceTypes cacheDataReferenceType;
		internal CacheDataReferenceTypes CacheDataReferenceType
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
		#endregion

		#region Ctors
		public PagedIndexQueryResult()
		{
			this.resultList = new List<TItem>();
			ByteArrayEqualityComparer byteArrayEqualityComparer = new ByteArrayEqualityComparer();
			this.resultMetadata = new Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/>(byteArrayEqualityComparer);
			this.totalCount = 0;
			this.sortDescriptor = null;
			this.cacheDataReferenceType = CacheDataReferenceTypes.CacheData;
		}

		public PagedIndexQueryResult(
			List<TItem> resultList,
			Dictionary<byte[] /*IndexId*/, byte[] /*metadata*/> resultMetadata, 
			int totalCount, 
			string sortDescriptor,
			CacheDataReferenceTypes cacheDataReferenceType)
		{
			this.resultList = resultList;
			this.resultMetadata = resultMetadata;
			this.totalCount = totalCount;
			this.sortDescriptor = sortDescriptor;
			this.cacheDataReferenceType = cacheDataReferenceType;
		}
		#endregion

		#region IVersionSerializable Members
		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			writer.Write(totalCount);

			int count = 0;
			if(resultList != null && resultList.Count > 0)
			{
				count = resultList.Count;
			}
			writer.Write(count);

			if (count > 0)
			{
				foreach (TItem cdr in resultList)
				{
					cdr.Serialize(writer);
				}
			}

			count = 0;
			if (resultMetadata != null && resultMetadata.Count > 0)
			{
				count = resultMetadata.Count;
			}
			writer.Write((ushort)count);

			if (count > 0)
			{
				foreach (KeyValuePair<byte[]/*metadata*/, byte[] /*metadata*/> kvp in resultMetadata)
				{
					writer.Write((ushort)kvp.Key.Length);
					writer.Write(kvp.Key);
					if (kvp.Value != null)
					{
						writer.Write((ushort)kvp.Value.Length);
						if (kvp.Value.Length > 0)
						{
							writer.Write(kvp.Value);
						}
					}
					else
					{
						writer.Write((ushort)0);
					}
				}
			}

			writer.Write(sortDescriptor);
			writer.Write((byte)cacheDataReferenceType);
		}

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
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
			totalCount = reader.ReadInt32();

			int resultListCount = reader.ReadInt32();
			if (resultListCount > 0)
			{
				for (int i = 0; i < resultListCount; i++)
				{
					TItem cdr = new TItem();
					cdr.Deserialize(reader);
					resultList.Add(cdr);
				}
			}

			ushort resultMetadataCount = reader.ReadUInt16();
			if (resultMetadataCount > 0)
			{
				byte[] key;
				byte[] value;
				ushort keyLength;
				ushort valueLength;

				for (int i = 0; i < resultMetadataCount; i++)
				{
					key = value = null;
					
					keyLength = reader.ReadUInt16();
					if (keyLength > 0)
					{
						key = reader.ReadBytes(keyLength);
					}

					valueLength = reader.ReadUInt16();
					if (valueLength > 0)
					{
						value = reader.ReadBytes(valueLength);
					}

					if (key != null)
					{
						resultMetadata.Add(key, value);
					}
				}
			}

			sortDescriptor = reader.ReadString();
			cacheDataReferenceType = (CacheDataReferenceTypes)reader.ReadByte();
		}

		#endregion
	}
}
