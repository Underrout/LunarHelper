#include <Windows.h>
#include <detours.h>
#include <CommCtrl.h>
#pragma comment (lib, "comctl32")

#include <iostream>
#include "OnLevelSave.h"
#include "OnMap16Save.h"
#include "OnGlobalDataSave.h"
#include "OnSharedPalettesSave.h"

#include "Constants.h"

#include "LM.h"
#include "resource.h"
#include "Config.h"

#include "BuildResultUpdater.h"

__declspec(dllexport) void __cdecl dummy(void);

constexpr const WORD IDM_EXPORT_ALL_BTN = 0x5BF9;

constexpr const size_t MAIN_EDITOR_STATUS_BAR_PARTS = 2;
constexpr const size_t SECOND_STATUSBAR_FIELD_WIDTH = 800;

constexpr const char* CONFIG_FILE_PATH = "lunar-monitor-config.txt";

std::optional<Config> config = std::nullopt;
LM lm{};

HMODULE g_hModule;

HWND gLmHandle;
DWORD verificationCode;

bool statusbar_added = false;
bool export_all_button_added = false;

std::optional<std::string> lastRomBuildTime = std::nullopt;

HANDLE lunarHelperDirChangeWaiter;
HANDLE lunarHelperDirChange;

BOOL WINAPI InitFunction(HWND hWnd, int nCmdShow);

#if LM_VERSION >= 331
    BOOL SaveLevelFunction(DWORD x, DWORD y);
#else
    BOOL SaveLevelFunction(DWORD x);
#endif
BOOL SaveMap16Function();
BOOL SaveOWFunction();
BOOL NewRomFunction(DWORD a, DWORD b);
BOOL SaveCreditsFunction();
BOOL SaveTitlescreenFunction();
BOOL SaveSharedPalettesFunction(BOOL x);

void ShowVolatileResourceExportError();

void WriteCommentFieldFunction(uint32_t a, const char* comment, uint32_t b);

auto LMRenderLevelFunction = AddressToFnPtr<renderLevelFunction>(LM_RENDER_LEVEL_FUNCTION);

auto LMSaveLevelFunction = AddressToFnPtr<saveLevelFunction>(LM_LEVEL_SAVE_FUNCTION);
auto LMSaveMap16Function = AddressToFnPtr<saveMap16Function>(LM_MAP16_SAVE_FUNCTION);
auto LMSaveOWFunction = AddressToFnPtr<saveOWFunction>(LM_OW_SAVE_FUNCTION);
auto LMNewRomFunction = AddressToFnPtr<newRomFunction>(LM_NEW_ROM_FUNCTION);
auto LMSaveCreditsFunction = AddressToFnPtr<saveCreditsFunction>(LM_CREDITS_SAVE_FUNCTION);
auto LMSaveTitlescreenFunction = AddressToFnPtr<saveTitlescreenFunction>(LM_TITLESCREEN_SAVE_FUNCTION);
auto LMSaveSharedPalettesFunction = AddressToFnPtr<saveSharedPalettesFunction>(LM_SHARED_PALETTES_SAVE_FUNCTION);
auto LMWritecommentFunction = AddressToFnPtr<comment_field_write_function>(LM_COMMENT_FIELD_WRITE_FUNCTION);

HWND mainEditorProc;

static BOOL(WINAPI* TrueShowWindow)(HWND hWnd, int nCmdShow) = ShowWindow;

void DllAttach(HMODULE hModule);
void DllDetach(HMODULE hModule);

void SetConfig(const fs::path& basePath);

void AddExportAllButton(HMODULE hModule);
void UpdateExportAllButton();
void AddStatusBarField();

void WatchLunarHelperDirectory();
void CALLBACK OnLunarHelperDirChange(_In_  PVOID unused, _In_  BOOLEAN TimerOrWaitFired);

bool CommentFieldIsAltered();

bool ExportAll(bool confirm_prompt);

LRESULT CALLBACK MainEditorReplacementWndProc(
    HWND hwnd,        // handle to window
    UINT uMsg,        // message identifier
    WPARAM wParam,    // first message parameter
    LPARAM lParam);    // second message parameter;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            DllAttach(hModule);
            break;
        case DLL_THREAD_ATTACH:
            break;
        case DLL_PROCESS_DETACH:
            DllDetach(hModule);
            break;
        case DLL_THREAD_DETACH:
            break;
    }
    return TRUE;
}

