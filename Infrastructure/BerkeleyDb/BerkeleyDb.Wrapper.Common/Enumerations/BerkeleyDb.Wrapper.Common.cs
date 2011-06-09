using System;

namespace BerkeleyDbWrapper
{

	public enum EnvCreateFlags
	{
		None = 0,
		RpcClient = LibConstants.DB_RPCCLIENT
	}

	[Flags]
	public enum EnvOpenFlags
	{
		/* subsystem initialization */
		InitCDB = LibConstants.DB_INIT_CDB,
		InitLog = LibConstants.DB_INIT_LOG,
		InitLock = LibConstants.DB_INIT_LOCK,
		InitMPool = LibConstants.DB_INIT_MPOOL,
		InitRep = LibConstants.DB_INIT_REP,
		InitTxn = LibConstants.DB_INIT_TXN,
		JoinEnv = LibConstants.DB_JOINENV,
		/* recovery */
		Recover = LibConstants.DB_RECOVER,
		RecoverFatal = LibConstants.DB_RECOVER_FATAL,
		/* file naming */
		UseEnviron = LibConstants.DB_USE_ENVIRON,
		UseEnvironRoot = LibConstants.DB_USE_ENVIRON_ROOT,
		/* additional */
		Create = LibConstants.DB_CREATE,
		LockDown = LibConstants.DB_LOCKDOWN,
		Private = LibConstants.DB_PRIVATE,
		SystemMem = LibConstants.DB_SYSTEM_MEM,
		ThreadSafe = LibConstants.DB_THREAD,
		Register = LibConstants.DB_REGISTER,

		All = LibConstants.DB_CREATE | LibConstants.DB_INIT_LOG | LibConstants.DB_INIT_LOCK | LibConstants.DB_INIT_MPOOL | LibConstants.DB_INIT_TXN,
	}

	[Flags]
	public enum EnvFlags
	{
		DbDirect = LibConstants.DB_DIRECT_DB,
		DbDSync = LibConstants.DB_DSYNC_DB,
		DbDirectLog = LibConstants.DB_LOG_DIRECT,
		DbSyncLog = LibConstants.DB_LOG_DSYNC,
		TxnNoSync = LibConstants.DB_TXN_NOSYNC,
		TxnNoWriteSync = LibConstants.DB_TXN_WRITE_NOSYNC,
		TxnNoWait = LibConstants.DB_TXN_NOWAIT,
		LogInMemory = LibConstants.DB_LOG_IN_MEMORY,
		LogFlags = DbDirectLog | DbSyncLog | LogInMemory //the flags which are log flags. not an actual flag 
	}

	public enum DeadlockDetectPolicy
	{
		Default = LibConstants.DB_LOCK_DEFAULT,
		Epire = LibConstants.DB_LOCK_EXPIRE,
		MaxLocks = LibConstants.DB_LOCK_MAXLOCKS,
		MaxWriteLocks = LibConstants.DB_LOCK_MAXWRITE,
		MinLocks = LibConstants.DB_LOCK_MINLOCKS,
		MinWriteLocks = LibConstants.DB_LOCK_MINWRITE,
		OldestLocks = LibConstants.DB_LOCK_OLDEST,
		Random = LibConstants.DB_LOCK_RANDOM,
		Youngest = LibConstants.DB_LOCK_YOUNGEST,
	}

	[Flags]
	public enum TimeoutFlags
	{
		LockTimeout = LibConstants.DB_SET_LOCK_TIMEOUT,
		TxnTimeout = LibConstants.DB_SET_TXN_TIMEOUT,
	}

	[Flags]
	public enum DbCreateFlags
	{
		None = 0,
		XACreate = LibConstants.DB_XA_CREATE
	}

	[Flags]
	public enum DbOpenFlags
	{
		None = 0,
		AutoCommit = LibConstants.DB_AUTO_COMMIT,
		Create = LibConstants.DB_CREATE,
		Exclusive = LibConstants.DB_EXCL,
		Multiversion = LibConstants.DB_MULTIVERSION,
		NoMemoryMap = LibConstants.DB_NOMMAP,
		ReadOnly = LibConstants.DB_RDONLY,
		DirtyRead = LibConstants.DB_READ_UNCOMMITTED,
		ThreadSafe = LibConstants.DB_THREAD,
		Truncate = LibConstants.DB_TRUNCATE,
	}

	[Flags]
	public enum DbFlags: int
	{
		None = 0,
		ChkSum = LibConstants.DB_CHKSUM,
		Dup = LibConstants.DB_DUP,
		DupSort = LibConstants.DB_DUPSORT,
		Encrypt = LibConstants.DB_ENCRYPT,
		InOrder = LibConstants.DB_INORDER,
		RecNum = LibConstants.DB_RECNUM,
		Renumber = LibConstants.DB_RENUMBER,
		RevSplitOff = LibConstants.DB_REVSPLITOFF,
		Snapshot = LibConstants.DB_SNAPSHOT,
		TxnNotDurable = LibConstants.DB_TXN_NOT_DURABLE
	}

