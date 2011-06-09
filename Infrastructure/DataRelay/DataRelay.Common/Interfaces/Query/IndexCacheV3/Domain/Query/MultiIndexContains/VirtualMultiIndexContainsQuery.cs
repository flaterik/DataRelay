using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class VirtualMultiIndexContainsQuery : MultiIndexContainsQuery, IVirtualCacheType
    {
        #region Ctors

        public VirtualMultiIndexContainsQuery()
        {
            Init(null);
        }

        public VirtualMultiIndexContainsQuery(MultiIndexContainsQuery query, string cacheTypeName)
            : base(query)
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
