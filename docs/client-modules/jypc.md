# jypc.dll - unified dossier

**GMS v270**: 485,168 bytes, linked 2021-10-20, 163 imported / 18 exported.
**MSEA**: 485,168 bytes, linked 2021-10-20, 163 imported / 18 exported.
**Status**: identical builds.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role and loading

The version resource identifies this as Nexon's `jyp` product, version `1.1.0.5`. Its imports indicate a monitoring-oriented client component: it can enumerate processes/modules and threads, inspect process memory and contexts, collect performance counters, use Direct3D 9, and open Winsock connections. That supports a process/performance-monitoring interpretation, but does not by itself establish anti-cheat behavior or a specific runtime purpose.

There is no direct static importer of `jypc.dll` in the supplied PE inventory. `MapleStory.exe` does contain the literal `jypc.dll` at PE offset `0x080CBB50`, which is consistent with a dynamic-load reference, but the string is not proof that the game actually loads it.

### Exports

18 exports, all with opaque `F`-prefixed names rather than descriptive API names:

| Export |
| --- |
| `F0e9292417034e96934eadd7e6fedafd8` |
| `F115b237976ec8bf35520b7902c7e6d60` |
| `F3ac1c848c87e599c50a6678d50c1a45a` |
| `F44378efa30359dfefb729d0c78d8d165` |
| `F538e7326e9781d04d9080aa6f34ad8a4` |
| `F5c3467cc03c2c02a32f0ee44fb0d091c` |
| `F5c92ca22a0292bf09dc00eabd164a92c0d` |
| `F5cea56786ca21f947588ec2697de703e` |
| `F72c5931210a394fb89fcac8833f733dd` |
| `F789442d28913a51877f4f75aea1c1b55` |
| `F89fc6c72bce7ade75c3266e5b75a75d0` |
| `F9340b5014dfa44350fa31431fa11c9dc` |
| `Fabc80445266abf307701a1b32d59e60a` |
| `Fb32f6d035d24c0561d9a68269c2eec07` |
| `Fd66f9f17b91646fea9c989edb0f79a4c` |
| `Fdc54bd409d31528919bc6d16d3118854` |
| `Fec680445266abf307601a8b32db9e60a` |
| `Fff4e83a9888f8c346ace9f45a35759c8` |

### Notable imports

- `PSAPI.DLL`: `EnumProcessModulesEx`, `GetProcessMemoryInfo`, and `GetModuleFileNameExW`.
- `KERNEL32.dll`: process/thread snapshots, `ReadProcessMemory`, `VirtualQuery`, thread suspend/context/resume APIs, and process timing/memory APIs.
- `pdh.dll`: query/counter path collection and formatted performance-counter values.
- `WS2_32.dll`: Winsock startup, address resolution, socket/connect, send, and receive APIs.
- `d3d9.dll`: `Direct3DCreate9`.
- `ADVAPI32.dll`: registry access.

### Embedded endpoints and product strings

- `jyp.nexon.com` appears at PE offset `0x00059DD0`; this is an embedded host string, not evidence of a connection.
- Other notable strings include `OnFrameRender check tick`, `SOFTWARE\Nexon\NGS`, and `dxgi.dll`.
- Version resource strings identify CompanyName `NEXON`, ProductName `jyp`, and FileVersion/ProductVersion `1.1.0.5` (for example, version string offset `0x00071BB4`). No specific protocol path or request was recovered.

### RE notes

The APIs show capabilities for monitoring process state, performance, and graphics activity. The opaque export names prevent assigning precise semantics to individual entry points. The game filename string plus lack of an import edge makes dynamic loading plausible, not confirmed. Sources: `analysis/pe_inventory.json` (`jypc.dll` entry and all-module imports); `analysis/strings/jypc.strings.txt` at the offsets above and `analysis/strings/MapleStory.strings.txt` at `0x080CBB50`.

## GMS v270

Bit-identical to the MSEA build; everything above applies.

## MSEA imports and exports

#### jypc.dll - exports (18)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | F0e9292417034e96934eadd7e6fedafd8 | 1840 |  |
| 2 | F115b237976ec8bf35520b7902c7e6d60 | 1940 |  |
| 3 | F3ac1c848c87e599c50a6678d50c1a45a | 17D0 |  |
| 4 | F44378efa30359dfefb729d0c78d8d165 | 1860 |  |
| 5 | F538e7326e9781d04d9080aa6f34ad8a4 | 1950 |  |
| 6 | F5c3467cc03c2c02a32f0ee44fb0d091c | 1930 |  |
| 7 | F5c92ca22a0292bf09dc00eabd164a92c0d | 1A30 |  |
| 8 | F5cea56786ca21f947588ec2697de703e | 1830 |  |
| 9 | F72c5931210a394fb89fcac8833f733dd | 1920 |  |
| 10 | F789442d28913a51877f4f75aea1c1b55 | 1900 |  |
| 11 | F89fc6c72bce7ade75c3266e5b75a75d0 | 1850 |  |
| 12 | F9340b5014dfa44350fa31431fa11c9dc | 18B0 |  |
| 13 | Fabc80445266abf307701a1b32d59e60a | 1A20 |  |
| 14 | Fb32f6d035d24c0561d9a68269c2eec07 | 19B0 |  |
| 15 | Fd66f9f17b91646fea9c989edb0f79a4c | 1A00 |  |
| 16 | Fdc54bd409d31528919bc6d16d3118854 | 1910 |  |
| 17 | Fec680445266abf307601a8b32db9e60a | 1A10 |  |
| 18 | Fff4e83a9888f8c346ace9f45a35759c8 | 1770 |  |

