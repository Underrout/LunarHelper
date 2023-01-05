#include "LM.h"

const Paths& LM::getPaths()
{
	return paths;
}

LevelEditor& LM::getLevelEditor()
{
	return levelEditor;
}

bool LM::WriteCommentToRom(const char* comment)
{
    std::ofstream rom;

    try {
        auto rom_path = getPaths().getRomPath();
        rom.open(rom_path, std::ios::binary | std::ios::out | std::ios::in);

        // assumes that if something isn't .smc, it's not headered
        const size_t comment_pos = rom_path.extension() == ".smc" ? COMMENT_FIELD_SMC_ROM_OFFSET : COMMENT_FIELD_SFC_ROM_OFFSET;

        rom.seekp(comment_pos);
        rom.write(comment, 0x20);
        rom.close();
        return true;
    }
    catch (std::exception)
    {
        if (rom.is_open())
        {
            rom.close();
        }
        return false;
    }
}

bool LM::WriteOriginalCommentToRom()
{
    return WriteCommentToRom(FISH);
}
