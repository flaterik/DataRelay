using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class VirtualRemoteClusteredPagedIndexQuery : RemoteClusteredPagedIndexQuery, IVirtualCacheType
    {
        #region Ctors
        public VirtualRemoteClusteredPagedIndexQuery()
        {
            Init(null);
        }

        public VirtualRemoteClusteredPagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName, string cacheTypeName)
            : base(indexIdList, pageSize, pageNum, targetIndexName)
        {
            Init(cacheTypeName);
        }

        public VirtualRemoteClusteredPagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName, int maxItemsPerIndex, string cacheTypeName)
            : base(indexIdList, pageSize, pageNum, targetIndexName, maxItemsPerIndex)
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
