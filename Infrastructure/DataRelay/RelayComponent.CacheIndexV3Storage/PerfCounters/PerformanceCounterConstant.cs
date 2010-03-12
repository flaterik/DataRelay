namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters
{
    /// <summary>
    /// This enum list all the counter (no instance) in the perf counte, 
    /// *** READ THIS BEFORE UPDATEING THIS ENUM***
    /// This enum needs to be sync with the 
    /// CounterInfo array below, the sequence should be the same
    /// </summary>
    public enum PerformanceCounterEnum
    {
        // save
        AddList,

        DeleteList,

        Save,

        NumberOfItemsInIndexPerSave,

        // Query
        ContainsIndexQuery,

        PagedIndexQuery,

        FirstLastQuery,

        GetRangeQuery,

        RandomQuery,

        IntersectionQuery,

        RemoteClusteredIntersectionQuery,

        // Index lookup for each type of query
        IndexLookupAvgPerPagedIndexQuery,

        IndexLookupAvgPerIntersectionQuery,

        IndexLookupAvgPerRemoteClusteredIntersectionQuery,

        // Number of items in an index for each type of query
        NumOfItemsInIndexPerContainsIndexQuery,

        NumOfItemsInIndexPerPagedIndexQuery,

        NumOfItemsInIndexPerFirstLastQuery,

        NumOfItemsInIndexPerGetRangeQuery,

        NumOfItemsInIndexPerRandomQuery,

        NumOfItemsInIndexPerIntersectionQuery,

        // Number of items deserialized(read) per query type
        NumOfItemsReadPerContainsIndexQuery,

        NumOfItemsReadPerPagedIndexQuery,

        NumOfItemsReadPerFirstLastQuery,

        NumOfItemsReadPerGetRangeQuery,

        NumOfItemsReadPerRandomQuery,

        NumOfItemsReadPerIntersectionQuery,

        // filter delete related counters
        FilterDelete,

        NumOfItemsFilteredPerFilterDeleteRequest,

        NumOfItemsInIndexPerFilterDeleteRequest,

        NumOfItemsReadPerFilterDeleteRequest,

        //Span Query Counters
        SpanQuery,

        IndexLookupAvgPerSpanQuery,

        NumOfItemsInIndexPerSpanQuery,

        NumOfItemsReadPerSpanQuery,

        RemoteClusteredPagedIndexQuery,

        IndexLookupAvgPerRemoteClusteredPagedIndexQuery,

        RemoteClusteredSpanQuery,

        IndexLookupAvgPerRemoteClusteredSpanQuery,

        // To add more, start from here
    }

    /// <summary>
    /// Contaions all the constant used in perf counter section
    /// </summary>
    public class PerformanceCounterConstant
    {
        public static string CategoryNameBase = "MySpace DataRelay IndexCacheV3";

        /// <summary>
        /// *** READ THIS BEFORE UPDATE THIS ENUM***
        /// This Counter info is a two dimension array, each array inside contains
        /// four pieces of information about a performance counter:
        /// 1. counter name string 
        /// 2. counter type string 
        /// 3. counter help string
        /// This array needs to be in sync with the 
        /// PerformanceCounterEnum enum above, the sequence should be the same
        /// </summary>
        public static readonly string[,] CounterInfo = new string[,]
        {
            // Save
            {  
                "Save - Items in AddList",
                "NumberOfItems32",
                "Number of items in the AddList"
            }, 
            
            {
                "Save - Items in DeleteList",
                "NumberOfItems32",
                "Number of items in the DeleteList"
            },

            {
                "Save - msg/sec",
                "RateOfCountsPerSecond64",
                "Total number of the save requests"
            }, 

            {
                "Save - Items in index/save",
                "NumberOfItems32",
                "Number of items in index per save request"
            }, 
            
            // Query
            {
                "ContainsIndexQuery - q/sec", 
                "RateOfCountsPerSecond64",
                "Number of the ContainsIndexQuery requests per second"
            },

            {
                "PagedIndexQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the PagedIndexQuery requests per second"
            },

            {
                "FirstLastQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the FirstLastQuery requests per second"
            },

            {
                "GetRangeQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the GetRangeQuery requests per second"
            },

            {
                "RandomQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the RandomQuery requests per second"
            },

            {
                "IntersectionQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the IntersectionQuery requests per second"
            },

            {
                "RemoteClusteredIntersectionQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the RemoteClusteredIntersectionQuery requests per second"
            },

            // Index lookup for each type of query
            {
                "PagedIndexQuery - Indexes lookup/query",
                "NumberOfItems32",
                "Number of the IndexLookup per PagedIndexQuery"
            },

            {
                "IntersectionQuery - Indexes lookup/query",
                "NumberOfItems32",
                "Number of the IndexLookup per IntersectionQuery"
            },

            {
                "RemoteClusteredIntersectionQuery - Indexes lookup/query",
                "NumberOfItems32",
                "Number of the IndexLookup per RemoteClusteredIntersectionQuery"
            },

            // Number of items in an index for each type of query
            {
                "ContainsIndexQuery - Items in index/query",
                "NumberOfItems32",
                "Number of items in index per ContainsIndexQuery"
            },

            {
                "PagedIndexQuery - Items in index/query",
                "NumberOfItems32",
                "Number of items in index per PagedIndexQuery"
            },

            {
                "FirstLastQuery - Items in index/query",
                "NumberOfItems32",
                "Number of items in index per FirstLastQuery"
            },

            {
                "GetRangeQuery - Items in index/query",
                "NumberOfItems32",
                "Number of items in index per GetRangeQuery"
            },

            {
                "RandomQuery - Items in index/query",
                "NumberOfItems32",
                "Number of items in index per RandomQuery"
            },

            {
                "IntersectionQuery - Items in index/query",
                "NumberOfItems32",
                "Number of items in index per IntersectionQuery"
            },

            // Number of items deserialized(read) per query type
            {
                "ContainsIndexQuery - Items read/query",
                "NumberOfItems32",
                "Number of items read (deserialized) in index per ContainsIndexQuery"
            },

            {
                "PagedIndexQuery - Items read/query",
                "NumberOfItems32",
                "Number of items read (deserialized) in index per PagedIndexQuery"
            },

            {
                "FirstLastQuery - Items read/query",
                "NumberOfItems32",
                "Number of items read (deserialized) in index per FirstLastQuery"
            },

            {
                "GetRangeQuery - Items read/query",
                "NumberOfItems32",
                "Number of items read (deserialized) in index per GetRangeQuery"
            },

            {
                "RandomQuery - Items read/query",
                "NumberOfItems32",
                "Number of items read (deserialized) in index per RandomQuery"
            },

            {
                "IntersectionQuery - Items read/query",
                "NumberOfItems32",
                "Number of items read (deserialized) in index per IntersectionQuery"
            },

            // filter delete related counters
            {  
                "Filter delete - request/sec",
                "RateOfCountsPerSecond64",
                "Number of the filter delete requests per second"
            }, 

            {
                "Filter delete - Items filtered Per request",
                "NumberOfItems32",
                "Average Number of items per Filter delete request"
            },

            {
                "Filter delete - Items in index Per request",
                "NumberOfItems32",
                "Average Items in index per Filter delete request"
            },

            {
                "Filter delete - Items read Per request",
                "NumberOfItems32",
                "Average Items read in index per Filter delete request"
            },

            //Span Query Counters
            {
                "SpanQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the SpanQuery requests per second"
            },

            {
                "SpanQuery - Indexes lookup/query",
                "NumberOfItems32",
                "Number of the IndexLookup per SpanQuery"
            },

            {
                "SpanQuery - Items in index/query",
                "NumberOfItems32",
                "Number of items in index per SpanQuery"
            },

            {
                "SpanQuery - Items read/query",
                "NumberOfItems32",
                "Number of items read (deserialized) in index per SpanQuery"
            },

            {
                "RemoteClusteredPagedIndexQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the RemoteClusteredPagedIndexQuery requests per second"
            },

            {
                "RemoteClusteredPagedIndexQuery - Indexes lookup/query",
                "NumberOfItems32",
                "Number of the IndexLookup per RemoteClusteredPagedIndexQuery"
            },

            {
                "RemoteClusteredSpanQuery - q/sec",
                "RateOfCountsPerSecond64",
                "Number of the RemoteClusteredSpanQuery requests per second"
            },

            {
                "RemoteClusteredSpanQuery - Indexes lookup/query",
                "NumberOfItems32",
                "Number of the IndexLookup per RemoteClusteredSpanQuery"
            }

            // To add more, start from here                           
        };
    }
}
