# Sound.dll - unified dossier

**GMS v270**: 909,792 bytes, linked 2026-07-20, 196 imported / 2 exported.
**MSEA**: 911,840 bytes, linked 2026-09-16, 187 imported / 2 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

This x64 COM-style audio module has `Sound_DX8` and `Sound` classes. IDA shows the `Sound_DX8` initialization path creating a DirectSound 8.1 device interface, setting its cooperative level, creating a sound buffer, and setting the buffer format. Separately, it parses RIFF/WAVE data with WinMM `mmio*` APIs and uses MSACM streams for compressed-audio format negotiation and conversion. The observed output backend is DirectSound, not `waveOut`.

### Key mechanisms

- `0x152818600` performs backend initialization. It scans for `*.acm` drivers, loads each candidate and resolves `DriverProc`, registers/opens ACM drivers, then initializes the DirectSound 8.1 interface, cooperative level, sound buffer, and format.
- `0x1528022B0` parses an in-memory RIFF/WAVE stream using `mmioOpenA`, `mmioDescend`, `mmioRead`, `mmioSeek`, and `mmioAscend`. It validates `RIFF`/`WAVE`, reads the `fmt ` chunk and opens an ACM stream for non-PCM formats.
- `0x152801A50` enumerates ACM drivers for `MPEG Layer-3`, opens a conversion stream, sizes the output, prepares an ACM header, converts with `acmStreamConvert`, and releases the stream/header.
- `0x15282DC80` is a buffered streaming conversion path using `acmStreamSize`, `acmStreamPrepareHeader`, and `acmStreamConvert`.

### Exports

| Name | Ordinal | VA |
| --- | ---: | ---: |
| `DllCanUnloadNow` | 1 | `0x152811A60` |
| `DllGetClassObject` | 2 | `0x1528119D0` |

### Notable imports by DLL

- `WINMM.dll`: `mmioOpenA`, `mmioDescend`, `mmioRead`, `mmioSeek`, `mmioAscend`, `mmioClose`.
- `MSACM32.dll`: `acmDriverEnum`, `acmDriverDetailsA`, `acmFormatEnumA`, `acmFormatSuggest`, `acmStreamOpen`, `acmStreamSize`, `acmStreamPrepareHeader`, `acmStreamConvert`, `acmStreamUnprepareHeader`, `acmStreamClose`.
- `KERNEL32.dll`: `LoadLibraryA`, `GetProcAddress`, file/mapping APIs, heap, and synchronization helpers.
- `ole32.dll` / `OLEAUT32.dll`: `CoCreateInstance`, task-memory, `VARIANT`, and error-info helpers.
- `USER32.dll`: window-class and string-formatting helpers.
- `ADVAPI32.dll`: registry query/create/update/delete APIs.

### RE notes

- Inventory identity: x64 PE, timestamp `2026-09-16`, image base `0x152800000`.
- `DllGetClassObject` routes the `Sound_DX8` and `Sound` class names. `DirectSoundRenderer` is referenced by `0x1528078C0`, and DirectX 8.1 setup diagnostics are referenced by `0x152818600`.
- The `WINMM.dll` import group contains only `mmio*` functions; there are no `waveOut*` or `waveIn*` imports. MSACM conversion is directly evidenced by callsites, including an MPEG Layer-3 enumeration path (`0x1528018F0`).
- These are static findings for the specified build; they do not establish runtime device selection or successful playback on a host.

## GMS v270 delta

Threadpool family; audio backends unchanged.

- +KERNEL32.dll (12): `CloseThreadpoolWork`, `CreateEventW`, `CreateThreadpoolWork`, `InitializeConditionVariable`, `InitializeSRWLock`, `IsThreadAFiber`, `ResetEvent`, `SetEvent`, `SleepConditionVariableCS`, `SubmitThreadpoolWork`, ...(12)
- -KERNEL32.dll (3): `FormatMessageA`, `GetLocaleInfoEx`, `TryAcquireSRWLockExclusive`

## MSEA imports and exports

#### Sound.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 11A60 |  |
| 2 | DllGetClassObject | 119D0 |  |

#### Sound.dll - imports (187 symbols)

