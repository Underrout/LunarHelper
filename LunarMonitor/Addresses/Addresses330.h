#pragma once
#include <Windows.h>
#include <stdint.h>

constexpr uintptr_t LM_CURR_LEVEL_NUMBER = 0x58C12C;
constexpr uintptr_t LM_CURR_LEVEL_NUMBER_BEING_SAVED = 0x7EF584;
constexpr uintptr_t LM_VERIFICATION_CODE = 0x8F3058;
constexpr uintptr_t LM_COMMAND_WINDOW = 0xDAFFA0;
constexpr uintptr_t LM_ALLOWED_TO_RELOAD_BOOLEAN = 0xDAFF6F;

constexpr uintptr_t LM_CURR_ROM_NAME = 0x5C0030;
constexpr uintptr_t LM_CURR_ROM_PATH = 0x7B5FF8;
constexpr uintptr_t LM_EXE_PATH = 0x592438;
constexpr uintptr_t LM_TOOLBAR_HANDLE = 0xDAFDC8;
constexpr uintptr_t LM_MAIN_EDITOR_WINDOW_HANDLE = 0x8B57F8;
constexpr uintptr_t LM_MAIN_STATUSBAR_HANDLE = 0xDAFDBC;

constexpr size_t COMMENT_FIELD_SFC_ROM_OFFSET = 0x7F120;
constexpr size_t COMMENT_FIELD_SMC_ROM_OFFSET = 0x7F320;

constexpr uintptr_t LM_RENDER_LEVEL_FUNCTION = 0x538876;
using renderLevelFunction = void(*)(DWORD a, DWORD b, DWORD c);

constexpr uintptr_t LM_MAP16_SAVE_FUNCTION = 0x440780;
using saveMap16Function = BOOL(*)();

constexpr uintptr_t LM_LEVEL_SAVE_FUNCTION = 0x46B5F0;
using saveLevelFunction = BOOL(*)(DWORD x);

constexpr uintptr_t LM_OW_SAVE_FUNCTION = 0x509AC0;
using saveOWFunction = BOOL(*)();

constexpr uintptr_t LM_NEW_ROM_FUNCTION = 0x467210;
using newRomFunction = BOOL(*)(DWORD a, DWORD b);

constexpr uintptr_t LM_TITLESCREEN_SAVE_FUNCTION = 0x4A3530;
using saveTitlescreenFunction = BOOL(*)();

constexpr uintptr_t LM_CREDITS_SAVE_FUNCTION = 0x4A3A20;
using saveCreditsFunction = BOOL(*)();

constexpr uintptr_t LM_SHARED_PALETTES_SAVE_FUNCTION = 0x44FD10;
using saveSharedPalettesFunction = BOOL(*)(BOOL x);

constexpr uintptr_t LM_EXPORT_ALL_MAP16_FUNCTION = 0x4CA8C0;
using export_all_map16_function = BOOL(*)(DWORD x, const char* full_output_path);

constexpr uintptr_t LM_COMMENT_FIELD_WRITE_FUNCTION = 0x540720;
using comment_field_write_function = void(*)(uint32_t a, const char* comment, uint32_t b);

template <typename T>
constexpr T AddressToFnPtr(const uintptr_t address) {
	return reinterpret_cast<T>(address);
}
