#include "Logger.h"
#define TIMED_LOGGER_IMPL 0

#include "Paths.h"
#include <Windows.h>
#include <CommCtrl.h>
#pragma comment (lib, "comctl32")

#include <cstdarg>
#include <vector>
#include <string>
#include <sstream>
#include <fstream>
#include <chrono>
#include <memory>
#if TIMED_LOGGER_IMPL
#include <thread>
#include <mutex>
#endif
#include <Windows.h>

constexpr const wchar_t* MODULE_NAME = L"lunar-monitor.dll";
constexpr const char* DEFAULT_LOG_FILE = "lunar_monitor_log.txt";

// A simple class to format a CharT buffer with a va_list, it uses RAII to dispose of its storage
// Doesn't require manual memory management
template <typename CharT, typename = std::enable_if_t<std::is_same_v<CharT, char> || std::is_same_v<CharT, wchar_t>>>
class FormatTempBuffer {
	CharT* m_buffer = nullptr;
public:
	FormatTempBuffer(const CharT* fmt, va_list argptr) noexcept {
		if constexpr (std::is_same_v<CharT, wchar_t>) {
			auto ret = _vscwprintf_l(fmt, nullptr, argptr);
			m_buffer = new CharT[ret + 1];
			vswprintf(m_buffer, ret + 1, fmt, argptr);
		}
		else {
			auto ret = _vscprintf_l(fmt, nullptr, argptr);
			m_buffer = new CharT[ret + 1];
			vsprintf(m_buffer, ret + 1, fmt, argptr);
		}
	}
	CharT* buffer() noexcept {
		return m_buffer;
	}

	~FormatTempBuffer() noexcept {
		delete[] m_buffer;
	}
};

WhatWide::WhatWide(const std::exception& exc) noexcept {
	const char* what = exc.what();
	size_t len = strlen(what);
	m_what = new wchar_t[len + 1];
	mbstowcs_s(nullptr, m_what, len + 1, what, len);
}

const wchar_t* WhatWide::what() noexcept
{
	return m_what;
}

WhatWide::~WhatWide() noexcept
{
	delete[] m_what;
}


// Function to use to log errors when the logger can't be used (e.g. during the Loggers' construction or destruction)
// Use sparingly and with caution
#ifdef _DEBUG
void EmergencyLogToFile(const wchar_t* fmt, ...) noexcept {
	va_list lst{};
	va_start(lst, fmt);
	std::wofstream emergency{};
	emergency.open("emergency_log.txt", std::wofstream::out | std::wofstream::trunc);
	FormatTempBuffer buf{ fmt, lst };
	emergency << buf.buffer() << L'\n';
	emergency.close();
	va_end(lst);
}
#endif

namespace Logger {
#ifdef _DEBUG
	class TheLoggerConsole {
		constexpr static auto inline C_RED = FOREGROUND_RED | FOREGROUND_INTENSITY;
		constexpr static auto inline C_YELLOW = FOREGROUND_RED | FOREGROUND_GREEN;
		constexpr static auto inline C_GREEN = FOREGROUND_GREEN;
		constexpr static auto inline C_WHITE = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE;
		HANDLE m_output;
	public:

		TheLoggerConsole() noexcept {
			if (!AllocConsole()) {
				// if we can't allocate the console we can't use the log file because this is called in its constructor
				// therefore the file handle is not guaranteed to exist and we'd invoke infinite recursion
				// use the emergency file
				auto lastError = GetLastError();
				auto hresult = HRESULT_FROM_WIN32(lastError);
				EmergencyLogToFile(L"Impossible to create console, error %X, HRESULT: %X", lastError, hresult);
			}
			m_output = GetStdHandle(STD_OUTPUT_HANDLE);
		}
		HANDLE getConsoleHandle() noexcept {
			return m_output;
		}
		void WriteToConsole(LogSeverity severity, const wchar_t* msg, size_t length) const noexcept {
			switch (severity) {
			case LogSeverity::Message:
				SetConsoleTextAttribute(m_output, C_GREEN);
				break;
			case LogSeverity::Warning:
				SetConsoleTextAttribute(m_output, C_YELLOW);
				break;
			case LogSeverity::Error:
				SetConsoleTextAttribute(m_output, C_RED);
				break;
			default:
				break;
			}
			BOOL res = WriteConsole(m_output, msg, length + 1, nullptr, nullptr);
			if (!res) {
				log_error(L"Impossible to write to console, error %X", GetLastError());
			}
			SetConsoleTextAttribute(m_output, C_WHITE);
		}

