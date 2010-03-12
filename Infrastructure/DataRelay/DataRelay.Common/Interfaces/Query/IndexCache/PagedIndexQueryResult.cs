using System;
using System.Collections.Generic;
using System.Text;
//using MySpace.DataRelay.RelayComponent.RollingStorage.Objects;
using System.IO;
using MySpace.Common;
using MySpace.DataRelay.Common.Util;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class PagedIndexQueryResult : IVersionSerializable
	{
		// Data Members
		private List<CacheData> cacheDataList;
		private int totalCount;

		// Constructors
		public PagedIndexQueryResult()
		{
			cacheDataList = new List<CacheData>();
		}
		
		// Properties
		public List<CacheData> CacheDataList
		{
			get
			{
				return this.cacheDataList;
			}
			set
			{
				if (value == null)
				{
					this.cacheDataList = new List<CacheData>();
				}
				else
				{
					this.cacheDataList = value;
				}
			}
		}
		public int TotalCount
		{
			get
			{
				return this.totalCount;
			}
			set
			{
				this.totalCount = value;
			}
		}
		
		#region IVersionSerializable Members
		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			writer.Write(this.totalCount);
			writer.Write(cacheDataList.Count); // List count

			if (cacheDataList.Count > 0)
			{
				writer.Write(cacheDataList[0].IndexId.Length);
				writer.Write(cacheDataList[0].Id.Length);

				foreach (CacheData cd in cacheDataList)
				{					
					writer.Write(cd.IndexId);
					writer.Write(cd.Id);
					if (cd.Data == null)
					{
						writer.Write((int)0);
					}
					else
					{
						writer.Write(cd.Data.Length);
						writer.Write(cd.Data);
					}					
					writer.Write( new SmallDateTime(cd.CreateTimestamp).TicksInt32);
					writer.Write(cd.CacheTypeId);
				}
			}
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
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			this.totalCount = reader.ReadInt32();
			int count = reader.ReadInt32();
			cacheDataList = new List<CacheData>(count);
			if (count > 0)
			{
				int IndexIdLen = reader.ReadInt32();
				int IdLen = reader.ReadInt32();
				
				for (int i = 0; i < count; i++)
				{					
					cacheDataList.Add(
						new CacheData(
							reader.ReadBytes(IndexIdLen),							// IndexId
							reader.ReadBytes(IdLen),								// Id
							reader.ReadBytes(reader.ReadInt32()),					// Data
							new SmallDateTime(reader.ReadInt32()).FullDateTime,		// CreateTimeStamp
							reader.ReadInt32()));									// CacheTypeId
				}
			}			
		}
		#endregion				
	

	}
}
