// BerkeleyDb.Wrapper.Common.h
#pragma once
#include "Stdafx.h"
#include <errno.h>


using namespace System;

namespace BerkeleyDbWrapper {

	public enum class EnvCreateFlags
	{
		None = 0,
		RpcClient = DB_RPCCLIENT
	};
	
	[Flags]
	public enum class EnvOpenFlags
	{
		/* subsystem initialization */
		InitCDB = DB_INIT_CDB,
		InitLog = DB_INIT_LOG,
		InitLock = DB_INIT_LOCK,
		InitMPool = DB_INIT_MPOOL,
		InitRep = DB_INIT_REP,
		InitTxn = DB_INIT_TXN,
		JoinEnv = DB_JOINENV,
		/* recovery */
		Recover = DB_RECOVER,
		RecoverFatal = DB_RECOVER_FATAL,
		/* file naming */
		UseEnviron = DB_USE_ENVIRON,
		UseEnvironRoot = DB_USE_ENVIRON_ROOT,
		/* additional */
		Create = DB_CREATE,
		LockDown = DB_LOCKDOWN,
		Private = DB_PRIVATE,
		SystemMem = DB_SYSTEM_MEM,
		ThreadSafe = DB_THREAD,

		All = DB_CREATE | DB_INIT_LOG | DB_INIT_LOCK | DB_INIT_MPOOL | DB_INIT_TXN,
	};

	[Flags]
	public enum class EnvFlags
	{
		DbDirect = DB_DIRECT_DB,
		DbDSync = DB_DSYNC_DB,
		DbDirectLog = DB_LOG_DIRECT,
		DbSyncLog = DB_LOG_DSYNC,
		TxnNoSync = DB_TXN_NOSYNC,
		TxnNoWriteSync = DB_TXN_WRITE_NOSYNC,
		LogInMemory = DB_LOG_IN_MEMORY,
		LogFlags = DbDirectLog | DbSyncLog | LogInMemory //the flags which are log flags. not an actual flag 
	};

	public enum class DeadlockDetectPolicy
	{
		Default = DB_LOCK_DEFAULT,
		Epire = DB_LOCK_EXPIRE,
		MaxLocks = DB_LOCK_MAXLOCKS,
		MaxWriteLocks = DB_LOCK_MAXWRITE,
		MinLocks = DB_LOCK_MINLOCKS,
		MinWriteLocks = DB_LOCK_MINWRITE,
		OldestLocks = DB_LOCK_OLDEST,
		Random = DB_LOCK_RANDOM,
		Youngest = DB_LOCK_YOUNGEST,
	};

	[Flags]
	public enum class TimeoutFlags
	{
		LockTimeout = DB_SET_LOCK_TIMEOUT,
		TxnTimeout = DB_SET_TXN_TIMEOUT,
	};

	[Flags]
	public enum class DbCreateFlags
	{
		None = 0,
		XACreate = DB_XA_CREATE
	};
	
	[Flags]
	public enum class DbOpenFlags
	{
		None = 0,
		AutoCommit = DB_AUTO_COMMIT,
		Create = DB_CREATE,
		Exclusive = DB_EXCL,
		Multiversion = DB_MULTIVERSION,
		NoMemoryMap = DB_NOMMAP,
		ReadOnly = DB_RDONLY,
		DirtyRead = DB_READ_UNCOMMITTED,
		ThreadSafe = DB_THREAD,
		Truncate = DB_TRUNCATE,
	};

	[Flags]
	public enum class DbFlags: int
	{
		None = 0,
		ChkSum = DB_CHKSUM,
		Dup = DB_DUP,
		DupSort = DB_DUPSORT,
		Encrypt = DB_ENCRYPT,
		InOrder = DB_INORDER,
		RecNum = DB_RECNUM,
		Renumber = DB_RENUMBER,
		RevSplitOff = DB_REVSPLITOFF,
		Snapshot = DB_SNAPSHOT,
		TxnNotDurable = DB_TXN_NOT_DURABLE
	};

	public enum class DatabaseType
	{
		BTree = DB_BTREE,
		Hash = DB_HASH,
		Queue = DB_QUEUE,
		Recno = DB_RECNO,
		Unknown = DB_UNKNOWN
	};

	[Flags]
	public enum class DbStatFlags
	{
		None = 0,
		FastStat = DB_FAST_STAT,
		StatAll = DB_STAT_ALL,
		StatClear = DB_STAT_CLEAR,
		ReadCommitted = DB_READ_COMMITTED,
		ReadUncommitted = DB_READ_UNCOMMITTED,
	};

