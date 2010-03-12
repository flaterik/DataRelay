using System;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class ContainsIndexQuery: IRelayMessageQuery, IPrimaryQueryId
	{
		#region Data Members
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

		private byte[] indexId;
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

		private byte[] id;
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


		#endregion

		#region Ctors
		public ContainsIndexQuery()
		{
			this.cacheDataReferenceType = CacheDataReferenceTypes.CacheData;
			this.indexId = null;
			this.id = null;
			this.cacheType = null;
			this.preferredIndexName = null;
			this.returnAllSortFields = false;
			this.metadataRequested = false;
		}

		public ContainsIndexQuery(
			CacheDataReferenceTypes cacheDataReferenceType,
			byte[] indexId, 
			byte[] dataId, 
			byte[] cacheType, 
			bool returnAllSortFields, 
			string preferredIndexName, 
			bool metadataRequested)
		{
			this.cacheDataReferenceType = cacheDataReferenceType;
			this.indexId = indexId;
			this.id = dataId;
			this.cacheType = cacheType;
			this.returnAllSortFields = returnAllSortFields;
			this.preferredIndexName = preferredIndexName;
			this.metadataRequested = metadataRequested;
		}
		#endregion

		#region IRelayMessageQuery Members

		public byte QueryId
		{
			get
			{
				return (byte)QueryTypes.ContainsIndexQuery;
			}
		}

		#endregion

		#region IVersionSerializable Members

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			writer.Write((byte)cacheDataReferenceType);

			if (indexId == null)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)indexId.Length);
				writer.Write(indexId);
			}

			if (Id == null)
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

			writer.Write(returnAllSortFields);
			writer.Write(preferredIndexName);
			writer.Write(metadataRequested);
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
			cacheDataReferenceType = (CacheDataReferenceTypes) reader.ReadByte();
			indexId = reader.ReadBytes(reader.ReadUInt16());
			id = reader.ReadBytes(reader.ReadUInt16());
			cacheType = reader.ReadBytes(reader.ReadUInt16());
			returnAllSortFields = reader.ReadBoolean();
			preferredIndexName = reader.ReadString();
			metadataRequested = reader.ReadBoolean();
		}

		#endregion

		#region IPrimaryQueryId Members

		public int PrimaryId
		{
			get 
			{
				return GeneratePrimaryId(indexId);
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
	}
}