		~TheLoggerConsole() noexcept {
			if (!FreeConsole()) {
				// if we can't free the console we can't use the log file because this is called in its destructor
				// therefore the file handle is not guaranteed to exist and we'd invoke undefined behavior and/or crashes
				// use the emergency file
				auto lastError = GetLastError();
				auto hresult = HRESULT_FROM_WIN32(lastError);
				EmergencyLogToFile(L"Impossible to free console, error %X, HRESULT: %X", lastError, hresult);
			}
		}
	};
#endif

	// This is a very simple buffered thread-safe¹ logger
	// This logger doesn't keep a file open, instead, it stores all messages sent to it into a buffer (vector)
	// After the buffer passes a certain threshold or a timer is elapsed depending on the implementation chosen, 
	// the logger opens a file, *appending* all messages to it, and then closes the file.
	// Flushing the buffers to the file also clears it.
	// On destruction it logs any leftover message that was still in the buffer.

	// To get flush-on-timer-elapsed behavior change the value of the define at the top of the file to 1
	// The logger on a timer may be preferable but it requires using a thread and a lock to make sure we don't cause data races

	// When the _DEBUG define exists, the logger also opens a Console window where it prints every message without buffering.
	// ¹: This class is only thread-safe when the flush-on-timer-elapsed behavior is chosen

	class TheLogger {
	private:
#ifdef _DEBUG
		TheLoggerConsole m_debug_console{};
#endif
		mutable bool m_has_logged_once = false;
		LogLevel m_level;
		mutable std::wofstream m_file;
		fs::path m_filepath;
		mutable std::vector<std::wstring> m_logs;
#if TIMED_LOGGER_IMPL
		std::thread m_thread;
		mutable std::mutex m_logs_mutex{};
		static constexpr long long s_seconds = 10;
#else
		static constexpr size_t s_threshold = 10;
#endif
		void open_log_file() const noexcept {
			m_file.open(m_filepath, std::wofstream::out | std::wofstream::app);
		}

		void close_log_file() const noexcept {
			m_file.close();
		}

		void flush_logs_to_file() const noexcept {
#if TIMED_LOGGER_IMPL
			std::lock_guard lock{ m_logs_mutex };
			if (m_logs.empty() || !m_has_logged_once)
				return;
#endif
			open_log_file();
			for (auto& log : m_logs)
				m_file << log;
			close_log_file();
			m_logs.clear();
		}

	public:
		TheLogger() noexcept : m_level(LogLevel::Log), m_filepath(DEFAULT_LOG_FILE), m_file() {
#if TIMED_LOGGER_IMPL
			// Flush messages to file every 10 seconds
			auto thread_executor = [this]() {
				while (true) {
					if (!m_logs.empty()) {
						flush_logs_to_file();
					}
					std::this_thread::sleep_for(std::chrono::seconds(s_seconds));
				}
			};
			m_thread = std::thread{ thread_executor };
#endif
		}

