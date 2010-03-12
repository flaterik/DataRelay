using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

using BerkeleyDbWrapper;
using MySpace.BerkeleyDb.Configuration;


namespace MySpace.BerkeleyDb.Facade
{
	/// <summary>
	/// Backups database for restoration. Backups are not necessarily complete in themselves, since
	/// databases without transactions in logs won't be copied. That's why Restore copies the files
	/// back to the home directory without deleting existing files, just copying over as necessary.
	/// </summary>
	public class BackupSet
	{
		internal BackupSet(BerkeleyDbStorage storage, string backupDir, Backup backupConfig)
			:
			this(storage.Environment.GetHomeDirectory(), backupDir, backupConfig)
		{
			this.storage = storage;
		}


		internal BackupSet(string homeDir, string backupDir, Backup backupConfig)
		{
			if (String.IsNullOrEmpty(backupDir))
			{
				throw new ApplicationException("No backup directory specified");
			}
			isInitialized = false;
			dataFilesCopied = new List<string>();
			logFilesCopied = new List<string>();
			lastCheckpointLogNumber = -1;
			copyLogFiles = backupConfig.CopyLogs;
			backupMethod = backupConfig.Method;
			dataCopyBufferSize = backupConfig.DataCopyBufferKByte * 1024;
			if (string.IsNullOrEmpty(backupDir))
			{
				backupDir = backupConfig.Directory;
			}
			this.homeDir = homeDir;
			this.backupDir = Path.Combine(homeDir, backupDir);
		}

		public bool IsInitialized { get { return isInitialized; } }
		public DateTime FirstBackupTime { get { return firstBackupTime; } }
		public DateTime LastUpdateTime { get { return lastUpdateTime; } }
		public string HomeDirectory { get { return homeDir; } }
		public string BackupDirectory { get { return backupDir; } }
		public byte[] CopyBuffer { get { return copyBuffer; } set { copyBuffer = value; } }

		static BackupSet()
		{
			byte[] buf = Encoding.BigEndianUnicode.GetBytes(new[] {
				Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar });
			StringBuilder sbd = new StringBuilder();
			const string byteFormat = "{0:X2}";
			sbd.Append("[\\u");
			sbd.AppendFormat(byteFormat, buf[0]);
			sbd.AppendFormat(byteFormat, buf[1]);
			sbd.Append("\\u");
			sbd.AppendFormat(byteFormat, buf[2]);
			sbd.AppendFormat(byteFormat, buf[3]);
			sbd.Append("]+");
		}
		
		#region Logging
		readonly StringBuilder sbdLog = new StringBuilder();
		void Log(string methodName, string format, params object[] values)
		{
			if (!BerkeleyDbStorage.Log.IsDebugEnabled) return;
			lock (sbdLog)
			{
				sbdLog.Length = 0;
				sbdLog.AppendFormat("{0}() ", methodName);
				sbdLog.AppendFormat(format, values);
				BerkeleyDbStorage.Log.Debug(sbdLog);
			}
		}
		
		void LogCompleted(string methodName)
		{
			Log(methodName, "completed");
		}
		
		void LogError(string methodName, Exception exc)
		{
			if (!BerkeleyDbStorage.Log.IsErrorEnabled) return;
			lock (sbdLog)
			{
				sbdLog.Length = 0;
				sbdLog.AppendFormat("{0}() Error", 
					 methodName);
				BerkeleyDbStorage.Log.Error(sbdLog, exc);
			}
		}
		#endregion

