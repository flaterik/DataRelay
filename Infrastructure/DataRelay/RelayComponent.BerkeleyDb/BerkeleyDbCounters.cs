using System;
using System.Collections.Generic;
using System.Diagnostics;
using MySpace.Logging;
using MySpace.DataRelay.Performance;

namespace MySpace.DataRelay.RelayComponent.BerkeleyDb
{
    public class BdbInstanceInfo
    {
        public PerformanceCounter[] counters;

        public MinuteAggregateCounter getHitCounter;
        public MinuteAggregateCounter getAttemptCounter;
        public MinuteAggregateCounter saveHitCounter;
        public MinuteAggregateCounter saveAttemptCounter;
    }

	public class BerkeleyDbCounters
	{
        private static readonly LogWrapper Log = new LogWrapper();

		public static BerkeleyDbCounters instance = new BerkeleyDbCounters();
        
        System.Timers.Timer timer;

		#region Counter Definitions

		public static readonly string PerformanceCategoryName = "MySpace BerkeleyDb";

        protected volatile Dictionary<string/*instance name*/, BdbInstanceInfo> Instances = new Dictionary<string, BdbInstanceInfo>();
        
        public enum PerformanceCounterIndexes
        {
            Save = 0,
			Get = 1,
			Delete = 2,
			DeleteInAllTypes = 3,
			DeleteAllInType = 4,
			DeleteAll = 5,
            GetHitRatio = 6,
            GetHitRatioBase = 7,
            AvgSaveBytes = 8,
            AvgSaveBytesBase = 9,

            AvgGetBytes = 10,
            AvgGetBytesBase = 11,
            AvgBytes = 12,
			AvgBytesBase = 13,
            ObjectsPerSecond = 14,
			ObjectsStored = 15,
			TotalObjects = 16,
            AvgThrottledQueueCount = 17,
            MaxThrottledQueueCount = 18,
            PagesTrickled = 19,

            DeletedObjects = 20,
            PooledBufferSize = 21,
            AllocatedBuffers = 22,
            BuffersInUse = 23,

            // race condition counters
            RaceDeletes = 24,
            SaveHitRatio = 25,
            SaveHitRatioBase = 26,

            // bdb lock counters
            LockStatLastLockerId = 27,                          //The last allocated locker ID. 
            LockStatCurrentMaxLockerId = 28,                    //The current maximum unused locker ID. 
            LockStatNumberLockModes = 29,                       //The number of lock modes. 
            LockStatMaxLocksPossible = 30,                      //The maximum number of locks possible. 
            LockStatMaxLockersPossible = 31,                    //The maximum number of lockers possible. 
            LockStatMaxLockObjectsPossible = 32,                //The maximum number of lock objects possible. 
            LockStatNumberCurrentLocks = 33,                    //The number of current locks. 
            LockStatMaxNumberLocksAtOneTime = 34,               //The maximum number of locks at any one time. 
            LockStatNumberCurrentLockers = 35,                  //The number of current lockers. 
            LockStatMaxNumberLockersAtOneTime = 36,             //The maximum number of lockers at any one time. 
            LockStatNumberCurrentLockObjects = 37,              //The number of current lock objects. 
            LockStatNumberCurrentLockObjectsAtOneTime = 38,     //The maximum number of lock objects at any one time. 
            LockStatNumberLocksRequested = 39,                  //The total number of locks requested. 
            LockStatNumberLocksReleased = 40,                   //The total number of locks released. 
            LockStatNumberLocksUpgraded = 41,                   //The total number of locks upgraded. 
            LockStatNumberLocksDownGraded = 42,                 //The total number of locks downgraded. 
            LockStatLockWait = 43,                              //The number of lock requests not immediately available due to conflicts, for which the thread of control waited. 
            LockStatLockNoWait = 44,                            //The number of lock requests not immediately available due to conflicts, for which the thread of control did not wait. 
            LockStatNumberDeadLocks = 45,                       //The number of deadlocks.         
            LockStatLockTimeout = 46,                           //Lock timeout value.         
            LockStatNumberLockTimeouts = 47,                    //The number of lock requests that have timed out.     
            LockStatTxnTimeout = 48,                            //Transaction timeout value. 
            LockStatNumberTxnTimeouts = 49,                     //The number of transactions that have timed out. This value is also a component of st_ndeadlocks, the total number of deadlocks detected. 
            LockStatObjectsWait = 50,                           //The number of requests to allocate or deallocate an object for which the thread of control waited. 
            LockStatObjectsNoWait = 51,                         //The number of requests to allocate or deallocate an object for which the thread of control did not wait. 
            LockStatLockersWait = 52,                           //The number of requests to allocate or deallocate a locker for which the thread of control waited. 
            LockStatLockersNoWait = 53,                         //The number of requests to allocate or deallocate a locker for which the thread of control did not wait. 
            LockStatLocksWait = 54,                             //The number of requests to allocate or deallocate a lock structure for which the thread of control waited. 
            LockStatLocksNoWait = 55,                           //The number of requests to allocate or deallocate a lock structure for which the thread of control did not wait. 
            LockStatLockHashLen = 56,                           //Maximum length of a lock hash bucket. 
            LockStatLockRegionSize = 57,                        //The size of the lock region, in bytes. 
            LockStatRegionWait = 58,                            //The number of times that a thread of control was forced to wait before obtaining the lock region mutex. 
            LockStatRegionNoWait = 59,                          //The number of times that a thread of control was able to obtain the lock region mutex without waiting. 
        }

