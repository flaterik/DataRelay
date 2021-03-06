#include "stdafx.h"
#include "CursorImpl.h"
#include "BdbExceptionFactory.h"

using namespace std;
using namespace BerkeleyDbWrapper;

CursorImpl::CursorImpl(DatabaseImpl^ db) : _db(db)
{
	_cursorp = _db->CreateCursorHandle();
}

CursorImpl::~CursorImpl()
{
	this->!CursorImpl();
	_db = nullptr;
}

CursorImpl::!CursorImpl()
{
	if (_cursorp != nullptr)
	{
		int err = 0;
		try
		{
			if (_db != nullptr && !_db->Disposed)
			{
				err = _cursorp->close();
			}
		}
		catch (const DbException &dex)
		{
			if (_db != nullptr)
			{
				_db->Log(dex.get_errno(), dex.what());
			}
		}
		catch (const exception &ex)
		{
			if (_db != nullptr)
			{
				_db->Log(err, ex.what());
			}
		}
		finally
		{
			_cursorp = nullptr;
		}
	}
}

int __cdecl get_core(Dbc *dbc, Dbt *key, Dbt *data, int options)
{
	return dbc->get(key, data, options);
}

int __cdecl put_core(Dbc *dbc, Dbt *key, Dbt *data, int options)
{
	return dbc->put(key, data, options);
}

int __cdecl del_core(Dbc *dbc, Dbt *key, Dbt *data, int options)
{
	return dbc->del(options);
}

int CursorImpl::DeadlockLoop(String ^methodName, Dbt *key,
	Dbt *data, int options, BdbCall bdbCall)
{
	int ret = 0;
	bool deadlock_occurred;
	int retry_count = 0;
	try
	{
		do 
		{ 
			deadlock_occurred = false; 
			try 
			{
				ret =  bdbCall(_cursorp, key, data, options);
				deadlock_occurred = (ret == intDeadlockValue);
			} 
			catch(DbDeadlockException) 
			{ 
				deadlock_occurred = true; 
			} 
			if (deadlock_occurred) 
			{
				_db->Log(intDeadlockValue, "Deadlock"); 
				++retry_count; 
				if (retry_count >= _db->MaxDeadlockRetries) break; 
			} 
		} while(deadlock_occurred);
		if (deadlock_occurred) 
		{
			ConvStr msg("Get exceeded retry limit. Giving up."); 
			_db->InternalEnvironment->Handle->errx(msg.Str()); 
			throw BdbExceptionFactory::Create(intDeadlockValue, gcnew String(db_strerror(intDeadlockValue))); 
		}
	}
	catch(DbMemoryException)
	{
		ret = intMemorySmallValue;
	}
	catch (const exception &ex) 
	{ 
		throw BdbExceptionFactory::Create(&ex, "BerkeleyDbWrapper:Cursor:" + methodName); 
	}
	return ret;
}

Lengths CursorImpl::Get(DataBuffer key,
	DataBuffer value, int offset, CursorPosition position,
	GetOpFlags flags)
{
	DbtHolder dbtKey;
	DbtHolder dbtBuffer;
	switch(position) {
		case CursorPosition::Set:
			dbtKey.initialize_for_read(key);
			break;
		default:
			dbtKey.initialize_for_write(key);
			break;
	}
	dbtBuffer.initialize_for_write(value);
	if (offset >= 0)
	{
		dbtBuffer.set_for_partial(offset, dbtBuffer.get_size());
	}
	u_int32_t allFlags = static_cast<u_int32_t>(position) |
		static_cast<u_int32_t>(flags);
	int ret = DeadlockLoop("Get", &dbtKey, &dbtBuffer, allFlags, get_core);
	switch(ret) {
	case DbRetVal::NOTFOUND:
		return Lengths(Lengths::NotFound, Lengths::NotFound);
	case DbRetVal::KEYEMPTY:
		return Lengths(Lengths::Deleted, Lengths::Deleted);
	case DbRetVal::SUCCESS:
	case DbRetVal::BUFFER_SMALL:
		break;
	default:
		throw BdbExceptionFactory::Create(ret, String::Format(
			L"BerkeleyDbWrapper:Database:Get: Unexpected error with ret value {0}", ret));
	}
	return Lengths(position == CursorPosition::Set ? 0 : dbtKey.get_size(),
		dbtBuffer.get_size());
}


Lengths CursorImpl::Get(DataBuffer key,
	int keyOffset, DataBuffer value, int valueOffset, CursorPosition position,
	GetOpFlags flags)
{
	DbtHolder dbtKey;
	DbtHolder dbtBuffer;
	switch(position) {
		case CursorPosition::Set:
			dbtKey.initialize_for_read(key);
			break;
		default:
			dbtKey.initialize_for_write(key);
			if (keyOffset >= 0)
			{
				dbtKey.set_for_partial(keyOffset, dbtKey.get_size());
			}
			break;
	}
	dbtBuffer.initialize_for_write(value);
	if (valueOffset >= 0)
	{
		dbtBuffer.set_for_partial(valueOffset, dbtBuffer.get_size());
	}
	u_int32_t allFlags = static_cast<u_int32_t>(position) |
		static_cast<u_int32_t>(flags);
	int ret = DeadlockLoop("Get", &dbtKey, &dbtBuffer, allFlags, get_core);
	switch(ret) {
	case DbRetVal::NOTFOUND:
		return Lengths(Lengths::NotFound, Lengths::NotFound);
	case DbRetVal::KEYEMPTY:
		return Lengths(Lengths::Deleted, Lengths::Deleted);
	case DbRetVal::SUCCESS:
	case DbRetVal::BUFFER_SMALL:
		break;
	default:
		throw BdbExceptionFactory::Create(ret, String::Format(
			L"BerkeleyDbWrapper:Database:Get: Unexpected error with ret value {0}", ret));
	}
	return Lengths(position == CursorPosition::Set ? 0 : dbtKey.get_size(),
		dbtBuffer.get_size());
}