void DllAttach(HMODULE hModule)
{
    g_hModule = hModule;

    DisableThreadLibraryCalls(hModule);
    DetourTransactionBegin();
    DetourUpdateThread(GetCurrentThread());
    DetourAttach(&(PVOID&)TrueShowWindow, InitFunction);
    DetourTransactionCommit();
}

void DllDetach(HMODULE hModule)
{
    DetourTransactionBegin();
    DetourUpdateThread(GetCurrentThread());
    DetourDetach(&(PVOID&)LMSaveLevelFunction, SaveLevelFunction);
    DetourDetach(&(PVOID&)LMSaveMap16Function, SaveMap16Function);
    DetourDetach(&(PVOID&)LMSaveOWFunction, SaveOWFunction);
    DetourDetach(&(PVOID&)LMNewRomFunction, NewRomFunction);
    DetourDetach(&(PVOID&)LMSaveCreditsFunction, SaveCreditsFunction);
    DetourDetach(&(PVOID&)LMSaveTitlescreenFunction, SaveTitlescreenFunction);
    DetourDetach(&(PVOID&)LMSaveSharedPalettesFunction, SaveSharedPalettesFunction);
    DetourDetach(&(PVOID&)LMWritecommentFunction, WriteCommentFieldFunction);
    DetourTransactionCommit();

    if (lunarHelperDirChangeWaiter != nullptr)
        UnregisterWait(lunarHelperDirChangeWaiter);
    if (lunarHelperDirChange != nullptr)
        FindCloseChangeNotification(lunarHelperDirChange);
}

void __cdecl dummy(void)
{
}

BOOL WINAPI InitFunction(HWND hWnd, int nCmdShow)
{
    HWND main_window_hwnd = *lm.getPaths().getMainEditorWindowHandle();

    if (main_window_hwnd != nullptr && hWnd == main_window_hwnd)
    {
        DetourTransactionBegin();
        DetourUpdateThread(GetCurrentThread());
        DetourDetach(&(PVOID&)TrueShowWindow, InitFunction);

        DetourAttach(&(PVOID&)LMSaveLevelFunction, SaveLevelFunction);
        DetourAttach(&(PVOID&)LMSaveMap16Function, SaveMap16Function);
        DetourAttach(&(PVOID&)LMSaveOWFunction, SaveOWFunction);
        DetourAttach(&(PVOID&)LMNewRomFunction, NewRomFunction);
        DetourAttach(&(PVOID&)LMSaveCreditsFunction, SaveCreditsFunction);
        DetourAttach(&(PVOID&)LMSaveTitlescreenFunction, SaveTitlescreenFunction);
        DetourAttach(&(PVOID&)LMSaveSharedPalettesFunction, SaveSharedPalettesFunction);
        DetourAttach(&(PVOID&)LMWritecommentFunction, WriteCommentFieldFunction);
        DetourTransactionCommit();

        AddStatusBarField();

        AddExportAllButton(g_hModule);
    }

    return TrueShowWindow(hWnd, nCmdShow);
}

bool CommentFieldIsAltered()
{
    std::ifstream rom;
    try {
        auto rom_path = lm.getPaths().getRomPath();
        rom.open(rom_path, std::ifstream::binary);

        // assumes that if something isn't .smc, it's not headered
        const size_t comment_pos = rom_path.extension() == ".smc" ? COMMENT_FIELD_SMC_ROM_OFFSET : COMMENT_FIELD_SFC_ROM_OFFSET;

        char comment[0x21];

        rom.seekg(comment_pos);
        rom.read(comment, 0x20);
        comment[0x20] = '\0';

        rom.close();

        return strcmp(FISH, comment) != 0;
    }
    catch (std::exception)
    {
        if (rom.is_open())
        {
            rom.close();
        }
        // if we can't read from the ROM, return true, if the ROM is missing or broken we 
        // have bigger issues than preserving its integrity via the comment field
        return true;
    }
}

