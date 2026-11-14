# PCOM.dll - unified dossier

**GMS v270**: 954,336 bytes, linked 2026-07-20, 186 imported / 16 exported.
**MSEA**: 1,128,416 bytes, linked 2026-09-16, 176 imported / 16 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

PCOM is the shared component-object and persistence framework used by the client DLLs. Its exports create objects by component name, serialize object/property streams, expose a root namespace, and support clone/save and delayed-load operations. The observed activation path is export-based: component DLLs provide a class factory, while PCOM handles module loading, factory lookup, and the requested interface identifier.

### Key mechanisms

- **Module activation and interface selection:** `PcCreateObject` (`0x152C13EE0`) hashes and checks its object cache, loads the needed module when absent, resolves `DllGetClassObject` (and tracks `DllCanUnloadNow`), requests the standard `IID_IClassFactory` at `0x152CDEF58`, and calls `CreateInstance` with the caller's requested IID. `PcInitModule` (`0x152CCE220`) records the module directory used by this framework; `PcTermModule` (`0x152CCE5F0`) and `PcFreeUnusedLibraries` (`0x152CCE720`) provide lifecycle cleanup.
- **PCOM's built-in classes:** its own `DllGetClassObject` (`0x152C0A760`) dispatches textual class names including `Property`, `List`, `UOL`, `RawData`, and `Unknown` to the corresponding object factory. This is the in-process class table; external modules plug into the same exported factory contract rather than requiring a central list of every class.
- **Object/property serialization:** `PcSerializeObject` (`0x152C1B8F0`) parses a serialized object/property stream and creates nested objects as it encounters component/type names. `PcSerializeString` (`0x152C16B00`) handles the string/property side of that representation. Observed qualified names include `Shape2D#Vector2D`, `Shape2D#Convex2D`, and `Sound_DX8`.
- **Namespaces, delayed loading, and persistence:** `PcRootNameSpace` (`0x152C16C90`) supplies the root namespace; `PcSetPropertyDelayedLoad` (`0x152C2E150`) registers delayed property loading. `PcMakeClone`, `PcMakeFileClone`, `PcSaveFile`, and `PcMakeObject` expose the associated clone, save, and object-construction paths.

### Exports

| Name | Ordinal | VA |
| --- | ---: | ---: |
| `DllCanUnloadNow` | 1 | `0x152C0A840` |
| `DllGetClassObject` | 2 | `0x152C0A760` |
| `OpenExternal` | 3 | `0x152C1CF40` |
| `PcCreateObject` | 4 | `0x152C13EE0` |
| `PcFreeUnusedLibraries` | 5 | `0x152CCE720` |
| `PcInitModule` | 6 | `0x152CCE220` |
| `PcMakeClone` | 7 | `0x152C1CF00` |
| `PcMakeFileClone` | 8 | `0x152C1CEC0` |
| `PcMakeObject` | 9 | `0x152C1DEB0` |
| `PcPassPackIgnoreProp` | 10 | `0x152C16DA0` |
| `PcRootNameSpace` | 11 | `0x152C16C90` |
| `PcSaveFile` | 12 | `0x152C1CF60` |
| `PcSerializeObject` | 13 | `0x152C1B8F0` |
| `PcSerializeString` | 14 | `0x152C16B00` |
| `PcSetPropertyDelayedLoad` | 15 | `0x152C2E150` |
| `PcTermModule` | 16 | `0x152CCE5F0` |

### Notable imports by DLL

- `KERNEL32.dll`: `LoadLibraryA`, `LoadLibraryExA`, `GetProcAddress`, `GetModuleHandleA`, and `FreeLibrary` support module discovery and dispatch; file, PE-resource, heap, and synchronization APIs support storage and object loading.
- `ole32.dll`: `CoCreateInstance` and `CoTaskMemAlloc`/`CoTaskMemRealloc`/`CoTaskMemFree` provide COM activation and task-memory operations.
- `OLEAUT32.dll`: `VariantCopy`, `VariantCopyInd`, `VariantChangeType`, `VariantClear`, `VariantInit`, BSTR cleanup, and error-info APIs support automation values and error reporting.
- `ADVAPI32.dll`: registry query/create/update/delete functions.
- `USER32.dll`: `wsprintfW`, `UnregisterClassA`, and `CharNextA`.

