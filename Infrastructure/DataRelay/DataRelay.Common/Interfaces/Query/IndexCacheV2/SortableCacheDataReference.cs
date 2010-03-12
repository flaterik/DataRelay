using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class SortableCacheDataReference:CacheDataReference
	{
		#region Data Members
		private Dictionary<string /*Indexname*/, byte[] /*Value*/> sortFields;
		public Dictionary<string/*Indexname*/, byte[]/*Value*/> SortFields
		{
			get 
			{ 
				return sortFields; 
			}
			set 
			{ 
				sortFields = value; 
			}
		}
		#endregion

		#region Ctors
		public SortableCacheDataReference():base()
		{
			sortFields = new Dictionary<string /*Indexname*/, byte[] /*Value*/>();
		}

		public SortableCacheDataReference(
			byte[] id, 
			byte[] indexId, 
			byte[] cacheType,
			Dictionary<string /*Indexname*/, byte[] /*Value*/> sortFields)
			: base(id, indexId, cacheType)
		{
			this.sortFields = sortFields;
		}
		#endregion

		#region IVersionSerializable Members

		public override void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			base.Serialize(writer);

			if (sortFields == null || sortFields.Count == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)sortFields.Count);
				foreach (KeyValuePair<string/*Indexname*/, byte[]/*Value*/> kvp in sortFields)
				{
					writer.Write(kvp.Key);
					writer.Write((ushort)kvp.Value.Length);
					writer.Write(kvp.Value);
				}
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

			ushort count = reader.ReadUInt16();
			if (count > 0)
			{
				string key;
				byte[] value;
				for (int i = 0; i < count; i++)
				{
					key = reader.ReadString();
					value = reader.ReadBytes(reader.ReadUInt16());
					sortFields.Add(key, value);
				}
			}
		}

		#endregion
	}
}