LRESULT CALLBACK MainEditorReplacementWndProc(
    HWND hwnd,        // handle to window
    UINT uMsg,        // message identifier
    WPARAM wParam,    // first message parameter
    LPARAM lParam)    // second message parameter
{
    if (uMsg == WM_COMMAND && wParam == IDM_EXPORT_ALL_BTN) {
        // export all button pressed, export all and then mark the ROM as having last been 
        // edited by a lunar monitor injected lunar magic, meaning there should now be no resources
        // in the ROM that are not exported
        bool res = ExportAll(true);
        if (res)
        {
            lm.WriteCommentToRom(FISH_REPLACEMENT);
        }
        else
        {
            if (!CommentFieldIsAltered())
                ShowVolatileResourceExportError();
        }
    }

    return CallWindowProc((WNDPROC)mainEditorProc, *lm.getPaths().getMainEditorWindowHandle(), uMsg, wParam, lParam);
}

void ShowVolatileResourceExportError()
{
    Logger::log_error(L"Failed to export at least one potentially volatile resource, notifying user");
    MessageBox(
        *lm.getPaths().getMainEditorWindowHandle(),
        (LPCWSTR)L"Failed to export at least one potentially volatile resource, check \"lunar-monitor-log.txt\" for details.\n"
        "It is HIGHLY recommended that you create a backup of your ROM and do not "
        "attempt to build with Lunar Helper until you have successfully exported your resources!",
        (LPCWSTR)L"Lunar Monitor: Failed to Export Volatile Resources",
        MB_ICONERROR
    );
}

bool ExportAll(bool confirm_prompt = false)
{
    if (confirm_prompt)
    {
        int msgboxID = MessageBox(
            *lm.getPaths().getMainEditorWindowHandle(),
            (LPCWSTR)L"Are you sure you want to export all modified levels, map16, global data and shared palettes from the ROM?",
            (LPCWSTR)L"Lunar Monitor: Export All",
            MB_ICONWARNING | MB_YESNO | MB_DEFBUTTON1
        );

        switch (msgboxID)
        {
        case IDYES:
            break;
        case IDNO:
            return false;
        }
    }

    Logger::log_message(L"Export all button pressed, attempting to export all now");

    try {
        OnGlobalDataSave::exportBps(lm, config.value());
    }
    catch (const std::exception& exc)
    {
        WhatWide what{ exc };
        Logger::log_error(L"Full export failed: Global data export failed with exception: \"%s\"", what.what());

        return false;
    }

    try {
        fs::path mwlPath = config.value().getLevelDirectory();
        const fs::path origPath = fs::path(mwlPath);
        mwlPath /= "level";
        fs::path romPath = lm.getPaths().getRomDir();
        romPath += lm.getPaths().getRomName();
        lm.getLevelEditor().exportAllMwls(lm.getPaths().getLmExePath(), romPath, mwlPath);

        Logger::log_message(L"Successfully exported all mwls to \"%s\"", mwlPath.c_str());
    }
    catch (const std::exception& exc)
    {
        WhatWide what{ exc };
        Logger::log_error(L"Full export failed: Export of all mwls failed with exception: \"%s\"", what.what());

        return false;
    }

    if (!OnMap16Save::onSuccessfulMap16Save(lm, config.value()))
    {
        Logger::log_error(L"Full export failed: Map16 export failed, check log for details");
        return false;
    }

    try {
        fs::path romPath = lm.getPaths().getRomDir();
        romPath += lm.getPaths().getRomName();

        OnSharedPalettesSave::exportSharedPalettes(romPath, config.value().getSharedPalettesPath(), lm.getPaths().getLmExePath());

        Logger::log_message(L"Successfully exported shared palettes to \"%s\"", config.value().getSharedPalettesPath().c_str());
    }
    catch (const std::runtime_error& err)
    {
        WhatWide what{ err };
        Logger::log_error(L"Full export failed: Shared palettes export failed with exception: \"%s\"", what.what());

        return false;
    }

    Logger::log_message(L"Successfully exported all!");

    if (CommentFieldIsAltered())
    {
        MessageBox(
            *lm.getPaths().getMainEditorWindowHandle(),
            (LPCWSTR)L"Successfuly exported all resources for Lunar Helper!\n(Hint: "
            "Using Export All is generally only necessary when you are explicitly prompted to do so, "
            "Lunar Monitor automatically exports resources when you save them and knows "
            "to prompt you if there may be unexported resources left over in the ROM!)",
            (LPCWSTR)L"Lunar Monitor: Successfully Exported All",
            MB_ICONINFORMATION
        );
    }
    else
    {
        // user just exported volatile resources! congratulate them!!
        MessageBox(
            *lm.getPaths().getMainEditorWindowHandle(),
            (LPCWSTR)L"Successfuly exported all resources for Lunar Helper.\n"
            "You can now safely build your ROM with Lunar Helper!",
            (LPCWSTR)L"Lunar Monitor: Successfully Exported All",
            MB_ICONINFORMATION
        );
    }

    return true;
}

