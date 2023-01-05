#include "Config.h"

#include <fstream>
#include <stdexcept>
#include <algorithm>

// trim from both ends and dequotes a string
static inline std::string trim_whitespace_dequote(const std::string& orig) {
	auto s{ orig };
	s.erase(s.begin(), std::find_if(s.begin(), s.end(), [](auto ch) { return !std::isspace(ch) && !(ch == '"'); }));
	s.erase(std::find_if(s.rbegin(), s.rend(), [](auto ch) { return !std::isspace(ch) && !(ch == '"'); }).base(), s.end());
	return s;
}

Config::Config(const fs::path& configFilePath)
{
	for (auto& option : configOptions)
	{
		std::get<Set>(option) = Set::No;
	}

	const fs::path basePath = configFilePath.parent_path();

	const auto OptionNotFound = configOptions.end();

	std::ifstream file(configFilePath);

	if (!file)
	{
		throw std::runtime_error("Couldn't open configuration file, no config file found at \"" + configFilePath.string() + "\"");
	}

	std::string currLine;

	while (std::getline(file, currLine))
	{
		if (currLine.substr(0, 2) == "--")
			continue;

		if (std::all_of(currLine.begin(), currLine.end(), [=](const auto &c){ return std::isspace(c); }))
			continue;

		auto it = std::find_if(configOptions.begin(), configOptions.end(), [&currLine](const OptionTuple& option_tpl) {
			const auto& option = std::get<const std::string_view>(option_tpl);
			return currLine.compare(0, option.size(), option) == 0;
			});

		if (it != OptionNotFound)
		{
			auto& [name, isoptional, isset] = *it;
			auto varName = std::string{ name };
			auto varVal = trim_whitespace_dequote(currLine.substr(varName.size()));

			if (isset == Set::No)
			{
				isset = Set::Yes;
				setConfigVar(varName, varVal, basePath);
			}
			else
			{
				throw std::runtime_error("Config var \"" + varName + "\" redefined");
			}
		}
		else {
			throw std::runtime_error("Non existing config var tried to be defined: " + currLine);
		}
	}
	auto all_non_optional_set = std::all_of(configOptions.begin(), configOptions.end(), [](const OptionTuple& tup) {
		auto isoptional = std::get<Optional>(tup);
		auto isset = std::get<Set>(tup);
		if (isoptional == Optional::Yes)		// if it's optional we don't care if it's set or not
			return true;
		if (isset == Set::Yes)					// if it's not optional, we check if it's set and return appropriately
			return true;
		return false;
		});
	if (!all_non_optional_set) {
		Logger::log_warning(L"Not all required config variables have been set");
	}

	if (std::get<Set>(configOptions[8]) == Set::No)
	{
		Logger::setDefaultLogPath(basePath);
	}

	if (std::get<Set>(configOptions[9]) == Set::No)
	{
		Logger::setDefaultLogLevel();
	}
}

void Config::setConfigVar(const std::string& varName, const std::string& varVal, const fs::path& basePath)
{
	if (varName == levelDirectoryOption)
	{
		levelDirectory = basePath;
		levelDirectory /= varVal;
	}
	else if (varName == flipsPathOption)
	{
		flipsPath = basePath;
		flipsPath /= varVal;
	}
	else if (varName == map16PathOption)
	{
		map16Path = basePath;
		map16Path /= varVal;
	}
	else if (varName == cleanRomPathOption)
	{
		cleanRomPath = basePath;
		cleanRomPath /= varVal;
	}
	else if (varName == sharedPalettesPathOption)
	{
		sharedPalettesPath = basePath;
		sharedPalettesPath /= varVal;
	}
	else if (varName == humanReadableMap16ExecutableOption)
	{
		fs::path path = basePath;
		path /= varVal;
		humanReadableMap16ExecutablePath = path;
	}
	else if (varName == humanReadableMap16DirectoryOption)
	{
		fs::path path = basePath;
		path /= varVal;
		humanReadableMap16DirectoryPath = path;
	}
	else if (varName == globalDataPathOption)
	{
		globalDataPath = basePath;
		globalDataPath /= varVal;
	} 
	else if (varName == logFilePathOption) {
		Logger::setLogPath(basePath / varVal);
	}
	else if (varName == logLevelOption) {
		if (varVal == "Warn"sv) {
			Logger::setLogLevel(LogLevel::Warn);
		}
		else if (varVal == "Log"sv) {
			Logger::setLogLevel(LogLevel::Log);
		}
		else if (varVal == "Silent"sv) {
			Logger::setLogLevel(LogLevel::Silent);
		}
		else {
			throw std::runtime_error("Invalid log level option, valid options are Warn, Log and Silent");
		}
	}
	else
	{
		throw std::runtime_error("Invalid config var detected");
	}
}

const fs::path& Config::getLevelDirectory() const
{
	return levelDirectory;
}

const fs::path& Config::getLogFilePath() const
{
	return Logger::getLogPath();
}

LogLevel Config::getLogLevel() const
{
	return Logger::getLogLevel();
}

const fs::path& Config::getMap16Path() const
{
	return map16Path;
}

const fs::path& Config::getFlipsPath() const
{
	return flipsPath;
}

const fs::path& Config::getGlobalDataPath() const
{
	return globalDataPath;
}

const fs::path& Config::getCleanRomPath() const
{
	return cleanRomPath;
}

const fs::path& Config::getSharedPalettesPath() const
{
	return sharedPalettesPath;
}

const std::optional<const fs::path> Config::getHumanReadableMap16ExecutablePath() const
{
	return humanReadableMap16ExecutablePath;
}

const std::optional<const fs::path> Config::getHumanReadableMap16DirectoryPath() const
{
	return humanReadableMap16DirectoryPath;
}
