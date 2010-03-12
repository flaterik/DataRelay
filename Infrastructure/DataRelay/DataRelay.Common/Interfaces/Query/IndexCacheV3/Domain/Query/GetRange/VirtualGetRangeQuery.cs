using System;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class VirtualGetRangeQuery: GetRangeQuery, IVirtualCacheType
	{
		#region Ctors
		public VirtualGetRangeQuery()
		{
			Init(null);
		}

		public VirtualGetRangeQuery(byte[] indexId, int offset, int itemNum, string targetIndexName, string cacheTypeName)
			: base(indexId, offset, itemNum, targetIndexName)
		{
			Init(cacheTypeName);
		}

        [Obsolete("This constructor is obsolete; use object initializer instead")]
		public VirtualGetRangeQuery(byte[] indexId, int offset, int itemNum, string targetIndexName, CriterionList criterionList, string cacheTypeName)
			: base(indexId, offset, itemNum, targetIndexName, criterionList)
		{
			Init(cacheTypeName);
		}

        [Obsolete("This constructor is obsolete; use object initializer instead")]
		public VirtualGetRangeQuery(byte[] indexId, int offset, int itemNum, string targetIndexName, CriterionList criterionList, bool excludeData, bool getMetadata, string cacheTypeName)
			: base(indexId, offset, itemNum, targetIndexName, criterionList, excludeData, getMetadata)
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
