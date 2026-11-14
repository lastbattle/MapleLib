# MachineIdLib.dll - unified dossier

**GMS v270**: 77,360 bytes, linked 2019-02-28, 71 imported / 2 exported.
**MSEA**: 77,360 bytes, linked 2019-02-28, 71 imported / 2 exported.
**Status**: identical builds.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role and loading

`MachineIdLib.dll` exposes Nexon machine-identity helpers. `MapleStory.exe` statically imports `MIDLib_GetMachineId`, so this is a direct game dependency loaded by the Windows loader. Its other export, `MIDLib_GetGameRoomCode`, suggests support for a PC-room/game-room identifier as well as a machine ID.

The PE VersionInfo fields are empty in the supplied target; no file or product version is assigned here. The strings identify Nexon copyright metadata but do not provide a reliable version number.

### Exports

2 exports.

| Export | Name-based interpretation |
| --- | --- |
| `MIDLib_GetMachineId` | Return or retrieve a machine identifier |
| `MIDLib_GetGameRoomCode` | Return or retrieve a game-room code |

### Notable imports

- `IPHLPAPI.DLL`: `GetAdaptersInfo`, providing access to network-adapter information.
- `KERNEL32.dll`: volume information, process/thread snapshots, process names, command-line/environment, and system/runtime helpers.
- `USER32.dll`: `GetWindowTextA` and `EnumThreadWindows`.

### Embedded endpoints and product strings

- No application endpoint or network transport import was identified.
- Strings include `MachineIdLib.dll`, `MIDLib_GetMachineId`, and legacy names such as `megalogon.exe`, `GGLogin`, `LoginGAME`, `report25.mod`, and `server.exe`. These are embedded identifiers, not proof that those processes were present at runtime.
- The DLL contains `NEXON Korea Corporation` metadata. No FileVersion/ProductVersion was available from its version resource.

### RE notes

Adapter and volume/process APIs are consistent with collecting inputs for a machine fingerprint, but static imports do not establish the exact fields, hashing scheme, persistence, or transmission behavior. The game has a direct import of `MIDLib_GetMachineId`; the second export may be available to dynamic callers, but no such caller is identified here. Sources: `analysis/pe_inventory.json` (`MachineIdLib.dll` entry and `MapleStory.exe` imports); `analysis/strings/MachineIdLib.strings.txt` near `0x0000DB7C` and `0x0000DBA4`.

## GMS v270

Bit-identical to the MSEA build; everything above applies.

## MSEA imports and exports

#### MachineIdLib.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | MIDLib_GetGameRoomCode | 1010 |  |
| 2 | MIDLib_GetMachineId | 1000 |  |

#### MachineIdLib.dll - imports (71 symbols)

- **KERNEL32.dll** (68): `Thread32Next`, `Thread32First`, `CloseHandle`, `Process32Next`, `GetWindowsDirectoryA`, `CreateToolhelp32Snapshot`, `Process32First`, `GetVolumeInformationA`, `GetLastError`, `HeapFree`, `HeapAlloc`, `GetCurrentThreadId`, `FlsSetValue`, `GetCommandLineA`, `GetCPInfo`, `GetACP`, `GetOEMCP`, `IsValidCodePage`, `EncodePointer`, `DecodePointer`, `FlsGetValue`, `FlsFree`, `SetLastError`, `FlsAlloc`, `TerminateProcess`, `GetCurrentProcess`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `IsDebuggerPresent`, `RtlVirtualUnwind`, `RtlLookupFunctionEntry`, `RtlCaptureContext`, `LCMapStringA`, `WideCharToMultiByte`, `MultiByteToWideChar`, `LCMapStringW`, `HeapSetInformation`, `HeapCreate`, `HeapDestroy`, `GetModuleHandleW`, `Sleep`, `GetProcAddress`, `ExitProcess`, `WriteFile`, `GetStdHandle`, `GetModuleFileNameA`, `SetHandleCount`, `GetFileType`, `GetStartupInfoA`, `DeleteCriticalSection`, `FreeEnvironmentStringsA`, `GetEnvironmentStrings`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `RtlUnwindEx`, `QueryPerformanceCounter`, `GetTickCount`, `GetCurrentProcessId`, `GetSystemTimeAsFileTime`, `GetStringTypeA`, `GetStringTypeW`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetLocaleInfoA`, `LoadLibraryA`, `InitializeCriticalSectionAndSpinCount`, `HeapReAlloc`, `HeapSize`
- **USER32.dll** (2): `GetWindowTextA`, `EnumThreadWindows`
- **IPHLPAPI.DLL** (1): `GetAdaptersInfo`

_GMS build bit-identical - the tables above cover both._
