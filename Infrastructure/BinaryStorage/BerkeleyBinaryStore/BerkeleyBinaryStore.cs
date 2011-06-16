using System;
using System.IO;
using MySpace.BerkeleyDb.Configuration;
using MySpace.BerkeleyDb.Facade;
using MySpace.BinaryStorage.Store.BerkeleyStore.PerfCounter;
using MySpace.Common.Storage;
using MySpace.Storage;

namespace MySpace.BinaryStorage.Store
{
    /// <summary>
    /// This class is a Berkeley DB implementation of the IBinaryStorage,
    /// internally, it has a Berkeley DB facade instance
    /// </summary>
    public class BerkeleyBinaryStore : IBinaryStorage
    {
        #region Member Fields

        private const string InstanceName = "BerkeleyBinaryStore";

        private BerkeleyDbStorage store = new BerkeleyDbStorage();

        private string perfCounterInstanceName;

        #endregion

        #region IBinaryStorage Members

        public bool SupportsIncompleteReads
        {
            get
            {
                return true;
            }
        }

        public bool StreamsData
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Clear all the objects in the key space
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <returns>count of how many objects deleted</returns>
        public int Clear(DataBuffer keySpace)
        {
            return this.store.DeleteAllInType((short)keySpace.Int32Value);
        }

        /// <summary>
        /// Delete specific entry in the db
        /// </summary>
        /// <param name="keySpace">contains type id</param>
        /// <param name="key">key</param>
        /// <returns>whether delete is successful or not</returns>
        public bool Delete(DataBuffer keySpace, StorageKey key)
        {
            if (this.store.DeleteEntry((short)keySpace.Int32Value, key.PartitionId, key.Key))
            {
                BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                    BerkeleyBinaryStorePerformanceCounterEnum.BdbObjectDeletePerSec,
                    1);

                return true;
            }

            return false;
        }

        public event EventHandler<BinaryEventArgs> Dropped;

        /// <summary>
        /// Check whether object exists
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <param name="key">object key</param>
        /// <returns>whether object exists</returns>
        public bool Exists(DataBuffer keySpace, StorageKey key)
        {
            return this.store.EntryExists((short) keySpace.Int32Value, key.PartitionId, key.Key);
        }

        public Stream Get(DataBuffer keySpace, StorageKey key)
        {
            throw new NotSupportedException();
        }

        public Stream Get(DataBuffer keySpace, StorageKey key, int offset, int length)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Partial get with an offset
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <param name="key">key</param>
        /// <param name="offset">offset</param>
        /// <param name="output">output buffer</param>
        /// <returns>bytes get</returns>
        public int Get(DataBuffer keySpace, StorageKey key, int offset, DataBuffer output)
        {
            int getBytes = this.store.GetEntry((short)keySpace.Int32Value, key.PartitionId, key.Key, output, GetOptions.Partial(offset));

            IncrementGetPerfCounter(getBytes);

            return getBytes;
        }

        /// <summary>
        /// Normal get
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <param name="key">key</param>
        /// <param name="output">output buffer</param>
        /// <returns>bytes get</returns>
        public int Get(DataBuffer keySpace, StorageKey key, DataBuffer output)
        {
            int getBytes = this.store.GetEntry((short)keySpace.Int32Value, key.PartitionId, key.Key, output, GetOptions.Default);

            IncrementGetPerfCounter(getBytes);

            return getBytes;
        }

        /// <summary>
        /// Normal Get buffer
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <param name="key">key</param>
        /// <returns>bytes buffer</returns>
        public byte[] GetBuffer(DataBuffer keySpace, StorageKey key)
        {
            byte[] returnBuffer = this.store.GetEntryBuffer((short) keySpace.Int32Value, key.PartitionId, key.Key);

            int length = returnBuffer == null ? 0 : returnBuffer.Length;

            IncrementGetPerfCounter(length);

            return returnBuffer;
        }

        /// <summary>
        /// Partial Get buffer
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <param name="key">key</param>
        /// <param name="offset">start offset</param>
        /// <param name="length">copy length</param>
        /// <returns>bytes buffer</returns>
        public byte[] GetBuffer(DataBuffer keySpace, StorageKey key, int offset, int length)
        {
            byte[] returnBuffer = this.store.GetEntryBuffer((short) keySpace.Int32Value, key.PartitionId, key.Key, GetOptions.Partial(offset, length));

            int bufferLength = returnBuffer == null ? 0 : returnBuffer.Length;

            IncrementGetPerfCounter(bufferLength);

            return returnBuffer;
        }

        public bool GetAllowsMultiple(DataBuffer keySpace)
        {
            // Todo: waiting on IBinaryCursor checkin
            throw new NotImplementedException();
        }

        public IBinaryCursor GetCursor(DataBuffer keySpace, StorageKey startKey)
        {
            // Todo: waiting on IBinaryCursor checkin
            throw new NotImplementedException();
        }

