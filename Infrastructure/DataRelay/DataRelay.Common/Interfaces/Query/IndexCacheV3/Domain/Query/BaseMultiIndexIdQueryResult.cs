using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public abstract class BaseMultiIndexIdQueryResult : IVersionSerializable
	{
        #region Data Members

        public List<ResultItem> ResultItemList
		{
			get; internal set;
		}

        public Dictionary<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> IndexIdIndexHeaderMapping
		{
			get; internal set;
		}

		public int TotalCount
		{
			get; internal set;
		}

		internal bool IsTagPrimarySort
		{
			get; set;
		}

        internal string SortFieldName
		{
			get; set;
		}
		
        internal List<SortOrder> SortOrderList
		{
			get; set;
		}

        public int AdditionalAvailableItemCount
        {
            get; internal set;
        }

		public string ExceptionInfo
		{
			get; internal set;
		}

		#endregion

        #region IVersionSerializable Members

        public abstract int CurrentVersion
        {
            get;
        }

        public abstract void Deserialize(IPrimitiveReader reader, int version);

        public abstract void Serialize(IPrimitiveWriter writer);

        public bool Volatile
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region ICustomSerializable Members

        public void Deserialize(IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion
	}
}
