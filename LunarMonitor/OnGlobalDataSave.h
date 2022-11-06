#pragma once

#include "LM.h"
#include "Config.h"
#include "BuildResultUpdater.h"

#include <filesystem>
#include <optional>

namespace fs = std::filesystem;

class OnGlobalDataSave
{
public:
	static void onGlobalDataSave(bool succeeded, LM& lm, const std::optional<const Config>& config);
	static void onSuccessfulGlobalDataSave(LM& lm, const Config& config);
	static void onFailedGlobalDataSave(LM& lm);
	static void exportBps(LM& lm, const Config& config);
private:
	static void createBpsPatch(const fs::path& sourceRom, const fs::path& cleanRomPath, const fs::path& destinationPath, const fs::path& flipsExePath);
};
