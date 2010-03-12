#pragma once

using namespace System;
using namespace System::Runtime::InteropServices;


public class ConvStr
{
public:
	ConvStr(String^ src);
	~ConvStr();
	char *Str();
private:
	char * chrPtr;
};
