using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Timers;

using BerkeleyDb;
using Wintellect.Threading.ResourceLocks;

using MySpace.Common;
using MySpace.Common.IO;
using MySpace.Common.CompactSerialization.Formatters;
using MySpace.ResourcePool;

namespace MySpace.DataRelay.RelayComponent.Bdb
{
	public class BerkeleyDbStorage: MarshalByRefObject
	{
		public override object InitializeLifetimeService()
		{
			return null;
		}

        private readonly string logPrefix = "BDB";
        private readonly short allTypes = 0;
        private readonly short adminDbKey = -1;
        private readonly string shutdownTimeKey = "ShutdownTime";

        private BerkeleyDbConfig bdbConfig;
        private EnvironmentConfig envConfig;
        //private DbHash adminDb;
        private IDictionary<short, DbHash> databases;
        private IList<short> badStates;
        private IDictionary<DbRetVal, int> errorCounts;
        private Env env = null;
        private ResourceLock envLock;
        private ResourceLock stateLock;
        private ResourceLock errLock;
        private IDictionary<ulong, ResourceLock> entryLocks;
        private MemoryStreamPool memoryPoolStream;
        private int bufferSize = 1048;
        private short minTypeId = -1;
        private short maxTypeId = 50;
		private string instanceName;

        public BerkeleyDbStorage()
		{
            databases = new Dictionary<short, DbHash>();
            badStates = new List<short>();
            errorCounts = new Dictionary<DbRetVal, int>();
            envLock = new OneManyResourceLock();
            stateLock = new OneManyResourceLock();
            errLock = new OneManyResourceLock();
            entryLocks = new Dictionary<ulong, ResourceLock>();
            memoryPoolStream = new MemoryStreamPool(this.bufferSize);
		}

		#region Private Methods
        private bool AddRecord(DbHash db, Txn txn, byte[] keyData, byte[] valueData, DbFile.WriteFlags flags)
        {
            // use standard .NET serialization, with the binary formatter
            //keyStream.Position = 0;
            //formatter.Serialize(keyStream, value.Name);
            //DbEntry key = DbEntry.InOut(keyStream.GetBuffer(), 0, (int)keyStream.Position);
            DbEntry key = DbEntry.InOut(keyData);
            //dataStream.Position = 0;
            //formatter.Serialize(dataStream, value);
            //DbEntry data = DbEntry.InOut(dataStream.GetBuffer(), 0, (int)dataStream.Position);
            DbEntry data = DbEntry.InOut(valueData);
            // calling PutNew means we don't want to overwrite an existing record
            //WriteStatus status = btree.PutNew(txn, ref key, ref data, DbFile.WriteFlags.None);
            try
            {
                WriteStatus status = db.Put(txn, ref key, ref data, flags);
                // if we tried to insert a duplicate, let's report it
                switch (status)
                {
                    case WriteStatus.KeyExist:
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:AddRecord() Duplicate record"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        break;
                    case WriteStatus.NotFound:
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:AddRecord() record not found"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        break;
                    case WriteStatus.Success:
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:AddRecord() record is added"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        break;
                    default:
                        if (Log.IsErrorEnabled)
                        {
                            Log.Write(Log.Status.Error, "[{0}] {1}: BerkeleyDbStorage:AddRecord() unknown Write Status"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        break;
                }
                return status == WriteStatus.Success;
            }
            catch (BdbException ex)
            {
                HandleBdbError(ex, db);
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:AddRecord() Error Adding record"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                throw;
            }
            return false;
        }

        private bool CanProcessMessage(short typeId)
        {
            using (stateLock.WaitToRead())
            {
                if (badStates.Contains(allTypes) || badStates.Contains(typeId))
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:GetObject() Ignores message due to database bad state (TypeId={2})"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, typeId);
                    }
                    return false;
                }
            }
            return true;
        }

        private void CloseAllDatabases()
        {
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CloseAllDatabases() Closing All databases ..."
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
            foreach (short key in databases.Keys)
            {
                DbHash dbHash = databases[key];
                if (dbHash != null)
                {
                    Db db = dbHash.GetDb();
                    if (db != null)
                    {
                        db.Close();
                        db.Dispose();
                    }
                }
            }
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CloseAllDatabases() Closing All databases complete"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
        }

