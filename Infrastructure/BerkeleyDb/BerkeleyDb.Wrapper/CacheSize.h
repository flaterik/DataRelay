#pragma once
#include "Stdafx.h"

namespace BerkeleyDbWrapper
{
	public ref class CacheSize
	{
	public:
		CacheSize(UInt32 gbytes, UInt32 bytes, int ncache);

		~CacheSize();

		UInt32 GetGigaBytes();
		UInt32 GetBytes();
		int GetNumCaches();
		
	private:
		//u_int32_t *gbytesp;
		//u_int32_t *bytesp;
		//int *ncachep;
		int m_gbytes;
		int m_bytes;
		int m_ncache;

	};
}