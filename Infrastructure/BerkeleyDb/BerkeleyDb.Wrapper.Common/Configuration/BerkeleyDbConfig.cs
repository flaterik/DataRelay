using System.Xml.Serialization;

using BerkeleyDbWrapper;

namespace MySpace.BerkeleyDb.Configuration
{
	/// <summary>
	/// Provides the configuration of a Berkeley Db instance.
	/// </summary>
	[XmlRoot("BerkeleyDbConfig", Namespace = "http://myspace.com/BerkeleyDbConfig.xsd")]
	public class BerkeleyDbConfig
	{
		private short minTypeId = 1;
		private short maxTypeId = 100;
		private DbLoadMode dbLoadMode = DbLoadMode.Lazy;
		private RecoveryFailureAction recoveryFailureAction = RecoveryFailureAction.ThrowException;
		private int bufferSize = 1024;
		private int maxPoolItemReuse = 10;
		private StatTimer statTimer = new StatTimer();
		private ThrottleThreads throttleThreads = new ThrottleThreads();
		private long shutdownWindow;
		private bool allowPartialDatabaseRecovery;
		private EnvironmentConfig envConfig = new EnvironmentConfig();

		[XmlElement("MinTypeId")]
		public short MinTypeId { get { return minTypeId; } set { minTypeId = value; } }

		[XmlElement("MaxTypeId")]
		public short MaxTypeId { get { return maxTypeId; } set { maxTypeId = value; } }

		[XmlElement("DbLoadMode")]
		public DbLoadMode DbLoadMode { get { return dbLoadMode; } set { dbLoadMode = value; } }

		[XmlElement("RecoveryFailureAction")]
		public RecoveryFailureAction RecoveryFailureAction { get { return recoveryFailureAction; } set { recoveryFailureAction = value; } }

		[XmlElement("BufferSize")]
		public int BufferSize { get { return bufferSize; } set { bufferSize = value; } }

		[XmlElement("MaxPoolItemReuse")]
		public int MaxPoolItemReuse { get { return maxPoolItemReuse; } set { maxPoolItemReuse = value; } }

		[XmlElement("StatTimer")]
		public StatTimer StatTimer { get { return statTimer; } set { statTimer = value; } }

		[XmlElement("ThrottleThreads")]
		public ThrottleThreads ThrottleThreads { get { return throttleThreads; } set { throttleThreads = value; } }

		[XmlElement("ShutdownWindow")]
		public long ShutdownWindow { get { return shutdownWindow; } set { shutdownWindow = value; } }

		[XmlElement("AllowPartialDatabaseRecovery")]
		public bool AllowPartialDatabaseRecovery { get { return allowPartialDatabaseRecovery; } set { allowPartialDatabaseRecovery = value; } }

		[XmlElement("EnvironmentConfig")]
		public EnvironmentConfig EnvironmentConfig { get { return envConfig; } set { envConfig = value; } }

		public int GetDatabaseCount()
		{
			//int typeCount = maxTypeId - minTypeId;
			int dbCount = 0;
			DatabaseConfigs dbConfigs = envConfig.DatabaseConfigs;
			for (int i = minTypeId; i <= maxTypeId; i++)
			{
				dbCount += dbConfigs.GetFederationSize(i);
			}
			return dbCount;
		}
	}

	/// <remarks/>
	public class ThrottleThreads
	{
		private int threadCount = 1;
		private int waitTimeout = 6000;

		[XmlElement("Enabled")]
		public bool Enabled { get; set; }
		[XmlElement("ThreadCount")]
		public int ThreadCount { get { return threadCount; } set { threadCount = value; } }
		[XmlElement("WaitTimeout")]
		public int WaitTimeout { get { return waitTimeout; } set { waitTimeout = value; } }
	}

	/// <remarks/>
	public class StatTimer : ITimerConfig
	{
		private int interval = 10000; //Milliseconds
		private DbStatFlags statFlag = DbStatFlags.FastStat;

		[XmlElement("Enabled")]
		public bool Enabled { get; set; }
		[XmlElement("Interval")]
		public int Interval { get { return interval; } set { interval = value; } }
		[XmlElement("StatFlag")]
		public DbStatFlags StatFlag { get { return statFlag; } set { statFlag = value; } }

	}

	/// <remarks/>
	public enum DbLoadMode
	{
		OnStartup = 0,
		Lazy = 1,
	}

	/// <remarks/>
	public enum RecoveryFailureAction
	{
		RemoveAllFiles = 0,
		ThrowException = 1,
	}

	
}
