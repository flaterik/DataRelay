#include "stdafx.h"
#include "BdbException.h"

void BerkeleyDbWrapper::BdbException::SetCode(int returnCode, const exception *cex, const DbException *dbex)
{
	handled = false;
	if (returnCode != 0)
	{
		code = returnCode;
		return;
	}
	if (dbex != NULL)
	{
		code = dbex->get_errno();
		return;
	}
	if (cex != NULL)
	{
		dbex = dynamic_cast<const DbException *>(cex);
		if (dbex != NULL)
		{
			code = dbex->get_errno();
			return;
		}
	}
	code = 0;
}

String ^BerkeleyDbWrapper::BdbException::CombineMessages(const exception *cex, String ^message)
{
	if (cex == NULL) return message;
	String ^libraryMessage = gcnew String(cex->what());
	if (String::IsNullOrEmpty(message)) return libraryMessage;
	return String::Format(L"{0}: {1}", message, libraryMessage);
}

int BerkeleyDbWrapper::BdbException::Code::get() { return code; }

bool BerkeleyDbWrapper::BdbException::Handled::get() { return handled; }
void BerkeleyDbWrapper::BdbException::Handled::set(bool val) { handled = val; }

BerkeleyDbWrapper::BdbException::BdbException(int returnCode, const exception *cex, String ^message) 
: ApplicationException(CombineMessages(cex, message))
{
	SetCode(returnCode, cex, NULL);
}

BerkeleyDbWrapper::BdbException::BdbException(int returnCode, const DbException *dbex, String ^message) 
: ApplicationException(CombineMessages(dbex, message))
{
	SetCode(returnCode, NULL, dbex);
}

BerkeleyDbWrapper::BdbException::BdbException(int returnCode, String ^message) 
: ApplicationException(message)
{
	SetCode(returnCode, NULL, NULL);
}

BerkeleyDbWrapper::BdbException::BdbException(const exception *cex, String ^message) 
: ApplicationException(CombineMessages(cex, message))
{
	SetCode(0, cex, NULL);
}

BerkeleyDbWrapper::BdbException::BdbException(const DbException *dbex, String ^message) 
: ApplicationException(CombineMessages(dbex, message))
{
	SetCode(0, NULL, dbex);
}

BerkeleyDbWrapper::BufferSmallException::BufferSmallException(String ^message) 
: BdbException(static_cast<int>(DbRetVal::BUFFER_SMALL), message)
{

}