- **WINMM.dll** (6): `mmioClose`, `mmioRead`, `mmioSeek`, `mmioOpenA`, `mmioAscend`, `mmioDescend`
- **MSACM32.dll** (14): `acmStreamSize`, `acmStreamConvert`, `acmStreamUnprepareHeader`, `acmStreamClose`, `acmStreamPrepareHeader`, `acmFormatEnumA`, `acmDriverDetailsA`, `acmDriverEnum`, `acmStreamOpen`, `acmDriverID`, `acmDriverAddA`, `acmDriverOpen`, `acmDriverClose`, `acmFormatSuggest`
- **KERNEL32.dll** (142): `GetProcessHeap`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `FindNextFileW`, `FindFirstFileExW`, `FindClose`, `Sleep`, `LocalFree`, `GetExitCodeThread`, `EncodePointer`, `DecodePointer`, `RaiseException`, `GetLastError`, `EnterCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `GetStringTypeW`, `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `FindResourceA`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `MapViewOfFile`, `CreateFileMappingA`, `OpenFileMappingA`, `SetFilePointer`, `GlobalMemoryStatusEx`, `SetFilePointerEx`, `GetFileSizeEx`, `ReadConsoleW`, `GetConsoleMode`, `ReadFile`, `GetFileType`, `SetStdHandle`, `FlushFileBuffers`, `WriteFile`, `GetConsoleOutputCP`, `CreateFileW`, `SetEndOfFile`, `WriteConsoleW`, `DeleteCriticalSection`, `SetThreadpoolTimer`, `GetStdHandle`, `HeapReAlloc`, `HeapSize`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetLocaleInfoW`, `LCMapStringW`, `CompareStringW`, `GetTimeFormatW`, `GetDateFormatW`, `FlsFree`, `FlsSetValue`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `GetCurrentThreadId`, `SetFileInformationByHandle`, `GetTempPathW`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolTimer`, `RtlUnwind`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `FormatMessageA`, `GetLocaleInfoEx`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `WakeAllConditionVariable`, `SleepConditionVariableSRW`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `CloseHandle`, `CreateThread`, `ExitThread`, `ResumeThread`, `FreeLibraryAndExitThread`, `GetModuleHandleExW`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `ExitProcess`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `FlsAlloc`, `FlsGetValue`
- **USER32.dll** (4): `wsprintfW`, `UnregisterClassA`, `wsprintfA`, `CharNextA`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoCreateInstance`, `CoTaskMemFree`, `CoTaskMemAlloc`, `CoTaskMemRealloc`
- **OLEAUT32.dll** (9): `CreateErrorInfo`, `VarUI4FromStr`, `SetErrorInfo`, `SysFreeString`, `VariantInit`, `VariantCopy`, `VariantClear`, `VariantChangeType`, `GetErrorInfo`

## GMS v270 imports and exports

#### Sound.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 11B90 |  |
| 2 | DllGetClassObject | 11AE0 |  |

#### Sound.dll - imports (196 symbols)

- **WINMM.dll** (6): `mmioClose`, `mmioRead`, `mmioSeek`, `mmioOpenA`, `mmioAscend`, `mmioDescend`
- **MSACM32.dll** (14): `acmStreamSize`, `acmStreamConvert`, `acmStreamUnprepareHeader`, `acmStreamClose`, `acmStreamPrepareHeader`, `acmFormatEnumA`, `acmDriverDetailsA`, `acmDriverEnum`, `acmStreamOpen`, `acmDriverID`, `acmDriverAddA`, `acmDriverOpen`, `acmDriverClose`, `acmFormatSuggest`
- **KERNEL32.dll** (151): `GetFileSizeEx`, `GetStringTypeW`, `GetProcessHeap`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `FindNextFileW`, `Sleep`, `LocalFree`, `GetExitCodeThread`, `EncodePointer`, `DecodePointer`, `RaiseException`, `GetLastError`, `EnterCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `SetFilePointerEx`, `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `FindResourceA`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `MapViewOfFile`, `CreateFileMappingA`, `OpenFileMappingA`, `SetFilePointer`, `GlobalMemoryStatusEx`, `FindFirstFileExW`, `FindClose`, `ReadConsoleW`, `GetConsoleMode`, `ReadFile`, `GetFileType`, `SetStdHandle`, `FlushFileBuffers`, `WriteFile`, `GetConsoleOutputCP`, `CreateFileW`, `SetEndOfFile`, `WriteConsoleW`, `DeleteCriticalSection`, `GetTickCount64`, `GetStdHandle`, `HeapReAlloc`, `HeapSize`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetLocaleInfoW`, `LCMapStringW`, `CompareStringW`, `GetTimeFormatW`, `GetDateFormatW`, `IsThreadAFiber`, `GetTempPathW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `SetFileInformationByHandle`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `InitializeSRWLock`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `InitializeConditionVariable`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `RtlUnwind`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolWork`, `SubmitThreadpoolWork`, `CloseThreadpoolWork`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `CloseHandle`, `SetEvent`, `ResetEvent`, `WaitForSingleObjectEx`, `CreateEventW`, `GetStartupInfoW`, `GetCurrentProcessId`, `GetCurrentThreadId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `CreateThread`, `ExitThread`, `ResumeThread`, `FreeLibraryAndExitThread`, `GetModuleHandleExW`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `ExitProcess`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`
- **USER32.dll** (4): `wsprintfW`, `UnregisterClassA`, `wsprintfA`, `CharNextA`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoCreateInstance`, `CoTaskMemFree`, `CoTaskMemAlloc`, `CoTaskMemRealloc`
- **OLEAUT32.dll** (9): `CreateErrorInfo`, `VarUI4FromStr`, `SetErrorInfo`, `SysFreeString`, `VariantInit`, `VariantCopy`, `VariantClear`, `VariantChangeType`, `GetErrorInfo`