		public static readonly string[] PerformanceCounterNames = { 			
			@"Obj/Sec - Save", 
			@"Obj/Sec - Get", 			
			@"Obj/Sec - Delete",			
			@"Delete In All Types", 
			@"Delete All In Type",
			@"Delete All",
			"Get Hit Ratio",
			"Get Hit Ratio Base",
			"Avg Save Bytes",
			"Avg Save Bytes Base",

			"Avg Get Bytes",
			"Avg Get Bytes Base",
			"Avg Get/Save Bytes",
			"Avg Get/Save Bytes Base",
            "Obj/Sec",
			"Total Objects Stored",
			"Total Objects Processed",
            "Avg Throttled Queue Count",
            "Max Throttled Queue Count",
            "Pages Trickled",

            "Deleted Objects",
            "Pool of Buffers: Size",
            "Pool of Buffers: Allocated",
            "Pool of Buffers: In Use",

            // race condition counters
            @"Obj/Sec - RaceDeletes",			
			"Save Hit Ratio",
			"Save Hit Ratio Base",

            // bdb lock counters
            "LockStat-Locker ID - last allocated",
            "LockStat-Locker ID - Current maximum unused", 
            "LockStat-Number of lock modes", 
            "LockStat-Maximum number of locks possible",
            "LockStat-Maximum number of lockers possible", 
            "LockStat-Maximum number of lock objects possible", 
            "LockStat-Number of current locks", 
            "LockStat-Maximum number of locks at any one time", 
            "LockStat-Number of current lockers", 
            "LockStat-Maximum number of lockers at any one time",
            "LockStat-Number of current lock objects",
            "LockStat-Maximum number of lock objects at any one time",
            "LockStat-Total number of locks requested",
            "LockStat-Total number of locks released", 
            "LockStat-Total number of locks upgraded", 
            "LockStat-Total number of locks downgraded",
            "LockStat-Number of lock requests waiting",
            "LockStat-Number of lock requests not waiting",
            "LockStat-Number of deadlocks",
            "LockStat-Timeout value",
            "LockStat-Number of lock requests that have timed out",
            "LockStat-Transaction timeout value",
            "LockStat-Number of transactions that have timed out",
            "LockStat-Object Allocate/Deallocate requests thread waitng",
            "LockStat-Object Allocate/Deallocate requests thread NOT waitng",
            "LockStat-Locker Allocate/Deallocate requests thread waitng",
            "LockStat-Locker Allocate/Deallocate requests thread NOT waitng",
            "LockStat-Lock structure Allocate/Deallocate requests thread waitng",
            "LockStat-Lock structure Allocate/Deallocate requests thread NOT waitng",
            "LockStat-Lock hash bucket max length", 
            "LockStat-Size of the lock region bytes",
            "LockStat-Lock region mutex - wait count", 
            "LockStat-Lock region mutex - not waintng count" 
		};

