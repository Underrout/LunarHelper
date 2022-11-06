#pragma once

#include <filesystem>
#include <cstdint>
#include <Windows.h>

#if LM_VERSION == 330
#include "Addresses/Addresses330.h"
#elif LM_VERSION == 331
#include "Addresses/Addresses331.h"
#elif LM_VERSION == 332
#include "Addresses/Addresses331.h"
#elif LM_VERSION == 333
#include "Addresses/Addresses331.h"
#endif

constexpr const char* FISH_REPLACEMENT = "   Mario says     TRANS RIGHTS  ";
constexpr const char* FISH = "I am Naaall, and I love fiiiish!";

class Paths
{
public:
	static const char* getRomName();
	static const char* getRomDir();
	static const char* getLmExePath();
	static std::filesystem::path getRomPath();
	static HWND* getToolbarHandle();
	static HWND* getMainEditorWindowHandle();
	static HWND* getMainEditorStatusbarHandle();
};
