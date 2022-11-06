#pragma once
#include "BuildResultUpdater.h"
#include "Constants.h"

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
