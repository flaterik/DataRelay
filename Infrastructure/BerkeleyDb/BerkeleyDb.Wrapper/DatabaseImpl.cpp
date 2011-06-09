#include <stdio.h>
#include "stdafx.h"
#include "DatabaseImpl.h"
#include "CursorImpl.h"
#include "BdbExceptionFactory.h"
#include "Alloc.h"

using namespace std;
using namespace System::Runtime::InteropServices;
using namespace System::IO;
using namespace BerkeleyDbWrapper;

#define CheckForEmptyKey(key, methodName) \
	if ((key)->Length == 0) \
		throw BdbExceptionFactory::Create((int)DbRetVal::KEYZEROLENGTH ,"BerkeleyDbWrapper:Database:" + (methodName) + ": Zero length key not allowed")

#define CheckForNullKey(key, methodName) \
	if ((key) == nullptr) \
		throw BdbExceptionFactory::Create((int)DbRetVal::KEYNULL ,"BerkeleyDbWrapper:Database:" + (methodName) + ": Null key not allowed")

#define CheckForNullOrEmptyKey(key, methodName) \
	CheckForNullKey(key, methodName); \
	else CheckForEmptyKey(key, methodName)

DatabaseImpl::DatabaseImpl(DatabaseConfig^ dbConfig): 
	m_pDb(NULL), m_pEnv(NULL), m_errpfx(0), m_dbConfig(dbConfig), Id(dbConfig->Id),
	m_isTxn(false), m_maxDeadlockRetries(1), m_pTrMode(dbConfig->TransactionMode),
	disposed(false), m_isCDB(false)
{
	try
	{
		m_pDb = new Db(0, 0);
		m_pDb->set_alloc(&malloc_wrapper, &realloc_wrapper, &free_wrapper);
		this->Open(dbConfig);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(&ex, gcnew String(ex.what()));
	}
}

DatabaseImpl::DatabaseImpl(EnvironmentImpl^ environment, DatabaseConfig^ dbConfig): 
	environment(environment), m_pDb(NULL), m_pEnv(environment->Handle), m_errpfx(0), m_dbConfig(dbConfig), Id(dbConfig->Id), 
	m_isTxn(false), m_maxDeadlockRetries(1), m_pTrMode(dbConfig->TransactionMode),
	disposed(false), m_isCDB((environment->GetOpenFlags() & EnvOpenFlags::InitCDB) == EnvOpenFlags::InitCDB)
{
	try
	{
		m_pDb = new Db(m_pEnv, 0);
		EnvOpenFlags envOpenFlags = environment->GetOpenFlags();
		if ((envOpenFlags & EnvOpenFlags::InitTxn)== EnvOpenFlags::InitTxn)
		{
			m_isTxn = true;
			m_maxDeadlockRetries = dbConfig->MaxDeadlockRetries;
		}

		this->Open(dbConfig);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(&ex, gcnew String(ex.what()));
	}
}

DatabaseImpl::DatabaseImpl():
	environment(nullptr), m_pDb(NULL), m_pEnv(NULL), m_errpfx(0), m_dbConfig(nullptr), Id(0), disposed(false),
	m_pTrMode()
{
}

DatabaseImpl::!DatabaseImpl()
{
	disposed = true;
	if (m_pDb != NULL)
	{
		try
		{
			m_pDb->close(0);
		}
		catch (const exception &ex)
		{
			if (environment != nullptr)
			{
				try
				{
					environment->RaisePanicEvent("Bdb", Marshal::PtrToStringAnsi(
						IntPtr(const_cast<char*>(ex.what()))));
				}
				catch(...)
				{
				}
			}
		}
		finally
		{
			m_pDb = NULL;
			if (m_errpfx != NULL)
			{
				try
				{
					delete m_errpfx;
				}
				finally
				{
					m_errpfx = NULL;
				}
			}
		}
	}
}

DatabaseImpl::~DatabaseImpl()
{
	this->!DatabaseImpl();
}

void DatabaseImpl::Open(DbTxn *txn, Db* pDb, String^ fileName, DatabaseType type, DbOpenFlags flags)
{
	if (pDb == NULL)
	{
		throw gcnew InvalidOperationException("Database handle is not created. Cannot open database.");
	}

	int ret = 0;
	if (pDb->get_env() == NULL)
	{
		ret = pDb->set_alloc(&malloc_wrapper, &realloc_wrapper, &free_wrapper);
	}
	ConvStr fn(fileName);
	ret = m_pDb->open(
		txn,
		fn.Str(),
		NULL,
		static_cast<DBTYPE>(type),
		static_cast<u_int32_t>(flags),
		0
		);
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Open: Unexpected error with ret value " + ret);
	}
}

void DatabaseImpl::Open(DatabaseConfig^ dbConfig)
{
	if (m_pDb == NULL)
	{
		m_pDb = new Db(m_pEnv, 0);
	}
	EnvironmentImpl::SetUserCopy(m_pDb->get_DB()->env, true);
	
	int ret = 0;
	DbTxn *txn = NULL;
	u_int32_t dbflags = 0;
	DbOpenFlags dbOpenFlags = DbOpenFlags::None;

	try
	{
		m_errpfx = new ConvStr("Bdb");
		m_pDb->set_errpfx(m_errpfx->Str());
		
		u_int pageSize = dbConfig->PageSize;
		if (pageSize > 0)
		{
			ret = m_pDb->set_pagesize(pageSize);
		}
		int hashFillFactor;
		u_int hashSize;
		int recordLen;
		DatabaseType dbType = dbConfig->Type;
		switch (dbType)
		{
		case DatabaseType::BTree:
		case DatabaseType::Hash:
			hashFillFactor = dbConfig->HashFillFactor;
			if (hashFillFactor > 0)
			{
				ret = m_pDb->set_h_ffactor(hashFillFactor);
			}
			
			hashSize = dbConfig->HashSize;
			if (hashSize > 0)
			{
				ret = m_pDb->set_h_nelem(hashSize);
			}
			break;
		case DatabaseType::Queue:
			recordLen = dbConfig->RecordLength;
			if (recordLen > 0)
			{
				ret = m_pDb->set_re_len(recordLen);
			}
			break;
		case DatabaseType::Unknown:
			break;
		default:
			if (m_pDb != NULL)
			{
				try
				{
					m_pDb->close(0);
				}
				finally
				{
					m_pDb = NULL;
				}
			}
			throw BdbExceptionFactory::Create(NULL, "BerkeleyDbWrapper:Database:Open: Unknown Database Type");
		}
		if (dbConfig->Flags != DbFlags::None)
		{
			dbflags = static_cast<u_int32_t>(dbConfig->Flags);
			ret = m_pDb->set_flags(dbflags);
		}

		dbOpenFlags = dbConfig->OpenFlags;
		if (m_isTxn 
			&& (dbOpenFlags & DbOpenFlags::AutoCommit) != DbOpenFlags::AutoCommit)
		{
			txn = BeginTrans();
		}
		this->Open(txn, m_pDb, dbConfig->FileName, dbType, dbOpenFlags);
		if (txn != NULL)
		{
			CommitTrans(txn);
		}
	}
	catch (const exception &ex)
	{
		if (txn != NULL)
		{
			try 
			{
				// For a generic error, log it and abort.
				RollbackTrans(txn);
			} 
			catch (DbException &ae) 
			{
				Log(ae.get_errno(), "txn abort failed in DbOpen.");
			}
		}
		if (m_pDb != NULL)
		{
			try
			{
				m_pDb->close(0);
			}
			finally
			{
				m_pDb = NULL;
			}
		}
		throw BdbExceptionFactory::Create(ret, &ex, String::Format("Flags: {0}, OpenFlags: {1} - {2}", dbflags,
			static_cast<u_int32_t>(dbOpenFlags), gcnew String(ex.what())));
	}
	switch(ret)
	{
		case DbRetVal::SUCCESS:
			break;
		default:
			if (m_pDb != NULL)
			{
				try
				{
					m_pDb->close(0);
				}
				finally
				{
					m_pDb = NULL;
				}
			}
			throw BdbExceptionFactory::Create(ret, String::Format(
				"BerkeleyDbWrapper:Database:Open: Unexpected error with ret value {0}, Flags: {0}, OpenFlags: {1}",
				ret, dbflags, static_cast<u_int32_t>(dbOpenFlags)));
	}

}

//void DatabaseImpl::Put(Dbt *dbtKey, Dbt *dbtValue, long lastUpdateTicks, bool bCheckRaceCondition)
//{
//	if( bCheckRaceCondition == false)
//	{
//		Put( dbtKey, dbtValue ) ;
//	}
//	else
//	{
//		CheckRacePut(dbtKey, dbtValue, lastUpdateTicks);
//	}
//}