void AddStatusBarField()
{
    int parts[MAIN_EDITOR_STATUS_BAR_PARTS + 1];
    SendMessage(*lm.getPaths().getMainEditorStatusbarHandle(), SB_GETPARTS, MAIN_EDITOR_STATUS_BAR_PARTS, (LPARAM)&parts);

    parts[MAIN_EDITOR_STATUS_BAR_PARTS] = -1;
    parts[MAIN_EDITOR_STATUS_BAR_PARTS - 1] = SECOND_STATUSBAR_FIELD_WIDTH;

    SendMessage(*lm.getPaths().getMainEditorStatusbarHandle(), SB_SETPARTS, MAIN_EDITOR_STATUS_BAR_PARTS + 1, (LPARAM)&parts);
}

void AddExportAllButton(HMODULE hModule)
{
    HWND toolbarHandle = *(lm.getPaths().getToolbarHandle());

    DWORD backgroundColor = GetSysColor(COLOR_BTNFACE);
    COLORMAP colorMap;
    colorMap.from = RGB(0xFF, 0xFF, 0xFF);
    colorMap.to = backgroundColor;
    HBITMAP hbm = CreateMappedBitmap(hModule, IDB_BITMAP1, 0, &colorMap, 1);
    TBADDBITMAP tb;
    tb.hInst = NULL;
    tb.nID = (UINT_PTR)hbm;

    int index = SendMessage(toolbarHandle, TB_ADDBITMAP, 0, (LPARAM)&tb);

    const DWORD buttonStyles = TBSTYLE_AUTOSIZE;

    TBBUTTON button = { index, IDM_EXPORT_ALL_BTN, (BYTE)(config.has_value() ? TBSTATE_ENABLED : TBSTATE_INDETERMINATE), 
        buttonStyles, { 0 }, 0, (INT_PTR)L"\"Export All\": Export map16, all levels, global data and shared palettes for Lunar Helper"};

    TBBUTTON sep = { I_IMAGENONE, 0, 0, BTNS_SEP, { 0 }, 0, 0 };

    SendMessage(toolbarHandle, TB_SETMAXTEXTROWS, 0, 0);
    SendMessage(toolbarHandle, TB_INSERTBUTTON, 5, (LPARAM)&button);
    SendMessage(toolbarHandle, TB_INSERTBUTTON, 5, (LPARAM)&sep);
    SendMessage(toolbarHandle, TB_AUTOSIZE, 0, 0);

    mainEditorProc = (HWND)SetWindowLong(*(lm.getPaths().getMainEditorWindowHandle()), GWL_WNDPROC, (LONG)MainEditorReplacementWndProc);
}

void PromptUserToExportUnexportedResources()
{
    MessageBox(
        *lm.getPaths().getMainEditorWindowHandle(),
        (LPCWSTR)L"There may be unexported resources in this ROM.\nIt is recommended that you "
        "export these resources by pressing the \"Export All\" button in the toolbar before "
        "attempting to build with Lunar Helper.",
        (LPCWSTR)L"Lunar Monitor: Volatile Resources",
        MB_ICONWARNING
    );
}

void WriteCommentFieldFunction(uint32_t write_location, const char* comment, uint32_t comment_length)
{
    if (config.has_value() && strcmp(comment, FISH) == 0 && fs::exists(lm.getPaths().getRomPath()))
    {
        if (CommentFieldIsAltered())
        {
            // comment field is still altered, so there should be nothing in the ROM that's unexported,
            // just keep the comment field altered and return
            LMWritecommentFunction(write_location, FISH_REPLACEMENT, comment_length);
            return;
        }
        else
        {
            // comment field is lunar magic's default, meaning the last lunar magic that has saved to this
            // ROM was not injected with lunar monitor, there could be unexported resources in the ROM, 
            // so let's remind the user to export all, to be safe
            Logger::log_message(L"Potential volatile resources in ROM, notifying user");
            PromptUserToExportUnexportedResources();
            // would love to offer them a yes/no prompt for exporting right now, but sadly lunar magic has a 
            // lock on the rom file during most functions I have hooked, so the export would just fail :/
            // maybe in the future!
        }
    }

    LMWritecommentFunction(write_location, comment, comment_length);
}

