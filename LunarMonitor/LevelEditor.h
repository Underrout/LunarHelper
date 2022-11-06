#pragma once
#include "BuildResultUpdater.h"

#if LM_VERSION == 330
#include "Addresses/Addresses330.h"
#elif LM_VERSION == 331
#include "Addresses/Addresses331.h"
#elif LM_VERSION == 332
#include "Addresses/Addresses331.h"
#elif LM_VERSION == 333
#include "Addresses/Addresses331.h"
#endif

#include <filesystem>

namespace fs = std::filesystem;

class LevelEditor
{
public:
	static unsigned int getCurrLevelNumber();
	static unsigned int getLevelNumberBeingSaved();
	static bool exportMwl(
		const fs::path& lmExePath, const fs::path& romPath,
		const fs::path& mwlFilePath, unsigned int levelNumber
	);
	static bool exportAllMwls(const fs::path& lmExePath, const fs::path& romPath, 
		const fs::path& mwlFilePath);
	static bool exportMap16(const fs::path& map16Path);
	static void reloadROM(HWND lmRequestWindowHandle);
};
