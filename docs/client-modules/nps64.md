# nps64.dll - unified dossier

**GMS v270**: 339,648 bytes, linked 2021-04-19, 96 imported / 8 exported.
**MSEA**: 339,648 bytes, linked 2021-04-19, 96 imported / 8 exported.
**Status**: identical builds.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role and loading

The version resource identifies this as the Nexon NPS DLL, version `2.3.0.0`. Its exports and strings identify a survey client that can add survey parameters and open an A/W survey window. `MapleStory.exe` statically imports `GetLastErrorCode`, so the DLL is a direct game dependency. The import table establishes only that named API; it does not show which survey functions the client invokes.

### Exports

8 exports.

| Export | Name-based interpretation |
| --- | --- |
| `AddSurveyParameterA` | Add an ANSI survey parameter |
| `AddSurveyParameterW` | Add a wide-character survey parameter |
| `GetLastErrorCode` | Retrieve last error code |
| `GetLastErrorMsg` | Retrieve last error message |
| `OpenSurveyWindowA` | Open survey window with ANSI input |
| `OpenSurveyWindowW` | Open survey window with wide-character input |
| `SetDebugMode` | Set debug mode |
| `SetRunMode` | Set run mode |

### Notable imports

- `WINHTTP.dll`: open/connect/request, send, receive, read, availability, and timeout APIs.
- `USER32.dll` and `SHELL32.dll`: desktop/window interaction, input-idle wait, window geometry, and `ShellExecuteExW`.
- `ole32.dll` and `OLEAUT32.dll`: COM initialization and variant cleanup.
- `KERNEL32.dll`: file, string conversion, and runtime support.

### Embedded endpoints and product strings

- Host strings: `external-api.cem.nexon.com`, `stage-external-api.cem.nexon.com`, and `dev-external-api.cem.nexon.com` (wide strings near PE offsets `0x000418F8`, `0x000419F0`, and `0x00041AE8`). No complete survey URL was identified.
- Related strings include `Survey URL :`, `Survey Param :`, `Survey Window`, and `Survey window popup failed!`.
- `accessKey: %s` and `secretKey: %s` are format placeholders, not literal credentials.
- Version resource identifies CompanyName `Nexon Corporation`, FileDescription/ProductName `Nexon NPS DLL`, and FileVersion/ProductVersion `2.3.0.0` (version string at `0x0004E7E0`).

### RE notes

The WINHTTP imports and survey-specific exports establish survey/network capability. Embedded Nexon hosts are candidate environment endpoints only; the static evidence does not show a request, selected host, or active survey. The game has a direct static import of `GetLastErrorCode`, while the remaining survey exports may be consumed through another path or not used. Sources: `analysis/pe_inventory.json` (`nps64.dll` entry and `MapleStory.exe` imports); `analysis/strings/nps64.strings.txt` at the offsets above.

## GMS v270

Bit-identical to the MSEA build; everything above applies.

## MSEA imports and exports

#### nps64.dll - exports (8)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | AddSurveyParameterA | 7D90 |  |
| 2 | AddSurveyParameterW | 7DD0 |  |
| 3 | GetLastErrorCode | 7B60 |  |
| 4 | GetLastErrorMsg | 7B70 |  |
| 5 | OpenSurveyWindowA | 7B90 |  |
| 6 | OpenSurveyWindowW | 7BD0 |  |
| 7 | SetDebugMode | 7B50 |  |
| 8 | SetRunMode | 7F00 |  |

#### nps64.dll - imports (96 symbols)

- **KERNEL32.dll** (79): `WideCharToMultiByte`, `GetLastError`, `WriteConsoleW`, `HeapSize`, `CreateFileW`, `SetStdHandle`, `MultiByteToWideChar`, `GetVersion`, `HeapReAlloc`, `SetFilePointerEx`, `ReadConsoleW`, `ReadFile`, `GetConsoleMode`, `GetConsoleCP`, `WriteFile`, `FlushFileBuffers`, `GetProcessHeap`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetOEMCP`, `IsValidCodePage`, `FindNextFileA`, `FindFirstFileExA`, `FindClose`, `GetACP`, `CloseHandle`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `IsDebuggerPresent`, `GetStartupInfoW`, `GetModuleHandleW`, `QueryPerformanceCounter`, `GetCurrentProcessId`, `GetCurrentThreadId`, `GetSystemTimeAsFileTime`, `InitializeSListHead`, `EnterCriticalSection`, `LeaveCriticalSection`, `DeleteCriticalSection`, `SetLastError`, `InitializeCriticalSectionAndSpinCount`, `SwitchToThread`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `GetProcAddress`, `EncodePointer`, `DecodePointer`, `LCMapStringW`, `GetLocaleInfoW`, `GetStringTypeW`, `GetCPInfo`, `LocalFree`, `RtlUnwindEx`, `RtlPcToFileHeader`, `RaiseException`, `InterlockedFlushSList`, `FreeLibrary`, `LoadLibraryExW`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameA`, `HeapAlloc`, `HeapFree`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `RtlUnwind`
- **USER32.dll** (4): `GetDesktopWindow`, `WaitForInputIdle`, `GetWindowRect`, `wsprintfW`
- **SHELL32.dll** (1): `ShellExecuteExW`
- **ole32.dll** (2): `CoUninitialize`, `CoInitialize`
- **OLEAUT32.dll** (1): `VariantClear`
- **WINHTTP.dll** (9): `WinHttpConnect`, `WinHttpReadData`, `WinHttpOpenRequest`, `WinHttpCloseHandle`, `WinHttpReceiveResponse`, `WinHttpSendRequest`, `WinHttpQueryDataAvailable`, `WinHttpSetTimeouts`, `WinHttpOpen`

_GMS build bit-identical - the tables above cover both._
