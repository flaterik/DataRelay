using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using Wintellect.PowerCollections;
using MySpace.Common.Framework;
using MySpace.DataRelay.Common.Util;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class CacheIndex : IExtendedCacheParameter, IVersionSerializable
	{
		private byte[] indexId;
		private List<CacheData> cacheDataList;
		private List<CacheData> cacheDataDeleteList;


		// Constructors
		public CacheIndex()
		{
			// param less constructor required for RelayMessage.GetObject<T>()
			this.indexId = null;
			this.cacheDataList = new List<CacheData>();
			this.cacheDataDeleteList = new List<CacheData>();
		}
		public CacheIndex(byte[] indexId)
		{
			this.indexId = indexId;
			this.cacheDataList = new List<CacheData>();
			this.cacheDataDeleteList = new List<CacheData>();
		}

		// Properties
		public byte[] IndexId
		{
			get
			{
				return this.indexId;
			}
			set
			{
				this.indexId = value;
			}
		}
		public IList<CacheData> CacheDataList
		{
			get
			{
				return this.cacheDataList;
			}
		}
		public IList<CacheData> CacheDataDeleteList
		{
			get
			{
				return this.cacheDataDeleteList;
			}
		}

		// Methods
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
		public void Prune(DateTime MinValidDate)
		{
			int index;
			for (index = cacheDataList.Count - 1; index >= 0; index--)
			{
				if (cacheDataList[index].CreateTimestamp <= MinValidDate)
				{
					// item at this index and all before are valid
					break;
				}
				else
				{
					// remove item at this index
					cacheDataList.Remove(cacheDataList[index]);
				}
			}
		}
		public void Add(CacheData cacheData)
		{
			this.cacheDataList.Add(cacheData);
		}
		public void AddToDeleteList(CacheData cacheData)
		{
			this.cacheDataDeleteList.Add(cacheData);
		}
		public void Sort()
		{
			cacheDataList.Sort(CacheDataComparison);
		}

		#region CacheDataComparer/Comparison
		internal static int CacheDataComparer(CacheData o1, CacheData o2)
		{
			return o1.CreateTimestamp.CompareTo(o2.CreateTimestamp);
		}
		internal static Comparison<CacheData> CacheDataComparison = new Comparison<CacheData>(CacheDataComparer);
		#endregion

		#region IExtendedCacheParameter Members
		public string ExtendedId
		{
			get
			{
				return Convert.ToBase64String(indexId);
			}
			set
			{
				throw new Exception("Setter for 'CacheIndex.ExtendedId' is not implemented and should not be invoked!");
			}
		}
		private DateTime? lastUpdatedDate;
		public DateTime? LastUpdatedDate
		{
			get { return lastUpdatedDate; }
			set { lastUpdatedDate = value; }
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
				throw new Exception("Setter for 'CacheIndex.PrimaryId' is not implemented and should not be invoked!");
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
			get { return false; }
			set { return; }
		}
		public bool IsValid
		{
			get { return true; }
		}
		public bool EditMode
		{
			get { return false; }
			set { return; }
		}
		#endregion

		#region IVersionSerializable Members
		private static void SerializeList(MySpace.Common.IO.IPrimitiveWriter writer, IList<CacheData> list)
		{			
			int count = 0;
			if (list != null && list.Count > 0)
			{
				count = list.Count;
				writer.Write(count);

				CacheData cacheData;
				for (int i = 0; i < count; i++)
				{
					cacheData = list[i];

					writer.Write(cacheData.Id.Length);
					writer.Write(cacheData.Id);

					if (cacheData.Data == null)
					{
						writer.Write((int)0);
					}
					else
					{
						writer.Write(cacheData.Data.Length);
						writer.Write(cacheData.Data);
					}				
					writer.Write(new SmallDateTime(cacheData.CreateTimestamp).TicksInt32);
					writer.Write(cacheData.CacheTypeId);
				}				
			}
			else
			{
				writer.Write(count);
			}
		}

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			writer.Write(indexId.Length);
			writer.Write(indexId);

			SerializeList(writer, cacheDataList);
			SerializeList(writer, cacheDataDeleteList);
		}
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			Deserialize(reader);
		}
		public int CurrentVersion
		{
			get { return 1; }
		}
		public bool Volatile
		{
			get { return false; }
		}
		#endregion

		#region ICustomSerializable Members	
		private static List<CacheData> DeserializeList(MySpace.Common.IO.IPrimitiveReader reader, byte[] indexId)
		{			
			int count = reader.ReadInt32();
			List<CacheData> list = new List<CacheData>();
			if (count > 0)
			{
				for (int i = 0; i < count; i++)
				{
					list.Add(new CacheData(
						indexId, 
						reader.ReadBytes(reader.ReadInt32()), 
						reader.ReadBytes(reader.ReadInt32()), 
						new SmallDateTime(reader.ReadInt32()).FullDateTime,
						reader.ReadInt32() ));
				}
			}

			return list;
		}

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			indexId = reader.ReadBytes(reader.ReadInt32());
			cacheDataList = DeserializeList(reader, indexId);
			cacheDataDeleteList = DeserializeList(reader, indexId);
		}
		#endregion		
	}
}
