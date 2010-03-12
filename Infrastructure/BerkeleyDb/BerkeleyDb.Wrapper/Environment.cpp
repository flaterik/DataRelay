#include "stdafx.h"
#include "Environment.h"
#include "Database.h"
#include "BdbException.h"
#include "Alloc.h"

using namespace std;
using namespace System;
using namespace System::Runtime::InteropServices;
using namespace MySpace::BerkeleyDb::Configuration;
using namespace MySpace::ResourcePool;
using namespace System::Collections::Generic;

void __cdecl msgcall(const DbEnv *pEnv, const char *pMsg);
void __cdecl errcall(const DbEnv *pEnv, const char *pErrpfx, const char *pMsg);

int env_setalloc(DbEnv *env)
{
	return env->set_alloc(&malloc_wrapper, &realloc_wrapper, &free_wrapper);
}

// flags that have to be set before DbEnv::open
const u_int32_t mustPreOpenFlags = static_cast<u_int32_t>(BerkeleyDbWrapper::EnvFlags::LogInMemory);

BerkeleyDbWrapper::Environment::Environment(EnvironmentConfig^ envConfig) : m_errpfx(0)//, bufferSize(1048), maxDbEntryReuse(5)
{
	int ret = 0;
	try
	{		
		m_log = gcnew MySpace::Logging::LogWrapper();

		m_pEnv = new DbEnv(static_cast<u_int32_t>(EnvCreateFlags::None));
		m_thisHandle = GCHandle::Alloc(this);
		m_pEnv->set_app_private(GCHandle::ToIntPtr(m_thisHandle).ToPointer());
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

		u_int32_t preOpenFlags = mustPreOpenFlags & static_cast<u_int32_t>(envConfig->Flags);
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
		if (m_pEnv)
		{
			ret = m_pEnv->close(0);
			delete m_pEnv;
			m_pEnv = NULL;
		}
		throw gcnew BdbException(ret, &ex, String::Format("Flags: {0}, OpenFlags: {1} - {2}",
			static_cast<u_int32_t>(envConfig->Flags), static_cast<u_int32_t>(envConfig->OpenFlags),
			gcnew String(ex.what())));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw gcnew BdbException(ret, String::Format(
				"BerkeleyDbWrappwer:Environment:Constructor: Unexpected error with ret value {0}, Flags: {0}, OpenFlags: {1}",
				ret, static_cast<u_int32_t>(envConfig->Flags), static_cast<u_int32_t>(envConfig->OpenFlags)));
	}
}

BerkeleyDbWrapper::Environment::Environment(String^ dbHome, EnvOpenFlags flags) : m_errpfx(0)//, bufferSize(1048), maxDbEntryReuse(5)
{
	int ret = 0;
	try
	{
		m_pEnv = new DbEnv(0);
		ConvStr pszDbHome(dbHome);
		ret = env_setalloc(m_pEnv);
		m_pEnv->open(pszDbHome.Str(), static_cast<u_int32_t>(flags), 0);
	}
	catch (const exception &ex)
	{
		if (m_pEnv)
		{
			m_pEnv->close(0);
			delete m_pEnv;
			m_pEnv = NULL;
		}
		throw gcnew BdbException(&ex, gcnew String(ex.what()));
	}
}

BerkeleyDbWrapper::Environment::!Environment()
{
	if (m_pEnv != NULL)
	{
		try
		{
			m_pEnv->close(0);
		}
		catch (const exception &ex)
		{
			char *errPrefix = nullptr;
			if (m_errpfx != nullptr) errPrefix = m_errpfx->Str();
			errcall(m_pEnv, errPrefix, ex.what());
		}
		finally
		{
			delete m_pEnv;
			m_pEnv = NULL;
		}
	}
}

BerkeleyDbWrapper::Environment::~Environment()
{
	this->!Environment();
}

BerkeleyDbWrapper::Database^ BerkeleyDbWrapper::Environment::OpenDatabase(DatabaseConfig^ dbConfig)
{	
	return gcnew Database(this, dbConfig);
}