		public static readonly string[] PerformanceCounterHelp = { 
			"Save Objects Per Second", 
			"Get Objects Per Second", 
			"Delete Objects Per Second", 
			"Delete In All Types Objects", 
			"Delete All In Type Objects",
			"Delete All Objects", 
			"Ratio of successful to unsuccessful Get Objects over the last minute",
			"Base for Get Hit Ratio",
			"Average number of bytes per Save objects",
			"Average Save Bytes Base",

			"Average number of bytes per Get objects",
			"Average Get Bytes Base",
			"Average number of bytes per Get/Save objects",
			"Average Get/Save Bytes Base",
			"Total Objects Per Second",
			"Total Objects Stored",
			"Total Objects Processed",
            "Avg BDB Throttled Queue Count",
            "Max BDB Throttled Queue Count",
            "Pages Trickled",

            "Deleted Objects",
            "Pool of Buffers: Size",
            "Pool of Buffers: Allocated",
            "Pool of Buffers: In Use",
            
            // race condition counters
            "Race Delete Objects Per Second", 
			"Ratio of successful to unsuccessful Save Objects over the last minute",
			"Base for Save Hit Ratio",

            // bdb lock counters
            "The last allocated locker ID",
            "The current maximum unused locker ID",
            "The number of lock modes",
            "The maximum number of locks possible",
            "The maximum number of lockers possible", 
            "The maximum number of lock objects possible",
            "The number of current locks",
            "The maximum number of locks at any one time",
            "The number of current lockers",
            "The maximum number of lockers at any one time",
            "The number of current lock objects",
            "The maximum number of lock objects at any one time",
            "The total number of locks requested",
            "The total number of locks released",
            "The total number of locks upgraded",
            "The total number of locks downgraded",
            "The number of lock requests not immediately available due to conflicts, for which the thread of control waited",
            "The number of lock requests not immediately available due to conflicts, for which the thread of control did not wait",
            "The number of deadlocks",
            "Lock timeout value",
            "The number of lock requests that have timed out",
            "Transaction timeout value",
            "The number of transactions that have timed out. This value is also a component of st_ndeadlocks, the total number of deadlocks detected",
            "The number of requests to allocate or deallocate an object for which the thread of control waited",
            "The number of requests to allocate or deallocate an object for which the thread of control did not wait",
            "The number of requests to allocate or deallocate a locker for which the thread of control waited",
            "The number of requests to allocate or deallocate a locker for which the thread of control did not wait",
            "The number of requests to allocate or deallocate a lock structure for which the thread of control waited",
            "The number of requests to allocate or deallocate a lock structure for which the thread of control did not wait",
            "Maximum length of a lock hash bucket",
            "The size of the lock region, in bytes",
            "The number of times that a thread of control was forced to wait before obtaining the lock region mutex",
            "The number of times that a thread of control was able to obtain the lock region mutex without waiting"
		};

		public static readonly PerformanceCounterType[] PerformanceCounterTypes = { 			
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.NumberOfItems64, 
			PerformanceCounterType.NumberOfItems64, 
			PerformanceCounterType.NumberOfItems64, 
			PerformanceCounterType.RawFraction,
			PerformanceCounterType.RawBase,
			PerformanceCounterType.AverageCount64,
			PerformanceCounterType.AverageBase,

			PerformanceCounterType.AverageCount64,
			PerformanceCounterType.AverageBase,
			PerformanceCounterType.AverageCount64,
			PerformanceCounterType.AverageBase,
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.NumberOfItems64,
			PerformanceCounterType.NumberOfItems64,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems64, 

            PerformanceCounterType.NumberOfItems64, 
            PerformanceCounterType.NumberOfItems64, 
            PerformanceCounterType.NumberOfItems64, 
            PerformanceCounterType.NumberOfItems64, 

            // race condition counters
			PerformanceCounterType.NumberOfItems64, 
			PerformanceCounterType.RawFraction,
			PerformanceCounterType.RawBase,

            // bdb lock counters
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32,
            PerformanceCounterType.NumberOfItems32
		};

		#endregion

        #region Constructors
        private BerkeleyDbCounters()
		{
		}
		

		public static BerkeleyDbCounters Instance
		{
			get
			{				
				return instance;
			}
        }
        #endregion

        #region Private Methods
        private void CountHitRatio(string instanceName, int hits, int attempts, PerformanceCounterIndexes hitRatio, PerformanceCounterIndexes hitRatioBase)
        {
            if (attempts > 0)
            {
                BdbInstanceInfo bdbInstanceInfo;
                if (Instances.TryGetValue(instanceName, out bdbInstanceInfo))
                {
                        bdbInstanceInfo.counters[(int)hitRatio].RawValue = hits;
                        bdbInstanceInfo.counters[(int)hitRatioBase].RawValue = attempts;
                }
            }
        }

