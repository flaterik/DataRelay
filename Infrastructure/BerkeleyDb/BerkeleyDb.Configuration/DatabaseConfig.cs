using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;
using BerkeleyDbWrapper;

namespace MySpace.BerkeleyDb.Configuration
{
	public class DatabaseConfig
	{
		public const string ErrorPrefix = "BDB";
		private int federationIndex;
		private int federationSize = 1;
		private string fileName;
		private int hashFillFactor;
		private uint hashSize;
		private string homeDirectory;
		private int id;
		private DbOpenFlags openFlags = DbOpenFlags.Create | DbOpenFlags.ThreadSafe;//DB_CREATE|DB_DIRTY_READ|DB_THREAD
		private DbFlags flags = DbFlags.None;
		private DbOpenFlagCollection dbOpenFlagCollection;
		private DbFlagCollection dbFlagCollection;
		private bool openFlagsInitialized;
		private bool flagsInitialized;
		private DatabaseType dbType = DatabaseType.BTree;
		private uint pageSize;
		private int recordLength;
		private int maxDeadlockRetries = 1;
		private DatabaseTransactionMode transactionMode = DatabaseTransactionMode.None;
		private DatabaseCompact compact;

		private static string GetFilePath(string directory, string fileName)
		{
			if (!string.IsNullOrEmpty(directory) && !fileName.Contains(":"))
			{
				return string.Format("{0}\\{1}", directory, fileName);
			}
			return fileName;

		}

		private void InitOpenFlags()
		{
			if (dbOpenFlagCollection == null)
			{
				return;
			}
			foreach (DatabaseOpenFlags flag in dbOpenFlagCollection)
			{
				switch (flag)
				{
					case DatabaseOpenFlags.AutoCommit:
						openFlags |= DbOpenFlags.AutoCommit;
						break;
					case DatabaseOpenFlags.Create:
						openFlags |= DbOpenFlags.Create;
						break;
					case DatabaseOpenFlags.DirtyRead:
						openFlags |= DbOpenFlags.DirtyRead;
						break;
					case DatabaseOpenFlags.Exclusive:
						openFlags |= DbOpenFlags.Exclusive;
						break;
					case DatabaseOpenFlags.NoMemoryMap:
						openFlags |= DbOpenFlags.NoMemoryMap;
						break;
					case DatabaseOpenFlags.None:
						openFlags |= DbOpenFlags.None;
						break;
					case DatabaseOpenFlags.ReadOnly:
						openFlags |= DbOpenFlags.ReadOnly;
						break;
					case DatabaseOpenFlags.ThreadSafe:
						openFlags |= DbOpenFlags.ThreadSafe;
						break;
					case DatabaseOpenFlags.Truncate:
						openFlags |= DbOpenFlags.Truncate;
						break;
					default:
						throw new ApplicationException("Unknown Db.OpenFlag '" + flag + "'");
				}
			}
		}

		private void InitFlags()
		{
			DbFlags initFlags = DbFlags.None;
			if (dbFlagCollection == null)
			{
				return;
			}
			foreach (DatabaseFlags flag in dbFlagCollection)
			{
				switch (flag)
				{
					case DatabaseFlags.None:
						initFlags |= DbFlags.None;
						break;
					case DatabaseFlags.ChkSum:
						initFlags |= DbFlags.ChkSum;
						break;
					case DatabaseFlags.Dup:
						initFlags |= DbFlags.Dup;
						break;
					case DatabaseFlags.DupSort:
						initFlags |= DbFlags.DupSort;
						break;
					case DatabaseFlags.Encrypt:
						initFlags |= DbFlags.Encrypt;
						break;
					case DatabaseFlags.InOrder:
						initFlags |= DbFlags.InOrder;
						break;
					case DatabaseFlags.RecNum:
						initFlags |= DbFlags.RecNum;
						break;
					case DatabaseFlags.Renumber:
						initFlags |= DbFlags.Renumber;
						break;
					case DatabaseFlags.RevSplitOff:
						initFlags |= DbFlags.RevSplitOff;
						break;
					case DatabaseFlags.Snapshot:
						initFlags |= DbFlags.Snapshot;
						break;
					case DatabaseFlags.TxnNotDurable:
						initFlags |= DbFlags.TxnNotDurable;
						break;
					default:
						throw new ApplicationException("Unknown DB Flag '" + flag + "'");
				}
				
				flags = initFlags;
			}
		}

		public DatabaseConfig()
		{
		}

		public DatabaseConfig(int id)
		{
			this.id = id;
		}

		[XmlElement("FederationIndex")]
		public int FederationIndex { get { return federationIndex; } set { federationIndex = value; } }

		[XmlElement("FederationSize")]
		public int FederationSize { get { return federationSize; } set { federationSize = value; } }

		[XmlElement("FileName")]
		public string FileName
		{
			get
			{
				if (String.IsNullOrEmpty(fileName))
				{
					return null;
				}

				if (id == -1)
				{
					return GetFilePath(homeDirectory, fileName);
				}
				string filePath = GetFilePath(homeDirectory, fileName) + id;
				if (federationSize < 2)
				{
					return filePath;
				}
				string fedSize = federationSize.ToString();
				return filePath + federationIndex.ToString().PadLeft(fedSize.Length, '0');

			}
			set { fileName = value; }
		}

		public string HomeDirectory { get { return homeDirectory; } set { homeDirectory = value; } }

		[XmlElement("Compact")]
		public DatabaseCompact Compact
		{
			get { return compact; }
			set { compact = value; }
		}

		[XmlAttribute("Id")]
		public int Id { get { return id; } set { id = value; } }

