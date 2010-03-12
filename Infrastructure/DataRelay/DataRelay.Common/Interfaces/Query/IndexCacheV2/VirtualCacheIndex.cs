using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class VirtualCacheDataIndex : VirtualCacheIndex<CacheData>
	{ 
	}

	public class VirtualCacheDataReferenceIndex : VirtualCacheIndex<CacheDataReference>
	{
	}

	public class VirtualSortableCacheDataReferenceIndex : VirtualCacheIndex<SortableCacheDataReference>
	{
	}

	public class VirtualCacheIndex<TItem> : CacheIndex<TItem>, IVirtualCacheType
		where TItem : CacheDataReference, new()
	{
		#region Ctors
		public VirtualCacheIndex():base()
		{
			cacheTypeName = null;
		}

		public VirtualCacheIndex(string cacheTypeName)
			: base()
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