### RE notes

- Inventory identity: x64 PE, timestamp `2026-09-16`, image base `0x152C00000`.
- The activation evidence establishes the plugin contract: PCOM loads a module, asks its `DllGetClassObject` for an `IClassFactory`, then forwards the requested IID to `CreateInstance`. `ResMan.dll` has the expected `DllGetClassObject`/`DllCanUnloadNow` exports and dynamically binds PCOM's service exports.
- The private interface IID set was not resolved. The framework's data includes the standard class-factory IID, but this dossier does not assign names to the other IID constants.
- Strings and factory routing support the listed object/type names; they do not by themselves establish every serialized field or the full on-disk container grammar.

## GMS v270 delta

Threadpool/condvar/SRW adoption; FindFirstFileW and GetLocaleInfoEx removed.

- +KERNEL32.dll (12): `CloseThreadpoolWork`, `CreateEventW`, `CreateThreadpoolWork`, `InitializeConditionVariable`, `InitializeSRWLock`, `IsThreadAFiber`, `ResetEvent`, `SetEvent`, `SleepConditionVariableCS`, `SubmitThreadpoolWork`, ...(12)
- +OLEAUT32.dll (1): `SysAllocString`
- -KERNEL32.dll (3): `FindFirstFileW`, `GetLocaleInfoEx`, `TryAcquireSRWLockExclusive`

## MSEA imports and exports

#### PCOM.dll - exports (16)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | A840 |  |
| 2 | DllGetClassObject | A760 |  |
| 3 | OpenExternal | 1CF40 |  |
| 4 | PcCreateObject | 13EE0 |  |
| 5 | PcFreeUnusedLibraries | CE720 |  |
| 6 | PcInitModule | CE220 |  |
| 7 | PcMakeClone | 1CF00 |  |
| 8 | PcMakeFileClone | 1CEC0 |  |
| 9 | PcMakeObject | 1DEB0 |  |
| 10 | PcPassPackIgnoreProp | 16DA0 |  |
| 11 | PcRootNameSpace | 16C90 |  |
| 12 | PcSaveFile | 1CF60 |  |
| 13 | PcSerializeObject | 1B8F0 |  |
| 14 | PcSerializeString | 16B00 |  |
| 15 | PcSetPropertyDelayedLoad | 2E150 |  |
| 16 | PcTermModule | CE5F0 |  |

#### PCOM.dll - imports (176 symbols)