void UpdateExportAllButton()
{
    SendMessage(*(lm.getPaths().getToolbarHandle()), TB_INDETERMINATE, IDM_EXPORT_ALL_BTN, (LPARAM) MAKELONG(!config.has_value(), 0));
    SendMessage(*(lm.getPaths().getToolbarHandle()), TB_ENABLEBUTTON, IDM_EXPORT_ALL_BTN, (LPARAM)MAKELONG(config.has_value(), 0));
}

void WatchLunarHelperDirectory()
{
    if (lunarHelperDirChangeWaiter != nullptr)
    {
        UnregisterWait(lunarHelperDirChangeWaiter);
        lunarHelperDirChangeWaiter = nullptr;
    }
    if (lunarHelperDirChange != nullptr)
        FindCloseChangeNotification(lunarHelperDirChange);

    fs::path lunarHelperDir = lm.getPaths().getRomDir();
    lunarHelperDir += ".lunar_helper";

    if (!fs::exists(lunarHelperDir) || !fs::is_directory(lunarHelperDir))
    {
        fs::create_directory(lunarHelperDir);
        SetFileAttributes(lunarHelperDir.c_str(), FILE_ATTRIBUTE_HIDDEN);
    }
    else
    {
        std::optional<json> buildReport = BuildResultUpdater::readInJson();

        if (buildReport.has_value())
        {
            try
            {
                lastRomBuildTime = buildReport.value()["build_time"].dump();
            }
            catch (const json::exception&)
            {
                lastRomBuildTime = std::nullopt;
            }
        }
        else
        {
            lastRomBuildTime = std::nullopt;
        }
    }

    lunarHelperDirChange = FindFirstChangeNotification(lunarHelperDir.c_str(), false, FILE_NOTIFY_CHANGE_LAST_WRITE);

    if (lunarHelperDirChange != INVALID_HANDLE_VALUE)
    {
        RegisterWaitForSingleObject(&lunarHelperDirChangeWaiter, lunarHelperDirChange, &OnLunarHelperDirChange, NULL, INFINITE, WT_EXECUTEONLYONCE);
    }
}

void CALLBACK OnLunarHelperDirChange(_In_  PVOID unused, _In_  BOOLEAN TimerOrWaitFired)
{
    std::optional<json> buildReport = BuildResultUpdater::readInJson();

    UnregisterWait(lunarHelperDirChangeWaiter);
    lunarHelperDirChangeWaiter = nullptr;

    if (buildReport.has_value())
    {
        try
        {
            std::string newHash = buildReport.value()["build_time"].dump();

            if (!lastRomBuildTime.has_value() || newHash != lastRomBuildTime.value())
            {
                FindCloseChangeNotification(lunarHelperDirChange);
                lunarHelperDirChange = nullptr;
                lastRomBuildTime = newHash;
                Logger::log_message(L"Change in Lunar Helper directory detected, reloading ROM...");
                lm.getLevelEditor().reloadROM(gLmHandle);
                return;
            }
        }
        catch (const json::exception&)
        {
            // pass
        }
    }

    RegisterWaitForSingleObject(&lunarHelperDirChangeWaiter, lunarHelperDirChange, &OnLunarHelperDirChange, NULL, INFINITE, WT_EXECUTEONLYONCE);

    FindNextChangeNotification(lunarHelperDirChange);
}

BOOL NewRomFunction(DWORD a, DWORD b)
{
    SetConfig(lm.getPaths().getRomDir());

    Logger::log_message(L"Attempting to switch to new ROM");

    BOOL result = LMNewRomFunction(a, b);

    if (result) 
    {
        fs::path romPath = lm.getPaths().getRomDir();
        romPath += lm.getPaths().getRomName();

        Logger::log_message(L"Successfully loaded ROM: \"%s\"", romPath.c_str());

        fs::current_path(lm.getPaths().getRomDir());
        SetConfig(lm.getPaths().getRomDir());

        UpdateExportAllButton();

        if (config.has_value())
        {
            WatchLunarHelperDirectory();

            if (fs::exists(romPath) && !CommentFieldIsAltered())
            {
                Logger::log_message(L"Potential volatile resources in ROM, notifying user");
                MessageBox(
                    *lm.getPaths().getMainEditorWindowHandle(),
                    (LPCWSTR)L"There may be unexported resources in the ROM you are opening.\nIt is recommended that you "
                    "export these resources by pressing the \"Export All\" button in the toolbar before "
                    "attempting to build with Lunar Helper.",
                    (LPCWSTR)L"Lunar Monitor: Volatile Resources",
                    MB_ICONWARNING
                );
            }
        }
    }
    else
    {
        Logger::log_message(L"Failed to switch to new ROM");
    }

    return result;
}

