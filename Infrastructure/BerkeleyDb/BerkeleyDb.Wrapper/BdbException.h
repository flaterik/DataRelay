#pragma once
#include "Stdafx.h"

using namespace std;
using namespace System;

namespace BerkeleyDbWrapper
{
	public ref class BdbException : ApplicationException
	{
	public:
		property int Code { int get(); }
		property bool Handled { bool get(); void set(bool val); }
		BdbException(int returnCode, const exception *cex, String ^message);
		BdbException(int returnCode, const DbException *dbex, String ^message);
		BdbException(int returnCode, String ^message);
		BdbException(const exception *cex, String ^message);
		BdbException(const DbException *dbex, String ^message);

	private:
		int code;
		bool handled;
		void SetCode(int returnCode, const exception *cex, const DbException *dbex);
		static String ^CombineMessages(const exception *cex, String ^message);
	};

	public ref class BufferSmallException : BdbException
	{
	public:
		UInt32 BufferLength;
		UInt32 RecordLength;

	internal:
		BufferSmallException(String ^message);
	};
}