#include "Paths.h"

std::filesystem::path Paths::getRomName()
{
	std::string path = reinterpret_cast<const char*>(LM_CURR_ROM_NAME);

	return trim(path);
}

std::filesystem::path Paths::getRomDir()
{
	std::string path = reinterpret_cast<const char*>(LM_CURR_ROM_PATH);

	return trim(path);
}

std::string Paths::trim(const std::string& str)
{
	size_t first = str.find_first_not_of(' ');
	if (std::string::npos == first)
	{
		return str;
	}
	size_t last = str.find_last_not_of(' ');
	return str.substr(first, (last - first + 1));
}

std::filesystem::path Paths::getLmExePath()
{
	return trim(std::string(reinterpret_cast<const char*>(LM_EXE_PATH)));
}

std::filesystem::path Paths::getRomPath()
{
	std::filesystem::path dir = getRomDir();
	dir += getRomName();

	return dir;
}

HWND* Paths::getToolbarHandle()
{
	return reinterpret_cast<HWND*>(LM_TOOLBAR_HANDLE);
}

HWND* Paths::getMainEditorWindowHandle()
{
	return reinterpret_cast<HWND*>(LM_MAIN_EDITOR_WINDOW_HANDLE);
}

HWND* Paths::getMainEditorStatusbarHandle()
{
	return reinterpret_cast<HWND*>(LM_MAIN_STATUSBAR_HANDLE);
}
