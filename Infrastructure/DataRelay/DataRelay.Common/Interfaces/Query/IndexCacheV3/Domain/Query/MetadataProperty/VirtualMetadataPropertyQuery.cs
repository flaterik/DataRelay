using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class VirtualMetadataPropertyQuery : MetadataPropertyQuery, IVirtualCacheType
    {
        /// <summary>
        /// Gets or sets the name of the cache type.
        /// </summary>
        /// <value>The name of the cache type.</value>
        public string CacheTypeName
        {
            get; set;
        }
    }
}