int BerkeleyDbWrapper::Environment::GetMaxLockers()
{
	u_int32_t maxLockers = 0;
	int ret = 0;
	try 
	{
		ret = m_pEnv->get_lk_max_lockers(&maxLockers);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return maxLockers;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetMaxLockers: Unexpected error with ret value " + ret);
	}
}

int BerkeleyDbWrapper::Environment::GetMaxLocks()
{
	u_int32_t maxLocks = 0;
	int ret = 0;
	try 
	{
		ret = m_pEnv->get_lk_max_locks(&maxLocks);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return maxLocks;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetMaxLockers: Unexpected error with ret value " + ret);
	}
}

int BerkeleyDbWrapper::Environment::GetMaxLockObjects()
{
	u_int32_t maxLockObjects = 0;
	int ret = 0;
	try 
	{
		ret = m_pEnv->get_lk_max_objects(&maxLockObjects);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return maxLockObjects;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetMaxLockers: Unexpected error with ret value " + ret);
	}
}

BerkeleyDbWrapper::EnvFlags BerkeleyDbWrapper::Environment::GetFlags()
{
	int ret = 0;
	BerkeleyDbWrapper::EnvFlags flags;
	try
	{
		u_int32_t flagsp = 0;
		ret = m_pEnv->get_flags(&flagsp);
		flags = static_cast<BerkeleyDbWrapper::EnvFlags>(flagsp);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return flags;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetFlags: Unexpected error with ret value " + ret);
	}
}

BerkeleyDbWrapper::EnvOpenFlags BerkeleyDbWrapper::Environment::GetOpenFlags()
{
	int ret = 0;
	BerkeleyDbWrapper::EnvOpenFlags flags;
	try
	{
		u_int32_t flagsp = 0;
		ret = m_pEnv->get_open_flags(&flagsp);
		flags = static_cast<BerkeleyDbWrapper::EnvOpenFlags>(flagsp);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return flags;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetOpenFlags: Unexpected error with ret value " + ret);
	}
}

int BerkeleyDbWrapper::Environment::GetTimeout(TimeoutFlags flag)
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return microseconds;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetTimeout: Unexpected error with ret value " + ret);
	}
}

bool BerkeleyDbWrapper::Environment::GetVerbose(u_int32_t which)
{
	int ret = 0;
	int onoff = 0;
	try
	{
		ret = m_pEnv->get_verbose(which, &onoff);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return onoff ? true : false;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetVerboseDeadlock: Unexpected error with ret value " + ret);
	}
}

bool BerkeleyDbWrapper::Environment::GetVerboseDeadlock()
{
	return GetVerbose(DB_VERB_DEADLOCK);
}

bool BerkeleyDbWrapper::Environment::GetVerboseRecovery()
{
	return GetVerbose(DB_VERB_RECOVERY);
}

bool BerkeleyDbWrapper::Environment::GetVerboseWaitsFor()
{
	return GetVerbose(DB_VERB_WAITSFOR);
}

int BerkeleyDbWrapper::Environment::LockDetect (DeadlockDetectPolicy detectPolicy)
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return aborted;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:LockDetect: Unexpected error with ret value " + ret);
	}
}