void DatabaseImpl::Put(Dbt *dbtKey, Dbt *dbtValue)
{
	int ret = 0;
	DBTYPE dbType = DB_UNKNOWN;
	DbTxn *txn = NULL;
	int retry_count = 0;

	// retry_count is a counter used to identify how many times
	// we've retried this operation. To avoid the potential for 
	// endless looping, we won't retry more than MAX_DEADLOCK_RETRIES 
	// times.

	// txn is a transaction handle.
	// key and data are DBT handles. Their usage is not shown here.
	while (retry_count < m_maxDeadlockRetries)
	{
		try
		{
			txn = BeginTrans();
			ret = m_pDb->put(txn, dbtKey, dbtValue, 0);
			CommitTrans(txn);
			switch(ret)
			{
				case DbRetVal::SUCCESS:
					return;
				case DbRetVal::NOTFOUND:
				case DbRetVal::KEYEMPTY:
				default:
					throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unexpected error with ret value " + ret);
			}
		}
		catch (DbDeadlockException &de) 
		{
			try 
			{
				// Abort the transaction and increment the 
				// retry counter
				RollbackTrans(txn);
				retry_count++;
				// If we've retried too many times, log it and exit
				if (retry_count >= m_maxDeadlockRetries)
				{
					m_pEnv->errx("Put exceeded retry limit. Giving up.");
					//return (EXIT_FAILURE);
					throw BdbExceptionFactory::Create(ret, &de, gcnew String(de.what()));
				}
				Log(dbtKey, de.get_errno(), "Retrying");
			} 
			catch (DbException &ae) 
			{
				Log(dbtKey, ae.get_errno(), "txn abort failed in Put.");
				//return (EXIT_FAILURE);
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()));
			}
		}
		catch (const DbException &ex)
		{
			try 
			{
				// For a generic error, log it and abort.
				Log(dbtKey, ex.get_errno(), "Error getting data");
				RollbackTrans(txn);
			} 
			catch (DbException &ae) 
			{
				Log(dbtKey, ae.get_errno(), "txn abort failed in Put.");
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ex.what()) + "\n" + gcnew String(ae.what()));
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		catch (const exception &ex)
		{
			if (txn != NULL)
			{
				try 
				{
					// For a generic error, log it and abort.
					RollbackTrans(txn);
				} 
				catch (DbException &ae) 
				{
					Log(dbtKey, ae.get_errno(), "txn abort failed in Put.");
					throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()) + "\n" + gcnew String(ex.what()));
				}
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
	}
}

void DatabaseImpl::Put(int objectId, array<Byte> ^key, DatabaseEntry ^dbEntry, RMWDelegate ^rmwDelegate)
{
	int ret = 0;

	Dbt dbtKey;//(pKeyData, keyBuffer->Length);
	if( key != nullptr )
	{
		CheckForEmptyKey(key, "Put");
		array<Byte> ^keyBuffer = key;
		pin_ptr<Byte> pKeyData(&keyBuffer[0]);
		dbtKey.set_data( pKeyData ) ;
		dbtKey.set_size(key->Length);
	}
	else
	{
		dbtKey.set_data( &objectId ) ;
		dbtKey.set_size(sizeof(int));
	}

	array<Byte> ^valueBuffer = dbEntry->Buffer;
	pin_ptr<Byte> pValueData(&valueBuffer[0]);
	int len = valueBuffer->Length;
	Dbt dbtGetValue(pValueData, len);

	// set up the partial record info
	dbtGetValue.set_dlen(dbEntry->Length);
	dbtGetValue.set_doff(dbEntry->StartPosition);
	dbtGetValue.set_size(dbEntry->Length);
	dbtGetValue.set_flags(DB_DBT_USERMEM | DB_DBT_PARTIAL);
	dbtGetValue.set_ulen( len );

	// put params ready
	DBTYPE dbType = DB_UNKNOWN;
	DbTxn *txn = NULL;
	int retry_count = 0;
	Dbc *cur = NULL;

	while (retry_count < m_maxDeadlockRetries)
	{
		try
		{
			txn = BeginTrans();

			// create the cursor
			ret = m_pDb->cursor(txn, &cur, m_isCDB ? DB_WRITECURSOR : 0);
			switch(ret)
			{
				case DbRetVal::SUCCESS:
					break;
				default:
					throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unexpected error on cursor open with ret value " + ret);
			}
			
			// get record with cursor retained read lock
			ret = cur->get(&dbtKey, &dbtGetValue, DB_SET | DB_RMW);
			
			// get returned buffer and overlay with PayloadStorage structure
			switch(ret)
			{

				case DbRetVal::NOTFOUND:
				case DbRetVal::KEYEMPTY:
					dbEntry->Length = 0;
				case DbRetVal::SUCCESS:

					rmwDelegate(dbEntry);
					if (dbEntry->Length > 0)
					{
						/* update dbtValue with data from dbEntry*/

						// if update the whole record greater than stored record, do update
						array<Byte> ^value = dbEntry->Buffer;
						pin_ptr<Byte> pValue(&value[0]);
						int len = value->Length;
						Dbt dbtSetValue(pValue, len);
						dbtSetValue.set_flags(DB_DBT_USERMEM);

						switch(ret)
						{
							case DbRetVal::SUCCESS:
								// modify existing record.
								ret = cur->put(NULL, &dbtSetValue, DB_CURRENT);
								break;
							case DbRetVal::NOTFOUND:
							case DbRetVal::KEYEMPTY:
								// add new record
								if (m_isCDB)
								{
									ret = cur->put(&dbtKey, &dbtSetValue, DB_KEYFIRST);
								}
								else
								{
									// original get didn't find a record, so no lock retained
									// do a put with no overwrite, and treat created by other
									// thread as a deadlock
									ret = m_pDb->put(txn, &dbtKey, &dbtSetValue, DB_NOOVERWRITE);
									switch(ret)
									{
										case DbRetVal::KEYEXIST:
											ret = static_cast<int>(DbRetVal::LOCK_DEADLOCK);
											--retry_count; // max count is always 1 if non-txn, so have to decrement
											throw DbDeadlockException("Other thread preempted add record");
									}
								}
								break;
						}


					}
					else if(dbEntry->Length == 0)
					{
						switch(ret)
						{
							case DbRetVal::SUCCESS:
								//delete stored record.
								ret = cur->del(0);
								break;
							case DbRetVal::NOTFOUND:
							case DbRetVal::KEYEMPTY:
								//no initial record so no action but set return value to success
								//to pass next check
								ret = static_cast<int>(DbRetVal::SUCCESS);
								break;
						}
					}
					break;

				default:
					throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unexpected error with ret value " + ret);
			}

			switch(ret)
			{
				case DbRetVal::SUCCESS:
					break;
				case DbRetVal::NOTFOUND:
				case DbRetVal::KEYEMPTY:
				default:
					throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unexpected error with ret value " + ret);
			}

			try
			{
				ret = cur->close();
				switch(ret)
				{
					case DbRetVal::SUCCESS:
						break;
					default:
						throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unexpected error on cursor close with ret value " + ret);
				}
			}
			catch (exception &cle) 
			{
				throw BdbExceptionFactory::Create(&cle, nullptr);
			}
			finally
			{
				cur = NULL;
			}

			CommitTrans(txn);

			return;
		}
		catch (DbDeadlockException &de) 
		{
			try 
			{
				// Abort the transaction and increment the 
				// retry counter
				RollbackTrans(txn);
				retry_count++;
				// If we've retried too many times, log it and exit
				if (retry_count >= m_maxDeadlockRetries)
				{
					m_pEnv->errx("Put exceeded retry limit. Giving up.");
					//return (EXIT_FAILURE);
					throw BdbExceptionFactory::Create(ret, &de, gcnew String(de.what()));
				}
				Log(objectId, de.get_errno(), "Retrying");
			} 
			catch (DbException &ae) 
			{
				Log(objectId, ae.get_errno(), "txn abort failed in Put.");
				//return (EXIT_FAILURE);
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()));
			}
		}
		catch (const DbException &ex)
		{

			try 
			{
				// For a generic error, log it and abort.
				Log(objectId, ex.get_errno(), "Error getting data");
				RollbackTrans(txn);
			} 
			catch (DbException &ae) 
			{
				Log(objectId, ae.get_errno(), "txn abort failed in Put.");
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ex.what()) + "\n" + gcnew String(ae.what()));
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		catch (const exception &ex)
		{
			if (txn != NULL)
			{
				try 
				{
					// For a generic error, log it and abort.
					RollbackTrans(txn);
				} 
				catch (DbException &ae) 
				{
					Log(objectId, ae.get_errno(), "txn abort failed in Put.");
					throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()) + "\n" + gcnew String(ex.what()));
				}
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		finally
		{
			if (cur != NULL)
			{
				try
				{
					ret = cur->close();
					switch(ret)
					{
						case DbRetVal::SUCCESS:
							break;
						default:
							throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unexpected error on cursor close with ret value " + ret);
					}
				}
				catch (exception &cle) 
				{
					throw BdbExceptionFactory::Create(&cle, nullptr);
				}
				finally
				{
					cur = NULL;
				}
			}
		}
	}
}

