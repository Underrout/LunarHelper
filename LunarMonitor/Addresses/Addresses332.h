#pragma once
#include <Windows.h>
#include <stdint.h>

constexpr uintptr_t LM_CURR_LEVEL_NUMBER = 0x59B3B4;
constexpr uintptr_t LM_CURR_LEVEL_NUMBER_BEING_SAVED = 0x8049E0;

constexpr uintptr_t LM_VERIFICATION_CODE = 0x90C5DC;
constexpr uintptr_t LM_COMMAND_WINDOW = 0xDD2644;
constexpr uintptr_t LM_ALLOWED_TO_RELOAD_BOOLEAN = 0xDD2612;

constexpr uintptr_t LM_CURR_ROM_NAME = 0x5D0800;
constexpr uintptr_t LM_CURR_ROM_PATH = 0x7C7640;
constexpr uintptr_t LM_EXE_PATH = 0x5A1D38;
constexpr uintptr_t LM_TOOLBAR_HANDLE = 0xDD2464;
constexpr uintptr_t LM_MAIN_EDITOR_WINDOW_HANDLE = 0x8CD4F4;
constexpr uintptr_t LM_MAIN_STATUSBAR_HANDLE = 0xDD2458;

constexpr uintptr_t LM_MAP16_SAVE_FUNCTION = 0x446AE0;
using saveMap16Function = BOOL(*)();

constexpr uintptr_t LM_LEVEL_SAVE_FUNCTION = 0x46F6E0;
using saveLevelFunction = BOOL(*)(DWORD x, DWORD y);

constexpr uintptr_t LM_OW_SAVE_FUNCTION = 0x5142D0;
using saveOWFunction = BOOL(*)();

constexpr uintptr_t LM_NEW_ROM_FUNCTION = 0x46AF40;
using newRomFunction = BOOL(*)(DWORD a, DWORD b);

constexpr uintptr_t LM_TITLESCREEN_SAVE_FUNCTION = 0x4AA910;
using saveTitlescreenFunction = BOOL(*)();

constexpr uintptr_t LM_CREDITS_SAVE_FUNCTION = 0x4AAE00;
using saveCreditsFunction = BOOL(*)();

constexpr uintptr_t LM_SHARED_PALETTES_SAVE_FUNCTION = 0x456400;
using saveSharedPalettesFunction = BOOL(*)(BOOL x);

constexpr uintptr_t LM_EXPORT_ALL_MAP16_FUNCTION = 0x4D48C0;
using export_all_map16_function = BOOL(*)(DWORD x, const char* full_output_path);

constexpr uintptr_t LM_COMMENT_FIELD_WRITE_FUNCTION = 0x54A930;
using comment_field_write_function = void(*)(uint32_t a, const char* comment, uint32_t b);

template <typename T>
constexpr T AddressToFnPtr(const uintptr_t address) {
	return reinterpret_cast<T>(address);
}
