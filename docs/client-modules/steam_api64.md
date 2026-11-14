# steam_api64.dll - unified dossier

**GMS v270**: 150,312 bytes, linked 2012-03-15, 89 imported / 56 exported.
**MSEA**: not shipped in the audited MSEA corpus.
**Status**: GMS-only.

## MSEA build

Not shipped in the audited MSEA corpus - GMS-only module.

## GMS v270 dossier

Role and version: Steamworks client API; PE FileVersion is `01.33.85.19` (the embedded description identifies it as Steam Client API). `MapleStory.exe` imports `SteamFriends`; `gamescale64.dll` declares 10 delay imports from this DLL.

Exports: 56 total, grouped as 18 SteamAPI/core and callback functions, 15 game-server functions, 15 interface accessors, 5 content-server functions, and 3 legacy/generic helpers.

Notable imports and strings: `gamescale64.dll` declares `SteamAPI_IsSteamRunning`, `SteamAPI_RestartAppIfNecessary`, callback registration/run/shutdown, and three `SteamInternal_*` functions. Its strings include `steam_appid.txt` and `SteamAppId`; the Steam API strings describe `SteamAPI_Init()` failure when no app ID is found. No literal app ID was identified in the available Steam/engine strings. The corpus has no `MapleStory.exe` strings sidecar, so an EXE numeric constant is not ruled out.

Integration notes: Seven of the ten delay-imported names are present in this DLL's export table. `SteamInternal_ContextInit`, `SteamInternal_FindOrCreateUserInterface`, and `SteamInternal_SteamAPI_Init` are not among its 56 recorded exports; this is a static import/export mismatch to resolve against the runtime/package arrangement, not proof of a runtime failure. `SteamFriends` is a matching direct import from the game EXE.

Evidence: `analysis/pe_inventory.json`; `sections/_tables/steam_api64.imports-exports.md`; `sections/_tables/gamescale64.imports-exports.md`; `strings/gamescale64.strings.txt`; `strings/steam_api64.strings.txt`; PE version resource at `<client-root>/steam_api64.dll`.

## GMS v270 imports and exports

#### steam_api64.dll - exports (56)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | GetHSteamPipe | 37D0 |  |
| 2 | GetHSteamUser | 4A60 |  |
| 3 | SteamAPI_GetHSteamPipe | 37D0 |  |
| 4 | SteamAPI_GetHSteamUser | 4A60 |  |
| 5 | SteamAPI_GetSteamInstallPath | 46D0 |  |
| 6 | SteamAPI_Init | 56B0 |  |
| 7 | SteamAPI_InitSafe | 56A0 |  |
| 8 | SteamAPI_IsSteamRunning | 4250 |  |
| 9 | SteamAPI_RegisterCallResult | 4480 |  |
| 10 | SteamAPI_RegisterCallback | 4460 |  |
| 11 | SteamAPI_RestartAppIfNecessary | 3B00 |  |
| 12 | SteamAPI_RunCallbacks | 43E0 |  |
| 13 | SteamAPI_SetBreakpadAppID | 53D0 |  |
| 14 | SteamAPI_SetMiniDumpComment | 5390 |  |
| 15 | SteamAPI_SetTryCatchCallbacks | 1050 |  |
| 16 | SteamAPI_Shutdown | 51F0 |  |
| 17 | SteamAPI_UnregisterCallResult | 4490 |  |
| 18 | SteamAPI_UnregisterCallback | 4470 |  |
| 19 | SteamAPI_UseBreakpadCrashHandler | 44C0 |  |
| 20 | SteamAPI_WriteMiniDump | 5340 |  |
| 21 | SteamApps | 3780 |  |
| 22 | SteamClient | 3710 |  |
| 23 | SteamContentServer | 56C0 |  |
| 24 | SteamContentServerUtils | 56D0 |  |
| 25 | SteamContentServer_Init | 56E0 |  |
| 26 | SteamContentServer_RunCallbacks | 58A0 |  |
| 27 | SteamContentServer_Shutdown | 57E0 |  |
| 28 | SteamFriends | 3730 |  |
| 29 | SteamGameServer | 58C0 |  |
| 30 | SteamGameServerApps | 58E0 |  |
| 31 | SteamGameServerHTTP | 5910 |  |
| 32 | SteamGameServerNetworking | 58F0 |  |
| 33 | SteamGameServerStats | 5900 |  |
| 34 | SteamGameServerUtils | 58D0 |  |
| 35 | SteamGameServer_BSecure | 5CF0 |  |
| 36 | SteamGameServer_GetHSteamPipe | 5920 |  |
| 37 | SteamGameServer_GetHSteamUser | 5930 |  |
| 38 | SteamGameServer_GetIPCCallCount | 5D10 |  |
| 39 | SteamGameServer_GetSteamID | 5D30 |  |
| 40 | SteamGameServer_Init | 5BE0 |  |
| 41 | SteamGameServer_InitSafe | 5BB0 |  |
| 42 | SteamGameServer_RunCallbacks | 5CD0 |  |
| 43 | SteamGameServer_Shutdown | 5C10 |  |
| 44 | SteamHTTP | 37C0 |  |
| 45 | SteamMatchmaking | 3750 |  |
| 46 | SteamMatchmakingServers | 3760 |  |
| 47 | SteamNetworking | 3790 |  |
| 48 | SteamRemoteStorage | 37A0 |  |
| 49 | SteamScreenshots | 37B0 |  |
| 50 | SteamUser | 3720 |  |
| 51 | SteamUserStats | 3770 |  |
| 52 | SteamUtils | 3740 |  |
| 53 | Steam_GetHSteamUserCurrent | 44B0 |  |
| 54 | Steam_RegisterInterfaceFuncs | 44A0 |  |
| 55 | Steam_RunCallbacks | 43D0 |  |
| 56 | g_pSteamClientGameServer | 22C70 |  |