		public void Backup()
		{
			Log("Backup", "Starting {0} backup to {1}", IsInitialized ? string.Empty : "noninitialized",
				backupDir);
			
			try
			{
				if (!IsInitialized)
				{
					ClearDirectory(backupDir);
				}

				// Get log of last checkpoint for last checkpoint
				int newCheckpointLogNumber = storage.Environment.GetLastCheckpointLogNumber();
				// Get list of unused log files
				unusedLogFiles = storage.Environment.GetUnusedLogFiles();
				Log("Backup", "Old log number = {0}, new = {1}", lastCheckpointLogNumber,
					newCheckpointLogNumber);
				if (unusedLogFiles != null && unusedLogFiles.Count > 0)
				{
					Log("Backup", "{0} unused log files - from {1} to {2}", unusedLogFiles.Count,
						unusedLogFiles[0], unusedLogFiles[unusedLogFiles.Count - 1]);
				   
				}
				

				// get new data files
				List<string> newDataFiles = storage.Environment.GetDataFilesForArchiving();
				if (newDataFiles != null)
				{
					// skip the ones already copied
					for (int idx = newDataFiles.Count - 1; idx >= 0; --idx)
					{
						if (dataFilesCopied.Contains(newDataFiles[idx]))
						{
							newDataFiles.RemoveAt(idx);
						}
					}
					if (newDataFiles.Count > 0)
					{
						DatabaseConfig config = new DatabaseConfig
												{
													Type = DatabaseType.Unknown,
													OpenFlags = DbOpenFlags.ReadOnly,
													Id = -1 // to avoid appending federation index to extension
												};

						if (dataCopyBufferSize > 0)
						{
							if (copyBuffer == null || copyBuffer.Length < dataCopyBufferSize)
							{
								copyBuffer = new byte[dataCopyBufferSize];
							}
						}

						// Copy database files as needed
						foreach (string dataFile in newDataFiles)
						{
							config.FileName = dataFile;
							string backupDataFile = MakeRelativeToNewFolder(backupDir, dataFile);

							using (Database db = storage.Environment.OpenDatabase(config))
							{
								switch (backupMethod)
								{
									case BackupMethod.MpoolFile:
										db.BackupFromMpf(backupDataFile, copyBuffer);
										break;
									case BackupMethod.Fstream:
										db.BackupFromDisk(backupDataFile, copyBuffer);
										break;
									default:
										throw new ArgumentOutOfRangeException(string.Format(
											"Unhandled backup method {0}", backupMethod));
								}
							}

							// Post data file copy ops
							dataFilesCopied.Add(dataFile);
						}
					}
				}


				// Copy log files as needed
				if (copyLogFiles)
				{
					
					List<string> logFiles = storage.Environment.GetAllLogFiles();
					if (logFiles != null)
					{
						if (unusedLogFiles != null)
						{
							// unused log files can be moved
							foreach (string logFile in unusedLogFiles)
							{
								if (File.Exists(logFile))
								{
									string backupLogFile = MakeRelativeToNewFolder(backupDir, logFile);
									if (File.Exists(backupLogFile)) File.Delete(backupLogFile);
									File.Move(logFile, backupLogFile);
									if (!logFilesCopied.Contains(logFile)) logFilesCopied.Add(logFile);
								}
								logFiles.Remove(logFile);
							}
						}
						// remaining log files are in use, so copy them
						foreach(string logFile in logFiles)
						{
							if (!File.Exists(logFile)) continue;
							string backupLogFile = MakeRelativeToNewFolder(backupDir, logFile);
							File.Copy(logFile, backupLogFile, true);
							if (!logFilesCopied.Contains(logFile)) logFilesCopied.Add(logFile);
						}
					}
				}


				// Set post backup values
				lastCheckpointLogNumber = newCheckpointLogNumber;
				lastUpdateTime = DateTime.Now;
				if (!isInitialized)
				{   
					firstBackupTime = lastUpdateTime;
					isInitialized = true;
				}
				LogCompleted("Backup");                
			}
			catch(Exception ex)
			{
				LogError("Backup", ex);                
				throw;
			}
		}

		void RestoreFileType(string pattern, ICollection<string> fileNames)
		{
			// copy data files
			foreach (string backupFile in Directory.GetFiles(backupDir, pattern))
			{
				string file = MakeRelativeToNewFolder(homeDir, backupFile);
				File.Copy(backupFile, file, true);
				// if not previously initialized, add the file to copied file list to keep in sync
				if (!isInitialized) fileNames.Add(file);
				if (BerkeleyDbStorage.Log.IsInfoEnabled)
				{
					BerkeleyDbStorage.Log.InfoFormat("Restore() {0} copied to {1}"
						, backupFile, file);
				}
			}
		}
		
