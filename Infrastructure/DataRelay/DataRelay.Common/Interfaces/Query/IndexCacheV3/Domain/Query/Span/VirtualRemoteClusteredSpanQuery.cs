using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class VirtualRemoteClusteredSpanQuery : RemoteClusteredSpanQuery, IVirtualCacheType
    {
        #region Ctors
        public VirtualRemoteClusteredSpanQuery()
        {
            Init(null);
        }

        public VirtualRemoteClusteredSpanQuery(string myCacheTypeName)
        {
            Init(myCacheTypeName);
        }

        private void Init(string myCacheTypeName)
        {
            this.cacheTypeName = myCacheTypeName;
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
