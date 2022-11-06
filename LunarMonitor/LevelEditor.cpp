#include "LevelEditor.h"

#include <sstream>
#include <vector>
#include <string>

namespace fs = std::filesystem;

unsigned int LevelEditor::getCurrLevelNumber()
{
	return *reinterpret_cast<unsigned int*>(LM_CURR_LEVEL_NUMBER);
}

unsigned int LevelEditor::getLevelNumberBeingSaved()
{
	return *reinterpret_cast<unsigned int*>(LM_CURR_LEVEL_NUMBER_BEING_SAVED);
}

bool LevelEditor::exportMwl(
	const fs::path& lmExePath, const fs::path& romPath, 
	const fs::path& mwlFilePath, unsigned int levelNumber
)
{
	std::wstringstream ws;

	ws << '\"' << lmExePath.wstring() << "\" -ExportLevel \"" << romPath.wstring() <<
		"\" \"" << mwlFilePath.wstring() << "\" " << std::hex << levelNumber;

	std::wstring command = ws.str();
	std::vector<wchar_t> buf(command.begin(), command.end());
	buf.push_back(0);

	STARTUPINFO si;
	PROCESS_INFORMATION pi;

	ZeroMemory(&si, sizeof(si));
	si.cb = sizeof(si);
	ZeroMemory(&pi, sizeof(pi));

	if (!CreateProcess(NULL, buf.data(), NULL, NULL, false, 0, NULL, NULL, &si, &pi))
	{
		return false;
	}

	WaitForSingleObject(pi.hProcess, INFINITE);

	DWORD exitCode;

	GetExitCodeProcess(pi.hProcess, &exitCode);

	CloseHandle(pi.hProcess);
	CloseHandle(pi.hThread);

	return exitCode == 0;
}

bool LevelEditor::exportAllMwls(const fs::path& lmExePath, const fs::path& romPath, const fs::path& mwlFilePath)
{
	std::wstringstream ws;

	ws << '\"' << lmExePath.wstring() << "\" -ExportMultLevels \"" << romPath.wstring() <<
		"\" \"" << mwlFilePath.wstring() << "\" ";

	std::wstring command = ws.str();
	std::vector<wchar_t> buf(command.begin(), command.end());
	buf.push_back(0);

	STARTUPINFO si;
	PROCESS_INFORMATION pi;

	ZeroMemory(&si, sizeof(si));
	si.cb = sizeof(si);
	ZeroMemory(&pi, sizeof(pi));

	if (!CreateProcess(NULL, buf.data(), NULL, NULL, false, 0, NULL, NULL, &si, &pi))
	{
		return false;
	}

	WaitForSingleObject(pi.hProcess, INFINITE);

	DWORD exitCode;

	GetExitCodeProcess(pi.hProcess, &exitCode);

	CloseHandle(pi.hProcess);
	CloseHandle(pi.hThread);

	if (exitCode != 0)
	{
		return false;
	}

	const fs::path rootPath = romPath.parent_path();

	BuildResultUpdater::updateAllLevelEntries(rootPath, mwlFilePath.parent_path());

	return true;
}

bool LevelEditor::exportMap16(const fs::path& map16Path)
{
	return AddressToFnPtr<export_all_map16_function>(LM_EXPORT_ALL_MAP16_FUNCTION)(0, map16Path.string().c_str());
}

void LevelEditor::reloadROM(HWND lmRequestWindowHandle)
{
	// this is literally gaslighting
	BOOL* we_can_reload = reinterpret_cast <BOOL*>(LM_ALLOWED_TO_RELOAD_BOOLEAN);
	*we_can_reload = true;

	PostMessage(
		(*reinterpret_cast<HWND*>(LM_COMMAND_WINDOW)), 
		0xBECB, 
		0, 
		(LPARAM)((*reinterpret_cast<unsigned int*>(LM_VERIFICATION_CODE)) << 16) + 2
	);
}