void DatabaseImpl::Put(String ^key, String ^value)
{
	CheckForNullOrEmptyKey(key, "Put");
	int ret = 0;
	DBTYPE dbType = DB_UNKNOWN;

	try
	{
		Dbt dbtKey;
		// Store as Unicode in this example
		pin_ptr<const wchar_t> pKeyStr = PtrToStringChars(key);
		dbtKey.set_data(const_cast<wchar_t*>(pKeyStr));

		ret = m_pDb->get_type(&dbType);
		if (ret)
		{
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Error getting DbType with ret value " + ret);
		}

		switch (dbType)
		{
		case DB_HASH:
		case DB_BTREE:
			dbtKey.set_size(key->Length * sizeof(Char));
			break;
		case DB_QUEUE:
		case DB_RECNO:
			dbtKey.set_size(sizeof(db_recno_t));
			break;
		case DB_UNKNOWN:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: DBTYPE = DB_UNKNOWN.");
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unknown DBTYPE");
		}

		pin_ptr<const wchar_t> pValueStr = PtrToStringChars(value);
		Dbt dbtValue(const_cast<wchar_t*>(pValueStr), value->Length * sizeof(Char));

		//Put(&dbtKey, &dbtValue, ((PayloadStorage *)pValueStr)->LastUpdatedTicks, m_dbConfig->CheckRaceCondition );
		Put(&dbtKey, &dbtValue );
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
}

void DatabaseImpl::Put(int key, array<Byte> ^value)
{
	int ret = 0;
	DBTYPE dbType = DB_UNKNOWN;

	try
	{
		Dbt dbtKey;
		dbtKey.set_data(&key);

		ret = m_pDb->get_type(&dbType);
		if (ret)
		{
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Error getting DbType with ret value " + ret);
		}

		switch (dbType)
		{
		case DB_HASH:
		case DB_BTREE:
			dbtKey.set_size(sizeof(key));
			break;
		case DB_QUEUE:
		case DB_RECNO:
			dbtKey.set_size(sizeof(db_recno_t));
			break;
		case DB_UNKNOWN:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: DBTYPE = DB_UNKNOWN.");
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unknown DBTYPE");
		}

		pin_ptr<Byte> pData = nullptr;
		u_int32_t size = 0;
		if (value != nullptr)
		{
			pData = &value[0];
			size = value->Length;
		}
		Dbt dbtValue(pData, size);

		//Put(&dbtKey, &dbtValue, ((PayloadStorage *)pData)->LastUpdatedTicks, m_dbConfig->CheckRaceCondition );
		Put(&dbtKey, &dbtValue);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
}

void DatabaseImpl::Put(int key, DatabaseEntry ^value)
{
	int ret = 0;
	DBTYPE dbType = DB_UNKNOWN;

	try
	{
		Dbt dbtKey;
		dbtKey.set_data(&key);

		ret = m_pDb->get_type(&dbType);
		if (ret)
		{
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Error getting DbType with ret value " + ret);
		}

		switch (dbType)
		{
		case DB_HASH:
		case DB_BTREE:
			dbtKey.set_size(sizeof(key));
			break;
		case DB_QUEUE:
		case DB_RECNO:
			dbtKey.set_size(sizeof(db_recno_t));
			break;
		case DB_UNKNOWN:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: DBTYPE = DB_UNKNOWN.");
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unknown DBTYPE");
		}

		pin_ptr<Byte> pValueData = nullptr;
		array<Byte> ^valueBuffer = value->Buffer;
		u_int32_t size = 0;
		if (value != nullptr && valueBuffer != nullptr)
		{
			pValueData = &valueBuffer[value->StartPosition];
			size = value->Length;
		}
		Dbt dbtValue(pValueData, size);

		//Put(&dbtKey, &dbtValue, ((PayloadStorage *)pValueData)->LastUpdatedTicks, m_dbConfig->CheckRaceCondition );
		Put(&dbtKey, &dbtValue);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
}

void DatabaseImpl::Put(array<Byte> ^key, array<Byte> ^value)
{
	CheckForNullOrEmptyKey(key, "Put");
	int ret = 0;
	DBTYPE dbType = DB_UNKNOWN;

	try
	{
		Dbt dbtKey;
		pin_ptr<Byte> pKey = &key[0];
		dbtKey.set_data(pKey);

		ret = m_pDb->get_type(&dbType);
		if (ret)
		{
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Error getting DbType with ret value " + ret);
		}

		switch (dbType)
		{
		case DB_HASH:
		case DB_BTREE:
			dbtKey.set_size(key->Length);
			break;
		case DB_QUEUE:
		case DB_RECNO:
			dbtKey.set_size(sizeof(db_recno_t));
			break;
		case DB_UNKNOWN:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: DBTYPE = DB_UNKNOWN.");
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unknown DBTYPE");
		}

		pin_ptr<Byte> pData = nullptr;
		u_int32_t size = 0;
		if (value != nullptr)
		{
			pData = &value[0];
			size = value->Length;
		}
		Dbt dbtValue(pData, size);

		//Put(&dbtKey, &dbtValue, ((PayloadStorage *)pData)->LastUpdatedTicks, m_dbConfig->CheckRaceCondition );
		Put(&dbtKey, &dbtValue);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
}

void DatabaseImpl::Put(array<Byte> ^key, DatabaseEntry ^value)
{
	CheckForNullOrEmptyKey(key, "Put");
	int ret = 0;
	DBTYPE dbType = DB_UNKNOWN;

	try
	{
		Dbt dbtKey;
		pin_ptr<Byte> pKey = &key[0];
		dbtKey.set_data(pKey);

		ret = m_pDb->get_type(&dbType);
		if (ret)
		{
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Error getting DbType with ret value " + ret);
		}

		switch (dbType)
		{
		case DB_HASH:
		case DB_BTREE:
			dbtKey.set_size(key->Length);
			break;
		case DB_QUEUE:
		case DB_RECNO:
			dbtKey.set_size(sizeof(db_recno_t));
			break;
		case DB_UNKNOWN:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: DBTYPE = DB_UNKNOWN.");
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unknown DBTYPE");
		}

		pin_ptr<Byte> pValueData = nullptr;
		array<Byte> ^valueBuffer = value->Buffer;
		u_int32_t size = 0;
		if (value != nullptr && valueBuffer != nullptr)
		{
			pValueData = &valueBuffer[value->StartPosition];
			size = value->Length;
		}
		Dbt dbtValue(pValueData, size);

		//Put(&dbtKey, &dbtValue, ((PayloadStorage *)pValueData)->LastUpdatedTicks, m_dbConfig->CheckRaceCondition );
		Put(&dbtKey, &dbtValue);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
}

void DatabaseImpl::Put(DatabaseEntry ^key, DatabaseEntry ^value)
{
	CheckForNullKey(key, "Put");
	int ret = 0;
	DBTYPE dbType = DB_UNKNOWN;

	try
	{
		Dbt dbtKey;
		array<Byte> ^keyBuffer = key->Buffer;
		CheckForNullOrEmptyKey(keyBuffer, "Put");
		pin_ptr<Byte> pKeyData(&keyBuffer[0]);
		dbtKey.set_data(pKeyData);

		ret = m_pDb->get_type(&dbType);
		if (ret)
		{
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Error getting DbType with ret value " + ret);
		}

		switch (dbType)
		{
		case DB_HASH:
		case DB_BTREE:
			dbtKey.set_size(keyBuffer->Length);
			break;
		case DB_QUEUE:
		case DB_RECNO:
			dbtKey.set_size(sizeof(db_recno_t));
			break;
		case DB_UNKNOWN:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: DBTYPE = DB_UNKNOWN.");
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Put: Unknown DBTYPE");
		}

		pin_ptr<Byte> pValueData = nullptr;
		array<Byte> ^valueBuffer = value->Buffer;
		u_int32_t size = 0;
		if (value != nullptr && valueBuffer != nullptr)
		{
			pValueData = &valueBuffer[value->StartPosition];
			size = value->Length;
		}
		Dbt dbtValue(pValueData, size);

		//Put(&dbtKey, &dbtValue, ((PayloadStorage *)pValueData)->LastUpdatedTicks, m_dbConfig->CheckRaceCondition );
		Put(&dbtKey, &dbtValue);
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
}

DbRetVal DatabaseImpl::Get(Dbt *dbtKey, Dbt *dbtValue)
{
	int ret = 0;

	BufferSmallException^ e;
	DbTxn *txn = NULL;
	int retry_count = 0;

	// retry_count is a counter used to identify how many times
	// we've retried this operation. To avoid the potential for 
	// endless looping, we won't retry more than MAX_DEADLOCK_RETRIES 
	// times.

	while (retry_count < m_maxDeadlockRetries)
	{
		try
		{
			txn = BeginTrans();
			ret = m_pDb->get(txn, dbtKey, dbtValue, NULL);
			CommitTrans(txn);
		} 
		catch (DbDeadlockException &de) 
		{
			try 
			{
				// Abort the transaction and increment the 
				// retry counter
				RollbackTrans(txn);
				retry_count++;
				// If we've retried too many times, log it and exit
				if (retry_count >= m_maxDeadlockRetries)
				{
					m_pEnv->errx("Get exceeded retry limit. Giving up.");
					throw BdbExceptionFactory::Create(ret, &de, gcnew String(de.what()));
				}
				Log(dbtKey, de.get_errno(), "Retrying");
			} 
			catch (DbException &ae) 
			{
				Log(dbtKey, ae.get_errno(), "txn abort failed in Get.");
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()));
			}
		}
		catch (const DbException &ex)
		{
			try 
			{
				// For a generic error, log it and abort.
				Log(dbtKey, ex.get_errno(), "Error getting data");
				RollbackTrans(txn);
			} 
			catch (DbException &ae) 
			{
				Log(dbtKey, ae.get_errno(), "txn abort failed in Get.");
				//return (EXIT_FAILURE);    
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ex.what()) + "\n" + gcnew String(ae.what()));
			}
			if (ex.get_errno() == static_cast<int>(DbRetVal::BUFFER_SMALL))
			{
				e = gcnew BufferSmallException(dbtValue->get_ulen(), dbtValue->get_size(), "Buffer is too small");
				throw e;
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		catch (const exception &ex)
		{
			if (txn != NULL)
			{
				try 
				{
					// For a generic error, log it and abort.
					RollbackTrans(txn);
				} 
				catch (DbException &ae) 
				{
					Log(dbtKey, ae.get_errno(), "txn abort failed in Get.");
					throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()) + "\n" + gcnew String(ex.what()));
				}
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		switch(ret)
		{
		case DbRetVal::SUCCESS:
		case DbRetVal::NOTFOUND:
		case DbRetVal::KEYEMPTY:
			return (DbRetVal)ret;
		case DbRetVal::BUFFER_SMALL:
			e = gcnew BufferSmallException(dbtValue->get_ulen(), dbtValue->get_size(), "Buffer is too small");
			throw e;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Get: Unexpected error with ret value " + ret);
		}
	}
	return (DbRetVal)ret; //can't get here, but compiler doesn't know that
}

int DatabaseImpl::DeadlockLoop(String ^methodName, TransactionContext &context, Dbt *key,
	Dbt *data, int options, BdbCall bdbCall)
{
	const int intDeadlockValue = static_cast<int>(DbRetVal::LOCK_DEADLOCK); 
	int retry_count = 0; 
	bool deadlock_occurred = false; 
	int ret = 0;
	do 
	{ 
		deadlock_occurred = false; 
		ret = 0; 
		try 
		{ 
			ret = bdbCall(m_pDb, context.begin(), key, data, options);
			deadlock_occurred = (ret == intDeadlockValue);
		} 
		catch(DbDeadlockException) 
		{ 
			deadlock_occurred = true; 
		} 
		if (deadlock_occurred) 
		{ 
			Log(intDeadlockValue, "Deadlock"); 
			context.rollback();
			++retry_count; 
			if (retry_count >= m_maxDeadlockRetries) break; 
		} 
	} while(deadlock_occurred); 
	if (deadlock_occurred) 
	{ 
		ConvStr msg(methodName + " exceeded retry limit. Giving up."); 
		m_pEnv->errx(msg.Str()); 
		throw BdbExceptionFactory::Create(intDeadlockValue, gcnew String(db_strerror(intDeadlockValue))); 
	}
	return ret;
}

int DatabaseImpl::TryStd(String ^methodName, TransactionContext &context, Dbt *key,
	Dbt *data, int options, BdbCall bdbCall)
{
	try
	{
		return DeadlockLoop(methodName, context, key, data, options, bdbCall);
	}
	catch (const exception &ex) 
	{ 
		throw BdbExceptionFactory::Create(&ex, "BerkeleyDbWrapper:Database:" + methodName); 
	}
}

int DatabaseImpl::TryMemStd(String ^methodName, TransactionContext &context, Dbt *key,
	Dbt *data, int *sizePtr, int options, BdbCall bdbCall)
{
	int ret = 0;
	try
	{
		ret = DeadlockLoop(methodName, context, key, data, options, bdbCall);
		*sizePtr = data->get_size();
	} 
	catch(DbMemoryException &mex)
	{
		ret = static_cast<int>(DbRetVal::BUFFER_SMALL);
		*sizePtr = mex.get_dbt()->get_size();
	}
	catch (const exception &ex) 
	{ 
		throw BdbExceptionFactory::Create(&ex, "BerkeleyDbWrapper:Database:" + methodName); 
	}
	return ret;
}

void DatabaseImpl::SwitchStd(String ^methodName, TransactionContext &context, int ret)
{
	switch(ret) { 
	case DbRetVal::SUCCESS:
		break; 
	default:
		throw BdbExceptionFactory::Create(ret, String::Format(
			L"BerkeleyDbWrapper:Database:{0}: Unexpected error with ret value {1}", methodName, ret));
	}
	context.commit();
}

int DatabaseImpl::SwitchMemStd(String ^methodName, TransactionContext &context, int ret, int size)
{
	switch(ret) {
	case DbRetVal::SUCCESS:
	case DbRetVal::BUFFER_SMALL:
		break;
	case DbRetVal::NOTFOUND:
	case DbRetVal::KEYEMPTY:
		size = -1;
		break;
	default:
		throw BdbExceptionFactory::Create(ret, String::Format(
			L"BerkeleyDbWrapper:Database:{0}: Unexpected error with ret value {1}", methodName, ret));
	}
	context.commit();
	return size;
}

int get_core(Db *db, DbTxn *txn, Dbt *key, Dbt *data, int options)
{
	return db->get(txn, key, data, options);
}

int put_core(Db *db, DbTxn *txn, Dbt *key, Dbt *data, int options)
{
	return db->put(txn, key, data, options);
}

int del_core(Db *db, DbTxn *txn, Dbt *key, Dbt *data, int options)
{
	return db->del(txn, key, options);
}

int exists_core(Db *db, DbTxn *txn, Dbt *key, Dbt *data, int options)
{
	return db->exists(txn, key, options);
}

int DatabaseImpl::Get(DataBuffer key, int offset, DataBuffer buffer,
	GetOpFlags flags)
{
	int ret = 0;
	int size = -1;
	DatabaseImpl ^db = this;
	TransactionContext context(db);
	{
		DbtHolder dbtKey;
		DbtHolder dbtBuffer;
		dbtKey.initialize_for_read(key);
		dbtBuffer.initialize_for_write(buffer);
		if (offset >= 0) {
			dbtBuffer.set_for_partial(offset, dbtBuffer.get_size());
		}
		ret = TryMemStd("Get", context, &dbtKey, &dbtBuffer, &size, static_cast<int>(flags),
			&get_core);
	}
	return SwitchMemStd("Get", context, ret, size);
}

Stream^ DatabaseImpl::Get(DataBuffer key,
	int offset, int length, GetOpFlags flags)
{
	int ret = 0;
	int size = -1;
	DatabaseImpl ^db = this;
	DbtExtended dbtBuffer;
	dbtBuffer.set_flags(DB_DBT_MALLOC);
	TransactionContext context(db);
	{
		DbtHolder dbtKey;
		dbtKey.initialize_for_read(key);
		if (offset > 0 || length > 0) {
			dbtBuffer.set_for_partial(offset, length);
		}
		ret = TryMemStd("Get", context, &dbtKey, &dbtBuffer, &size, static_cast<int>(flags),
			&get_core);
	}
	size = SwitchMemStd("Get", context, ret, size);
	if (size < 0) return nullptr;
	return dbtBuffer.CreateStream();
}

array<Byte>^ DatabaseImpl::GetBuffer(DataBuffer key,
	int offset, int length, GetOpFlags flags)
{
	int ret = 0;
	int size = -1;
	DatabaseImpl ^db = this;
	DbtExtended dbtBuffer;
	dbtBuffer.set_flags(DB_DBT_USERCOPY);
	Dbt *pBuffer = &dbtBuffer;
	TransactionContext context(db);
	{
		DbtHolder dbtKey;
		dbtKey.initialize_for_read(key);
		if (offset > 0 || length > 0) {
			dbtBuffer.set_for_partial(offset, length);
		}
		ret = TryMemStd("GetBuffer", context, &dbtKey, pBuffer, &size, static_cast<int>(flags),
			&get_core);
	}
	size = SwitchMemStd("GetBuffer", context, ret, size);
	if (size < 0) return nullptr;
	return dbtBuffer.CreateBuffer();
}

int DatabaseImpl::Put(DataBuffer key, int offset, int count, DataBuffer buffer,
	PutOpFlags flags)
{
	int ret = 0;
	int size = -1;
	DatabaseImpl ^db = this;
	TransactionContext context(db);
	{
		DbtHolder dbtKey;
		DbtHolder dbtBuffer;
		dbtKey.initialize_for_read(key);
		dbtBuffer.initialize_for_read(buffer);
		if (offset >= 0) {
			if (count < 0) {
				count = buffer.ByteLength;
			}
			dbtBuffer.set_for_partial(offset, count);
		}
		ret = TryMemStd("Put", context, &dbtKey, &dbtBuffer, &size, static_cast<int>(flags),
			&put_core);
	}
	SwitchStd("Put", context, ret);
	return size;
}

bool DatabaseImpl::Delete(DataBuffer key, DeleteOpFlags flags)
{
	int ret = 0;
	DatabaseImpl ^db = this;
	TransactionContext context(db);
	{
		DbtHolder dbtKey;
		dbtKey.initialize_for_read(key);
		ret = TryStd("Delete", context, &dbtKey, NULL, static_cast<int>(flags), &del_core);
	}
	bool found;
	switch(ret) {
	case DbRetVal::SUCCESS:
		found = true;
		break;
	case DbRetVal::NOTFOUND:
	case DbRetVal::KEYEMPTY:
		found = false;
		break;
	default:
		throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Delete: Unexpected error with ret value " + ret);
	}
	context.commit();
	return found;
}

DbRetVal DatabaseImpl::Exists(DataBuffer key, ExistsOpFlags flags)
{
	int ret = 0;
	DatabaseImpl ^db = this;
	TransactionContext context(db);
	{
		DbtHolder dbtKey;
		dbtKey.initialize_for_read(key);
		ret = TryStd("Exists", context, &dbtKey, NULL, static_cast<int>(flags), &exists_core);
	}
	switch(ret) {
	case DbRetVal::SUCCESS:
	case DbRetVal::NOTFOUND:
	case DbRetVal::KEYEMPTY:
		break;
	default:
		throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Exists: Unexpected error with ret value " + ret);
	}
	context.commit();
	return static_cast<DbRetVal>(ret);
}

int DatabaseImpl::GetLength(DataBuffer key, GetOpFlags flags)
{
	int ret = 0;
	int size = -1;
	DatabaseImpl ^db = this;
	TransactionContext context(db);
	{
		DbtHolder dbtKey;
		dbtKey.initialize_for_read(key);
		Dbt dbtBuffer;
		dbtBuffer.set_size(-1);
		dbtBuffer.set_flags(DB_DBT_USERMEM);
		ret = TryMemStd("GetLength", context, &dbtKey, &dbtBuffer, &size, static_cast<int>(flags),
			&get_core);
	}
	return SwitchMemStd("GetLength", context, ret, size);
}

String^ DatabaseImpl::Get(String ^key)
{
	CheckForNullOrEmptyKey(key, "Get");
	int ret = 0;
	pin_ptr<const wchar_t> pKeyStr = PtrToStringChars(key);
	Dbt dbtKey(const_cast<wchar_t*>(pKeyStr), key->Length * sizeof(Char));

	Dbt dbtValue;
	try
	{
		/* This is the only Get method not supplied with a value buffer, so let Bdb allocate it, but
		it has to be freed before exiting */
		dbtValue.set_flags(DB_DBT_MALLOC);

		DbTxn *txn = NULL;
		int retry_count = 0;

		// retry_count is a counter used to identify how many times
		// we've retried this operation. To avoid the potential for 
		// endless looping, we won't retry more than MAX_DEADLOCK_RETRIES 
		// times.

		// txn is a transaction handle.
		// key and data are DBT handles. Their usage is not shown here.
		while (retry_count < m_maxDeadlockRetries)
		{	
			try
			{
				txn = BeginTrans();
				ret = m_pDb->get(txn, &dbtKey, &dbtValue, NULL);
				CommitTrans(txn);
			}
			catch (DbDeadlockException &de) 
			{
				try 
				{
					// Abort the transaction and increment the 
					// retry counter
					RollbackTrans(txn);
					retry_count++;
					// If we've retried too many times, log it and exit
					if (retry_count >= m_maxDeadlockRetries)
					{
						m_pEnv->errx("Get exceeded retry limit. Giving up.");
						throw BdbExceptionFactory::Create(ret, &de, gcnew String(de.what()));
					}
				} 
				catch (DbException &ae) 
				{
					Log(key, ae.get_errno(), "txn abort failed in Get.");
					throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()));
				}
			}
			catch (const DbException &ex)
			{
				try 
				{
					// For a generic error, log it and abort.
					Log(key, ex.get_errno(), "Error getting data");
					RollbackTrans(txn);
				} 
				catch (DbException &ae) 
				{
					Log(key, ae.get_errno(), "txn abort failed in Get.");
					//return (EXIT_FAILURE);    
					throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ex.what()) + "\n" + gcnew String(ae.what()));
				}
				throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
			}
			catch (const exception &ex)
			{
				if (txn != NULL)
				{
					try 
					{
						// For a generic error, log it and abort.
						RollbackTrans(txn);
					} 
					catch (DbException &ae) 
					{
						Log(key, ae.get_errno(), "txn abort failed in Get.");
						throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()) + "\n" + gcnew String(ex.what()));
					}
				}
				throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
			}
			switch(ret)
			{
			case DbRetVal::SUCCESS:
				return Marshal::PtrToStringUni(IntPtr(dbtValue.get_data()), dbtValue.get_size() / sizeof(wchar_t));
			case DbRetVal::NOTFOUND:
			case DbRetVal::KEYEMPTY:
				return nullptr;
			case DbRetVal::BUFFER_SMALL:
			default:
				throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Get: Unexpected error with ret value " 
					+ ret);
			}
		}
	}
	finally
	{
		void *data = dbtValue.get_data();
		if (data != NULL)
		{
			free_wrapper(data);
		}
	}
	return nullptr;
}


