using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class VirtualSpanQuery : SpanQuery, IVirtualCacheType
    {
        #region Ctors
        public VirtualSpanQuery()
        {
            Init(null);
        }

        public VirtualSpanQuery(SpanQuery query)
            : base(query)
        {
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
