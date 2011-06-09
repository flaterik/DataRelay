using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class VirtualTagQuery : TagQuery, IVirtualCacheType
    {
        public string CacheTypeName
        {
            get; set;
        }
    }
}
