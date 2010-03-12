#include "stdafx.h"
#include "DatabaseRecord.h"

using namespace std;


BerkeleyDbWrapper::DatabaseRecord::DatabaseRecord() : key( nullptr ), value( nullptr )
{
}

BerkeleyDbWrapper::DatabaseRecord::DatabaseRecord(int nKeyCapacity, int nValueCapacity)
{
	this->key = gcnew BerkeleyDbWrapper::DatabaseEntry(nKeyCapacity);
	this->value = gcnew BerkeleyDbWrapper::DatabaseEntry(nValueCapacity);
}


BerkeleyDbWrapper::DatabaseRecord::~DatabaseRecord()
{
	if( key != nullptr )
	{
		// do something
	}

	if( value != nullptr )
	{
		// do something
	}
}
