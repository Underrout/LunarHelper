#pragma once

#include "Paths.h"
#include "LevelEditor.h"

class LM
{
private:
	const Paths paths{};
	LevelEditor levelEditor{};

public:
	const Paths& getPaths();
	LevelEditor& getLevelEditor();
	bool WriteCommentToRom(const char* comment);
	bool WriteOriginalCommentToRom();
};