        private static void CreatePerformanceCounterCategory()
        {
            CounterInstaller.InstallCounters();
        }

        private bool TryGetCounter(string instanceName, PerformanceCounterIndexes counterName, out PerformanceCounter counter)
        {
            counter = null;
            BdbInstanceInfo bdbInstanceInfo;
            if (Instances.TryGetValue(instanceName, out bdbInstanceInfo))
            {
                counter = bdbInstanceInfo.counters[(int)counterName];
                return true;
            }
            return false;
        }
        
        private void RemoveCounters()
        {
            CounterInstaller.RemoveCounters();
        }

        private void StartTimer()
        {
            //To prevent exception when a timer is already initialized and started
            if (timer == null)
            {
                timer = new System.Timers.Timer {Interval = 1000, AutoReset = true};
                timer.Elapsed += timer_Elapsed;
                timer.Start();
            }

        }
        #endregion

        public void Initialize(string instanceName)
		{
            if (!PerformanceCounterCategory.Exists(PerformanceCategoryName))
            {
                CreatePerformanceCounterCategory();
            }
            BdbInstanceInfo bdbInstanceInfo;
            if (Instances.TryGetValue(instanceName, out bdbInstanceInfo))
            {
                Log.Info("Performance counters instance " + instanceName + " is already exists, instance will not be re-initialized.");
            }
            else
            {
                try
                {
                    bdbInstanceInfo = new BdbInstanceInfo();
                    int numCounters = PerformanceCounterNames.Length;
                    bdbInstanceInfo.counters = new PerformanceCounter[numCounters];
                    for (int i = 0; i < numCounters; i++)
                    {
                        bdbInstanceInfo.counters[i] = new PerformanceCounter(
                            PerformanceCategoryName,
                            PerformanceCounterNames[i],
                            instanceName,
                            false
                            );
                    }

                    bdbInstanceInfo.getHitCounter = new MinuteAggregateCounter();
                    bdbInstanceInfo.getAttemptCounter = new MinuteAggregateCounter();
                    bdbInstanceInfo.saveHitCounter = new MinuteAggregateCounter();
                    bdbInstanceInfo.saveAttemptCounter = new MinuteAggregateCounter();

                    Instances.Add(instanceName, bdbInstanceInfo);

                    ResetCounters(bdbInstanceInfo.counters);
                    StartTimer();
                }
                catch (Exception ex)
                {
                    if (Log.IsErrorEnabled)
                    {   
                        Log.ErrorFormat("BerkeleyDbCounters:Initialize() Exception creating Memory Store Counters: {0}. The counters might need to be reinstalled via InstallUtil."
                            , ex);                     
                    }
                }
                Log.DebugFormat("Performance counters instance {0} initialized.", instanceName);
            }
		}

        public void CountGet(string instanceName, bool success, int byteArrayLength)
        {
            BdbInstanceInfo bdbInstanceInfo;
            if (Instances.TryGetValue(instanceName, out bdbInstanceInfo))
            {
                bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.ObjectsPerSecond].Increment();
                bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.TotalObjects].Increment();
                bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.Get].Increment();

