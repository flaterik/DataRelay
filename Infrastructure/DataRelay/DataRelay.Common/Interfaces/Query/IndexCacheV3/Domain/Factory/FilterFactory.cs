using System;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    internal static class FilterFactory
    {        
        internal static Filter CreateFilter(IPrimitiveReader reader, FilterType filterType)
        {
            Filter filter;

            switch (filterType)
            {
                case FilterType.And:                    
                    filter = new AndFilter();                    
                    break;

                case FilterType.Or:
                    filter = new OrFilter();                    
                    break;

                case FilterType.Condition:
                    filter = new Condition();                    
                    break;
                    
                default:
                    throw new Exception("Unknown FilterType " + filterType);
            }

            Serializer.Deserialize(reader.BaseStream, filter);
            return filter;
        }
    }
}