array<Byte>^ DatabaseImpl::Get(int key, array<Byte> ^buffer)
{
	int ret = 0;
	Dbt dbtKey(&key, sizeof(key));
	
	pin_ptr<Byte> pBuffer = &buffer[0];
	int len = buffer->Length;
	Dbt dbtValue(pBuffer, len);
	
	DbTxn *txn = NULL;
	
	int retry_count = 0;
	dbtValue.set_flags(DB_DBT_USERMEM);
	dbtValue.set_ulen(len);

	if (Get(&dbtKey, &dbtValue) == DbRetVal::SUCCESS && dbtValue.get_size() > 0)
	{
		Array::Resize(buffer, dbtValue.get_size());
		return buffer;
	}
	return nullptr;
}

DatabaseEntry^ DatabaseImpl::Get(DatabaseEntry ^key, DatabaseEntry ^value)
{
	int ret = 0;

	CheckForNullKey(key, "Get");
	array<Byte> ^keyBuffer = key->Buffer;
	CheckForNullOrEmptyKey(keyBuffer, "Get");
	pin_ptr<Byte> pKeyData(&keyBuffer[0]);
	Dbt dbtKey(pKeyData, keyBuffer->Length);

	array<Byte> ^valueBuffer = value->Buffer;
	pin_ptr<Byte> pValueData(&valueBuffer[0]);
	int len = valueBuffer->Length;
	Dbt dbtValue(pValueData, len);
	dbtValue.set_flags(DB_DBT_USERMEM);
	dbtValue.set_ulen(len);

	if (Get(&dbtKey, &dbtValue) == DbRetVal::SUCCESS)
	{
		value->Length = dbtValue.get_size();
	}
	return value;
}

