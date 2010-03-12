#pragma once
#include "Stdafx.h"
#include "DbtHolder.h"
#include "Environment.h"
#include "DatabaseEntry.h"
#include "Databaserecord.h"
#include "CacheSize.h"
#include "ConvStr.h"
#include "OperationFlags.h"

using namespace System::Runtime::InteropServices;

using namespace System;
using namespace System::Collections;
using namespace System::IO;
using namespace System::Security;
using namespace MySpace::BerkeleyDb::Configuration;


namespace BerkeleyDbWrapper
{
	//[StructLayout(LayoutKind::Sequential, Pack = 1)]
 //   value struct PayloadStorage
 //   {
 //       bool Compressed;                                             //1
 //       int TTL;                                                     //5
 //       long LastUpdatedTicks;                                       //13
 //       long ExpirationTicks;                                        //21
 //   };
	//

	public delegate void RMWDelegate( DatabaseEntry ^ ) ;

	class TransactionContext;

	[SuppressUnmanagedCodeSecurity()]
	public ref class Database : Generic::IEnumerable<BerkeleyDbWrapper::DatabaseRecord^>
	{
	public:
		Database(DatabaseConfig ^dbConfig);

		~Database();
		!Database();

		int Id;

		BerkeleyDbWrapper::DbRetVal Delete(int key);
		BerkeleyDbWrapper::DbRetVal Delete(array<Byte> ^key);
		void Delete(String ^key);
		void Delete(DatabaseEntry ^key);

		String^ Get(String ^key);
		//array<Byte>^ Get(int key);
		array<Byte>^ Get(int key, array<Byte> ^buffer);
		BerkeleyDbWrapper::DatabaseEntry^ Get(DatabaseEntry ^key, DatabaseEntry ^value);
		BerkeleyDbWrapper::DatabaseEntry^ Get(int key, DatabaseEntry ^value);
		BerkeleyDbWrapper::DatabaseEntry^ Get(array<Byte> ^key, DatabaseEntry ^value);

		BerkeleyDbWrapper::CacheSize^ GetCacheSize();
		DatabaseConfig^ GetDatabaseConfig();
		String^ GetErrorPrefix();
		DbFlags GetFlags();
		int GetHashFillFactor();
		DbOpenFlags GetOpenFlags();
		int GetPageSize();
		int GetRecordLength();
		DatabaseType GetType();
		int GetKeyCount(DbStatFlags statFlag);
		//void Stat(DbStatFlags statFlags);
		void PrintStats(DbStatFlags statFlags);

		void Put(String ^key, String ^value);
		void Put(int key, array<Byte> ^value);
		void Put(int key, DatabaseEntry ^value);
		void Put(array<Byte> ^key, array<Byte> ^value);
		void Put(array<Byte> ^key, DatabaseEntry ^value);
		void Put(DatabaseEntry ^key, DatabaseEntry ^value);
		void Put(int objectId, array<Byte> ^key, DatabaseEntry ^dbEntry, RMWDelegate ^rmwDelegate);

		property BerkeleyDbWrapper::Environment^ Environment
		{
			BerkeleyDbWrapper::Environment^ get();
		}

		int Truncate();
		BerkeleyDbWrapper::DatabaseRecord^ GetCurrent(Dbc *cursor, DatabaseRecord ^value);
		bool MoveNext(Dbc *cursor);
		BerkeleyDbWrapper::DbRetVal Reset(Dbc *cursor);

		static DbRetVal Verify(String ^fileName);
		static void Remove(BerkeleyDbWrapper::Environment^ env, String^ fileName);
		void BackupFromMpf(String^ backupFile, array<Byte>^ copyBuffer);
		void BackupFromDisk(String^ backupFile, array<Byte>^ copyBuffer);
		int Compact(int fillPercentage, int maxPagesFreed, int implicitTxnTimeoutMsecs);
		void Sync();

		int Get(DataBuffer key, int offset, DataBuffer buffer, GetOpFlags flags);
		Stream^ Get(DataBuffer key, int offset, int length, GetOpFlags flags);
		int Put(DataBuffer key, int offset, int count, DataBuffer buffer, PutOpFlags flags);
		bool Delete(DataBuffer key, DeleteOpFlags flags);
		DbRetVal Exists(DataBuffer key, ExistsOpFlags flags);
		int GetLength(DataBuffer key, GetOpFlags flags);

		property bool Disposed
		{
			bool get() { return disposed; }
		}

		property int MaxDeadlockRetries
		{
			int get() { return m_maxDeadlockRetries; }
		}

	internal:
		Database(BerkeleyDbWrapper::Environment ^environment, DatabaseConfig^ dbCOnfig);
		void Log(int errNumber, const char *errMessage);

		inline DbTxn *BeginTrans();
		inline void CommitTrans(DbTxn *txn);
		inline void RollbackTrans(DbTxn *txn);
		static PostAccessUnmanagedMemoryCleanup^ MemoryCleanup;

	private:
		BerkeleyDbWrapper::Environment^ environment;
		Db *m_pDb;
		DbEnv *m_pEnv;
		ConvStr *m_errpfx; 
		bool m_isTxn;
		int m_maxDeadlockRetries;
		DatabaseConfig^ m_dbConfig;
		void Database::Open(DbTxn *txn, Db* pDb, String ^path, DatabaseType type, DbOpenFlags flags);
		void Open(DatabaseConfig ^dbConfig);
		//void Open(String ^path, DatabaseType type, DbOpenFlags flags);
		BerkeleyDbWrapper::DbRetVal Delete(Dbt *dbtKey);
		BerkeleyDbWrapper::DbRetVal Get(Dbt *dbtKey, Dbt *dbtValue);
		void Put(Dbt *dbtKey, Dbt *dbtValue);
		//void Put(Dbt *dbtKey, Dbt *dbtValue, long lastUpdateTicks, bool bCheckRaceCondition);
		//void CheckRacePut(Dbt *dbtKey, Dbt *dbtValue, long lastUpdateTicks);
		//void MsgCall (const DbEnv *dbenv, char *msg);
		void Log(int objectId, int errNumber, const char *errMessage);
		void Log(System::String^ key, int errNumber, const char *errMessage);
		void Log(Dbt * dbtKey, int errNumber, const char *errMessage);
		BerkeleyDbWrapper::DbRetVal GetCurrent(Dbc *cursor, Dbt *dbtKey, Dbt *dbtValue);
		//void MemCpy(byte* ptrDest, byte* ptrSource, long len);
		bool disposed;
		const DatabaseTransactionMode m_pTrMode;

	public:
		virtual Generic::IEnumerator<DatabaseRecord^>^ GetEnumerator();

		virtual System::Collections::IEnumerator^ GetEnumeratorNonGeneric() = System::Collections::IEnumerable::GetEnumerator
		{
			return GetEnumerator();
		}

		Dbc *GetCursor();
	private:
		typedef int (*BdbCall)(Db *, DbTxn *, Dbt *, Dbt *, int);
		int DeadlockLoop(String ^methodName, TransactionContext &context, Dbt *key, Dbt *data, int options,
			BdbCall bdbCall);
		int TryStd(String ^methodName, TransactionContext &context, Dbt *key, Dbt *data, int options,
			BdbCall bdbCall);
		int TryMemStd(String ^methodName, TransactionContext &context, Dbt *key, Dbt *data, int *sizePtr,
			int options, BdbCall bdbCall);
		int SwitchMemStd(String ^methodName, TransactionContext &context, int ret, int size);
		void SwitchStd(String ^methodName, TransactionContext &context, int ret);
	};

	class TransactionContext
	{
	public:
		TransactionContext(Database ^&db) : m_db(db), begun(false), txn(NULL) {}
		DbTxn *begin()
		{
			if (!begun)
			{
				txn = m_db->BeginTrans();
				begun = true;
			}
			return txn;
		}
		void commit()
		{
			if (begun)
			{
				m_db->CommitTrans(txn);
				txn = NULL;
				begun = false;
			}
		}
		void rollback()
		{
			if (begun)
			{
				// for rollback set begun to false first in case rollback throws we don't
				// want it tried again from destructor
				begun = false;
				m_db->RollbackTrans(txn);
				txn = NULL;
			}
		}
		~TransactionContext()
		{
			rollback();
		}
	private:
		Database ^&m_db;
		bool begun;
		DbTxn *txn;
		// to prevent copying
		TransactionContext(const TransactionContext &context);
		TransactionContext& operator =(const TransactionContext &context);
	};

}