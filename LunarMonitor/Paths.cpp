#include "Paths.h"

const char* Paths::getRomName()
{
	return reinterpret_cast<const char*>(LM_CURR_ROM_NAME);
}

const char* Paths::getRomDir()
{
	return reinterpret_cast<const char*>(LM_CURR_ROM_PATH);
}

const char* Paths::getLmExePath()
{
	return reinterpret_cast<const char*>(LM_EXE_PATH);
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