DatabaseEntry^ DatabaseImpl::Get(int key, DatabaseEntry ^value)
{
	int ret = 0;
	Dbt dbtKey(&key, sizeof(key));

	array<Byte> ^valueBuffer = value->Buffer;
	pin_ptr<Byte> pValueData(&valueBuffer[0]);
	int len = valueBuffer->Length;
	Dbt dbtValue(pValueData, len);
	dbtValue.set_flags(DB_DBT_USERMEM);
	dbtValue.set_ulen(len);

	if (Get(&dbtKey, &dbtValue) == DbRetVal::SUCCESS)
	{
		value->Length = dbtValue.get_size();
	}
	return value;
}

DatabaseEntry^ DatabaseImpl::Get(array<Byte> ^key, DatabaseEntry ^value)
{
	CheckForNullOrEmptyKey(key, "Get");
	int ret = 0;
	pin_ptr<Byte> pKey = &key[0];
	Dbt dbtKey(pKey, key->Length);

	array<Byte> ^valueBuffer = value->Buffer;
	pin_ptr<Byte> pValueData(&valueBuffer[0]);
	int len = valueBuffer->Length;
	Dbt dbtValue(pValueData, len);
	dbtValue.set_flags(DB_DBT_USERMEM);
	dbtValue.set_ulen(len);

	if (Get(&dbtKey, &dbtValue) == DbRetVal::SUCCESS)
	{
		value->Length = dbtValue.get_size();
	}
	return value;
}

