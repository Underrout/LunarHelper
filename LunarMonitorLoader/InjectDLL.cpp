#include "InjectDLL.h"

BOOL WINAPI InjectDLL(__in LPCWSTR lpcwszDll, __in HANDLE processHandle)
{
	if (processHandle == NULL)
	{
		return FALSE;
	}

	SIZE_T nLength;
	LPVOID lpLoadLibraryW = NULL;
	LPVOID lpRemoteString;
	HMODULE hMods[1024];
	DWORD cbNeeded;
	size_t i{ 0 };

	if (EnumProcessModules(processHandle, hMods, sizeof(hMods), &cbNeeded))
	{
		for (i = 0; i < (cbNeeded / sizeof(HMODULE)); i++)
		{
			char szModName[MAX_PATH];

			if (GetModuleBaseNameA(processHandle, hMods[i], szModName,
				sizeof(szModName) / sizeof(char)))
			{
				std::string as_string{ std::string(szModName).substr(0, 13)};
				if (as_string == "lunar-monitor") {
					return FALSE;
				}
			}
		}
	}
	else {
		DWORD last{ GetLastError() };
		std::ofstream os{ "D:/slready.txt" };
		os << "slj";
		os << last;
		os.close();
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