void SetConfig(const fs::path& basePath) 
{
    fs::path configPath = basePath;
    configPath += CONFIG_FILE_PATH;

    try
    {
        config = Config(configPath);

        Logger::log_message(L"------- START OF LOG -------");
        Logger::log_message(L"Successfully loaded config file from \"%s\"", configPath.wstring().c_str());
    }
    catch (const std::runtime_error& err)
    {
        if (!fs::exists(configPath))
        {
            Logger::setLogLevel(LogLevel::Silent);
        }
        else
        {
            Logger::setDefaultLogLevel();
        }

        Logger::setDefaultLogPath(basePath);

        WhatWide what{ err };
        Logger::log_message(L"------- START OF LOG -------");
        Logger::log_error(L"Failed to setup configuration file, error was \"%s\"", what.what());
        config = std::nullopt;
    }
    catch (const std::exception& exc) 
    {
        if (!fs::exists(configPath))
        {
            Logger::setLogLevel(LogLevel::Silent);
        }
        else
        {
            Logger::setDefaultLogLevel();
        }

        Logger::setDefaultLogPath(basePath);

        WhatWide what{ exc };
        Logger::log_message(L"------- START OF LOG -------");
        Logger::log_error(L"Uncaught exception while reading config file, error was \"%s\"", what.what());
        config = std::nullopt;
    }
}
#if LM_VERSION >= 331
BOOL SaveLevelFunction(DWORD x, DWORD y)
#else
BOOL SaveLevelFunction(DWORD x)
#endif
{
#if LM_VERSION >= 331
    BOOL succeeded = LMSaveLevelFunction(x, y);
#else
    BOOL succeeded = LMSaveLevelFunction(x);
#endif

    OnLevelSave::onLevelSave(succeeded, lm.getLevelEditor().getLevelNumberBeingSaved(), lm, config);

    return succeeded;
}

BOOL SaveMap16Function()
{
#if  LM_VERSION >= 331
    __asm {
        mov eax,ebx
    }
#else
    __asm {
        push ebp
        mov ebp,edi
        mov eax,edi
    }
#endif

    BOOL succeeded = LMSaveMap16Function();

#if LM_VERSION < 331
    __asm {
        pop ebp
    }
#endif

    OnMap16Save::onMap16Save(succeeded, lm, config);

    return succeeded;
}

BOOL SaveOWFunction()
{
#if LM_VERSION >= 331
    __asm {
        mov eax,[ebp]
    }
#endif
    BOOL succeeded = LMSaveOWFunction();

    OnGlobalDataSave::onGlobalDataSave(succeeded, lm, config);

    return succeeded;
}

BOOL SaveTitlescreenFunction()
{
#if LM_VERSION >= 331
    __asm {
        mov eax,edi
    }
#else
    __asm {
        mov eax,[ebp]
    }
#endif
    BOOL succeeded = LMSaveTitlescreenFunction();

    OnGlobalDataSave::onGlobalDataSave(succeeded, lm, config);

    return succeeded;
}

BOOL SaveCreditsFunction()
{
    BOOL succeeded = LMSaveCreditsFunction();

    OnGlobalDataSave::onGlobalDataSave(succeeded, lm, config);

    return succeeded;
}

BOOL SaveSharedPalettesFunction(BOOL x)
{
    BOOL succeeded;

#if LM_VERSION >= 331
    __asm {
        mov eax, esi
        push x
        call LMSaveSharedPalettesFunction
        mov succeeded, eax
    }
#else
    __asm {
        push x
        mov eax,esi
        call LMSaveSharedPalettesFunction
        mov succeeded,eax
    }
#endif

    OnSharedPalettesSave::onSharedPalettesSave(succeeded, lm, config);

    return succeeded;
}
