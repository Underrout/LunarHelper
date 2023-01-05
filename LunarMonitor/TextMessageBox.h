#pragma once

#include <string>
#include <locale>
#include <codecvt>
#include <Windows.h>
#include <array>
#include <tuple>
#include "Constants.h"

static int(WINAPI* TrueMessageBoxW)(HWND hWnd, LPCTSTR lpText, LPCTSTR lpCaption, UINT uType) = MessageBoxW;
static int(WINAPI* TrueMessageBoxA)(HWND hWnd, LPCSTR lpText, LPCSTR lpCaption, UINT uType) = MessageBoxA;

int WINAPI TextMessageBoxW(HWND hWnd, LPCTSTR lpText, LPCTSTR lpCaption, UINT uType);
int WINAPI TextMessageBoxA(HWND hwnd, LPCSTR lpText, LPCSTR lpCaption, UINT uType);