DbRetVal DatabaseImpl::Delete(Dbt *dbtKey)
{
	int ret = 0;
	DbTxn *txn = NULL;
	int retry_count = 0;

	// retry_count is a counter used to identify how many times
	// we've retried this operation. To avoid the potential for 
	// endless looping, we won't retry more than MAX_DEADLOCK_RETRIES 
	// times.

	while (retry_count < m_maxDeadlockRetries)
	{
		try
		{
			txn = BeginTrans();
			ret = m_pDb->del(txn, dbtKey, 0);
			CommitTrans(txn);
		}
		catch (DbDeadlockException &de) 
		{
			try 
			{
				// Abort the transaction and increment the 
				// retry counter
				RollbackTrans(txn);
				retry_count++;
				// If we've retried too many times, log it and exit
				if (retry_count >= m_maxDeadlockRetries)
				{
					m_pEnv->errx("Get exceeded retry limit. Giving up.");
					throw BdbExceptionFactory::Create(ret, &de, gcnew String(de.what()));
				}
			} 
			catch (DbException &ae) 
			{
				Log(dbtKey, ae.get_errno(), "txn abort failed in Get.");
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()));
			}
		}
		catch (const DbException &ex)
		{
			try 
			{
				// For a generic error, log it and abort.
				Log(dbtKey, ex.get_errno(), "Error getting data");
				RollbackTrans(txn);
			} 
			catch (DbException &ae) 
			{
				Log(dbtKey, ae.get_errno(), "txn abort failed in Get.");
				//return (EXIT_FAILURE);    
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ex.what()) + "\n" + gcnew String(ae.what()));
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		catch (const exception &ex)
		{
			if (txn != NULL)
			{
				try 
				{
					// For a generic error, log it and abort.
					RollbackTrans(txn);
				} 
				catch (DbException &ae) 
				{
					Log(dbtKey, ae.get_errno(), "txn abort failed in Get.");
					throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()) + "\n" + gcnew String(ex.what()));
				}
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		switch(ret)
		{
		case DbRetVal::SUCCESS:
		case DbRetVal::NOTFOUND:
		case DbRetVal::KEYEMPTY:
			return (DbRetVal)ret;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Delete: Unexpected error with ret value " + ret);
		}
	}
	return (DbRetVal)ret;//can't get here but compiler doesn't know that
}

DbRetVal DatabaseImpl::Delete(int key)
{
	Dbt dbtKey(&key, sizeof(key));
	return Delete(&dbtKey);
}

DbRetVal DatabaseImpl::Delete(array<Byte> ^key)
{
	CheckForNullOrEmptyKey(key, "Delete");
	pin_ptr<Byte> pKey = &key[0];
	Dbt dbtKey(pKey, key->Length);

	return Delete(&dbtKey);
}

void DatabaseImpl::Delete(String ^key)
{
	CheckForNullOrEmptyKey(key, "Delete");
	pin_ptr<const wchar_t> pKeyStr = PtrToStringChars(key);
	Dbt dbtKey(const_cast<wchar_t*>(pKeyStr), key->Length * sizeof(Char));

	Delete(&dbtKey);
}

void DatabaseImpl::Delete(DatabaseEntry ^key)
{
	CheckForNullKey(key, "Delete");
	array<Byte> ^keyBuffer = key->Buffer;
	CheckForNullOrEmptyKey(keyBuffer, "Delete");
	pin_ptr<Byte> pKeyData(&keyBuffer[0]);
	Dbt dbtKey(pKeyData, keyBuffer->Length);

	Delete(&dbtKey);
}

CacheSize^ DatabaseImpl::GetCacheSize()
{
	CacheSize ^cacheSize;
	if (m_pDb != NULL)
	{
		int ret = 0;
		try
		{
			u_int32_t gbytes = 0;
			u_int32_t bytes = 0;
			int ncache = 0;
			ret = m_pDb->get_cachesize(&gbytes, &bytes, &ncache);
			cacheSize = gcnew CacheSize(gbytes, bytes, ncache); 
			//m_pDb->get_dbname();
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:GetCacheSize: Unexpected error with ret value " + ret);
		}
	}
	return cacheSize;
}

DatabaseConfig^ DatabaseImpl::GetDatabaseConfig()
{
	return m_dbConfig;
}

String^ DatabaseImpl::GetErrorPrefix()
{
	String ^errPrefix;
	if (m_pDb != NULL)
	{
		try
		{
			const char *errPrefixp = NULL;
			m_pDb->get_errpfx(&errPrefixp);
			errPrefix = gcnew String(errPrefixp);
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(&ex, gcnew String(ex.what()));
		}
		catch (const exception &ex)
		{
			throw BdbExceptionFactory::Create(&ex, gcnew String(ex.what()));
		}
	}
	return errPrefix;
	//return Marshal::PtrToStringAnsi(IntPtr(const_cast<char*>(errPrefixp)));
}

DbFlags DatabaseImpl::GetFlags()
{
	DbFlags dbFlags;
	if (m_pDb != NULL)
	{
		int ret = 0;
		try
		{
			u_int32_t flagsp = 0;
			ret = m_pDb->get_flags(&flagsp);
			dbFlags = static_cast<DbFlags>(flagsp);

			//m_pDb->get_h_nelem();
			//m_pDb->get_open_flags();
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:GetFlags: Unexpected error with ret value " + ret);
		}
	}
	return dbFlags;
}

DbOpenFlags DatabaseImpl::GetOpenFlags()
{
	DbOpenFlags dbOpenFlags;
	if (m_pDb != NULL)
	{
		int ret = 0;
		try
		{
			u_int32_t flags = 0;
			ret = m_pDb->get_open_flags(&flags);
			dbOpenFlags = static_cast<DbOpenFlags>(flags);
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:GetOpenFlags: Unexpected error with ret value " + ret);
		}
	}
	return dbOpenFlags;
}

int DatabaseImpl::GetHashFillFactor()
{
	u_int32_t hFFactor = 0;
	if (m_pDb != NULL)
	{
		int ret = 0;
		try
		{
			ret = m_pDb->get_h_ffactor(&hFFactor);
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:GetHashFillFactor: Unexpected error with ret value " + ret);
		}
	}
	return hFFactor;
}

int DatabaseImpl::GetPageSize()
{
	u_int32_t pagesize = 0;
	if (m_pDb != NULL)
	{
		int ret = 0;
		try
		{
			ret = m_pDb->get_pagesize(&pagesize);
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:GetPageSize: Unexpected error with ret value " + ret);
		}
	}
	return pagesize;
}

int DatabaseImpl::GetRecordLength()
{
	u_int32_t recordLength = 0;
	if (m_pDb != NULL)
	{
		int ret = 0;
		try
		{
			switch (GetDatabaseType())
			{
			case DatabaseType::Queue:
				ret = m_pDb->get_re_len(&recordLength);
				break;
			}
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:GetRecordLength: Unexpected error with ret value " + ret);
		}
	}
	return recordLength;
}

DatabaseType DatabaseImpl::GetDatabaseType()
{
	DatabaseType dbType;
	if (m_pDb != NULL)
	{
		int ret = 0;
		try
		{
			DBTYPE dbtype = (DBTYPE)0;
			ret = m_pDb->get_type(&dbtype);
			dbType = static_cast<DatabaseType>(dbtype);
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:GetType: Unexpected error with ret value " + ret);
		}
	}
	return dbType;
}

void DatabaseImpl::Log(int errNumber, const char *errMessage)
{
	if (m_pEnv != NULL)
	{
		m_pEnv->err(errNumber, errMessage);
	}
}

void DatabaseImpl::Log(int objectId, int errNumber, const char *errMessage)
{
	if (m_pEnv != NULL)
	{
		m_pEnv->err(errNumber, "ID %d: %s", objectId, errMessage);
	}
}

void DatabaseImpl::Log(System::String^ key, int errNumber, const char *errMessage)
{
	if (m_pEnv != NULL)
	{
		ConvStr key2(key);
		m_pEnv->err(errNumber, "Key '%s': %s", key2.Str(), errMessage);
	}
}

