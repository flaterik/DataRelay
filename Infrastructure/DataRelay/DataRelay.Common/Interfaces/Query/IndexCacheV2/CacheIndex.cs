using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using MySpace.Common.Framework;
using MySpace.DataRelay.Common.Util;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	//The purpose of this enum is to serialize/deserialize a byte 
	//to identify type of TItem from this set {CacheDataReference, SortableCacheDataReference, CacheData}
	public enum CacheDataReferenceTypes : byte
	{
		CacheDataReference = 1,
		SortableCacheDataReference,
		CacheData

		// ALWAYS add new values to the END of the enumeration
	}

	public class CacheIndex<TItem> : IVersionSerializable, IExtendedRawCacheParameter where TItem : CacheDataReference, new()
	{
		#region Data Members
		protected byte[] indexId;
		public byte[] IndexId

		{
			get
			{
				return indexId;
			}
			set
			{
				indexId = value;
			}
		}

		protected List<TItem> addList;
		public List<TItem> AddList
		{
			get
			{
				return addList;
			}
			set
			{
				addList = value;
			}
		}

		protected byte[] metadata;
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

		protected List<TItem> deleteList;
		public List<TItem> DeleteList
		{
			get 
			{ 
				return deleteList; 
			}
			set 
			{ 
				deleteList = value; 
			}
		}

		protected bool updateMetadata;
		public bool UpdateMetadata
		{
			get
			{
				return updateMetadata;
			}
			set
			{
				updateMetadata = value;
			}
		}

		protected bool replaceFullIndex;
		public bool ReplaceFullIndex
		{
			get
			{
				return replaceFullIndex;
			}
			set
			{
				replaceFullIndex = value;
			}
		}
		#endregion

		#region Ctors
		public CacheIndex()
		{
			// param less constructor required for RelayMessage.GetObject<T>()
			this.indexId = null;
			this.addList = new List<TItem>();
			this.metadata = null;
			this.deleteList = new List<TItem>();
			this.updateMetadata = false;
			this.replaceFullIndex = false;
		}

		public CacheIndex(byte[] indexId, List<TItem> addList, List<TItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex)
		{
			this.indexId = indexId;
			this.addList = addList;
			this.metadata = metadata;
			this.deleteList = deleteList;
			this.updateMetadata = updateMetadata;
			this.replaceFullIndex = replaceFullIndex;
		}
		#endregion

		#region Methods
		public void Add(TItem cacheDataReference)
		{
			addList.Add(cacheDataReference);
		}
		
		public void AddToDeleteList(TItem cacheDataReference)
		{
			deleteList.Add(cacheDataReference);
		}

		//public void Sort()
		//{
		//   addList.Sort();
		//}
		#endregion

		#region IExtendedRawCacheParameter Members
		public byte[] ExtendedId
		{
			get
			{
				return indexId;
			}
			set
			{
				throw new Exception("Setter for 'CacheIndex.ExtendedId' is not implemented and should not be invoked!");
			}
		}
		private DateTime? lastUpdatedDate;
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
		public int PrimaryId
		{
			get
			{
				return GeneratePrimaryId(indexId);
			}
			set
			{
				throw new Exception("Setter for 'CacheIndexInternal.PrimaryId' is not implemented and should not be invoked!");
			}
		}

		private DataSource dataSource = DataSource.Unknown;
		public MySpace.Common.Framework.DataSource DataSource
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
					//return Math.Abs(Convert.ToBase64String(Id).GetHashCode());
					return Math.Abs((int)bytes[0]);
				}
			}
		}
		#endregion

		#region IVersionSerializable Members
		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			byte cacheDataReferenceType = (byte)0;
			if (addList.Count > 0 || deleteList.Count > 0)
			{
				TItem cdr = (addList.Count > 0) ? addList[0] : deleteList[0];
				if (cdr is CacheData)
				{
					cacheDataReferenceType = (byte)CacheDataReferenceTypes.CacheData;
				}
				else if (cdr is SortableCacheDataReference)
				{
					cacheDataReferenceType = (byte)CacheDataReferenceTypes.SortableCacheDataReference;
				}
				else if (cdr is CacheDataReference)
				{
					cacheDataReferenceType = (byte)CacheDataReferenceTypes.CacheDataReference;
				}
			}
			writer.Write(cacheDataReferenceType);

			if (indexId == null)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)indexId.Length);
				writer.Write(indexId);
			}

			int count = 0;
			if (addList != null && addList.Count > 0)
			{
				count = addList.Count;
			}
			writer.Write(count);

			if (count > 0)
			{
				foreach (TItem cdr in addList)
				{
					cdr.Serialize(writer);
				}
			}

			if (metadata == null)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)metadata.Length);
				writer.Write(metadata);
			}

			count = 0;
			if (deleteList != null && deleteList.Count > 0)
			{
				count = deleteList.Count;
			}
			writer.Write(count);

			if (count > 0)
			{
				foreach (TItem cdr in deleteList)
				{
					cdr.Serialize(writer);
				}
			}

			writer.Write(updateMetadata);
			writer.Write(replaceFullIndex);
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
			//Skip byte
			reader.ReadByte();

			ushort indexIdLength = reader.ReadUInt16();
			if (indexIdLength > 0)
			{
				indexId = reader.ReadBytes(indexIdLength);
			}

			int count = reader.ReadInt32();
			if (count > 0)
			{
				TItem cdr = new TItem();
				for (int i = 0; i < count; i++)
				{
					cdr.Deserialize(reader);
					addList.Add(cdr);
				}
			}

			ushort metadataLength = reader.ReadUInt16();
			if (metadataLength > 0)
				metadata = reader.ReadBytes(metadataLength);

			count = reader.ReadInt32();
			if (count > 0)
			{
				TItem cdr = new TItem();
				for (int i = 0; i < count; i++)
				{
					cdr.Deserialize(reader);
					deleteList.Add(cdr);
				}
			}

			updateMetadata = reader.ReadBoolean();
			replaceFullIndex = reader.ReadBoolean();
		}
		#endregion		
	}
}