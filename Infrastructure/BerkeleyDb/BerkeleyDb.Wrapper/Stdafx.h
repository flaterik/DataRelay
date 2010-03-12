// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently,
// but are changed infrequently

#pragma once

#ifdef _WIN64
#ifdef _DEBUG
#pragma comment(lib, "libdb47.Debug.x64.lib")
#else
#pragma comment(lib, "libdb47.x64.lib")
#endif
#else
#ifdef _DEBUG
#pragma comment(lib, "libdb47.Debug.win32.lib")
#else
#pragma comment(lib, "libdb47.win32.lib")
#endif

#endif
#define _WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <vcclr.h>
#include "db_cxx.h"
#include "util.h"
#include <crtdbg.h>
#include <iostream>
#include <fstream>


int ret = _CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_CHECK_ALWAYS_DF | _CRTDBG_CHECK_CRT_DF | _CRTDBG_LEAK_CHECK_DF);