void DatabaseImpl::Log(Dbt * dbtKey, int errNumber, const char *errMessage)
{
	if (m_pEnv != NULL)
	{
		const int maxLen = 32;
		int len = dbtKey->get_size();
		int fmtLen = len > maxLen ? maxLen : len;
		char buf[3 * maxLen + 1];
		void * data = dbtKey->get_data();
		char * bytePtr = (char *) data;
		char * writePtr = buf;
		for(int n = 0; n < fmtLen; ++n)
		{
			int ret = sprintf(writePtr, "%02.2hx ", (unsigned char) *bytePtr);
			if (ret > 0)
			{
				writePtr += ret;
			}
			++bytePtr;
		}
		switch(len)
		{
		case sizeof(__int32):
			__int32 id;
			memcpy(&id, data, sizeof(__int32));
			m_pEnv->err(errNumber, "DbtKey (len=%d) %s= %d: %s", len, buf, id, errMessage);
			break;
		case sizeof(__int64):
			__int64 longid;
			memcpy(&longid, data, sizeof(__int64));
			m_pEnv->err(errNumber, "DbtKey (len=%d) %s= %ld: %s", len, buf, longid, errMessage);
			break;
		case sizeof(__int16):
			__int16 shortid;
			memcpy(&shortid, data, sizeof(__int16));
			m_pEnv->err(errNumber, "DbtKey (len=%d) %s= %hd: %s", len, buf, shortid, errMessage);
			break;
		case 0:
			m_pEnv->err(errNumber, "DbtKey (len=0): %s", errMessage);
			break;
		default:
			m_pEnv->err(errNumber, "DbtKey (len=%d) %s: %s", len, buf, errMessage);
			break;
		}
	}
}

void DatabaseImpl::PrintStats (DbStatFlags statFlags)
{
	if (m_pDb != NULL)
	{
		int ret = 0;
		try
		{
			ret = m_pDb->stat_print((u_int32_t)statFlags);
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:PrintStats: Unexpected error with ret value " + ret);
		}
	}
}

int DatabaseImpl::GetKeyCount(DbStatFlags statFlag)
{
	if (m_pDb != NULL)
	{
		int ret = 0;
		void *sp = NULL;
		int keyCount = 0;
		try
		{
			ret = m_pDb->stat(NULL, &sp, (u_int32_t)statFlag);
			DatabaseType dbType = GetDatabaseType();
			switch (dbType)
			{
			case DatabaseType::Hash:
				keyCount = (int)((DB_HASH_STAT*)sp)->hash_ndata;
				break;
			case DatabaseType::BTree:
			case DatabaseType::Recno:
				keyCount = (int)((DB_BTREE_STAT*)sp)->bt_nkeys;
				break;
			case DatabaseType::Queue:
				keyCount = (int)((DB_QUEUE_STAT*)sp)->qs_nkeys;
				break;
			default:
				throw BdbExceptionFactory::Create(ret, String::Format("Unhandled database type {0}", dbType));
			}
		}
		catch (const DbException &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		catch (const exception &ex)
		{
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		finally
		{
			if (sp != NULL)
			{
				free_wrapper(sp);
			}
		}
		switch(ret)
		{
		case DbRetVal::SUCCESS:
			return keyCount;
		default:
			throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:PrintStats: Unexpected error with ret value " + ret);
		}
	}
	else
	{
		return 0;
	}
}

int DatabaseImpl::Compact(int fillPercentage, int maxPagesFreed, int implicitTxnTimeoutMsecs)
{
	int ret = 0;
	DB_COMPACT cmpt;
	cmpt.compact_fillpercent = fillPercentage > 0 ? fillPercentage : 0;
	cmpt.compact_pages = maxPagesFreed > 0 ? maxPagesFreed : 0;
	cmpt.compact_timeout = implicitTxnTimeoutMsecs > 0 ? implicitTxnTimeoutMsecs : 0;
	try
	{
		ret = m_pDb->compact(NULL, NULL, NULL, &cmpt, DB_FREE_SPACE, NULL);
	}
	catch (const DbException &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	catch (const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
	}
	switch(ret)
	{
	case DbRetVal::SUCCESS: case DbRetVal::PAGE_NOTFOUND:
		return cmpt.compact_pages_truncated;
	default:
		throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Compact: Unexpected error with ret value " + ret);
	}
}

int DatabaseImpl::Truncate()
{
	u_int32_t count = 0;
	DbTxn *txn = NULL;
	int retry_count = 0;

	int ret = 0;

	while (retry_count < m_maxDeadlockRetries)
	{
		try
		{
			txn = BeginTrans();
			ret = m_pDb->truncate(txn, &count, 0);
			CommitTrans(txn);
			break;
		}
		catch (DbDeadlockException &de) 
		{
			try 
			{
				// Abort the transaction and increment the 
				// retry counter
				RollbackTrans(txn);
				retry_count++;
				// If we've retried too many times, log it and exit
				if (retry_count >= m_maxDeadlockRetries)
				{
					m_pEnv->errx("Get exceeded retry limit. Giving up.");
					throw BdbExceptionFactory::Create(ret, &de, gcnew String(de.what()));
				}
				Log(de.get_errno(), "Retrying");
			} 
			catch (DbException &ae) 
			{
				Log(ae.get_errno(), "txn abort failed in Truncate.");
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()));
			}
		}
		catch (const DbException &ex)
		{
			try 
			{
				// For a generic error, log it and abort.
				Log(ex.get_errno(), "Error truncating.");
				RollbackTrans(txn);
			} 
			catch (DbException &ae) 
			{
				Log(ae.get_errno(), "txn abort failed in Truncate.");
				//return (EXIT_FAILURE);    
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ex.what()) + "\n" + gcnew String(ae.what()));
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		catch (const exception &ex)
		{
			if (txn != NULL)
			{
				try 
				{
					// For a generic error, log it and abort.
					RollbackTrans(txn);
				} 
				catch (DbException &ae) 
				{
					Log(ae.get_errno(), "txn abort failed in Truncate.");
					throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()) + "\n" + gcnew String(ex.what()));
				}
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
	}
	switch(ret)
	{
	case DbRetVal::SUCCESS:
		break;
	default:
		throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Truncate: Unexpected error with ret value " + ret);
	}
	return count;
}

void DatabaseImpl::Sync()
{
	int ret = 0;
	try
	{
		ret = m_pDb->sync(0);
	}
	catch (const DbException &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
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
		throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Sync: Unexpected error with ret value " + ret);
	}
}

Dbc *DatabaseImpl::CreateCursorHandle()
{
	int ret = 0;
	Dbc *cursor = NULL;
	DbTxn *txn = NULL;
	int retry_count = 0;

	while (retry_count < m_maxDeadlockRetries)
	{
		try
		{
			ret = m_pDb->cursor(txn, &cursor, 0);
		}
		catch (DbDeadlockException &de) 
		{
			try 
			{
				retry_count++;
				// If we've retried too many times, log it and exit
				if (retry_count >= m_maxDeadlockRetries)
				{
					m_pEnv->errx("Get cursor exceeded retry limit. Giving up.");
					throw BdbExceptionFactory::Create(ret, &de, gcnew String(de.what()));
				}
				Log(de.get_errno(), "Retrying");
			} 
			catch (DbException &ae) 
			{
				Log(ae.get_errno(), "txn abort failed in Get cursor.");
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()));
			}
		}
		catch (const DbException &ex)
		{
			String ^errMsg = gcnew String(ex.what());
			try 
			{
				// For a generic error, log it and abort.
				Log(ex.get_errno(), "Error getting data");
				if (txn != NULL)
				{
					txn->abort();
				}
			} 
			catch (DbException &ae) 
			{
				Log(ae.get_errno(), "txn abort failed in Get.");
				//return (EXIT_FAILURE);    
				throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ex.what()) + "\n" + gcnew String(ae.what()));
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}
		catch (const exception &ex)
		{
			if (txn != NULL)
			{
				try 
				{
					// For a generic error, log it and abort.
					txn->abort();
				} 
				catch (DbException &ae) 
				{
					Log(ae.get_errno(), "txn abort failed in Get.");
					throw BdbExceptionFactory::Create(ret, &ae, gcnew String(ae.what()) + "\n" + gcnew String(ex.what()));
				}
			}
			throw BdbExceptionFactory::Create(ret, &ex, gcnew String(ex.what()));
		}

		switch(ret)
		{
			case DbRetVal::SUCCESS:
				break;
			default:
				throw BdbExceptionFactory::Create(ret, "BerkeleyDbWrapper:Database:Truncate: Unexpected error with ret value " + ret);
		}

		return cursor;
	}
	return cursor; //can't get here but the compiler can't figure that out.
}

