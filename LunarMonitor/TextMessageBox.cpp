#include "TextMessageBox.h"

int WINAPI TextMessageBoxA(HWND hwnd, LPCSTR lpText, LPCSTR lpCaption, UINT uType)
{
	std::string text{ lpText };
	std::wstring wText{ text.begin(), text.end() };

	std::string cap{ lpCaption };
	std::wstring wCap{ cap.begin(), cap.end() };

	return TextMessageBoxW(hwnd, wText.c_str(), wCap.c_str(), uType);
}

constexpr std::array<std::tuple<UINT, const wchar_t*>, 5> AVAILABLE_KEYS {{
	{MB_OK, L"O"},
	{MB_OKCANCEL, L"OC"},
	{MB_RETRYCANCEL, L"RC"},
	{MB_YESNO, L"YN"},
	{MB_YESNOCANCEL, L"YNC"},
}};

constexpr std::array<std::tuple<TCHAR, UINT>, 4> KEY_TO_RES {{
	{L'O', IDOK},
	{L'C', IDCANCEL},
	{L'Y', IDYES},
	{L'N', IDNO}
}};

int GetResponse(HANDLE console_out, UINT uType)
{
	const wchar_t* prompt = L"";
	UINT buttons = uType & 0xF;
	DWORD written;

	switch (buttons)
	{
	case MB_OK:
		WriteConsole(
			console_out,
			L"\n\n",
			wcslen(L"\n\n"),
			&written,
			NULL
		);

		return IDOK;

	case MB_OKCANCEL:
		prompt = L"(O)k/(C)ancel?";
		break;

	case MB_RETRYCANCEL:
		prompt = L"(R)etry/(C)ancel?";
		break;

	case MB_YESNO:
		prompt = L"(Y)es/(N)o?";
		break;

	case MB_YESNOCANCEL:
		prompt = L"(Y)es/(N)o/(C)ancel?";
		break;
	}

	std::wstring acceptable_keys;

	for (const auto& tup : AVAILABLE_KEYS)
	{
		if (std::get<UINT>(tup) == buttons)
		{
			acceptable_keys = std::get<const wchar_t*>(tup);
		}
	}

	DWORD read;

	HANDLE console_in = GetStdHandle(STD_INPUT_HANDLE);

	WriteConsole(
		console_out,
		L"\n",
		wcslen(L"\n"),
		&written,
		NULL
	);

	WriteConsole(
		console_out,
		prompt,
		wcslen(prompt),
		&written,
		NULL
	);

	WriteConsole(
		console_out,
		L"\n",
		wcslen(L"\n"),
		&written,
		NULL
	);

	TCHAR c = L' ';

	while (acceptable_keys.find(c) == std::wstring::npos)
	{
		const auto res = ReadConsole(
			console_in,
			(LPVOID)&c,
			1,
			&read,
			NULL
		);
	}

	WriteConsole(
		console_out,
		L"\n",
		wcslen(L"\n"),
		&written,
		NULL
	);

	for (const auto& tup : KEY_TO_RES)
	{
		if (std::get<TCHAR>(tup) == c)
		{
			return std::get<UINT>(tup);
		}
	}
}

int WINAPI TextMessageBoxW(HWND hWnd, LPCTSTR lpText, LPCTSTR lpCaption, UINT uType)
{
	HANDLE console_out = GetStdHandle(STD_OUTPUT_HANDLE);

	DWORD written;

	WriteConsole(
		console_out,
		L"Lunar Magic Error: ",
		wcslen(L"Lunar Magic Error: "),
		&written,
		NULL
	);

	WriteConsole(
		console_out,
		lpCaption,
		wcslen(lpCaption),
		&written,
		NULL
	);

	WriteConsole(
		console_out,
		L" - ",
		wcslen(L" - "),
		&written,
		NULL
	);

	WriteConsole(
		console_out,
		lpText,
		wcslen(lpText),
		&written,
		NULL
	);

	return GetResponse(console_out, uType);
}