	public enum class CursorPosition
	{
		Current = DB_CURRENT,
		First = DB_FIRST,
		Previous = DB_PREV,
		PreviousDuplicate = DB_PREV_DUP,
		PreviousNoDuplicate = DB_PREV_NODUP,
		Next = DB_NEXT,
		NextDuplicate = DB_NEXT_DUP,
		NextNoDuplicate = DB_NEXT_NODUP,
		Last = DB_LAST,
		Set = DB_SET,
		SetRange = DB_SET_RANGE,
		Before = DB_BEFORE,
		After = DB_AFTER,
		KeyFirst = DB_KEYFIRST,
		KeyLast = DB_KEYLAST,
		NoUpdate = DB_NODUPDATA,
	};

	/// <summary>General Berkeley DB API return code.</summary>
	/// <remarks>Also includes framework specific custom codes such as those returned from a call-back.</remarks>
	public enum class DbRetVal : int
	{
		/* Error codes for .NET wrapper. 
		 * Keep in sync with error strings defined in Util.dotNetStr.
		 */
		KEYGEN_FAILED = -40999,         /* Key generator callback failed. */
		APPEND_RECNO_FAILED = -40998,   /* Append record number callback failed. */
		DUPCOMP_FAILED = -40997,        /* Duplicate comparison callback failed. */
		BTCOMP_FAILED = -40996,         /* BTree key comparison callback failed. */
		BTPREFIX_FAILED = -40995,       /* BTree prefix comparison callback failed. */
		HHASH_FAILED = -40994,          /* Hash function callback failed. */
		FEEDBACK_FAILED = -40993,       /* Feedback callback failed. */
		PANICCALL_FAILED = -40992,      /* Panic callback failed. */
		APP_RECOVER_FAILED = -40991,    /* Application recovery callback failed. */
		VERIFY_FAILED = -40990,         /* Verify callback failed. */
		REPSEND_FAILED = -40899,        /* Replication callback failed. */
		PAGE_IN_FAILED = -40898,        /* Cache page-in callback failed. */
		PAGE_OUT_FAILED = -40897,       /* Cache page-out callback failed. */
		KEYNULL = -40896,				/* Key is null */
		KEYZEROLENGTH = -40895,         /* Key is zero length */
		LENGTHMISMATCH = -40894,        /* Operation returned an expected mismatch */

		/* DB (public) error return codes. Range reserved: -30,800 to -30,999 */
		BUFFER_SMALL = DB_BUFFER_SMALL,          /* User memory too small for return. */
		DONOTINDEX = DB_DONOTINDEX,            /* "Null" return from 2ndary callbk. */
		KEYEMPTY = DB_KEYEMPTY,              /* Key/data deleted or never created. */
		KEYEXIST = DB_KEYEXIST,              /* The key/data pair already exists. */
		LOCK_DEADLOCK = DB_LOCK_DEADLOCK,         /* Deadlock. */
		LOCK_NOTGRANTED = DB_LOCK_NOTGRANTED,       /* Lock unavailable. */
		LOG_BUFFER_FULL = DB_LOG_BUFFER_FULL,       /* In-memory log buffer full. */
		NOSERVER = DB_NOSERVER,              /* Server panic return. */
		NOSERVER_HOME = DB_NOSERVER_HOME,         /* Bad home sent to server. */
		NOSERVER_ID = DB_NOSERVER_ID,           /* Bad ID sent to server. */
		NOTFOUND = DB_NOTFOUND,              /* Key/data pair not found (EOF). */
		OLD_VERSION = DB_OLD_VERSION,           /* Out-of-date version. */
		PAGE_NOTFOUND = DB_PAGE_NOTFOUND,         /* Requested page not found. */
		REP_DUPMASTER = DB_REP_DUPMASTER,         /* There are two masters. */
		REP_HANDLE_DEAD = DB_REP_HANDLE_DEAD,       /* Rolled back a commit. */
		REP_HOLDELECTION = DB_REP_HOLDELECTION,      /* Time to hold an election. */
		REP_ISPERM = DB_REP_ISPERM,            /* Cached not written perm written.*/
		REP_NEWMASTER = DB_REP_NEWMASTER,         /* We have learned of a new master. */
		REP_NEWSITE = DB_REP_NEWSITE,           /* New site entered system. */
		REP_NOTPERM = DB_REP_NOTPERM,           /* Permanent log record not written. */
		REP_STARTUPDONE = DB_EVENT_REP_STARTUPDONE,       /* Client startup complete. */
		REP_UNAVAIL = DB_REP_UNAVAIL,           /* Site cannot currently be reached. */
		RUNRECOVERY = DB_RUNRECOVERY,           /* Panic return. */
		SECONDARY_BAD = DB_SECONDARY_BAD,         /* Secondary index corrupt. */
		VERIFY_BAD = DB_VERIFY_BAD,            /* Verify failed; bad format. */
		VERSION_MISMATCH = DB_VERSION_MISMATCH,      /* Environment version mismatch. */
		/* DB (private) error return codes. */
		ALREADY_ABORTED = DB_ALREADY_ABORTED,
		DELETED = DB_DELETED,               /* Recovery file marked deleted. */
		//LOCK_NOTEXIST = -30897,         /* Object to lock is gone. */
		NEEDSPLIT = DB_NEEDSPLIT,             /* Page needs to be split. */
		REP_EGENCHG = DB_REP_EGENCHG,           /* Egen changed while in election. */
		REP_LOGREADY = DB_REP_LOGREADY,          /* Rep log ready for recovery. */
		REP_PAGEDONE = DB_REP_PAGEDONE,          /* This page was already done. */
		SURPRISE_KID = DB_SURPRISE_KID,          /* Child commit where parent didn't know it was a parent. */
		SWAPBYTES = DB_SWAPBYTES,             /* Database needs byte swapping. */
		TIMEOUT = DB_TIMEOUT,               /* Timed out waiting for election. */
		TXN_CKP = DB_TXN_CKP,               /* Encountered ckp record in log. */
		VERIFY_FATAL = DB_VERIFY_FATAL,          /* DB->verify cannot proceed. */