		public bool Restore()
		{
			if (BerkeleyDbStorage.Log.IsInfoEnabled)
			{
				BerkeleyDbStorage.Log.InfoFormat("Restore() Starting restoration from {0}"
					,   backupDir);
			}
			try
			{
				if (!Directory.Exists(backupDir))
				{
					if (BerkeleyDbStorage.Log.IsInfoEnabled)
					{
						BerkeleyDbStorage.Log.InfoFormat("Restore() Missing directory {0}"
							,   backupDir);
					}
					return false;
				}
				// copy data files
				RestoreFileType("*.bdb*", dataFilesCopied);
				// copy log files
				RestoreFileType("log.*", logFilesCopied);
				// initialize 
				if (BerkeleyDbStorage.Log.IsInfoEnabled)
				{
					BerkeleyDbStorage.Log.InfoFormat("Restore() Restoration completed"
						,   backupDir);
				}
				return true;
			}
			catch (Exception ex)
			{
				if (BerkeleyDbStorage.Log.IsErrorEnabled)
				{
					BerkeleyDbStorage.Log.Error(string.Format("Restore() Error"), ex);
				}
				throw;
			}
		}

		static readonly Regex rePath = new Regex("^(?<root>.*?)([(](?<index>[0-9]+)[)])?$",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);
		public bool Move(string newBackupDir, bool allowNameSerializing)
		{
			newBackupDir = Path.Combine(homeDir, newBackupDir);
			if (string.Compare(newBackupDir, backupDir) == 0) return false;
			if (Directory.Exists(newBackupDir))
			{
				if (allowNameSerializing)
				{
					Match mtch = rePath.Match(newBackupDir);
					string root = mtch.Groups["root"].Value;
					int idx = 1;
					do
					{
						++idx;
						newBackupDir = string.Format("{0}({1})", root, idx);
					} while (Directory.Exists(newBackupDir));
				}
				else
				{
					return false;
				}
			}
			Directory.Move(backupDir, newBackupDir);
			backupDir = newBackupDir;
			return true;
		}
		
		public IList<string> GetUnusedLogFiles()
		{
			return unusedLogFiles;
		}

		bool IsOnFirstBackup { get { return firstBackupTime == lastUpdateTime; } }

		static void DeleteFiles(IEnumerable<string> fileList)
		{
			if (fileList != null)
			{
				foreach (string file in fileList)
				{
					if (File.Exists(file)) File.Delete(file);
				}
			}
		}

		public void DeleteUnusedLogFiles()
		{
			if (copyLogFiles || IsOnFirstBackup)
			{
				DeleteFiles(GetUnusedLogFiles());
			}
		}

		public IList<string> GetDataFilesCopied() { return new List<string>(dataFilesCopied); }

		public IList<string> GetLogFilesCopied() { return new List<string>(logFilesCopied); }


		#region Private Members
		#region Methods
		static void ClearDirectory(string path)
		{
			if (Directory.Exists(path))
			{
				foreach (string file in Directory.GetFiles(path))
				{
					File.Delete(file);
				}
			}
			else
			{
				Directory.CreateDirectory(path);
			}
		}

		

		static int GetNullSafeListCount(ICollection list)
		{
			if (list != null) 
				return list.Count;
			
			return 0;
		}

		public int GetRemovableLogFileCount()
		{
			int unusedLogFileCount = GetNullSafeListCount(storage.Environment.GetUnusedLogFiles());
			if (copyLogFiles)
			{
				// if log files copy, removable count is (# in bkp dir - (# all logs - # unused logs)
				int logFileCount = GetNullSafeListCount(storage.Environment.GetAllLogFiles());
				int backupLogFileCount = GetNullSafeListCount(logFilesCopied);
				return backupLogFileCount - logFileCount + unusedLogFileCount;
			}
			
			return unusedLogFileCount;
			
		}

		static string MakeRelativeToNewFolder(string newFolderPath, string path)
		{
			return Path.Combine(newFolderPath, Path.GetFileName(path));
		}
		#endregion
		
		#region Fields

		readonly BerkeleyDbStorage storage;
		bool isInitialized;
		readonly List<string> dataFilesCopied;
		readonly List<string> logFilesCopied;
		readonly string homeDir;
		string backupDir;
		
		int lastCheckpointLogNumber;
		readonly int dataCopyBufferSize;
		DateTime firstBackupTime = DateTime.MinValue;
		DateTime lastUpdateTime = DateTime.MinValue;
		readonly bool copyLogFiles;
		readonly BackupMethod backupMethod;
		byte[] copyBuffer;
		List<string> unusedLogFiles;
		#endregion
		
		#endregion

		
	}
}
