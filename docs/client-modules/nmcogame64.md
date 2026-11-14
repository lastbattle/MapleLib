# nmcogame64.dll - unified dossier

**GMS v270**: 468,784 bytes, linked 2021-10-26, 179 imported / 6 exported.
**MSEA**: not shipped in the audited MSEA corpus.
**Status**: GMS-only.

## MSEA build

Not shipped in the audited MSEA corpus - GMS-only module.

## GMS v270 dossier

**Target:** `<client-root>\nmcogame64.dll`.

**Role.** A Nexon Messenger Game Service compatibility/bootstrap DLL. Its six-export NMCO API exposes patch and locale configuration plus generic message dispatch; embedded configuration and updater strings point to the larger `nmconew64.dll` / NexonPlug subsystem. The evidence favors a launcher/messenger integration component, not an anti-cheat agent. This is a static classification, not an observation of runtime behavior.

### Mechanisms

- **NMCO call boundary:** `NMCO_CallNMFunction` (VA `0x18001BC40`) and `NMCO_MemoryFree` (`0x18001BD40`) are the generic call/result ownership interface. Export names establish the API surface; the exact command semantics are not recovered here.
- **Region and patch configuration:** `NMCO_SetLocaleAndRegion` (`0x18001BD80`), `NMCO_SetPatchOption` (`0x18001BE40`), and the ANSI/wide version-URL setters (`0x18001BE80`, `0x18001BF50`) expose the game's locale and patch metadata to the service layer.
- **Messenger module/updater path:** the string table names `nmconew64.dll` (RVA `0x4EC40`), `NMService.ini`, `MessengerModule`, `VersionFileUrl`, `CModuleUpdater::Download`, and `CModuleUpdater::IsNeedUpdate`. Imports include `LoadLibraryW`/`LoadLibraryExW`, WinINet HTTP, and file APIs, consistent with dynamic module loading and update support.
- **Interprocess coordination:** strings `CSLock_Event_` and `CSLock_MMF_` (RVAs `0x519B0` and `0x519C0`) sit alongside event, file-mapping, thread, and ACL imports. This suggests named synchronization/shared-memory plumbing; it does not by itself establish a protection function.

### Exports

Image base: `0x180000000`. VA = image base + RVA.

| Ordinal | Export | RVA | VA |
|---:|---|---:|---:|
| 1 | `NMCO_CallNMFunction` | `0x1BC40` | `0x18001BC40` |
| 2 | `NMCO_MemoryFree` | `0x1BD40` | `0x18001BD40` |
| 3 | `NMCO_SetLocaleAndRegion` | `0x1BD80` | `0x18001BD80` |
| 4 | `NMCO_SetPatchOption` | `0x1BE40` | `0x18001BE40` |
| 5 | `NMCO_SetVersionFileUrlA` | `0x1BE80` | `0x18001BE80` |
| 6 | `NMCO_SetVersionFileUrlW` | `0x1BF50` | `0x18001BF50` |

### Notable imports

- `WININET.dll` HTTP download/query functions and `WS2_32.dll` socket/event functions.
- `KERNEL32.dll` module loading, file/INI access, threads, events, file mappings, and process/module identity functions.
- `ADVAPI32.dll` registry and security-descriptor/ACL functions; `VERSION.dll` file-version lookup; `SHLWAPI.dll` path helpers; `ole32.dll` COM initialization.
- The import set does not include Service Control Manager entry points such as `OpenSCManager` or `CreateService`; the service terminology below is not evidence that this DLL installs a Windows service itself.

### Embedded strings and RTTI

- Version-resource strings identify `Nexon Corp.`, `NexonMessenger Game Service`, internal name `nmcogame`, and original filename `nmcogame.dll`; file/product version `2.5.29.3`.
- RTTI includes `CNMLocaleManager`, locale records for KR/JP/TW/CN/US, `CStreamCryptor` versions, XML/Ajax/HTTP clients, and local/global lock watchers. These are consistent with a regionalized messaging/client SDK, not an anti-cheat-specific class set.
- `nmconew64.dll`, `nmconew.ini`, `nmconew.cfg`, `NMService.ini`, `MessengerModule`, `VersionFileUrl`, `PlatformModule path`, and updater log strings mark the integration boundary.
- The embedded PDB basename is `nmcogame64.pdb` under a NexonTalk/PlatformSDK release tree. The PE timestamp is `2021-10-26`.

### RE notes

- The version resource says Nexon Messenger Game Service, and the Authenticode check on the audit host reported a valid signature by NEXON Korea Corporation.
- `MapleStory.exe` statically imports only `NMCO_SetPatchOption` from this DLL. `gamescale.nm64.dll` statically imports `NMCO_CallNMFunction`, `NMCO_MemoryFree`, and `NMCO_SetLocaleAndRegion`. `nmconew64.dll` is not a static import of the EXE; its basename and the loader/updater markers in this DLL support a dynamic relationship, but no runtime load was observed.
- Source: `analysis/pe_inventory.json`, `strings/nmcogame64.strings.txt`, and `sections/_tables/nmcogame64.imports-exports.md`. Addresses are VAs for exports and RVAs for string-table entries.
- Function-body decompilation was not completed; mechanisms are attributed from exports, imports, strings, RTTI, and resources.

## GMS v270 imports and exports

