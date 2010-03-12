using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class ContainsIndexQueryDataResult : ContainsIndexQueryResult<CacheData>
	{
		public ContainsIndexQueryDataResult():base()
		{
		}

		public ContainsIndexQueryDataResult(CacheData cacheData, byte[] metadata, bool indexExists, int indexSize)
			: base(cacheData, metadata, indexExists, indexSize)
		{
		}

	}

	public class ContainsIndexQueryDataReferenceResult : ContainsIndexQueryResult<CacheDataReference>
	{
		public ContainsIndexQueryDataReferenceResult():base()
		{
		}

		public ContainsIndexQueryDataReferenceResult(CacheDataReference cacheDataReference, byte[] metadata, bool indexExists, int indexSize)
			: base(cacheDataReference, metadata, indexExists, indexSize)
		{
		}
	}

	public class ContainsIndexQuerySortableDataReferenceResult : ContainsIndexQueryResult<SortableCacheDataReference>
	{
		public ContainsIndexQuerySortableDataReferenceResult():base()
		{
		}

		public ContainsIndexQuerySortableDataReferenceResult(SortableCacheDataReference sortableCacheDataReference, byte[] metadata, bool indexExists, int indexSize)
			: base(sortableCacheDataReference, metadata, indexExists, indexSize)
		{
		}
	}

	public class ContainsIndexQueryResult<TItem> : IVersionSerializable where TItem:CacheDataReference, new()
	{
		#region Data Members
		private TItem result;
		public TItem Result
		{
			get 
			{ 
				return result; 
			}
			set 
			{ 
				result = value; 
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

		private bool indexExists;
		public bool IndexExists
		{
			get 
			{ 
				return indexExists; 
			}
			set 
			{ 
				indexExists = value; 
			}
		}

		private int indexSize;
		public int IndexSize
		{
			get 
			{ 
				return indexSize; 
			}
			set 
			{ 
				indexSize = value; 
			}
		}

		#endregion

		#region Ctors
		public ContainsIndexQueryResult()
		{
			result = new TItem();
			metadata = null;
			indexExists = false;
			indexSize = 0;
		}

		public ContainsIndexQueryResult(TItem result, byte[] metadata, bool indexExists, int indexSize)
		{
			this.result = result;
			this.metadata = metadata;
			this.indexExists = indexExists;
			this.indexSize = indexSize;
		}
		#endregion
		
		#region IVersionSerializable Members

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			result.Serialize(writer);

			if (metadata == null)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)metadata.Length);
				writer.Write(metadata);
			}

			writer.Write(indexExists);
			writer.Write(indexSize);
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
			result.Deserialize(reader);

			ushort metadataLen = reader.ReadUInt16();
			if (metadataLen > 0)
				metadata = reader.ReadBytes(metadataLen);

			indexExists = reader.ReadBoolean();
			indexSize = reader.ReadInt32();
		}
		#endregion
	}
}