int BerkeleyDbWrapper::Environment::MempoolTrickle (int percentage)
{
	int ret = 0;
	int nwrote = 0;
	try
	{
		ret = m_pEnv->memp_trickle(percentage, &nwrote);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return nwrote;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:MempoolTrickle: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::Checkpoint(int sizeKbytes, int ageMinutes, bool force)
{
	int ret = 0;
	u_int32_t flags = force ? DB_FORCE : 0;
	try
	{
		ret = m_pEnv->txn_checkpoint(sizeKbytes, ageMinutes, flags);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:Checkpoint: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::DeleteUnusedLogs()
{
	GetArchiveFiles(DB_ARCH_REMOVE, "DeleteUnusedLogs", 0, -2);
}

System::Collections::Generic::List<String^>^ BerkeleyDbWrapper::Environment::GetUnusedLogFiles()
{
	return GetArchiveFiles(DB_ARCH_ABS, "GetUnusedLogFiles", 0, -2);
}

System::Collections::Generic::List<String^>^ BerkeleyDbWrapper::Environment::GetAllLogFiles(int startIdx, int endIdx)
{
	return GetArchiveFiles(DB_ARCH_LOG | DB_ARCH_ABS, "GetAllLogFiles", startIdx, endIdx);
}
System::Collections::Generic::List<String^>^ BerkeleyDbWrapper::Environment::GetAllLogFiles()
{
	return GetAllLogFiles(0, -2);
}

System::Collections::Generic::List<String^>^ BerkeleyDbWrapper::Environment::GetDataFilesForArchiving()
{
	return GetArchiveFiles(DB_ARCH_DATA | DB_ARCH_ABS, "GetDataFilesForArchiving", 0, -2);
}

System::Collections::Generic::List<String^>^ BerkeleyDbWrapper::Environment::GetArchiveFiles(
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
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
			throw gcnew BdbException(ret, String::Format(
				"BerkeleyDbWrappwer:Environment:{0}: Unexpected error with ret value {1}",
				gcnew String(procName), ret));
	}
}

String^ BerkeleyDbWrapper::Environment::GetHomeDirectory()
{
	int ret = 0;
	const char *path;
	try
	{
		ret = m_pEnv->get_home(&path);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return gcnew String(path);
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetHomeDirectory: Unexpected error with ret value " + ret);
	}
}

int BerkeleyDbWrapper::Environment::GetLastCheckpointLogNumber()
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
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
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetLastCheckpointLogNumber: Unexpected error with ret value " + ret);
	}
}

int BerkeleyDbWrapper::Environment::GetCurrentLogNumber()
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
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
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetCurrentLogNumber: Unexpected error with ret value " + ret);
	}
}

