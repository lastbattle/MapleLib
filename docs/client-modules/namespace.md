# NameSpace.dll - unified dossier

**GMS v270**: 2,583,520 bytes, linked 2026-07-20, 220 imported / 2 exported.
**MSEA**: 2,693,088 bytes, linked 2026-09-16, 209 imported / 2 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

NameSpace.dll is the client filesystem and package-IO kernel. It resolves names through a virtual
filesystem (`Lambda::FileSystem_CR`, `FolderNode`), opens and decrypts the pack containers under
`Data/Packs` (`Lambda::Package`, `Lambda::PackageHeader`, `Lambda::PackFileStream`), validates file
integrity (`Lambda::FileValidator` with `CreateDigest`/`ValidateFiles`), and feeds the engine
through an asynchronous prefetch cache (`LiveAssist` namespace: `LiveAssistBuffer`,
`LiveAssistFileStream`, `LiveAssistStreamBuf`, `Cache_CR`, plus `LoadDetailInfo`/
`OpenFileDetailInfo`/`UnloadDetailInfo` records). Legacy Wizet `ZFileStream`/`ZLAFileStream` and
`ZException` machinery remains underneath. It reaches the process via COM activation, exporting
only the `DllCanUnloadNow`/`DllGetClassObject` pair.

The full pack/key analysis - reader chain, cipher factories, key schedule, and the embedded
obfuscation table - is in "The pack pipeline" section; the module-level map:

- reader entry `sub_15283B140` / `sub_1528419C0` -> `sub_15283EC80` -> opener `sub_152859450`
  (9391 pseudocode lines; ten `PackageHeader` constructions, four `ChaCha20Cipher` and four
  `SnowCipher` bindings, returns `std::_Ref_count<Lambda::Package>`)
- stream dispatcher `sub_152867A40` -> `sub_152867CD0` -> `CipherHeaderStream<SnowCipher>`
  factory `sub_152868220` and `CipherStream<SnowCipher>` factory `sub_15286B7E0`
- writer side `sub_1529ED220` (`Data/Packs`) and `sub_1529F27D0` (`PKG%04x.tmp`)

### Key mechanisms

1. Package reader/crypto - see "The pack pipeline"; key schedule verified against the opener
   (FNV-1a over UTF-16, obfuscation-table xor, nonce scramble) with no additional embedded secret.
2. `FileValidator::CreateDigest` / `ValidateFiles` - digest generation and validation used for
     file integrity checks (anti-tamper / patch verification support).
3. `LiveAssist` asynchronous IO - refcounted buffer cache with load/open/unload detail records,
     i.e. the background streaming path for game resources.
4. Virtual filesystem - `FileSystem_CR` / `FolderNode` name resolution over packs and plain files,
   with legacy `ZFileStream` fallback.

### Imports (selected)

209 symbols; heavy `KERNEL32` (file, memory-mapped IO, threads), `ole32`/`OLEAUT32` (COM), plus
`USER32`, `ADVAPI32` (registry), `WINMM`, `SHELL32`, `SHLWAPI` (path helpers). Notably no
`BCrypt`/`NCrypt`/`CRYPT32` - the crypto is fully self-contained, matching the hand-rolled
ChaCha/Snow implementations.

### RE notes

- The opener defeats one-shot Hex-Rays summarization (9391 lines); the cipher-relevant windows
  were extracted around the four obfuscation-table xor sites instead.
- `Package file v1.0 Copyright 2002 Wizet, ZMS` has no direct code xref - legacy stamp only.
- `CipherStream<ChaCha20Cipher>` wrapper construction not pinned; RTTI proves the template family,
  and the opener binds raw cipher objects directly.

## GMS v270 delta

Threadpool family; pack crypto unchanged.

- +KERNEL32.dll (12): `CloseThreadpoolWork`, `CreateEventW`, `CreateThreadpoolWork`, `InitializeConditionVariable`, `InitializeSRWLock`, `IsThreadAFiber`, `ResetEvent`, `SetEvent`, `SleepConditionVariableCS`, `SubmitThreadpoolWork`, ...(12)
- -KERNEL32.dll (1): `TryAcquireSRWLockExclusive`

