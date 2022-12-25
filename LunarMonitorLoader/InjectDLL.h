#pragma once

#include <Windows.h>
#include <iostream>
#include <tchar.h>
#include <stdio.h>
#include <psapi.h>
#include <fstream>

BOOL WINAPI InjectDLL(__in LPCWSTR lpcwszDll, __in HANDLE processHandle);
