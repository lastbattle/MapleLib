# gamescale.nm64.dll - unified dossier

**GMS v270**: 468,960 bytes, linked 2025-08-18, 108 imported / 10 exported.
**MSEA**: not shipped in the audited MSEA corpus.
**Status**: GMS-only.

## MSEA build

Not shipped in the audited MSEA corpus - GMS-only module.

## GMS v270 dossier

### Role

This is a small x64 companion (468,960 bytes) whose import surface and embedded strings point to Nexon's legacy messenger/auth client, not graphics scaling or anti-cheat. It imports three `NMCO_*` functions from `nmcogame64.dll`; its strings name Nexon passport, login/logout, player-info, and auth-closed-callback wrappers, as well as a NexonMessenger log location. The large GameScale SDK contains the literal module name `gamescale.nm64`, which is consistent with a dynamically connected adapter. The PE inventory shows no static import edge to this DLL, so the precise loading caller and runtime sequence remain unproven.

### Export contract

Target: `<client-root>\gamescale.nm64.dll`.

The export directory has ten ordinal-only entries: the import/export table contains no names for them. All use image base `0x180000000`.

| Ordinal | RVA | VA |
|---:|---:|---:|
| 1 | `0x19F40` | `0x180019F40` |
| 2 | `0x1A2A0` | `0x18001A2A0` |
| 3 | `0x1A840` | `0x18001A840` |
| 4 | `0x1B390` | `0x18001B390` |
| 5 | `0x1B730` | `0x18001B730` |
| 6 | `0x1F210` | `0x18001F210` |
| 7 | `0x21B60` | `0x180021B60` |
| 8 | `0x21FE0` | `0x180021FE0` |
| 9 | `0x22AC0` | `0x180022AC0` |
| 10 | `0x25270` | `0x180025270` |

The imports and string/type-name evidence describe the likely adapter contract, but do not map individual ordinals to individual operations:

- Dispatch into the legacy client via `NMCO_CallNMFunction`.
- Release returned data via `NMCO_MemoryFree`.
- Set locale/region via `NMCO_SetLocaleAndRegion`.
- Embedded wrapper type names: `CNMGetMyInfoFunc`, `CNMGetNexonPassportFunc`, `CNMLoginAuthFunc`, `CNMLogoutAuthFunc`, and `CNMSetAuthClosedCallbackFunc`.

These indicate player-info/passport retrieval, login/logout, and auth-close notification capabilities. Exact calling convention, argument/return layouts, and ordinal-to-wrapper mapping are unresolved from the supplied tables and strings.

### Imports and embedded strings

- 108 ordinary imports total: 104 from `KERNEL32.dll`, one from `USER32.dll` (`UnregisterClassW`), and three from `nmcogame64.dll` (the `NMCO_*` functions above). No delay imports are recorded.
- Embedded strings include `gamescale.nm64.dll`, `gamescale.nm.log`, `%USERPROFILE%\AppData\LocalLow\GameScale\NexonMessenger`, `GAMESCALE_LOGGER_LEVEL`, and `Global\LoggerMutex_`.
- The extracted strings do not show a standard FileDescription, ProductName, or ProductVersion record; this is a statement about the supplied strings corpus, not a complete resource audit.

### RE notes

- The likely shim role is supported by the import dependency and wrapper names, while the module-to-ordinal ABI is not symbolically named. Do not infer which ordinal implements which wrapper without xrefs or disassembly.
- `MapleStory.exe` directly imports `gamescale64.dll`, not this companion. The big SDK's `gamescale.nm64` literal suggests dynamic coupling only; a static import relationship is absent from the provided PE inventory.
- Evidence basis: `analysis/pe_inventory.json`, `sections/_tables/gamescale.nm64.imports-exports.md`, `strings/gamescale.nm64.strings.txt`, and the matching `gamescale64` table/string corpus. Static evidence only; no runtime behavior was observed.

## GMS v270 imports and exports

#### gamescale.nm64.dll - exports (10)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | - | 19F40 |  |
| 2 | - | 1A2A0 |  |
| 3 | - | 1A840 |  |
| 4 | - | 1B390 |  |
| 5 | - | 1B730 |  |
| 6 | - | 1F210 |  |
| 7 | - | 21B60 |  |
| 8 | - | 21FE0 |  |
| 9 | - | 22AC0 |  |
| 10 | - | 25270 |  |

#### gamescale.nm64.dll - imports (108 symbols)

- **KERNEL32.dll** (104): `AcquireSRWLockExclusive`, `CloseHandle`, `CompareStringW`, `CreateDirectoryW`, `CreateFileW`, `CreateMutexW`, `DecodePointer`, `DeleteCriticalSection`, `EncodePointer`, `EnterCriticalSection`, `EnumSystemLocalesW`, `ExitProcess`, `ExpandEnvironmentStringsW`, `FileTimeToSystemTime`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `FlsAlloc`, `FlsFree`, `FlsGetValue`, `FlsSetValue`, `FlushFileBuffers`, `FreeEnvironmentStringsW`, `FreeLibrary`, `GetACP`, `GetCPInfo`, `GetCommandLineA`, `GetCommandLineW`, `GetConsoleMode`, `GetConsoleOutputCP`, `GetCurrentProcess`, `GetCurrentProcessId`, `GetCurrentThreadId`, `GetDateFormatW`, `GetEnvironmentStringsW`, `GetEnvironmentVariableW`, `GetFileAttributesExW`, `GetFileType`, `GetLastError`, `GetLocaleInfoW`, `GetModuleFileNameW`, `GetModuleHandleExW`, `GetModuleHandleW`, `GetOEMCP`, `GetProcAddress`, `GetProcessHeap`, `GetStartupInfoW`, `GetStdHandle`, `GetStringTypeW`, `GetSystemTimeAsFileTime`, `GetTickCount`, `GetTimeFormatW`, `GetTimeZoneInformation`, `GetUserDefaultLCID`, `HeapAlloc`, `HeapDestroy`, `HeapFree`, `HeapReAlloc`, `HeapSize`, `InitializeCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `InitializeCriticalSectionEx`, `InitializeSListHead`, `InterlockedFlushSList`, `IsDebuggerPresent`, `IsProcessorFeaturePresent`, `IsValidCodePage`, `IsValidLocale`, `LCMapStringEx`, `LCMapStringW`, `LeaveCriticalSection`, `LoadLibraryExW`, `MoveFileExW`, `MultiByteToWideChar`, `OutputDebugStringW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `RaiseException`, `ReleaseMutex`, `ReleaseSRWLockExclusive`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlPcToFileHeader`, `RtlUnwindEx`, `RtlVirtualUnwind`, `SetEnvironmentVariableW`, `SetFilePointer`, `SetFilePointerEx`, `SetLastError`, `SetStdHandle`, `SetUnhandledExceptionFilter`, `Sleep`, `SleepConditionVariableSRW`, `TerminateProcess`, `TlsAlloc`, `TlsFree`, `TlsGetValue`, `TlsSetValue`, `UnhandledExceptionFilter`, `WaitForSingleObject`, `WakeAllConditionVariable`, `WideCharToMultiByte`, `WriteConsoleW`, `WriteFile`
- **USER32.dll** (1): `UnregisterClassW`
- **nmcogame64.dll** (3): `NMCO_CallNMFunction`, `NMCO_MemoryFree`, `NMCO_SetLocaleAndRegion`