#### nmcogame64.dll - exports (6)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | NMCO_CallNMFunction | 1BC40 |  |
| 2 | NMCO_MemoryFree | 1BD40 |  |
| 3 | NMCO_SetLocaleAndRegion | 1BD80 |  |
| 4 | NMCO_SetPatchOption | 1BE40 |  |
| 5 | NMCO_SetVersionFileUrlA | 1BE80 |  |
| 6 | NMCO_SetVersionFileUrlW | 1BF50 |  |

#### nmcogame64.dll - imports (179 symbols)

- **KERNEL32.dll** (114): `Sleep`, `ReadFile`, `FindClose`, `CloseHandle`, `GetPrivateProfileStringW`, `CreateFileW`, `FindFirstFileW`, `FreeLibrary`, `GetProcAddress`, `LocalFree`, `GetCurrentProcessId`, `GetLastError`, `SetFilePointer`, `FormatMessageW`, `LoadLibraryExW`, `GetModuleFileNameW`, `WideCharToMultiByte`, `CreateDirectoryW`, `GetTickCount`, `GetLocalTime`, `DeleteFileW`, `WriteFile`, `SetFileAttributesW`, `GetFileAttributesW`, `FindNextFileW`, `CopyFileW`, `LoadLibraryW`, `RaiseException`, `InitializeCriticalSectionAndSpinCount`, `DeleteCriticalSection`, `MultiByteToWideChar`, `FindResourceExW`, `FindResourceW`, `SizeofResource`, `LoadResource`, `GetProcessHeap`, `HeapSize`, `HeapFree`, `HeapReAlloc`, `HeapAlloc`, `HeapDestroy`, `GetModuleHandleW`, `WriteConsoleW`, `SetEndOfFile`, `SetStdHandle`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `IsValidCodePage`, `FindNextFileA`, `FindFirstFileExA`, `FlushFileBuffers`, `LCMapStringW`, `GetStringTypeW`, `GetStdHandle`, `GetACP`, `GetConsoleCP`, `SetFilePointerEx`, `GetFileType`, `ReadConsoleW`, `GetConsoleMode`, `GetModuleFileNameA`, `ExitProcess`, `GetModuleHandleExW`, `FreeLibraryAndExitThread`, `ExitThread`, `CreateThread`, `VirtualQuery`, `VirtualProtect`, `LockResource`, `VirtualAlloc`, `SetLastError`, `InterlockedFlushSList`, `TlsFree`, `TlsSetValue`, `TlsGetValue`, `TlsAlloc`, `RtlUnwindEx`, `EncodePointer`, `RtlPcToFileHeader`, `InitializeSListHead`, `GetSystemTimeAsFileTime`, `QueryPerformanceCounter`, `GetCurrentThreadId`, `SetEvent`, `WaitForSingleObject`, `GetSystemInfo`, `MapViewOfFile`, `UnmapViewOfFile`, `CreateEventA`, `CreateEventW`, `CreateFileMappingA`, `EnterCriticalSection`, `LeaveCriticalSection`, `LoadLibraryA`, `GetSystemDirectoryA`, `LocalAlloc`, `GetVersionExW`, `IsDebuggerPresent`, `OutputDebugStringW`, `ResetEvent`, `WaitForSingleObjectEx`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`
- **USER32.dll** (1): `wsprintfA`
- **ADVAPI32.dll** (10): `SetEntriesInAclW`, `FreeSid`, `InitializeSecurityDescriptor`, `SetSecurityDescriptorDacl`, `RegQueryValueExW`, `RegOpenKeyExW`, `RegCloseKey`, `SetSecurityDescriptorSacl`, `GetSecurityDescriptorSacl`, `AllocateAndInitializeSid`
- **SHELL32.dll** (1): `SHGetSpecialFolderPathW`
- **SHLWAPI.dll** (10): `PathFindExtensionW`, `PathRemoveExtensionW`, `PathRemoveFileSpecW`, `PathAppendW`, `PathFileExistsW`, `StrToIntExW`, `PathCanonicalizeW`, `PathGetDriveNumberW`, `PathRemoveBackslashW`, `PathRemoveBlanksW`
- **WININET.dll** (12): `InternetOpenW`, `InternetCloseHandle`, `InternetConnectW`, `HttpQueryInfoW`, `HttpSendRequestW`, `HttpOpenRequestW`, `InternetQueryDataAvailable`, `InternetOpenUrlW`, `InternetGetLastResponseInfoW`, `InternetSetOptionW`, `InternetSetFilePointer`, `InternetReadFile`
- **WS2_32.dll** (25): `closesocket`, `htonl`, `htons`, `inet_addr`, `inet_ntoa`, `ntohs`, `gethostbyaddr`, `gethostbyname`, `getservbyport`, `getservbyname`, `WSAStartup`, `WSACleanup`, `WSASetLastError`, `WSAGetLastError`, `WSACloseEvent`, `WSACreateEvent`, `WSAEnumNetworkEvents`, `WSAEventSelect`, `WSAGetOverlappedResult`, `WSARecv`, `WSAResetEvent`, `WSASend`, `WSASetEvent`, `WSASocketW`, `WSAConnect`
- **VERSION.dll** (3): `GetFileVersionInfoA`, `VerQueryValueA`, `GetFileVersionInfoSizeA`
- **ole32.dll** (3): `CoUninitialize`, `CoCreateInstance`, `CoInitialize`