#### steam_api64.dll - imports (89 symbols)

- **KERNEL32.dll** (85): `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetCommandLineA`, `MultiByteToWideChar`, `GetExitCodeProcess`, `OpenProcess`, `OutputDebugStringA`, `SetEnvironmentVariableA`, `LoadLibraryExW`, `LoadLibraryExA`, `CloseHandle`, `GetProcAddress`, `SetEndOfFile`, `WriteConsoleW`, `TerminateProcess`, `GetCurrentProcess`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `IsDebuggerPresent`, `RtlCaptureContext`, `RaiseException`, `RtlPcToFileHeader`, `RtlLookupFunctionEntry`, `RtlUnwindEx`, `GetLastError`, `HeapFree`, `HeapAlloc`, `EnterCriticalSection`, `LeaveCriticalSection`, `GetCurrentThreadId`, `FlsSetValue`, `GetVersionExA`, `GetProcessHeap`, `WriteFile`, `GetStdHandle`, `FlsGetValue`, `TlsFree`, `FlsFree`, `SetLastError`, `TlsSetValue`, `FlsAlloc`, `HeapSetInformation`, `HeapCreate`, `HeapDestroy`, `ExitProcess`, `Sleep`, `HeapSize`, `SetHandleCount`, `GetFileType`, `GetStartupInfoA`, `DeleteCriticalSection`, `GetCPInfo`, `GetACP`, `GetOEMCP`, `IsValidCodePage`, `WideCharToMultiByte`, `FreeEnvironmentStringsA`, `GetEnvironmentStrings`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `QueryPerformanceCounter`, `GetTickCount`, `GetCurrentProcessId`, `GetSystemTimeAsFileTime`, `LoadLibraryA`, `GetLocaleInfoA`, `InitializeCriticalSection`, `HeapReAlloc`, `SetStdHandle`, `GetConsoleCP`, `GetConsoleMode`, `FlushFileBuffers`, `ReadFile`, `CreateFileA`, `LCMapStringA`, `LCMapStringW`, `GetStringTypeA`, `GetStringTypeW`, `SetFilePointer`, `CompareStringA`, `CompareStringW`, `RtlVirtualUnwind`, `WriteConsoleA`, `GetConsoleOutputCP`
- **ADVAPI32.dll** (3): `RegQueryValueExA`, `RegCloseKey`, `RegOpenKeyExA`
- **SHELL32.dll** (1): `ShellExecuteA`
