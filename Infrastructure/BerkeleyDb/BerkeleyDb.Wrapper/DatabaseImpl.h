#pragma once
#include "Stdafx.h"
#include "DbtHolder.h"
#include "EnvironmentImpl.h"
#include "ConvStr.h"



namespace BerkeleyDbWrapper
{
	using namespace System::Runtime::InteropServices;

	using namespace System;
	using namespace System::Collections;
	using namespace System::IO;
	using namespace System::Security;
	using namespace MySpace::BerkeleyDb::Configuration;


	class TransactionContext;
	ref class CursorImpl;

	public ref class DatabaseImpl sealed : public Database 
	{
	public:
		DatabaseImpl(DatabaseConfig ^dbConfig);
		DatabaseImpl(); // ctor intended for pseudosingleton to handle static methods in base class

		~DatabaseImpl();
		!DatabaseImpl();

		int Id;

		virtual CacheSize^ GetCacheSize() override;
		virtual Cursor^ GetCursor() override;
		virtual DatabaseConfig^ GetDatabaseConfig() override;
		virtual DatabaseEntry^ Get(DatabaseEntry^ key, DatabaseEntry^ value) override;
		virtual DatabaseEntry^ Get(array<unsigned char>^ key, DatabaseEntry^ value) override;
		virtual DatabaseEntry^ Get(int key, DatabaseEntry^ value) override;
		virtual DatabaseType GetDatabaseType() override;
		virtual DbFlags GetFlags() override;
		virtual DbOpenFlags GetOpenFlags() override;
		virtual DbRetVal Delete(array<unsigned char>^ key) override;
		virtual DbRetVal Delete(int key) override;
		virtual DbRetVal Exists(DataBuffer key, ExistsOpFlags flags) override;
		virtual Stream^ Get(DataBuffer key, int offset, int length, GetOpFlags flags) override;
		virtual String^ Get(String^ key) override;
		virtual String^ GetErrorPrefix() override;
		virtual array<unsigned char>^ Get(int key, array<unsigned char>^ buffer) override;
		virtual array<unsigned char>^ GetBuffer(DataBuffer key, int offset, int length, GetOpFlags flags) override;
		virtual bool Delete(DataBuffer key, DeleteOpFlags flags) override;
		virtual int Compact(int fillPercentage, int maxPagesFreed, int implicitTxnTimeoutMsecs) override;
		virtual int Get(DataBuffer key, int offset, DataBuffer buffer, GetOpFlags flags) override;
		virtual int GetHashFillFactor() override;
		virtual int GetKeyCount(DbStatFlags statFlag) override;
		virtual int GetLength(DataBuffer key, GetOpFlags flags) override;
		virtual int GetPageSize() override;
		virtual int GetRecordLength() override;
		virtual int Put(DataBuffer key, int offset, int count, DataBuffer buffer, PutOpFlags flags) override;
		virtual int Truncate() override;
		virtual void BackupFromDisk(String^ backupFile, array<unsigned char>^ copyBuffer) override;
		virtual void BackupFromMpf(String^ backupFile, array<unsigned char>^ copyBuffer) override;
		virtual void Delete(DatabaseEntry^ key) override;
		virtual void Delete(String^ key) override;
		virtual void PrintStats(DbStatFlags statFlags) override;
		virtual void Put(DatabaseEntry^ key, DatabaseEntry^ value) override;
		virtual void Put(String^ key, String^ value) override;
		virtual void Put(array<unsigned char>^ key, DatabaseEntry^ value) override;
		virtual void Put(array<unsigned char>^ key, array<unsigned char>^ value) override;
		virtual void Put(int key, DatabaseEntry^ value) override;
		virtual void Put(int key, array<unsigned char>^ value) override;
		virtual void Put(int objectId, array<unsigned char>^ key, DatabaseEntry^ dbEntry, RMWDelegate^ rmwDelegate) override;
		virtual void Sync() override;

		virtual property BerkeleyDbWrapper::Environment^ Environment
		{
			BerkeleyDbWrapper::Environment^ get() override { return InternalEnvironment; }
		}
		virtual property bool Disposed
		{
			bool get() override { return disposed; }
		}
		virtual property int MaxDeadlockRetries
		{
			int get() override { return m_maxDeadlockRetries; }
		}

	protected:
		virtual DbRetVal DoVerify(String^ fileName) override;
		virtual void DoRemove(BerkeleyDbWrapper::Environment^ env, String^ fileName) override;


	internal:
		DatabaseImpl(EnvironmentImpl ^environment, DatabaseConfig^ dbconfig);
		void Log(int errNumber, const char *errMessage);
		property EnvironmentImpl^ InternalEnvironment
		{
			EnvironmentImpl^ get() { return environment; }
		}

		inline DbTxn *BeginTrans();
		inline void CommitTrans(DbTxn *txn);
		inline void RollbackTrans(DbTxn *txn);
		static PostAccessUnmanagedMemoryCleanup^ MemoryCleanup;
		Dbc *CreateCursorHandle();

	private:
		EnvironmentImpl^ environment;
		Db *m_pDb;
		DbEnv *m_pEnv;
		ConvStr *m_errpfx; 
		bool m_isTxn;
		bool m_isCDB;
		int m_maxDeadlockRetries;
		DatabaseConfig^ m_dbConfig;
		void Open(DbTxn *txn, Db* pDb, String ^path, DatabaseType type, DbOpenFlags flags);
		void Open(DatabaseConfig ^dbConfig);
		//void Open(String ^path, DatabaseType type, DbOpenFlags flags);
		DbRetVal Delete(Dbt *dbtKey);
		DbRetVal Get(Dbt *dbtKey, Dbt *dbtValue);
		void Put(Dbt *dbtKey, Dbt *dbtValue);
		//void Put(Dbt *dbtKey, Dbt *dbtValue, long lastUpdateTicks, bool bCheckRaceCondition);
		//void CheckRacePut(Dbt *dbtKey, Dbt *dbtValue, long lastUpdateTicks);
		//void MsgCall (const DbEnv *dbenv, char *msg);
		void Log(int objectId, int errNumber, const char *errMessage);
		void Log(System::String^ key, int errNumber, const char *errMessage);
		void Log(Dbt * dbtKey, int errNumber, const char *errMessage);
		//DbRetVal GetCurrent(Dbc *cursor, Dbt *dbtKey, Dbt *dbtValue);
		//void MemCpy(byte* ptrDest, byte* ptrSource, long len);
		bool disposed;
		const DatabaseTransactionMode m_pTrMode;
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
		TransactionContext(DatabaseImpl ^&db) : m_db(db), begun(false), txn(NULL) {}
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
		DatabaseImpl ^&m_db;
		bool begun;
		DbTxn *txn;
		// to prevent copying
		TransactionContext(const TransactionContext &context);
		TransactionContext& operator =(const TransactionContext &context);
	};

}