        private DbHash CreateDatabase(ref Env environment, DatabaseConfig dbConfig)
        {
            Db db = null;
            DbHash dbHash = null;
            string fileName = dbConfig.FileName;
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() creates new database for the file {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, fileName);
            }
            // create, configure and open database under a transaction
            //txn = env.TxnBegin(null, Txn.BeginFlags.None);
            try
            {
                if (environment != null && !environment.IsDisposed)
                {
                    db = environment.CreateDatabase(dbConfig.CreateFlags);
                }
                else
                {
                    db = new Db(dbConfig.CreateFlags);
                    Env dbEnv = db.GetEnv();
                    string dir = this.bdbConfig.EnvironmentConfig.HomeDirectory;
                    CreateDirectory(dir);
                    dbEnv.SetDataDir(dir);
                }
                if (Log.IsDebugEnabled)
                {
                    Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() DB handle is created."
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                }
                SetDatabaseConfiguration(db, dbConfig);
                //DbBTree dbTree = (DbBTree)db.Open(null, myDb, null, BerkeleyDb.DbType.BTree, Db.OpenFlags.Create, 0);
                if (dbConfig.OpenFlags == Db.OpenFlags.None && File.Exists(Path.GetFullPath(fileName)))
                {
                    DbFile dbFile = db.Open(null, fileName, null, BerkeleyDb.DbType.Unknown, Db.OpenFlags.None, 0);
                    if (dbFile.DbType == BerkeleyDb.DbType.Hash)
                    {
                        dbHash = (DbHash)dbFile;
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:CreateDatabase() File {2} contains {3} type of database. DbHash is expected."
                                , Thread.CurrentThread.ManagedThreadId, logPrefix, fileName, dbFile.DbType);
                        if (Log.IsErrorEnabled)
                        {
                            Log.Write(Log.Status.Error, sb.ToString());
                        }
                        if (db != null)
                        {
                            db.Close();
                        }
                        RemoveDb(dbConfig.FileName, null);
                        throw new ApplicationException(sb.ToString());
                    }
                }
                else
                {
                    dbHash = (DbHash)db.Open(null, fileName, null, BerkeleyDb.DbType.Hash, dbConfig.OpenFlags, 0);
                }
                if (Log.IsDebugEnabled)
                {
                    Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() DB for the file {2} is opened."
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, fileName);
                }
                //txn.Commit(Txn.CommitMode.None);
                if (dbHash != null)
                {
                    if (Log.IsInfoEnabled)
                    {
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: DbFlags = {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, db.GetFlags());
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: FileName = {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, GetDbFileName(db));
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: OpenFlags = {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, dbHash.DbOpenFlags);
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: ErrorPrefix = {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, db.ErrorPrefix);
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: CacheFile MaxSize = {2}, PageSize = {3}, CacheFileFlags = {4}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, db.CacheFile.MaxSize, db.CacheFile.PageSize, db.CacheFile.GetFlags());
                        //Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: CacheSize GigaBytes = {2}, Bytes {3}, NumCaches ={4}"
                        //    , Thread.CurrentThread.ManagedThreadId, logPrefix, db.CacheSize.GigaBytes, db.CacheSize.Bytes, db.CacheSize.NumCaches);
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: HashCode = {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, db.GetHashCode());
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: HashFillFactor = {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, db.HashFillFactor);
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: PageSize = {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, db.PageSize);
                        //Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: RecDelim = {2}"
                        //    , Thread.CurrentThread.ManagedThreadId, logPrefix, db.RecDelim);
                        //Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: RecLen = {2}"
                        //    , Thread.CurrentThread.ManagedThreadId, logPrefix, db.RecLen);
                        //Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: RecPad = {2}"
                        //    , Thread.CurrentThread.ManagedThreadId, logPrefix, db.RecPad);
                        //Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Db: RecSource = {2}"
                        //    , Thread.CurrentThread.ManagedThreadId, logPrefix, db.RecSource);
                    }
                    if (Log.IsDebugEnabled)
                    {
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() ##BEGIN DB STAT############################################"
                           , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        dbHash.PrintStats(StatPrintFlags.All);
                        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() ##END DB STAT##############################################"
                           , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    }
                }
                return dbHash;
            }
            catch (BdbException ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Got BdbException"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                if (db != null)
                {
                    db.Close();
                }
                throw;
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:CreateDatabase() Error Creating Database for the file {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, fileName);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                if (db != null)
                {
                    db.Close();
                }
                throw;
            }
            finally
            {
            }
        }

        private void CreateDirectory(string dir)
        {
            if (!Directory.Exists(dir))
            {
                if (Log.IsInfoEnabled)
                {
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateDirectory() Creates directory {2}."
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, dir);
                }
                Directory.CreateDirectory(dir);
            }
        }

        private Env CreateEnvironment(EnvironmentConfig envConfig)
        {
            Txn txn = null;
            try
            {
                Env environment = new Env(envConfig.CreateFlags);
                if (Log.IsInfoEnabled)
                {
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateEnvironment() Environment handle is created"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                }
                int gigaBytes = envConfig.CacheSize.GigaBytes;
                int bytes = envConfig.CacheSize.Bytes;  // 50 * 1024 * 1024;
                int numberCaches = envConfig.CacheSize.NumberCaches;
                environment.CacheSize = new CacheSize(gigaBytes, bytes, numberCaches);
                if (Log.IsInfoEnabled)
                {
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateEnvironment() Environment CacheSize GigaBytes = {2}, Bytes = {3}, NumberCaches = {4}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, environment.CacheSize.GigaBytes, environment.CacheSize.Bytes, environment.CacheSize.NumCaches);
                }

                SetEnvironmentConfiguration(ref environment, envConfig);
                CreateDirectory(envConfig.HomeDirectory);
                environment.Open(envConfig.HomeDirectory, envConfig.OpenFlags, 0);
                if (Log.IsInfoEnabled)
                {
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:CreateEnvironment() Environment is opened"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                }
                return environment;
            }
            catch (BdbException ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:CreateEnvironment() Got BdbException"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                if (txn != null && !txn.IsDisposed)
                    txn.Abort();

                throw;
                // errors are reported through error stream already
                //return;
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:CreateEnvironment() Error Initializing Environment"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                if (txn != null && !txn.IsDisposed)
                    txn.Abort();
                //errWriter.WriteLine(ex.ToString());
                //return;
                throw;
            }
            finally
            {
                //if (db != null)
                //    db.Close();
                //if (env != null)
                //{
                //    env.Close();
                //}
                //msgWriter.Flush();
                //string msgText = utf8.GetString(msgStream.GetBuffer());
                //if (msgText != "")
                //{
                //    msgBox.Text = msgText;
                //}
                //msgWriter.Close();
                //errWriter.Flush();
                //string errText = utf8.GetString(errStream.GetBuffer());
                //if (errText != "")
                //{
                //    errBox.Text = errText;
                //    tabControl.SelectTab("errorPage");
                //}
                //errWriter.Close();
            }
        }

        private bool DeleteRecord(DbHash db, Txn txn, byte[] keyData, DbFile.WriteFlags flags)
        {
            //ulong keyValue = GetKey(messageID, typeID);
            DbEntry key = DbEntry.InOut(keyData);
            try
            {
                DeleteStatus status = db.Delete(txn, ref key, flags);
                switch (status)
                {
                    case DeleteStatus.Success:
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:DeleteObject() object is deleted"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        break;
                    case DeleteStatus.KeyEmpty:
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:DeleteObject() Key is empty"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        break;
                    case DeleteStatus.NotFound:
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:DeleteObject() Key is not found"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        break;
                    default:
                        if (Log.IsErrorEnabled)
                        {
                            Log.Write(Log.Status.Error, "[{0}] {1}: BerkeleyDbStorage:DeleteObject() unknkown Delete Status"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        break;
                }
                return status == DeleteStatus.Success;
            }
            catch (BdbException ex)
            {
                HandleBdbError(ex, db);
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:DeleteRecord() Error Deleting record"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                throw;
            }
            return false;
        }

        private DbHash GetDatabase(short key)
        {
            DbHash dbHash;
            if (!databases.ContainsKey(key))
            {
                using (envLock.WaitToWrite())
                {
                    if (!databases.ContainsKey(key))
                    {
                        if (Log.IsInfoEnabled)
                        {
                            Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:GetDatabase() no database found for the key {2}"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix, key);
                        }
                        DatabaseConfig dbConfig = GetDatabaseConfig(key);
                        databases.Add(key, CreateDatabase(ref this.env, dbConfig));
                    }
                }
            }
            using (envLock.WaitToRead())
            {
                dbHash = databases[key];
                if (Log.IsDebugEnabled)
                {
                    Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:GetDatabase() got database for the key {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, key);
                }
            }
            if (dbHash.GetDb().IsDisposed)
            {
                if (Log.IsInfoEnabled)
                {
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:GetDatabase() database for the key {2} is disposed"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, key);
                }
                Db db = null;
                //string fileName;
                //string dbName;
                try
                {
                    db = dbHash.GetDb();
                    db.Close();
                    databases.Remove(key);
                    //db.Dbf.GetName(out fileName, out dbName);
                    DatabaseConfig dbConfig = GetDatabaseConfig(key);
                    dbHash = CreateDatabase(ref this.env, dbConfig);
                }
                catch (Exception ex)
                {
                    if (Log.IsErrorEnabled)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:GetDatabase() Error opening database for the key {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, key);
                        Log.Write(Log.Status.Error, sb.ToString(), ex);
                    }
                    if (db != null)
                    {
                        db.Close();
                    }
                    throw;
                }
            }
            return dbHash;
            //return databases[key];
        }

        private DatabaseConfig GetDatabaseConfig(short key)
        {
            DatabaseConfig dbConfig = envConfig.DatabaseConfigs.GetConfigFor(key);
            if (dbConfig == null)
            {
                //dbConfig = new DatabaseConfig(key);
                DatabaseConfig defaultConfig = envConfig.DatabaseConfigs.GetConfigFor(0);
                dbConfig = defaultConfig.Clone(key);
            }
            return dbConfig;
        }

        private string GetDbFileName(Db db)
        {
            string fileName = "";
            string dbName = "";
            db.Dbf.GetName(out fileName, out dbName);
            return fileName;
        }

        private string GetDbFilePath(string homeDir, short typeId)
        {
            return homeDir + "/" + GetDatabaseConfig(typeId).FileName;
        }

        //private ulong GetKey(int messageID, int typeID)
        //{
        //    int temp = 0;

        //    temp = messageID << 8;
        //    temp = temp | typeID;

        //    return (ulong)temp;
        //}

        private byte[] GetKey(ulong key)
        {
            using (MemoryStream keyStream = new MemoryStream())
            {
                keyStream.Position = 0;
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(keyStream, key);
                return keyStream.GetBuffer();
            }
        }

        private byte[] GetKey(int key)
        {
            using (MemoryStream keyStream = new MemoryStream())
            {
                keyStream.Position = 0;
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(keyStream, key);
                return keyStream.GetBuffer();
            }
        }

        private byte[] GetKey(string key)
        {
            ASCIIEncoding encoder = new ASCIIEncoding();
            return encoder.GetBytes(key);
        }

        private ReadStatus GetRecord(DbHash db, Txn txn, ref DbEntry key, ref DbEntry data, DbFile.ReadFlags readFlags)
        {
            ReadStatus status = db.Get(txn, ref key, ref data, readFlags);
            if (status == ReadStatus.BufferSmall)
            {
                data.ResizeBuffer(data.Size);
                status = db.Get(txn, ref key, ref data, readFlags);
            }
            return status;
        }

        private ReadStatus GetRecord(DbHash db, Txn txn, ref DbEntry key, ref DbEntry data, DbFile.ReadFlags readFlags, MemoryStream dataStream)
        {
            ReadStatus status = db.Get(txn, ref key, ref data, readFlags);
            if (status == ReadStatus.BufferSmall)
            {
                //if (key.Buffer.Length < data.Size)
                //{
                //    keyStream.SetLength(key.Size);
                //    key = DbEntry.Out(keyStream.GetBuffer());
                //}
                if (data.Buffer.Length < data.Size)
                {
                    dataStream.SetLength(data.Size);
                    data = DbEntry.Out(dataStream.GetBuffer());
                }
                status = db.Get(txn, ref key, ref data, readFlags);
            }
            return status;
        }

        private byte[] GetRecord(DbHash db, Txn txn, byte[] keyData, DbFile.ReadFlags readFlags)
        {
            ResourcePoolItem<MemoryStream> pooledStream = null;
            try
            {
                DbEntry key = DbEntry.InOut(keyData);

                pooledStream = memoryPoolStream.GetItem();
                MemoryStream dataStream = pooledStream.Item;
                dataStream.SetLength(dataStream.Capacity);
                byte[] bytes = dataStream.GetBuffer();
                DbEntry data = DbEntry.Out(bytes);
                //DbEntry data = DbEntry.Out(new byte[bufferSize]);

                ReadStatus status = GetRecord(db, txn, ref key, ref data, readFlags, dataStream);
                //ReadStatus status = GetRecord(db, txn, ref key, ref data, readFlags, dataStream);
                switch (status)
                {
                    case ReadStatus.BufferSmall:
                    case ReadStatus.KeyEmpty:
                    case ReadStatus.NotFound:
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:GetRecord() record is not fetched with status {4}"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix, status);
                        }
                        return null;
                    case ReadStatus.Success:
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:GetRecord() record is fetched"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        //if (data.Size != data.Buffer.Length)
                        //{
                        //    data.ResizeBuffer(data.Size);
                        //}
                        //return data.Buffer;
                        //dataStream.Position = 0;
                        dataStream.SetLength(data.Size);
                        return dataStream.ToArray();
                    default:
                        if (Log.IsErrorEnabled)
                        {
                            Log.Write(Log.Status.Error, "[{0}] {1}: BerkeleyDbStorage:GetRecord() unknown Write Status"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix);
                        }
                        return null;
                }
            }
            catch (BdbException ex)
            {
                HandleBdbError(ex, db);
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:AddRecord() Error Adding record"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                throw;
            }
            finally
            {
                memoryPoolStream.ReleaseItem(pooledStream);
            }
            return null;
       }

        private void HandleBdbError(BdbException ex, DbHash dbHash)
        {
            if (Log.IsErrorEnabled)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:HandleBdbError() trying to handle BdbException"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
                Log.Write(Log.Status.Error, sb.ToString(), ex);
            }

            switch (ex.Error)
            {
                case DbRetVal.PAGE_NOTFOUND:
                case DbRetVal.RUNRECOVERY:
                    //dbHash.Truncate(null, DbFile.TruncateMode.None);
                    //if (Log.IsErrorEnabled)
                    //{
                    //    StringBuilder sb = new StringBuilder();
                    //    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:HandleBdbError() database has been trancated"
                    //        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    //    Log.Write(Log.Status.Error, sb.ToString(), ex);
                    //} 
                    RemoveDb(dbHash.GetDb());
                    break;
                default:
                    throw ex;
            }
        }

        //private void HandleBdbError(BdbException ex, Db db)
        //{
        //    if (Log.IsErrorEnabled)
        //    {
        //        StringBuilder sb = new StringBuilder();
        //        sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:HandleBdbError() trying to handle BdbException"
        //            , Thread.CurrentThread.ManagedThreadId, logPrefix);
        //        Log.Write(Log.Status.Error, sb.ToString(), ex);
        //    }

        //    switch (ex.Error)
        //    {
        //        case DbRetVal.RUNRECOVERY:
        //            RunRecovery(db);
        //            break;
        //        default:
        //            throw ex;
        //    }
        //}

        private bool IsEnvironmentValid()
        {
            Env environment = null;
            DbHash adminDb = null;
            try
            {
                //DbHash dbHash = GetDatabase(adminDbKey);
                DatabaseConfig dbConfig = GetDatabaseConfig(adminDbKey);
                adminDb = CreateDatabase(ref environment, dbConfig);
                byte[] key = GetKey(shutdownTimeKey);
                byte[] data = GetRecord(adminDb, null, key, DbFile.ReadFlags.None);
                DateTime shutdownTime = ToDateTime(data);
                long shutdownWindow = (DateTime.Now.Ticks - shutdownTime.Ticks) / TimeSpan.TicksPerSecond;
                return shutdownWindow > bdbConfig.ShutdownWindow;
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:IsEnvironmentValid() Error validating environment."
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
            }
            finally
            {
                if (adminDb != null)
                {
                    Db db = adminDb.GetDb();
                    Env dbEnv = db.GetEnv();
                    db.Close();
                    db.Dispose();
                    if (dbEnv != null)
                    {
                        dbEnv.Close();
                        dbEnv.Dispose();
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// This guy does the meat of cleaning expired files off of the disk. This is
        /// called on a configurable interval. Once the file has expired, the directory
        /// will automatically ruturn null even thought the file might not be cleaned up
        /// yet. After expiration it is assumed to be dead.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //void KillExpiredFiles(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    try
        //    {
        //        if (Log.IsInfoEnabled)
        //        {
        //            //Debug.WriteLine("Expiration: Beginning...", "Nemo");
        //            Log.Write(Log.Status.Info, "{0}: Expiration: Beginning...", "Nemo");
        //        }
        //        bool drivesReactivated = false;

        //        int count = 0;

        //        for (int i = 0; i < drives.Length; i++)
        //        {
        //            DriveInfo di = drives[i];

        //            bool filesKilled = false;

        //            if (Log.IsInfoEnabled)
        //            {
        //                //Debug.WriteLine("Expiration: Looking in drive " + di.RootDirectory.ToString(), "Nemo");
        //                Log.Write(Log.Status.Info, "{0}: Expiration: Looking in drive {1}", "Nemo", di.RootDirectory.ToString());
        //            }

        //            foreach (FileInfo fi in di.RootDirectory.GetFiles("*.c", SearchOption.TopDirectoryOnly))
        //            {
        //                if (Expired(fi.CreationTime.Ticks))
        //                {
        //                    try
        //                    {
        //                        File.Delete(fi.FullName);
        //                        File.Delete(fi.FullName.Replace("c", "d"));
        //                        count++;

        //                        //Keep CPU load down as much as possible
        //                        Thread.Sleep(2);
        //                        filesKilled = true;
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        // If the file is in use, it will just stay on this disk and be picked
        //                        // up next expiration process.
        //                        if (Log.IsErrorEnabled)
        //                        {
        //                            Log.Write(Log.Status.Error, "Nemo: Error Deleting File", ex);
        //                        }
        //                    }
        //                }
        //            }

        //            #region Reactivate drives available for write

        //            if (filesKilled && !driveUse[i])
        //            {
        //                drivesReactivated = true;
        //                driveUse[i] = true;
        //            }

        //            #endregion
        //        }

        //        if (!takingInserts)
        //        {
        //            if (drivesReactivated)
        //            {
        //                takingInserts = true;
        //                if (Log.IsInfoEnabled)
        //                {
        //                    //Debug.WriteLine("Expiration: Has reactivated drive(s)", "Nemo");
        //                    Log.Write(Log.Status.Info, "{0}: Expiration: Has reactivated drive(s)", "Nemo");
        //                }
        //            }
        //        }

        //        if (Log.IsInfoEnabled)
        //        {
        //            //Debug.WriteLine("Expiration: " + count + " files expired...", "Nemo");
        //            Log.Write(Log.Status.Info, "{0}: Expiration: {1} files expired...", "Nemo", count);
        //        }

        //        if (serCounter != null)
        //        {
        //            serCounter.RawValue = memoryDirectory.DiskCount;
        //        }
        //    }
        //    catch (Exception exc)
        //    {
        //        if (Log.IsErrorEnabled)
        //        {
        //            //Debug.WriteLine("Expiration: An error has occured durring file expiration", "Nemo");
        //            //Debug.WriteLine("Expiration: " + exc.Message + " " + exc.StackTrace, "Nemo");
        //            Log.Write(Log.Status.Error, "Nemo: Expiration: An error has occured durring file expiration", exc);
        //        }
        //    }
        //}

        private void LoadConfig(BerkeleyDbConfig bdbConfig)
        {
            this.bdbConfig = bdbConfig;
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() BerkeleyDbConfig: BufferSize = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, bdbConfig.BufferSize);
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() BerkeleyDbConfig: MinTypeId = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, bdbConfig.MinTypeId);
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() BerkeleyDbConfig: MaxTypeId = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, bdbConfig.MaxTypeId);
            }
            this.bufferSize = bdbConfig.BufferSize;
            this.minTypeId = bdbConfig.MinTypeId;
            this.maxTypeId = bdbConfig.MaxTypeId;
            this.envConfig = bdbConfig.EnvironmentConfig;
            if (Log.IsDebugEnabled)
            {
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() EnvironmentConfig: CreateFlags = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, this.envConfig.CreateFlags);
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() EnvironmentConfig: ErrorPrefix = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, this.envConfig.ErrorPrefix);
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() EnvironmentConfig: CacheSize = {2} gigabyts, {3} bytes, {4} number of caches"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, this.envConfig.CacheSize.GigaBytes, this.envConfig.CacheSize.Bytes, this.envConfig.CacheSize.NumberCaches);
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() EnvironmentConfig: HomeDirectory = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, this.envConfig.HomeDirectory);
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() EnvironmentConfig: OpenFlags = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, this.envConfig.OpenFlags);
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() EnvironmentConfig: TempDirectory = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, this.envConfig.TempDirectory);
            }
        }

        private void LogMessages(Env environment, string message)
        {
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: {2}", Thread.CurrentThread.ManagedThreadId, logPrefix, message);
            }
            int keyCount = 0;
            string keyPhrase = "Number of keys in the database";
            if (message.EndsWith(keyPhrase))
            {
                int index = message.IndexOf("\t");
                string keyCountString = message.Substring(0, index);
                keyCount = Convert.ToInt32(keyCountString);
                BerkeleyDbCounters.Instance.SetCurrentNumberOfObjects(keyCount);
            }
        }

        private void RecreateEnv(ref Env environment, EnvironmentConfig envConfig)
        {
            try
            {
                CloseAllDatabases();
                if (environment != null && !environment.IsDisposed)
                {
                    //Shutdown();
                    environment.Close();
                    environment.Dispose();
                }
                RemoveAllFiles(envConfig.HomeDirectory);
                //using (Env env1 = new Env(EnvCreateFlags.None))
                //{
                //    if (Log.IsInfoEnabled)
                //    {
                //        Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:RecreateEnv() Removes environment ..."
                //            , Thread.CurrentThread.ManagedThreadId, logPrefix);
                //    }
                //    env1.Remove(envConfig.HomeDirectory, Env.RemoveFlags.Force);
                //}
                //Initialize(this.instanceName, this.bdbConfig);
                environment = CreateEnvironment(envConfig);
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:RecreateEnv() fails recreating environment"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                throw;
            }
        }

        private void ReloadDb(DatabaseConfig dbConfig)
        {
            short key = dbConfig.Id;
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() DatabaseConfig: Id = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, key);
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() DatabaseConfig: CreateFlags = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, dbConfig.CreateFlags);
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() DatabaseConfig: FileName = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, dbConfig.FileName);
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() DatabaseConfig: OpenFlags = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, dbConfig.OpenFlags);
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() DatabaseConfig: ErrorPrefix = {2}"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, dbConfig.ErrorPrefix);
            }
            if (databases.ContainsKey(key))
            {
                Db db;
                DbHash dbHash = null;
                using (envLock.WaitToRead())
                {
                    if (databases.ContainsKey(key))
                    {
                        dbHash = databases[key];
                    }
                }
                if (dbHash != null)
                {
                    db = dbHash.GetDb();
                    if (db.IsDisposed)
                    {
                        using (envLock.WaitToWrite())
                        {
                            databases.Remove(key);
                        }
                    }
                    else
                    {
                        if (dbHash.DbOpenFlags != dbConfig.OpenFlags || GetDbFileName(db) != dbConfig.FileName)
                        {
                            using (envLock.WaitToWrite())
                            {
                                databases.Remove(key);
                            }
                            db.Close();
                            db.Dispose();
                        }
                        else
                        {
                            SetDatabaseConfiguration(db, dbConfig);
                        }
                    }
                }
            }
        }

        private void RemoveAllFiles(string homeDir)
        {
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:RemoveAllFiles() Removing all files in {2} directory ..."
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
            if (Directory.Exists(homeDir))
            {
                Directory.Delete(homeDir, true);
                Directory.CreateDirectory(homeDir);
            }
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:RemoveAllFiles() Removing all files in {2} directory complete"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
        }

        private void RemoveAllDbFiles(string homeDir)
        {
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:RemoveAllFiles() Removing all DB files in {2} directory ..."
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, homeDir);
            }
            for (short i = minTypeId; i <= maxTypeId; i++)
            {
                RemoveDb(GetDbFilePath(homeDir, i), null);
            }
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:RemoveAllFiles() Removing all DB files {2} directory complete"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, homeDir);
            }
        }

        private void RemoveDb(Db db)
        {
            string dir = this.env.Home;
            string fileName = "";
            string dbName = "";
            string filePath = "";
            db.Dbf.GetName(out fileName, out dbName);
            Shutdown();
            filePath = dir + "/" + fileName;
            RemoveDb(filePath, dbName);
            //Shutdown();
            //Initialize(this.instanceName, this.bdbConfig);
        }

        private void RemoveDb(string filePath, string dbName)
        {
            try
            {
                using (Db db1 = new Db(DbCreateFlags.None))
                {
                    if (dbName == "")
                    {
                        dbName = null;
                    }
                    db1.Remove(filePath, dbName);
                }
                if (Log.IsDebugEnabled)
                {
                    if (dbName == null)
                    {
                        Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:RemoveDb() removed file {2}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, filePath);
                    }
                    else
                    {
                        Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:RemoveDb() removed database {2} from file {3}"
                            , Thread.CurrentThread.ManagedThreadId, logPrefix, dbName, filePath);
                    }
                }
            }
            catch (BdbException ex)
            {
                switch (ex.Error)
                {
                    case DbRetVal.ENOENT:
                        if (Log.IsErrorEnabled)
                        {
                            Log.Write(Log.Status.Error, "[{0}] {1}: BerkeleyDbStorage:RemoveDb() file {2} is not found. Do nothing."
                                , Thread.CurrentThread.ManagedThreadId, logPrefix, filePath);
                        }
                        break;
                    default:
                        if (Log.IsErrorEnabled)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:RemoveDb() got BdbException removing file {2}"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix, filePath);
                            Log.Write(Log.Status.Error, sb.ToString(), ex);
                        }
                        throw;
                }
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:RemoveDb() fails removing file {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, filePath);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                throw;
            }
        }

        private bool RequiresRecreateEnv(EnvironmentConfig oldConfig, EnvironmentConfig newConfig)
        {
            EnvCacheSize oldCacheSize = oldConfig.CacheSize;
            EnvCacheSize newCacheSize = newConfig.CacheSize;
            return newConfig.OpenFlags != oldConfig.OpenFlags
                || newConfig.HomeDirectory != oldConfig.HomeDirectory
                || newCacheSize.GigaBytes != oldCacheSize.GigaBytes
                || newCacheSize.Bytes != oldCacheSize.Bytes
                || newCacheSize.NumberCaches != oldCacheSize.NumberCaches;
        }

        private void RunRecovery(Env environment, int errValue)
        {
            switch (errValue)
            {
                default:
                    if (Log.IsErrorEnabled)
                    {
                        Log.Write(Log.Status.Error, "[{0}] {1}: BerkeleyDbStorage:RunRecovery() errValue = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, errValue);
                    }
                    break;
            }
        }

        ////DO NOT USE !!! (Not ready yet)
        //private void RunRecovery(Db db)
        //{
        //    string fileName = "";
        //    string dbName = "";
        //    db.Dbf.GetName(out fileName, out dbName);
        //    VerifyFlags flags = VerifyFlags.Aggressive;
        //    using (Stream dumpStream = new MemoryStream())
        //    {
        //        db.Verify(fileName, dbName, dumpStream, flags);
        //    }
        //}

        private void SaveEnvironmentData()
        {
            try
            {
                DbHash adminDb = GetDatabase(adminDbKey);
                byte[] key = GetKey(shutdownTimeKey);
                DateTime shutdownTime = DateTime.Now;
                byte[] data = ToByteArray(shutdownTime);
                bool success = AddRecord(adminDb, null, key, data, DbFile.WriteFlags.None);
                if (Log.IsInfoEnabled)
                {
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:SaveEnvironmentData() saved ShutdownTime = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, shutdownTime);
                }
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:Shutdown() Failed to write Shutdown time."
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
            }
        }

        private void SetDatabaseConfiguration(Db db, DatabaseConfig dbConfig)
        {
            // set the BTree comparison function
            //db.BTreeCompare = AppCompare;
            // error and message reporting already configured on environment
            db.ErrorPrefix = dbConfig.ErrorPrefix;//Path.GetFileName(AppDomain.CurrentDomain.BaseDirectory);
            // db.ErrorStream = errStream;
            // db.MessageStream = msgStream;
            //db.CacheFile.MaxSize = new DataSize(0, 0);
            //db.CacheFile.Priority = CacheFilePriority.Default;
            //db.CacheFile.SetFlags(CacheFileFlags.NoFile, true);
            //CacheSize cacheSize = new CacheSize(0, 0, 0);
            //db.CacheSize = cacheSize;
        }

        private void SetEnvironmentConfiguration(ref Env environment, EnvironmentConfig envConfig)
        {
            //MemoryStream errStream = new MemoryStream();
            //MemoryStream msgStream = new MemoryStream();
            //TextWriter errWriter = new StreamWriter(errStream);
            //TextWriter msgWriter = new StreamWriter(msgStream);

            // configure for error and message reporting
            environment.ErrorPrefix = envConfig.ErrorPrefix;
            //env.ErrorStream = errStream;
            //env.MessageStream = msgStream;

            CreateDirectory(envConfig.TempDirectory);
            environment.TmpDir = envConfig.TempDirectory;
            environment.PanicCall += new Env.PanicCallFcn(RunRecovery);
            environment.MessageCall += new Env.MsgCallFcn(LogMessages);
            if (Log.IsDebugEnabled)
            {
                environment.SetVerbose(Env.DbVerb.DeadLock, true);
                environment.SetVerbose(Env.DbVerb.Recovery, true);
                environment.SetVerbose(Env.DbVerb.Replication, true);
                environment.SetVerbose(Env.DbVerb.WaitsFor, true);
            }
        }

        private byte[] ToByteArray(long data)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                stream.Position = 0;
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, data);
                return stream.GetBuffer();
            }
        }

        private byte[] ToByteArray(DateTime data)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                stream.Position = 0;
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, data);
                return stream.GetBuffer();
            }
        }

        private DateTime ToDateTime(byte[] data)
        {
            if (data == null)
            {
                return DateTime.MinValue;
            }
            using (MemoryStream stream = new MemoryStream(data))
            {
                stream.Position = 0;
                BinaryFormatter formatter = new BinaryFormatter();
                DateTime dt = (DateTime)formatter.Deserialize(stream);
                return dt;
            }
        }
        #endregion

		#region Public Methods
        public int DeleteAll()
        {
            int count = 0;
            if (Log.IsDebugEnabled)
            {
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:DeleteAll() is deleting all."
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
            //using (directoryLock.WaitToWrite())
            //{
            //    memoryDirectory.ClearAll();
            //    memoryDirectory = new MemoryDirectory(settings.DiskAgeOut, settings.MaximumType);
            //}
            for (short key = minTypeId; key <= maxTypeId; key++)
            {
                count += DeleteAllInType(key);
            }
            return count;
            //UpdateMemoryCounters();
        }
        
        /// <summary>
        /// Remove all objects in the store of a given type
        /// </summary>
        /// <param name="typeID">The type to remove</param>
        public int DeleteAllInType(short typeId)
        {
            if (Log.IsDebugEnabled)
            {
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:DeleteAllInType() deletes all objects of the type (TypeId={2})"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, typeId);
            }
            DbHash dbHash = null;
            try
            {
                dbHash = GetDatabase(typeId);
                return dbHash.Truncate(null, DbFile.TruncateMode.None);
            }
            catch (BdbException ex)
            {
                HandleBdbError(ex, dbHash);
            }
            catch (Exception ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:DeleteAllInType() Error deleting all objects of the type {2}" 
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, typeId);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                throw;
            }
            return 0;
        }

        public bool DeleteObject(short typeId, int objectId)
        {
            if (!CanProcessMessage(typeId))
            {
                return false;
            }

            //using (GetEntryLock(keyValue).WaitToWrite())
            //{
            if (Log.IsDebugEnabled)
            {
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:DeleteObject() deletes object (TypeId={2}, ObjectId={3})"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, typeId, objectId);
            }
            DbHash db = GetDatabase(typeId);
            return DeleteRecord(db, null, GetKey(objectId), DbFile.WriteFlags.None);
            //}
            //UpdateMemoryCounters();
        }

        /// <summary>
        /// Remove the object id in all known types
        /// </summary>
        /// <param name="objectID">The object to remove</param>
        public int DeleteObjectInAllTypes(int objectId)
        {
            int count = 0;
            using (envLock.WaitToRead())
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:DeleteObjectInAllTypes() deletes all types of objects for the given ID (ObjectId={2})"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, objectId);
                }
                foreach (short dbKey in databases.Keys)
                {
                    //DbHash db = databases[key];
                    //DeleteRecord(db, null, GetKey(objectID, key), DbFile.WriteFlags.None);
                    if (DeleteObject(dbKey, objectId))
                    {
                        count++;
                    }
                }
            }
            return count;
            //UpdateMemoryCounters();
        }

        /// <summary>
        /// Retrieve a CacheMessage object from the Nemo Store
        /// </summary>
        /// <param name="messageID">the Message ID</param>
        /// <param name="typeID">The Type ID of the message</param>
        /// <returns>The stored CacheMessage object</returns>
        public byte[] GetObject(short typeId, int objectId)
        {
            if (!CanProcessMessage(typeId))
            {
                return null;
            }
            
            //using (GetEntryLock(keyValue).WaitToRead())
            //{
            if (Log.IsDebugEnabled)
            {
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:GetObject() gets record (TypeId={2}, ObjectId={3})"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, typeId, objectId);
            }
                //Txn txn = env.TxnBegin(null, Txn.BeginFlags.None);
            DbHash db = GetDatabase(typeId);
            return GetRecord(db, null, GetKey(objectId), DbFile.ReadFlags.None);
                //txn.Commit(Txn.CommitMode.None);

            //}
        }

        public void GetStats()
        {
            using (envLock.WaitToRead())
            {
                foreach (short key in databases.Keys)
                {
                    DbHash dbHash = databases[key];
                    if (!dbHash.GetDb().IsDisposed)
                    {
                        dbHash.PrintStats(StatPrintFlags.All);
                    }
                    else
                    {
                        if (Log.IsDebugEnabled)
                        {
                            Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:GetStats() cannot print statistic since database is disposed for the key {2}"
                                , Thread.CurrentThread.ManagedThreadId, logPrefix, key);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// General initialization of the BerkeleyDbStorage. This will load all physical drives into memory for
        /// fast access later, install performance counters, and initialize worker services. This must be called
        /// before attempting to use the BerkeleyDbStorage.
        /// </summary>
        /// <param name="attemptRestore">If true, the BerkeleyDbStorage will attempt to recover the cache from disk.
        /// If false, the BerkeleyDbStorage will contain no data.</param>
        public void Initialize(string instanceName, BerkeleyDbConfig bdbConfig)
        {
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:Initialize() Initializing ..."
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
            if (bdbConfig == null)
            {
                throw new ApplicationException("Unable to Reload NULL BereleyDbConfig.");
            }
            if (bdbConfig.EnvironmentConfig == null)
            {
                throw new ApplicationException("Unable to Reload NULL EnvironmentConfig.");
            }
            LoadConfig(bdbConfig);
            try
            {
                if (IsEnvironmentValid())
                {
                    RemoveAllFiles(envConfig.HomeDirectory);
                }
                this.env = CreateEnvironment(envConfig);
            }
            catch (BdbException ex)
            {
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("[{0}] {1}: BerkeleyDbStorage:Initialize() Got BdbException. Trying to Recreate Environment."
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                    Log.Write(Log.Status.Error, sb.ToString(), ex);
                }
                RecreateEnv(ref this.env, envConfig);
            }

            this.instanceName = instanceName;

            //ArrayList tempDrives = new ArrayList();
            //ArrayList tempUse = new ArrayList();

            #region Load in Drive Information

            //foreach (DriveInfo di in DriveInfo.GetDrives())
            //{
            //    //Add the current drive information into a list with the current space
            //    //the space will be modified later to keep track without going back to
            //    //the disk.
            //    if (di.DriveType == DriveType.Fixed)
            //    {
            //        tempDrives.Add(di);
            //        tempUse.Add(true);
            //    }
            //}

            //// Cache the drive information in memory
            //drives = (DriveInfo[])tempDrives.ToArray(typeof(DriveInfo));
            //driveUse = (bool[])tempUse.ToArray(typeof(bool));

            #endregion

            //BootUpWorkers();

            //takingInserts = true;

            //ReviveDirectories();

            using (stateLock.WaitToWrite())
            {
                badStates.Clear();
            }

            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:Initialize() Initialize Complete."
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
        }

        public void ReloadConfig(BerkeleyDbConfig bdbConfig)
        {
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Reloading ..."
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
            if (bdbConfig == null)
            {
                throw new ApplicationException("Unable to Reload NULL BereleyDbConfig.");
            }
            EnvironmentConfig newEnvConfig = bdbConfig.EnvironmentConfig;
            if (newEnvConfig == null)
            {
                throw new ApplicationException("Unable to Reload NULL EnvironmentConfig.");
            }

            if (RequiresRecreateEnv(this.envConfig, newEnvConfig))
            {
                if (Log.IsInfoEnabled)
                {
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Reload requires recreating Environment"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                }
                Shutdown();
                Initialize(this.instanceName, bdbConfig);
            }
            else
            {
                LoadConfig(bdbConfig);
                SetEnvironmentConfiguration(ref this.env, this.envConfig);

                if (Log.IsInfoEnabled)
                {
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: Home = {2}"
                       , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.Home);
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: EnvOpenFlags = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.EnvOpenFlags);
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: ErrorPrefix = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.ErrorPrefix);
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: CacheSize = {2} gigabyts, {3} bytes, {4} number of caches"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.CacheSize.GigaBytes, env.CacheSize.Bytes, env.CacheSize.NumCaches);
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: TmpDir = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.TmpDir);
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: LogDir = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.LogDir);
                    //Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: LogBufSize = {2}"
                    //    , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.LogBufSize);
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: LockConflicts = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.LockConflicts);
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: LockDetectMode = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.LockDetectMode);
                    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Env: LockTimeout = {2}"
                        , Thread.CurrentThread.ManagedThreadId, logPrefix, this.env.LockTimeout);
                }
                //if (Log.IsDebugEnabled)
                //{
                //    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() ##BEGIN ENV STAT############################################"
                //       , Thread.CurrentThread.ManagedThreadId, logPrefix);
                //    this.env.PrintStats(Env.StatPrintFlags.All);
                //    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() ##END ENV STAT##############################################"
                //        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                //    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() ##BEGIN ENV CACHE STAT######################################"
                //        , Thread.CurrentThread.ManagedThreadId, logPrefix);
                //    this.env.PrintCacheStats(CacheStatPrintFlags.All);
                //    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() ##END ENV CACHE STAT########################################"
                //       , Thread.CurrentThread.ManagedThreadId, logPrefix);
                //    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() ##BEGIN ENV LOCK STAT#######################################"
                //       , Thread.CurrentThread.ManagedThreadId, logPrefix);
                //    this.env.PrintLockStats(LockStatPrintFlags.All);
                //    Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() ##END ENV LOCK STAT#########################################"
                //       , Thread.CurrentThread.ManagedThreadId, logPrefix);
                //    //env.PrintLogStats(StatPrintFlags.All);
                //    //env.PrintRepStats(StatPrintFlags.All);
                //    //env.PrintTxnStats(StatPrintFlags.All);
                //}

                foreach (DatabaseConfig dbConfig in this.envConfig.DatabaseConfigs)
                {
                    ReloadDb(dbConfig);
                }
            }
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: BerkeleyDbStorage:ReloadConfig() Reloading is complete"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
        }

        /// <summary>
        /// Save a CacheMessage object into the store
        /// </summary>
        /// <param name="message">The message to be saved into the store</param>
        public bool SaveObject(short typeId, int objectId, byte[] payload)
        {
            if (!CanProcessMessage(typeId))
            {
                return false;
            }

            //using (GetEntryLock(GetKey(obj)).WaitToWrite())
            //{
            if (Log.IsDebugEnabled)
            {
                Log.Write(Log.Status.Debug, "[{0}] {1}: BerkeleyDbStorage:SaveObject() saves object (TypeId={2}, ObjectId={3})"
                    , Thread.CurrentThread.ManagedThreadId, logPrefix, typeId, objectId);
            }
                //txn = env.TxnBegin(null, Txn.BeginFlags.None);
            DbHash db = GetDatabase(typeId);
            return AddRecord(db, null, GetKey(objectId), payload, DbFile.WriteFlags.None);
                //txn.Commit(Txn.CommitMode.None);
            //}

            //    UpdateMemoryCounters();
        }

        /// <summary>
        /// This method will run all clean up and serialization routines
        /// </summary>
        public void Shutdown()
		{
            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: Shutting down ..."
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }

            using (stateLock.WaitToWrite())
            {
                badStates.Add(allTypes);
            }
            SaveEnvironmentData();
            using (envLock.WaitToRead())
            {
                CloseAllDatabases();
            }
            using (envLock.WaitToWrite())
            {
                databases.Clear();
            }
            if (this.env != null)
            {
                this.env.Close();
                this.env.Dispose();
            }

            if (Log.IsInfoEnabled)
            {
                Log.Write(Log.Status.Info, "[{0}] {1}: Shutdown Complete."
                    , Thread.CurrentThread.ManagedThreadId, logPrefix);
            }
		}
		#endregion

		#region Timer Elapsed Events

        //void configUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    long ei = settings.ExpirationInterval;
        //    decimal dao = settings.DiskAgeOut;

        //    if (fileKillerTimer.Interval != ei)
        //        fileKillerTimer.Interval = ei;

        //    if (memoryDirectory.AgeOut != dao)
        //        memoryDirectory.AgeOut = dao;
        //}

		#endregion

	}
}
