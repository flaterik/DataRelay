#include "stdafx.h"
#include "CacheSize.h"

BerkeleyDbWrapper::CacheSize::CacheSize(UInt32 gbytes, UInt32 bytes, int ncache)
{
	m_gbytes = gbytes;
	m_bytes = bytes;
	m_ncache = ncache;
}

BerkeleyDbWrapper::CacheSize::~CacheSize()
{
	m_gbytes = NULL;
	m_bytes = NULL;
	m_ncache = NULL;
}

UInt32 BerkeleyDbWrapper::CacheSize::GetGigaBytes()
{
	return m_gbytes;
}
UInt32 BerkeleyDbWrapper::CacheSize::GetBytes()
{
	return m_bytes;
}
int BerkeleyDbWrapper::CacheSize::GetNumCaches()
{
	return m_ncache;
}
