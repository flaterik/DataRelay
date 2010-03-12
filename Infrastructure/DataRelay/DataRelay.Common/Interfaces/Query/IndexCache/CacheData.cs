using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay;
using MySpace.Common;
using MySpace.Common.Framework;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class CacheData : CacheDataReference, IExtendedCacheParameter, IVersionSerializable
	{

		private static readonly Encoding stringEncoder = new UTF8Encoding(false, true); //same as the default for a BinaryWriter
		private byte[] data;
	
		// Constructors
		public CacheData() : base()
		{
			this.data = null;
		}
		//public CacheData(byte[] IndexId, byte[] Id, byte[] Data, DateTime CreateTimestamp)
		//    : base(Id, CreateTimestamp, IndexId, 0)
		//{
		//    this.data = Data;
		//}
		public CacheData(byte[] IndexId, byte[] Id, byte[] Data, DateTime CreateTimestamp, int CacheTypeId)
			: base(Id, CreateTimestamp, IndexId, CacheTypeId)
		{
			this.data = Data;
		}

		// Properties
		public byte[] Data
		{
			get
			{
				return this.data;
			}
			set
			{
				this.data = value;
			}
		}

		// Methods
		public byte[] ExtendedIdBytes
		{
			get
			{
				return Id;
			}
		}

		#region IExtendedCacheParameter Members
		public string ExtendedId
		{
			get
			{
				return Convert.ToBase64String(Id);
			}
			set
			{
				throw new Exception("Setter for 'CacheData.ExtendedId' is not implemented and should not be invoked!");
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
				return GeneratePrimaryId(Id);
			}
			set
			{
				throw new Exception("Setter for 'CacheData.PrimaryId' is not implemented and should not be invoked!");
			}
		}
		private DataSource dataSource = DataSource.Unknown;
		public DataSource DataSource
		{
			get { return dataSource; }
			set { dataSource = value; }
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
		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			if (this.data == null)
			{
				writer.Write((int)0);
			}
			else
			{
				writer.Write(this.data.Length);
				writer.Write(this.data);
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
			int Count = reader.ReadInt32();
			if (Count > 0)
			{
				data = reader.ReadBytes(Count);
			}
		}
		#endregion
	}
}
