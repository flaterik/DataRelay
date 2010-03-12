using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class CacheData:SortableCacheDataReference
	{    
		#region Data members
		private byte[] data;
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
		#endregion

		#region Ctors
		public CacheData():base()
		{
			this.data = null;
		}

		//All fields
		public CacheData(
			byte[] id, 
			byte[] indexId, 
			byte[] cacheType, 
			Dictionary<string /*Indexname*/, byte[] /*Value*/> sortFields, 
			byte[] data)
			: base(id, indexId, cacheType, sortFields)
		{
			this.data = data;
		}

		// No CacheType
		public CacheData(
			byte[] id, 
			byte[] indexId,
			Dictionary<string /*Indexname*/, byte[] /*Value*/> sortFields, 
			byte[] data)
			: base(id, indexId, null, sortFields)
		{
			this.data = data;
		}

		//No SortFields
		public CacheData(
			byte[] id,
			byte[] indexId,
			byte[] cacheType,
			byte[] data)
			: base(id, indexId, cacheType, null)
		{
			this.data = data;
		}

		//No CacheType, No SortFields
		public CacheData(
			byte[] id,
			byte[] indexId,
			byte[] data)
			: base(id, indexId, null, null)
		{
			this.data = data;
		}

		#endregion

		#region IVersionSerializable Members
		public override void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			base.Serialize(writer);
			
			if (data == null)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)data.Length);
				writer.Write(data);
			}
		}
		public override void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			Deserialize(reader);
		}
		#endregion

		#region ICustomSerializable Members
		public override void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			base.Deserialize(reader);

			ushort Count = reader.ReadUInt16();
			if (Count > 0)
			{
				data = reader.ReadBytes(Count);
			}
		}
		#endregion
	}
}