## MSEA imports and exports

#### NameSpace.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | EAA40 |  |
| 2 | DllGetClassObject | EA960 |  |

#### NameSpace.dll - imports (209 symbols)

- **KERNEL32.dll** (168): `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `FindResourceA`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `CreateFileW`, `GetFileSizeEx`, `DeleteCriticalSection`, `CreateFileMappingW`, `MapViewOfFile`, `UnmapViewOfFile`, `GetFileAttributesW`, `SetFilePointer`, `CreateFileMappingA`, `OpenFileMappingA`, `QueryPerformanceCounter`, `GetCurrentProcessId`, `GetCurrentThreadId`, `GetSystemTimeAsFileTime`, `InitializeCriticalSectionAndSpinCount`, `WriteConsoleW`, `SetEndOfFile`, `SetStdHandle`, `FlushFileBuffers`, `ReadConsoleW`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `LocalFree`, `CloseHandle`, `Sleep`, `GetConsoleMode`, `GetConsoleOutputCP`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineA`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `FindFirstFileExW`, `GetTimeZoneInformation`, `SetFilePointerEx`, `GetFileType`, `GetStdHandle`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `QueryPerformanceFrequency`, `FormatMessageA`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `SetFileInformationByHandle`, `GetTempPathW`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `GetLocaleInfoEx`, `LCMapStringEx`, `GetStringTypeW`, `CompareStringEx`, `GetCPInfo`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `WakeAllConditionVariable`, `SleepConditionVariableSRW`, `GetStartupInfoW`, `InitializeSListHead`, `GetProcessHeap`, `HeapDestroy`, `HeapAlloc`, `HeapFree`, `HeapReAlloc`, `HeapSize`, `GetLocalTime`, `GetModuleFileNameW`, `GetFileAttributesExW`, `FileTimeToLocalFileTime`, `FileTimeToSystemTime`, `CreateDirectoryW`, `WriteFile`, `WaitForSingleObject`, `TerminateThread`, `CreateThread`, `FindFirstFileW`, `FindNextFileW`, `FindClose`, `GetFileTime`, `CompareFileTime`, `GetFileSize`, `ReadFile`, `GetCommandLineW`, `GlobalFree`, `GetCurrentDirectoryW`, `SetCurrentDirectoryW`, `InitializeCriticalSection`, `FindResourceExW`, `LockResource`, `FindResourceW`, `GetSystemInfo`, `VirtualLock`, `VirtualUnlock`, `VirtualProtect`, `VirtualFree`, `VirtualAlloc`, `lstrlenA`, `lstrlenW`, `lstrcpyW`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `VirtualQuery`, `ExitProcess`, `GetModuleHandleExW`, `GetCurrentThread`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `RtlUnwind`
- **USER32.dll** (11): `UnregisterClassW`, `IsWindow`, `CharNextA`, `GetWindowTextW`, `FlashWindow`, `BringWindowToTop`, `MessageBoxW`, `wsprintfW`, `UnregisterClassA`, `wsprintfA`, `GetWindowTextLengthW`
- **ADVAPI32.dll** (9): `RegSetValueExA`, `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `SystemFunction036`
- **ole32.dll** (4): `CoTaskMemFree`, `CoTaskMemRealloc`, `CoTaskMemAlloc`, `CoCreateInstance`
- **OLEAUT32.dll** (14): `SysFreeString`, `SafeArrayPutElement`, `SafeArrayCreate`, `SetErrorInfo`, `VarUI4FromStr`, `VariantChangeType`, `VariantCopy`, `VariantClear`, `VariantInit`, `CreateErrorInfo`, `VarUdateFromDate`, `SystemTimeToVariantTime`, `VariantTimeToSystemTime`, `GetErrorInfo`
- **WINMM.dll** (1): `timeGetTime`
- **SHELL32.dll** (1): `CommandLineToArgvW`
- **SHLWAPI.dll** (1): `PathCanonicalizeW`

## GMS v270 imports and exports

#### NameSpace.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | EA3F0 |  |
| 2 | DllGetClassObject | EA2A0 |  |

#### NameSpace.dll - imports (220 symbols)

- **KERNEL32.dll** (179): `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `FindResourceA`, `MultiByteToWideChar`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `DeleteCriticalSection`, `GetFileSizeEx`, `CloseHandle`, `CreateFileMappingW`, `MapViewOfFile`, `UnmapViewOfFile`, `GetFileAttributesW`, `SetFilePointer`, `CreateFileMappingA`, `OpenFileMappingA`, `CreateEventW`, `WaitForSingleObjectEx`, `ResetEvent`, `SetEvent`, `InitializeCriticalSectionAndSpinCount`, `WriteConsoleW`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLastError`, `RaiseException`, `DecodePointer`, `EncodePointer`, `LocalFree`, `CreateFileW`, `Sleep`, `SetEndOfFile`, `SetStdHandle`, `FlushFileBuffers`, `ReadConsoleW`, `SetFilePointerEx`, `GetConsoleMode`, `GetConsoleOutputCP`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineA`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `FindFirstFileExW`, `GetTimeZoneInformation`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `FormatMessageA`, `SetFileInformationByHandle`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `InitializeSRWLock`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `InitializeConditionVariable`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolWork`, `SubmitThreadpoolWork`, `CloseThreadpoolWork`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `LCMapStringEx`, `GetLocaleInfoEx`, `GetStringTypeW`, `CompareStringEx`, `GetCPInfo`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `GetCurrentThreadId`, `InitializeSListHead`, `GetProcessHeap`, `HeapDestroy`, `HeapAlloc`, `HeapFree`, `HeapReAlloc`, `HeapSize`, `GetLocalTime`, `GetModuleFileNameW`, `GetFileAttributesExW`, `FileTimeToLocalFileTime`, `FileTimeToSystemTime`, `CreateDirectoryW`, `WriteFile`, `WaitForSingleObject`, `TerminateThread`, `CreateThread`, `FindFirstFileW`, `FindNextFileW`, `FindClose`, `GetFileTime`, `CompareFileTime`, `GetFileSize`, `ReadFile`, `GetCommandLineW`, `GlobalFree`, `GetCurrentDirectoryW`, `SetCurrentDirectoryW`, `InitializeCriticalSection`, `FindResourceExW`, `LockResource`, `FindResourceW`, `GetSystemInfo`, `VirtualLock`, `VirtualUnlock`, `VirtualProtect`, `VirtualFree`, `VirtualAlloc`, `lstrlenA`, `lstrlenW`, `lstrcpyW`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `VirtualQuery`, `ExitProcess`, `GetModuleHandleExW`, `GetCurrentThread`, `GetTempPathW`, `IsThreadAFiber`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `RtlUnwind`
- **USER32.dll** (11): `FlashWindow`, `BringWindowToTop`, `GetWindowTextLengthW`, `wsprintfA`, `IsWindow`, `UnregisterClassW`, `UnregisterClassA`, `CharNextA`, `wsprintfW`, `GetWindowTextW`, `MessageBoxW`
- **ADVAPI32.dll** (9): `RegSetValueExA`, `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `SystemFunction036`
- **ole32.dll** (4): `CoTaskMemFree`, `CoTaskMemRealloc`, `CoTaskMemAlloc`, `CoCreateInstance`
- **OLEAUT32.dll** (14): `VariantTimeToSystemTime`, `SafeArrayPutElement`, `SafeArrayCreate`, `SetErrorInfo`, `VarUI4FromStr`, `VariantChangeType`, `VariantCopy`, `VariantClear`, `VariantInit`, `CreateErrorInfo`, `SysFreeString`, `GetErrorInfo`, `VarUdateFromDate`, `SystemTimeToVariantTime`
- **WINMM.dll** (1): `timeGetTime`
- **SHELL32.dll** (1): `CommandLineToArgvW`
- **SHLWAPI.dll** (1): `PathCanonicalizeW`
