using System;
using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class VirtualPagedIndexQuery: PagedIndexQuery, IVirtualCacheType
	{
		#region Ctors
		public VirtualPagedIndexQuery()
		{
			Init(null);
		}

       // copy ctor
       public VirtualPagedIndexQuery(PagedIndexQuery query)
           : base(query)
       {
       }

		public VirtualPagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName, string cacheTypeName)
			: base(indexIdList, pageSize, pageNum, targetIndexName)
		{
			Init(cacheTypeName);
		}

		public VirtualPagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName, string cacheTypeName, int maxItemsPerIndex)
			: base(indexIdList, pageSize, pageNum, targetIndexName, maxItemsPerIndex)
		{
			Init(cacheTypeName);
		}

        [Obsolete("This constructor is obsolete; use object initializer instead")]
		public VirtualPagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName, List<string> tagsFromIndexes, TagSort tagSort, CriterionList criterionList, int maxItemsPerIndex, bool excludeData, bool getIndexHeader, string cacheTypeName)
			: base(indexIdList, pageSize, pageNum, targetIndexName, tagsFromIndexes, tagSort, criterionList, maxItemsPerIndex, excludeData, getIndexHeader)
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
