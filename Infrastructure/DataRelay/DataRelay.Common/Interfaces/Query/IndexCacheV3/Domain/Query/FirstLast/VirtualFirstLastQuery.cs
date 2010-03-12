using System;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class VirtualFirstLastQuery: FirstLastQuery, IVirtualCacheType
	{
		#region Ctors
		public VirtualFirstLastQuery()
		{
			Init(null);
		}

		public VirtualFirstLastQuery(byte[] indexId, int firstPageSize, int lastPageSize, string targetIndexName, string cacheTypeName)
			: base(indexId, firstPageSize, lastPageSize, targetIndexName)
		{
			Init(cacheTypeName);
		}

        [Obsolete("This constructor is obsolete; use object initializer instead")]
		public VirtualFirstLastQuery(byte[] indexId, int firstPageSize, int lastPageSize, string targetIndexName, CriterionList criterionList, string cacheTypeName)
			: base(indexId, firstPageSize, lastPageSize, targetIndexName, criterionList)
		{
			Init(cacheTypeName);
		}

        [Obsolete("This constructor is obsolete; use object initializer instead")]
		public VirtualFirstLastQuery(byte[] indexId, int firstPageSize, int lastPageSize, string targetIndexName, CriterionList criterionList, bool excludeData, bool getMetadata, string cacheTypeName)
			: base(indexId, firstPageSize, lastPageSize, targetIndexName, criterionList, excludeData, getMetadata)
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
