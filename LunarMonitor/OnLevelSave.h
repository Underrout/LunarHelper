#pragma once

#include "LM.h"
#include "BuildResultUpdater.h"

#include "Config.h"
#include <filesystem>
#include <optional>

namespace fs = std::filesystem;

class OnLevelSave
{
public:
	static void onLevelSave(bool succeeded, unsigned int savedLevelNumber, LM& lm, const std::optional<const Config>& config);
private:
	static void onSuccessfulLevelSave(unsigned int savedLevelNumber, LM& lm, const Config& config);
	static void onFailedLevelSave(unsigned int savedLevelNumber, LM& lm);
};
