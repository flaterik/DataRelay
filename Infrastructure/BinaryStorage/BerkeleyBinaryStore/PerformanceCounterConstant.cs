namespace MySpace.BinaryStorage.Store.BerkeleyStore.PerfCounter
{
    public enum BerkeleyBinaryStorePerformanceCounterEnum
    {
        #region BDB perf counter

        // entry saved to bdb per sec
        BdbObjectsSavePerSec,

        // entry deleted from bdb per sec 
        BdbObjectDeletePerSec,

        // entry get from bdb per sec (total get includs get 
        // from partial get smart stream plus full get)
        BdbObjectsGetPerSec,

        // avg get bytes per second
        BdbGetBytesPerSec,

        // avg bytes per get 
        BdbAvgGetBytes,

        BdbAvgGetBytesBase,

        // avg bytes per save
        BdbAvgSaveBytes,

        BdbAvgSaveBytesBase,

        // hit ratio for get
        GetHitRatio,

        GetHitRatioBase,

        #endregion

        // number of partial get per sec
        PartialGetPerSec,

        // avg db get per partial get
        AvgDbGetPerPartialGet,

        AvgDbGetPerPartialGetBase,

        // avg bytes per partial get
        AvgBytesPerPartialGet,

        AvgBytesPerPartialGetBase,
    }

    public class BerkeleyBinaryStorePerformanceCounterConstant
    {
        public static string CategoryName = "MySpace BerkeleyDB BinaryStore";

        /// <summary>
        /// *** READ THIS BEFORE UPDATE THIS ENUM***
        /// This Counter info is a two dimension array, each array inside contains
        /// three pieces of information about a performance counter:
        /// 1. counter name string 
        /// 2. counter type string 
        /// 3. counter help string
        /// This array needs to be in sync with the 
        /// PerformanceCounterEnum enum above, the sequence should be the same
        /// </summary>
        public static readonly string[,] CounterInfo = new string[,]
                                                           {
                                                               // Berkeley DB counters
                                                               {
                                                                   "Object Save/sec",
                                                                   "RateOfCountsPerSecond32",
                                                                   "Objects saved per second"
                                                               },

                                                               {
                                                                   "Object Delete/sec",
                                                                   "RateOfCountsPerSecond32",
                                                                   "Objects deleteed per second"
                                                               },

                                                               {
                                                                   "Object Get/sec", 
                                                                   "RateOfCountsPerSecond32",
                                                                   "Objects get per second"
                                                               },

                                                               {
                                                                   "Get Bytes/sec", 
                                                                   "RateOfCountsPerSecond32",
                                                                   "Bytes get per second"
                                                               },

                                                               {
                                                                   "Avg Get Bytes",
                                                                   "AverageCount64",
                                                                   "Average bytes retrieved per get call"
                                                               },

                                                               {
                                                                   "Avg Get Bytes Base",
                                                                   "AverageBase",
                                                                   "Average bytes retrieved per get call base"
                                                               },

                                                               {
                                                                   "Avg Save Bytes",
                                                                   "AverageCount64",
                                                                   "Average bytes saved per save call"
                                                               },

                                                               {
                                                                   "Avg Save Bytes Base",
                                                                   "AverageBase",
                                                                   "Average bytes saved per save call Base"
                                                               },

                                                               {
                                                                   "Hit Ratio Get",
                                                                   "RawFraction",
                                                                   "hit ratio of get operation"
                                                               },

                                                               {
                                                                   "Hit Ratio Get Base",
                                                                   "RawBase",
                                                                   "hit ratio of get operation base"
                                                               },

                                                               // Partial | full get counters
                                                               {
                                                                   "Partial Get/sec",
                                                                   "RateOfCountsPerSecond32",
                                                                   "Number of partial get per second"
                                                               },

                                                               {
                                                                   "Avg # DB Access/Partial Get",
                                                                   "AverageCount64",
                                                                   "Avg # db access per partial get"
                                                               },

                                                               {
                                                                   "Avg # DB Access/Partial Get base",
                                                                   "AverageBase",
                                                                   "Avg # db access per partial get Base"
                                                               },

                                                               {
                                                                   "Avg # Get Bytes/Partial Get",
                                                                   "AverageCount64",
                                                                   "Avg # Get bytes per partial get"
                                                               },

                                                               {
                                                                   "Avg # Get Bytes/Partial Get base",
                                                                   "AverageBase",
                                                                   "Avg # Get bytes per partial get base"
                                                               },

                                                               // To add more, start from here
                                                           };
    }
}
