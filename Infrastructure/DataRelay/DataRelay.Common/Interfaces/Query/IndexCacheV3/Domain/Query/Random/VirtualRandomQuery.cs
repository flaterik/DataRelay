using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class VirtualRandomQuery: RandomQuery, IVirtualCacheType
	{
		#region Ctors
		public VirtualRandomQuery()
		{
			Init(null);
		}

        public VirtualRandomQuery(byte[] indexId, int count, string targetIndexName, string cacheTypeName)
            : base(indexId, count, targetIndexName)
        {
            Init(cacheTypeName);
        }

        public VirtualRandomQuery(byte[] indexId, int count, string targetIndexName, bool excludeData, bool getMetadata, Filter filter, string cacheTypeName)
			: base(indexId, count, targetIndexName, excludeData, getMetadata, filter)
		{
			Init(cacheTypeName);
		}

		private void Init(string cacheTypeName)
		{
			this.cacheTypeName = cacheTypeName;
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
