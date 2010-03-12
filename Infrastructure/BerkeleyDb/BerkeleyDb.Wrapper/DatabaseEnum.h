#pragma once
#include "Alloc.h"
#include "Stdafx.h"
#include "Environment.h"
#include "DatabaseEntry.h"
#include "DatabaseRecord.h"
#include "CacheSize.h"
#include "Database.h"

using namespace System;
using namespace System::Collections;
using namespace System::Security;
using namespace MySpace::BerkeleyDb::Configuration;


namespace BerkeleyDbWrapper
{
	[SuppressUnmanagedCodeSecurity()]
	public ref class DatabaseRecordEnum : Generic::IEnumerator<BerkeleyDbWrapper::DatabaseRecord^>, System::Collections::IEnumerator
	{

	public:
		DatabaseRecordEnum(Database ^db);
		DatabaseRecordEnum(Database ^db, int nKeyCapacity, int nValueCapacity);

		~DatabaseRecordEnum();

		!DatabaseRecordEnum();

		property BerkeleyDbWrapper::DatabaseRecord^ Current 
		{ 
			virtual BerkeleyDbWrapper::DatabaseRecord^ get() = Generic::IEnumerator<BerkeleyDbWrapper::DatabaseRecord^>::Current::get 
			{
				return _current;
			}
		}
	
		property Object^ RawCurrent
		{
			virtual Object^ get(void) = System::Collections::IEnumerator::Current::get
			{
				return Current;
			}
		}

		virtual bool MoveNext() = System::Collections::IEnumerator::MoveNext
		{
			if (_database->Disposed)
			{
				ClearAll();
				return false;
			}
			if (!_database->MoveNext(_cursorp))
			{
				_current = nullptr;
				return false;
			}
			BerkeleyDbWrapper::DatabaseRecord^ data = gcnew BerkeleyDbWrapper::DatabaseRecord;
			data->Key = gcnew BerkeleyDbWrapper::DatabaseEntry(_nKeyCapacity);
			data->Value = gcnew BerkeleyDbWrapper::DatabaseEntry(_nValueCapacity);
			_database->GetCurrent(_cursorp, data );
			_current = data;
			return true;
		}

		virtual void Reset() = System::Collections::IEnumerator::Reset
		{
			if (_database->Disposed) return;
			_current = nullptr;
			_database->Reset(_cursorp);
		}

	private:
		DatabaseRecordEnum(); // made private so that the client does not call

		void ClearAll()
		{
			try
			{
				if(_cursorp != NULL )
				{
					int err = 0;
					try
					{
						if (_database != nullptr && !_database->Disposed)
						{
							err = _cursorp->close();
						}
					}
					catch (const DbException &dex)
					{
						if (_database != nullptr)
						{
							_database->Log(dex.get_errno(), dex.what());
						}
					}
					catch (const exception &ex)
					{
						if (_database != nullptr)
						{
							_database->Log(err, ex.what());
						}
					}
					finally
					{
						_cursorp = NULL;
					}
				}
			}
			finally
			{
				_database = nullptr;
				_current = nullptr;
			}
		}

	private:
		Database ^_database;
		Dbc *_cursorp;
		int _nKeyCapacity;
		int _nValueCapacity;
		BerkeleyDbWrapper::DatabaseRecord^ _current;
	};
}