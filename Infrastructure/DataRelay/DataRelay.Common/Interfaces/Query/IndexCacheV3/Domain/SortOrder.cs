using MySpace.Common;
using System.Text;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class SortOrder:IVersionSerializable
	{
		#region Data Members

	    public DataType DataType { get; set; }

	    public SortBy SortBy { get; set; }

	    #endregion

        #region Methods

        public override string ToString()
        {
            var stb = new StringBuilder();
            stb.Append("(").Append("DataType: ").Append(DataType.ToString()).Append("),");
            stb.Append("(").Append("SortBy: ").Append(SortBy.ToString()).Append("),");
            return stb.ToString();
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
			this.DataType = dataType;
			this.SortBy = sortBy;
		}

		#endregion

		#region IVersionSerializable Members

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			//DataType
			writer.Write((byte)DataType);

			//SortBy
			writer.Write((byte)SortBy);
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
			DataType = (DataType)reader.ReadByte();

			//SortBy
			SortBy = (SortBy)reader.ReadByte();
		}

		#endregion
	}
}