        public IBinaryCursor GetCursor(DataBuffer keySpace)
        {
            // Todo: waiting on IBinaryCursor checkin
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the length of the object
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <param name="key">key</param>
        /// <returns>object length</returns>
        public int GetLength(DataBuffer keySpace, StorageKey key)
        {
            return this.store.GetEntryLength((short) keySpace.Int32Value, key.PartitionId, key.Key);
        }

        /// <summary>
        /// Partial Save
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <param name="key">key</param>
        /// <param name="offset">start offset</param>
        /// <param name="length">length to save</param>
        /// <param name="input">input buffer</param>
        /// <returns>bytes saved</returns>
        public int Put(DataBuffer keySpace, StorageKey key, int offset, int length, DataBuffer input)
        {
            if (store.SaveEntry((short)keySpace.Int32Value, key.PartitionId, key.Key, input, PutOptions.Partial(offset, length)))
            {
                IncrementSavePerfCounter(length);

                return length;
            }

            return 0;
        }

        /// <summary>
        /// Normal save
        /// </summary>
        /// <param name="keySpace">type id</param>
        /// <param name="key">key</param>
        /// <param name="input">input buffer</param>
        /// <returns>bytes saved</returns>
        public int Put(DataBuffer keySpace, StorageKey key, DataBuffer input)
        {
            if (store.SaveEntry((short) keySpace.Int32Value, key.PartitionId, key.Key, input, PutOptions.Default))
            {
                IncrementSavePerfCounter(input.Length);

                return input.Length;
            }

            return 0;
        }

        #endregion

        #region internal help function

        /// <summary>
        /// Increment Get performance counters
        /// </summary>
        /// <param name="length">bytes get</param>
        private static void IncrementGetPerfCounter(int length)
        {
            BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                BerkeleyBinaryStorePerformanceCounterEnum.BdbObjectsGetPerSec,
                1);

            BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                BerkeleyBinaryStorePerformanceCounterEnum.BdbAvgGetBytesBase,
                1);

            BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                BerkeleyBinaryStorePerformanceCounterEnum.GetHitRatioBase,
                1);

            if (length > 0)
            {
                BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                BerkeleyBinaryStorePerformanceCounterEnum.BdbAvgGetBytes,
                length);

                BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                BerkeleyBinaryStorePerformanceCounterEnum.GetHitRatio,
                1);

                BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                BerkeleyBinaryStorePerformanceCounterEnum.BdbGetBytesPerSec,
                length);
            }
        }

        /// <summary>
        /// Increment Save performance counters
        /// </summary>
        /// <param name="length">save bytes length</param>
        private static void IncrementSavePerfCounter(int length)
        {
            BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                    BerkeleyBinaryStorePerformanceCounterEnum.BdbObjectsSavePerSec,
                    1);

            BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                BerkeleyBinaryStorePerformanceCounterEnum.BdbAvgSaveBytes,
               length);

            BerkeleyBinaryStorePerformanceCounters.Instance.IncrementCounter(
                BerkeleyBinaryStorePerformanceCounterEnum.BdbAvgSaveBytesBase,
                1);
        }

        #endregion

        #region IStorage Members

        public TransactionCommitType CommitType
        {
            get { throw new NotImplementedException(); }
        }

        public ExecutionScope ExecutionScope
        {
            get { throw new NotImplementedException(); }
        }

        public bool GetKeySpacePartitionSupport(DataBuffer keySpace)
        {
            return true;
        }

        public OutOfSpacePolicy OutOfSpacePolicy
        {
            get { throw new NotImplementedException(); }
        }

        public bool SupportsKeySpacePartitions
        {
            get { return true; }
        }

        public bool SupportsKeySpaces
        {
            get { return true; }
        }

        public TransactionSupport TransactionSupport
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Initialize Store
        /// </summary>
        /// <param name="config">BerkeleyDb Config</param>
        public void Initialize(object config)
        {
            BerkeleyDbConfig bdbConfig = (BerkeleyDbConfig)config;

            store.Initialize(InstanceName, bdbConfig);

            string perfInstanceName = RetrievePerfCounterInstanceName(bdbConfig.EnvironmentConfig.HomeDirectory);

            this.perfCounterInstanceName = perfInstanceName;

            BerkeleyBinaryStorePerformanceCounters.Instance.InitializeCounters(this.perfCounterInstanceName);
        }

        /// <summary>
        /// Help function to get perf counter instance name from bdb home path,
        /// the instance name is taken from the db path 
        /// </summary>
        /// <param name="homePath">path of the bdb path on disk</param>
        /// <returns>the instance name</returns>
        private static string RetrievePerfCounterInstanceName(string homePath)
        {
            string[] splitResult = homePath.Split(new char[]{'/', '\\'});

            return splitResult[splitResult.Length - 1];
        }

        /// <summary>
        /// Re-initialize
        /// </summary>
        /// <param name="config">BerkeleyDb Config</param>
        public void Reinitialize(object config)
        {
            store.ReloadConfig((BerkeleyDbConfig)config);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            store.Shutdown();
        }

        #endregion
    }
}
