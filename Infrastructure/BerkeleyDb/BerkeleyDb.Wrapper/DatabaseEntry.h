#pragma once
#include "Stdafx.h"

using namespace MySpace::Common::Storage;

namespace BerkeleyDbWrapper
{
	public ref class DatabaseEntry
	{
	public:
		array<Byte> ^Buffer;
		int StartPosition;
		int Length;

		DatabaseEntry(int capacity);
		void Resize(int size);

		static operator DataBuffer(DatabaseEntry^ entry);

	private:
	};
}