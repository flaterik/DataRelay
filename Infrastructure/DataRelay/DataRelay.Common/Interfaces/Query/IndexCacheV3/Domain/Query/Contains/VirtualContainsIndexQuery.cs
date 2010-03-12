using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class VirtualContainsIndexQuery : ContainsIndexQuery, IVirtualCacheType
	{
		#region Ctors
		public VirtualContainsIndexQuery()
		{
			Init(null);
		}

        public VirtualContainsIndexQuery(byte[] indexId, IndexItem indexItem, string targetIndexName, string cacheTypeName)
            : base(indexId, indexItem, targetIndexName)
        {
            Init(cacheTypeName);
        }

        public VirtualContainsIndexQuery(byte[] indexId, IndexItem indexItem, string targetIndexName, List<string> tagsFromIndexes, bool excludeData, bool getMetadata, string cacheTypeName)
            : base(indexId, indexItem, targetIndexName, tagsFromIndexes, excludeData, getMetadata)
        {
            Init(cacheTypeName);
        }

		public VirtualContainsIndexQuery(byte[] indexId, List<IndexItem> indexItemList, string targetIndexName, string cacheTypeName)
            : base(indexId, indexItemList, targetIndexName)
		{
			Init(cacheTypeName);
		}

        public VirtualContainsIndexQuery(byte[] indexId, List<IndexItem> indexItemList, string targetIndexName, List<string> tagsFromIndexes, bool excludeData, bool getMetadata, string cacheTypeName)
            : base(indexId, indexItemList, targetIndexName, tagsFromIndexes, excludeData, getMetadata)
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
