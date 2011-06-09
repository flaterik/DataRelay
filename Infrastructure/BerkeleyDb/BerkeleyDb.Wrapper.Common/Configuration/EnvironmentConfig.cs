using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;

using BerkeleyDbWrapper;

namespace MySpace.BerkeleyDb.Configuration
{
	/// <summary>
	/// Provides the configuration of an <see cref="BerkeleyDbWrapper.Environment"/>. 
	/// </summary>
	public class EnvironmentConfig
	{
		private static readonly string defaultFolder = Path.Combine(Path.GetTempPath(), "BerkeleyDb"); 
		private EnvCacheSize cacheSize = new EnvCacheSize();

		private string homeDirectoryField = defaultFolder;

		private EnvOpenFlags openFlagsField =
			EnvOpenFlags.Create | //Cause Berkeley DB subsystems to create any underlying files
			//Flags to ensure concurent access
			EnvOpenFlags.Private |
			//Specify that the environment will only be accessed by a single process (although that process may be multithreaded), and will not back memory with the file system
			EnvOpenFlags.InitMPool | //Initialize the shared memory buffer pool subsystem
			EnvOpenFlags.ThreadSafe |  //Evn handle can be concurrently usable by multiple threads
			EnvOpenFlags.InitCDB;// |      //Provides multiple reader/single writer access. Implies InitLock.
		//EnvOpenFlags.InitLock |// |    //Initialize the locking subsystem (when multiple processes or threads are going to be reading and writing)  
		//EnvOpenFlags.InitTxn;     //Initialize the transaction subsystem (when recovery and atomicity of multiple operations are important) requires DB_INIT_LOG flag              
		//EnvOpenFlags.InitLog |     //Initialize the logging subsystem (when recovery is necessary). This is implied by InitTxn.
			
		//Applications that need recoverability. (These flags cannot go together with InitCDB flag, but InitMPool can be used)
		//EnvOpenFlags.Recover;      //Run normal recovery on this environment before opening it for normal use (requires DB_CREATE)
		//EnvOpenFlags.RecoverFatal;//Run catastrophic recovery on this environment before opening it for normal use (requires DB_CREATE)

		//Other Flags:
		//EnvOpenFlags.JoinEnv |     //Join an existing environment
		//EnvOpenFlags.InitRep |     //Initialize the replication subsystem (requires DB_INIT_TXN and DB_INIT_LOCK flags also be configured)
		//EnvOpenFlags.LockDown |    //Lock shared Berkeley DB environment files and memory-mapped databases into memory
		//
		//EnvOpenFlags.SystemMem |   //Allocate memory from system shared memory instead of from memory backed by the filesystem

		private bool openFlagsInitialized;
		private EnvFlags flagsField =
			EnvFlags.DbDirect;// |    //Turn off system buffering of Berkeley DB database files to avoid double caching
			//EnvFlags.DbDirectLog | //Turn off system buffering of Berkeley DB log files to avoid double caching
			//EnvFlags.DbDSync |      //Configure Berkeley DB to flush database writes to the backing disk before returning from the write system call, rather than flushing database writes explicitly in a separate system call, as necessary.
			//EnvFlags.DbSyncLog |    //Configure Berkeley DB to flush log writes to the backing disk before returning from the write system call, rather than flushing log writes explicitly in a separate system call, as necessary.         
			//EnvFlags.LogInMemory;// |  //Maintain transaction logs in memory rather than on disk.
			//EnvFlags.TxnNoSync |    //If set, Berkeley DB will not write or synchronously flush the log on transaction commit.
			//EnvFlags.TxnNoWriteSync;//If set, Berkeley DB will write, but will not synchronously flush, the log on transaction commit.
			
			
		private EnvFlagCollection envFlagCollection;
		private bool flagsInitialized;

		private DatabaseConfigs databaseConfigs = new DatabaseConfigs();

		public EnvironmentConfig()
		{
			TempDirectory = defaultFolder;
			LockStatistics = new LockStatistics();
			DeadlockDetection = new DeadlockDetection();
		}

