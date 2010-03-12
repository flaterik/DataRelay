using System.Collections.Generic;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class ResultItem: IndexDataItem
	{
		#region Data Members
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
		#endregion

		#region Ctors
		public ResultItem()
		{
			Init(null);
		}

		public ResultItem(byte[] indexId, byte[] itemId, byte[] data, Dictionary<string /*TagName*/, byte[] /*TagValue*/> tags)
			: base(itemId, data, tags)
		{
			Init(indexId);
		}

		private void Init(byte[] indexId)
		{
			this.indexId = indexId;
		}
		#endregion

		#region IVersionSerializable Members
		public override void Serialize(IPrimitiveWriter writer)
		{
			base.Serialize(writer);

			//IndexId
			if (indexId == null || indexId.Length == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)indexId.Length);
				writer.Write(indexId);
			}
		}

		public override void Deserialize(IPrimitiveReader reader, int version)
		{
			Deserialize(reader);
		}

		#endregion

		#region ICustomSerializable Members

		public override void Deserialize(IPrimitiveReader reader)
		{
			base.Deserialize(reader);

			//IndexId
			ushort len = reader.ReadUInt16();
			if (len > 0)
			{
				indexId = reader.ReadBytes(len);
			}
		}

		#endregion

	}
}
