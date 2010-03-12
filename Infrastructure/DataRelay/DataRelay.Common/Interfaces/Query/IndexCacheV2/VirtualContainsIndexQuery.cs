using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class VirtualContainsIndexQuery : ContainsIndexQuery, IVirtualCacheType
	{
		#region Ctors
		public VirtualContainsIndexQuery()
			: base()
		{
			cacheTypeName = null;
		}

		public VirtualContainsIndexQuery(
			CacheDataReferenceTypes cacheDataReferenceType,
			byte[] indexId,
			byte[] dataId,
			byte[] cacheType,
			bool returnAllSortFields,
			string preferredIndexName,
			bool metadataRequested,
			string cacheTypeName)
			: base(cacheDataReferenceType, indexId, dataId, cacheType, returnAllSortFields, preferredIndexName, metadataRequested)
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
