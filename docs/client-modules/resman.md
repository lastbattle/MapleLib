# ResMan.dll - unified dossier

**GMS v270**: 736,736 bytes, linked 2026-07-20, 162 imported / 2 exported.
**MSEA**: 996,832 bytes, linked 2026-09-16, 181 imported / 2 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

ResMan is the client resource-manager component. It exposes a `ResMan` class factory, binds to PCOM's object and namespace services at runtime, and combines file-backed resource access with a PE-resource lookup path. The observed names suggest hierarchical language and image-resource namespaces, but they are not enough to claim a particular complete archive format.

### Key mechanisms

- **Component entry point and PCOM integration:** `DllGetClassObject` (`0x1530042E0`) routes the `ResMan` class request to its factory. The initialization/binding path at `0x15300CA80` loads PCOM dynamically and resolves services including `PcCreateObject`, `PcSerializeObject`, `PcSerializeString`, `PcRootNameSpace`, and `PcSetPropertyDelayedLoad`; PCOM is not a static PE import of ResMan.
- **File-backed resource paths:** internal enumeration/open/read paths include `0x1530A2D04`, `0x1530ABF9C`, and `0x1530B3F80`. ResMan imports filesystem enumeration and stream APIs, while its strings include the wide-string path fragments `Language/` and `.img/`. These are evidence for path/namespace-backed resources, not proof of a `.wz` parser.
- **Embedded PE resources:** `0x153008AA0` uses `FindResourceA`, `LoadResource`, and `SizeofResource` to locate and access resources stored in a PE module.
- **Image-object and UOL integration:** strings include `_Canvas` and UOL/cache diagnostics; together with the dynamic PCOM bindings, this supports resource nodes being surfaced through PCOM objects and delayed properties. The precise on-disk encoding for every node type is not established here.

### Exports

| Name | Ordinal | VA |
| --- | ---: | ---: |
| `DllCanUnloadNow` | 1 | `0x153004340` |
| `DllGetClassObject` | 2 | `0x1530042E0` |

### Notable imports by DLL

- `KERNEL32.dll`: `FindFirstFileExW`, `FindNextFileW`, `FindClose`, `CreateFileW`, `ReadFile`, `GetFileSizeEx`, and `SetFilePointerEx` support file-backed lookup and reads; `FindResourceA`, `LoadResource`, and `SizeofResource` support PE resources; `LoadLibraryExA`, `GetProcAddress`, and `FreeLibrary` support dynamic component binding.
- `ole32.dll`: `CoCreateInstance`, `CoInitializeEx`, `CoUninitialize`, and task-memory allocation helpers.
- `OLEAUT32.dll`: `VARIANT`, error-info, and `SAFEARRAY` APIs.
- `ADVAPI32.dll`: registry query/create/update/delete functions.
- `USER32.dll`: `UnregisterClassA`, `wsprintfW`, and `CharNextA`.

### RE notes

- Inventory identity: x64 PE, timestamp `2026-09-16`, image base `0x153000000`.
- The PE export table contains only `DllGetClassObject` and `DllCanUnloadNow`; ResMan's PCOM dependency is resolved dynamically at `0x15300CA80`.
- Supplied strings corroborate `Language/`, `.img/`, `_Canvas`, and UOL-related behavior. No confirmed `.wz` filename/parser path or complete resource-type catalog was established, so format-specific claims should be verified against additional call paths or runtime samples.

## GMS v270 delta

Largest slim-down: COM apartment and SafeArray usage removed.

- +KERNEL32.dll (7): `CloseThreadpoolWork`, `CreateThreadpoolWork`, `InitializeConditionVariable`, `InitializeSRWLock`, `IsThreadAFiber`, `SleepConditionVariableCS`, `SubmitThreadpoolWork`
- +OLEAUT32.dll (1): `SysAllocString`
- -KERNEL32.dll (20): `CreateEventW`, `CreateSemaphoreW`, `CreateThread`, `DeleteFileW`, `ExitThread`, `FormatMessageA`, `FreeLibraryAndExitThread`, `GetExitCodeThread`, `GetLocaleInfoEx`, `GetNativeSystemInfo`, ...(20)
- -OLEAUT32.dll (5): `SafeArrayAccessData`, `SafeArrayDestroy`, `SafeArrayGetLBound`, `SafeArrayGetUBound`, `SafeArrayUnaccessData`
- -ole32.dll (2): `CoInitializeEx`, `CoUninitialize`

