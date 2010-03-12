#pragma once
#include "Stdafx.h"
#include "databaseentry.h"
#include "ConvStr.h"

using namespace System;
using namespace System::Diagnostics;
using namespace System::Security;
using namespace System::Runtime::InteropServices;
using namespace MySpace::BerkeleyDb::Configuration;
using namespace MySpace::ResourcePool;
using namespace MySpace::Logging;

namespace BerkeleyDbWrapper
{
	ref class Database;

	public ref class BerkeleyDbMessageEventArgs : EventArgs
	{
	public:
		property String ^Message;

	internal:
		BerkeleyDbMessageEventArgs(String ^message)
		{
			Message = message;
		}
	};

	public ref class BerkeleyDbPanicEventArgs : EventArgs
	{
	public:
		property String ^ErrorPrefix;
		property String ^Message;

	internal:
		BerkeleyDbPanicEventArgs(String ^errorPrefix, String ^message)
		{
			ErrorPrefix = errorPrefix;
			Message = message;
		}
	};

	
	[SuppressUnmanagedCodeSecurity()]
	public ref class Environment
	{
	public:
		Environment(EnvironmentConfig^ envConfig);
		Environment(String^ dbHome, EnvOpenFlags flags);

		~Environment();
		!Environment();
		
		Database^ OpenDatabase(DatabaseConfig^ dbConfig);
		
		int GetMaxLockers();
		int GetMaxLocks();
		int GetMaxLockObjects();
		EnvOpenFlags GetOpenFlags();
		EnvFlags GetFlags();
		int GetTimeout(BerkeleyDbWrapper::TimeoutFlags timeoutFlag);
		bool GetVerboseDeadlock();
		bool GetVerboseRecovery();
		bool GetVerboseWaitsFor();
		int LockDetect (BerkeleyDbWrapper::DeadlockDetectPolicy detectPolicy);
		int MempoolTrickle (int percent);
		void Checkpoint(int sizeKbytes, int ageMinutes, bool force);
		void DeleteUnusedLogs();
		System::Collections::Generic::List<String^>^ GetUnusedLogFiles();
		System::Collections::Generic::List<String^>^ GetAllLogFiles(int startIdx, int endIdx);
		System::Collections::Generic::List<String^>^ GetAllLogFiles();
		System::Collections::Generic::List<String^>^ GetDataFilesForArchiving();
		void PrintStats ();
		void PrintCacheStats ();
		void PrintLockStats ();
		void RemoveFlags (BerkeleyDbWrapper::EnvFlags flags);
		void SetFlags (BerkeleyDbWrapper::EnvFlags flags);
		void SetTimeout(int microseconds, BerkeleyDbWrapper::TimeoutFlags timeoutFlag);
		void SetVerboseDeadlock(bool verboseDeadlock);
		void SetVerboseRecovery(bool verboseRecovery);
		void SetVerboseWaitsFor(bool verboseWaitsFor);
		void GetLockStatistics();
		String^ GetHomeDirectory();
		int GetLastCheckpointLogNumber();
		int GetCurrentLogNumber();

		delegate void MessageEventHandler(Object ^sender, BerkeleyDbMessageEventArgs ^e);
		event MessageEventHandler ^MessageCall;
		delegate void PanicEventHandler(Object ^sender, BerkeleyDbPanicEventArgs ^e);
		event PanicEventHandler ^PanicCall;

		void CancelPendingTransactions();
		void FlushLogsToDisk();
		String^ GetLogFileNameFromNumber(int logNumber);

		static void Remove(String^ dbHome, EnvOpenFlags openFlags, bool force);

		static property BerkeleyDbWrapper::EnvFlags PreOpenSetFlags
		{
			static BerkeleyDbWrapper::EnvFlags get();
		}

		void RemoveDatabase(String^ dbPath);
		

	internal:
		DbEnv *m_pEnv;
		void RaiseMessageEvent(String ^message);
		void RaisePanicEvent(String ^errorPrefix, String ^message);

	private:
		
		GCHandle m_thisHandle;
		ConvStr *m_errpfx; 
		LogWrapper^ m_log;
		bool GetVerbose(u_int32_t which);
		void SetFlags (BerkeleyDbWrapper::EnvFlags flags, int onoff);
		void SetVerbose(u_int32_t which, int onoff);
		System::Collections::Generic::List<String^>^ GetArchiveFiles(u_int32_t flags, const char *procName, int startIdx, int endIdx);
		
	private:
		// lock stat counters
		PerformanceCounter^ lockStatLastLockerId;
        PerformanceCounter^ lockStatCurrentMaxLockerId;
        PerformanceCounter^ lockStatNumberLockModes;
        PerformanceCounter^ lockStatMaxLocksPossible;
        PerformanceCounter^ lockStatMaxLockersPossible;
        PerformanceCounter^ lockStatMaxLockObjectsPossible;
        PerformanceCounter^ lockStatNumberCurrentLocks;
        PerformanceCounter^ lockStatMaxNumberLocksAtOneTime;
        PerformanceCounter^ lockStatNumberCurrentLockers;
        PerformanceCounter^ lockStatMaxNumberLockersAtOneTime;
        PerformanceCounter^ lockStatNumberCurrentLockObjects;
        PerformanceCounter^ lockStatNumberCurrentLockObjectsAtOneTime;
        PerformanceCounter^ lockStatNumberLocksRequested;
        PerformanceCounter^ lockStatNumberLocksReleased;
        PerformanceCounter^ lockStatNumberLocksUpgraded;
        PerformanceCounter^ lockStatNumberLocksDownGraded;
        PerformanceCounter^ lockStatLockWait;
        PerformanceCounter^ lockStatLockNoWait;
        PerformanceCounter^ lockStatNumberDeadLocks;
        PerformanceCounter^ lockStatLockTimeout;
        PerformanceCounter^ lockStatNumberLockTimeouts;
        PerformanceCounter^ lockStatTxnTimeout;
        PerformanceCounter^ lockStatNumberTxnTimeouts;
        PerformanceCounter^ lockStatObjectsWait;
        PerformanceCounter^ lockStatObjectsNoWait;
        PerformanceCounter^ lockStatLockersWait;
        PerformanceCounter^ lockStatLockersNoWait;
        PerformanceCounter^ lockStatLocksWait;
        PerformanceCounter^ lockStatLocksNoWait;
        PerformanceCounter^ lockStatLockHashLen;
        PerformanceCounter^ lockStatLockRegionSize;
        PerformanceCounter^ lockStatRegionWait;
        PerformanceCounter^ lockStatRegionNoWait;

		public:
		property PerformanceCounter^ LockStatLastLockerId
		{
			PerformanceCounter^ get()
			{
				return lockStatRegionNoWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatRegionNoWait = x;
			}
		}
		property PerformanceCounter^ LockStatCurrentMaxLockerId
		{
			PerformanceCounter^ get()
			{
				return lockStatCurrentMaxLockerId;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatCurrentMaxLockerId = x;
			}

		}
		property PerformanceCounter^ LockStatNumberLockModes
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberLockModes;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberLockModes = x;
			}

		}
		property PerformanceCounter^ LockStatMaxLocksPossible
		{
			PerformanceCounter^ get()
			{
				return lockStatMaxLocksPossible;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatMaxLocksPossible = x;
			}

		}
		property PerformanceCounter^ LockStatMaxLockersPossible
		{
			PerformanceCounter^ get()
			{
				return lockStatMaxLockersPossible;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatMaxLockersPossible = x;
			}

		}
		property PerformanceCounter^ LockStatMaxLockObjectsPossible
		{
			PerformanceCounter^ get()
			{
				return lockStatMaxLockObjectsPossible;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatMaxLockObjectsPossible = x;
			}

		}
		property PerformanceCounter^ LockStatNumberCurrentLocks
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberCurrentLocks;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberCurrentLocks = x;
			}

		}
		property PerformanceCounter^ LockStatMaxNumberLocksAtOneTime
		{
			PerformanceCounter^ get()
			{
				return lockStatMaxNumberLocksAtOneTime;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatMaxNumberLocksAtOneTime = x;
			}

		}
		property PerformanceCounter^ LockStatNumberCurrentLockers
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberCurrentLockers;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberCurrentLockers = x;
			}

		}
		property PerformanceCounter^ LockStatMaxNumberLockersAtOneTime
		{
			PerformanceCounter^ get()
			{
				return lockStatMaxNumberLockersAtOneTime;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatMaxNumberLockersAtOneTime = x;
			}

		}
		property PerformanceCounter^ LockStatNumberCurrentLockObjects
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberCurrentLockObjects;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberCurrentLockObjects = x;
			}

		}
		property PerformanceCounter^ LockStatNumberCurrentLockObjectsAtOneTime
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberCurrentLockObjectsAtOneTime;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberCurrentLockObjectsAtOneTime = x;
			}

		}
		property PerformanceCounter^ LockStatNumberLocksRequested
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberLocksRequested;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberLocksRequested = x;
			}

		}
		property PerformanceCounter^ LockStatNumberLocksReleased
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberLocksReleased;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberLocksReleased = x;
			}

		}
		property PerformanceCounter^ LockStatNumberLocksUpgraded
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberLocksUpgraded;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberLocksUpgraded = x;
			}

		}
		property PerformanceCounter^ LockStatNumberLocksDownGraded
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberLocksDownGraded;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberLocksDownGraded = x;
			}

		}
		property PerformanceCounter^ LockStatLockWait
		{
			PerformanceCounter^ get()
			{
				return lockStatLockWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLockWait = x;
			}

		}
		property PerformanceCounter^ LockStatLockNoWait
		{
			PerformanceCounter^ get()
			{
				return lockStatLockNoWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLockNoWait = x;
			}

		}
		property PerformanceCounter^ LockStatNumberDeadLocks
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberDeadLocks;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberDeadLocks = x;
			}

		}
		property PerformanceCounter^ LockStatLockTimeout
		{
			PerformanceCounter^ get()
			{
				return lockStatLockTimeout;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLockTimeout = x;
			}

		}
		property PerformanceCounter^ LockStatNumberLockTimeouts
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberLockTimeouts;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberLockTimeouts = x;
			}

		}
		property PerformanceCounter^ LockStatTxnTimeout
		{
			PerformanceCounter^ get()
			{
				return lockStatTxnTimeout;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatTxnTimeout = x;
			}

		}
		property PerformanceCounter^ LockStatNumberTxnTimeouts
		{
			PerformanceCounter^ get()
			{
				return lockStatNumberTxnTimeouts;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatNumberTxnTimeouts = x;
			}

		}
		property PerformanceCounter^ LockStatObjectsWait
		{
			PerformanceCounter^ get()
			{
				return lockStatObjectsWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatObjectsWait = x;
			}

		}
		property PerformanceCounter^ LockStatObjectsNoWait
		{
			PerformanceCounter^ get()
			{
				return lockStatObjectsNoWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatObjectsNoWait = x;
			}

		}
		property PerformanceCounter^ LockStatLockersWait
		{
			PerformanceCounter^ get()
			{
				return lockStatLockersWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLockersWait = x;
			}

		}
		property PerformanceCounter^ LockStatLockersNoWait
		{
			PerformanceCounter^ get()
			{
				return lockStatLockersNoWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLockersNoWait = x;
			}

		}
		property PerformanceCounter^ LockStatLocksWait
		{
			PerformanceCounter^ get()
			{
				return lockStatLocksWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLocksWait = x;
			}

		}
		property PerformanceCounter^ LockStatLocksNoWait
		{
			PerformanceCounter^ get()
			{
				return lockStatLocksNoWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLocksNoWait = x;
			}

		}
		property PerformanceCounter^ LockStatLockHashLen
		{
			PerformanceCounter^ get()
			{
				return lockStatLockHashLen;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLockHashLen = x;
			}

		}
		property PerformanceCounter^ LockStatLockRegionSize
		{
			PerformanceCounter^ get()
			{
				return lockStatLockRegionSize;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatLockRegionSize = x;
			}

		}
		property PerformanceCounter^ LockStatRegionWait
		{
			PerformanceCounter^ get()
			{
				return lockStatRegionWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatRegionWait = x;
			}

		}
		property PerformanceCounter^ LockStatRegionNoWait
		{
			PerformanceCounter^ get()
			{
				return lockStatRegionNoWait;
			}
		    
			void set(PerformanceCounter^ x)
			{
				lockStatRegionNoWait = x;
			}
		}
	};
}