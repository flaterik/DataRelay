#pragma once
#include "Stdafx.h"


namespace BerkeleyDbWrapper
{
	using namespace std;
	using namespace System;

	public ref class BdbExceptionFactory abstract sealed
	{
	public:
		static BdbException^ Create(int returnCode, const exception *cex, String ^message);
		static BdbException^ Create(int returnCode, const DbException *dbex, String ^message);
		static BdbException^ Create(int returnCode, String ^message);
		static BdbException^ Create(const exception *cex, String ^message);
		static BdbException^ Create(const DbException *dbex, String ^message);

	private:
		static int SetCode(int returnCode, const exception *cex, const DbException *dbex);
		static String ^CombineMessages(const exception *cex, String ^message);
	};
}