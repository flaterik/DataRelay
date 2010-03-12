using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.ListCache
{
    public class VirtualCacheList : CacheList, IVirtualCacheType
    {
        #region IVirtualCacheType Members
        protected string cacheTypeName;
        public string CacheTypeName
        {
            get
            {
                return this.cacheTypeName;
            }
            set
            {
                this.cacheTypeName = value;
            }
        }

        #endregion
    }
}
