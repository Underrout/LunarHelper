#pragma once

#include <Windows.h>
#include <iostream>
#include <fstream>

BOOL WINAPI InjectDLL(__in LPCWSTR lpcwszDll, __in HANDLE processHandle);