	public enum DatabaseType
	{
		BTree = LibConstants.DB_BTREE,
		Hash = LibConstants.DB_HASH,
		Queue = LibConstants.DB_QUEUE,
		Recno = LibConstants.DB_RECNO,
		Unknown = LibConstants.DB_UNKNOWN
	}

	[Flags]
	public enum DbStatFlags
	{
		None = 0,
		FastStat = LibConstants.DB_FAST_STAT,
		StatAll = LibConstants.DB_STAT_ALL,
		StatClear = LibConstants.DB_STAT_CLEAR,
		ReadCommitted = LibConstants.DB_READ_COMMITTED,
		ReadUncommitted = LibConstants.DB_READ_UNCOMMITTED,
	}

	public enum CursorPosition
	{
		Current = LibConstants.DB_CURRENT,
		First = LibConstants.DB_FIRST,
		Previous = LibConstants.DB_PREV,
		PreviousDuplicate = LibConstants.DB_PREV_DUP,
		PreviousNoDuplicate = LibConstants.DB_PREV_NODUP,
		Next = LibConstants.DB_NEXT,
		NextDuplicate = LibConstants.DB_NEXT_DUP,
		NextNoDuplicate = LibConstants.DB_NEXT_NODUP,
		Last = LibConstants.DB_LAST,
		Set = LibConstants.DB_SET,
		SetRange = LibConstants.DB_SET_RANGE,
		Before = LibConstants.DB_BEFORE,
		After = LibConstants.DB_AFTER,
		KeyFirst = LibConstants.DB_KEYFIRST,
		KeyLast = LibConstants.DB_KEYLAST,
		NoUpdate = LibConstants.DB_NODUPDATA,
	}

	/// <summary>General Berkeley DB API return code.</summary>
	/// <remarks>Also includes framework specific custom codes such as those returned from a call-back.</remarks>
	public enum DbRetVal
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
		BUFFER_SMALL = LibConstants.DB_BUFFER_SMALL,          /* User memory too small for return. */
		DONOTINDEX = LibConstants.DB_DONOTINDEX,            /* "Null" return from 2ndary callbk. */
		KEYEMPTY = LibConstants.DB_KEYEMPTY,              /* Key/data deleted or never created. */
		KEYEXIST = LibConstants.DB_KEYEXIST,              /* The key/data pair already exists. */
		LOCK_DEADLOCK = LibConstants.DB_LOCK_DEADLOCK,         /* Deadlock. */
		LOCK_NOTGRANTED = LibConstants.DB_LOCK_NOTGRANTED,       /* Lock unavailable. */
		LOG_BUFFER_FULL = LibConstants.DB_LOG_BUFFER_FULL,       /* In-memory log buffer full. */
		NOSERVER = LibConstants.DB_NOSERVER,              /* Server panic return. */
		NOSERVER_HOME = LibConstants.DB_NOSERVER_HOME,         /* Bad home sent to server. */
		NOSERVER_ID = LibConstants.DB_NOSERVER_ID,           /* Bad ID sent to server. */
		NOTFOUND = LibConstants.DB_NOTFOUND,              /* Key/data pair not found (EOF). */
		OLD_VERSION = LibConstants.DB_OLD_VERSION,           /* Out-of-date version. */
		PAGE_NOTFOUND = LibConstants.DB_PAGE_NOTFOUND,         /* Requested page not found. */
		REP_DUPMASTER = LibConstants.DB_REP_DUPMASTER,         /* There are two masters. */
		REP_HANDLE_DEAD = LibConstants.DB_REP_HANDLE_DEAD,       /* Rolled back a commit. */
		REP_HOLDELECTION = LibConstants.DB_REP_HOLDELECTION,      /* Time to hold an election. */
		REP_ISPERM = LibConstants.DB_REP_ISPERM,            /* Cached not written perm written.*/
		REP_NEWMASTER = LibConstants.DB_REP_NEWMASTER,         /* We have learned of a new master. */
		REP_NEWSITE = LibConstants.DB_REP_NEWSITE,           /* New site entered system. */
		REP_NOTPERM = LibConstants.DB_REP_NOTPERM,           /* Permanent log record not written. */
		REP_STARTUPDONE = LibConstants.DB_EVENT_REP_STARTUPDONE,       /* Client startup complete. */
		REP_UNAVAIL = LibConstants.DB_REP_UNAVAIL,           /* Site cannot currently be reached. */
		RUNRECOVERY = LibConstants.DB_RUNRECOVERY,           /* Panic return. */
		SECONDARY_BAD = LibConstants.DB_SECONDARY_BAD,         /* Secondary index corrupt. */
		VERIFY_BAD = LibConstants.DB_VERIFY_BAD,            /* Verify failed; bad format. */
		VERSION_MISMATCH = LibConstants.DB_VERSION_MISMATCH,      /* Environment version mismatch. */
		/* DB (private) error return codes. */
		ALREADY_ABORTED = LibConstants.DB_ALREADY_ABORTED,
		DELETED = LibConstants.DB_DELETED,               /* Recovery file marked deleted. */
		//LOCK_NOTEXIST = -30897,         /* Object to lock is gone. */
		NEEDSPLIT = LibConstants.DB_NEEDSPLIT,             /* Page needs to be split. */
		REP_EGENCHG = LibConstants.DB_REP_EGENCHG,           /* Egen changed while in election. */
		REP_LOGREADY = LibConstants.DB_REP_LOGREADY,          /* Rep log ready for recovery. */
		REP_PAGEDONE = LibConstants.DB_REP_PAGEDONE,          /* This page was already done. */
		SURPRISE_KID = LibConstants.DB_SURPRISE_KID,          /* Child commit where parent didn't know it was a parent. */
		SWAPBYTES = LibConstants.DB_SWAPBYTES,             /* Database needs byte swapping. */
		TIMEOUT = LibConstants.DB_TIMEOUT,               /* Timed out waiting for election. */
		TXN_CKP = LibConstants.DB_TXN_CKP,               /* Encountered ckp record in log. */
		VERIFY_FATAL = LibConstants.DB_VERIFY_FATAL,          /* DB->verify cannot proceed. */