DbRetVal DatabaseImpl::DoVerify(String ^fileName)
{
	int ret = 0;
	Db *db = NULL;
	try
	{
		ConvStr fn(fileName);
		// verify has to use unopened db handle that can't be used thereafter
		db = new Db(NULL, 0);
		ret = db->verify(fn.Str(), NULL, NULL, 0);
	}
	catch(const DbException &dex)
	{
		int localErrno = dex.get_errno();
		switch(localErrno)
		{
			case DbRetVal::VERIFY_BAD:
			case DbRetVal::VERIFY_FATAL:
				ret = localErrno;
				break;
			default:
				throw BdbExceptionFactory::Create(ret, &dex, String::Format("While verifying {0}: {1}", fileName,
					gcnew String(dex.what())));
		}
	}
	catch(const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, String::Format("While verifying {0}: {1}", fileName,
			gcnew String(ex.what())));
	}
	finally
	{
		if (db != NULL)
		{
			try
			{
				delete db;
			}
			finally
			{
				db = NULL;
			}
		}

	}
	switch (ret)
	{
		case DbRetVal::SUCCESS:
		case DbRetVal::VERIFY_BAD:
		case DbRetVal::VERIFY_FATAL:
			return static_cast<DbRetVal>(ret);
		default:
			throw BdbExceptionFactory::Create(ret, "Unrecognized return code from verify");
	}
}

void DatabaseImpl::DoRemove(BerkeleyDbWrapper::Environment^ env, String ^fileName)
{
	EnvironmentImpl ^envImpl = safe_cast<EnvironmentImpl ^>(env);
	int ret = 0;
	Db *db = NULL;
	DbEnv *m_pEnv = NULL;
	if (envImpl != nullptr)
	{
		m_pEnv = envImpl->Handle;
	}
	try
	{
		ConvStr fn(fileName);
		// use dummy handle
		db = new Db(m_pEnv, 0);
		ret = db->remove(fn.Str(), NULL, 0);
	}
	catch(const DbException &dex)
	{
		throw BdbExceptionFactory::Create(ret, &dex, String::Format("While removing {0}: {1}", fileName,
			gcnew String(dex.what())));
	}
	catch(const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, String::Format("While removing {0}: {1}", fileName,
			gcnew String(ex.what())));
	}
	finally
	{
		if (db != NULL)
		{
			try
			{
				delete db;
			}
			finally
			{
				db = NULL;
			}
		}
	}
	switch (ret)
	{
		case DbRetVal::SUCCESS:
			return;
		default:
			throw BdbExceptionFactory::Create(ret, "Unrecognized return code from remove");
	}
}

void DatabaseImpl::BackupFromDisk(String^ backupFile, array<Byte>^ copyBuffer)
{
	int ret = 0;
	int pageSize = GetPageSize();
	if (copyBuffer == nullptr)
	{
		throw BdbExceptionFactory::Create(0, "");
	}
	int bufferSize = copyBuffer->Length;
	if (bufferSize < pageSize)
	{
		throw BdbExceptionFactory::Create(0, "Buffer size " + bufferSize + " less than page size " + pageSize);
	}
	int pages = bufferSize / pageSize;
	int copySize = pages * pageSize;
	FileStream^ backupStream = nullptr;
	FileStream^ sourceStream = nullptr;
	const char *dataFile;
	try
	{
		ret = m_pDb->get_dbname(&dataFile, NULL);
		if (ret != 0)
		{
			throw BdbExceptionFactory::Create(ret,
			"BackupFromDisk::Backup: Unexpected error while getting data file path " + ret);
		}
		sourceStream = gcnew FileStream(gcnew String(dataFile), FileMode::Open, FileAccess::Read, FileShare::ReadWrite,
			pageSize, FileOptions::SequentialScan);
		backupStream = gcnew FileStream(backupFile, FileMode::Create, FileAccess::Write, FileShare::None, copySize,
			FileOptions::WriteThrough);
		bool eof = false;
		do
		{
			int totalBytesCopied = 0;
			do
			{
				int bytesCopied = sourceStream->Read(copyBuffer, totalBytesCopied, pageSize);
				if (bytesCopied < pageSize)
				{
					eof = sourceStream->Position >= sourceStream->Length;
					if (!eof)
					{
						throw BdbExceptionFactory::Create(ret,
						"BackupFromDisk::Backup: Tried to read " + pageSize +
						" bytes from data file, got " + bytesCopied + " bytes instead");
					}
					break;
				}
				totalBytesCopied += pageSize;
			} while(totalBytesCopied < copySize);
			if (totalBytesCopied > 0)
			{
				backupStream->Write(copyBuffer, 0, totalBytesCopied);
			}
		} while(!eof);
	}
	catch(const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, String::Format("While backing up to {0}: {1}", backupFile,
			gcnew String(ex.what())));
	}
	finally
	{
		if (sourceStream != nullptr)
		{
			sourceStream->Close();
			sourceStream = nullptr;
		}
		if (backupStream != nullptr)
		{
			backupStream->Close();
			backupStream = nullptr;
		}
	}
}

void DatabaseImpl::BackupFromMpf(String^ backupFile, array<Byte>^ copyBuffer)
{
	int ret = 0;
	int pageSize = GetPageSize();
	pin_ptr<void> buf = nullptr;
	char *cbuf = NULL;
	int pages = 0;
	int copiedPages = 0;
	if (copyBuffer != nullptr)
	{
		int bufferSize = copyBuffer->Length;
		pages = bufferSize / pageSize;
		if (pages >= 2)
		{
			buf = &copyBuffer[0];
			cbuf = reinterpret_cast<char *>(buf);
		}
	}
	DbMpoolFile *mpf;
	void *bufRead;
	char *bufWrite;
	ofstream backupStream;
	try
	{
		mpf = m_pDb->get_mpf();
		do
		{
			pin_ptr<wchar_t> backupFilePath = &(backupFile->ToCharArray())[0];
			backupStream.open(backupFilePath, ofstream::binary | ios_base::out | ios_base::trunc);
		} while(false);
		bufWrite = cbuf;
		db_pgno_t pageNumber = 0;
		do
		{
			try
			{
				bufRead = NULL;
				ret = mpf->get(&pageNumber, NULL, 0, &bufRead);
				switch(ret)
				{
				case DbRetVal::SUCCESS:
					if (buf != nullptr)
					{
						memcpy(bufWrite, bufRead, pageSize);
						++copiedPages;
						bufWrite += pageSize;
					}
					else
					{
						backupStream.write(reinterpret_cast<const char *>(bufRead), pageSize);
					}
					break;
				case DbRetVal::PAGE_NOTFOUND:
					break;
				default:
					throw BdbExceptionFactory::Create(ret,
						"BerkeleyDbWrapper:Database:BackupFromMpf: Unexpected error in getting page " + pageNumber +
						" with ret value " + ret);
				}
			}
			finally
			{
				if (bufRead != NULL)
					mpf->put(bufRead, DB_PRIORITY_DEFAULT, 0);
			}
			if (buf != nullptr && (copiedPages == pages || ret == static_cast<int>(DbRetVal::PAGE_NOTFOUND)))
			{
				backupStream.write(cbuf, copiedPages * pageSize);
				copiedPages = 0;
				bufWrite = cbuf;
			}
			++pageNumber;
		} while(ret == static_cast<int>(DbRetVal::SUCCESS));
	}
	catch(const exception &ex)
	{
		throw BdbExceptionFactory::Create(ret, &ex, String::Format("While backing up to {0}: {1}", backupFile,
			gcnew String(ex.what())));
	}
	finally
	{
		if (backupStream.is_open())
			backupStream.close();
	}
}

DbTxn * DatabaseImpl::BeginTrans()
{
	switch(m_pTrMode) {
		case DatabaseTransactionMode::None:
			return NULL;
			break;
		case DatabaseTransactionMode::PerCall:
			if (m_isTxn) {
				DbTxn *txn;
				m_pEnv->txn_begin(NULL, &txn, 0);
				return txn;
			} else {
				return NULL;
			}
			break;
		default:
			throw new exception("Unrecognized transaction mode");
	}
}

void DatabaseImpl::CommitTrans(DbTxn *txn)
{
	if (txn == NULL) return;
	switch(m_pTrMode) {
		case DatabaseTransactionMode::None:
			break;
		case DatabaseTransactionMode::PerCall:
			txn->commit(0);
			break;
		default:
			throw new exception("Unrecognized transaction mode");
	}
}

void DatabaseImpl::RollbackTrans(DbTxn *txn)
{
	if (txn == NULL) return;
	switch(m_pTrMode) {
		case DatabaseTransactionMode::None:
			break;
		case DatabaseTransactionMode::PerCall:
			txn->abort();
			break;
		default:
			throw new exception("Unrecognized transaction mode");
	}
}

Cursor ^DatabaseImpl::GetCursor()
{
	return gcnew CursorImpl(this);
}