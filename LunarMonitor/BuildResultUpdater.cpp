#include "BuildResultUpdater.h"

std::optional<json> BuildResultUpdater::readInJson()
{
	if (!fs::exists(jsonPath))
	{
		return std::nullopt;
	}

	std::ifstream i(jsonPath);
	json j;
	try
	{
		i >> j;
	}
	catch (const json::exception&)
	{
		i.close();
		return std::nullopt;
	}

	i.close();

	return j;
}

bool BuildResultUpdater::updateLevelEntry(const std::string& entryName, const fs::path& mwlPath)
{
	if (!fs::exists(mwlPath))
	{
		return false;
	}

	std::optional<json> j = readInJson();

	if (!j.has_value())
	{
		return false;
	}

	try
	{
		j.value()["levels"][entryName] = md5IfExists(mwlPath).value();
	}
	catch (const json::exception&)
	{
		return false;
	}

	std::ofstream o(jsonPath);
	o << std::setw(2) << j.value();
	o.close();

	return true;
}

bool BuildResultUpdater::updateAllLevelEntries(const fs::path& rootPath, const fs::path& levelDirectoryPath)
{
	if (!fs::exists(levelDirectoryPath))
	{
		return false;
	}

	std::optional<json> j = readInJson();

	if (!j.has_value())
	{
		return false;
	}

	try
	{
		for (const auto& mwlPath : fs::directory_iterator(levelDirectoryPath))
		{
			std::string mwlSubPath = mwlPath.path().string().substr(rootPath.string().length() + 1, std::string::npos);
			std::replace(mwlSubPath.begin(), mwlSubPath.end(), '\\', '/');

			j.value()["levels"][mwlSubPath] = md5IfExists(mwlPath).value();
		}
	}
	catch (const json::exception&)
	{
		return false;
	}

	std::ofstream o(jsonPath);
	o << std::setw(2) << j.value();
	o.close();

	return true;
}

bool BuildResultUpdater::updateResourceEntry(const std::string& entryName, const fs::path& resourcePath)
{
	if (!fs::exists(resourcePath))
	{
		return false;
	}

	std::optional<json> j = readInJson();
	
	if (!j.has_value())
	{
		return false;
	}

	try
	{
		j.value()[entryName] = md5IfExists(resourcePath).value();
	}
	catch (const json::exception&)
	{
		return false;
	}

	std::ofstream o(jsonPath);
	o << std::setw(2) << j.value();
	o.close();

	return true;
}
