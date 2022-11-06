#pragma once
#include <filesystem>

namespace fs = std::filesystem;

enum class LogLevel {
	Silent,
	Log,
	Warn
};

enum class LogSeverity {
	Message,
	Warning,
	Error
};

// The logger uses wide char for everything, exceptions what()'s don't.
// To fix this issue we provide this WhatWide wrapper that just takes any exception and copies it's what() argument
// into a wide char buffer, resources are managed automatically using RAII
class WhatWide {
	wchar_t* m_what = nullptr;
public:
	WhatWide(const std::exception& exc) noexcept;
	const wchar_t* what() noexcept;
	~WhatWide() noexcept;
};

namespace Logger {
	LogLevel getLogLevel() noexcept;
	void setLogLevel(LogLevel) noexcept;
	void setDefaultLogLevel() noexcept;
	void setLogPath(fs::path path) noexcept;
	void setDefaultLogPath(const fs::path& prefix) noexcept;
	const fs::path& getLogPath() noexcept;
	void log(LogSeverity, const wchar_t* fmt, ...) noexcept;
	void log_message(const wchar_t* fmt, ...) noexcept;
	void log_warning(const wchar_t* fmt, ...) noexcept;
	void log_error(const wchar_t* fmt, ...) noexcept;
}