Streams CursorImpl::Get(DataBuffer key,
	int offset, int length, CursorPosition position,
	GetOpFlags flags)
{
	DbtHolder dbtKey;
	DbtExtended dbtBuffer;
	switch(position) {
		case CursorPosition::Set:
			dbtKey.initialize_for_read(key);
			break;
		case CursorPosition::SetRange:
			dbtKey.initialize_for_read(key);
			dbtKey.set_flags(DB_DBT_MALLOC);
			break;
		default:
			dbtKey.set_flags(DB_DBT_MALLOC);
			break;
	}
	dbtBuffer.set_flags(DB_DBT_MALLOC);
	if (offset > 0 || length >= 0) {
		dbtBuffer.set_for_partial(offset, length);
	}
	u_int32_t allFlags = static_cast<u_int32_t>(position) |
		static_cast<u_int32_t>(flags);
	int ret = DeadlockLoop("Get", &dbtKey, &dbtBuffer, allFlags, get_core);
	switch(ret) {
	case DbRetVal::NOTFOUND:
		return Streams(nullptr, nullptr, Lengths::NotFound);
	case DbRetVal::KEYEMPTY:
		return Streams(nullptr, nullptr, Lengths::Deleted);
	case DbRetVal::SUCCESS:
		break;
	default:
		throw BdbExceptionFactory::Create(ret, String::Format(
			L"BerkeleyDbWrapper:Database:Get: Unexpected error with ret value {0}", ret));
	}
	return Streams(position == CursorPosition::Set ? nullptr : dbtKey.CreateStream(),
		dbtBuffer.CreateStream(), 0);
}

Buffers CursorImpl::GetBuffers(DataBuffer key,
	int offset, int length, CursorPosition position,
	GetOpFlags flags)
{
	DbtHolder dbtKey;
	DbtExtended dbtBuffer;
	switch(position) {
		case CursorPosition::Set:
			dbtKey.initialize_for_read(key);
			break;
		case CursorPosition::SetRange:
			// cant handle this, since DB_DBT_USERCOPY excludes USERMEM, and without it
			// its calling free(dbtKey.data). This is the only position that both reads
			// and writes the key
			throw gcnew NotSupportedException();
			break;
		default:
			dbtKey.set_flags(DB_DBT_USERCOPY);
			break;
	}
	dbtBuffer.set_flags(DB_DBT_USERCOPY);
	if (offset > 0 || length >= 0) {
		dbtBuffer.set_for_partial(offset, length);
	}
	u_int32_t allFlags = static_cast<u_int32_t>(position) |
		static_cast<u_int32_t>(flags);
	int ret = DeadlockLoop("Get", &dbtKey, &dbtBuffer, allFlags, get_core);
	switch(ret) {
	case DbRetVal::NOTFOUND:
		return Buffers(nullptr, nullptr, Lengths::NotFound);
	case DbRetVal::KEYEMPTY:
		return Buffers(nullptr, nullptr, Lengths::Deleted);
	case DbRetVal::SUCCESS:
		break;
	default:
		throw BdbExceptionFactory::Create(ret, String::Format(
			L"BerkeleyDbWrapper:Database:Get: Unexpected error with ret value {0}", ret));
	}
	return Buffers(position == CursorPosition::Set ? nullptr : dbtKey.CreateBuffer(),
		dbtBuffer.CreateBuffer(), 0);
}

Lengths CursorImpl::Put(DataBuffer key,
	DataBuffer value, int offset, int length, CursorPosition position,
	PutOpFlags flags)
{
	DbtHolder dbtKey;
	DbtHolder dbtBuffer;
	dbtKey.initialize_for_read(key);
	dbtBuffer.initialize_for_read(value);
	if (offset > 0 || length >= 0) {
		dbtBuffer.set_for_partial(offset, length);
	}
	u_int32_t allFlags = static_cast<u_int32_t>(position) |
		static_cast<u_int32_t>(flags);
	int ret = DeadlockLoop("Put", &dbtKey, &dbtBuffer, allFlags, put_core);
	switch(ret) {
	case DbRetVal::NOTFOUND:
		return Lengths(Lengths::NotFound, Lengths::NotFound);
	case DbRetVal::KEYEXIST:
		return Lengths(Lengths::KeyExists, Lengths::KeyExists);
	case DbRetVal::SUCCESS:
		break;
	default:
		throw BdbExceptionFactory::Create(ret, String::Format(
			L"BerkeleyDbWrapper:Database:Put: Unexpected error with ret value {0}", ret));
	}
	return Lengths(dbtKey.get_size(), dbtBuffer.get_size());
}

bool CursorImpl::Delete(DeleteOpFlags flags)
{
	u_int32_t allFlags = static_cast<u_int32_t>(flags);
	int ret = DeadlockLoop("Delete", nullptr, nullptr, allFlags, del_core);
	switch(ret) {
	case DbRetVal::KEYEMPTY:
		return false;
	case DbRetVal::SUCCESS:
		return true;
	default:
		throw BdbExceptionFactory::Create(ret, String::Format(
			L"BerkeleyDbWrapper:Database:Delete: Unexpected error with ret value {0}", ret));
	}
}