                if (byteArrayLength > 0)
                    bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.AvgGetBytes].IncrementBy(byteArrayLength);
                bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.AvgGetBytesBase].Increment();

                if (success)
                    bdbInstanceInfo.getHitCounter.IncrementCounter();
                bdbInstanceInfo.getAttemptCounter.IncrementCounter();
            }
        }

        public void CountSave(string instanceName, bool success, int byteArrayLength)
        {
            BdbInstanceInfo bdbInstanceInfo;
            if (Instances.TryGetValue(instanceName, out bdbInstanceInfo))
            {
                bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.ObjectsPerSecond].Increment();
                bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.TotalObjects].Increment();
                bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.Save].Increment();

                if (byteArrayLength > 0)
                    bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.AvgSaveBytes].IncrementBy(byteArrayLength);

                bdbInstanceInfo.counters[(int)PerformanceCounterIndexes.AvgSaveBytesBase].Increment();

                if (success)
                    bdbInstanceInfo.saveHitCounter.IncrementCounter();
                bdbInstanceInfo.saveAttemptCounter.IncrementCounter();
            }
        }

        public void ResetAllCounters()
        {
            foreach (KeyValuePair<string, BdbInstanceInfo> kvp in Instances)
            {
                ResetCounters(kvp.Value.counters);
            }
        }

        public void ResetCounters(PerformanceCounter[] perfCounter)
        {
            perfCounter[(int)PerformanceCounterIndexes.DeleteInAllTypes].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.DeleteAllInType].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.DeleteAll].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.GetHitRatio].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.GetHitRatioBase].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.AvgSaveBytes].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.AvgSaveBytesBase].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.AvgGetBytes].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.AvgGetBytesBase].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.AvgBytes].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.AvgBytesBase].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.ObjectsStored].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.TotalObjects].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.AvgThrottledQueueCount].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.MaxThrottledQueueCount].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.PagesTrickled].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.DeletedObjects].RawValue = 0;

            perfCounter[(int)PerformanceCounterIndexes.RaceDeletes].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.SaveHitRatio].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.SaveHitRatioBase].RawValue = 0;

            perfCounter[(int)PerformanceCounterIndexes.LockStatLastLockerId].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatCurrentMaxLockerId].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberLockModes].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatMaxLocksPossible].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatMaxLockersPossible].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatMaxLockObjectsPossible].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberCurrentLocks].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatMaxNumberLocksAtOneTime].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberCurrentLockers].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatMaxNumberLockersAtOneTime].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberCurrentLockObjects].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberCurrentLockObjectsAtOneTime].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberLocksRequested].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberLocksReleased].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberLocksUpgraded].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberLocksDownGraded].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLockWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLockNoWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberDeadLocks].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLockTimeout].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberLockTimeouts].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatTxnTimeout].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatNumberTxnTimeouts].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatObjectsWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatObjectsNoWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLockersWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLockersNoWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLocksWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLocksNoWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLockHashLen].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatLockRegionSize].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatRegionWait].RawValue = 0;
            perfCounter[(int)PerformanceCounterIndexes.LockStatRegionNoWait].RawValue = 0;
        }

		public void Shutdown()
		{
            foreach (KeyValuePair<string, BdbInstanceInfo> kvp in Instances)
            {
                foreach (PerformanceCounter counter in kvp.Value.counters)
                {
                    counter.Close();
                    counter.Dispose();
                }
            }
		}

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            foreach (KeyValuePair<string, BdbInstanceInfo> kvp in Instances)
            {
                CountHitRatio(kvp.Key, kvp.Value.getHitCounter.Tick(), kvp.Value.getAttemptCounter.Tick(), PerformanceCounterIndexes.GetHitRatio, PerformanceCounterIndexes.GetHitRatioBase);
                CountHitRatio(kvp.Key, kvp.Value.saveHitCounter.Tick(), kvp.Value.saveAttemptCounter.Tick(), PerformanceCounterIndexes.SaveHitRatio, PerformanceCounterIndexes.SaveHitRatioBase);
            }
        }

        public PerformanceCounter GetCounter(string instanceName, PerformanceCounterIndexes counterName)
        {
            PerformanceCounter counter;
            TryGetCounter(instanceName, counterName, out counter);
            return counter;
        }

        public void IncrementCounter(string instanceName, PerformanceCounterIndexes counterName)
        {
            PerformanceCounter counter = null;
            if (TryGetCounter(instanceName, counterName, out counter))
            {
                counter.Increment();
            }
        }

        public void IncrementCounter(string instanceName, PerformanceCounterIndexes counterName, int value)
        {
            PerformanceCounter counter = null;
            if (TryGetCounter(instanceName, counterName, out counter))
            {
                counter.IncrementBy(value);
            }
        }

        public void SetCounter(string instanceName, PerformanceCounterIndexes counterName, int value)
        {
            PerformanceCounter counter = null;
            if (TryGetCounter(instanceName, counterName, out counter))
            {
                counter.RawValue = value;
            }
        }
        public void SetCounter(string instanceName, PerformanceCounterIndexes counterName, long value)
        {
            PerformanceCounter counter;
            if (TryGetCounter(instanceName, counterName, out counter))
            {
                counter.RawValue = value;
            }
        }
	}
}
