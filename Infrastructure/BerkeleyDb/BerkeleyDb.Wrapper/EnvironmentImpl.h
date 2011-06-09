#pragma once
#include "Stdafx.h"
#include "ConvStr.h"

namespace BerkeleyDbWrapper
{
	using namespace System;
	using namespace System::Diagnostics;
	using namespace System::Security;
	using namespace System::Runtime::InteropServices;
	using namespace MySpace::BerkeleyDb::Configuration;
	using namespace MySpace::ResourcePool;
	using namespace MySpace::Logging;
	using namespace System::Collections::Generic;

	public ref class EnvironmentImpl sealed : public Environment
	{
	public:
		EnvironmentImpl(EnvironmentConfig^ envConfig);
		EnvironmentImpl(String^ dbHome, EnvOpenFlags flags);
		EnvironmentImpl(); // ctor intended for pseudosingleton to handle static methods in base class

		~EnvironmentImpl();
		!EnvironmentImpl();
		
		virtual void CancelPendingTransactions() override;
		virtual void Checkpoint(int sizeKbytes, int ageMinutes, bool force) override;
		virtual void DeleteUnusedLogs() override;
		virtual void FlushLogsToDisk() override;
		virtual List<String^>^ GetAllLogFiles() override;
		virtual List<String^>^ GetAllLogFiles(int startIdx, int endIdx) override;
		virtual int GetCurrentLogNumber() override;
		virtual List<String^>^ GetDataFilesForArchiving() override;
		virtual EnvFlags GetFlags() override;
		virtual String^ GetHomeDirectory() override;
		virtual int GetLastCheckpointLogNumber() override;
		virtual void GetLockStatistics() override;
		virtual String^ GetLogFileNameFromNumber(int logNumber) override;
		virtual int GetMaxLockers() override;
		virtual int GetMaxLockObjects() override;
		virtual int GetMaxLocks() override;
		virtual EnvOpenFlags GetOpenFlags() override;
		virtual int GetTimeout(TimeoutFlags timeoutFlag) override;
		virtual List<String^>^ GetUnusedLogFiles() override;
		virtual bool GetVerboseDeadlock() override;
		virtual bool GetVerboseRecovery() override;
		virtual bool GetVerboseWaitsFor() override;
		virtual int LockDetect(DeadlockDetectPolicy detectPolicy) override;
		virtual int MempoolTrickle(int percent) override;
		virtual Database^ OpenDatabase(DatabaseConfig^ dbConfig) override;
		virtual void PrintCacheStats() override;
		virtual void PrintLockStats() override;
		virtual void PrintStats() override;
		virtual void RemoveDatabase(String^ dbPath) override;
		virtual void RemoveFlags(EnvFlags flags) override;
		virtual void SetFlags(EnvFlags flags) override;
		virtual void SetTimeout(int microseconds, TimeoutFlags timeoutFlag) override;
		virtual void SetVerboseDeadlock(bool verboseDeadlock) override;
		virtual void SetVerboseRecovery(bool verboseRecovery) override;
		virtual void SetVerboseWaitsFor(bool verboseWaitsFor) override;
		virtual property int SpinWaits
		{
			int get() override;
			void set(int spinWaits) override;
		}

	protected:
		virtual void DoRemove(String^ dbHome, EnvOpenFlags openFlags, bool force) override;

	internal:
		property DbEnv *Handle
		{
			DbEnv *get() { return m_pEnv; }
		}
		void RaiseMessageEvent(String ^message);
		void RaisePanicEvent(String ^errorPrefix, String ^message);
		static void SetUserCopy(ENV *env, bool doSet);		

	private:
		DbEnv *m_pEnv;
		ConvStr *m_errpfx;
		LogWrapper^ m_log;
		bool GetVerbose(u_int32_t which);
		void SetFlags (BerkeleyDbWrapper::EnvFlags flags, int onoff);
		void SetVerbose(u_int32_t which, int onoff);
		List<String^>^ GetArchiveFiles(u_int32_t flags, const char *procName, int startIdx, int endIdx);
	};
}