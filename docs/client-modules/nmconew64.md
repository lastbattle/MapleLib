# nmconew64.dll - unified dossier

**GMS v270**: 3,005,232 bytes, linked 2021-09-07, 256 imported / 3 exported.
**MSEA**: not shipped in the audited MSEA corpus.
**Status**: GMS-only.

## MSEA build

Not shipped in the audited MSEA corpus - GMS-only module.

## GMS v270 dossier

**Target:** `<client-root>\nmconew64.dll`.

**Role.** Nexon's Nexon Messenger Dynamic Link Library (NMCO New), with NexonPlug/NMService attachment and messenger functionality. Its public ABI is a small generic function-call dispatcher; the strings, RTTI, process/IPC imports, and networking imports identify a client/agent-style service integration. No evidence here supports classifying it as the game's anti-cheat or protection engine.

### Mechanisms

- **Generic NMCO dispatch:** `NMCO_CallNMFunc` (VA `0x180110EE0`) and `NMCO_CallNMFunc2` (`0x180111070`) are the exported call interfaces; `NMCO_MemoryFree` (`0x180111220`) releases returned memory. Names are observed; command IDs and payload semantics remain opaque.
- **Attach to the Nexon messenger server:** RTTI/string entries include `CNMAttachToNMCOServerFunc` (RVA `0x213130`), `CNMClientObject`, `OpenFunctionCallEvent`, and `_NMCONEW_FuncCall_Event_%s_` / `_NMCONEW_Reply_Event_%s_` (RVAs `0x22A5B0` / `0x22A618`). Process enumeration/open, named-event, mutex, and file-mapping imports fit local server discovery and IPC.
- **NMService/NexonPlug launch and patch path:** strings name `NMService.exe` (RVA `0x222688`), `NexonPlug`, `NMService is not running`, and command templates containing `-mode:launch`, `-mode:install`, and `-service:%u` (RVA `0x229A80`). `CreateProcessW`, registry, path, file, and version APIs support a launcher/updater role; they do not prove service installation or execution in this audit.
- **Messenger/network payloads:** RTTI includes login, plug-token, chat/channel event and client-object classes; imports include WinINet, Winsock, PSAPI, and IP Helper APIs. This is a broad messaging/platform SDK footprint, not solely an anti-tamper surface.

### Exports

Image base: `0x180000000`. VA = image base + RVA.

| Ordinal | Export | RVA | VA |
|---:|---|---:|---:|
| 1 | `NMCO_CallNMFunc` | `0x110EE0` | `0x180110EE0` |
| 2 | `NMCO_CallNMFunc2` | `0x111070` | `0x180111070` |
| 3 | `NMCO_MemoryFree` | `0x111220` | `0x180111220` |

### Notable imports

- `KERNEL32.dll`: `CreateToolhelp32Snapshot`, `Process32FirstW`/`Process32NextW`, `OpenProcess`, `CreateProcessW`, `OpenEventW`, `OpenFileMappingW`, `MapViewOfFile`, `CreateMutexW`, plus file, thread, and synchronization APIs.
- `WININET.dll` HTTP functions and `WS2_32.dll` socket/listen/connect/send/receive functions; `PSAPI.DLL` module enumeration; `IPHLPAPI.DLL` adapter/interface queries.
- `ADVAPI32.dll` registry and ACL/security-descriptor functions; `VERSION.dll` file-version lookup; `OLE32.dll` stream helpers.
- The PE import set has no Service Control Manager entry points. `NMService.exe` and `-service` are service-process markers, but do not show that this DLL registers a kernel driver or Windows service.

### Embedded strings and RTTI

- Version-resource strings identify `Nexon Corp.`, `NexonMessenger Dynamic Link Library`, internal name `NMCONewDll`, original filename `NMCONew.dll`, and file/product version `2.5.176.0`.
- Relevant RTTI includes `CNMClientObject`, `CNMAttachToNMCOServerFunc`, `CNMLoginMessengerFunc`, `CNMPlugTokenLoginFunc`, messenger/chat/channel event classes, `CHTTPClient`, and `CStreamCryptor`.
- Service/IPC strings include `NMService.exe`, `NMService.ini`, `NexonPlug`, `SOFTWARE\\Nexon\\NexonPlug`, `_NexonMessengerNewSI_%s_`, `Global\\_NexonMessengerNewSI_%s_`, `_NMCONEW_EventNotify_Event_%s_`, and `NMService is not running`.
- The PE timestamp is `2021-09-07`; the Authenticode check on the audit host reported a valid signature by NEXON Korea Corporation.

