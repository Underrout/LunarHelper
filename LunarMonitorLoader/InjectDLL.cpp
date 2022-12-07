#include "InjectDLL.h"

BOOL WINAPI InjectDLL(__in LPCWSTR lpcwszDll, __in HANDLE processHandle)
{
	SIZE_T nLength;
	LPVOID lpLoadLibraryW = NULL;
	LPVOID lpRemoteString;

	if (processHandle == NULL)
	{
		return FALSE;
	}

	lpLoadLibraryW = GetProcAddress(GetModuleHandle(L"KERNEL32.DLL"), "LoadLibraryW");

	if (!lpLoadLibraryW)
	{
		return FALSE;
	}

	nLength = wcslen(lpcwszDll) * sizeof(WCHAR);

	// allocate mem for dll name
	lpRemoteString = VirtualAllocEx(processHandle, NULL, nLength + 1, MEM_COMMIT, PAGE_READWRITE);

	if (!lpRemoteString)
	{
		return FALSE;
	}

	// write dll name
	if (!WriteProcessMemory(processHandle, lpRemoteString, lpcwszDll, nLength, NULL))
	{
		// free allocated memory
		VirtualFreeEx(processHandle, lpRemoteString, 0, MEM_RELEASE);

		return FALSE;
	}

	HANDLE hThread = CreateRemoteThread(processHandle, NULL, NULL, (LPTHREAD_START_ROUTINE)lpLoadLibraryW, lpRemoteString, NULL, NULL);

	return TRUE;
}
