using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using MySpace.Common;
using Wintellect.PowerCollections;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters
{
    /// <summary>
    /// This class contains Performance counters for index cache v3
    /// </summary>
    public class PerformanceCounters
    {
        /// <summary>
        /// performance counter instance
        /// </summary>
        private static readonly PerformanceCounters instance = new PerformanceCounters();

        /// <summary>
        /// CounterTable is a 2 dimension array of SafeCounters, 
        /// dimension 0 is counter names (row),
        /// dimension 1 is the type id (column, counter instance)
        /// </summary>
        private volatile SafeCounter[,] counterTable;

        /// <summary>
        /// the mapping between the type id and the index for that type in the counterTable
        /// </summary>
        short[] typeIdIndexMappingArray;

        /// <summary>
        /// The number of index type ids configured in the RelayComponent.config
        /// </summary>
        private int numberOfTypeIds;

        /// <summary>
        /// The counter caterogy string
        /// </summary>
        private string perfCounterCategoryNameString = PerformanceCounterConstant.CategoryNameBase;

        /// <summary>
        /// Private constructor
        /// </summary>
        private PerformanceCounters() {}

        /// <summary>
        /// Gets PerformanceCounters instance
        /// </summary>
        public static PerformanceCounters Instance
        {
            get { return instance; }
        }

        /// <summary>
        /// Update multiple counters 
        /// </summary>
        /// <param name="counterValues">list of performance counter enums and increment values</param>
        /// <param name="typeId">type id</param>
        public void IncrementMultipleCounters(IEnumerable<Pair<PerformanceCounterEnum, int>> counterValues, short typeId)
        {
            foreach(KeyValuePair<PerformanceCounterEnum, int> valuePair in counterValues)
            {
                IncrementCounter(valuePair.Key, typeId, valuePair.Value);
            }
        }

        /// <summary>
        /// Set values to the counters in the counterValues collection
        /// </summary>
        /// <param name="counterValues">collection of the counters and their values</param>
        /// <param name="typeId">type id</param>
        public void SetMultipleCounters(IEnumerable<Pair<PerformanceCounterEnum, int>> counterValues, short typeId)
        {
            foreach (KeyValuePair<PerformanceCounterEnum, int> valuePair in counterValues)
            {
                SetCounterValue(valuePair.Key, typeId, valuePair.Value);
            }
        }

        /// <summary>
        /// This will set the perf counter to a cretain value
        /// </summary>
        /// <param name="counterItem">counter item</param>
        /// <param name="typeId">type id</param>
        /// <param name="counterValue">counter value</param>
        public void SetCounterValue(PerformanceCounterEnum counterItem, short typeId, int counterValue)
        {
            int typeIdIndex = typeIdIndexMappingArray[typeId];
            
            if (counterTable != null && (counterTable[(int)counterItem, typeIdIndex] != null))
            {
                counterTable[(int)counterItem, typeIdIndex].Value = counterValue;
            }
            else
            {
                if (LoggingUtil.Log.IsWarnEnabled)
                {
                    LoggingUtil.Log.Warn("The Counter you want to set value is null");
                }
            }
        }

        /// <summary>
        /// Increment a perf counter and then increment the total counter as well
        /// </summary>
        /// <param name="counterItem">enum of the counter</param>
        /// <param name="typeId">type id of the relay message</param>
        /// <param name="incrementValue">the value to incremnt on the counter</param>
        public void IncrementCounter(PerformanceCounterEnum counterItem, short typeId, long incrementValue)
        {
            int typeIdIndex = typeIdIndexMappingArray[typeId];

            if (counterTable != null && (counterTable[(int)counterItem, typeIdIndex] != null))
            {
                counterTable[(int)counterItem, typeIdIndex].Increment(incrementValue);
            }
            else
            {
                if (LoggingUtil.Log.IsWarnEnabled)
                {
                    LoggingUtil.Log.Warn("The Counter you want to increment is null");
                }
            }

            // increment the total count
            IncrementTotalCounter(counterItem, incrementValue);
        }

        /// <summary>
        /// Increment the total instance of a specific counter
        /// </summary>
        /// <param name="counterItem">PerformanceCounterEnum to specify a counter</param>
        /// <param name="incrementValue">the value to incremnt on the counter</param>
        private void IncrementTotalCounter(PerformanceCounterEnum counterItem, long incrementValue)
        {
            if (counterTable != null && (counterTable[(int)counterItem, numberOfTypeIds] != null))
            {
                counterTable[(int)counterItem, numberOfTypeIds].Increment(incrementValue);
            }
            else
            {
                if (LoggingUtil.Log.IsWarnEnabled)
                {
                    LoggingUtil.Log.Warn("The Counter you want to increment is null");
                }
            }
        }

        /// <summary>
        /// Reset a specific counter value to 0
        /// </summary>
        /// <param name="counterItem">counter enum</param>
        /// <param name="typeId">type id</param>
        public void ResetCounter(PerformanceCounterEnum counterItem, short typeId)
        {
            int typeIdIndex = typeIdIndexMappingArray[typeId];

            if (counterTable != null && (counterTable[(int)counterItem, typeIdIndex] != null))
            {
                counterTable[(int)counterItem, typeIdIndex].Reset();
            }
            else
            {
                if (LoggingUtil.Log.IsWarnEnabled)
                {
                    LoggingUtil.Log.Warn("The Counter you want to reset is null");
                }
            }
        }

        /// <summary>
        /// Reset the total instance of a counter
        /// </summary>
        /// <param name="counterItem">PerformanceCounterEnum to specify a counter</param>
        public void ResetTotalCounter(PerformanceCounterEnum counterItem)
        {
            if (counterTable != null && (counterTable[(int)counterItem, numberOfTypeIds] != null))
            {
                counterTable[(int)counterItem, numberOfTypeIds].Reset();
            }
            else
            {
                if (LoggingUtil.Log.IsWarnEnabled)
                {
                    LoggingUtil.Log.Warn("The Counter you want to increment is null");
                }
            }
        }

        /// <summary>
        /// Dispose all the counter objects in the table
        /// </summary>
        public void DisposeCounters()
        {
            for (int i = 0; i < Enum.GetNames(typeof(PerformanceCounterEnum)).Length; i++)
            {
                for (int j = 0; j < numberOfTypeIds+1; j++)
                {
                    if (counterTable[i, j] != null)
                    {
                        counterTable[i, j].Dispose();
                    }
                }
            }

            // remove the counter category
            PerformanceCounterCategory.Delete(perfCounterCategoryNameString);
        }

        /// <summary>
        /// Only the counter that 
        /// </summary>
        /// <param name="counterType">counter type string</param>
        /// <returns>ture if a total counter is needed, false if not</returns>
        private static bool NeedTotalCounter(string counterType)
        {
            if (!string.IsNullOrEmpty(counterType))
            {
                if (counterType.Equals("RateOfCountsPerSecond64", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Initialize all the counters in the table
        /// </summary>
        /// <param name="listenPort">listenPort</param>
        /// <param name="typeIdList">type id list</param>
        /// <param name="maxTypeId">max type id</param>
        /// <param name="isInit">true for init and false for restart</param>
        public void InitializeCounters(int listenPort, List<short> typeIdList,  short maxTypeId, bool isInit)
        {
            // compose category string
            perfCounterCategoryNameString = 
                PerformanceCounterConstant.CategoryNameBase + "(Port " + listenPort + ")";

            if (isInit)
            {
                CreateCounterCategory(perfCounterCategoryNameString);
            }

            int tempNumberOfTypeIds = typeIdList.Count;

            // initialize all the counters
            if (PerformanceCounterCategory.Exists(perfCounterCategoryNameString))
            {
                // create type id and index mapping array
                short[] tempTypeIdIndexMappingArray = new short[maxTypeId +1];

                for (short i = 0; i < tempNumberOfTypeIds; i++)
                {
                    tempTypeIdIndexMappingArray[typeIdList[i]] = i;
                }
                
                // clean-up and initialize the counter table,  the first dimension is all the counter names
                // the second dimension is all the type ids plus a "total" instance 
                SafeCounter[,] tempCounterTable = new SafeCounter[PerformanceCounterConstant.CounterInfo.GetLength(0), tempNumberOfTypeIds + 1];

                // fill the counterTable
                for (int j=0; j<PerformanceCounterConstant.CounterInfo.GetLength(0); j++)
                {
                    for (int k = 0; k < tempNumberOfTypeIds + 1; k++)
                    {
                        string instanceName;

                        // *** total instance is the last entry in the row ***
                        if (k == tempNumberOfTypeIds)   
                        {
                            // need to check if the we have total counter in this case
                            // if the counter is of rate type, then we have a total counter
                            // if not, total counter will not make sense. 
                            if (!NeedTotalCounter(PerformanceCounterConstant.CounterInfo[j, 1]))
                            {
                                continue;
                            }

                            instanceName = "total";
                        }
                        else
                        {
                            instanceName = "type id " + typeIdList[(short) k];
                        }
                        
                        tempCounterTable[j, k] = new SafeCounter(
                            perfCounterCategoryNameString,
                            PerformanceCounterConstant.CounterInfo[j, 0],
                            instanceName,
                            false);

                        tempCounterTable[j,k].Reset();
                    }
                }

                Interlocked.Exchange(ref numberOfTypeIds, tempNumberOfTypeIds);
                Interlocked.Exchange(ref typeIdIndexMappingArray, tempTypeIdIndexMappingArray);

                counterTable = tempCounterTable;
            }
            else
            {
                if (LoggingUtil.Log.IsWarnEnabled)
                {
                    LoggingUtil.Log.Warn(string.Format("The performance counter category {0} does not exist", perfCounterCategoryNameString));
                }
            }
        }

        /// <summary>
        /// Create performance counter category
        /// </summary>
        /// <param name="categoryName">name of the counter category</param>
        private static void CreateCounterCategory(string categoryName)
        {
            if (PerformanceCounterCategory.Exists(categoryName))
            {
                PerformanceCounterCategory.Delete(categoryName);
            }

            CounterCreationDataCollection counterDataCollection = new CounterCreationDataCollection();

            for (int i = 0; i < PerformanceCounterConstant.CounterInfo.GetLength(0); i++)
            {
                // create the countercreationdata
                counterDataCollection.Add(new CounterCreationData(
                    PerformanceCounterConstant.CounterInfo[i, 0],
                    PerformanceCounterConstant.CounterInfo[i, 2],
                    (PerformanceCounterType)Enum.Parse(typeof(PerformanceCounterType), PerformanceCounterConstant.CounterInfo[i, 1])));
            }

            // create counter category
            PerformanceCounterCategory.Create(
                categoryName,
                "Performance counters for Index Cache Data Relay Component.",
                PerformanceCounterCategoryType.MultiInstance,
                counterDataCollection);

            // log info
            if (LoggingUtil.Log.IsInfoEnabled)
            {
                LoggingUtil.Log.Info(string.Format(CultureInfo.InvariantCulture, "Performance counter category {0} is created.", categoryName));
            }
        }
    }
}
