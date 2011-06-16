using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using MySpace.Logging;

namespace MySpace.BinaryStorage.Store.BerkeleyStore.PerfCounter
{
    public class BdbCounterInstance
    {
        internal PerformanceCounter[] counters;
    }

    /// <summary>
    /// This class contains Performance counters for index cache v3
    /// </summary>
    public class BerkeleyBinaryStorePerformanceCounters : IDisposable
    {
        /// <summary>
        /// performance counter instance
        /// </summary>
        private readonly static BerkeleyBinaryStorePerformanceCounters instance = new BerkeleyBinaryStorePerformanceCounters();

        /// <summary>
        /// LogWrapper
        /// </summary>
        private static readonly LogWrapper Log = new LogWrapper();

        /// <summary>
        /// the mapping between the port number and the index for that instance in the counterTable
        /// </summary>
        private string perfCounterInstanceName = "";

        private BdbCounterInstance CounterArray;

        /// <summary>
        /// The counter caterogy string
        /// </summary>
        private readonly string perfCounterCategoryNameString = BerkeleyBinaryStorePerformanceCounterConstant.CategoryName;

        /// <summary>
        /// Private constructor
        /// </summary>
        private BerkeleyBinaryStorePerformanceCounters() { }

        /// <summary>
        /// Gets PerformanceCounters instance
        /// </summary>
        public static BerkeleyBinaryStorePerformanceCounters Instance
        {
            get { return instance; }
        }

        public PerformanceCounter GetCounter(BerkeleyBinaryStorePerformanceCounterEnum counter)
        {
            return CounterArray.counters[(int)counter];
        }

        /// <summary>
        /// This will set the perf counter to a cretain value
        /// </summary>
        /// <param name="counterItem">counter item</param>
        /// <param name="counterValue">counter value</param>
        public void SetCounterValue(BerkeleyBinaryStorePerformanceCounterEnum counterItem, int counterValue)
        {
            if (this.CounterArray != null)
            {
                this.CounterArray.counters[(int)counterItem].RawValue = counterValue;
            }
            else
            {
                if (Log.IsWarnEnabled)
                {
                    Log.Warn(string.Format("The Counter instance {0} is null, can not be set", this.perfCounterInstanceName));
                }
            }
        }

        /// <summary>
        /// Increment a perf counter and then increment the total counter as well
        /// </summary>
        /// <param name="counterItem">enum of the counter</param>
        /// <param name="incrementValue">the value to incremnt on the counter</param>
        public void IncrementCounter(BerkeleyBinaryStorePerformanceCounterEnum counterItem, long incrementValue)
        {
            if (this.CounterArray != null)
            {
                this.CounterArray.counters[(int)counterItem].IncrementBy(incrementValue);
            }
            else
            {
                if (Log.IsWarnEnabled)
                {
                    Log.Warn(string.Format("The Counter instance {0} is null, can not be incremented", this.perfCounterInstanceName));
                }
            }
        }

        /// <summary>
        /// Reset a specific counter value to 0
        /// </summary>
        /// <param name="counterItem">counter enum</param>
        public void ResetCounter(BerkeleyBinaryStorePerformanceCounterEnum counterItem)
        {
            if (this.CounterArray != null)
            {
                this.CounterArray.counters[(int)counterItem].RawValue = 0;
            }
            else
            {
                if (Log.IsWarnEnabled)
                {
                    Log.Warn(string.Format("The Counter instance {0} is null, can not be reset", this.perfCounterInstanceName)); 
                }
            }
        }

        /// <summary>
        /// Dispose all the counter objects in the table
        /// </summary>
        public void Dispose()
        {
            if (CounterArray.counters != null)
                {
                    foreach (PerformanceCounter counter in CounterArray.counters)
                    {
                        counter.Dispose();
                    }
            }

            // remove the counter category
            PerformanceCounterCategory.Delete(perfCounterCategoryNameString);
        }

        /// <summary>
        /// Initialize all the counters
        /// </summary>
        /// <param name="instanceName">instance Name</param>
        public void InitializeCounters(string instanceName)
        {
            CreateCounterCategory(this.perfCounterCategoryNameString);
            
            // check if instance already there
            if ((this.perfCounterInstanceName == instanceName) && (this.CounterArray != null)) 
            {
                Log.Info("Performance counters instance " + instanceName + " is already exists, instance will not be re-initialized.");
            }
            else
            {
                BdbCounterInstance myInstance = new BdbCounterInstance();

                int numberOfCounter = BerkeleyBinaryStorePerformanceCounterConstant.CounterInfo.GetLength(0);
                myInstance.counters = new PerformanceCounter[numberOfCounter];

                for (int i = 0; i < numberOfCounter; i++)
                {
                    myInstance.counters[i] = new PerformanceCounter(
                        this.perfCounterCategoryNameString,
                        BerkeleyBinaryStorePerformanceCounterConstant.CounterInfo[i, 0],
                        instanceName,
                        false);

                    if (myInstance.counters[i] != null)
                    {
                        myInstance.counters[i].RawValue = 0;
                    }
                }

                Interlocked.Exchange(ref this.perfCounterInstanceName, instanceName);
                Interlocked.Exchange(ref this.CounterArray, myInstance);
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

            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                CounterCreationDataCollection counterDataCollection = new CounterCreationDataCollection();

                for (int i = 0; i < BerkeleyBinaryStorePerformanceCounterConstant.CounterInfo.GetLength(0); i++)
                {
                    // create the countercreationdata
                    counterDataCollection.Add(new CounterCreationData(
                                                  BerkeleyBinaryStorePerformanceCounterConstant.CounterInfo[i, 0],
                                                  BerkeleyBinaryStorePerformanceCounterConstant.CounterInfo[i, 2],
                                                  (PerformanceCounterType)
                                                  Enum.Parse(typeof (PerformanceCounterType),
                                                             BerkeleyBinaryStorePerformanceCounterConstant.CounterInfo[i, 1])));
                }

                // create counter category
                if (!PerformanceCounterCategory.Exists(categoryName))
                {
                    PerformanceCounterCategory.Create(
                        categoryName,
                        "Performance counters for BerkeleyDB BinaryStore Component.",
                        PerformanceCounterCategoryType.MultiInstance,
                        counterDataCollection);

                    // log info
                    if (Log.IsInfoEnabled)
                    {
                        Log.Info(string.Format(CultureInfo.InvariantCulture,
                                               "Performance counter category {0} is created.", categoryName));
                    }
                }
            }
        }
    }
}