String^ BerkeleyDbWrapper::Environment::GetLogFileNameFromNumber(int logNumber)
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			return gcnew String(buffer);
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetLogFileNameFromSequence: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::PrintStats ()
{
	int ret = 0;
	try
	{
		ret = m_pEnv->stat_print(DB_STAT_ALL);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:PrintStats: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::PrintCacheStats ()
{
	int ret = 0;
	try
	{
		ret = m_pEnv->memp_stat_print(DB_STAT_ALL);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:PrintCacheStats: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::PrintLockStats ()
{
	int ret = 0;
	try
	{
		ret = m_pEnv->lock_stat_print(DB_STAT_ALL);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:PrintLockStats: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::RemoveFlags (BerkeleyDbWrapper::EnvFlags flags)
{
	SetFlags(flags, 0);
}

void BerkeleyDbWrapper::Environment::SetFlags (BerkeleyDbWrapper::EnvFlags flags, int onoff)
{
	int ret = 0;
	try
	{
		u_int32_t envFlags = static_cast<u_int32_t>(flags);
		envFlags &= ~mustPreOpenFlags; // these flags can only be changed before open
		u_int32_t logFlags = static_cast<u_int32_t>(BerkeleyDbWrapper::EnvFlags::LogFlags) & envFlags;
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:SetFlags: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::SetFlags (BerkeleyDbWrapper::EnvFlags flags)
{
	SetFlags(flags, 1);
}

void BerkeleyDbWrapper::Environment::SetTimeout (int microseconds, BerkeleyDbWrapper::TimeoutFlags flag)
{
	int ret = 0;
	try
	{
		u_int32_t timeoutflag = static_cast<u_int32_t>(flag);
		ret = m_pEnv->set_timeout(microseconds, timeoutflag);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:SetTimeout: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::GetLockStatistics()
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
			this->lockStatNumberCurrentLockers->RawValue = pLockStat->st_nlockers;
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
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
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:GetLockStatistics: Unexpected error with ret value " + ret);
	}
}


void BerkeleyDbWrapper::Environment::SetVerbose(u_int32_t which, int onoff)
{
	int ret = 0;
	try
	{
		ret = m_pEnv->set_verbose(which, onoff);
	}
	catch (const exception &ex)
	{
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:SetVerbose: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::Remove(String^ dbHome, EnvOpenFlags openFlags, bool force)
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	finally
	{
		if (env != NULL)
		{
			delete env;
			env = NULL;
		}
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrappwer:Environment:Remove: Unexpected error with ret value " + ret);
	}
}

void BerkeleyDbWrapper::Environment::RemoveDatabase(String^ dbPath)
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret) {
		case DbRetVal::SUCCESS:
			return;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrapper::Environment::RemoveDatabase: Unexpected error" + ret);
	}
}

void BerkeleyDbWrapper::Environment::SetVerboseDeadlock(bool verboseDeadlock)
{
	SetVerbose(DB_VERB_DEADLOCK, verboseDeadlock ? 1 : 0);
}

void BerkeleyDbWrapper::Environment::SetVerboseRecovery(bool verboseRecovery)
{
	SetVerbose(DB_VERB_RECOVERY, verboseRecovery ? 1 : 0);
}

void BerkeleyDbWrapper::Environment::SetVerboseWaitsFor(bool verboseWaitsFor)
{
	SetVerbose(DB_VERB_WAITSFOR, verboseWaitsFor ? 1 : 0);
}

void BerkeleyDbWrapper::Environment::RaiseMessageEvent(String ^message)
{
	MessageCall(this, gcnew BerkeleyDbMessageEventArgs(message));
}

void __cdecl msgcall(const DbEnv *pEnv, const char *pMsg)
{
	GCHandle gch = GCHandle::FromIntPtr(IntPtr(pEnv->get_app_private()));
	BerkeleyDbWrapper::Environment ^env = safe_cast<BerkeleyDbWrapper::Environment^>(gch.Target);
	env->RaiseMessageEvent(Marshal::PtrToStringAnsi(IntPtr(const_cast<char*>(pMsg))));
}

void BerkeleyDbWrapper::Environment::RaisePanicEvent(String ^errorPrefix, String ^message)
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
	PanicCall(this, gcnew BerkeleyDbPanicEventArgs(errorPrefix, message));	
}

void __cdecl errcall(const DbEnv *pEnv, const char *pErrpfx, const char *pMsg)
{
	GCHandle gch = GCHandle::FromIntPtr(IntPtr(pEnv->get_app_private()));
	BerkeleyDbWrapper::Environment ^env = safe_cast<BerkeleyDbWrapper::Environment^>(gch.Target);
	env->RaisePanicEvent(Marshal::PtrToStringAnsi(IntPtr(const_cast<char*>(pErrpfx))), 
		Marshal::PtrToStringAnsi(IntPtr(const_cast<char*>(pMsg))));
}

void BerkeleyDbWrapper::Environment::FlushLogsToDisk()
{
	int ret = 0;
	try {
		ret = m_pEnv->log_flush(NULL);
	}
	catch(const exception &ex) {
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret) {
		case DbRetVal::SUCCESS:
			return;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrapper::Environment::FlushLogsToDisk: Unexpected error" + ret);
	}
}

void BerkeleyDbWrapper::Environment::CancelPendingTransactions()
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
		throw gcnew BdbException(ret, &ex, gcnew String(ex.what()));
	}
	finally {
		if (prepList != NULL) {
			delete prepList;
		}
	}
Returning:
	switch(ret) {
		case DbRetVal::SUCCESS:
			return;
		default:
			throw gcnew BdbException(ret, "BerkeleyDbWrapper::Environment::CancelPendingTransactions: Unexpected error" + ret);
	}
}

BerkeleyDbWrapper::EnvFlags BerkeleyDbWrapper::Environment::PreOpenSetFlags::get()
{
	return static_cast<BerkeleyDbWrapper::EnvFlags>(mustPreOpenFlags);
}

