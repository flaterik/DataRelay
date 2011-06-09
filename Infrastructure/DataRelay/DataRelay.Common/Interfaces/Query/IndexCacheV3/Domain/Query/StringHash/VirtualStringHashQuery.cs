using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class VirtualStringHashQuery : StringHashQuery, IVirtualCacheType
    {
        public string CacheTypeName
        {
            get; set;
        }
    }
}
