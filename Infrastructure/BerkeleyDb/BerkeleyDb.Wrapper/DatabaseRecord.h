#pragma once
#include "Stdafx.h"

#include "DatabaseEntry.h"

namespace BerkeleyDbWrapper
{
	public ref class DatabaseRecord
	{
	public: 
		DatabaseRecord();
		DatabaseRecord(int nKeyCapacity, int nValueCapacity);

		~DatabaseRecord();

		property DatabaseEntry^ Key
		{
			DatabaseEntry^ get()
			{
				return this->key;
			}
			
			void set(DatabaseEntry^ dbKey)
			{
				this->key = dbKey;
			}
		}

		property DatabaseEntry^ Value
		{
			DatabaseEntry^ get()
			{
				return this->value;
			}
			void set(DatabaseEntry^ dbValue)
			{
				this->value = dbValue;
			}
		}
	private:		
		DatabaseEntry ^key;
		DatabaseEntry ^value;
	};
}