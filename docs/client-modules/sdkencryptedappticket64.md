# sdkencryptedappticket64.dll - unified dossier

**GMS v270**: 873,768 bytes, linked 2012-03-15, 74 imported / 8 exported.
**MSEA**: not shipped in the audited MSEA corpus.
**Status**: GMS-only.

## MSEA build

Not shipped in the audited MSEA corpus - GMS-only module.

## GMS v270 dossier

Role and version: Steamworks encrypted-app-ticket helper for decrypting and checking ticket contents. No PE FileVersion/ProductVersion was stamped in the supplied DLL, so an exact release version is not established. The inventory records no direct or delay-import consumer in the EXE/engine modules.

Exports: 8 total; full export list:

| Export | Purpose indicated by name |
|---|---|
| `SteamEncryptedAppTicket_BDecryptTicket` | Decrypt a ticket |
| `SteamEncryptedAppTicket_BIsTicketForApp` | Check ticket app association |
| `SteamEncryptedAppTicket_BUserIsVacBanned` | Query VAC-ban claim in ticket |
| `SteamEncryptedAppTicket_BUserOwnsAppInTicket` | Check ownership claim |
| `SteamEncryptedAppTicket_GetTicketAppID` | Read ticket app ID |
| `SteamEncryptedAppTicket_GetTicketIssueTime` | Read issue time |
| `SteamEncryptedAppTicket_GetTicketSteamID` | Read Steam ID |
| `SteamEncryptedAppTicket_GetUserVariableData` | Read user-variable data |

Notable imports and strings: The DLL imports only standard `KERNEL32.dll` routines in the supplied table. Its strings repeat the eight public export names; no client app ID or consumer reference was identified in the EXE/engine import tables or extracted strings.

Integration notes: This is a bundled ticket parsing/validation surface, but its presence alone does not establish that GMS v270 requests or validates Steam tickets. A dynamic-load or out-of-inventory caller remains possible but unconfirmed.

Evidence: `analysis/pe_inventory.json`; `sections/_tables/sdkencryptedappticket64.imports-exports.md`; `strings/sdkencryptedappticket64.strings.txt`.

## GMS v270 imports and exports

#### sdkencryptedappticket64.dll - exports (8)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | SteamEncryptedAppTicket_BDecryptTicket | 1600 |  |
| 2 | SteamEncryptedAppTicket_BIsTicketForApp | 1200 |  |
| 3 | SteamEncryptedAppTicket_BUserIsVacBanned | 12E0 |  |
| 4 | SteamEncryptedAppTicket_BUserOwnsAppInTicket | 1650 |  |
| 5 | SteamEncryptedAppTicket_GetTicketAppID | 12A0 |  |
| 6 | SteamEncryptedAppTicket_GetTicketIssueTime | 1020 |  |
| 7 | SteamEncryptedAppTicket_GetTicketSteamID | 1240 |  |
| 8 | SteamEncryptedAppTicket_GetUserVariableData | 1060 |  |

#### sdkencryptedappticket64.dll - imports (74 symbols)

- **KERNEL32.dll** (74): `InitializeCriticalSection`, `DeleteCriticalSection`, `LeaveCriticalSection`, `EnterCriticalSection`, `Sleep`, `RaiseException`, `RtlPcToFileHeader`, `RtlLookupFunctionEntry`, `RtlUnwindEx`, `TerminateProcess`, `GetCurrentProcess`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `IsDebuggerPresent`, `RtlCaptureContext`, `GetCurrentThreadId`, `FlsSetValue`, `GetCommandLineA`, `HeapFree`, `GetVersionExA`, `HeapAlloc`, `GetProcessHeap`, `GetLastError`, `LCMapStringA`, `WideCharToMultiByte`, `MultiByteToWideChar`, `LCMapStringW`, `GetCPInfo`, `GetModuleHandleA`, `GetProcAddress`, `FlsGetValue`, `TlsFree`, `FlsFree`, `SetLastError`, `TlsSetValue`, `FlsAlloc`, `WriteFile`, `GetStdHandle`, `GetModuleFileNameA`, `HeapSize`, `ExitProcess`, `HeapSetInformation`, `HeapCreate`, `HeapDestroy`, `SetHandleCount`, `GetFileType`, `GetStartupInfoA`, `FreeEnvironmentStringsA`, `GetEnvironmentStrings`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `QueryPerformanceCounter`, `GetTickCount`, `GetCurrentProcessId`, `GetSystemTimeAsFileTime`, `RtlVirtualUnwind`, `GetConsoleCP`, `GetConsoleMode`, `FlushFileBuffers`, `GetACP`, `GetOEMCP`, `IsValidCodePage`, `GetLocaleInfoA`, `GetStringTypeA`, `GetStringTypeW`, `HeapReAlloc`, `LoadLibraryA`, `WriteConsoleA`, `GetConsoleOutputCP`, `WriteConsoleW`, `SetFilePointer`, `SetStdHandle`, `CreateFileA`, `CloseHandle`
