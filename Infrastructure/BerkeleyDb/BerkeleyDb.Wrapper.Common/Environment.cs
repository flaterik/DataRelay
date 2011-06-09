using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using MySpace.BerkeleyDb.Configuration;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// The abstract base class for a managed wrapper for a Berkeley Db environment.
	/// </summary>
	public abstract class Environment : IDisposable
	{
		/// <summary>
		/// The environment flags that must be set before 
		/// </summary>
		protected const EnvFlags MustPreOpenFlags = EnvFlags.LogInMemory;

		// Static Fields
		private static readonly Environment _pseudoSingleton = ConcreteFactory.Create<Environment>();

		// Events
		/// <summary>
		/// The handler delegate for <see cref="MessageCall"/>.
		/// </summary>
		protected MessageEventHandler MessageCallHandler;
		/// <summary>
		/// Occurs when a message arises.
		/// </summary>
		public event MessageEventHandler MessageCall
		{
			add { MessageCallHandler += value; }
			remove { MessageCallHandler -= value; }
		}

		/// <summary>
		/// The handler delegate for <see cref="PanicCall"/>.
		/// </summary>
		protected PanicEventHandler PanicCallHandler;
		/// <summary>
		/// Occurs when a panic state happens.
		/// </summary>
		public event PanicEventHandler PanicCall
		{
			add { PanicCallHandler += value; }
			remove { PanicCallHandler -= value; }
		}

		// Methods
		/// <summary>
		/// Cancels the pending transactions.
		/// </summary>
		public abstract void CancelPendingTransactions();
		/// <summary>
		/// Checkpoints the environment.
		/// </summary>
		/// <param name="sizeKbytes">The log size in kbytes needed for checkpointing.</param>
		/// <param name="ageMinutes">The log age in minutes needed for checkpointing.</param>
		/// <param name="force">if <see langword="true"/> then force a checkpoint regardless of log size or age.</param>
		public abstract void Checkpoint(int sizeKbytes, int ageMinutes, bool force);
		/// <summary>
		/// Deletes the unused logs.
		/// </summary>
		public abstract void DeleteUnusedLogs();
		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public abstract void Dispose();
		/// <summary>
		/// Flushes the logs to disk.
		/// </summary>
		[HandleProcessCorruptedStateExceptions]
		public abstract void FlushLogsToDisk();
		/// <summary>
		/// Gets all log files.
		/// </summary>
		/// <returns>A <see cref="List{String}"/> of the log file names.</returns>
		public abstract List<string> GetAllLogFiles();
		/// <summary>
		/// Gets all log files.
		/// </summary>
		/// <param name="startIdx">The start index.</param>
		/// <param name="endIdx">The end index.</param>
		/// <returns>A <see cref="List{String}"/> of the log file names.</returns>
		public abstract List<string> GetAllLogFiles(int startIdx, int endIdx);
		/// <summary>
		/// Gets the current log number.
		/// </summary>
		/// <returns>The <see cref="Int32"/> log number.</returns>
		public abstract int GetCurrentLogNumber();
		/// <summary>
		/// Gets the data files for archiving.
		/// </summary>
		/// <returns>A <see cref="List{String}"/> of data file names needed for archiving.</returns>
		public abstract List<string> GetDataFilesForArchiving();
		/// <summary>
		/// Gets the flags.
		/// </summary>
		/// <returns>The <see cref="EnvFlags"/>.</returns>
		public abstract EnvFlags GetFlags();
		/// <summary>
		/// Gets the home directory.
		/// </summary>
		/// <returns>The <see cref="String"/> home directory path.</returns>
		public abstract string GetHomeDirectory();
		/// <summary>
		/// Gets the last checkpointed log number.
		/// </summary>
		/// <returns>The <see cref="Int32"/> number of the last checkpointed log.</returns>
		public abstract int GetLastCheckpointLogNumber();
		/// <summary>
		/// Gets the lock statistics.
		/// </summary>
		public abstract void GetLockStatistics();
		/// <summary>
		/// Gets the log file name from log number.
		/// </summary>
		/// <param name="logNumber">The log number.</param>
		/// <returns>The log file name.</returns>
		public abstract string GetLogFileNameFromNumber(int logNumber);
		/// <summary>
		/// Gets the maximum number of lockers.
		/// </summary>
		/// <returns>The maximum number of lockers.</returns>
		public abstract int GetMaxLockers();
		/// <summary>
		/// Gets the maximum number of locker objects.
		/// </summary>
		/// <returns>The maximum number of locker objects.</returns>
		public abstract int GetMaxLockObjects();
		/// <summary>
		/// Gets the maximum number of locks.
		/// </summary>
		/// <returns>The maximum number of locks.</returns>
		public abstract int GetMaxLocks();
		/// <summary>
		/// Gets the open flags.
		/// </summary>
		/// <returns>The <see cref="EnvOpenFlags"/>.</returns>
		public abstract EnvOpenFlags GetOpenFlags();
		/// <summary>
		/// Gets the timeout.
		/// </summary>
		/// <param name="timeoutFlag">The timeout flag specifying the type of timeout.</param>
		/// <returns>The specified timeout.</returns>
		public abstract int GetTimeout(TimeoutFlags timeoutFlag);
		/// <summary>
		/// Gets the unused log files.
		/// </summary>
		/// <returns>A <see cref="List{String}"/> of names of unused log files.</returns>
		public abstract List<string> GetUnusedLogFiles();
		/// <summary>
		/// Gets whether deadlocking is verbose.
		/// </summary>
		/// <returns><see langword="true"/> if deadlocking is verbose; otherwise <see langword="false"/>.</returns>
		public abstract bool GetVerboseDeadlock();
		/// <summary>
		/// Gets whether recorvery is verbose.
		/// </summary>
		/// <returns><see langword="true"/> if recovery is verbose; otherwise <see langword="false"/>.</returns>
		public abstract bool GetVerboseRecovery();
		/// <summary>
		/// Gets whether waiting is verbose.
		/// </summary>
		/// <returns><see langword="true"/> if waiting is verbose; otherwise <see langword="false"/>.</returns>
		public abstract bool GetVerboseWaitsFor();
		/// <summary>
		/// Detects if there's a deadlock.
		/// </summary>
		/// <param name="detectPolicy">The lock detection policy.</param>
		/// <returns>0 if successful; otherwise some error code.</returns>
		public abstract int LockDetect(DeadlockDetectPolicy detectPolicy);
		/// <summary>
		/// Flushes pages from cache.
		/// </summary>
		/// <param name="percent">The minimum percentage of pages that must be clean after the flush.</param>
		/// <returns>The number of pages written.</returns>
		public abstract int MempoolTrickle(int percent);
		/// <summary>
		/// Opens a database.
		/// </summary>
		/// <param name="dbConfig">The database configuration.</param>
		/// <returns>The new <see cref="Database"/>.</returns>
		public abstract Database OpenDatabase(DatabaseConfig dbConfig);
		/// <summary>
		/// Prints the memory cache statistics.
		/// </summary>
		/// <remarks>Statistics are printed as a message output to
		/// <see cref="MessageCall"/>.</remarks>
		public abstract void PrintCacheStats();
		/// <summary>
		/// Prints the lock statistics.
		/// </summary>
		/// <remarks>Statistics are printed as a message output to
		/// <see cref="MessageCall"/>.</remarks>
		public abstract void PrintLockStats();
		/// <summary>
		/// Prints the default statistics.
		/// </summary>
		/// <remarks>Statistics are printed as a message output to
		/// <see cref="MessageCall"/>.</remarks>
		public abstract void PrintStats();
		/// <summary>
		/// Removes a database.
		/// </summary>
		/// <param name="dbPath">The path to the backing database file.</param>
		public abstract void RemoveDatabase(string dbPath);
		/// <summary>
		/// Clears  flags.
		/// </summary>
		/// <param name="flags">The <see cref="EnvFlags"/> to clear.</param>
		public abstract void RemoveFlags(EnvFlags flags);
		/// <summary>
		/// Sets flags.
		/// </summary>
		/// <param name="flags">The <see cref="EnvFlags"/> to set.</param>
		public abstract void SetFlags(EnvFlags flags);
		/// <summary>
		/// Sets a timeout.
		/// </summary>
		/// <param name="microseconds">The duration of the timeout in microseconds.</param>
		/// <param name="timeoutFlag">The type of timeout.</param>
		public abstract void SetTimeout(int microseconds, TimeoutFlags timeoutFlag);
		/// <summary>
		/// Sets the verbosity of deadlocks.
		/// </summary>
		/// <param name="verboseDeadlock"><see langword="true"/> to make deadlock verbose; otherwise
		/// <see langword="false"/>.</param>
		public abstract void SetVerboseDeadlock(bool verboseDeadlock);
		/// <summary>
		/// Sets the verbosity of recovery.
		/// </summary>
		/// <param name="verboseRecovery"><see langword="true"/> to make reovery verbose; otherwise
		/// <see langword="false"/>.</param>
		public abstract void SetVerboseRecovery(bool verboseRecovery);
		/// <summary>
		/// Sets the verbosity of waiting.
		/// </summary>
		/// <param name="verboseWaitsFor"><see langword="true"/> to make waiting verbose; otherwise
		/// <see langword="false"/>.</param>
		public abstract void SetVerboseWaitsFor(bool verboseWaitsFor);
		/// <summary>
		/// Implementation for <see cref="Remove"/> using pseudosingleton.
		/// </summary>
		/// <param name="dbHome">The home directory of the environment to remove.</param>
		/// <param name="openFlags">The <see cref="EnvOpenFlags"/> to use for the removal.</param>
		/// <param name="force">if <see langword="true"/> then try to remove the environment even if
		/// other processes are using it; otherwise don't.</param>
		protected abstract void DoRemove(string dbHome, EnvOpenFlags openFlags, bool force);

		// static methods
		/// <summary>
		/// Removes an environment.
		/// </summary>
		/// <param name="dbHome">The home directory of the environment to remove.</param>
		/// <param name="openFlags">The <see cref="EnvOpenFlags"/> to use for the removal.</param>
		/// <param name="force">if <see langword="true"/> then try to remove the environment even if
		/// other processes are using it; otherwise don't.</param>
		public static void Remove(string dbHome, EnvOpenFlags openFlags, bool force)
		{
			_pseudoSingleton.DoRemove(dbHome, openFlags, force);
		}
		/// <summary>
		/// Creates an environment.
		/// </summary>
		/// <param name="config">The <see cref="EnvironmentConfig"/> to configures the new
		/// environment.</param>
		/// <returns>A new <see cref="Environment"/></returns>
		public static Environment Create(EnvironmentConfig config)
		{
			return ConcreteFactory.Create<EnvironmentConfig, Environment>(config);
		}
		/// <summary>
		/// Creates an environment.
		/// </summary>
		/// <param name="dbHome">The home directory of the new environment.</param>
		/// <param name="flags">The <see cref="EnvOpenFlags"/> used to open the new
		/// environment.</param>
		/// <returns>A new <see cref="Environment"/></returns>
		public static Environment Create(string dbHome, EnvOpenFlags flags)
		{
			return ConcreteFactory.Create<string, EnvOpenFlags, Environment>(dbHome, flags);
		}

		// Properties
		/// <summary>
		/// Gets or sets the lock stat current max locker id.
		/// </summary>
		/// <value>The lock stat current max locker id.</value>
		public PerformanceCounter LockStatCurrentMaxLockerId { get; set; }
		/// <summary>
		/// Gets or sets the lock stat last locker id.
		/// </summary>
		/// <value>The lock stat last locker id.</value>
		public PerformanceCounter LockStatLastLockerId { get; set; }
		/// <summary>
		/// Gets or sets the lock stat lockers no wait.
		/// </summary>
		/// <value>The lock stat lockers no wait.</value>
		public PerformanceCounter LockStatLockersNoWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat lockers wait.
		/// </summary>
		/// <value>The lock stat lockers wait.</value>
		public PerformanceCounter LockStatLockersWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat lock hash len.
		/// </summary>
		/// <value>The lock stat lock hash len.</value>
		public PerformanceCounter LockStatLockHashLen { get; set; }
		/// <summary>
		/// Gets or sets the lock stat lock no wait.
		/// </summary>
		/// <value>The lock stat lock no wait.</value>
		public PerformanceCounter LockStatLockNoWait { get; set; }
		/// <summary>
		/// Gets or sets the size of the lock stat lock region.
		/// </summary>
		/// <value>The size of the lock stat lock region.</value>
		public PerformanceCounter LockStatLockRegionSize { get; set; }
		/// <summary>
		/// Gets or sets the lock stat locks no wait.
		/// </summary>
		/// <value>The lock stat locks no wait.</value>
		public PerformanceCounter LockStatLocksNoWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat locks wait.
		/// </summary>
		/// <value>The lock stat locks wait.</value>
		public PerformanceCounter LockStatLocksWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat lock timeout.
		/// </summary>
		/// <value>The lock stat lock timeout.</value>
		public PerformanceCounter LockStatLockTimeout { get; set; }
		/// <summary>
		/// Gets or sets the lock stat lock wait.
		/// </summary>
		/// <value>The lock stat lock wait.</value>
		public PerformanceCounter LockStatLockWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat max lockers possible.
		/// </summary>
		/// <value>The lock stat max lockers possible.</value>
		public PerformanceCounter LockStatMaxLockersPossible { get; set; }
		/// <summary>
		/// Gets or sets the lock stat max lock objects possible.
		/// </summary>
		/// <value>The lock stat max lock objects possible.</value>
		public PerformanceCounter LockStatMaxLockObjectsPossible { get; set; }
		/// <summary>
		/// Gets or sets the lock stat max locks possible.
		/// </summary>
		/// <value>The lock stat max locks possible.</value>
		public PerformanceCounter LockStatMaxLocksPossible { get; set; }
		/// <summary>
		/// Gets or sets the lock stat max number lockers at one time.
		/// </summary>
		/// <value>The lock stat max number lockers at one time.</value>
		public PerformanceCounter LockStatMaxNumberLockersAtOneTime { get; set; }
		/// <summary>
		/// Gets or sets the lock stat max number locks at one time.
		/// </summary>
		/// <value>The lock stat max number locks at one time.</value>
		public PerformanceCounter LockStatMaxNumberLocksAtOneTime { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number current lockers.
		/// </summary>
		/// <value>The lock stat number current lockers.</value>
		public PerformanceCounter LockStatNumberCurrentLockers { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number current lock objects.
		/// </summary>
		/// <value>The lock stat number current lock objects.</value>
		public PerformanceCounter LockStatNumberCurrentLockObjects { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number current lock objects at one time.
		/// </summary>
		/// <value>The lock stat number current lock objects at one time.</value>
		public PerformanceCounter LockStatNumberCurrentLockObjectsAtOneTime { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number current locks.
		/// </summary>
		/// <value>The lock stat number current locks.</value>
		public PerformanceCounter LockStatNumberCurrentLocks { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number dead locks.
		/// </summary>
		/// <value>The lock stat number dead locks.</value>
		public PerformanceCounter LockStatNumberDeadLocks { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number lock modes.
		/// </summary>
		/// <value>The lock stat number lock modes.</value>
		public PerformanceCounter LockStatNumberLockModes { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number locks down graded.
		/// </summary>
		/// <value>The lock stat number locks down graded.</value>
		public PerformanceCounter LockStatNumberLocksDownGraded { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number locks released.
		/// </summary>
		/// <value>The lock stat number locks released.</value>
		public PerformanceCounter LockStatNumberLocksReleased { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number locks requested.
		/// </summary>
		/// <value>The lock stat number locks requested.</value>
		public PerformanceCounter LockStatNumberLocksRequested { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number locks upgraded.
		/// </summary>
		/// <value>The lock stat number locks upgraded.</value>
		public PerformanceCounter LockStatNumberLocksUpgraded { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number lock timeouts.
		/// </summary>
		/// <value>The lock stat number lock timeouts.</value>
		public PerformanceCounter LockStatNumberLockTimeouts { get; set; }
		/// <summary>
		/// Gets or sets the lock stat number TXN timeouts.
		/// </summary>
		/// <value>The lock stat number TXN timeouts.</value>
		public PerformanceCounter LockStatNumberTxnTimeouts { get; set; }
		/// <summary>
		/// Gets or sets the lock stat objects no wait.
		/// </summary>
		/// <value>The lock stat objects no wait.</value>
		public PerformanceCounter LockStatObjectsNoWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat objects wait.
		/// </summary>
		/// <value>The lock stat objects wait.</value>
		public PerformanceCounter LockStatObjectsWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat region no wait.
		/// </summary>
		/// <value>The lock stat region no wait.</value>
		public PerformanceCounter LockStatRegionNoWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat region wait.
		/// </summary>
		/// <value>The lock stat region wait.</value>
		public PerformanceCounter LockStatRegionWait { get; set; }
		/// <summary>
		/// Gets or sets the lock stat TXN timeout.
		/// </summary>
		/// <value>The lock stat TXN timeout.</value>
		public PerformanceCounter LockStatTxnTimeout { get; set; }
		/// <summary>
		/// Gets the pre open set flags.
		/// </summary>
		/// <value>The pre open set flags.</value>
		public static EnvFlags PreOpenSetFlags { get { return MustPreOpenFlags; } }
		/// <summary>
		/// Gets or sets the spin waits.
		/// </summary>
		/// <value>The spin waits.</value>
		public abstract int SpinWaits { get; set; }

		// Nested Types
		/// <summary>
		/// 
		/// </summary>
		public delegate void MessageEventHandler(object sender, BerkeleyDbMessageEventArgs e);

		/// <summary>
		/// 
		/// </summary>
		public delegate void PanicEventHandler(object sender, BerkeleyDbPanicEventArgs e);
	}
}