		void log(LogSeverity severity, const wchar_t* fmt, va_list argptr) const noexcept {
			switch (m_level) {
			case LogLevel::Warn:
				if (severity != LogSeverity::Message) {
					FormatTempBuffer msgbuf{ fmt, argptr };
					MessageBoxW(NULL, msgbuf.buffer(), L"Lunar Monitor Warning", MB_OK | MB_ICONWARNING | MB_APPLMODAL);
				}
				[[fallthrough]];
			case LogLevel::Log:
			{
				m_has_logged_once = true;

				FormatTempBuffer buf{ fmt, argptr };
				auto t_c = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
				std::wstringstream stream{};
				switch (severity) {
				case LogSeverity::Message:
					stream << L"[Message] - ";
					break;
				case LogSeverity::Warning:
					stream << L"[Warning] - ";
					break;
				case LogSeverity::Error:
					stream << L"[Error] - ";
					break;
				default:
					stream << L"[Unspecified] - ";
					break;
				}
				struct tm time_now {};
				auto err = localtime_s(&time_now, &t_c);
				if (err == 0)
					stream << std::put_time(&time_now, L"[ %F %T ] - ") << buf.buffer() << L'\n';
#if TIMED_LOGGER_IMPL
				std::lock_guard lock{ m_logs_mutex };
#endif
				const std::wstring s = stream.str();
				const wchar_t* w = s.c_str();
				SendMessage((HWND)*((HWND*)LM_MAIN_STATUSBAR_HANDLE), SB_SETTEXT, MAKEWORD(2, 0), (LPARAM)w);
				m_logs.push_back(stream.str());
#ifdef _DEBUG
				m_debug_console.WriteToConsole(severity, m_logs.back().c_str(), m_logs.back().size());
#endif
#if not TIMED_LOGGER_IMPL
				if (m_logs.size() == s_threshold)
					flush_logs_to_file();
#endif
			}
				break;
			default:
				break;
			}
		}

		void setLogLevel(LogLevel level) noexcept {
			m_level = level;
		}

		void setDefaultLogLevel() noexcept {
			m_level = LogLevel::Log;
		}

		void setLogPath(fs::path&& path) noexcept {
			if (m_has_logged_once)
				flush_logs_to_file();

			m_has_logged_once = false;

			m_filepath = std::move(path);
		}

		void setDefaultLogPath(const fs::path& prefix) noexcept {
			if (m_has_logged_once)
				flush_logs_to_file();

			m_has_logged_once = false;

			fs::path logPath = prefix;
			logPath /= DEFAULT_LOG_FILE;

			m_filepath = logPath;
		}

		LogLevel getLogLevel() const noexcept {
			return m_level;
		}

		const fs::path& getLogPath() const noexcept {
			return m_filepath;
		}

		~TheLogger() noexcept {
			if (!m_has_logged_once)
				return;
			if (m_level != LogLevel::Silent)
				flush_logs_to_file();
		}
	};

	static const std::unique_ptr<TheLogger>& getLoggerInstance() noexcept
	{
		static auto sLoggerInstance = std::make_unique<TheLogger>();
		return sLoggerInstance;
	}

	LogLevel getLogLevel() noexcept
	{
		return getLoggerInstance()->getLogLevel();
	}

	void setLogLevel(LogLevel level) noexcept {
		getLoggerInstance()->setLogLevel(level);
	}

	void setDefaultLogLevel() noexcept {
		getLoggerInstance()->setDefaultLogLevel();
	}

	void log(LogSeverity severity, const wchar_t* fmt, ...) noexcept {
		va_list lst{};
		va_start(lst, fmt);
		getLoggerInstance()->log(severity, fmt, lst);
		va_end(lst);
	}

	void log_message(const wchar_t* fmt, ...) noexcept {
		va_list lst{};
		va_start(lst, fmt);
		getLoggerInstance()->log(LogSeverity::Message, fmt, lst);
		va_end(lst);
	}
	void log_warning(const wchar_t* fmt, ...) noexcept {
		va_list lst{};
		va_start(lst, fmt);
		getLoggerInstance()->log(LogSeverity::Warning, fmt, lst);
		va_end(lst);
	}
	void log_error(const wchar_t* fmt, ...) noexcept {
		va_list lst{};
		va_start(lst, fmt);
		getLoggerInstance()->log(LogSeverity::Error, fmt, lst);
		va_end(lst);
	}

	void setLogPath(fs::path path) noexcept {
		getLoggerInstance()->setLogPath(std::forward<fs::path>(path));
	}

	void setDefaultLogPath(const fs::path& prefix) noexcept {
		getLoggerInstance()->setDefaultLogPath(prefix);
	}

	const fs::path& getLogPath() noexcept {
		return getLoggerInstance()->getLogPath();
	}
}
