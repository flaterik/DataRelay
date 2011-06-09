#pragma once


private class ConvStr
{
public:
	ConvStr(System::String^ src);
	~ConvStr();
	char *Str();
private:
	char * chrPtr;
};
