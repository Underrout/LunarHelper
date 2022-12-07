#pragma once
#include <Windows.h>
#include <stdint.h>

constexpr uintptr_t LM_CURR_LEVEL_NUMBER = 0x59C3B4;
constexpr uintptr_t LM_CURR_LEVEL_NUMBER_BEING_SAVED = 0x805AE4;

constexpr uintptr_t LM_VERIFICATION_CODE = 0x90D6DC;
constexpr uintptr_t LM_COMMAND_WINDOW = 0xDD4554;
constexpr uintptr_t LM_ALLOWED_TO_RELOAD_BOOLEAN = 0xDD4522;

constexpr uintptr_t LM_CURR_ROM_NAME = 0x5D1800;
constexpr uintptr_t LM_CURR_ROM_PATH = 0x7C8740;
constexpr uintptr_t LM_EXE_PATH = 0x5A2D38;
constexpr uintptr_t LM_TOOLBAR_HANDLE = 0xDD4374;
constexpr uintptr_t LM_MAIN_EDITOR_WINDOW_HANDLE = 0x8CE5F4;
constexpr uintptr_t LM_MAIN_STATUSBAR_HANDLE = 0xDD4368;

constexpr uintptr_t LM_RENDER_LEVEL_FUNCTION = 0x54939A;
using renderLevelFunction = void(*)(DWORD a);

constexpr uintptr_t LM_MAP16_SAVE_FUNCTION = 0x446D60;
using saveMap16Function = BOOL(*)();

constexpr uintptr_t LM_LEVEL_SAVE_FUNCTION = 0x46FBF0;
using saveLevelFunction = BOOL(*)(DWORD x, DWORD y);

constexpr uintptr_t LM_OW_SAVE_FUNCTION = 0x514BE0;
using saveOWFunction = BOOL(*)();

constexpr uintptr_t LM_NEW_ROM_FUNCTION = 0x46B450;
using newRomFunction = BOOL(*)(DWORD a, DWORD b);

constexpr uintptr_t LM_TITLESCREEN_SAVE_FUNCTION = 0x4AADC0;
using saveTitlescreenFunction = BOOL(*)();

constexpr uintptr_t LM_CREDITS_SAVE_FUNCTION = 0x4AB2B0;
using saveCreditsFunction = BOOL(*)();

constexpr uintptr_t LM_SHARED_PALETTES_SAVE_FUNCTION = 0x456930;
using saveSharedPalettesFunction = BOOL(*)(BOOL x);

constexpr uintptr_t LM_EXPORT_ALL_MAP16_FUNCTION = 0x4D4C40;
using export_all_map16_function = BOOL(*)(DWORD x, const char* full_output_path);

constexpr uintptr_t LM_COMMENT_FIELD_WRITE_FUNCTION = 0x54B320;
using comment_field_write_function = void(*)(uint32_t a, const char* comment, uint32_t b);

template <typename T>
constexpr T AddressToFnPtr(const uintptr_t address) {
	return reinterpret_cast<T>(address);
}
