#include "OnGlobalDataSave.h"

#include <sstream>

void OnGlobalDataSave::onGlobalDataSave(bool succeeded, LM& lm, const std::optional<const Config>& config)
{
	if (succeeded && config.has_value())
	{
		onSuccessfulGlobalDataSave(lm, config.value());
	}
	else
	{
		onFailedGlobalDataSave(lm);
	}
}

void OnGlobalDataSave::onSuccessfulGlobalDataSave(LM& lm, const Config& config)
{
	try {
		exportBps(lm, config);
	}
	catch (const std::exception& exc)
	{
		lm.WriteOriginalCommentToRom();
		WhatWide what{ exc };
		Logger::log_error(L"Global data export failed with exception: \"%s\"", what.what());
	}
}

void OnGlobalDataSave::exportBps(LM& lm, const Config& config)
{
	fs::path romPath = lm.getPaths().getRomDir();
	romPath += lm.getPaths().getRomName();

	createBpsPatch(romPath, config.getCleanRomPath(), config.getGlobalDataPath(), config.getFlipsPath());
	Logger::log_message(L"Successfully exported global data to \"%s\"", config.getGlobalDataPath().c_str());

	if (BuildResultUpdater::updateResourceEntry("global_data", config.getGlobalDataPath()))
	{
		Logger::log_message(L"Successfully updated build report entry for global data");
	}
}

void OnGlobalDataSave::onFailedGlobalDataSave(LM& lm)
{
	Logger::log_error(L"Saving global data to ROM failed");
}

void OnGlobalDataSave::createBpsPatch(const fs::path& sourceRom, const fs::path& cleanRomPath, const fs::path& destinationPath, const fs::path& flipsExePath)
{
	std::wstringstream ws;

	ws << '\"' << flipsExePath.wstring() << "\" --create --bps-delta \"" << 
		cleanRomPath.wstring() << "\" \"" << sourceRom.wstring() << "\" \"" <<
		destinationPath.wstring() << "\"";

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
		throw std::runtime_error("Failed to create FLIPS process");
	}

	WaitForSingleObject(pi.hProcess, INFINITE);

	DWORD exitCode;

	GetExitCodeProcess(pi.hProcess, &exitCode);

	CloseHandle(pi.hProcess);
	CloseHandle(pi.hThread);

	if (exitCode != 0)
	{
		throw std::runtime_error("FLIPS failed to create bps patch");
	}
}
