#include "stdafx.h"
#include "EnvironmentImpl.h"
#include "DatabaseImpl.h"
#include "BdbExceptionFactory.h"
#include "Alloc.h"

#define HAVE_MEMCPY
#include "db_int.h"

using namespace std;
using namespace System;
using namespace System::Runtime::InteropServices;
using namespace MySpace::BerkeleyDb::Configuration;
using namespace MySpace::ResourcePool;
using namespace System::Collections::Generic;
using namespace BerkeleyDbWrapper;

void __cdecl msgcall(const DbEnv *pEnv, const char *pMsg);
void __cdecl errcall(const DbEnv *pEnv, const char *pErrpfx, const char *pMsg);

//#undef memcpy

int env_setalloc(DbEnv *env)
{
	return env->set_alloc(&malloc_wrapper, &realloc_wrapper, &free_wrapper);
}

int usercopy(DBT *data, u_int32_t offset, void *buffer, u_int32_t size,
	u_int32_t flags)
{
	if (flags == DB_USERCOPY_SETDATA)
	{
		if (data->size > 0)
		{
			array<Byte>^ allocData;
			GCHandle handle;
			void *appData = data->app_data;
			if (appData == nullptr)
			{
				// first call on this DBT

				// in case DBT data was read from to start with, for example for a cursor set range, null it to
				// avoid erroneous call to free(void *)
				data->data = NULL;

				allocData = gcnew array<Byte>(data->size);
				handle = GCHandle::Alloc(allocData); // we don't need to pin here, since pin_ptr pData will do that
				data->app_data = GCHandle::ToIntPtr(handle).ToPointer();
			}
			else
			{
				// subsequent call on this DBT
				handle = GCHandle::FromIntPtr(IntPtr(appData));
				allocData = static_cast<array<Byte>^>(handle.Target);
			}
			pin_ptr<Byte> pData(&allocData[offset]);
			memcpy(pData, buffer, size);
		}
		else
		{
			data->app_data = nullptr; // no need to allocate buffer for 0 size
		}
	}
	else
	{
		ConvStr s("Can't handle user copy flag " + flags);
		throw new exception(s.Str());
	}
	return 0; // success
}

void EnvironmentImpl::SetUserCopy(ENV *env, bool doSet)
{
	env->dbt_usercopy = doSet ? &usercopy : nullptr;
}

