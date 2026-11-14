# Canvas.dll - unified dossier

**GMS v270**: 2,764,768 bytes, linked 2026-11-09, 191 imported / 2 exported.
**MSEA**: 2,703,840 bytes, linked 2026-09-16, 189 imported / 2 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

This x64 COM-style Nexon module is associated with WZ canvas/media handling. IDA shows ZLZ64 stream filters used for compressed property streams, and a separate WebM path that labels and emits VP8/VP9 video frames. The binary also contains a WebM Project VP9 decoder (version 1.14.1). These are direct signs of asset serialization and video processing; the evidence reviewed here does not establish a general PNG/JPEG decoder or an alpha-compositing routine.

### Key mechanisms

- `0x1501A9650` selects a ZLZ stream filter: a nonzero direction/size parameter creates an inflator, zero creates a deflator, then the stream helper runs and closes the filter.
- `0x1501A9430` wraps an input `ZIStream` with `ZLZCreateInflator`, passes the filtered stream to the internal stream helper, and closes the filter.
- `0x1501A9E50` handles WZ property-stream operations. It reads property keys including `_filepath` and `_inlink`, and has multiple deflator paths for writing content.
- `0x15018C610` builds a `WzLib` WebM media stream, assigns `V_VP8` or `V_VP9` tags, and emits queued frames with timestamps. `0x150039D00` is in the VP9 frame-decode path and handles corrupt frame-header/data failures.

### Exports

| Name | Ordinal | VA |
| --- | ---: | ---: |
| `DllCanUnloadNow` | 1 | `0x15016BEE0` |
| `DllGetClassObject` | 2 | `0x15016BE20` |

### Notable imports by DLL

- `ZLZ64.dll`: `ZLZCreateInflator`, `ZLZCreateDeflator`, `ZLZCloseFilter`.
- `GDI32.dll`: `CreateDIBSection`, `CreateCompatibleDC`, `SelectObject`, `TextOutW`, font and text-metric helpers. These imports indicate bitmap/text support, not by themselves an image decoder.
- `ole32.dll` / `OLEAUT32.dll`: `CoCreateInstance`, task-memory allocation, and `VARIANT`/error-info helpers for COM properties.
- `KERNEL32.dll`: dynamic loading and symbol lookup, file I/O, heap, synchronization, and thread-pool APIs.
- `USER32.dll`: rectangle helpers and window/class/string utilities.
- `ADVAPI32.dll`: registry query/create/update/delete APIs.

### RE notes

- Inventory identity: x64 PE, timestamp `2026-09-16`, image base `0x150000000`.
- The only exports are the standard COM class-factory/unload pair; useful behavior is internal or reached through COM interfaces.
- ZLZ filter use is confirmed by direct IDA callsites, not inferred from imports alone. The supplied strings and IDA pass surfaced VP9 video decode/encode evidence, but no explicit PNG/JPEG/WIC path. Static analysis does not prove that no custom image decoder or compositor exists elsewhere in the module.

## GMS v270 delta

Glyph-metrics text work; CharNextW removed.

- +GDI32.dll (2): `GetCharABCWidthsW`, `GetGlyphIndicesW`
- +KERNEL32.dll (1): `IsThreadAFiber`
- -USER32.dll (1): `CharNextW`

## MSEA imports and exports

#### Canvas.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 16BEE0 |  |
| 2 | DllGetClassObject | 16BE20 |  |

#### Canvas.dll - imports (189 symbols)

