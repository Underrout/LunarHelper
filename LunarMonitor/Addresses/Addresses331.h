#pragma once
#include <Windows.h>
#include <stdint.h>

constexpr uintptr_t LM_CURR_LEVEL_NUMBER = 0x592134;
constexpr uintptr_t LM_CURR_LEVEL_NUMBER_BEING_SAVED = 0x7FAD34;

constexpr uintptr_t LM_VERIFICATION_CODE = 0x90292C;
constexpr uintptr_t LM_COMMAND_WINDOW = 0xDBF76C;
constexpr uintptr_t LM_ALLOWED_TO_RELOAD_BOOLEAN = 0xDBF77C;

constexpr uintptr_t LM_CURR_ROM_NAME = 0x5C6B98;
constexpr uintptr_t LM_CURR_ROM_PATH = 0x7BD990;
constexpr uintptr_t LM_EXE_PATH = 0x598478;
constexpr uintptr_t LM_TOOLBAR_HANDLE = 0xDBF5A0;
constexpr uintptr_t LM_MAIN_EDITOR_WINDOW_HANDLE = 0x8C3844;
constexpr uintptr_t LM_MAIN_STATUSBAR_HANDLE = 0xDBF594;

constexpr uintptr_t LM_RENDER_LEVEL_FUNCTION = 0x53D1D8;
using renderLevelFunction = void(*)(DWORD a, DWORD b, DWORD c);

constexpr uintptr_t LM_MAP16_SAVE_FUNCTION = 0x441E10;
using saveMap16Function = BOOL(*)();

constexpr uintptr_t LM_LEVEL_SAVE_FUNCTION = 0x46A6F0;
using saveLevelFunction = BOOL(*)(DWORD x, DWORD y);

constexpr uintptr_t LM_OW_SAVE_FUNCTION = 0x50E310;
using saveOWFunction = BOOL(*)();

constexpr uintptr_t LM_NEW_ROM_FUNCTION = 0x465F70;
using newRomFunction = BOOL(*)(DWORD a, DWORD b);

constexpr uintptr_t LM_TITLESCREEN_SAVE_FUNCTION = 0x4A53A0;
using saveTitlescreenFunction = BOOL(*)();

constexpr uintptr_t LM_CREDITS_SAVE_FUNCTION = 0x4A5890;
using saveCreditsFunction = BOOL(*)();

constexpr uintptr_t LM_SHARED_PALETTES_SAVE_FUNCTION = 0x451630;
using saveSharedPalettesFunction = BOOL(*)(BOOL x);

constexpr uintptr_t LM_EXPORT_ALL_MAP16_FUNCTION = 0x4CEF60;
using export_all_map16_function = BOOL(*)(DWORD x, const char* full_output_path);

constexpr uintptr_t LM_COMMENT_FIELD_WRITE_FUNCTION = 0x5448C0;
using comment_field_write_function = void(*)(uint32_t a, const char* comment, uint32_t b);

template <typename T>
constexpr T AddressToFnPtr(const uintptr_t address) {
	return reinterpret_cast<T>(address);
}
