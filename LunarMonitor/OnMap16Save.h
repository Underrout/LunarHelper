#pragma once

#include "LM.h"
#include "Config.h"
#include "BuildResultUpdater.h"

#include <filesystem>
#include <optional>
#include <sstream>
#include <string>

namespace fs = std::filesystem;

class OnMap16Save
{
public:
	static void onMap16Save(bool succeeded, LM& lm, const std::optional<const Config>& config);
	static bool onSuccessfulMap16Save(LM& lm, const Config& config);
private:
	static void onFailedMap16Save(LM& lm);
};