### RE notes

- `MapleStory.exe` has no static import of `nmconew64.dll`; the direct static importer observed for the sibling API is `nmcogame64.dll` (its strings name `nmconew64.dll` and its import set includes DLL loading). The main EXE's direct NMCO dependency is through `nmcogame64.dll`, not this DLL's exports.
- Imports and service strings prove capability/intent in the binary, not that a service was running during gameplay. No driver name or SCM registration API was identified in the supplied import/string evidence.
- Source: `analysis/pe_inventory.json`, `strings/nmconew64.strings.txt`, and `sections/_tables/nmconew64.imports-exports.md`. Addresses are VAs for exports and RVAs for string-table entries.
- Function-body decompilation was not completed; mechanisms are attributed from exports, imports, strings, RTTI, and resources.

## GMS v270 imports and exports

#### nmconew64.dll - exports (3)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | NMCO_CallNMFunc | 110EE0 |  |
| 2 | NMCO_CallNMFunc2 | 111070 |  |
| 3 | NMCO_MemoryFree | 111220 |  |

#### nmconew64.dll - imports (256 symbols)

- **KERNEL32.dll** (158): `SetEvent`, `LoadResource`, `SizeofResource`, `LockResource`, `GetExitCodeThread`, `TerminateThread`, `WaitForMultipleObjects`, `Sleep`, `FindResourceExW`, `FindResourceW`, `GetLocalTime`, `GetCurrentThreadId`, `GetTickCount`, `CreateFileW`, `WriteFile`, `CompareStringW`, `MultiByteToWideChar`, `WideCharToMultiByte`, `FindFirstFileW`, `FindClose`, `GetPrivateProfileStringW`, `GetPrivateProfileIntW`, `GetFileSize`, `ReadFile`, `GetVersion`, `GetLocaleInfoW`, `CreateDirectoryW`, `CreateToolhelp32Snapshot`, `GetCurrentProcessId`, `Process32FirstW`, `OpenProcess`, `Process32NextW`, `GetProcAddress`, `GetModuleHandleW`, `SetFilePointer`, `LoadLibraryExW`, `FormatMessageW`, `LocalFree`, `FreeLibrary`, `GlobalAlloc`, `GlobalLock`, `GlobalUnlock`, `GlobalSize`, `LCMapStringW`, `OpenEventW`, `GetFileAttributesW`, `SystemTimeToFileTime`, `GetFileInformationByHandle`, `CompareFileTime`, `SetEndOfFile`, `SetFileTime`, `CreateProcessW`, `GetCommandLineW`, `UnmapViewOfFile`, `OpenFileMappingW`, `MapViewOfFile`, `GetTempPathW`, `DeleteFileW`, `QueryPerformanceCounter`, `CreateEventW`, `ResumeThread`, `WaitForSingleObject`, `GetModuleFileNameW`, `CloseHandle`, `GetProcessHeap`, `DeleteCriticalSection`, `HeapDestroy`, `DecodePointer`, `HeapAlloc`, `RaiseException`, `HeapReAlloc`, `GetLastError`, `OutputDebugStringA`, `ReadConsoleW`, `WriteConsoleW`, `SetFilePointerEx`, `SetStdHandle`, `SetConsoleCtrlHandler`, `SetEnvironmentVariableW`, `SetEnvironmentVariableA`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `IsValidCodePage`, `FindNextFileW`, `FindNextFileA`, `FindFirstFileExW`, `FindFirstFileExA`, `GetConsoleMode`, `GetConsoleCP`, `FlushFileBuffers`, `GetStringTypeW`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetTimeFormatW`, `GetDateFormatW`, `GetFileType`, `GetStdHandle`, `GetACP`, `GetCurrentThread`, `GetModuleFileNameA`, `ExitProcess`, `VirtualQuery`, `VirtualProtect`, `VirtualAlloc`, `GetModuleHandleExW`, `FreeLibraryAndExitThread`, `ExitThread`, `CreateThread`, `SetLastError`, `InterlockedFlushSList`, `InterlockedPushEntrySList`, `TlsFree`, `TlsSetValue`, `HeapQueryInformation`, `TlsGetValue`, `TlsAlloc`, `RtlUnwindEx`, `EncodePointer`, `RtlPcToFileHeader`, `InitializeSListHead`, `GetSystemTimeAsFileTime`, `GetStartupInfoW`, `IsProcessorFeaturePresent`, `TerminateProcess`, `SetUnhandledExceptionFilter`, `UnhandledExceptionFilter`, `RtlVirtualUnwind`, `RtlLookupFunctionEntry`, `RtlCaptureContext`, `WaitForSingleObjectEx`, `ResetEvent`, `OutputDebugStringW`, `IsDebuggerPresent`, `GetVolumeInformationW`, `GetDiskFreeSpaceExW`, `GetSystemDirectoryW`, `GlobalMemoryStatus`, `CreateMutexW`, `ReleaseMutex`, `DuplicateHandle`, `GetCurrentProcess`, `GetSystemDirectoryA`, `LoadLibraryA`, `LeaveCriticalSection`, `EnterCriticalSection`, `GetVersionExW`, `LoadLibraryW`, `LocalAlloc`, `CreateFileMappingA`, `CreateEventA`, `HeapSize`, `InitializeCriticalSectionAndSpinCount`, `HeapFree`, `GetSystemInfo`
- **USER32.dll** (7): `GetSystemMetrics`, `UnregisterClassW`, `PostMessageW`, `GetForegroundWindow`, `GetWindowThreadProcessId`, `wsprintfA`, `MessageBoxW`
- **ADVAPI32.dll** (12): `GetSecurityDescriptorSacl`, `SetSecurityDescriptorSacl`, `AllocateAndInitializeSid`, `SetSecurityDescriptorDacl`, `RegSetValueExW`, `RegCreateKeyExW`, `RegQueryValueExW`, `RegOpenKeyExW`, `RegCloseKey`, `FreeSid`, `InitializeSecurityDescriptor`, `SetEntriesInAclW`
- **SHELL32.dll** (1): `SHGetSpecialFolderPathW`
- **ole32.dll** (5): `CoCreateInstance`, `CoInitialize`, `CoUninitialize`, `GetHGlobalFromStream`, `CreateStreamOnHGlobal`
- **OLEAUT32.dll** (2): `SysFreeString`, `SysAllocString`
- **SHLWAPI.dll** (11): `UrlEscapeW`, `StrToIntExW`, `PathCanonicalizeW`, `PathGetDriveNumberW`, `PathStripPathW`, `PathFileExistsW`, `PathRemoveBackslashW`, `PathAppendW`, `PathIsURLW`, `PathRemoveFileSpecW`, `PathRemoveBlanksW`
- **VERSION.dll** (3): `GetFileVersionInfoA`, `GetFileVersionInfoSizeA`, `VerQueryValueA`
- **WS2_32.dll** (36): `inet_addr`, `htons`, `htonl`, `closesocket`, `inet_ntoa`, `gethostbyname`, `ntohs`, `getsockname`, `gethostbyaddr`, `getservbyport`, `getservbyname`, `recv`, `recvfrom`, `send`, `sendto`, `shutdown`, `WSAStartup`, `WSASetLastError`, `listen`, `WSACloseEvent`, `WSAConnect`, `WSACreateEvent`, `WSAEnumNetworkEvents`, `WSAEventSelect`, `WSAGetOverlappedResult`, `WSARecv`, `WSASend`, `WSASetEvent`, `WSASocketW`, `accept`, `bind`, `connect`, `getpeername`, `WSACleanup`, `WSAGetLastError`, `WSAResetEvent`
- **WININET.dll** (17): `HttpQueryInfoW`, `HttpSendRequestW`, `InternetQueryDataAvailable`, `InternetReadFile`, `HttpEndRequestW`, `InternetWriteFile`, `InternetSetFilePointer`, `HttpAddRequestHeadersA`, `HttpOpenRequestW`, `InternetConnectW`, `InternetCloseHandle`, `InternetOpenUrlW`, `InternetOpenW`, `InternetSetCookieW`, `InternetSetOptionW`, `InternetGetLastResponseInfoW`, `HttpSendRequestExW`
- **PSAPI.DLL** (2): `EnumProcessModules`, `GetModuleFileNameExW`
- **IPHLPAPI.DLL** (2): `GetAdaptersInfo`, `GetBestInterface`
