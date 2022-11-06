#include "OnMap16Save.h"

void OnMap16Save::onMap16Save(bool succeeded, LM& lm, const std::optional<const Config>& config)
{
    if (succeeded && config.has_value()) 
    {
		if (!onSuccessfulMap16Save(lm, config.value()))
		{
			lm.WriteOriginalCommentToRom();
		}
    }
    else
    {
        onFailedMap16Save(lm);
    }
}

bool OnMap16Save::onSuccessfulMap16Save(LM& lm, const Config& config)
{
    fs::path romPath = lm.getPaths().getRomDir();
    romPath += lm.getPaths().getRomName();

    if (lm.getLevelEditor().exportMap16(config.getMap16Path()))
    {
		fs::path export_path;
        if (config.getHumanReadableMap16ExecutablePath().has_value()) 
		{
			if (config.getHumanReadableMap16DirectoryPath().has_value()) {
				export_path = config.getHumanReadableMap16DirectoryPath().value();
			} else {
				size_t extension = config.getMap16Path().string().find_last_of(".");
				export_path = config.getMap16Path().string().substr(0, extension);
			}

			std::wstringstream ws;
			ws << config.getHumanReadableMap16ExecutablePath().value() << " --from-map16 " << 
				config.getMap16Path() << " " << export_path;

			std::wstring command = ws.str();
			std::vector<wchar_t> buf(command.begin(), command.end());
			buf.push_back(0);

			STARTUPINFO si;
			PROCESS_INFORMATION pi;

			ZeroMemory(&si, sizeof(si));
			si.cb = sizeof(si);
			ZeroMemory(&pi, sizeof(pi));

			if (!CreateProcess(NULL, buf.data(), NULL, NULL, false, CREATE_NO_WINDOW, NULL, NULL, &si, &pi))
			{
				Logger::log_error(L"Failed to create human readable map16 executable process");
				return false;
			}

			WaitForSingleObject(pi.hProcess, INFINITE);

			DWORD exitCode;

			GetExitCodeProcess(pi.hProcess, &exitCode);

			CloseHandle(pi.hProcess);
			CloseHandle(pi.hThread);
			
			if (exitCode == 0) {
				Logger::log_message(L"Successfully exported and converted map16 to \"%s\"", export_path);

				if (BuildResultUpdater::updateResourceEntry("map16", export_path))
				{
					Logger::log_message(L"Successfully updated build report entry for map16");
				}

				return true;
			}

			Logger::log_error(L"Failed to convert map16 after export");
			return false;
		}

		Logger::log_message(L"Successfully exported map16 to \"%s\"", config.getMap16Path().c_str());

		if (BuildResultUpdater::updateResourceEntry("map16", export_path))
		{
			Logger::log_message(L"Successfully updated build report entry for map16");
		}

		return true;
    }

	Logger::log_error(L"Failed to export map16");
	return false;
}

void OnMap16Save::onFailedMap16Save(LM& lm)
{
    Logger::log_error(L"Saving map16 to ROM failed");
}
