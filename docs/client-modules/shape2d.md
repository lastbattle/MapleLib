# Shape2D.dll - unified dossier

**GMS v270**: 756,192 bytes, linked 2026-07-20, 163 imported / 2 exported.
**MSEA**: 757,216 bytes, linked 2026-09-16, 155 imported / 2 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

This x64 COM-style module exposes geometry-related object classes named `Vector2D`, `Convex2D`, and `PolyShape2D`. Its class factory compares those names and dispatches to class implementations; helper methods return qualified type identifiers such as `Shape2D#Vector2D`. This supports a role as a shape/geometry data-object module. The import inventory contains no graphics API, so the evidence does not support describing it as the sprite renderer itself.

### Key mechanisms

- `0x153405EA0` (`DllGetClassObject`) selects factories for `Vector2D`, `Convex2D`, and `PolyShape2D`, then delegates COM object construction/querying to the shared factory helper.
- Shape type-identifier helpers return the corresponding qualified names: `0x15341AFE0` returns `Shape2D#Vector2D`, `0x153404C40` returns `Shape2D#Convex2D`, and `0x153404E20` returns `Shape2D#PolyShape2D`.

### Exports

| Name | Ordinal | VA |
| --- | ---: | ---: |
| `DllCanUnloadNow` | 1 | `0x153405F50` |
| `DllGetClassObject` | 2 | `0x153405EA0` |

### Notable imports by DLL

- `ole32.dll`: `CoCreateInstance`, `CoTaskMemAlloc`, `CoTaskMemRealloc`, `CoTaskMemFree`.
- `OLEAUT32.dll`: `VariantInit`, `VariantCopy`, `VariantClear`, `VariantChangeType`, and error-info/BSTR helpers.
- `KERNEL32.dll`: file/resource access, dynamic loading, heap, synchronization, and thread-pool helpers.
- `ADVAPI32.dll`: registry query/create/update/delete APIs.
- `USER32.dll`: `UnregisterClassA`, `wsprintfW`, `CharNextA`.

### RE notes

- Inventory identity: x64 PE, timestamp `2026-09-16`, image base `0x153400000`.
- The supplied strings identify `Shape2D#Convex2D`, `Shape2D#PolyShape2D`, `Shape2D#Vector2D`, and their short class names. IDA confirms the class-factory routing and the type-name helpers.
- The two standard COM exports are the only PE exports. The exact geometric operations behind each class remain unnamed in this stripped build; class names establish object categories, not their full algorithms.

## GMS v270 delta

Threadpool family.

- +KERNEL32.dll (10): `CloseThreadpoolWork`, `CreateThreadpoolWork`, `InitializeConditionVariable`, `InitializeSRWLock`, `IsThreadAFiber`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `SubmitThreadpoolWork`, `WakeAllConditionVariable`, `WakeConditionVariable`
- +OLEAUT32.dll (1): `SysAllocString`
- -KERNEL32.dll (3): `FormatMessageA`, `GetLocaleInfoEx`, `TryAcquireSRWLockExclusive`

## MSEA imports and exports

#### Shape2D.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 5F50 |  |
| 2 | DllGetClassObject | 5EA0 |  |

#### Shape2D.dll - imports (155 symbols)

- **KERNEL32.dll** (131): `DeleteCriticalSection`, `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `InitializeCriticalSectionAndSpinCount`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `WriteConsoleW`, `CreateFileW`, `ReadConsoleW`, `ReadFile`, `CloseHandle`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `LocalFree`, `FindResourceA`, `Sleep`, `GetConsoleMode`, `GetConsoleOutputCP`, `WriteFile`, `FlushFileBuffers`, `SetStdHandle`, `GetStringTypeW`, `GetProcessHeap`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `InitOnceBeginInitialize`, `InitOnceComplete`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `GetCurrentThreadId`, `SetFileInformationByHandle`, `GetTempPathW`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `FormatMessageA`, `GetLocaleInfoEx`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlUnwindEx`, `RtlPcToFileHeader`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `HeapSize`, `HeapReAlloc`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `GetFileSizeEx`, `SetFilePointerEx`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `IsValidCodePage`, `GetACP`, `GetOEMCP`, `GetCPInfo`, `RtlUnwind`
- **USER32.dll** (3): `UnregisterClassA`, `wsprintfW`, `CharNextA`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoCreateInstance`, `CoTaskMemRealloc`, `CoTaskMemFree`, `CoTaskMemAlloc`
- **OLEAUT32.dll** (9): `SetErrorInfo`, `VariantChangeType`, `VariantClear`, `VariantInit`, `VariantCopy`, `CreateErrorInfo`, `SysFreeString`, `GetErrorInfo`, `VarUI4FromStr`

## GMS v270 imports and exports

#### Shape2D.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 6200 |  |
| 2 | DllGetClassObject | 6100 |  |

#### Shape2D.dll - imports (163 symbols)

- **KERNEL32.dll** (138): `DeleteCriticalSection`, `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `InitializeCriticalSectionAndSpinCount`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `WriteConsoleW`, `CreateFileW`, `CloseHandle`, `ReadConsoleW`, `ReadFile`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `LocalFree`, `FindResourceA`, `Sleep`, `GetConsoleMode`, `GetConsoleOutputCP`, `WriteFile`, `FlushFileBuffers`, `SetStdHandle`, `SetFilePointerEx`, `GetFileSizeEx`, `GetStringTypeW`, `GetProcessHeap`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `InitOnceBeginInitialize`, `InitOnceComplete`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `SetFileInformationByHandle`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `InitializeSRWLock`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `InitializeConditionVariable`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolWork`, `SubmitThreadpoolWork`, `CloseThreadpoolWork`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `GetCurrentThreadId`, `InitializeSListHead`, `RtlUnwindEx`, `RtlPcToFileHeader`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `HeapSize`, `HeapReAlloc`, `GetTempPathW`, `IsThreadAFiber`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `IsValidCodePage`, `GetACP`, `GetOEMCP`, `GetCPInfo`, `GetCommandLineA`, `GetCommandLineW`, `RtlUnwind`
- **USER32.dll** (3): `wsprintfW`, `CharNextA`, `UnregisterClassA`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoTaskMemAlloc`, `CoTaskMemRealloc`, `CoTaskMemFree`, `CoCreateInstance`
- **OLEAUT32.dll** (10): `SetErrorInfo`, `VariantChangeType`, `VariantClear`, `VariantInit`, `VariantCopy`, `CreateErrorInfo`, `SysFreeString`, `SysAllocString`, `GetErrorInfo`, `VarUI4FromStr`
