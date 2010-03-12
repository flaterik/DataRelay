#include "stdafx.h"
#include "DatabaseEntry.h"
#include "BdbException.h"

using namespace std;

BerkeleyDbWrapper::DatabaseEntry::DatabaseEntry(int capacity) : StartPosition(0), Length(0)
{
	Buffer = gcnew array<Byte>(capacity);
}

void BerkeleyDbWrapper::DatabaseEntry::Resize(int size)
{
	Buffer = gcnew array<Byte>(size);
}

BerkeleyDbWrapper::DatabaseEntry::operator DataBuffer(DatabaseEntry^ entry)
{
	if (entry == nullptr) return DataBuffer::Empty;
	array<Byte> ^buffer = entry->Buffer;
	if (buffer == nullptr) return DataBuffer::Empty;
	int blen = buffer->Length;
	if (blen == 0) return DataBuffer::Empty;
	int start = entry->StartPosition;
	if (start >= blen) start = blen - 1;
	if (start < 0) start = 0;
	int len = entry->Length;
	if (start + len > blen) len = blen - start;
	if (len <= 0) return DataBuffer::Empty;
	return DataBuffer::Create(buffer, start, len);
}

