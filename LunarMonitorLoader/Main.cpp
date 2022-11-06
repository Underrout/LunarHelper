#include <Windows.h>
#include <filesystem>
#include <optional>
#include <winver.h>
#include <array>
#include <iostream>
#include <string>

#pragma comment(lib,"Version.lib")

#include <fstream>
#include <format>

#include <detours.h>

#include "../LunarMonitor/md5.h"

constexpr const char* DLL_FORMAT = "{}/DLLs/lunar-monitor-{}.dll";

namespace fs = std::filesystem;

constexpr std::array<std::tuple<const char*, size_t>, 4> LUNAR_MAGIC_HASHES {{
	{"1f555cd921124183d0d6db1e326201de", 330},
	{"970ff7be02f2dfa833c32f658ba0203f", 331},
	{"1346dd0510e6316643235c9853d6f252", 332},
	{"90294785aff9d7cef5e2671a71e791b1", 333},
}};

std::optional<std::tuple<const fs::path, size_t>> get_lunar_magic();

int main(int argc, char* argv[])
{
	if (argc <= 2)
		FreeConsole();

	const auto lunar_magic = get_lunar_magic();

	if (!lunar_magic.has_value())
	{
		MessageBox(NULL, L"No viable Lunar Magic found!", NULL, MB_OK | MB_ICONERROR);
		return 1;
	}

	const auto lunar_magic_path = std::get<const fs::path>(lunar_magic.value());
	const auto lunar_magic_version = std::get<size_t>(lunar_magic.value());

	wchar_t szPath[MAX_PATH];
	GetModuleFileNameW(NULL, szPath, MAX_PATH);
	const auto our_path{ fs::path{ szPath }.parent_path() / "" };

	const fs::path dll_path = std::format(DLL_FORMAT, our_path.string(), std::to_string(lunar_magic_version));

	if (!fs::exists(dll_path))
	{
		MessageBox(NULL, L"DLL for viable Lunar Magic not found!", NULL, MB_OK | MB_ICONERROR);
		return 1;
	}

	STARTUPINFO si;
	PROCESS_INFORMATION pi;

	ZeroMemory(&si, sizeof(si));
	si.cb = sizeof(si);
	ZeroMemory(&pi, sizeof(pi));

	size_t converted_chars;

	std::string commandLineStr = "";
	for (int i = 1; i < argc; i++) commandLineStr.append(std::string(argv[i]).append(" "));
	
	TCHAR args[4096];

	std::wstring quoted_lunar_magic_path = L'"' + lunar_magic_path.wstring() + L'"';

	mbstowcs_s(&converted_chars, args, 
		strlen(commandLineStr.c_str()) + 1, commandLineStr.c_str(), _TRUNCATE);

	std::wstring args_string = std::wstring(args);

	std::wstring full_command_line = quoted_lunar_magic_path + L' ' + args_string;

	size_t i = 0;
	for (const auto c : full_command_line)
	{
		args[i] = c;
		++i;
	}
	args[i] = L'\0';

	DetourCreateProcessWithDll(
		NULL,
		args,
		NULL,
		NULL,
		FALSE,
		0,
		NULL,
		NULL,
		&si,
		&pi,
		dll_path.string().c_str(),
		NULL
	);

	if (argc >= 3)
	{
		WaitForSingleObject(pi.hProcess, INFINITE);

		DWORD exitCode;

		GetExitCodeProcess(pi.hProcess, &exitCode);

		CloseHandle(pi.hProcess);
		CloseHandle(pi.hThread);

		return exitCode;
	}

	CloseHandle(pi.hProcess);
	CloseHandle(pi.hThread);

	return 0;
}

std::optional<std::tuple<const fs::path, size_t>> get_lunar_magic()
{
	size_t curr_version = 0;
	fs::path curr_path{};

	wchar_t szPath[MAX_PATH];
	GetModuleFileNameW(NULL, szPath, MAX_PATH);
	const auto our_path{ fs::path{ szPath }.parent_path() / "" };

	for (const auto entry : fs::directory_iterator(our_path))
	{
		if (!entry.is_regular_file())
			continue;

		const auto hash = md5File(entry);

		for (const auto& tup : LUNAR_MAGIC_HASHES)
		{
			if (std::get<const char*>(tup) == hash && std::get<size_t>(tup) > curr_version)
			{
				curr_version = std::get<size_t>(tup);
				curr_path = entry;
			}
		}
	}

	if (curr_version == 0)
		return std::nullopt;

	return std::make_tuple(curr_path, curr_version);
}
