#pragma once

#include <fstream>
#include <optional>
#include <filesystem>
namespace fs = std::filesystem;

#include "json.hpp"
using json = nlohmann::json;

#include "md5.h"

constexpr auto jsonPath = ".lunar_helper/build_report.json";

class BuildResultUpdater
{
	public:
		static bool updateLevelEntry(const std::string& entryName, const fs::path& mwlPath);
		static bool updateAllLevelEntries(const fs::path& rootPath, const fs::path& levelDirectoryPath);
		static bool updateResourceEntry(const std::string& entryName, const fs::path& resourcePath);
		static std::optional<json> readInJson();
};
