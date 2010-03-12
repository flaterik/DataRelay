using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class VirtualPagedIndexDataQuery : VirtualPagedIndexQuery<PageIndexDataQueryResult, CacheData>
	{
		public VirtualPagedIndexDataQuery()
			: base(CacheDataReferenceTypes.CacheData)
		{
		}

		public VirtualPagedIndexDataQuery(int pageSize, int pageNum, byte[] minValue, byte[] maxValue, List<byte[]> indexIdList, List<byte[]> cacheTypeList, bool cacheTypeListIsInclusionList, bool metadataRequested, bool returnAllSortFields, string preferredIndexName, string cacheTypeName)
			: base(pageSize, pageNum, minValue, maxValue, indexIdList, cacheTypeList, cacheTypeListIsInclusionList, metadataRequested, returnAllSortFields, preferredIndexName, CacheDataReferenceTypes.CacheData, cacheTypeName)
		{
		}
	}

	public class VirtualPagedIndexSortableDataReferenceQuery : VirtualPagedIndexQuery<PageIndexSortableDataReferenceQueryResult, SortableCacheDataReference>
	{
		public VirtualPagedIndexSortableDataReferenceQuery()
			: base(CacheDataReferenceTypes.SortableCacheDataReference)
		{
		}

		public VirtualPagedIndexSortableDataReferenceQuery(int pageSize, int pageNum, byte[] minValue, byte[] maxValue, List<byte[]> indexIdList, List<byte[]> cacheTypeList, bool cacheTypeListIsInclusionList, bool metadataRequested, bool returnAllSortFields, string preferredIndexName, string cacheTypeName)
			: base(pageSize, pageNum, minValue, maxValue, indexIdList, cacheTypeList, cacheTypeListIsInclusionList, metadataRequested, returnAllSortFields, preferredIndexName, CacheDataReferenceTypes.SortableCacheDataReference, cacheTypeName)
		{
		}
	}

	public class VirtualPagedIndexDataReferenceQuery : VirtualPagedIndexQuery<PageIndexDataReferenceQueryResult, CacheDataReference>
	{
		public VirtualPagedIndexDataReferenceQuery()
			: base(CacheDataReferenceTypes.CacheDataReference)
		{
		}

		public VirtualPagedIndexDataReferenceQuery(int pageSize, int pageNum, byte[] minValue, byte[] maxValue, List<byte[]> indexIdList, List<byte[]> cacheTypeList, bool cacheTypeListIsInclusionList, bool metadataRequested, bool returnAllSortFields, string preferredIndexName, string cacheTypeName)
			: base(pageSize, pageNum, minValue, maxValue, indexIdList, cacheTypeList, cacheTypeListIsInclusionList, metadataRequested, returnAllSortFields, preferredIndexName, CacheDataReferenceTypes.CacheDataReference, cacheTypeName)
		{
		}
	}

	public class VirtualPagedIndexQuery<TQueryResult, TItem> : PagedIndexQuery<TQueryResult, TItem>, IVirtualCacheType
		where TQueryResult : PagedIndexQueryResult<TItem>, new()
		where TItem : CacheDataReference, new()
	{
		#region Ctors
		public VirtualPagedIndexQuery():base()
		{
			cacheTypeName = null;
		}

		public VirtualPagedIndexQuery(CacheDataReferenceTypes cacheDataReferenceType):base(cacheDataReferenceType)
		{
			this.cacheTypeName = null;
		}

		public VirtualPagedIndexQuery(int pageSize, int pageNum, 	byte[] minValue, byte[] maxValue, List<byte[]> indexIdList, List<byte[]> cacheTypeList, bool cacheTypeListIsInclusionList, bool metadataRequested, bool returnAllSortFields, string preferredIndexName, CacheDataReferenceTypes cacheDataReferenceType, string cacheTypeName)
			: base(pageSize, pageNum, minValue, maxValue, indexIdList, cacheTypeList, cacheTypeListIsInclusionList, metadataRequested, returnAllSortFields, preferredIndexName, cacheDataReferenceType)
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
