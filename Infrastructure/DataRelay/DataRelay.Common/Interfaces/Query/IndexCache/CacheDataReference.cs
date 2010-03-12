using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class CacheDataReference
	{
		// Data Members
		protected byte[] indexId;
		protected byte[] id;
		protected DateTime createTimestamp;
		protected int cacheTypeId;

		// Constructors
		public CacheDataReference()
		{
			Init(null, DateTime.MinValue, null, 0);
		}		
		public CacheDataReference(byte[] id, DateTime timestamp, byte[] indexId, int cacheTypeId)
		{
			Init(id, timestamp, indexId, cacheTypeId);	
		}
		private void Init(byte[] id, DateTime timestamp, byte[] indexId, int cacheTypeId)
		{			
			this.id = id;
			this.createTimestamp = timestamp;
			this.indexId = indexId;
			this.cacheTypeId = cacheTypeId;
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
		public byte[] Id
		{
			get
			{
				return this.id;
			}
			set
			{
				this.id = value;
			}
		}
		public DateTime CreateTimestamp
		{
			get
			{
				return this.createTimestamp;
			}
			set
			{
				this.createTimestamp = value;
			}
		}
		public int CacheTypeId
		{
			get
			{
				return this.cacheTypeId;
			}
			set
			{
				this.cacheTypeId = value;
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
					return Math.Abs( (int) bytes[0] );
				}
			}
		}		
	}
}