EnvironmentImpl::EnvironmentImpl(EnvironmentConfig^ envConfig) : m_errpfx(0)//, bufferSize(1048), maxDbEntryReuse(5)
{
	int ret = 0;
	try
	{		
		m_log = gcnew MySpace::Logging::LogWrapper();

		m_pEnv = new DbEnv(static_cast<u_int32_t>(EnvCreateFlags::None));
		GCHandle thisHandle = GCHandle::Alloc(this);
		m_pEnv->set_app_private(GCHandle::ToIntPtr(thisHandle).ToPointer());
		m_pEnv->set_msgcall(msgcall);
		m_pEnv->set_errcall(errcall);

		if (envConfig->CacheSize != nullptr)
		{
			ret = m_pEnv->set_cachesize(envConfig->CacheSize->GigaBytes, 
				envConfig->CacheSize->Bytes, 
				envConfig->CacheSize->NumberCaches);
		}

		if( envConfig->MutexIncrement > 0 )
		{
			// use this to get rid of 'unable to allocate memory for mutex; resize mutex region' problem.
			m_pEnv->mutex_set_increment(envConfig->MutexIncrement);
		}

		int maxLockers = envConfig->MaxLockers;
		if (maxLockers > 0)
		{
			ret = m_pEnv->set_lk_max_lockers(maxLockers);
		}
		int maxLockObjects = envConfig->MaxLockObjects;
		if (maxLockObjects > 0)
		{
			ret = m_pEnv->set_lk_max_objects(maxLockObjects);
		}
		int maxLocks = envConfig->MaxLocks;
		if (maxLocks > 0)
		{
			ret = m_pEnv->set_lk_max_locks(maxLocks);
		}
		int logBufferSize = envConfig->LogBufferSize;
		if (logBufferSize > 0)
		{
			ret = m_pEnv->set_lg_bsize(logBufferSize);
		}
		int maxLogSize = envConfig->MaxLogSize;
		if (maxLogSize > 0)
		{
			ret = m_pEnv->set_lg_max(maxLogSize);
		}
		if (envConfig->DeadlockDetection != nullptr 
			&& envConfig->DeadlockDetection->Enabled 
			&& envConfig->DeadlockDetection->IsOnEveryTransaction())
		{
			u_int32_t detectPolicy = static_cast<u_int32_t>(envConfig->DeadlockDetection->DetectPolicy);
			ret = m_pEnv->set_lk_detect(detectPolicy);
		}

		u_int32_t preOpenFlags = static_cast<u_int32_t>(MustPreOpenFlags) & static_cast<u_int32_t>(envConfig->Flags);
		if (preOpenFlags != 0)
		{
			ret = m_pEnv->set_flags(preOpenFlags, 1);
		}

		ConvStr homeDir(envConfig->HomeDirectory);
		ret = env_setalloc(m_pEnv);
		ret = m_pEnv->open(homeDir.Str(), static_cast<u_int32_t>(envConfig->OpenFlags), 0);

		ConvStr tmpDir(envConfig->TempDirectory);
		ret = m_pEnv->set_tmp_dir(tmpDir.Str());
		
 	}
	catch (const exception &ex)
	{
		if (m_pEnv != NULL)
		{
			try
			{
				m_pEnv->close(0);
			}
			finally
			{
				m_pEnv = NULL;
			}
		}
		throw BdbExceptionFactory::Create(ret, &ex, String::Format("Flags: {0}, OpenFlags: {1} - {2}",
			static_cast<u_int32_t>(envConfig->Flags), static_cast<u_int32_t>(envConfig->OpenFlags),
			gcnew String(ex.what())));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, String::Format(
				"BerkeleyDbWrappwer:Environment:Constructor: Unexpected error with ret value {0}, Flags: {0}, OpenFlags: {1}",
				ret, static_cast<u_int32_t>(envConfig->Flags), static_cast<u_int32_t>(envConfig->OpenFlags)));
	}
}

EnvironmentImpl::EnvironmentImpl(String^ dbHome, EnvOpenFlags flags) : m_errpfx(0)//, bufferSize(1048), maxDbEntryReuse(5)
{
	int ret = 0;
	try
	{
		m_pEnv = new DbEnv(0);
		GCHandle thisHandle = GCHandle::Alloc(this);
		m_pEnv->set_app_private(GCHandle::ToIntPtr(thisHandle).ToPointer());
		ConvStr pszDbHome(dbHome);
		ret = env_setalloc(m_pEnv);
		m_pEnv->open(pszDbHome.Str(), static_cast<u_int32_t>(flags), 0);
	}
	catch (const exception &ex)
	{
		if (m_pEnv != NULL)
		{
			try
			{
				m_pEnv->close(0);
			}
			finally
			{
				m_pEnv = NULL;
			}
		}
		throw BdbExceptionFactory::Create(&ex, gcnew String(ex.what()));
	}
}

EnvironmentImpl::EnvironmentImpl() : m_errpfx(0)
{
}

EnvironmentImpl::!EnvironmentImpl()
{
	if (m_pEnv != NULL)
	{
		void *p = m_pEnv->get_app_private();
		if (p != NULL)
		{
			m_pEnv->set_app_private(NULL);
			GCHandle gch = GCHandle::FromIntPtr(IntPtr(p));
			if (gch.IsAllocated)
			{
				gch.Free();
			}
		}
		try
		{
			m_pEnv->close(0);
		}
		catch (const exception &ex)
		{
			try
			{
				char *errPrefix = nullptr;
				if (m_errpfx != nullptr) errPrefix = m_errpfx->Str();
				errcall(m_pEnv, errPrefix, ex.what());
			}
			catch(...)
			{
			}
		}
		finally
		{
			m_pEnv = NULL;
		}
	}
}