		private void InitOpenFlags()
		{
			EnvOpenFlags openFlags = 0; 
			if (EnvOpenFlagCollection == null) //nothing 
			{
				return;
			}
			foreach (EnvironmentOpenFlags flag in EnvOpenFlagCollection)
			{
				switch (flag)
				{
					case EnvironmentOpenFlags.Create:
						openFlags |= EnvOpenFlags.Create;
						break;
					case EnvironmentOpenFlags.InitCDB:
						openFlags |= EnvOpenFlags.InitCDB;
						break;
					case EnvironmentOpenFlags.InitLock:
						openFlags |= EnvOpenFlags.InitLock;
						break;
					case EnvironmentOpenFlags.InitLog:
						openFlags |= EnvOpenFlags.InitLog;
						break;
					case EnvironmentOpenFlags.InitMPool:
						openFlags |= EnvOpenFlags.InitMPool;
						break;
					case EnvironmentOpenFlags.InitRep:
						openFlags |= EnvOpenFlags.InitRep;
						break;
					case EnvironmentOpenFlags.InitTxn:
						openFlags |= EnvOpenFlags.InitTxn;
						break;
					case EnvironmentOpenFlags.JoinEnv:
						openFlags |= EnvOpenFlags.JoinEnv;
						break;
					case EnvironmentOpenFlags.LockDown:
						openFlags |= EnvOpenFlags.LockDown;
						break;                    
					case EnvironmentOpenFlags.Private:
						openFlags |= EnvOpenFlags.Private;
						break;
					case EnvironmentOpenFlags.Recover:
						openFlags |= EnvOpenFlags.Recover;
						break;
					case EnvironmentOpenFlags.RecoverFatal:
						openFlags |= EnvOpenFlags.RecoverFatal;
						break;
					case EnvironmentOpenFlags.SystemMem:
						openFlags |= EnvOpenFlags.SystemMem;
						break;
					case EnvironmentOpenFlags.ThreadSafe:
						openFlags |= EnvOpenFlags.ThreadSafe;
						break;
					case EnvironmentOpenFlags.UseEnviron:
						openFlags |= EnvOpenFlags.UseEnviron;
						break;
					case EnvironmentOpenFlags.UseEnvironRoot:
						openFlags |= EnvOpenFlags.UseEnvironRoot;
						break;
                    case EnvironmentOpenFlags.Register:
                        openFlags |= EnvOpenFlags.Register;
                        break;
                    default:
						throw new ApplicationException("Unknown Env.OpenFlag '" + flag + "'");
				}
				
				openFlagsField = openFlags;
				
			}
		}

		private void InitFlags()
		{
			EnvFlags flags = 0;
			if (envFlagCollection == null)
			{
				return;
			}
			foreach (EnvironmentFlags flag in envFlagCollection)
			{
				switch (flag)
				{
					case EnvironmentFlags.Direct:
						flags |= EnvFlags.DbDirect;
						break;
					case EnvironmentFlags.DSync:
						flags |= EnvFlags.DbDSync;
						break;
					case EnvironmentFlags.DirectLog:
						flags |= EnvFlags.DbDirectLog;
						break;
					case EnvironmentFlags.LSync:
						flags |= EnvFlags.DbSyncLog;
						break;
					case EnvironmentFlags.TxnNoSync:
						flags |= EnvFlags.TxnNoSync;
						break;
					case EnvironmentFlags.TxnNoWriteSync:
						flags |= EnvFlags.TxnNoWriteSync;
						break;
					case EnvironmentFlags.LogInMemory:
						flags |= EnvFlags.LogInMemory;
						break;
                    case EnvironmentFlags.TxnNoWait:
                        flags |= EnvFlags.TxnNoWait;
                        break;
                    default:
						throw new ApplicationException("Unknown Env.Flag '" + flag + "'");
				}
				
				flagsField = flags;
				
			}
		}

		[XmlElement("CacheSize")]
		public EnvCacheSize CacheSize
		{
			get
			{
				return cacheSize;
			}
			set
			{
				cacheSize = value;
			}
		}

		[XmlElement("CacheTrickle")]
		public CacheTrickle CacheTrickle { get; set; }

		[XmlElement("Checkpoint")]
		public Checkpoint Checkpoint { get; set; }

		[XmlElement("Compact")]
		public Compact Compact { get; set; }

		[XmlElement("HomeDirectory")]
		public string HomeDirectory
		{
			get
			{
				return homeDirectoryField;
			}
			set
			{
				homeDirectoryField = value;
			}
		}

		[XmlIgnore]
		public EnvOpenFlags OpenFlags
		{
			get
			{
				if (!openFlagsInitialized)
				{
					InitOpenFlags();
					openFlagsInitialized = true;
				}
				return openFlagsField;
			}
			set
			{
				openFlagsField = value;
			}
		}

		[XmlArray("OpenFlags"), XmlArrayItem("OpenFlag")]
		public EnvOpenFlagCollection EnvOpenFlagCollection { get; set; }

		[XmlIgnore]
		public EnvFlags Flags
		{
			get
			{
				if (!flagsInitialized)
				{
					InitFlags();
					flagsInitialized = true;
				}
				return flagsField;
			}
			set
			{
				flagsField = value;
			}
		}

		[XmlArray("Flags")]
		[XmlArrayItem("Flag")]
		public EnvFlagCollection EnvFlagCollection
		{
			get
			{
				return envFlagCollection;
			}
			set
			{
				envFlagCollection = value;
			}
		}

