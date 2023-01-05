#include "OnLevelSave.h"

#include <sstream>
#include <algorithm>

void OnLevelSave::onLevelSave(bool succeeded, unsigned int savedLevelNumber, LM& lm, const std::optional<const Config>& config)
{
    if (succeeded && config.has_value()) 
    {
        onSuccessfulLevelSave(savedLevelNumber, lm, config.value());
    }
    else
    {
        onFailedLevelSave(savedLevelNumber, lm);
    }
}

void OnLevelSave::onSuccessfulLevelSave(unsigned int savedLevelNumber, LM& lm, const Config& config)
{
    fs::path mwlPath = config.getLevelDirectory();
    std::string mwlFileName = "level #.mwl";

    int indexToInsertLvlNum = mwlFileName.find("#");

    if (indexToInsertLvlNum != std::string::npos)
    {
        std::stringstream sstream;
        sstream << std::hex << std::uppercase << lm.getLevelEditor().getLevelNumberBeingSaved() << std::nouppercase << std::dec;
        std::string lvlNumString = sstream.str();

        while (lvlNumString.size() != 3)
            lvlNumString = "0" + lvlNumString;

        mwlFileName = mwlFileName.substr(0, indexToInsertLvlNum) + lvlNumString + mwlFileName.substr(indexToInsertLvlNum + 1);
    }

    mwlPath /= mwlFileName;

    fs::path romPath = lm.getPaths().getRomDir();
    romPath += lm.getPaths().getRomName();

    if (lm.getLevelEditor().exportMwl(lm.getPaths().getLmExePath(), romPath, mwlPath, savedLevelNumber))
    {
        Logger::log_message(L"Successfully exported level to \"%s\"", mwlPath.c_str());

        const fs::path rootPath = lm.getPaths().getRomDir();
        std::string mwlSubPath = mwlPath.string().substr(rootPath.string().length(), std::string::npos);
        std::replace(mwlSubPath.begin(), mwlSubPath.end(), '\\', '/');

        if (BuildResultUpdater::updateLevelEntry(mwlSubPath, mwlPath))
        {
            Logger::log_message(L"Successfully updated build report entry for level \"%s\"", mwlPath.c_str());
        }
    }
    else
    {
        lm.WriteOriginalCommentToRom();
        Logger::log_error(L"Failed to export level");
    }
}

void OnLevelSave::onFailedLevelSave(unsigned int savedLevelNumber, LM& lm)
{
    Logger::log_error(L"Saving level to ROM failed");
}
