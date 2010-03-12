#include "StdAfx.h"
#include "ConvStr.h"

ConvStr::ConvStr(String^ src)
{
	if (src != nullptr) {
		chrPtr = (char *) Marshal::StringToHGlobalAnsi(src).ToPointer();
	} else {
		chrPtr = NULL;
	}
}

ConvStr::~ConvStr()
{
	if (chrPtr != NULL) {
		try {  Marshal::FreeHGlobal((IntPtr)chrPtr); }
		finally { chrPtr = NULL; }
	}
}

char * ConvStr::Str() {
	return chrPtr;
}