- **KERNEL32.dll** (151): `DeleteCriticalSection`, `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `InitializeCriticalSectionAndSpinCount`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `GetLocalTime`, `WriteConsoleW`, `SetEndOfFile`, `GetConsoleOutputCP`, `WriteFile`, `FlushFileBuffers`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `LocalFree`, `FindResourceA`, `Sleep`, `SetStdHandle`, `GetProcessHeap`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `SetFilePointerEx`, `GetFileSizeEx`, `GetStringTypeW`, `ReadConsoleW`, `FormatMessageA`, `GetLocaleInfoEx`, `SetCurrentDirectoryW`, `GetCurrentDirectoryW`, `CreateDirectoryW`, `CreateFileW`, `FindClose`, `FindFirstFileW`, `FindFirstFileExW`, `FindNextFileW`, `GetDiskFreeSpaceExW`, `GetFileAttributesW`, `GetFileAttributesExW`, `GetFileInformationByHandle`, `GetFinalPathNameByHandleW`, `GetFullPathNameW`, `SetFileAttributesW`, `SetFileInformationByHandle`, `SetFileTime`, `GetTempPathW`, `AreFileApisANSI`, `CloseHandle`, `DeviceIoControl`, `GetModuleHandleW`, `CreateDirectoryExW`, `CopyFileW`, `MoveFileExW`, `CreateHardLinkW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `GetCurrentThreadId`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `WakeAllConditionVariable`, `SleepConditionVariableSRW`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `GetCPInfo`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `HeapSize`, `HeapReAlloc`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `ReadFile`, `GetConsoleMode`, `RtlUnwind`
- **USER32.dll** (3): `wsprintfW`, `UnregisterClassA`, `CharNextA`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoCreateInstance`, `CoTaskMemAlloc`, `CoTaskMemFree`, `CoTaskMemRealloc`
- **OLEAUT32.dll** (10): `CreateErrorInfo`, `VariantCopy`, `VariantChangeType`, `VariantClear`, `VariantCopyInd`, `SetErrorInfo`, `VarUI4FromStr`, `SysFreeString`, `GetErrorInfo`, `VariantInit`

## GMS v270 imports and exports

#### PCOM.dll - exports (16)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | AB10 |  |
| 2 | DllGetClassObject | A9E0 |  |
| 3 | OpenExternal | 1BE00 |  |
| 4 | PcCreateObject | 12E20 |  |
| 5 | PcFreeUnusedLibraries | A8F50 |  |
| 6 | PcInitModule | A89E0 |  |
| 7 | PcMakeClone | 1BDC0 |  |
| 8 | PcMakeFileClone | 1BD80 |  |
| 9 | PcMakeObject | 1D070 |  |
| 10 | PcPassPackIgnoreProp | 15DA0 |  |
| 11 | PcRootNameSpace | 15C80 |  |
| 12 | PcSaveFile | 1BE20 |  |
| 13 | PcSerializeObject | 1A6B0 |  |
| 14 | PcSerializeString | 15AF0 |  |
| 15 | PcSetPropertyDelayedLoad | 31270 |  |
| 16 | PcTermModule | A8E00 |  |

#### PCOM.dll - imports (186 symbols)

- **KERNEL32.dll** (160): `DeleteCriticalSection`, `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `InitializeCriticalSectionAndSpinCount`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `GetLocalTime`, `WriteConsoleW`, `GetConsoleOutputCP`, `WriteFile`, `FlushFileBuffers`, `SetStdHandle`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `LocalFree`, `FindResourceA`, `Sleep`, `GetFileSizeEx`, `GetProcessHeap`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `GetStringTypeW`, `ReadConsoleW`, `GetConsoleMode`, `ReadFile`, `FormatMessageA`, `SetCurrentDirectoryW`, `GetCurrentDirectoryW`, `CreateDirectoryW`, `CreateFileW`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `GetDiskFreeSpaceExW`, `GetFileAttributesW`, `GetFileAttributesExW`, `GetFileInformationByHandle`, `GetFinalPathNameByHandleW`, `GetFullPathNameW`, `SetEndOfFile`, `SetFileAttributesW`, `SetFileInformationByHandle`, `SetFilePointerEx`, `SetFileTime`, `GetTempPathW`, `AreFileApisANSI`, `CloseHandle`, `DeviceIoControl`, `CreateDirectoryExW`, `CopyFileW`, `MoveFileExW`, `CreateHardLinkW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `InitializeSRWLock`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `InitializeConditionVariable`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolWork`, `SubmitThreadpoolWork`, `CloseThreadpoolWork`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `SetEvent`, `ResetEvent`, `WaitForSingleObjectEx`, `CreateEventW`, `GetStartupInfoW`, `GetCurrentProcessId`, `GetCurrentThreadId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `GetSystemInfo`, `VirtualAlloc`, `VirtualProtect`, `VirtualQuery`, `GetCPInfo`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `HeapSize`, `HeapReAlloc`, `IsThreadAFiber`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `RtlUnwind`
- **USER32.dll** (3): `UnregisterClassA`, `wsprintfW`, `CharNextA`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (4): `CoCreateInstance`, `CoTaskMemAlloc`, `CoTaskMemFree`, `CoTaskMemRealloc`
- **OLEAUT32.dll** (11): `CreateErrorInfo`, `VariantCopy`, `VariantChangeType`, `VariantClear`, `VariantInit`, `VariantCopyInd`, `VarUI4FromStr`, `SysFreeString`, `SysAllocString`, `GetErrorInfo`, `SetErrorInfo`