#### jypc.dll - imports (163 symbols)

- **PSAPI.DLL** (3): `EnumProcessModulesEx`, `GetProcessMemoryInfo`, `GetModuleFileNameExW`
- **WS2_32.dll** (11): `WSAStartup`, `send`, `select`, `closesocket`, `freeaddrinfo`, `recv`, `WSAGetLastError`, `getaddrinfo`, `socket`, `__WSAFDIsSet`, `connect`
- **d3d9.dll** (1): `Direct3DCreate9`
- **pdh.dll** (5): `PdhOpenQueryA`, `PdhMakeCounterPathA`, `PdhCollectQueryData`, `PdhGetFormattedCounterValue`, `PdhAddCounterA`
- **KERNEL32.dll** (133): `RtlCaptureContext`, `VirtualQuery`, `FlushInstructionCache`, `HeapCreate`, `SetThreadContext`, `ReadConsoleW`, `WriteConsoleW`, `CreateFileW`, `GetStringTypeW`, `SetFilePointerEx`, `GetConsoleMode`, `GetConsoleCP`, `FlushFileBuffers`, `SetStdHandle`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetLocaleInfoW`, `LCMapStringW`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `QueryPerformanceFrequency`, `QueryPerformanceCounter`, `Sleep`, `GetMailslotInfo`, `ReadFile`, `CreateThread`, `MultiByteToWideChar`, `GetLastError`, `WideCharToMultiByte`, `GetCurrentThreadId`, `GetCurrentProcess`, `GetModuleHandleA`, `ReadProcessMemory`, `GetCurrentProcessId`, `CreateToolhelp32Snapshot`, `Thread32First`, `Thread32Next`, `CloseHandle`, `Module32First`, `CreateFileA`, `CreateFileMappingA`, `MapViewOfFile`, `UnmapViewOfFile`, `Module32Next`, `OpenThread`, `SuspendThread`, `GetThreadContext`, `LoadLibraryA`, `GetProcAddress`, `ResumeThread`, `GetProcessTimes`, `GlobalMemoryStatusEx`, `GetModuleHandleW`, `GetSystemInfo`, `LoadLibraryW`, `FreeLibrary`, `EncodePointer`, `DecodePointer`, `GetCommandLineA`, `HeapFree`, `IsDebuggerPresent`, `IsProcessorFeaturePresent`, `HeapAlloc`, `RtlPcToFileHeader`, `RaiseException`, `ExitProcess`, `GetModuleHandleExW`, `HeapSize`, `RtlUnwindEx`, `SetLastError`, `GetCurrentThread`, `GetProcessHeap`, `GetStdHandle`, `GetFileType`, `DeleteCriticalSection`, `GetStartupInfoW`, `GetModuleFileNameA`, `GetSystemTimeAsFileTime`, `GetEnvironmentStringsW`, `FreeEnvironmentStringsW`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `InitializeCriticalSectionAndSpinCount`, `CreateEventW`, `TerminateProcess`, `TlsFree`, `GetTickCount`, `CreateSemaphoreW`, `EnterCriticalSection`, `LeaveCriticalSection`, `SetEvent`, `WaitForSingleObjectEx`, `SignalObjectAndWait`, `SwitchToThread`, `SetThreadPriority`, `GetThreadPriority`, `GetLogicalProcessorInformation`, `CreateTimerQueueTimer`, `ChangeTimerQueueTimer`, `DeleteTimerQueueTimer`, `GetNumaHighestNodeNumber`, `GetProcessAffinityMask`, `SetThreadAffinityMask`, `RegisterWaitForSingleObject`, `UnregisterWait`, `CreateTimerQueue`, `DuplicateHandle`, `ReleaseSemaphore`, `InitializeSListHead`, `InterlockedPopEntrySList`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `QueryDepthSList`, `UnregisterWaitEx`, `GetVersionExW`, `VirtualAlloc`, `VirtualFree`, `VirtualProtect`, `OutputDebugStringW`, `GetThreadTimes`, `FreeLibraryAndExitThread`, `GetModuleFileNameW`, `LoadLibraryExW`, `IsValidCodePage`, `GetACP`, `GetOEMCP`, `GetCPInfo`, `WriteFile`, `HeapReAlloc`
- **USER32.dll** (7): `GetForegroundWindow`, `DefWindowProcA`, `RegisterClassW`, `CreateWindowExW`, `DestroyWindow`, `GetWindowThreadProcessId`, `PostQuitMessage`
- **ADVAPI32.dll** (3): `RegCloseKey`, `RegOpenKeyExW`, `RegQueryValueExA`

_GMS build bit-identical - the tables above cover both._
