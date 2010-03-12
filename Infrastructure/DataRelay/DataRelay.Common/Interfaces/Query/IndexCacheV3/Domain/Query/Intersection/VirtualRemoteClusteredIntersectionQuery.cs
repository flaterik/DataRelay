using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class VirtualRemoteClusteredIntersectionQuery : RemoteClusteredIntersectionQuery, IVirtualCacheType
    {
        #region Ctors
        public VirtualRemoteClusteredIntersectionQuery()
        {
            Init(null);
        }

        public VirtualRemoteClusteredIntersectionQuery(List<byte[]> indexIdList, string targetIndexName, string cacheTypeName)
            : base(indexIdList, targetIndexName)
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