## MSEA imports and exports

#### ResMan.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 4340 |  |
| 2 | DllGetClassObject | 42E0 |  |

#### ResMan.dll - imports (181 symbols)

- **KERNEL32.dll** (150): `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `FindResourceA`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `DeleteFileW`, `OutputDebugStringA`, `DeleteCriticalSection`, `CloseHandle`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `SetEvent`, `ReleaseSemaphore`, `WaitForSingleObject`, `CreateEventW`, `WaitForMultipleObjects`, `CreateSemaphoreW`, `LocalFree`, `lstrlenW`, `WriteConsoleW`, `ReadConsoleW`, `ReadFile`, `SetEndOfFile`, `GetConsoleMode`, `InitializeCriticalSectionAndSpinCount`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `OutputDebugStringW`, `Sleep`, `GetConsoleOutputCP`, `WriteFile`, `FlushFileBuffers`, `SetStdHandle`, `CreateFileW`, `GetStringTypeW`, `GetProcessHeap`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `FindNextFileW`, `FindFirstFileExW`, `FindClose`, `WaitForSingleObjectEx`, `SwitchToThread`, `GetCurrentThreadId`, `GetExitCodeThread`, `GetNativeSystemInfo`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `SetFileInformationByHandle`, `GetTempPathW`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableSRW`, `FormatMessageA`, `GetLocaleInfoEx`, `IsDebuggerPresent`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `CreateThread`, `ExitThread`, `ResumeThread`, `FreeLibraryAndExitThread`, `GetModuleHandleExW`, `ExitProcess`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `HeapSize`, `HeapReAlloc`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `GetFileSizeEx`, `SetFilePointerEx`, `RtlUnwind`
- **USER32.dll** (3): `UnregisterClassA`, `wsprintfW`, `CharNextA`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (6): `CoCreateInstance`, `CoTaskMemAlloc`, `CoTaskMemRealloc`, `CoTaskMemFree`, `CoInitializeEx`, `CoUninitialize`
- **OLEAUT32.dll** (14): `VariantChangeType`, `VariantCopy`, `SafeArrayUnaccessData`, `SafeArrayAccessData`, `SafeArrayGetLBound`, `SafeArrayGetUBound`, `SetErrorInfo`, `VariantInit`, `VarUI4FromStr`, `VariantClear`, `CreateErrorInfo`, `SysFreeString`, `GetErrorInfo`, `SafeArrayDestroy`

## GMS v270 imports and exports

#### ResMan.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 4540 |  |
| 2 | DllGetClassObject | 44E0 |  |

#### ResMan.dll - imports (162 symbols)

- **KERNEL32.dll** (137): `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `FindResourceA`, `MultiByteToWideChar`, `DeleteCriticalSection`, `IsDBCSLeadByte`, `LoadLibraryA`, `OutputDebugStringA`, `OutputDebugStringW`, `LocalFree`, `WriteConsoleW`, `CreateFileW`, `CloseHandle`, `ReadConsoleW`, `ReadFile`, `InitializeCriticalSectionAndSpinCount`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `WideCharToMultiByte`, `Sleep`, `GetConsoleMode`, `GetConsoleOutputCP`, `WriteFile`, `FlushFileBuffers`, `SetStdHandle`, `SetFilePointerEx`, `GetFileSizeEx`, `GetStringTypeW`, `GetProcessHeap`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `SetFileInformationByHandle`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `InitializeSRWLock`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `InitializeConditionVariable`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolWork`, `SubmitThreadpoolWork`, `CloseThreadpoolWork`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `IsDebuggerPresent`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `GetCurrentThreadId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `HeapSize`, `HeapReAlloc`, `GetTempPathW`, `IsThreadAFiber`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `IsValidCodePage`, `GetACP`, `GetOEMCP`, `GetCPInfo`, `GetCommandLineA`, `GetCommandLineW`, `RtlUnwind`
- **USER32.dll** (3): `UnregisterClassA`, `CharNextA`, `wsprintfW`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoTaskMemAlloc`, `CoTaskMemRealloc`, `CoTaskMemFree`, `CoCreateInstance`
- **OLEAUT32.dll** (10): `VariantChangeType`, `VariantCopy`, `VariantClear`, `VariantInit`, `SetErrorInfo`, `CreateErrorInfo`, `SysFreeString`, `SysAllocString`, `GetErrorInfo`, `VarUI4FromStr`