EnvironmentImpl::~EnvironmentImpl()
{
	this->!EnvironmentImpl();
}

Database^ EnvironmentImpl::OpenDatabase(DatabaseConfig^ dbConfig)
{	
	return gcnew DatabaseImpl(this, dbConfig);
}

int EnvironmentImpl::GetMaxLockers()
{
	u_int32_t maxLockers = 0;
	int ret = 0;
	try 
	{
		ret = m_pEnv->get_lk_max_lockers(&maxLockers);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return maxLockers;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetMaxLockers: Unexpected error with ret value " + ret);
	}
}

int EnvironmentImpl::SpinWaits::get()
{
	u_int32_t spinWaits = 0;
	int ret = 0;
	try 
	{
		ret = m_pEnv->mutex_get_tas_spins(&spinWaits);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return spinWaits;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:SpinWaits::get: Unexpected error with ret value " + ret);
	}	
}


void EnvironmentImpl::SpinWaits::set(int spinWaits)
{
	int ret = 0;
	try 
	{
		ret = m_pEnv->mutex_set_tas_spins(spinWaits);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:SpinWaits::get: Unexpected error with ret value " + ret);
	}	
}

int EnvironmentImpl::GetMaxLocks()
{
	u_int32_t maxLocks = 0;
	int ret = 0;
	try 
	{
		ret = m_pEnv->get_lk_max_locks(&maxLocks);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return maxLocks;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetMaxLockers: Unexpected error with ret value " + ret);
	}
}

int EnvironmentImpl::GetMaxLockObjects()
{
	u_int32_t maxLockObjects = 0;
	int ret = 0;
	try 
	{
		ret = m_pEnv->get_lk_max_objects(&maxLockObjects);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return maxLockObjects;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetMaxLockers: Unexpected error with ret value " + ret);
	}
}

EnvFlags EnvironmentImpl::GetFlags()
{
	int ret = 0;
	EnvFlags flags;
	try
	{
		u_int32_t flagsp = 0;
		ret = m_pEnv->get_flags(&flagsp);
		flags = static_cast<EnvFlags>(flagsp);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return flags;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetFlags: Unexpected error with ret value " + ret);
	}
}

EnvOpenFlags EnvironmentImpl::GetOpenFlags()
{
	int ret = 0;
	EnvOpenFlags flags;
	try
	{
		u_int32_t flagsp = 0;
		ret = m_pEnv->get_open_flags(&flagsp);
		flags = static_cast<EnvOpenFlags>(flagsp);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return flags;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetOpenFlags: Unexpected error with ret value " + ret);
	}
}

int EnvironmentImpl::GetTimeout(TimeoutFlags flag)
{
	int ret = 0;
	db_timeout_t microseconds = 0;
	try
	{
		u_int32_t timeoutflag = static_cast<u_int32_t>(flag);
		ret = m_pEnv->get_timeout(&microseconds, timeoutflag);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return microseconds;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetTimeout: Unexpected error with ret value " + ret);
	}
}

bool EnvironmentImpl::GetVerbose(u_int32_t which)
{
	int ret = 0;
	int onoff = 0;
	try
	{
		ret = m_pEnv->get_verbose(which, &onoff);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return onoff ? true : false;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetVerboseDeadlock: Unexpected error with ret value " + ret);
	}
}

bool EnvironmentImpl::GetVerboseDeadlock()
{
	return GetVerbose(DB_VERB_DEADLOCK);
}

bool EnvironmentImpl::GetVerboseRecovery()
{
	return GetVerbose(DB_VERB_RECOVERY);
}

bool EnvironmentImpl::GetVerboseWaitsFor()
{
	return GetVerbose(DB_VERB_WAITSFOR);
}

