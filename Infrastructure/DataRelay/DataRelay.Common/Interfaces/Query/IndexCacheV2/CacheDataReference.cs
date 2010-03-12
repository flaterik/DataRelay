using System;
using MySpace.Common;
using MySpace.Common.Framework;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class CacheDataReference : IExtendedRawCacheParameter, IVersionSerializable, IVirtualCacheType
	{
		#region Data members
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

		protected byte[] id;
		public byte[] Id
		{
			get 
			{
				return id;
			}
			set
			{
				id = value;
			}
		}
		
		private byte[] cacheType;
		public byte[] CacheType
		{
			get
			{
				return cacheType;
			}
			set
			{
				cacheType = value;
			}
		}
		
		#endregion

		#region Ctors
		public CacheDataReference()
		{
			this.id = null;
			this.indexId = null;
			this.cacheType = null;
		}

		public CacheDataReference(byte[] id, byte[] indexId, byte[] cacheType)
		{
			this.id = id;
			this.indexId = indexId;
			this.cacheType = cacheType;
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

		public byte[] ExtendedIdBytes
		{
			get
			{
				return Id;
			}
		}

		#endregion

		#region IExtendedRawCacheParameter Members

		public byte[] ExtendedId
		{
			get
			{
				return Id;
			}
			set
			{
				throw new Exception("Setter for 'CacheDataReference.ExtendedId' is not implemented and should not be invoked!");
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

		#endregion

		#region IVersionSerializable Members

		public virtual void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			if (indexId == null)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)indexId.Length);
				writer.Write(indexId);
			}
			
			if (id == null)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)id.Length);
				writer.Write(id);
			}

			if (cacheType == null)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)cacheType.Length);
				writer.Write(cacheType);
			}			
		}

		public virtual void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
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

		public virtual void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			ushort indexIdLength = reader.ReadUInt16();
			if (indexIdLength > 0)
			{
				indexId = reader.ReadBytes(indexIdLength);
			}

			ushort idLength = reader.ReadUInt16();
			if (idLength > 0)
			{
				id = reader.ReadBytes(idLength);
			}

			ushort cacheTypeLength = reader.ReadUInt16();
			if (cacheTypeLength > 0)
			{
				cacheType = reader.ReadBytes(cacheTypeLength);
			}
		}

		#endregion

		#region IVirtualCacheType Members
		protected string cacheTypeName;

		public string CacheTypeName
		{
			get
			{
				return cacheTypeName;
			}
			set
			{
				cacheTypeName = value;
			}
		}
		#endregion
	}
}
