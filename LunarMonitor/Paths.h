#pragma once

#include <filesystem>
#include <cstdint>
#include <Windows.h>

#include "Constants.h"

constexpr const char* FISH_REPLACEMENT = "   Mario says     TRANS RIGHTS  ";
constexpr const char* FISH = "I am Naaall, and I love fiiiish!";

class Paths
{
public:
	static std::filesystem::path getRomName();
	static std::filesystem::path getRomDir();
	static std::filesystem::path getLmExePath();
	static std::filesystem::path getRomPath();
	static HWND* getToolbarHandle();
	static HWND* getMainEditorWindowHandle();
	static HWND* getMainEditorStatusbarHandle();
private:
	static std::string trim(const std::string &str);
};