int EnvironmentImpl::LockDetect (DeadlockDetectPolicy detectPolicy)
{
	int ret = 0;
	int aborted = 0;
	try
	{
		u_int32_t policy = static_cast<u_int32_t>(detectPolicy);
		ret = m_pEnv->lock_detect(0, policy, &aborted);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return aborted;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:LockDetect: Unexpected error with ret value " + ret);
	}
}

int EnvironmentImpl::MempoolTrickle (int percentage)
{
	int ret = 0;
	int nwrote = 0;
	try
	{
		ret = m_pEnv->memp_trickle(percentage, &nwrote);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return nwrote;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:MempoolTrickle: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::Checkpoint(int sizeKbytes, int ageMinutes, bool force)
{
	int ret = 0;
	u_int32_t flags = force ? DB_FORCE : 0;
	try
	{
		ret = m_pEnv->txn_checkpoint(sizeKbytes, ageMinutes, flags);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:Checkpoint: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::DeleteUnusedLogs()
{
	GetArchiveFiles(DB_ARCH_REMOVE, "DeleteUnusedLogs", 0, -2);
}

System::Collections::Generic::List<String^>^ EnvironmentImpl::GetUnusedLogFiles()
{
	return GetArchiveFiles(DB_ARCH_ABS, "GetUnusedLogFiles", 0, -2);
}

System::Collections::Generic::List<String^>^ EnvironmentImpl::GetAllLogFiles(int startIdx, int endIdx)
{
	return GetArchiveFiles(DB_ARCH_LOG | DB_ARCH_ABS, "GetAllLogFiles", startIdx, endIdx);
}
System::Collections::Generic::List<String^>^ EnvironmentImpl::GetAllLogFiles()
{
	return GetAllLogFiles(0, -2);
}

System::Collections::Generic::List<String^>^ EnvironmentImpl::GetDataFilesForArchiving()
{
	return GetArchiveFiles(DB_ARCH_DATA | DB_ARCH_ABS, "GetDataFilesForArchiving", 0, -2);
}

System::Collections::Generic::List<String^>^ EnvironmentImpl::GetArchiveFiles(
	u_int32_t flags, const char *procName, int startIdx, int endIdx)
{
	int ret = 0;
	char **fileList = NULL;
	List<String^>^ pathList = nullptr;
	try
	{
		ret = m_pEnv->log_archive(&fileList, flags);
		if (fileList != NULL)
		{
			pathList = gcnew List<String^>();
			++endIdx;
			endIdx -= startIdx;
			for (char **path = fileList + startIdx; endIdx != 0 && *path != NULL; ++path)
			{
				pathList->Add(gcnew String(*path));
				--endIdx;
			}
		}
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	finally
	{
		if (fileList != NULL)
		{
			free_wrapper(fileList);
		}
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return pathList;
		default:
			throw BdbExceptionFactory::Create(ret, String::Format(
				"BerkeleyDbWrappwer:Environment:{0}: Unexpected error with ret value {1}",
				gcnew String(procName), ret));
	}
}

String^ EnvironmentImpl::GetHomeDirectory()
{
	int ret = 0;
	const char *path;
	try
	{
		ret = m_pEnv->get_home(&path);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return gcnew String(path);
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetHomeDirectory: Unexpected error with ret value " + ret);
	}
}

int EnvironmentImpl::GetLastCheckpointLogNumber()
{
	int ret = 0;
	int logNumber = -1;
	DB_TXN_STAT *stat = NULL;
	try
	{
		ret = m_pEnv->txn_stat(&stat, 0);
		if (ret == 0) {
			logNumber = stat->st_last_ckp.file;
		}
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	finally {
		if (stat != NULL) {
			free_wrapper(stat);
		}
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return logNumber;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetLastCheckpointLogNumber: Unexpected error with ret value " + ret);
	}
}

int EnvironmentImpl::GetCurrentLogNumber()
{
	int ret = 0;
	int logNumber = -1;
	DB_LOG_STAT *stat = NULL;
	try
	{
		ret = m_pEnv->log_stat(&stat, 0);
		if (ret == 0) {
			logNumber = stat->st_cur_file;
		}
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	finally {
		if (stat != NULL) {
			free_wrapper(stat);
		}
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return logNumber;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetCurrentLogNumber: Unexpected error with ret value " + ret);
	}
}

String^ EnvironmentImpl::GetLogFileNameFromNumber(int logNumber)
{
	int ret = 0;
	const int bufferLength = 256;
	char buffer[bufferLength];
	DbLsn lsn;
	lsn.file = logNumber;
	lsn.offset = 0;
	try
	{
		ret = m_pEnv->log_file(&lsn, buffer, bufferLength);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return gcnew String(buffer);
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetLogFileNameFromSequence: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::PrintStats ()
{
	int ret = 0;
	try
	{
		ret = m_pEnv->stat_print(DB_STAT_ALL);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:PrintStats: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::PrintCacheStats ()
{
	int ret = 0;
	try
	{
		ret = m_pEnv->memp_stat_print(DB_STAT_ALL);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:PrintCacheStats: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::PrintLockStats ()
{
	int ret = 0;
	try
	{
		ret = m_pEnv->lock_stat_print(DB_STAT_ALL);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:PrintLockStats: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::RemoveFlags (EnvFlags flags)
{
	SetFlags(flags, 0);
}

void EnvironmentImpl::SetFlags (EnvFlags flags, int onoff)
{
	int ret = 0;
	try
	{
		u_int32_t envFlags = static_cast<u_int32_t>(flags);
		envFlags &= ~static_cast<u_int32_t>(MustPreOpenFlags); // these flags can only be changed before open
		u_int32_t logFlags = static_cast<u_int32_t>(EnvFlags::LogFlags) & envFlags;
		if (logFlags != 0)
		{
			envFlags &= ~logFlags;
			ret = m_pEnv->log_set_config(logFlags, onoff);
		}
		if (ret == 0)
		{
			ret = m_pEnv->set_flags(envFlags, onoff);
		}
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:SetFlags: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::SetFlags (EnvFlags flags)
{
	SetFlags(flags, 1);
}

void EnvironmentImpl::SetTimeout (int microseconds, TimeoutFlags flag)
{
	int ret = 0;
	try
	{
		u_int32_t timeoutflag = static_cast<u_int32_t>(flag);
		ret = m_pEnv->set_timeout(microseconds, timeoutflag);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:SetTimeout: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::GetLockStatistics()
{
	DB_LOCK_STAT *pLockStat = 0;

	int ret = 0;
	try
	{
		ret = m_pEnv->lock_stat(&pLockStat, 0);

		if( pLockStat != 0 && ret == (int)DbRetVal::SUCCESS )
		{
			this->LockStatCurrentMaxLockerId->RawValue = pLockStat->st_cur_maxid;
			this->LockStatLastLockerId->RawValue = pLockStat->st_id;
			this->LockStatLockersNoWait->RawValue = pLockStat->st_lock_nowait;
			this->LockStatLockersWait->RawValue = pLockStat->st_lock_wait;
			this->LockStatLockTimeout->RawValue = pLockStat->st_locktimeout;
			this->LockStatMaxLockersPossible->RawValue = pLockStat->st_maxlockers;
			this->LockStatMaxLocksPossible->RawValue = pLockStat->st_maxlocks;
			this->LockStatMaxNumberLockersAtOneTime->RawValue = pLockStat->st_maxnlockers;
			this->LockStatMaxNumberLocksAtOneTime->RawValue = pLockStat->st_maxnlocks;
			this->LockStatNumberCurrentLockObjectsAtOneTime->RawValue = pLockStat->st_maxnobjects;
			this->LockStatMaxLockObjectsPossible->RawValue	= pLockStat->st_maxobjects;
			this->LockStatNumberDeadLocks->RawValue = pLockStat->st_ndeadlocks;
			this->LockStatNumberLocksDownGraded->RawValue = pLockStat->st_ndowngrade;
			this->LockStatNumberCurrentLockers->RawValue = pLockStat->st_nlockers;
			this->LockStatNumberCurrentLocks->RawValue = pLockStat->st_nlocks;
			this->LockStatNumberLockTimeouts->RawValue = pLockStat->st_nlocktimeouts;
			this->LockStatNumberLockModes->RawValue = pLockStat->st_nmodes;
			this->LockStatNumberCurrentLockObjects->RawValue = pLockStat->st_nobjects;
			this->LockStatNumberLocksReleased->RawValue = pLockStat->st_nreleases;
			this->LockStatNumberLocksRequested->RawValue = pLockStat->st_nrequests;
			this->LockStatNumberTxnTimeouts->RawValue = pLockStat->st_ntxntimeouts;
			this->LockStatNumberLocksUpgraded->RawValue = pLockStat->st_nupgrade;
			this->LockStatRegionNoWait->RawValue = pLockStat->st_region_nowait;
			this->LockStatRegionWait->RawValue = pLockStat->st_region_wait;
			this->LockStatLockRegionSize->RawValue = pLockStat->st_regsize;
			this->LockStatTxnTimeout->RawValue = pLockStat->st_txntimeout;
		}
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	finally
	{
		if( pLockStat != 0 )
			free_wrapper( pLockStat );
	}

	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:GetLockStatistics: Unexpected error with ret value " + ret);
	}
}


void EnvironmentImpl::SetVerbose(u_int32_t which, int onoff)
{
	int ret = 0;
	try
	{
		ret = m_pEnv->set_verbose(which, onoff);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:SetVerbose: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::DoRemove(String^ dbHome, EnvOpenFlags openFlags, bool force)
{
	int ret = 0;
	DbEnv *env = NULL;
	ConvStr dir(dbHome);
	char *path = dir.Str();
	// use os process environment variables if requested
	u_int32_t flags = static_cast<u_int32_t>(openFlags & (EnvOpenFlags::UseEnviron | EnvOpenFlags::UseEnvironRoot));
	if (force)
		flags |= DB_FORCE;
	try
	{
		env = new DbEnv(0);
		ret = env->remove(path, flags);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	finally
	{
		if (env != NULL)
		{
			try
			{
				delete env;
			}
			finally
			{
				env = NULL;
			}
		}
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrappwer:Environment:Remove: Unexpected error with ret value " + ret);
	}
}

void EnvironmentImpl::RemoveDatabase(String^ dbPath)
{
	int ret = 0;
	ConvStr cPath(dbPath);
	char *path = cPath.Str();
	bool isTrans = ((GetOpenFlags() & EnvOpenFlags::InitTxn) == EnvOpenFlags::InitTxn);
	try
	{
		ret = m_pEnv->dbremove(NULL, path, NULL, isTrans ? DB_AUTO_COMMIT : 0);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret) {
		case DbRetVal::SUCCESS:
			return;
		default:
			throw BdbExceptionFactory::Create(ret, "EnvironmentImpl::RemoveDatabase: Unexpected error" + ret);
	}
}

void EnvironmentImpl::SetVerboseDeadlock(bool verboseDeadlock)
{
	SetVerbose(DB_VERB_DEADLOCK, verboseDeadlock ? 1 : 0);
}

void EnvironmentImpl::SetVerboseRecovery(bool verboseRecovery)
{
	SetVerbose(DB_VERB_RECOVERY, verboseRecovery ? 1 : 0);
}

void EnvironmentImpl::SetVerboseWaitsFor(bool verboseWaitsFor)
{
	SetVerbose(DB_VERB_WAITSFOR, verboseWaitsFor ? 1 : 0);
}

void EnvironmentImpl::RaiseMessageEvent(String ^message)
{
	MessageEventHandler ^messageCall = MessageCallHandler; // for thread safety
	if (messageCall == nullptr) return;
	messageCall(this, gcnew BerkeleyDbMessageEventArgs(message));
}

void __cdecl msgcall(const DbEnv *pEnv, const char *pMsg)
{
	GCHandle gch;
	try
	{
		gch = GCHandle::FromIntPtr(IntPtr(pEnv->get_app_private()));
		EnvironmentImpl ^env =
			safe_cast<EnvironmentImpl^>(gch.Target);
		if (env != nullptr)
		{
			env->RaiseMessageEvent(Marshal::PtrToStringAnsi(IntPtr(
				const_cast<char*>(pMsg))));
		}
	}
	catch (const exception &ex)
	{
		errcall(pEnv, nullptr, ex.what());
	}
	catch(Exception ^)
	{
	}
}

void EnvironmentImpl::RaisePanicEvent(String ^errorPrefix, String ^message)
{
	if (m_log != nullptr)
	{
		if(message->Contains("DB_BUFFER_SMALL"))
		{
			m_log->InfoFormat("BerkelyDb Message: {0}",message);
		}
		else
		{
			m_log->ErrorFormat("BerkeleyDb Error Message: {0}", message);	
			if(message->Contains("MapViewOfFile: Not enough storage is available to process this command"))
				m_log->ErrorFormat("There is not enough memory available to map the cache to a file at the size specified. Try using PRIVATE or reducing the cache size.");
			if(message->Contains("MapViewOfFile: The parameter is incorrect."))
				m_log->ErrorFormat("The amount of cache specified is not valid on this system. Ensure the amount specified is positive, and on 32 bit systems, less than 2 gigabytes.");
		}
	}
	PanicEventHandler ^panicCall = PanicCallHandler; // for thread safety
	if (panicCall == nullptr) return;
	panicCall(this, gcnew BerkeleyDbPanicEventArgs(errorPrefix, message));	
}

void __cdecl errcall(const DbEnv *pEnv, const char *pErrpfx, const char *pMsg)
{
	GCHandle gch;
	try
	{
		gch = GCHandle::FromIntPtr(IntPtr(pEnv->get_app_private()));
		EnvironmentImpl ^env =
			safe_cast<EnvironmentImpl^>(gch.Target);
		if (env != nullptr)
		{
			env->RaisePanicEvent(Marshal::PtrToStringAnsi(IntPtr(
				const_cast<char*>(pErrpfx))),  Marshal::PtrToStringAnsi(IntPtr(
				const_cast<char*>(pMsg))));
		}
	}
	catch(Exception ^)
	{
	}
	catch(...)
	{
	}
}

void EnvironmentImpl::FlushLogsToDisk()
{
	int ret = 0;
	try {
		ret = m_pEnv->log_flush(NULL);
	}
	catch(const exception &ex) {
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret) {
		case DbRetVal::SUCCESS:
			return;
		default:
			throw BdbExceptionFactory::Create(ret, "EnvironmentImpl::FlushLogsToDisk: Unexpected error" + ret);
	}
}

void EnvironmentImpl::CancelPendingTransactions()
{
	int ret = 0;
	const long listSize = 255;
	long actualSize = 0;
	DbPreplist *prepList = NULL;
	try {
		prepList = new DbPreplist[listSize];
		ret = m_pEnv->txn_recover(prepList, listSize, &actualSize, DB_FIRST);
		if (ret != 0) goto Returning;
		while (actualSize > 0) {
			for(long idx = 0; idx < actualSize; ++idx) {
				ret = prepList[idx].txn->abort();
				if (ret != 0) goto Returning;
			}
			ret = m_pEnv->txn_recover(prepList, listSize, &actualSize, DB_NEXT);
			if (ret != 0) goto Returning;
		}
	}
	catch(const exception &ex) {
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	finally {
		if (prepList != NULL) {
			delete[] prepList;
		}
	}
Returning:
	switch(ret) {
		case DbRetVal::SUCCESS:
			return;
		default:
			throw BdbExceptionFactory::Create(ret, "EnvironmentImpl::CancelPendingTransactions: Unexpected error" + ret);
	}
}

