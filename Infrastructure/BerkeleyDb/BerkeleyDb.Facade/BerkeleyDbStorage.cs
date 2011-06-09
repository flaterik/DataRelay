using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;

using BerkeleyDbWrapper;
using MySpace.BerkeleyDb.Configuration;
using MySpace.Logging;
using MySpace.ResourcePool;
using MySpace.Common.HelperObjects;


namespace MySpace.BerkeleyDb.Facade
{
	public delegate void DatabaseEntryMapper(DatabaseEntry databaseEntry);

	public enum FederatedDatabaseSelectionStrategy
	{
		Sequential = 0,
		RoundRobin = 1
	}

	/// <summary>
	/// The status of a <see cref="BerkeleyDbStorage"/> instance.
	/// </summary>
	public enum BerkeleyDbStatus
	{
		/// <summary>
		/// Instance not running.
		/// </summary>
		NotRunning = 0,
		/// <summary>
		/// Instance running but not responding to operations.
		/// </summary>
		Offline = 1,
		/// <summary>
		/// Instance running.
		/// </summary>
		Online = 2,
	}

    [Serializable]
	public class RecoveryFailedException : ApplicationException
	{
		public RecoveryFailedException(Exception exc) : base("Recovery failed", exc) { }
	}

	/// <summary>
	/// The main class for BerkeleyDb.
	/// </summary>
	public partial class BerkeleyDbStorage : MarshalByRefObject, IEnumerable, IDisposable
	{
		public override object InitializeLifetimeService()
		{
			return null;
		}

		internal static readonly LogWrapper Log = new LogWrapper();

		private const short allTypes = 0;
		private const short adminDbKey = -1; //used for config access
		private const string shutdownTimeKey = "ShutdownTime";
		private const string dataVersion = "DataVersion";

		// if there are changes in data that require a complete 
		// database rebuild (ie flush all of the data)
		// change the version below.
		private const string Version = "1.0";

		private BerkeleyDbConfig bdbConfig;
		private EnvironmentConfig envConfig;
		//private DbHash adminDb;
		//private IDictionary<short, Database> databases;
		private Database[,] databases;
		private int[] databaseFederationSizes;
		private object[,] databaseCreationLocks;

		private BerkeleyDbWrapper.Environment env ;
		private BackupSet backupSet;
		
		private readonly MemoryStreamPool memoryPoolStream;
		private short minTypeId;
		private short maxTypeId = 50;
		private ConfigurableCallbackTimer trickleTimer;
		private ConfigurableCallbackTimer checkpointTimer;
		private ConfigurableCallbackTimer deadlockDetectTimer;
		private ConfigurableCallbackTimer dbStatTimer;
		private ConfigurableCallbackTimer dbLockStatCounterTimer;
		private ConfigurableCallbackTimer dbCompactTimer;
		private const int maxDbEntryReuse = 5;
		private readonly ResourcePool<DatabaseEntry> dbEntryPool;
		private const int initialBufferSize = 1048;
	    private const double  expandBufferSizeMultiplier = 1.5;
		private int bufferSize = initialBufferSize;
		private bool isShuttingDown;
		private DateTime lastCompactTime = DateTime.MinValue;
		private int _status = (int) BerkeleyDbStatus.NotRunning;


		public BerkeleyDbStorage()
		{
			memoryPoolStream = new MemoryStreamPool(bufferSize);
			dbEntryPool = new ResourcePool<DatabaseEntry>(CreatedDatabaseEntry, ResetDatabaseEntry, maxDbEntryReuse);
		}

		#region Private Methods
		private bool AddRecord(Database db, byte[] key, byte[] data)
		{
			try
			{
				DatabaseType dbType = db.GetDatabaseType();
				switch (dbType)
				{
					case DatabaseType.Queue:
						throw new ApplicationException("Database type " + dbType + " cannot have binary key");
					default:
						db.Put(key, data);
						break;
				}
				return true;
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("AddRecord() Error Adding record", ex);
				}
				throw;
			}
			return false;
		}