		/* No error. */
		SUCCESS = 0,

		/* Error Codes defined in C runtime (errno.h) */
		RET_VAL_E2BIG = Errno.E2BIG,
		RET_VAL_EACCES = Errno.EACCES,
		RET_VAL_EAGAIN = Errno.EAGAIN,
		RET_VAL_EBADF = Errno.EBADF,
		RET_VAL_EBUSY = Errno.EBUSY,
		RET_VAL_ECHILD = Errno.ECHILD,
		RET_VAL_EDEADLK = Errno.EDEADLK,
		RET_VAL_EDOM = Errno.EDOM,
		RET_VAL_EEXIST = Errno.EEXIST,
		RET_VAL_EFAULT = Errno.EFAULT,
		RET_VAL_EFBIG = Errno.EFBIG,
		RET_VAL_EILSEQ = Errno.EILSEQ,
		RET_VAL_EINTR = Errno.EINTR,
		RET_VAL_EINVAL = Errno.EINVAL,
		RET_VAL_EIO = Errno.EIO,
		RET_VAL_EISDIR = Errno.EISDIR,
		RET_VAL_EMFILE = Errno.EMFILE,
		RET_VAL_EMLINK = Errno.EMLINK,
		RET_VAL_ENAMETOOLONG = Errno.ENAMETOOLONG,
		RET_VAL_ENFILE = Errno.ENFILE,
		RET_VAL_ENODEV = Errno.ENODEV,
		RET_VAL_ENOENT = Errno.ENOENT,
		RET_VAL_ENOEXEC = Errno.ENOEXEC,
		RET_VAL_ENOLCK = Errno.ENOLCK,
		RET_VAL_ENOMEM = Errno.ENOMEM,
		RET_VAL_ENOSPC = Errno.ENOSPC,
		RET_VAL_ENOSYS = Errno.ENOSYS,
		RET_VAL_ENOTDIR = Errno.ENOTDIR,
		RET_VAL_ENOTEMPTY = Errno.ENOTEMPTY,
		/* Error codes used in the Secure CRT functions */
		RET_VAL_ENOTTY = Errno.ENOTTY,
		RET_VAL_ENXIO = Errno.ENXIO,
		RET_VAL_EPERM = Errno.EPERM,
		RET_VAL_EPIPE = Errno.EPIPE,
		RET_VAL_ERANGE = Errno.ERANGE,
		RET_VAL_EROFS = Errno.EROFS,
		RET_VAL_ESPIPE = Errno.ESPIPE,
		RET_VAL_ESRCH = Errno.ESRCH,
		RET_VAL_EXDEV = Errno.EXDEV,
		RET_VAL_STRUNCATE = Errno.STRUNCATE
	}

	public enum ReadStatus
	{
		Success = DbRetVal.SUCCESS,
		NotFound = DbRetVal.NOTFOUND,
		KeyEmpty = DbRetVal.KEYEMPTY,
		BufferSmall = DbRetVal.BUFFER_SMALL
	}

	public enum WriteStatus
	{
		Success = DbRetVal.SUCCESS,
		NotFound = DbRetVal.NOTFOUND,
		KeyExist = DbRetVal.KEYEXIST
	}

	public enum DeleteStatus
	{
		Success = DbRetVal.SUCCESS,
		NotFound = DbRetVal.NOTFOUND,
		KeyEmpty = DbRetVal.KEYEMPTY
	}
}
