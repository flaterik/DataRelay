#include "stdafx.h"
#include "BdbExceptionFactory.h"

using namespace std;
using namespace System;
using namespace BerkeleyDbWrapper;

int BdbExceptionFactory::SetCode(int returnCode, const exception *cex, const DbException *dbex)
{
	if (returnCode != 0)
	{
		return returnCode;
	}
	if (dbex != NULL)
	{
		return dbex->get_errno();
	}
	if (cex != NULL)
	{
		dbex = dynamic_cast<const DbException *>(cex);
		if (dbex != NULL)
		{
			return dbex->get_errno();
		}
	}
	return 0;
}

String ^BdbExceptionFactory::CombineMessages(const exception *cex, String ^message)
{
	if (cex == NULL) return message;
	String ^libraryMessage = gcnew String(cex->what());
	if (String::IsNullOrEmpty(message)) return libraryMessage;
	return String::Format(L"{0}: {1}", message, libraryMessage);
}

BdbException^ BdbExceptionFactory::Create(int returnCode, const exception *cex, String ^message)
{
	return gcnew BdbException(SetCode(returnCode, cex, NULL), CombineMessages(cex, message));
}

BdbException^ BdbExceptionFactory::Create(int returnCode, const DbException *dbex, String ^message) 
{
	return gcnew BdbException(SetCode(returnCode, NULL, dbex), CombineMessages(dbex, message));
}

BdbException^ BdbExceptionFactory::Create(int returnCode, String ^message) 
{
	return gcnew BdbException(SetCode(returnCode, NULL, NULL), message);
}

BdbException^ BdbExceptionFactory::Create(const exception *cex, String ^message) 
{
	return gcnew BdbException(SetCode(0, cex, NULL), CombineMessages(cex, message));
}

BdbException^ BdbExceptionFactory::Create(const DbException *dbex, String ^message)
{
	return gcnew BdbException(SetCode(0, NULL, dbex), CombineMessages(dbex, message));
}