		[XmlElement("MaxLockers")]
		public int MaxLockers { get; set; }

		[XmlElement("MaxLocks")]
		public int MaxLocks { get; set; }

		[XmlElement("SpinWaits")]
        public int SpinWaits { get; set; }

        [XmlElement("MaxLockObjects")]
        public int MaxLockObjects { get; set; }

		[XmlElement("MaxLogSize")]
		public int MaxLogSize { get; set; }

		[XmlElement("LogBufferSize")]
		public int LogBufferSize { get; set; }

		[XmlElement("MutexIncrement")]
		public UInt32 MutexIncrement { get; set; }

		[XmlElement("DeadlockDetection")]
		public DeadlockDetection DeadlockDetection { get; set; }

		[XmlElement("LockStatistics")]
		public LockStatistics LockStatistics { get; set; }

		[XmlElement("TempDirectory")]
		public string TempDirectory { get; set; }

		[XmlElement("VerboseDeadlock")]
		public bool VerboseDeadlock { get; set; }

		[XmlElement("VerboseRecovery")]
		public bool VerboseRecovery { get; set; }

		[XmlElement("VerboseWaitsFor")]
		public bool VerboseWaitsFor { get; set; }

		[XmlElement("VerifyOnStartup")]
		public bool VerifyOnStartup { get; set; }

		[XmlElement("DatabaseConfigs")]
		public DatabaseConfigs DatabaseConfigs
		{
			get
			{
				return databaseConfigs;
			}
			set
			{
				databaseConfigs = value;
			}
		}
	}

	/// <remarks/>
	public class EnvCacheSize
	{
		private int gigaBytes;
		private int bytes = 536870912;
		private int numberCaches = 1;

		
		[XmlElement("GigaBytes")]
		public int GigaBytes { get { return gigaBytes; } set { gigaBytes = value; } }
		[XmlElement("Bytes")]
		public int Bytes { get { return bytes; } set { bytes = value; } }
		[XmlElement("NumberCaches")]
		public int NumberCaches { get { return numberCaches; } set { numberCaches = value; } }
	}

	/// <remarks/>
	public class CacheTrickle : ITimerConfig
	{
		private int interval = 10000;//Milliseconds
		private int percentage = 70;

		[XmlElement("Enabled")]
		public bool Enabled { get; set; }

		[XmlElement("Interval")]
		public int Interval { get { return interval; } set { interval = value; } }
		[XmlElement("Percentage")]
		public int Percentage { get { return percentage; } set { percentage = value; } }
	}

	/// <remarks/>
	public class Checkpoint : ITimerConfig
	{
		private bool enabled ;
		private int interval = 10000;//Milliseconds
		private int logAgeMinutes ;
		private int logSizeKbyte;
		private bool force;
		private Backup backup;

		[XmlElement("Enabled")]
		public bool Enabled { get { return enabled; } set { enabled = value; } }
		[XmlElement("Interval")]
		public int Interval { get { return interval; } set { interval = value; } }
		[XmlElement("LogAgeMinutes")]
		public int LogAgeMinutes { get { return logAgeMinutes; } set { logAgeMinutes = value; } }
		[XmlElement("LogSizeKByte")]
		public int LogSizeKByte { get { return logSizeKbyte; } set { logSizeKbyte = value; } }
		[XmlElement("Force")]
		public bool Force { get { return force; } set { force = value; } }
		[XmlElement("Backup")]
		public Backup Backup { get { return backup; } set { backup = value; } }
	}

	/// <remarks/>
	public class Backup : ITimerConfig
	{
		private bool enabled;
		private bool copyLogs = true;
		private int interval = -1;//Milliseconds
		private int reinitializeInterval = -1; //Milliseconds
		private int reinitializeLogFileCount;
		private int dataCopyBufferKByte;
		private string directory = "Bkp";
		private BackupMethod method = BackupMethod.MpoolFile;

		[XmlElement("Enabled")]
		public bool Enabled { get { return enabled; } set { enabled = value; } }
		[XmlElement("CopyLogs")]
		public bool CopyLogs { get { return copyLogs; } set { copyLogs = value; } }
		[XmlElement("Directory")]
		public string Directory { get { return directory; } set { directory = value; } }
		[XmlElement("DataCopyBufferKByte")]
		public int DataCopyBufferKByte { get { return dataCopyBufferKByte; } set { dataCopyBufferKByte = value; } }
		[XmlElement("Interval")]
		public int Interval { get { return interval; } set { interval = value; } }
		[XmlElement("ReinitializeInterval")]
		public int ReinitializeInterval { get { return reinitializeInterval; } set { reinitializeInterval = value; } }
		[XmlElement("ReinitializeLogFileCount")]
		public int ReinitializeLogFileCount { get { return reinitializeLogFileCount; } set { reinitializeLogFileCount = value; } }
		[XmlElement("Method")]
		public BackupMethod Method { get { return method; } set { method = value; } }
	}

