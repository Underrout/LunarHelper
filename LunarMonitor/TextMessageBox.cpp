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
	{MB_OK, L"o"},
	{MB_OKCANCEL, L"co"},
	{MB_RETRYCANCEL, L"cr"},
	{MB_YESNO, L"ny"},
	{MB_YESNOCANCEL, L"cyn"},
}};

constexpr std::array<std::tuple<TCHAR, UINT, const wchar_t*>, 4> KEY_TO_RES {{
	{L'o', IDOK, L"ok"},
	{L'c', IDCANCEL, L"cancel"},
	{L'y', IDYES, L"yes"},
	{L'n', IDNO, L"no"}
}};

int PromptUser(HANDLE console_out, std::wstring acceptable_keys, const wchar_t* prompt)
{
	DWORD read, written;

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

	UINT chosen_response = 0;
	const wchar_t* chosen_response_text = L"";

	TCHAR c = L' ';

	if (show_prompts)
	{

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
	}
	else
	{
		c = acceptable_keys.at(0);
	}

	for (const auto& tup : KEY_TO_RES)
	{
		if (std::get<TCHAR>(tup) == c)
		{
			chosen_response = std::get<UINT>(tup);
			chosen_response_text = std::get<const wchar_t*>(tup);
		}
	}

	WriteConsole(
		console_out,
		L"Choice: ",
		wcslen(L"Choice: "),
		&written,
		NULL
	);

	WriteConsole(
		console_out,
		chosen_response_text,
		wcslen(chosen_response_text),
		&written,
		NULL
	);

	WriteConsole(
		console_out,
		L"\n\n",
		wcslen(L"\n\n"),
		&written,
		NULL
	);

	return chosen_response;
}



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
		prompt = L"(o)k/(c)ancel? (default: cancel)";
		break;

	case MB_RETRYCANCEL:
		prompt = L"(r)etry/(c)ancel? (default: cancel)";
		break;

	case MB_YESNO:
		prompt = L"(y)es/(n)o? (default: no)";
		break;

	case MB_YESNOCANCEL:
		prompt = L"(y)es/(n)o/(c)ancel? (default: cancel)";
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

	return PromptUser(console_out, acceptable_keys, prompt);
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