		private bool AddRecord(Database db, int key, byte[] data)
		{
			ResourcePoolItem<DatabaseEntry> pooledDbEntry = null;
			try
			{
				switch (db.GetDatabaseType())
				{
					case DatabaseType.Queue:
						pooledDbEntry = dbEntryPool.GetItem();
						DatabaseEntry newData = pooledDbEntry.Item;
						const int headerLen = sizeof(int);
						int recLen = db.GetRecordLength();
						int dataLen = 0;
						int totalLen = headerLen + dataLen;
						if (data != null && data.Length > 0)
						{
							dataLen = data.Length;
							totalLen += dataLen;
							Array.Copy(data, 0, newData.Buffer, headerLen, dataLen);
						}
						if (recLen < totalLen)
						{
							throw new ApplicationException("Payload length '" + dataLen + "' exceeds database record length '" + recLen + "'");
						}
						newData.Length = totalLen;
						if (newData.Buffer.Length < recLen)
						{
						    bufferSize = GetExpandedBufferSize(recLen);
                            newData.Resize(bufferSize);							
						}
						Serialize(dataLen, newData.Buffer);
						db.Put(key, newData);
						break;
					default:
						db.Put(key, data);
						break;
				}
				return true;
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("AddRecord() Error Adding record", ex);
				}
				throw;
			}
			finally
			{
				if (dbEntryPool != null && pooledDbEntry != null)
				{
					dbEntryPool.ReleaseItem(pooledDbEntry);
				}
			}
			return false;
		}

		private bool AddRecord(Database db, int objectId, byte[] key, int startPosition, int length, RMWDelegate rmwDelegate)
		{
			ResourcePoolItem<DatabaseEntry> pooledDbEntry = null;
			try
			{
				pooledDbEntry = dbEntryPool.GetItem();
				DatabaseEntry dbEntry = pooledDbEntry.Item;
				dbEntry.StartPosition = startPosition;
				dbEntry.Length = length;

				DatabaseType dbType = db.GetDatabaseType();
				switch (dbType)
				{
					case DatabaseType.Queue:
						throw new ApplicationException("Database type " + dbType + " cannot have binary key");
					default:
						db.Put(objectId, key, dbEntry, rmwDelegate);
						break;
				}
				return true;
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("AddRecord() Error Adding record", ex);
				}
				throw;
			}
			finally
			{
				if (dbEntryPool != null && pooledDbEntry != null)
				{
					dbEntryPool.ReleaseItem(pooledDbEntry);
				}
			}
			return false;
		}

		private static void CloseAllDatabases(Database[,] databasesToClose,
			object[,] databaseLocks)
		{
			if (Log.IsInfoEnabled)
			{
				Log.InfoFormat("CloseAllDatabases() Closing All databases ...");
			}
			try
			{
				if (databasesToClose == null)
				{
					if (Log.IsInfoEnabled)
					{
						Log.InfoFormat("CloseAllDatabases() No databases to be closed.");
					}
					return;
				}
				//foreach (Database db in databases.Values)
				Database db;
				for (short typeIndex = 0; typeIndex < databasesToClose.GetLength(0); typeIndex++)
				{
					for (int federationIndex = 0; federationIndex < databasesToClose.GetLength(1); federationIndex++)
					{
						if (databasesToClose[typeIndex, federationIndex] != null)
						{
							lock (databaseLocks[typeIndex, federationIndex])
							{
								db = databasesToClose[typeIndex, federationIndex];
								if (db != null)
								{
									databasesToClose[typeIndex, federationIndex] = null;
									try
									{
										db.Sync();
									}
									catch (Exception ex)
									{
										Log.Error(string.Format("CloseAllDatabases() got error syncing db[{0},{1}]", typeIndex, federationIndex), ex);
									}
									try
									{
										db.Dispose();
									}
									catch (Exception ex)
									{
										Log.Error(string.Format("CloseAllDatabases() got error disposing db[{0},{1}]", typeIndex, federationIndex), ex);

									}
								}
							}
						}
					}
				}
				if (Log.IsInfoEnabled)
				{
					Log.Info("CloseAllDatabases() Closing All databases complete");
				}
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("CloseAllDatabases() fails closing databases", ex);
				}
				throw;
			}
		}

		private Database CreateDatabase(BerkeleyDbWrapper.Environment environment, int typeId, int objectId)
		{
			DatabaseConfig dbConfig = GetDatabaseConfig(typeId, objectId);
			return CreateDatabase(environment, dbConfig);
		}

		private Database CreateDatabase(BerkeleyDbWrapper.Environment environment, DatabaseConfig dbConfig)
		{
			Database db = null;
			string fileName = dbConfig.FileName;

			if (fileName == null)
			{
				Log.InfoFormat("Database with id {0} is in memory only. Changes will not persist.", dbConfig.Id);
			}
			if (Log.IsDebugEnabled)
			{
				if (fileName != null)
				{
					Log.DebugFormat("CreateDatabase() for the file {0} and id {1}", fileName, dbConfig.Id);
				}
				else
				{
					Log.DebugFormat("CreateDatabase() for in memory database with id {0}", dbConfig.Id);
				}
				Log.DebugFormat("CreateDatabase() DbConfig: Type = {0}", dbConfig.Type);
				Log.DebugFormat("CreateDatabase() DbConfig: FileName = {0}", dbConfig.FileName);
				Log.DebugFormat("CreateDatabase() DbConfig: OpenFlags = {0}", dbConfig.OpenFlags);
				Log.DebugFormat("CreateDatabase() DbConfig: Flags = {0}", dbConfig.Flags);
				Log.DebugFormat("CreateDatabase() DbConfig: PageSize = {0}", dbConfig.PageSize);
				Log.DebugFormat("CreateDatabase() DbConfig: HashFillFactor = {0}", dbConfig.HashFillFactor);
				Log.DebugFormat("CreateDatabase() DbConfig: HashSize = {0}", dbConfig.HashSize);
				Log.DebugFormat("CreateDatabase() DbConfig: RecordLength = {0}", dbConfig.RecordLength);
				Log.DebugFormat("CreateDatabase() DbConfig: TransactionMode = {0}", dbConfig.TransactionMode);
			}
			try
			{
				
				string dir = bdbConfig.EnvironmentConfig.HomeDirectory;
				if (dbConfig.FileName != null)
				{
					CreateDirectory(dir);
				}

				if (environment != null)
				{
					db = environment.OpenDatabase(dbConfig);
				}
				else
				{
					dbConfig.HomeDirectory = dir;
					db = Database.Create(dbConfig);
				}

				if (db != null)
				{
					GC.SuppressFinalize(db);
					if (Log.IsDebugEnabled)
					{
							if (Log.IsDebugEnabled)
					{
								if (fileName != null)
						{
									Log.InfoFormat("CreateDatabase() DB for the file {0} is opened", fileName);
						}
								else
						{
									Log.InfoFormat("CreateDatabase() DB for in memory database is opened.");
								}
							}
							Log.DebugFormat("CreateDatabase() ##BEGIN CREATED DB INFO############################################");
							Log.DebugFormat("CreateDatabase() Db: Type = {0}", db.GetDatabaseType());
							Log.DebugFormat("CreateDatabase() Db: DbFlags = {0}", db.GetFlags());
							Log.DebugFormat("CreateDatabase() Db: OpenFlags = {0}", db.GetOpenFlags());
							Log.DebugFormat("CreateDatabase() Db: ErrorPrefix = {0}", db.GetErrorPrefix());
							Log.DebugFormat("CreateDatabase() Db: PageSize = {0}", db.GetPageSize());
							Log.DebugFormat("CreateDatabase() Db: RecordLength = {0} (For Queue only)", db.GetRecordLength());
							Log.DebugFormat("CreateDatabase() Db: HashFillFactor = {0}", db.GetHashFillFactor());
							db.PrintStats(DbStatFlags.StatAll);
							Log.DebugFormat("CreateDatabase() ##END DB STAT##############################################");
						}
					}
				return db;
			}
			catch (BdbException ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.ErrorFormat("CreateDatabase() Got BdbException");					
				}
				HandleBdbError(ex);
				if (db != null)
				{
					db.Dispose();
				}
				//RemoveDbFiles(dbConfig); //Why would we remove the files, this seems like not a good idea.
				throw;
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error(string.Format("CreateDatabase() Error Creating Database for the file {0}", fileName), ex);
				}
				if (db != null)
				{
					db.Dispose();
				}
				throw;
			}
		}

		private DatabaseEntry CreatedDatabaseEntry()
		{
			return new DatabaseEntry(bufferSize);
		}

		private static void CreateDirectory(string dir)
		{
			if (!Directory.Exists(dir))
			{
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("CreateDirectory() Creates directory {0}.", dir);
				}
				Directory.CreateDirectory(dir);
			}
		}

		private BerkeleyDbWrapper.Environment CreateEnvironment(EnvironmentConfig envConfigToCreate)
		{
			try
			{
				CreateDirectory(envConfigToCreate.TempDirectory);
				if (!((envConfigToCreate.OpenFlags & EnvOpenFlags.Private) == EnvOpenFlags.Private))
				{
					//don't need to create this folder if private is specified because it won't create files if it's not.
					CreateDirectory(envConfigToCreate.HomeDirectory);
				}
				var environment = BerkeleyDbWrapper.Environment.Create(envConfigToCreate);
				GC.SuppressFinalize(environment);

				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("CreateEnvironment() Environment is created");
				}

				environment.PanicCall += PanicCall;
				environment.MessageCall += MessageCall;

				SetEnvironmentConfiguration(environment, envConfigToCreate);
				return environment;
			}
			catch (BdbException ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("CreateEnvironment() BdbException Initializing Environment:", ex);
				}
				throw;
			}
			catch (Exception ex)
			{
				if (!(ex is BdbException) && Log.IsErrorEnabled)
				{
					Log.Error("CreateEnvironment() Error Initializing Environment:", ex);
				}
				throw;
			}
		}

		private void SetLockCounters(BerkeleyDbWrapper.Environment envToSet)
		{
			envToSet.LockStatCurrentMaxLockerId = LockStatCurrentMaxLockerId;
			envToSet.LockStatLastLockerId = LockStatLastLockerId;
			envToSet.LockStatLockersNoWait = LockStatLockersNoWait;
			envToSet.LockStatLockersWait = LockStatLockersWait;
			envToSet.LockStatLockNoWait = LockStatLockNoWait;
			envToSet.LockStatLocksWait = LockStatLocksWait;
			envToSet.LockStatLockTimeout = LockStatLockTimeout;
			envToSet.LockStatLockWait = LockStatLockWait;
			envToSet.LockStatMaxLockersPossible = LockStatMaxLockersPossible;
			envToSet.LockStatMaxLockObjectsPossible = LockStatMaxLockObjectsPossible;
			envToSet.LockStatMaxLocksPossible = LockStatMaxLocksPossible;
			envToSet.LockStatMaxNumberLockersAtOneTime = LockStatMaxNumberLockersAtOneTime;
			envToSet.LockStatMaxNumberLocksAtOneTime = LockStatMaxNumberLocksAtOneTime;
			envToSet.LockStatNumberCurrentLockers = LockStatNumberCurrentLockers;
			envToSet.LockStatNumberCurrentLockObjects = LockStatNumberCurrentLockObjects;
			envToSet.LockStatNumberCurrentLockObjectsAtOneTime = LockStatNumberCurrentLockObjectsAtOneTime;
			envToSet.LockStatNumberCurrentLocks = LockStatNumberCurrentLocks;
			envToSet.LockStatNumberDeadLocks = LockStatNumberDeadLocks;
			envToSet.LockStatNumberLockModes = LockStatNumberLockModes;
			envToSet.LockStatNumberLocksDownGraded = LockStatNumberLocksDownGraded;
			envToSet.LockStatNumberLocksReleased = LockStatNumberLocksReleased;
			envToSet.LockStatNumberLocksRequested = LockStatNumberLocksRequested;
			envToSet.LockStatNumberLocksUpgraded = LockStatNumberLocksUpgraded;
			envToSet.LockStatNumberLockTimeouts = LockStatNumberLockTimeouts;
			envToSet.LockStatNumberTxnTimeouts = LockStatNumberTxnTimeouts;
			envToSet.LockStatObjectsNoWait = LockStatObjectsNoWait;
			envToSet.LockStatObjectsWait = LockStatObjectsWait;
			envToSet.LockStatTxnTimeout = LockStatTxnTimeout;
			envToSet.LockStatLockHashLen = LockStatLockHashLen;
			envToSet.LockStatLockRegionSize = LockStatLockRegionSize;
			envToSet.LockStatLocksNoWait = LockStatLocksNoWait;
			envToSet.LockStatRegionNoWait = LockStatRegionNoWait;
			envToSet.LockStatRegionWait = LockStatRegionWait;

			if (Log.IsInfoEnabled)
			{
				if (envToSet.LockStatRegionWait == null)
				{
					Log.WarnFormat("Lock Statistics not initialized properly from BerkeleyDbStorage.");
				}
				else
				{
					Log.DebugFormat("Lock Statistics initialized properly from BerkeleyDbStorage.");
				}
			}
		}

		private bool DeleteRecord(Database db, int key)
		{
			try
			{
				DbRetVal dbRetVal = db.Delete(key);
				return dbRetVal == DbRetVal.SUCCESS;
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("DeleteRecord() Error Deleting record", ex);
				}
				throw;
			}
			return false;
		}

		private bool DeleteRecord(Database db, byte[] key)
		{
			try
			{
				DatabaseType dbType = db.GetDatabaseType();
				if (dbType != DatabaseType.BTree && dbType != DatabaseType.Hash)
				{
					throw new ApplicationException("Database type " + dbType + " cannot have binary key");
				}

				DbRetVal dbRetVal = db.Delete(key);
				return dbRetVal == DbRetVal.SUCCESS;
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("DeleteRecord() Error Deleting record", ex);
				}
				throw;
			}
			return false;
		}

		private void DeadlockDetect()
		{
			DeadlockDetection deadlockDetection = envConfig.DeadlockDetection;
			if (deadlockDetection != null && deadlockDetection.Enabled
				&& deadlockDetection.Mode == DeadlockDetectionMode.OnTimer)
			{
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("DeadlockDetect() Deadlock Detection started ...");
				}
				int abortedLocks = env.LockDetect(deadlockDetection.DetectPolicy);
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("DeadlockDetect() Deadlock Detection is complete. {0} locks aborted.", abortedLocks);
				}
			}
		}

		private static int DeserializeToInt(byte[] bytes, int startIndex)
		{
			return bytes[startIndex]
				| bytes[startIndex + 1] << 8
				| bytes[startIndex + 2] << 16
				   | bytes[startIndex + 3] << 24;
		}

		private void DbStatPrint()
		{
			StatTimer statTimer = bdbConfig.StatTimer;
			if (statTimer != null && statTimer.Enabled)
			{
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("DbStatPrint() started ...");
				}
				
				SetStoredObjects(GetKeyCount(databases, statTimer.StatFlag));

				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("DbStatPrint() is complete.");
				}
			}
		}

		/// <summary>
		/// Gets the federation size for a type.
		/// </summary>
		/// <param name="typeId">The <see cref="Int32"/> id of the type.</param>
		/// <returns>The <see cref="Int32"/> federation size.</returns>
		internal int GetFederationSize(int typeId)
		{
			return databaseFederationSizes[typeId - minTypeId];
		}

		internal Database GetDatabase(int typeId, int objectId)
		{
			DatabaseConfig dbConfig = null;
			if (typeId < minTypeId || typeId > maxTypeId)
			{
				string error =
					string.Format("Database ID {0} cannot be outside of the configured TypeId range: {1} - {2}",
					typeId, minTypeId, maxTypeId);
				if (Log.IsErrorEnabled)
				{
					Log.ErrorFormat(error);
				}
				throw new ApplicationException(error);
			}
			int typeIndex = typeId - minTypeId;
			int federationSize = databaseFederationSizes[typeIndex];
			if (federationSize <= 0)
			{
				dbConfig = GetDatabaseConfig(typeId, objectId);
				federationSize = dbConfig.FederationSize;
				if (federationSize <= 0) federationSize = 1;
				databaseFederationSizes[typeIndex] = federationSize;
			}
            
            int federationIndex = DatabaseConfig.CalculateFederationIndex(objectId, federationSize);
            
			Database database = databases[typeIndex, federationIndex];

			if (database == null)
			{
				lock (databaseCreationLocks[typeIndex, federationIndex])
				{
					database = databases[typeIndex, federationIndex];
					if (database == null)
					{
						if (dbConfig == null)
						{
							dbConfig = GetDatabaseConfig(typeId, objectId);
						}
						
						if (Log.IsInfoEnabled)
						{
							Log.InfoFormat("GetDatabase() no database [{0},{1}] found for typeId {2}, creating."
								, typeIndex, federationIndex, dbConfig.Id);
						}
						
						try
						{
							database = CreateDatabase(env, dbConfig);
						}
						catch (BdbException ex)
						{
							if (Log.IsErrorEnabled)
							{
								Log.ErrorFormat("GetDatabase() error creating database [{0},{1}] for typeId {2}. Trying one more time."
									, typeIndex, federationIndex, dbConfig.Id);
							}
							HandleBdbError(ex);
							//Let's try one more time, since db files should have been removed.
							database = CreateDatabase(env, dbConfig);
							if (Log.IsErrorEnabled)
							{
								Log.ErrorFormat("GetDatabase() this time database [{0},{1}] for typeId {2} was created."
									, typeIndex, federationIndex, dbConfig.Id);
							}
						}
						databases[typeIndex, federationIndex] = database;
					}
				}
			}
			return database;
		}

		private DatabaseConfig GetDatabaseConfig(int typeId, int objectId)
		{
			return envConfig.DatabaseConfigs.GetConfigFor(typeId, objectId);
		}

		private static int GetMaxFederationSize(EnvironmentConfig envConfig)
		{
			int maxFederationSize = 0;
			DatabaseConfigs databaseConfigs = envConfig.DatabaseConfigs;
			if (databaseConfigs.Count > 0)
			{
			foreach (DatabaseConfig dbConfig in databaseConfigs)
			{
					int federationSize = dbConfig.FederationSize;
				if (federationSize > maxFederationSize)
				{
					maxFederationSize = federationSize;
				}
			}
			return maxFederationSize;
		}

			return new DatabaseConfig().FederationSize;
		}

		private byte[] GetRecord(Database db, int key, byte[] buffer, MemoryStream dataStream)
		{
			byte[] bytes;
			try
			{
				bytes = db.Get(key, buffer);
				if (bytes != null)
				{
					switch (db.GetDatabaseType())
					{
						case DatabaseType.Queue:
							const int intLen = 4;
							Array.Resize(ref bytes, intLen);
							dataStream.Read(bytes, 0, intLen);
							//int len = BitConverter.ToInt32(bytes, 0);
							int len = (bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24);
							if (len == 0)
							{
								return null;
							}
							Array.Resize(ref bytes, len);
							dataStream.Read(bytes, 0, len);
							break;
					}
				}
			}
			catch (BufferSmallException ex)
			{
				uint recLen = ex.RecordLength;
				if (Log.IsWarnEnabled)
				{
					Log.WarnFormat("GetRecord() {0}. Increazing buffer up to {1} bytes.", ex.Message, recLen);
				}

				if (buffer.Length < recLen)
				{
                    bufferSize = GetExpandedBufferSize((int)recLen);
                    dataStream.SetLength(bufferSize);
					buffer = dataStream.GetBuffer();
					memoryPoolStream.InitialBufferSize = bufferSize;
					SetPooledBufferSize(bufferSize);
				}
				bytes = db.Get(key, buffer);
			}
			return bytes;
		}

		private byte[] GetRecord(Database db, int key)
		{
			ResourcePoolItem<MemoryStream> pooledStream = null;
			try
			{
				pooledStream = memoryPoolStream.GetItem();
				MemoryStream dataStream = pooledStream.Item;
				dataStream.SetLength(dataStream.Capacity);
				byte[] buffer = dataStream.GetBuffer();
				byte[] data = GetRecord(db, key, buffer, dataStream);
				return data;
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("GetRecord() Error Adding record", ex);
				}
				throw;
			}
			finally
			{
				if (memoryPoolStream != null && pooledStream != null)
				{
					memoryPoolStream.ReleaseItem(pooledStream);
				}
			}
			return null;
		}

		private DatabaseEntry GetValue(Database db, int key, DatabaseEntry value)
		{
			DatabaseEntry retValue;
			try
			{
				retValue = db.Get(key, value);
			}
			catch (BufferSmallException ex)
			{
				uint recLen = ex.RecordLength;
				int bufferLen = value.Buffer.Length;
				
				Log.InfoFormat("GetValue() {0}. Increasing buffer from {1} to {2} bytes.", ex.Message, bufferLen, recLen);

				if (bufferLen < recLen)
				{
                    bufferSize = GetExpandedBufferSize((int)recLen);					
					value.Resize(bufferSize);
					SetPooledBufferSize(bufferSize);
				}
				retValue = db.Get(key, value);
			}

			return retValue;
		}

		private DatabaseEntry GetValue(Database db, byte[] key, DatabaseEntry value)
		{
			DatabaseEntry retValue;
			try
			{
				retValue = db.Get(key, value);
			}
			catch (BufferSmallException ex)
			{
				uint recLen = ex.RecordLength;
				int bufferLen = value.Buffer.Length;
				
				Log.InfoFormat("GetValue() {0}. Increasing buffer from {1} to {2} bytes.", ex.Message, bufferLen, recLen);

				if (bufferLen < recLen)
				{
				    bufferSize = GetExpandedBufferSize((int)recLen);
					value.Resize(bufferSize);
					SetPooledBufferSize(bufferSize);
				}
				retValue = db.Get(key, value);
			}

			return retValue;
		}

		private void GetValue(Database db, int key, DatabaseEntryMapper databaseEntryMapper)
		{
			ResourcePoolItem<DatabaseEntry> pooledDbEntry = null;
			try
			{
				pooledDbEntry = dbEntryPool.GetItem();
				DatabaseEntry data = GetValue(db, key, pooledDbEntry.Item);
				if (data != null && data.Length > 0)
				{
					switch (db.GetDatabaseType())
					{
						case DatabaseType.Queue:
							int len = DeserializeToInt(data.Buffer, 0);
							data.StartPosition = 4;
							data.Length = len;
							break;
					}
					databaseEntryMapper(data);
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
					Log.Error("GetValue() Error Adding record", ex);
				}
				throw;
			}
			finally
			{
				if (pooledDbEntry != null)
				{
					pooledDbEntry.Release();
				}
			}
		}

		private void GetValue(Database db, byte[] key, DatabaseEntryMapper databaseEntryMapper)
		{
			ResourcePoolItem<DatabaseEntry> pooledDbEntry = null;
			try
			{
				pooledDbEntry = dbEntryPool.GetItem();
				DatabaseEntry data = GetValue(db, key, pooledDbEntry.Item);
				if (data != null && data.Length > 0)
				{
					DatabaseType dbType = db.GetDatabaseType();
					switch (dbType)
					{
						case DatabaseType.Queue:
							throw new ApplicationException("Database type " + dbType + " cannot have binary key");
						//int len = DeserializeToInt(data.Buffer, 0);
						//data.StartPosition = 4;
						//data.Length = len;
						//break;
					}
					databaseEntryMapper(data);
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
					Log.Error("GetValue() Error Adding record", ex);
				}
				throw;
			}
			finally
			{
				if (pooledDbEntry != null)
				{
					pooledDbEntry.Release();
				}
			}
		}

		#region Recovery locking
		readonly object recoveryLock = new object();

		bool TryStartRecoveryRegion()
		{
			return Monitor.TryEnter(recoveryLock);
		}

		void EndRecoveryRegion()
		{
			Monitor.Exit(recoveryLock);
		}

		public void BlockOnRecovery()
		{
			try
			{
				Monitor.Enter(recoveryLock);
			}
			finally
			{
				Monitor.Exit(recoveryLock);
			}
		}
		#endregion

		static readonly Regex reNumeric = new Regex("[0-9]*$", RegexOptions.Compiled);

		bool AttemptRecovery(bool removeEnvironment, bool allowPartialDatabaseRecovery, bool verify, out bool enviornmentCreated)
		{
			enviornmentCreated = false;
			const EnvOpenFlags removalMask = EnvOpenFlags.Recover;
			// Close all the existing handles
			CloseAllHandles();
			string envHomeDir = bdbConfig.EnvironmentConfig.HomeDirectory;
			while (true)
			{
				EnvOpenFlags existingFlags = 0;
				// clear out old environment files
				if (removeEnvironment)
				{
					if (Log.IsInfoEnabled)
					{
						Log.InfoFormat("AttemptRecovery() Removal of old environment files starting");
					}
					existingFlags = bdbConfig.EnvironmentConfig.OpenFlags & removalMask;
					bdbConfig.EnvironmentConfig.OpenFlags &= ~existingFlags;
// ReSharper disable RedundantNameQualifier
					BerkeleyDbWrapper.Environment.Remove(envHomeDir, bdbConfig.EnvironmentConfig.OpenFlags, true);
// ReSharper restore RedundantNameQualifier
					if (Log.IsInfoEnabled)
					{
						Log.InfoFormat("AttemptRecovery() Removal of old environment files completed");
					}
				}
				try
				{
					// attempt environment recovery
					RecreateEnv(bdbConfig.EnvironmentConfig);
					enviornmentCreated = true;
				}
				catch(BdbException e)
				{
					if (e.Message.Contains("DB_RUNRECOVERY")) //the environment is not openable 
					{
						throw;
					}
					if (!removeEnvironment)
					{
						// if error on old environment, remove the environment and try again
						removeEnvironment = true;
						continue;
					}
				}
				catch
				{
					if (!removeEnvironment)
					{
						// if error on old environment, remove the environment and try again
						removeEnvironment = true;
						continue;
					}
					else
					{
						throw;
					}
				}
				finally
				{
					bdbConfig.EnvironmentConfig.OpenFlags |= existingFlags;
				}
				break;
			}
			// clear out any pending transactions to avoid locks and help get db file back to consistent state
			if (!removeEnvironment &&
				(bdbConfig.EnvironmentConfig.OpenFlags & EnvOpenFlags.InitTxn) == EnvOpenFlags.InitTxn)
			{
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("AttemptRecovery() Cancelling pending transactions");
				}
				env.CancelPendingTransactions();
			}
			if (verify)
			{
				// verify all outstanding db files
				foreach (DatabaseConfig dbConfig in bdbConfig.EnvironmentConfig.DatabaseConfigs)
				{
					string homeDir = dbConfig.HomeDirectory;
					if (string.IsNullOrEmpty(homeDir)) homeDir = envHomeDir;
					string dbBaseFile = Path.Combine(homeDir, dbConfig.FileName);
					string folder = Path.GetDirectoryName(dbBaseFile);
					dbBaseFile = Path.GetFileName(dbBaseFile);
					Match mtch = reNumeric.Match(dbBaseFile);
					string baseRoot = dbBaseFile.Substring(0, mtch.Index);
					foreach (string dbFile in Directory.GetFiles(folder, baseRoot + "*"))
					{
						string numberExtension = Path.GetFileName(dbFile).Substring(baseRoot.Length);
						if (reNumeric.IsMatch(numberExtension))
						{
							DbRetVal ret = Database.Verify(dbFile);
							if (Log.IsInfoEnabled)
							{
								Log.InfoFormat("Verify on {0} returned {1}", dbFile, ret);
							}
							if (ret != DbRetVal.SUCCESS)
							{
								if (allowPartialDatabaseRecovery)
								{
									File.Delete(dbFile);
									if (Log.IsInfoEnabled)
									{
										Log.InfoFormat("Deleted {0}", dbFile);
									}
								}
								else
								{
									return false;
								}
							}
						}
					}
				}
			}
			return true;
		}


		static bool NeedsRecovery(BdbException ex)
		{
			return ex.Code == (int)DbRetVal.RUNRECOVERY;
		}

		[ThreadStatic]
		static bool threadInRecovery;

		public int HandleIteration { get; private set; }
		public bool IsInRecovery { get; private set; }

		void IncrementHandleIteration()
		{
			++HandleIteration;
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("IncrementHandleIteration(): HandleIteration = {0}", HandleIteration);
			}
		}

		enum RecoveryLevel
		{
			Simple,
			Catastrophic,
			ClearAll
		}
		void Recover(bool verify) { Recover(RecoveryLevel.Simple, verify); }
		void Recover(RecoveryLevel recoveryLevel, bool verify)
		{
			if (IsInRecovery || isShuttingDown)
			{
				return;
			}
			// Only first run recovery error thread will try to recover. Other threads will just wait
			if (TryStartRecoveryRegion())
			{
				var oldStatus = Status;
				if (oldStatus == BerkeleyDbStatus.Online)
				{
					Status = BerkeleyDbStatus.Offline;
				}
				IncrementHandleIteration();

				var databaseLockIdx = 0;
				var federationLockIdx = -1;
            	var isTransactional = (bdbConfig.EnvironmentConfig.OpenFlags &
					EnvOpenFlags.InitTxn) == EnvOpenFlags.InitTxn;

				bool environmentCreated = false;
				try
				{
					threadInRecovery = true;
					IsInRecovery = true;

					// iterate over database locks, keeping track what locks were gotten successfully
					while (databaseLockIdx < typeRangeSize)
					{
						for (var trialFedLockIdx = 0; trialFedLockIdx < maxFederationSize; ++trialFedLockIdx)
						{
							try
							{
								Monitor.Enter(databaseCreationLocks[databaseLockIdx, trialFedLockIdx]);
							}
							finally
							{
								federationLockIdx = trialFedLockIdx;
							}
						}
						try { }
						finally
						{
							++databaseLockIdx;
							federationLockIdx = -1;
						}
					}

					Exception lastException = null;

					// Try Simple Recovery first
					// Make sure database recreation will use existing files if present
					foreach (DatabaseConfig dbConfig in bdbConfig.EnvironmentConfig.DatabaseConfigs)
					{
						dbConfig.OpenFlags &= ~DbOpenFlags.Exclusive;
					}
					// Set environment flags to simple recovery only
					if (isTransactional)
						bdbConfig.EnvironmentConfig.OpenFlags |= EnvOpenFlags.Recover;
					else
						bdbConfig.EnvironmentConfig.OpenFlags &= ~EnvOpenFlags.Recover;
					bdbConfig.EnvironmentConfig.OpenFlags &= ~EnvOpenFlags.RecoverFatal;

					bool success = false;
					
					if (recoveryLevel == RecoveryLevel.Simple)
					{
						if (Log.IsInfoEnabled)
						{
							Log.InfoFormat("Recover() Starting simple recovery");
						}
						try
						{
							success = AttemptRecovery(false, false, verify, out environmentCreated);
						}
						catch (Exception ex)
						{
							//if we can't create the environment nothing else is going to work AND it's already been logged   
							if (!environmentCreated) throw;

							lastException = ex;
							success = false;
							if (Log.IsErrorEnabled)
							{
								Log.Error("Recover() Error in simple recovery", ex);
							}
							
						}
						if (Log.IsInfoEnabled)
						{
							Log.Info(success ?
								"Recover() Simple recovery completed"
								: "Recover() Simple recovery failed");
						}
						
						if (!success) recoveryLevel = RecoveryLevel.Catastrophic;
					}

					// if simple recovery fails and backups enabled, try catastrophic recovery
					if (!success)
					{
						if (recoveryLevel == RecoveryLevel.Catastrophic)
						{
							bool doRestore = bdbConfig.EnvironmentConfig.Checkpoint != null &&
								bdbConfig.EnvironmentConfig.Checkpoint.Enabled &&
								bdbConfig.EnvironmentConfig.Checkpoint.Backup != null &&
								bdbConfig.EnvironmentConfig.Checkpoint.Backup.Enabled;
							if (doRestore && backupSet == null)
							{
								backupSet = MakeBackupSet();
							}
							// Set environment flags to catastrophic recovery only
							if (isTransactional)
								bdbConfig.EnvironmentConfig.OpenFlags |= EnvOpenFlags.RecoverFatal;
							else
								bdbConfig.EnvironmentConfig.OpenFlags &= ~EnvOpenFlags.RecoverFatal;
							bdbConfig.EnvironmentConfig.OpenFlags &= ~EnvOpenFlags.Recover;
							if (Log.IsInfoEnabled)
							{
								Log.InfoFormat("Recover() Starting catastrophic recovery");
							}
							try
							{
								if (doRestore)
								{
									success = backupSet.Restore();
								}
								if (!doRestore || success)
								{
									success = AttemptRecovery(true, bdbConfig.AllowPartialDatabaseRecovery,
										doRestore || verify, out environmentCreated);
								}
							}
							catch (Exception ex)
							{
								lastException = ex;
								success = false;
								if (Log.IsErrorEnabled)
								{
									Log.Error("Recover() Error in catastrophic recovery", ex);
								}
							}
							if (Log.IsInfoEnabled)
							{
								Log.InfoFormat(success ?
									"Recover() Catastrophic recovery completed"
									: "Recover() Catastrophic recovery failed");
							}
							if (!success) recoveryLevel = RecoveryLevel.ClearAll;
						}
					}

					// if no recovery recreate environment from scratch
					if (!success)
					{
                        throw new RecoveryFailedException(lastException);
					}

					// post recovery
					// do normal preloading as needed
					if (bdbConfig.DbLoadMode == DbLoadMode.OnStartup)
					{
						PreLoadDatabasesOnDemand(bdbConfig, databases, databaseFederationSizes, minTypeId, maxTypeId);
					}
					Status = oldStatus;
				}
				catch (Exception ex)
				{

					if (environmentCreated//if the problem was creating the env, we already logged it
						&& Log.IsErrorEnabled )
					{
						Log.Error("CheckForRecovery() Error", ex);
					}
					throw;
				}
				finally
				{
					// release the locks that were taken
					for (; databaseLockIdx >= 0; --databaseLockIdx)
					{
						for (; federationLockIdx >= 0; --federationLockIdx)
						{
							try
							{
								Monitor.Exit(databaseCreationLocks[databaseLockIdx, federationLockIdx]);
							}
							catch(Exception e)
							{
								if(Log.IsErrorEnabled)
								{
									Log.ErrorFormat("Exception exiting database creation lock: {0}", e);

								}
							}
						}
						federationLockIdx = maxFederationSize - 1;
					}

					IsInRecovery = false;
					threadInRecovery = false;
					EndRecoveryRegion();
				}
			}
		}

		#region Record Enumeration By Type Id
		public int MinimumTypeId { get { return minTypeId; } }

		public int MaximumTypeId { get { return maxTypeId; } }

		IEnumerable<DatabaseRecord> GetRecordsCore(int typeId, FederatedDatabaseSelectionStrategy strategy)
		{
			var dbConfig = envConfig.DatabaseConfigs.GetConfigFor(typeId);
			Database db;
			var fedSize = dbConfig.FederationSize;
			if (fedSize < 2)
			{
				db = GetDatabase(typeId, 0);
				if (db != null)
				{
					foreach (var record in db)
					{
						yield return record;
					}
				}
			}
			else
			{
				switch (strategy)
				{
					case FederatedDatabaseSelectionStrategy.Sequential:
						for (var idx = 0; idx < fedSize; ++idx)
						{
							db = GetDatabase(typeId, idx);
							if (db != null)
							{
								foreach (var record in db)
								{
									yield return record;
								}
							}
						}
						break;
					case FederatedDatabaseSelectionStrategy.RoundRobin:
						var enumerators = new List<IEnumerator<DatabaseRecord>>();
						try
						{
							for (var idx = 0; idx < fedSize; ++idx)
							{
								db = GetDatabase(typeId, idx);
								if (db != null)
								{
									enumerators.Add(db.GetEnumerator());
								}
							}
							var enumLen = enumerators.Count;
							var enumIdx = 0;
							IEnumerator<DatabaseRecord> enumerator;
							while (enumLen > 1)
							{
								if (enumIdx == enumLen) enumIdx = 0;
								enumerator = enumerators[enumIdx];
								if (enumerator.MoveNext())
								{
									yield return enumerator.Current;
									++enumIdx;
								}
								else
								{
									enumerator.Dispose();
									enumerators.RemoveAt(enumIdx);
									--enumLen;
								}
							}
							if (enumLen == 1)
							{
								enumerator = enumerators[0];
								while (enumerator.MoveNext())
								{
									yield return enumerator.Current;
								}
								enumerator.Dispose();
								enumerators.RemoveAt(0);
							}
						}
						finally
						{
							// clean up after any possible exception
							for (var idx = enumerators.Count - 1; idx >= 0; --idx)
							{
								var enm = enumerators[idx];
								if (enm != null)
								{
									enm.Dispose();
								}
							}
						}
						break;
					default:
						throw new NotImplementedException("Not implemented for strategy " + strategy);
				}
			}
		}
		public IEnumerable<DatabaseRecord> GetRecords(int typeId, FederatedDatabaseSelectionStrategy strategy)
		{
			return new EnumerableWrapper<DatabaseRecord>(this, GetRecordsCore(typeId, strategy));
		}
		public IEnumerable<DatabaseRecord> GetRecords(int typeId)
		{
			return GetRecords(typeId, FederatedDatabaseSelectionStrategy.Sequential);
		}

		/// <summary>
		/// A wrapper of <see cref="IEnumerable{T}"/> that ends prematurely if
		/// the master <see cref="BerkeleyDbStorage"/> cycles.
		/// </summary>
		/// <typeparam name="T">The type of object enumerated.</typeparam>
		internal sealed class EnumerableWrapper<T> : IEnumerable<T>
		{
			private readonly BerkeleyDbStorage storage;
			private readonly IEnumerable<T> src;

			/// <summary>
			/// 	<para>Initializes a new instance of the <see cref="EnumerableWrapper"/> class.</para>
			/// </summary>
			/// <param name="storage">
			/// 	<para>The <see cref="BerkeleyDbStorage"/> whose lifecycle
			///		is tracked.</para>
			/// </param>
			/// <param name="src">
			/// 	<para>The <see cref="IEnumerable{T}"/> to be wrapped.</para>
			/// </param>
			public EnumerableWrapper(BerkeleyDbStorage storage, IEnumerable<T> src)
			{
				this.storage = storage;
				this.src = src;
			}

			IEnumerator<T> IEnumerable<T>.GetEnumerator()
			{
				return new EnumeratorWrapper(this);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return new EnumeratorWrapper(this);
			}

			sealed class EnumeratorWrapper : IEnumerator<T>
			{
				private readonly EnumerableWrapper<T> parent;
				private IEnumerator<T> enm;
				private int startingHandleIteration = -1;

				public EnumeratorWrapper(EnumerableWrapper<T> parent)
				{
					this.parent = parent;
					try
					{
						enm = parent.src.GetEnumerator();
						startingHandleIteration = parent.storage.HandleIteration;
					}
					catch (BdbException ex)
					{
						parent.storage.HandleBdbError(ex);
					}
					catch (Exception ex)
					{
						if (Log.IsErrorEnabled)
						{
							Log.Error("EnumeratorWrapper() Error Initiating", ex);
						}
						throw;
					}
				}

				void CheckHandleIteration()
				{
					if (startingHandleIteration < 0 ||
						parent.storage.HandleIteration != startingHandleIteration)
					{
						startingHandleIteration = -1;
						throw new ApplicationException(
							"Bdb storage has started re-initialization since the enumeration started");
					}
				}

				public T Current
				{
					get
					{
						if (enm == null) throw new InvalidOperationException();
						CheckHandleIteration();
						try
						{
							return enm.Current;
						}
						catch (BdbException ex)
						{
							parent.storage.HandleBdbError(ex);
							Dispose();
							throw new InvalidOperationException();
						}
						catch (InvalidOperationException)
						{
							throw;
						}
						catch (Exception ex)
						{
							if (Log.IsErrorEnabled)
							{
								Log.Error("EnumeratorWrapper() Error Getting Current", ex);
							}
							throw;
						}
					}
				}

				public void Dispose()
				{
					if (enm != null)
					{
						enm.Dispose();
						enm = null;
					}
					startingHandleIteration = -1;
				}

				object IEnumerator.Current
				{
					get { return Current; }
				}

				public bool MoveNext()
				{
					if (enm == null) return false;
					CheckHandleIteration();
					try
					{
						return enm.MoveNext();
					}
					catch (BdbException ex)
					{
						parent.storage.HandleBdbError(ex);
						Dispose();
						return false;
					}
					catch (Exception ex)
					{
						if (Log.IsErrorEnabled)
						{
							Log.Error("EnumeratorWrapper() Error Moving Next", ex);
						}
						throw;
					}
				}

				public void Reset()
				{
					if (enm == null) return;
					CheckHandleIteration();
					try
					{
						enm.Reset();
					}
					catch (BdbException ex)
					{
						parent.storage.HandleBdbError(ex);
						Dispose();
						return;
					}
					catch (Exception ex)
					{
						if (Log.IsErrorEnabled)
						{
							Log.Error("EnumeratorWrapper() Error Resetting", ex);
						}
						throw;
					}
				}
			}
		}
		#endregion

		internal void HandleGeneralError(Exception exc)
		{
			if (threadInRecovery)
			{
				return;
			}
			if (Log.IsErrorEnabled)
			{
				Log.Error("HandleGeneralError() trying to handle Exception", exc);
			}
			Recover(RecoveryLevel.Catastrophic, true);
		}

		internal void HandleBdbError(BdbException exc)
		{
			if (!exc.Handled)
			{
				try
				{
					if (Log.IsErrorEnabled)
					{
						Log.Error("HandleBdbError() trying to handle BdbException", exc);
					}
					if (NeedsRecovery(exc))
					{
						Recover(true);
					}
				}
				finally
				{
					exc.Handled = true;
				}
			}
		}

		internal void HandleBdbError(BdbException exc, Database db)
		{
			if (db == null)
			{
				HandleBdbError(exc);
				return;
			}
			if (!exc.Handled)
			{
				try
				{
					if (Log.IsErrorEnabled)
					{
						Log.Error(string.Format("HandleBdbError() trying to handle BdbException from db {0}",
							db.Id), exc);						
					}
					if (NeedsRecovery(exc))
					{
						Recover(true);
					}
				}
				finally
				{
					exc.Handled = true;
				}
			}
		}

		public Database GetAdminDatabase()
		{
			return CreateDatabase(env, adminDbKey, 0);
		}

		BackupSet MakeBackupSet()
		{
			BackupSet ret = null;
			if (envConfig.Checkpoint != null && envConfig.Checkpoint.Backup != null &&
				envConfig.Checkpoint.Backup.Enabled)
			{
				Backup backupConfig = envConfig.Checkpoint.Backup;
				string backupDirectory = "Bkp";
				if (!string.IsNullOrEmpty(backupConfig.Directory))
				{
					backupDirectory = backupConfig.Directory;
				}
				ret = (env == null) ? 
					CreateBackup(envConfig, backupDirectory, backupConfig) : 
					CreateBackup(backupDirectory, backupConfig);
			}
			return ret;
		}

		private int typeRangeSize;
		private int maxFederationSize;
		private void LoadConfig(BerkeleyDbConfig newBdbConfig)
		{
			IncrementHandleIteration();
			bdbConfig = newBdbConfig;
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("LoadConfig() BerkeleyDbConfig: BufferSize = {0}", newBdbConfig.BufferSize);
				Log.DebugFormat("LoadConfig() BerkeleyDbConfig: MinTypeId = {0}", newBdbConfig.MinTypeId);
				Log.DebugFormat("LoadConfig() BerkeleyDbConfig: MaxTypeId = {0}", newBdbConfig.MaxTypeId);
			}
			bufferSize = newBdbConfig.BufferSize;
			dbEntryPool.MaxItemReuses = newBdbConfig.MaxPoolItemReuse;
			envConfig = newBdbConfig.EnvironmentConfig;

			if (databases == null)
			{ //initial load
				if (newBdbConfig.MinTypeId < 1)
				{
					Log.Warn("Berkeley Db Component minimum Type Id must be greater than 0. Using 1 as default.");
					minTypeId = 1;
				}
				else
				{
					minTypeId = newBdbConfig.MinTypeId;
				}
				maxTypeId = newBdbConfig.MaxTypeId;
				typeRangeSize = maxTypeId - minTypeId + 1;
				

				databaseFederationSizes = new int[typeRangeSize];
				maxFederationSize = GetMaxFederationSize(newBdbConfig.EnvironmentConfig);
				databases = new Database[typeRangeSize, maxFederationSize];
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("Using type range size {0} and max federation size {1}", typeRangeSize, maxFederationSize);
				}
				databaseCreationLocks = new object[typeRangeSize, maxFederationSize];
				for (int i = 0; i < databaseCreationLocks.GetLength(0); i++)
				{
					for (int j = 0; j < databaseCreationLocks.GetLength(1); j++)
					{
						databaseCreationLocks[i, j] = new object();
					}
				}
			}
			else
			{ //reload
				if (newBdbConfig.MinTypeId != minTypeId ||
					newBdbConfig.MaxTypeId != maxTypeId)
				{
					// the following implementation satisfies the scenario of increasing the typeID range.  
					// nothing will change if the typeID range is decreased.
					// In the case of decreasing the range, the service needs to be restarted. 

					short nOldMinTypeId = minTypeId;
					short nOldMaxTypeId = maxTypeId;
					short nOffset = 0;

					if (newBdbConfig.MinTypeId < 1)
					{
						Log.Warn("Berkeley Db Component minimum Type Id must be greater than 0. Using 1 as default.");
						minTypeId = 1;
					}
					else
					{
						if (minTypeId > newBdbConfig.MinTypeId)
						{
							nOffset = (short)(minTypeId - newBdbConfig.MinTypeId);
							minTypeId = newBdbConfig.MinTypeId;
						}
					}

					if (newBdbConfig.MaxTypeId > maxTypeId)
					{
						maxTypeId = newBdbConfig.MaxTypeId;
					}

					int newTypeRangeSize = maxTypeId - minTypeId + 1;
					int newMaxFederationSize = GetMaxFederationSize(newBdbConfig.EnvironmentConfig);
					Database[,] newDatabases = new Database[newTypeRangeSize, newMaxFederationSize];
					databaseFederationSizes = new int[newTypeRangeSize];
					object[,] newDatabaseCreationLocks = new object[newTypeRangeSize, newMaxFederationSize];

					DatabaseConfigs dbConfigs = envConfig.DatabaseConfigs;

					for (int typeId = minTypeId; typeId <= maxTypeId; typeId++)
					{
						int federationSize = dbConfigs.GetFederationSize(typeId);
						int typeIndex = typeId - minTypeId;
						databaseFederationSizes[typeIndex] = federationSize;
						for (int federationIndex = 0; federationIndex < federationSize; federationIndex++)
						{
							if (typeId < nOldMinTypeId || typeId > nOldMaxTypeId)
							{   // outside of existing database range
								DatabaseConfig dbConfig = dbConfigs.GetConfigForFederated(typeId, federationIndex);
								Database db = CreateDatabase(env, dbConfig);
								newDatabases[typeIndex, federationIndex] = db;
								newDatabaseCreationLocks[typeIndex, federationIndex] = new object();
							}
							else
							{
								newDatabases[typeIndex, federationIndex] = databases[typeIndex - nOffset, federationIndex];
								newDatabaseCreationLocks[typeIndex, federationIndex] = databaseCreationLocks[typeIndex - nOffset, federationIndex];
							}
						}
					}
					databases = newDatabases;
					databaseCreationLocks = newDatabaseCreationLocks;
				}
			}

			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("LoadConfig() EnvironmentConfig: CacheSize = {0} gigabyts, {1} bytes, {2} number of caches"
					, envConfig.CacheSize.GigaBytes, envConfig.CacheSize.Bytes, envConfig.CacheSize.NumberCaches);
				Log.DebugFormat("LoadConfig() EnvironmentConfig: HomeDirectory = {0}"
					, envConfig.HomeDirectory);
				Log.DebugFormat("LoadConfig() EnvironmentConfig: OpenFlags = {0}"
					, envConfig.OpenFlags);
				Log.DebugFormat("LoadConfig() EnvironmentConfig: Flags = {0}"
					, envConfig.Flags);
				Log.DebugFormat("LoadConfig() EnvironmentConfig: TempDirectory = {0}"
					, envConfig.TempDirectory);
				Log.DebugFormat("LoadConfig() EnvironmentConfig: VerboseDeadlock = {0}"
				   , envConfig.VerboseDeadlock);
				Log.DebugFormat("LoadConfig() EnvironmentConfig: VerboseRecovery = {0}"
				   , envConfig.VerboseRecovery);
				Log.DebugFormat("LoadConfig() EnvironmentConfig: VerboseWaitsFor = {0}"
				   , envConfig.VerboseWaitsFor);
			}
		}

		private void MessageCall(object sender, BerkeleyDbMessageEventArgs e)
		{
			string message = e.Message;
			if (Log.IsDebugEnabled)
			{
				Log.Debug(message);
			}
			const string keyPhrase = "Number of keys in the database";
			if (message.EndsWith(keyPhrase))
			{
				int index = message.IndexOf("\t");
				string keyCountString = message.Substring(0, index);
				int keyCount = Convert.ToInt32(keyCountString);
				
				SetStoredObjects(keyCount);
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("{0}", message);
				}
			}
		}

		private static void PanicCall(object sender, BerkeleyDbPanicEventArgs e)
		{
			string errMessage = e.Message;
			if (errMessage.Contains("DB_BUFFER_SMALL"))
			{//this will have been logged inside of bdb, and will have thrown an exception that will be logged elsewhere with more detail
			}
			else
			{
				Log.ErrorFormat("PanicCall() {0}", errMessage);
			}
		}

		internal BerkeleyDbWrapper.Environment Environment { get { return env; } }

		internal bool IsShuttingDown { get { return isShuttingDown; } }

		private void CloseEnvironment()
		{
			if (env != null)
			{
				try
				{
					env.Dispose();
				}
				catch (Exception ex)
				{
					if (Log.IsErrorEnabled)
					{
						Log.Error("CloseEnvironment() threw error closing", ex);
					}
				}
				finally
				{
					env = null;
				}
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("CloseEnvironment() Environment closed");
				}
			}
		}

		private void CloseAllHandles()
		{
			CloseAllDatabases(databases, databaseCreationLocks);
			CloseEnvironment();
		}

		private void RecreateEnv(EnvironmentConfig newEnvConfig)
		{
			CloseEnvironment();
			if (env != null)
			{
				env.Dispose();
			}
			env = CreateEnvironment(newEnvConfig);
				SetLockCounters(env);
			}

		/// <summary>
		/// All files (Bdb region files, Admin files and data files) in Home Directory get removed. 
		/// </summary>
		private static void RemoveAllFiles(EnvironmentConfig envConfigToRemove)
		{
			string homeDir = envConfigToRemove.HomeDirectory;
			if (Directory.Exists(homeDir))
			{
				string relocateDirectory = Path.Combine(
					Path.GetDirectoryName(homeDir), Path.GetFileName(homeDir) + "__Removed");
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("RemoveAllFiles() Moving {0} directory to {1} ...", homeDir, relocateDirectory);
				}
				if (Directory.Exists(relocateDirectory))
				{
					Directory.Delete(relocateDirectory, true);
				}
				Directory.Move(homeDir, relocateDirectory);
				Directory.CreateDirectory(homeDir);
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("RemoveAllFiles() Moving {0} directory to {1} complete", homeDir, relocateDirectory);
				}
			}
		}

		/// <summary>
		/// All Bdb supporting region files get removed.
		/// Usually it should be 4 __db.001 - __db.004 files.
		/// Bdb environment uses them to configure environment.
		/// </summary>
		/// <param name="homeDir">Home directory</param>
		private static void RemoveAllRegionFiles(string homeDir)
		{
			if (Log.IsInfoEnabled)
			{
				Log.InfoFormat("RemoveAllRegionFiles() Remove all region files.");
			}

			BerkeleyDbWrapper.Environment.Remove(homeDir, EnvOpenFlags.UseEnviron | EnvOpenFlags.UseEnvironRoot,
				true);

		}

		private void RemoveDb(int typeIndex, int federationIndex, DatabaseConfig oldDbConfig, DatabaseConfig newDbConfig)
		{
			if (databases[typeIndex, federationIndex] != null)
			{
				lock (databaseCreationLocks[typeIndex, federationIndex])
				{
					Database db = databases[typeIndex, federationIndex];
					if (db != null)
					{
						databases[typeIndex, federationIndex] = null;
						db.Dispose();
					}
					if (RequiresDbFileRemoval(oldDbConfig, newDbConfig))
					{
						if (Log.IsInfoEnabled)
						{
							Log.InfoFormat("RemoveDb() Database with Id = {0} requires file removal. Please stop the service, remvove DB files and start service again."
								, newDbConfig.Id);
						}
						RemoveDbFiles(oldDbConfig);
					}
				}
			}
		}

		private void RemoveDbFiles(DatabaseConfig dbConfig)
		{
			string filePath = dbConfig.FileName;
			if (filePath == null)
			{
				return;
			}

			if (envConfig != null && !string.IsNullOrEmpty(envConfig.HomeDirectory))
			{
				filePath = Path.Combine(envConfig.HomeDirectory, filePath);
			}
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("RemoveDbFiles() remove file {0}", filePath);
			}
			try
			{
				File.Delete(filePath);
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error(string.Format("RemoveDbFiles() fails deleting {0}", filePath), ex);
				}
			}
		}

		private static bool RequiresDbReload(DatabaseConfig oldConfig, DatabaseConfig newConfig)
		{
			return RequiresDbFileRemoval(oldConfig, newConfig)
				|| newConfig.FileName != oldConfig.FileName
				|| newConfig.MaxDeadlockRetries != oldConfig.MaxDeadlockRetries;
		}

		private static bool RequiresDbFileRemoval(DatabaseConfig oldConfig, DatabaseConfig newConfig)
		{
			return newConfig.FederationSize != oldConfig.FederationSize
				|| newConfig.OpenFlags != oldConfig.OpenFlags
				|| newConfig.Flags != oldConfig.Flags
				|| newConfig.PageSize != oldConfig.PageSize
				|| newConfig.Type != oldConfig.Type
				|| newConfig.HashFillFactor != oldConfig.HashFillFactor
				|| newConfig.HashSize != oldConfig.HashSize
				|| newConfig.RecordLength != oldConfig.RecordLength;
		}


		private static bool FederationSizeChanged(EnvironmentConfig oldConfig, EnvironmentConfig newConfig)
		{
			DatabaseConfigs newDatabaseConfigs = newConfig.DatabaseConfigs;
			DatabaseConfigs oldDatabaseConfigs = oldConfig.DatabaseConfigs;

			if (newDatabaseConfigs.Count != oldDatabaseConfigs.Count)
				return true;

			foreach (DatabaseConfig dcOld in oldDatabaseConfigs)
			{
				try
				{
					if (dcOld.FederationSize != newDatabaseConfigs[dcOld.Id].FederationSize)
						return true;
				}
				catch
				{
					return true;
				}
			}

			return false;
		}

		private static bool RequiresRecreateEnv(EnvironmentConfig oldConfig, EnvironmentConfig newConfig)
		{
			EnvCacheSize oldCacheSize = oldConfig.CacheSize;
			EnvCacheSize newCacheSize = newConfig.CacheSize;
			return newConfig.OpenFlags != oldConfig.OpenFlags
				|| (newConfig.Flags & BerkeleyDbWrapper.Environment.PreOpenSetFlags) != (oldConfig.Flags & BerkeleyDbWrapper.Environment.PreOpenSetFlags)
				|| newConfig.HomeDirectory != oldConfig.HomeDirectory
				|| newCacheSize.GigaBytes != oldCacheSize.GigaBytes
				|| newCacheSize.Bytes != oldCacheSize.Bytes
				|| newCacheSize.NumberCaches != oldCacheSize.NumberCaches
				|| newConfig.MaxLockers != oldConfig.MaxLockers
				|| newConfig.MaxLocks != oldConfig.MaxLocks
				|| newConfig.MaxLockObjects != oldConfig.MaxLockObjects
				|| newConfig.MutexIncrement != oldConfig.MutexIncrement;
		}

		private void ResetDatabaseEntry(DatabaseEntry databaseEntry)
		{
			databaseEntry.StartPosition = 0;
			databaseEntry.Length = 0;
			if (databaseEntry.Buffer.Length < bufferSize)
			{
				databaseEntry.Resize(bufferSize);
			}
		}


		private void SaveEnvironmentData()
		{
			DateTime shutdownTime = DateTime.Now;
			try
			{
				using (Database adminDb = GetAdminDatabase())
				{
					adminDb.Put(dataVersion, Version);
					if (Log.IsInfoEnabled)
					{
						Log.InfoFormat("SaveEnvironmentData() saved Version = {0}", Version);
					}
					adminDb.Put(shutdownTimeKey, shutdownTime.ToString("F"));
					if (Log.IsInfoEnabled)
					{
						Log.InfoFormat("SaveEnvironmentData() saved ShutdownTime = {0}", shutdownTime);
					}
					adminDb.Sync();
				}
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("SaveEnvironmentData() Failed trying to write Shutdown time and Version.", ex);
				}
			}
		}

		private static void Serialize(int intValue, byte[] bytes)
		{
			uint intVal = (uint)intValue;
			bytes[0] = (byte)intVal;
			bytes[1] = (byte)(intVal >> 8);
			bytes[2] = (byte)(intVal >> 16);
			bytes[3] = (byte)(intVal >> 24);
		}

		private void SetStoredObjects(int keyCount)
		{
			if (StoredObjectsCounter != null)
			{
				StoredObjectsCounter.RawValue = keyCount;
			}
		}

		private void SetPooledBufferSize(int newBufferSize)
		{
			if (PooledBufferSizeCounter != null)
			{
				PooledBufferSizeCounter.RawValue = newBufferSize;
			}
		}

		private static void SetEnvironmentConfiguration(BerkeleyDbWrapper.Environment environment, EnvironmentConfig envConfig)
		{
			if (envConfig.Flags > 0)
			{
				environment.SetFlags(envConfig.Flags);
			}
			environment.SetVerboseDeadlock(envConfig.VerboseDeadlock);
			environment.SetVerboseRecovery(envConfig.VerboseRecovery);
			environment.SetVerboseWaitsFor(envConfig.VerboseWaitsFor);
            if (envConfig.SpinWaits > 0)
            {
                environment.SpinWaits = envConfig.SpinWaits;
            }
			EnvOpenFlags envOpenFlags = envConfig.OpenFlags;
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("SetEnvironmentConfiguration() Env: OpenFlags = {0}", environment.GetOpenFlags());
				Log.DebugFormat("SetEnvironmentConfiguration() Env: Flags = {0}", environment.GetFlags());
				if ((envOpenFlags & EnvOpenFlags.InitLock) == EnvOpenFlags.InitLock
					|| (envOpenFlags & EnvOpenFlags.InitCDB) == EnvOpenFlags.InitCDB)
				{
					Log.InfoFormat("SetEnvironmentConfiguration() Env: MaxLockers = {0}", environment.GetMaxLockers());
					Log.InfoFormat("SetEnvironmentConfiguration() Env: MaxLocks = {0}", environment.GetMaxLocks());
					Log.InfoFormat("SetEnvironmentConfiguration() Env: MaxLockObjects = {0}", environment.GetMaxLockObjects());
					Log.InfoFormat("SetEnvironmentConfiguration() Env: LockTimeout = {0}", environment.GetTimeout(TimeoutFlags.LockTimeout));
					Log.InfoFormat("SetEnvironmentConfiguration() Env: TxnTimeout = {0}", environment.GetTimeout(TimeoutFlags.TxnTimeout));
			}

				Log.DebugFormat("SetEnvironmentConfiguration() Env: VerboseDeadlock = {0}", environment.GetVerboseDeadlock());
				Log.DebugFormat("SetEnvironmentConfiguration() Env: VerboseRecovery = {0}", environment.GetVerboseRecovery());
				Log.DebugFormat("SetEnvironmentConfiguration() Env: VerboseWaitsFor = {0}", environment.GetVerboseWaitsFor());				
			
				Log.DebugFormat("SetEnvironmentConfiguration() ##BEGIN ENV STAT############################################");
				environment.PrintStats();
				Log.DebugFormat("SetEnvironmentConfiguration() ##END ENV STAT##############################################");
				Log.DebugFormat("SetEnvironmentConfiguration() ##BEGIN ENV CACHE STAT######################################");
				environment.PrintCacheStats();
				Log.DebugFormat("SetEnvironmentConfiguration() ##END ENV CACHE STAT########################################");
				Log.DebugFormat("SetEnvironmentConfiguration() ##BEGIN ENV LOCK STAT#######################################");
				if ((envOpenFlags & EnvOpenFlags.InitLock) == EnvOpenFlags.InitLock
					|| (envOpenFlags & EnvOpenFlags.InitCDB) == EnvOpenFlags.InitCDB)
				{
					environment.PrintLockStats();
					Log.DebugFormat("CreateEnvironment() ##END ENV LOCK STAT#########################################");
				}
			}
		}

		private static DateTime ToDateTime(string data)
		{
			if (data == null)
			{
				return DateTime.MinValue;
			}
			DateTime dt = DateTime.Parse(data);
			return dt;
		}

		private void TrickleCache()
		{
			CacheTrickle cacheTrickle = envConfig.CacheTrickle;
			if (cacheTrickle != null && cacheTrickle.Enabled)
			{
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("TrickleCache() CacheTrickling started ...");
				}
				int pagesCleaned = env.MempoolTrickle(cacheTrickle.Percentage);
				
				if (TrickledPagesCounter != null)
				{
					TrickledPagesCounter.RawValue = pagesCleaned;
				}
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("TrickleCache() CacheTrickling is complete. {0} pages cleaned.", pagesCleaned);
				}
			}
		}

		private void CompactDatabases()
		{
			Compact compact = envConfig.Compact;
			if (compact != null && compact.Enabled)
			{
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("Compact() started ...");
				}
				Database[,] databasesToCompact = databases;
				for (short typeIndex = 0; typeIndex < databasesToCompact.GetLength(0); typeIndex++)
				{
					for (int federationIndex = 0; federationIndex < databasesToCompact.GetLength(1); federationIndex++)
					{
						Database db = databasesToCompact[typeIndex, federationIndex];
						if (db != null)
						{
							DatabaseConfig dbConfig = db.GetDatabaseConfig();
							DatabaseCompact dbCompact = dbConfig.Compact;
							if (dbCompact != null && dbCompact.Enabled)
							{
								try
								{
									int pagesFreed = db.Compact(dbCompact.Percentage, dbCompact.MaxPages, dbCompact.Timeout);
									if (pagesFreed > 1)
									{
										if (Log.IsInfoEnabled)
										{
											Log.InfoFormat("Compact() Freed {0} pages from {1}", pagesFreed,
												dbConfig.FileName);
										}
									}
								}
								catch (BdbException exc)
								{
									switch (exc.Code)
									{
										case (int)DbRetVal.PAGE_NOTFOUND: // ignore page not found
											break;
										default:
											HandleBdbError(exc, db);
											break;
									}
								}
							}
						}
					}
				}
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("Compact() completed ...");
				}
				lastCompactTime = DateTime.Now;
			}
		}

		private static bool HaveMillisecondsElapsed(DateTime referenceTime, int milliseconds)
		{
			return DateTime.Now >= referenceTime + TimeSpan.FromMilliseconds(milliseconds);
		}

		private void Checkpoint()
		{
			Checkpoint checkpoint = envConfig.Checkpoint;
			if (checkpoint != null && checkpoint.Enabled)
			{
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("Checkpoint() started ...");
				}
				env.Checkpoint(checkpoint.LogSizeKByte, checkpoint.LogAgeMinutes,
					checkpoint.Force);
				// force a log file write for in memory logging so recovery can occur
				if (IsLoggingInMemory)
				{
					env.FlushLogsToDisk();
				}

				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("Checkpoint() is complete");
				}
				Backup backupConfig = checkpoint.Backup;
				if (backupConfig != null && backupConfig.Enabled)
				{
					// If backup interval specified, use it. Otherwise backup after each checkpoint
					bool doBackup = (backupConfig.Interval <= 0) ||
						HaveMillisecondsElapsed(backupSet.LastUpdateTime, backupConfig.Interval);
					// Reinit backup if interval specified or necessary number of removable log files
					bool reinitBackup = backupSet.IsInitialized &&
						(backupConfig.ReinitializeInterval > 0 &&
							HaveMillisecondsElapsed(backupSet.FirstBackupTime, backupConfig.ReinitializeInterval))
						|| (backupConfig.ReinitializeLogFileCount > 0 &&
							backupSet.GetRemovableLogFileCount() >= backupConfig.ReinitializeLogFileCount);
					
					if (reinitBackup)
					{
						string oldBackupDirectory = backupSet.BackupDirectory;
						string newBackupDirectory = backupConfig.Directory + "__new";
						BackupSet newBackupSet = CreateBackup(newBackupDirectory, backupConfig);
						newBackupSet.CopyBuffer = backupSet.CopyBuffer; // to avoid realloc of large buffer for log file copy
						if (Log.IsDebugEnabled)
						{
							Log.DebugFormat("New backup() started ...");
						}
						// compact databases if enabled before recopying databases done in backup reinit
						Compact compactConfig = envConfig.Compact;
						if (compactConfig != null && compactConfig.Enabled && (compactConfig.Interval <= 0 ||
							HaveMillisecondsElapsed(lastCompactTime, compactConfig.Interval)))
						{
							CompactDatabases();
						}
						newBackupSet.Backup();
						if (oldBackupDirectory != newBackupDirectory &&
							Directory.Exists(oldBackupDirectory))
						{
							Directory.Delete(oldBackupDirectory, true);
						}
						backupSet = newBackupSet;
						backupSet.Move(backupConfig.Directory, false);
						backupSet.DeleteUnusedLogFiles();
						if (Log.IsDebugEnabled)
						{
							Log.DebugFormat("New backup() is complete");
						}
					}
					else if (doBackup)
					{
						string op = backupSet.IsInitialized ? "Backup update" : "Backup";
						if (Log.IsDebugEnabled)
						{
							Log.DebugFormat("{0}() started ...", op);
						}
						backupSet.Backup();
						backupSet.DeleteUnusedLogFiles();
						if (Log.IsDebugEnabled)
						{
							Log.DebugFormat("{0}() is complete", op);
						}
					}
				}
				else
				{
					if (Log.IsDebugEnabled)
					{
						Log.DebugFormat("Unused log deletion() started ...");
					}
					env.DeleteUnusedLogs();
					if (Log.IsDebugEnabled)
					{
						Log.DebugFormat("Unused log deletion() is complete");
					}
				}
			}
		}

		private void LockStatisticsMonitor()
		{
			LockStatistics lockStatistics = envConfig.LockStatistics;
			if (lockStatistics != null && lockStatistics.Enabled)
			{
				env.GetLockStatistics();
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("LockStatisticsMonitor() performed ...");
				}
			}
		}

        private static int GetExpandedBufferSize(int recLen)
        {
            return (int)(recLen * expandBufferSizeMultiplier);
        }
		#endregion

		#region Public Perf Counters

        public PerformanceCounter TrickledPagesCounter { get; set; }

		public PerformanceCounter DeletedObjectsCounter { get; set; }

		public PerformanceCounter StoredObjectsCounter { get; set; }

		public PerformanceCounter PooledBufferSizeCounter { get; set; }

		public PerformanceCounter AllocatedBuffersCounter { get; set; }

		public PerformanceCounter BuffersInUseCounter { get; set; }

		public PerformanceCounter LockStatLastLockerId { get; set; }

		public PerformanceCounter LockStatCurrentMaxLockerId { get; set; }

		public PerformanceCounter LockStatNumberLockModes { get; set; }

		public PerformanceCounter LockStatMaxLocksPossible { get; set; }

		public PerformanceCounter LockStatMaxLockersPossible { get; set; }

		public PerformanceCounter LockStatMaxLockObjectsPossible { get; set; }

		public PerformanceCounter LockStatNumberCurrentLocks { get; set; }

		public PerformanceCounter LockStatMaxNumberLocksAtOneTime { get; set; }

		public PerformanceCounter LockStatNumberCurrentLockers { get; set; }

		public PerformanceCounter LockStatMaxNumberLockersAtOneTime { get; set; }

		public PerformanceCounter LockStatNumberCurrentLockObjects { get; set; }

		public PerformanceCounter LockStatNumberCurrentLockObjectsAtOneTime { get; set; }

		public PerformanceCounter LockStatNumberLocksRequested { get; set; }

		public PerformanceCounter LockStatNumberLocksReleased { get; set; }

		public PerformanceCounter LockStatNumberLocksUpgraded { get; set; }

		public PerformanceCounter LockStatNumberLocksDownGraded { get; set; }

		public PerformanceCounter LockStatLockWait { get; set; }

		public PerformanceCounter LockStatLockNoWait { get; set; }

		public PerformanceCounter LockStatNumberDeadLocks { get; set; }

		public PerformanceCounter LockStatLockTimeout { get; set; }

		public PerformanceCounter LockStatNumberLockTimeouts { get; set; }

		public PerformanceCounter LockStatTxnTimeout { get; set; }


		public PerformanceCounter LockStatNumberTxnTimeouts { get; set; }

		public PerformanceCounter LockStatObjectsWait { get; set; }

		public PerformanceCounter LockStatObjectsNoWait { get; set; }

		public PerformanceCounter LockStatLockersWait { get; set; }

		public PerformanceCounter LockStatLockersNoWait { get; set; }

		public PerformanceCounter LockStatLocksWait { get; set; }

		public PerformanceCounter LockStatLocksNoWait { get; set; }

		public PerformanceCounter LockStatLockHashLen { get; set; }

		public PerformanceCounter LockStatLockRegionSize { get; set; }

		public PerformanceCounter LockStatRegionWait { get; set; }

		public PerformanceCounter LockStatRegionNoWait { get; set; }

		#endregion


		#region Public Methods
		/// <summary>
		/// Occurs when <see cref="Status"/> changes.
		/// </summary>
		public event EventHandler StatusChanged;

		/// <summary>
		/// Gets the status of this instance.
		/// </summary>
		/// <value>A <see cref="BerkeleyDbStatus"/> that represents the ability of this instance to perform
		/// data operations.</value>
		public BerkeleyDbStatus Status
		{
			get { return (BerkeleyDbStatus)_status; }
			private set
			{
				var val = (int)value;
				if (Interlocked.Exchange(ref _status, val) != val)
				{
					if (Log.IsInfoEnabled)
					{
						Log.InfoFormat("Status changed to " + value);
					}
					var statusChanged = StatusChanged;
					if (statusChanged != null)
					{
						statusChanged(this, EventArgs.Empty);
					}
				}
			}
		}

		public int DeleteAll()
		{
			int count = 0;
			if (Log.IsInfoEnabled)
			{
				Log.InfoFormat("DeleteAll() is deleting all.");
			}
			
			for (short typeId = minTypeId; typeId <= maxTypeId; typeId++)
			{
				count += DeleteAllInType(typeId);
			}
			return count;
			
		}

		/// <summary>
		/// Remove all objects in the store of a given type
		/// </summary>
		/// <param name="typeId">The type to remove</param>
		public int DeleteAllInType(short typeId)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("DeleteAllInType() deletes all objects of the type (TypeId={0})", typeId);
			}
			var isTransactional = (envConfig.OpenFlags & EnvOpenFlags.InitTxn) ==
				EnvOpenFlags.InitTxn;
			Database db = null;
			var totalCount = 0;
			var typeIndex = typeId - minTypeId;
			var federationCount = databases.GetLength(1);
			try
			{
				for (var federationIndex = 0; federationIndex < federationCount; ++federationIndex)
				{
					var count = 0;
                    lock (databaseCreationLocks[typeIndex, federationIndex])
                    {
						db = databases[typeIndex, federationIndex];
						if (db != null)
						{
							databases[typeIndex, federationIndex] = null;
							try
							{
								try
								{
									db.Dispose();
								} finally
								{
									var config = GetDatabaseConfig(typeId, federationIndex);
									if (isTransactional)
									{
										env.RemoveDatabase(config.FileName);
									} else
									{
										Database.Remove(env, config.FileName);										
									}
									switch (bdbConfig.DbLoadMode)
									{
										case DbLoadMode.OnStartup:
											db = GetDatabase(typeId, federationIndex);
											databases[typeIndex, federationIndex] = db;
											break;
										case DbLoadMode.Lazy:
											break;
										default:
											throw new ApplicationException("Unhandled db load mode"
												+ bdbConfig.DbLoadMode);
									}
								}
							} catch(BdbException ex)
							{
								HandleBdbError(ex, db);								
							}
						}
                    }
					db = null;
					if (Log.IsDebugEnabled)
					{
						Log.DebugFormat("DeleteAllInType() deleted {0} objects of the type (TypeId={1}, FedIdx={2})"
							, count, typeId, federationIndex);
					}
				}
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("DeleteAllInType() deleted {0} objects of the type (TypeId={1})", totalCount, typeId);
				}
				return totalCount;
			}
			catch (BdbException ex)
			{
				HandleBdbError(ex, db);
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error(string.Format("DeleteAllInType() Error deleting all objects of the type {0}", typeId), ex);
				}
				throw;
			}
			return 0;
		}

		public bool DeleteObject(short typeId, int objectId)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("DeleteObject() deletes object (TypeId={0}, ObjectId={1})", typeId, objectId);
			}
			Database db = GetDatabase(typeId, objectId);
			return DeleteRecord(db, objectId);
		}

		public bool DeleteObject(short typeId, int objectId, byte[] key)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("DeleteObject() deletes object (TypeId={0}, ObjectId={1})", typeId, objectId);
			}
			Database db = GetDatabase(typeId, objectId);
			if (key != null)
			{
				return DeleteRecord(db, key);
			}
			return DeleteRecord(db, objectId);
		}

		/// <summary>
		/// Remove the object id in all known types
		/// </summary>
		/// <param name="objectId">The object to remove</param>
		public int DeleteObjectInAllTypes(int objectId)
		{
			int count = 0;
			//using (envLock.WaitToRead())
			//{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("DeleteObjectInAllTypes() deletes all types of objects for the given ID (ObjectId={0})", objectId);
			}
			//foreach (short dbKey in databases.Keys)
			for (short typeId = minTypeId; typeId <= maxTypeId; typeId++)
			{
				//DbHash db = databases[key];
				//DeleteRecord(db, null, GetKey(objectID, key), DbFile.WriteFlags.None);
				if (DeleteObject(typeId, objectId))
				{
					count++;
				}
			}
			//}
			return count;
			//UpdateMemoryCounters();
		}

		/// <summary>
		/// Remove the object id in all known types
		/// </summary>
		/// <param name="objectId">The primary id of the object to remove</param>
		/// <param name="key">The extended id of the object to remove </param>
		public int DeleteObjectInAllTypes(int objectId, byte[] key)
		{
			int count = 0;
			
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("DeleteObjectInAllTypes() deletes all types of objects for the given ID (ObjectId={0})", objectId);
			}
			
			for (short typeId = minTypeId; typeId <= maxTypeId; typeId++)
			{
				if (DeleteObject(typeId, objectId, key))
				{
					count++;
				}
			}
			
			return count;
		}

		/// <summary>
		/// Retrieve a byte array from the BerkeleyDb Store
		/// </summary>
		/// <param name="typeId">The Type ID of the message</param>
		/// <param name="objectId">The Object ID of the message</param>
		/// <returns>The stored CacheMessage object</returns>
		public byte[] GetObject(short typeId, int objectId)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("GetObject() gets record (TypeId={0}, ObjectId={1})", typeId, objectId);
			}
			Database db = GetDatabase(typeId, objectId);
			return GetRecord(db, objectId);
		}

		public string GetString(short typeId, int objectId, string key)
		{
			Database db = GetDatabase(typeId, objectId);
			return db.Get(key);
		}

		public bool SaveString(short typeId, int objectId, string key, string data)
		{
			Database db = GetDatabase(typeId, objectId);
			db.Put(key, data);
			return true;
		}

		public void GetDbObject(short typeId, int objectId, DatabaseEntryMapper databaseEntryMapper)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("GetObject() gets record (TypeId={0}, ObjectId={1})", typeId, objectId);
			}
			Database db = GetDatabase(typeId, objectId);
			GetValue(db, objectId, databaseEntryMapper);
		}

		public void GetDbObject(short typeId, int objectId, byte[] key, DatabaseEntryMapper databaseEntryMapper)
		{
			if (databaseEntryMapper == null)
			{
				return;
			}

			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("GetObject() gets record (TypeId={0}, ObjectId={1})", typeId, objectId);
			}
			Database db = GetDatabase(typeId, objectId);
			if (key != null)
			{
				GetValue(db, key, databaseEntryMapper);
			}
			else
			{
				GetValue(db, objectId, databaseEntryMapper);
			}
		}

		public int GetKeyCount(DbStatFlags dbStatFlags)
		{
			return GetKeyCount(databases, dbStatFlags);
		}

		public static int GetKeyCount(Database[,] databasesToCount, DbStatFlags dbStatFlag)
		{
			int totalKeyCount = 0;
			for (short typeIndex = 0; typeIndex < databasesToCount.GetLength(0); typeIndex++)
			{
				int typeKeyCount = 0;
				for (int federationIndex = 0; federationIndex < databasesToCount.GetLength(1); federationIndex++)
				{
					Database db = databasesToCount[typeIndex, federationIndex];
					if (db != null)
					{
						try
						{
							int dbKeyCount = db.GetKeyCount(dbStatFlag);
							if (Log.IsDebugEnabled)
							{
								Log.DebugFormat("GetKeyCount() DB (typeIdx={0}, fedIdx={1}) contains {2} unique keys."
									, typeIndex, federationIndex, dbKeyCount);
							}
							typeKeyCount += dbKeyCount;
						}
						catch (Exception ex)
						{
							if (Log.IsErrorEnabled)
							{
								Log.Error("GetKeyCount() Error getting DB Stats.", ex);
							}
						}
					}
					else
					{
						if (Log.IsWarnEnabled)
						{
							Log.WarnFormat("GetKeyCount() cannot print statistic since database is disposed for the index {0},{1}",
								typeIndex, federationIndex);
						}
					}
				}
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("GetKeyCount() All databases of the typeIdx={0} contains {1} unique keys.", typeIndex, typeKeyCount);
				}
				totalKeyCount += typeKeyCount;
			}
			if (Log.IsInfoEnabled)
			{
				Log.InfoFormat("GetKeyCount() Total number of unique keys is {0}.", totalKeyCount);
			}
			return totalKeyCount;
		}

		public void GetStats()
		{
			GetStats(databases);
		}

		private static void GetStats(Database[,] databaseArrays)
		{
			if (databaseArrays != null)
			{
				for (short typeIndex = 0; typeIndex < databaseArrays.GetLength(0); typeIndex++)
			{
					for (int federationIndex = 0; federationIndex < databaseArrays.GetLength(1); federationIndex++)
				{
						Database db = databaseArrays[typeIndex, federationIndex];
					if (db != null)
					{
						db.PrintStats(DbStatFlags.FastStat);
					}
					else
					{
						if (Log.IsWarnEnabled)
						{
								Log.WarnFormat(
									"GetStats() cannot print statistic since database is disposed for the index {0},{1}",
									typeIndex, federationIndex);
							}
						}
					}
				}
			}
		}

		private void PreLoadDatabasesOnDemand(BerkeleyDbConfig bdbConfigToLoad, 
			Database[,] databasesToLoadInto, int[] federationSizesToLoadInto, 
			short minTypeIdToLoad, short maxTypeIdToLoad)
		{
			EnvironmentConfig envConfigToLoad = bdbConfigToLoad.EnvironmentConfig;
			DatabaseConfigs dbConfigs = envConfigToLoad.DatabaseConfigs;
			for (int typeId = minTypeIdToLoad; typeId <= maxTypeIdToLoad; typeId++)
			{
				int typeIndex = typeId - minTypeId;
				if (federationSizesToLoadInto[typeIndex] > 0) continue;
				int federationSize = dbConfigs.GetFederationSize(typeId);
				federationSizesToLoadInto[typeIndex] = federationSize;
				for (int federationIndex = 0; federationIndex < federationSize; federationIndex++)
				{
					DatabaseConfig dbConfig = dbConfigs.GetConfigForFederated(typeId, federationIndex);
					if(Log.IsInfoEnabled)
					{
						Log.InfoFormat("Preloading database for type Id {0} and federation index {1}", typeId, federationIndex);
					}
					Database db = CreateDatabase(env, dbConfig);
					databasesToLoadInto[typeIndex, federationIndex] = db;
				}
			}
		}

		/// <summary>
		/// Preloads databases for a range of types. Will not reload already loaded
		/// databases.
		/// </summary>
		/// <param name="beginTypeIdRange">The lower limit of the range to load.</param>
		/// <param name="endTypeIdRange">The upper limit of the range to load.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="beginTypeIdRange"/> is less than
		/// <see cref="MinimumTypeId"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="endTypeIdRange"/> is greater than
		/// <see cref="MaximumTypeId"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="endTypeIdRange"/> is less than
		/// <paramref name="beginTypeIdRange"/>.</para>
		/// </exception>
		public void PreLoadDatabases(short beginTypeIdRange, short endTypeIdRange)
		{
			if (beginTypeIdRange < minTypeId)
			{
				throw new ArgumentOutOfRangeException("beginTypeIdRange", string.Format(
              		"Begin type id range is {0}, less than the minimum type id {1}",
              		beginTypeIdRange, minTypeId));
			}
			if (endTypeIdRange > maxTypeId)
			{
				throw new ArgumentOutOfRangeException("beginTypeIdRange", string.Format(
					"Begin type id range is {0}, less than the minimum type id {1}",
					endTypeIdRange, maxTypeId));
			}
			if (endTypeIdRange < beginTypeIdRange)
			{
				throw new ArgumentOutOfRangeException("endTypeIdRange", string.Format(
					"End type id range {1} is less than begin type id {0}",
					beginTypeIdRange, endTypeIdRange));
			}
			PreLoadDatabasesOnDemand(bdbConfig, databases, databaseFederationSizes,
				beginTypeIdRange, endTypeIdRange);
		}

		/// <summary>
		/// General initialization of the BerkeleyDbStorage. This will load all physical drives into memory for
		/// fast access later, install performance counters, and initialize worker services. This must be called
		/// before attempting to use the BerkeleyDbStorage.
		/// </summary>
		/// If false, the BerkeleyDbStorage will contain no data.</param>
		public void Initialize(string instanceName, BerkeleyDbConfig bdbConfigToInit)
		{
			Status = BerkeleyDbStatus.NotRunning;
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("Initialize() Initializing ...");
			}
			if (bdbConfigToInit == null)
			{
				if (Log.IsInfoEnabled)
			{
					Log.Info("Initialize() No configuration found, using defaults.");
			}
				bdbConfigToInit = new BerkeleyDbConfig();
			}
			
			isShuttingDown = false;
			LoadConfig(bdbConfigToInit);
			try
			{
				bool shutdownWindowExceeded = false;
				bool versionChanged = false;
				try
				{
					using (Database adminDb = GetAdminDatabase())
					{
						string version = adminDb.Get(dataVersion);
						if (Log.IsInfoEnabled)
						{
							Log.InfoFormat("Initialize() retrieved Version = {0}", version);
						}
						DateTime shutdownTime = ToDateTime(adminDb.Get(shutdownTimeKey));
						if (Log.IsInfoEnabled)
						{
							Log.InfoFormat("Initialize() retrieved ShutdownTime = {0}", shutdownTime);
						}

						if (!string.IsNullOrEmpty(version) && string.Compare(version, Version) != 0)
						{
							Log.ErrorFormat("Initialize() Stored version {0} differs from current version {1}",
											version, Version);
							versionChanged = true;

						}
						else
						{
							if (shutdownTime != DateTime.MinValue && bdbConfigToInit.ShutdownWindow > 0)
							{
								long downTimeSeconds = (DateTime.Now.Ticks - shutdownTime.Ticks)/TimeSpan.TicksPerSecond;

								if (downTimeSeconds > bdbConfigToInit.ShutdownWindow)
								{

									Log.ErrorFormat(
										"Initialize() Shutdown time of {0} seconds exceeds configured shutdown window of {1}. Either remove the databases or increase the shutdown window to preserve the data.",
										downTimeSeconds, bdbConfigToInit.ShutdownWindow);
									shutdownWindowExceeded = true;
								}
							}
							else
							{
								adminDb.Delete(shutdownTimeKey);
								if (Log.IsDebugEnabled)
								{
									Log.DebugFormat("Initialize() cleared ShutdownTime");
								}
							}
						}
						adminDb.Sync();
					}
				}
				catch (Exception ex)
				{
					if (Log.IsErrorEnabled)
					{
						Log.ErrorFormat("Initialize() Failed trying to retrieve Shutdown time and Version: {0}", ex);
					}
					throw new ApplicationException("Could not read admin database");
				}
				
				if(versionChanged)
					throw new ApplicationException("Data version changed.");
				if (shutdownWindowExceeded)
					throw new ApplicationException("Shutdown time exceeded");
				
				

				Recover(envConfig.VerifyOnStartup);
				

				ShutdownTimers();
				StartTimers();

				if (AllocatedBuffersCounter != null)
				{
					dbEntryPool.AllocatedItemsCounter = AllocatedBuffersCounter;
				}
				if (BuffersInUseCounter != null)
				{
					dbEntryPool.ItemsInUseCounter = BuffersInUseCounter;
				}
			}
			catch (Exception ex)
			{
				CloseAllHandles();
				Log.ErrorFormat("Initialize() Got {0}. Aborting.", ex.GetType().Name);
                if (ex is RecoveryFailedException)
                {
                    throw;
                }
		        throw new RecoveryFailedException(ex);						
			}

			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("Initialize() Initialize Complete.");
			}
			Status = BerkeleyDbStatus.Online;
		}

		public void ReloadConfig(BerkeleyDbConfig newBdbConfig)
		{
			var oldStatus = Status;
			if (oldStatus == BerkeleyDbStatus.Online)
			{
				Status = BerkeleyDbStatus.Offline;
			}
			if (Log.IsInfoEnabled)
			{
				Log.InfoFormat("ReloadConfig() Reloading ...");
			}
			if (bdbConfig == null)
			{
				throw new ApplicationException("Unable to Reload NULL BereleyDbConfig.");
			}
			EnvironmentConfig oldEnvConfig = envConfig;
			EnvironmentConfig newEnvConfig = newBdbConfig.EnvironmentConfig;
			if (newEnvConfig == null)
			{
				throw new ApplicationException("Unable to Reload NULL EnvironmentConfig.");
			}
			if (RequiresRecreateEnv(oldEnvConfig, newEnvConfig) || FederationSizeChanged(oldEnvConfig, newEnvConfig))
			{
				if (Log.IsInfoEnabled)
				{
					Log.InfoFormat("ReloadConfig() Reload requires recreating Environment. Please restart the service.");
				}
			}
			else
			{
				LoadConfig(newBdbConfig);
				env.RemoveFlags(oldEnvConfig.Flags);
				SetEnvironmentConfiguration(env, newEnvConfig);

				//Just close all databases. New configuration will be picked up on next GetDatabase call.
				//CreateDatabase method should handle cases (remove underling files) when database
				//is out of sync with file settings.
				for (int i = 0; i < databases.GetLength(0); i++)
				{
					int id = i + minTypeId;
					for (int j = 0; j < databases.GetLength(1); j++)
					{
						DatabaseConfig oldDbConfig = oldEnvConfig.DatabaseConfigs.GetConfigForFederated(id, j);
						DatabaseConfig newDbConfig = newEnvConfig.DatabaseConfigs.GetConfigForFederated(id, j);
						if (RequiresDbReload(oldDbConfig, newDbConfig))
						{
							RemoveDb(i, j, oldDbConfig, newDbConfig);
						}
					}
				}
			}

			ShutdownTimers();
			StartTimers();

			if (Log.IsInfoEnabled)
			{
				Log.InfoFormat("ReloadConfig() Reloading is complete");
			}
			Status = oldStatus;
		}

		/// <summary>
		/// Save a CacheMessage object into the store
		/// </summary>
		public bool SaveObject(short typeId, int objectId, byte[] data)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("SaveObject() saves object (TypeId={0}, ObjectId={1})", typeId, objectId);
			}
			
			Database db = GetDatabase(typeId, objectId);
			return AddRecord(db, objectId, data);
			
		}

		public bool SaveObject(short typeId, int objectId, byte[] key, byte[] data)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("SaveObject() saves object (TypeId={0}, ObjectId={1})", typeId, objectId);
			}
			Database db = GetDatabase(typeId, objectId);
			if (key != null)
			{
				return AddRecord(db, key, data);
			}
			return AddRecord(db, objectId, data);
		}

		public bool SaveObject(short typeId, int objectId, byte[] key, int startPosition, int length, RMWDelegate rmwDelegate)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("SaveObject() saves object (TypeId={0}, ObjectId={1})", typeId, objectId);
			}
			Database db = GetDatabase(typeId, objectId);

			return AddRecord(db, objectId, key, startPosition, length, rmwDelegate);
		}

		void StartTimers()
		{
			// set up the lock statistics stuff after the CreateEnvironment call. 
			// CreateEnvironment is where environment open() is called.
			dbLockStatCounterTimer = new ConfigurableCallbackTimer(this, envConfig.LockStatistics,
				"Lock Statistics Counter", 10000,
				LockStatisticsMonitor);

			trickleTimer = new ConfigurableCallbackTimer(this, envConfig.CacheTrickle,
				"Cache Trickle", 10000,
				TrickleCache);

			backupSet = MakeBackupSet();
			checkpointTimer = new ConfigurableCallbackTimer(this, envConfig.Checkpoint,
				"Checkpoint", 10000,
				Checkpoint);

			DeadlockDetection deadlockDetection = envConfig.DeadlockDetection;
			if (deadlockDetection != null && deadlockDetection.Mode == DeadlockDetectionMode.OnTimer)
			{
				deadlockDetectTimer = new ConfigurableCallbackTimer(this, deadlockDetection,
					"Deadlock Detection", 10000,
					DeadlockDetect);
			}

			dbStatTimer = new ConfigurableCallbackTimer(this, bdbConfig.StatTimer,
				"Stat Timer", 10000,
				DbStatPrint);

			// compaction has to be co-ordinated with any backups
			if (envConfig.Checkpoint == null || !envConfig.Checkpoint.Enabled ||
				envConfig.Checkpoint.Backup == null || !envConfig.Checkpoint.Backup.Enabled)
			{
				dbCompactTimer = new ConfigurableCallbackTimer(this, envConfig.Compact, "Compact", 60000,
					CompactDatabases);
			}
		}

		void ShutdownTimers()
		{
			ShutdownTimer(ref trickleTimer);
			backupSet = null;
			ShutdownTimer(ref checkpointTimer);
			ShutdownTimer(ref dbLockStatCounterTimer);
			ShutdownTimer(ref deadlockDetectTimer);
			ShutdownTimer(ref dbStatTimer);
			ShutdownTimer(ref dbCompactTimer);
		}

		static void ShutdownTimer(ref ConfigurableCallbackTimer timer)
		{
			if (timer != null)
			{
				timer.Dispose();
				timer = null;
			}
		}

		/// <summary>
		/// This method will run all clean up and serialization routines
		/// </summary>
		public void Shutdown()
		{
			Status = BerkeleyDbStatus.Offline;
			GC.SuppressFinalize(this);
			isShuttingDown = true;
			if (Log.IsInfoEnabled)
			{
				Log.InfoFormat("Shutting down ...");
			}

			ShutdownTimers();
			if (IsLogging)
			{
				env.FlushLogsToDisk();
			}
			CloseAllHandles();
			databases = null;
			SaveEnvironmentData();

			if (Log.IsInfoEnabled)
			{
				Log.InfoFormat("Shutdown Complete.");
			}
			Status = BerkeleyDbStatus.NotRunning;
		}

		protected void Dispose(bool disposing)
		{
			if (Status != BerkeleyDbStatus.NotRunning)
			{
				Shutdown();
			}			
		}

		~BerkeleyDbStorage()
		{
			Dispose(false);
		}

		void IDisposable.Dispose()
		{
			Dispose(true);
		}

		bool IsLogging
		{
			get { return ((envConfig.OpenFlags & EnvOpenFlags.InitLog) == EnvOpenFlags.InitLog); }
		}

		bool IsLoggingInMemory
		{
			get { return ((envConfig.Flags & EnvFlags.LogInMemory) == EnvFlags.LogInMemory); }
		}

		public BackupSet CreateBackup(string backupDir, Backup backupConfig)
		{
			return new BackupSet(this, backupDir, backupConfig);
		}
		public BackupSet CreateBackup(EnvironmentConfig config, string backupDir, Backup backupConfig)
		{
			return new BackupSet(config.HomeDirectory, backupDir, backupConfig);
		}
		#endregion

		#region IEnumerator
		[Obsolete("To iterate over objects, use GetRecords instead")]
		public IEnumerator GetEnumerator()
		{
			return new BDBStorageEnum(databases);
		}

		#endregion
	}
}
