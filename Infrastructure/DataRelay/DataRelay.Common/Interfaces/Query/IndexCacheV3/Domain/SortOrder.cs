using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class SortOrder:IVersionSerializable
	{
		#region Data Members
		private DataType dataType;
		public DataType DataType
		{
			get
			{
				return dataType;
			}
			set
			{
				dataType = value;
			}
		}

		private SortBy sortBy;
		public SortBy SortBy
		{
			get
			{
				return sortBy;
			}
			set
			{
				sortBy = value;
			}
		}
		#endregion

		#region Ctors
		public SortOrder()
		{
			Init(DataType.Int32, SortBy.DESC);
		}

		public SortOrder(DataType dataType, SortBy sortBy)
		{
			Init(dataType, sortBy);
		}

		private void Init(DataType dataType, SortBy sortBy)
		{
			this.dataType = dataType;
			this.sortBy = sortBy;
		}
		#endregion

		#region IVersionSerializable Members
		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			//DataType
			writer.Write((byte)dataType);

			//SortBy
			writer.Write((byte)sortBy);
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
			//DataType
			dataType = (DataType)reader.ReadByte();

			//SortBy
			sortBy = (SortBy)reader.ReadByte();
		}
		#endregion
	}
}