		/* No error. */
		SUCCESS = 0,

		/* Error Codes defined in C runtime (errno.h) */
		RET_VAL_E2BIG = E2BIG,
		RET_VAL_EACCES = EACCES,
		RET_VAL_EAGAIN = EAGAIN,
		RET_VAL_EBADF = EBADF,
		RET_VAL_EBUSY = EBUSY,
		RET_VAL_ECHILD = ECHILD,
		RET_VAL_EDEADLK = EDEADLK,
		RET_VAL_EDOM = EDOM,
		RET_VAL_EEXIST = EEXIST,
		RET_VAL_EFAULT = EFAULT,
		RET_VAL_EFBIG = EFBIG,
		RET_VAL_EILSEQ = EILSEQ,
		RET_VAL_EINTR = EINTR,
		RET_VAL_EINVAL = EINVAL,
		RET_VAL_EIO = EIO,
		RET_VAL_EISDIR = EISDIR,
		RET_VAL_EMFILE = EMFILE,
		RET_VAL_EMLINK = EMLINK,
		RET_VAL_ENAMETOOLONG = ENAMETOOLONG,
		RET_VAL_ENFILE = ENFILE,
		RET_VAL_ENODEV = ENODEV,
		RET_VAL_ENOENT = ENOENT,
		RET_VAL_ENOEXEC = ENOEXEC,
		RET_VAL_ENOLCK = ENOLCK,
		RET_VAL_ENOMEM = ENOMEM,
		RET_VAL_ENOSPC = ENOSPC,
		RET_VAL_ENOSYS = ENOSYS,
		RET_VAL_ENOTDIR = ENOTDIR,
		RET_VAL_ENOTEMPTY = ENOTEMPTY,
		/* Error codes used in the Secure CRT functions */
		RET_VAL_ENOTTY = ENOTTY,
		RET_VAL_ENXIO = ENXIO,
		RET_VAL_EPERM = EPERM,
		RET_VAL_EPIPE = EPIPE,
		RET_VAL_ERANGE = ERANGE,
		RET_VAL_EROFS = EROFS,
		RET_VAL_ESPIPE = ESPIPE,
		RET_VAL_ESRCH = ESRCH,
		RET_VAL_EXDEV = EXDEV,
		RET_VAL_STRUNCATE = STRUNCATE
	};

	public enum class ReadStatus : int
    {
		Success = DbRetVal::SUCCESS,
        NotFound = DbRetVal::NOTFOUND,
        KeyEmpty = DbRetVal::KEYEMPTY,
        BufferSmall = DbRetVal::BUFFER_SMALL
    };

    public enum class WriteStatus : int
    {
        Success = DbRetVal::SUCCESS,
        NotFound = DbRetVal::NOTFOUND,
        KeyExist = DbRetVal::KEYEXIST
    };

    public enum class DeleteStatus : int
    {
        Success = DbRetVal::SUCCESS,
        NotFound = DbRetVal::NOTFOUND,
        KeyEmpty = DbRetVal::KEYEMPTY
    };
}
