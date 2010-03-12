#include "stdafx.h"
#include "Database.h"
#include "DatabaseEnum.h"
#include "BdbException.h"

using namespace std;
using namespace System::Runtime::InteropServices;

BerkeleyDbWrapper::DatabaseRecordEnum::DatabaseRecordEnum(Database ^db):_cursorp(nullptr),_database(nullptr), _nKeyCapacity(16), _nValueCapacity(1024)
{
	if( db != nullptr )
	{
		this->_database = db;
		this->_cursorp = _database->GetCursor();
	}
}

BerkeleyDbWrapper::DatabaseRecordEnum::DatabaseRecordEnum():_cursorp(nullptr),_database(nullptr), _nKeyCapacity(16), _nValueCapacity(1024){}

BerkeleyDbWrapper::DatabaseRecordEnum::!DatabaseRecordEnum()
{
	ClearAll();
}

BerkeleyDbWrapper::DatabaseRecordEnum::~DatabaseRecordEnum()
{
	this->!DatabaseRecordEnum();
}

BerkeleyDbWrapper::DatabaseRecordEnum::DatabaseRecordEnum(Database ^db, int nKeyCapacity, int nValueCapacity)
{
	if( db != nullptr )
	{
		this->_database = db;
		this->_cursorp = _database->GetCursor();
	}
	this->_nKeyCapacity = nKeyCapacity;
	this->_nValueCapacity = nValueCapacity;
}