	/// <remarks/>
	public class DeadlockDetection : ITimerConfig
	{
		private bool enabled;
		private DeadlockDetectionMode mode = DeadlockDetectionMode.OnTransaction;
		private DeadlockDetectPolicy detectPolicy = DeadlockDetectPolicy.Random;
		private int timerInterval = 5000;//Milliseconds
		private Timeout timeout = new Timeout();

		[XmlElement("Enabled")]
		public bool Enabled { get { return enabled; } set { enabled = value; } }
		[XmlElement("Mode")]
		public DeadlockDetectionMode Mode { get { return mode; } set { mode = value; } }
		[XmlElement("DetectPolicy")]
		public DeadlockDetectPolicy DetectPolicy { get { return detectPolicy; } set { detectPolicy = value; } }
		[XmlElement("TimerInterval")]
		public int TimerInterval { get { return timerInterval; } set { timerInterval = value; } }
		int ITimerConfig.Interval { get { return timerInterval; } set { timerInterval = value; } }
		[XmlElement("Timeout")]
		public Timeout Timeout { get { return timeout; } set { timeout = value; } }
		
		public bool IsOnEveryTransaction()
		{
			return mode == DeadlockDetectionMode.OnTransaction;
		}
	}

	/// <remarks/>
	public class LockStatistics : ITimerConfig
	{
		private int timerInterval = 60000;//Milliseconds

		[XmlElement("Enabled")]
		public bool Enabled { get; set; }

		[XmlElement("TimerInterval")]
		public int TimerInterval { get { return timerInterval; } set { timerInterval = value; } }
		int ITimerConfig.Interval { get { return timerInterval; } set { timerInterval = value; } }
	}

	/// <remarks/>
	public class Compact : ITimerConfig
	{
		[XmlElement("Enabled")]
		public bool Enabled { get; set; }

		[XmlElement("Interval")]
		public int Interval { get; set; }
	}

	/// <remarks/>
	public class Timeout
	{
		private int interval = 1000000;//Microseconds

		[XmlElement("Interval")]
		public int Interval { get { return interval; } set { interval = value * 1000; } }

		[XmlElement("Flag")]
		public TimeoutFlags Flag { get; set; }
	}



	public class EnvOpenFlagCollection : KeyedCollection<string, EnvironmentOpenFlags>
	{
		protected override string GetKeyForItem(EnvironmentOpenFlags item)
		{
			return item.ToString();
		}

		public string GetGroupNameForId(string id)
		{
			if (Contains(id))
			{
				return this[id].ToString();
			}
			
			return null;
			
		}
	}

	public class EnvFlagCollection : KeyedCollection<string, EnvironmentFlags>
	{
		protected override string GetKeyForItem(EnvironmentFlags item)
		{
			return item.ToString();
		}

		public string GetGroupNameForId(string id)
		{
			if (Contains(id))
			{
				return this[id].ToString();
			}


			return null;

		}
	}

	/// <remarks/>
	[Flags]
	[Serializable]
	public enum EnvironmentOpenFlags
	{

		/// <remarks/>
		None = 1,

		/// <remarks/>
		JoinEnv = 2,

		/// <remarks/>
		InitCDB = 4,

		/// <remarks/>
		InitLock = 8,

		/// <remarks/>
		InitLog = 16,

		/// <remarks/>
		InitMPool = 32,

		/// <remarks/>
		InitRep = 64,

		/// <remarks/>
		InitTxn = 128,

		/// <remarks/>
		Recover = 256,

		/// <remarks/>
		RecoverFatal = 512,

		/// <remarks/>
		UseEnviron = 1024,

		/// <remarks/>
		UseEnvironRoot = 2048,

		/// <remarks/>
		Create = 4096,

		/// <remarks/>
		LockDown = 8192,

		/// <remarks/>
		Private = 16384,

		/// <remarks/>
		SystemMem = 32768,

		/// <remarks/>
		ThreadSafe = 65536,

        /// <remarks/>
        Register = 0x20000,
    }

	/// <remarks/>
	[Flags]
	[Serializable]
	public enum EnvironmentFlags
	{
		/// <remarks/>
		Direct = 1,
		DSync = 2,
		DirectLog = 3,
		LSync = 4,
		TxnNoSync = 5,
		TxnNoWriteSync = 6,
		LogInMemory = 7,
        TxnNoWait = 8,
	}

		/// <remarks/>
	[Flags]
	public enum DeadlockDetectionMode
	{
		/// <remarks/>
		OnTransaction = 1,
		OnTimer = 2,
		OnTimeout = 3,
	}

	
	public enum BackupMethod
	{
		MpoolFile = 1,
		Fstream
	}
}