- **KERNEL32.dll** (146): `LoadLibraryA`, `SleepConditionVariableCS`, `WakeConditionVariable`, `InitializeCriticalSectionEx`, `InitializeConditionVariable`, `QueryPerformanceFrequency`, `QueryPerformanceCounter`, `SetThreadPriority`, `WaitForSingleObjectEx`, `GetTickCount`, `WakeAllConditionVariable`, `TryEnterCriticalSection`, `WriteConsoleW`, `SetEndOfFile`, `CreateFileW`, `FlushFileBuffers`, `SetStdHandle`, `GetStringTypeW`, `GetProcessHeap`, `SetEnvironmentVariableW`, `IsDBCSLeadByte`, `WideCharToMultiByte`, `MultiByteToWideChar`, `FindResourceA`, `lstrcmpiA`, `SizeofResource`, `LoadResource`, `LoadLibraryExA`, `GetProcAddress`, `GetModuleHandleA`, `GetModuleFileNameA`, `FreeLibrary`, `InitializeCriticalSectionAndSpinCount`, `DeleteCriticalSection`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `LocalFree`, `CloseHandle`, `Sleep`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `FindNextFileW`, `FindFirstFileExW`, `FindClose`, `HeapReAlloc`, `HeapSize`, `GetFileSizeEx`, `GetConsoleOutputCP`, `WriteFile`, `SwitchToThread`, `GetCurrentThreadId`, `GetExitCodeThread`, `GetNativeSystemInfo`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `SetFileInformationByHandle`, `GetTempPathW`, `InitOnceExecuteOnce`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `SleepConditionVariableSRW`, `FormatMessageA`, `GetLocaleInfoEx`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `RtlUnwind`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `ReadFile`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `CreateThread`, `ExitThread`, `ResumeThread`, `FreeLibraryAndExitThread`, `GetModuleHandleExW`, `ExitProcess`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `GetStdHandle`, `GetFileType`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `SetFilePointerEx`, `GetConsoleMode`, `ReadConsoleW`
- **USER32.dll** (6): `CharNextW`, `UnregisterClassA`, `IntersectRect`, `OffsetRect`, `wsprintfW`, `CharNextA`
- **GDI32.dll** (13): `TextOutW`, `GetObjectA`, `CreateDIBSection`, `GetTextMetricsA`, `SetTextAlign`, `SetTextColor`, `SetBkMode`, `SelectObject`, `GetTextExtentPoint32W`, `DeleteObject`, `DeleteDC`, `CreateFontIndirectA`, `CreateCompatibleDC`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoTaskMemAlloc`, `CoTaskMemFree`, `CoTaskMemRealloc`, `CoCreateInstance`
- **OLEAUT32.dll** (9): `CreateErrorInfo`, `GetErrorInfo`, `SysFreeString`, `VariantInit`, `SetErrorInfo`, `VarUI4FromStr`, `VariantChangeType`, `VariantCopy`, `VariantClear`
- **ZLZ64.dll** (3): `?ZLZCreateInflator@@YAPEAUZIStream@@HPEAU1@I@Z`, `?ZLZCreateDeflator@@YAPEAUZIStream@@HPEAU1@H@Z`, `?ZLZCloseFilter@@YAXPEAUZIStream@@@Z`

## GMS v270 imports and exports

#### Canvas.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 16C0B0 |  |
| 2 | DllGetClassObject | 16BFF0 |  |

#### Canvas.dll - imports (191 symbols)

- **KERNEL32.dll** (147): `SleepConditionVariableCS`, `WakeConditionVariable`, `InitializeCriticalSectionEx`, `InitializeConditionVariable`, `QueryPerformanceFrequency`, `QueryPerformanceCounter`, `SetThreadPriority`, `WaitForSingleObjectEx`, `LoadLibraryA`, `WakeAllConditionVariable`, `TryEnterCriticalSection`, `WriteConsoleW`, `SetEndOfFile`, `CreateFileW`, `FlushFileBuffers`, `SetStdHandle`, `GetStringTypeW`, `GetProcessHeap`, `GetTickCount`, `IsDBCSLeadByte`, `WideCharToMultiByte`, `MultiByteToWideChar`, `FindResourceA`, `lstrcmpiA`, `SizeofResource`, `LoadResource`, `LoadLibraryExA`, `GetProcAddress`, `GetModuleHandleA`, `GetModuleFileNameA`, `FreeLibrary`, `DeleteCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `EnterCriticalSection`, `SetLastError`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `LocalFree`, `CloseHandle`, `Sleep`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `FindNextFileW`, `FindFirstFileExW`, `FindClose`, `HeapReAlloc`, `HeapSize`, `GetFileSizeEx`, `GetConsoleOutputCP`, `SwitchToThread`, `GetCurrentThreadId`, `GetExitCodeThread`, `GetNativeSystemInfo`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `SetFileInformationByHandle`, `GetTempPathW`, `InitOnceExecuteOnce`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `SleepConditionVariableSRW`, `FormatMessageA`, `GetLocaleInfoEx`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `RtlUnwind`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `ReadFile`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `CreateThread`, `ExitThread`, `ResumeThread`, `FreeLibraryAndExitThread`, `GetModuleHandleExW`, `ExitProcess`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `GetStdHandle`, `GetFileType`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `IsThreadAFiber`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `SetFilePointerEx`, `GetConsoleMode`, `ReadConsoleW`, `WriteFile`
- **USER32.dll** (5): `UnregisterClassA`, `IntersectRect`, `OffsetRect`, `wsprintfW`, `CharNextA`
- **GDI32.dll** (15): `TextOutW`, `GetObjectA`, `CreateDIBSection`, `GetTextMetricsA`, `SetTextAlign`, `SetTextColor`, `SetBkMode`, `SelectObject`, `GetGlyphIndicesW`, `GetTextExtentPoint32W`, `GetCharABCWidthsW`, `DeleteObject`, `DeleteDC`, `CreateFontIndirectA`, `CreateCompatibleDC`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoTaskMemAlloc`, `CoTaskMemFree`, `CoTaskMemRealloc`, `CoCreateInstance`
- **OLEAUT32.dll** (9): `CreateErrorInfo`, `GetErrorInfo`, `SysFreeString`, `VariantInit`, `SetErrorInfo`, `VarUI4FromStr`, `VariantChangeType`, `VariantCopy`, `VariantClear`
- **ZLZ64.dll** (3): `?ZLZCreateInflator@@YAPEAUZIStream@@HPEAU1@I@Z`, `?ZLZCreateDeflator@@YAPEAUZIStream@@HPEAU1@H@Z`, `?ZLZCloseFilter@@YAXPEAUZIStream@@@Z`