		[XmlIgnore]
		public DbOpenFlags OpenFlags
		{
			get
			{
				if (!openFlagsInitialized)
				{
					InitOpenFlags();
					openFlagsInitialized = true;
				}
				return openFlags;
			}
			set
			{
				openFlags = value;
			}
		}

		[XmlArray("OpenFlags")]
		[XmlArrayItem("OpenFlag")]
		public DbOpenFlagCollection DbOpenFlagCollection
		{
			get
			{
				return dbOpenFlagCollection;
			}
			set
			{
				dbOpenFlagCollection = value;
			}
		}

		[XmlIgnore]
		public DbFlags Flags
		{
			get
			{
				if (!flagsInitialized)
				{
					InitFlags();
					flagsInitialized = true;
				}
				return flags;
			}
			set
			{
				flags = value;
			}
		}

		[XmlArray("Flags")]
		[XmlArrayItem("Flag")]
		public DbFlagCollection DbFlagCollection
		{
			get
			{
				return dbFlagCollection;
			}
			set
			{
				dbFlagCollection = value;
			}
		}

		[XmlElement("PageSize")]
		public uint PageSize { get { return pageSize; } set { pageSize = value; } }

		[XmlElement("Type")]
		public DatabaseType Type { get { return dbType; } set { dbType = value; } }

		[XmlElement("HashFillFactor")]
		public int HashFillFactor { get { return hashFillFactor; } set { hashFillFactor = value; } }

		[XmlElement("HashSize")]
		public uint HashSize { get { return hashSize; } set { hashSize = value; } }

		[XmlElement("RecordLength")]
		public int RecordLength { get { return recordLength; } set { recordLength = value; } }

		[XmlElement("MaxDeadlockRetries")]
		public int MaxDeadlockRetries { get { return maxDeadlockRetries; } set { maxDeadlockRetries = value; } }

		[XmlElement("TransactionMode")]
		public DatabaseTransactionMode TransactionMode { get { return transactionMode; } set { transactionMode = value; } }
		
		public DatabaseConfig Clone(int newId)
		{
			DatabaseConfig newDbConfig = new DatabaseConfig(newId)
										 {
											 FederationIndex = federationIndex,
											 FederationSize = federationSize,
											 FileName = fileName,
											 HomeDirectory = homeDirectory,
											 DbOpenFlagCollection = dbOpenFlagCollection,
											 DbFlagCollection = dbFlagCollection,
											 PageSize = pageSize,
											 Type = dbType,
											 HashFillFactor = hashFillFactor,
											 HashSize = hashSize,
											 RecordLength = recordLength,
											 MaxDeadlockRetries = maxDeadlockRetries,
											 TransactionMode = transactionMode
										 };
			
			if (compact != null)
			{
				newDbConfig.Compact = new DatabaseCompact
									  {
										  Enabled = compact.Enabled,
										  MaxPages = compact.MaxPages,
										  Percentage = compact.Percentage,
										  Timeout = compact.Timeout
									  };
			}
			return newDbConfig;
		}

		public static int CalculateFederationIndex(int objectId, int federationSize)
		{
			if (federationSize <= 0)
			{
				federationSize = 1;
			}
			//if the id is negative, use the inverse, unless it's minvalue, which is one less than
			//-maxvalue. In that case use 0.            
			return (objectId > 0 ?
				objectId : (objectId == Int32.MinValue ? 0 : -objectId)) % federationSize;                    
		}

		public void SetFederationIndex(int objectId)
		{
			federationIndex = CalculateFederationIndex(objectId, federationSize);
		}

		[XmlIgnore]
		public bool CheckRaceCondition { get; set; }
	}


	public class DbOpenFlagCollection : KeyedCollection<string, DatabaseOpenFlags>
	{
		protected override string GetKeyForItem(DatabaseOpenFlags item)
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

	public class DbFlagCollection : KeyedCollection<string, DatabaseFlags>
	{
		protected override string GetKeyForItem(DatabaseFlags item)
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
	public enum DatabaseOpenFlags
	{

		/// <remarks/>
		None = 1,

		/// <remarks/>
		Create = 2,

		/// <remarks/>
		NoMemoryMap = 4,

		/// <remarks/>
		ReadOnly = 8,

		/// <remarks/>
		ThreadSafe = 16,

		/// <remarks/>
		Truncate = 32,

		/// <remarks/>
		Exclusive = 64,

		/// <remarks/>
		AutoCommit = 128,

		/// <remarks/>
		DirtyRead = 256,
	}

	/// <remarks/>
	public enum DatabaseFlags
	{
		None = 0,
		ChkSum = 1,
		Dup = 2,
		DupSort = 3,
		Encrypt = 4,
		InOrder = 5,
		RecNum = 6,
		Renumber = 7,
		RevSplitOff = 8,
		Snapshot = 9,
		TxnNotDurable = 10,
	}

	/// <remarks/>
	public enum DatabaseTransactionMode
	{
		None = 0,
		PerCall = 1,
	}

	
	public class DatabaseCompact
	{
		private bool enabled;
		private int percentage = 50;
		private int maxPages;
		private int timeout;

		[XmlElement("Enabled")]
		public bool Enabled { get { return enabled; } set { enabled = value; } }
		[XmlElement("Percentage")]
		public int Percentage { get { return percentage; } set { percentage = value; } }
		[XmlElement("MaxPages")]
		public int MaxPages { get { return maxPages; } set { maxPages = value; } }
		[XmlElement("Timeout")]
		public int Timeout { get { return timeout; } set { timeout = value; } }
	}
}
