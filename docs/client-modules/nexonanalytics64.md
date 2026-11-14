# NexonAnalytics64.dll - unified dossier

**GMS v270**: 8,252,912 bytes, linked 2026-03-11, 295 imported / 267 exported.
**MSEA**: 6,560,240 bytes, linked 2026-08-10, 296 imported / 189 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role and loading

This is Nexon's Analytics SDK, version `3.12.9.18`. Its exported API families cover user registration/update, common and extra fields, user/system/input/funnel/error logs, configuration, delivery callbacks, and finalization. It is a telemetry/event collection SDK by product identity and interface. `MapleStory.exe` contains the string `NexonAnalytics64.dll` at PE offset `0x07A31E80`, but the supplied import inventory has no static importer for this DLL. The name is consistent with runtime/dynamic loading, but does not prove a load occurred or identify the loader call.

### Exports

189 exports, grouped by name family (examples shown):

- `Java_*` JNI wrappers (27): Java bridge functions for logging, configuration, registration, and finalization.
- `enqueue*` (40): log/event submission, including `enqueueInputAction`, `enqueueFunnelLog`, `enqueueSystemInfoLog`, and `enqueueUserLog`.
- `get*` (39): SDK version, config, common fields, processing result, and sent-count/byte queries.
- `set*` (34): client/server config, input options, current game state, developer level, and user extra info.
- `assignInfo*` (4), `devGet*` (4), `finalize*` (4), `handle*` (4), `initialize*` (8), `inputProc` (1), `preset*` (10), `register*` (6), `unregister*` (4), and `update*` (4).

### Notable imports

- `WS2_32.dll`, `Secur32.dll`, and `CRYPT32.dll`: socket transport, security contexts, encryption, and certificate-store operations.
- `IPHLPAPI.DLL` and `WTSAPI32.dll`: adapter/interface and terminal-session information.
- `USER32.dll`: `GetAsyncKeyState`, `SetWindowsHookExW`, `CallNextHookEx`, and related window APIs. Together with `inputProc`, `registerKeymapInfo`, and `enqueueInputAction`, this indicates that the SDK supports input-event collection; it does not prove MapleStory enables or uses that feature.
- `ADVAPI32.dll`: registry, event logging, and CryptoAPI operations. `KERNEL32.dll` imports include extensive file, thread, timer, and asynchronous I/O support.

### Embedded endpoints and product strings

- Version resource: FileVersion/ProductVersion `3.12.9.18`, FileDescription `Nexon Analytics SDK`, ProductName `NexonAnalytics SDK (Dynamic Link Library)`; version string at PE offset `0x0063285C`.
- Nexon host/config strings include `public.api.nexon.com/stream-processor-proxy/ap-northeast-1`, `public.api.nexon.com`, `config.na.nexon.com`, `dev-public.api.nexon.com`, `sandbox.api.nexon.com`, `test-public.api.nexon.com`, `test-private.api.nexon.com`, and `private.api.nexon.com` (around `0x00558CC8`-`0x00558FC8`). These are embedded candidates/environment strings, not observed destinations.
- The binary contains extensive `librdkafka` 1.8.2 strings, including Kafka topic/broker errors and properties such as `client.id`, `group.id`, `ssl.ca.location`, and SASL settings. This supports a Kafka-capable telemetry transport build; it does not show that the game connects to a broker.

### RE notes

The large cross-language export surface and logging/event APIs identify the SDK's intended role. A possible input-logging capability merits attention in an audit, but neither the DLL string nor imports prove active collection. Static inventory shows no direct import edge from the game or another supplied PE, so the load path and active configuration remain unresolved. Sources: `analysis/pe_inventory.json` (`NexonAnalytics64.dll` entry and all-module imports); `analysis/strings/NexonAnalytics64.strings.txt` at the version/host offsets above and `analysis/strings/MapleStory.strings.txt` at `0x07A31E80`.

## GMS v270 delta

Crypto rework; raw socket loops and RawInput added.

- +KERNEL32.dll (1): `GetSystemDirectoryA`
- +USER32.dll (2): `GetRawInputData`, `RegisterRawInputDevices`
- +WS2_32.dll (10): `WSACloseEvent`, `WSACreateEvent`, `WSAEnumNetworkEvents`, `WSAEventSelect`, `WSAWaitForMultipleEvents`, `gethostbyaddr`, `gethostbyname`, `getservbyname`, `getservbyport`, `inet_ntoa`
- -ADVAPI32.dll (10): `CryptCreateHash`, `CryptDecrypt`, `CryptDestroyHash`, `CryptDestroyKey`, `CryptEnumProvidersW`, `CryptExportKey`, `CryptGetProvParam`, `CryptGetUserKey`, `CryptSetHashParam`, `CryptSignHashW`
- -CRYPT32.dll (4): `CertDuplicateCertificateContext`, `CertFindCertificateInStore`, `CertFreeCertificateContext`, `CertGetCertificateContextProperty`

## MSEA imports and exports

#### NexonAnalytics64.dll - exports (189)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | Java_nxa_DllHelper_devGetBackupDataAfterFinalizeJV | 16BB30 |  |
| 2 | Java_nxa_DllHelper_enqueueDevLogJV | 16B250 |  |
| 3 | Java_nxa_DllHelper_enqueuePrimitiveLogJV | 16B320 |  |
| 4 | Java_nxa_DllHelper_enqueueUserLogExJV | 16B240 |  |
| 5 | Java_nxa_DllHelper_enqueueUserLogJV | 16A6C0 |  |
| 6 | Java_nxa_DllHelper_finalizeModuleJV | 169B30 |  |
| 7 | Java_nxa_DllHelper_getCallbackPointer | 16B9D0 |  |
| 8 | Java_nxa_DllHelper_getCommonFieldJV | 16B3E0 |  |
| 9 | Java_nxa_DllHelper_getLastErrorJV | 15BEF0 |  |
| 10 | Java_nxa_DllHelper_getProcessingResultJV | 16B820 |  |
| 11 | Java_nxa_DllHelper_getSentBytesJV | 16B600 |  |
| 12 | Java_nxa_DllHelper_getSentCountJV | 16B5A0 |  |
| 13 | Java_nxa_DllHelper_getServerModuleConfigString | 169110 |  |
| 14 | Java_nxa_DllHelper_getVersionJV | 16B580 |  |
| 15 | Java_nxa_DllHelper_handleDeliveryCallbackJV | 16BB20 |  |
| 16 | Java_nxa_DllHelper_initializeServerModuleJV | 169420 |  |
| 17 | Java_nxa_DllHelper_presetBaseClassVersionJV | 1692E0 |  |
| 18 | Java_nxa_DllHelper_presetCommonFieldFilterJV | 169210 |  |
| 19 | Java_nxa_DllHelper_registerUserJV | 169B50 |  |
| 20 | Java_nxa_DllHelper_setDevLogLevelJV | 16B660 |  |
| 21 | Java_nxa_DllHelper_setExtraCommonFieldJV | 16B670 |  |
| 22 | Java_nxa_DllHelper_setServerModuleConfigString | 169020 |  |
| 23 | Java_nxa_DllHelper_setUnitTestModeJV | 16B800 |  |
| 24 | Java_nxa_DllHelper_setUserExtraInfoJV | 16A360 |  |
| 25 | Java_nxa_DllHelper_setUserExtraInfoJsonJV | 16A580 |  |
| 26 | Java_nxa_DllHelper_unregisterUserJV | 16A2B0 |  |
| 27 | Java_nxa_DllHelper_updateUserJV | 169F00 |  |
| 28 | assignInfoUser | 164420 |  |
| 29 | assignInfoUserCS | 166EB0 |  |
| 30 | assignInfoUserJson | 164EB0 |  |
| 31 | assignInfoUserJsonCS | 166F60 |  |
| 32 | devGetBackupDataAfterFinalize | 165350 |  |
| 33 | devGetBackupDataAfterFinalizeC | 15C1A0 |  |
| 34 | devGetBackupDataAfterFinalizeCS | 1678B0 |  |
| 35 | devGetBackupDataAfterFinalizeGO | 168FD0 |  |
| 36 | enqueueDevLog | 164280 |  |
| 37 | enqueueDevLogC | 15B700 |  |
| 38 | enqueueDevLogCS | 166CE0 |  |
| 39 | enqueueDevLogGO | 168A60 |  |
| 40 | enqueueErrorLog | 163460 |  |
| 41 | enqueueErrorLogCS | 1662A0 |  |
| 42 | enqueueFunnelLog | 163550 |  |
| 43 | enqueueFunnelLogJson | 164F90 |  |
| 44 | enqueueFunnelLogJsonCS | 166E00 |  |
| 45 | enqueueInputAction | 164D40 |  |
| 46 | enqueueInputActionCS | 167450 |  |
| 47 | enqueueJsonBodyLog | 1642E0 |  |
| 48 | enqueueJsonBodyUserLogEx | 165070 |  |
| 49 | enqueueJsonLog | 164220 |  |
| 50 | enqueueJsonLogCS | 166C80 |  |
| 51 | enqueueLog | 164060 |  |
| 52 | enqueueLogC | 15B4A0 |  |
| 53 | enqueueLogCS | 1663C0 |  |
| 54 | enqueueLogGO | 168020 |  |
| 55 | enqueueLogOnlyOnce | 1634C0 |  |
| 56 | enqueueLogOnlyOnceCS | 166310 |  |
| 57 | enqueueNxLog | 164180 |  |
| 58 | enqueuePrimitiveLog | 1643D0 |  |
| 59 | enqueuePrimitiveLogC | 15B770 |  |
| 60 | enqueuePrimitiveLogCS | 166D40 |  |
| 61 | enqueuePrimitiveLogGO | 168AC0 |  |
| 62 | enqueueStageLog | 163400 |  |
| 63 | enqueueStageLogCS | 166230 |  |
| 64 | enqueueSystemInfoLog | 1635D0 |  |
| 65 | enqueueSystemInfoLogCS | 166DA0 |  |
| 66 | enqueueSystemInfoLogJson | 165020 |  |
| 67 | enqueueUserJsonBodyLog | 163750 |  |
| 68 | enqueueUserLog | 163620 |  |
| 69 | enqueueUserLogC | 15B5E0 |  |
| 70 | enqueueUserLogCS | 166500 |  |
| 71 | enqueueUserLogExCS | 166C70 |  |
| 72 | enqueueUserLogExGO | 168A50 |  |
| 73 | enqueueUserLogGO | 168160 |  |
| 74 | enqueueUserNxLog | 163700 |  |
| 75 | enqueueUserNxLogEx | 164050 |  |
| 76 | finalizeModule | 1631A0 |  |
| 77 | finalizeModuleC | 15B0C0 |  |
| 78 | finalizeModuleCS | 165E10 |  |
| 79 | finalizeModuleGO | 167C80 |  |
| 80 | getClientModuleConfig | 162790 |  |
| 81 | getClientModuleConfigC | 15A1B0 |  |
| 82 | getClientModuleConfigString | 162A10 |  |
| 83 | getClientModuleConfigStringCS | 1655B0 |  |
| 84 | getClientModuleConfigStringW | 162D70 |  |
| 85 | getCommonFieldA | 1644B0 |  |
| 86 | getCommonFieldAC | 15B7E0 |  |
| 87 | getCommonFieldCS | 166FC0 |  |
| 88 | getCommonFieldGO | 168B20 |  |
| 89 | getCommonFieldW | 164820 |  |
| 90 | getCommonFieldWC | 15BB80 |  |
| 91 | getInputLogOption | 164E60 |  |
| 92 | getInputLogOptionCS | 1676A0 |  |
| 93 | getLastError | 15BEF0 |  |
| 94 | getLastErrorC | 15BEF0 |  |
| 95 | getLastErrorCS | 15BEF0 |  |
| 96 | getLastErrorGO | 15BEF0 |  |
| 97 | getPoolAllocator | 165370 |  |
| 98 | getProcessingResultA | 165330 |  |
| 99 | getProcessingResultAC | 15C180 |  |
| 100 | getProcessingResultCS | 1677A0 |  |
| 101 | getProcessingResultGO | 168EC0 |  |
| 102 | getProcessingResultW | 165340 |  |
| 103 | getProcessingResultWC | 15C190 |  |
| 104 | getSentBytes | 164BC0 |  |
| 105 | getSentBytesC | 15BF80 |  |
| 106 | getSentBytesCS | 167360 |  |
| 107 | getSentCount | 164B60 |  |
| 108 | getSentCountC | 15BF20 |  |
| 109 | getSentCountCS | 167300 |  |
| 110 | getServerModuleConfig | 162820 |  |
| 111 | getServerModuleConfigC | 15A540 |  |
| 112 | getServerModuleConfigString | 162B20 |  |
| 113 | getServerModuleConfigStringCS | 165760 |  |
| 114 | getServerModuleConfigStringW | 162E70 |  |
| 115 | getVersion | 15BF10 |  |
| 116 | getVersionC | 15BF10 |  |
| 117 | getVersionCS | 167190 |  |
| 118 | getVersionGO | 168D00 |  |
| 119 | handleDeliveryCallback | 1651C0 |  |
| 120 | handleDeliveryCallbackC | 15C150 |  |
| 121 | handleDeliveryCallbackCS | 1678A0 |  |
| 122 | handleDeliveryCallbackGO | 168FC0 |  |
| 123 | initializeClientModule | 1630A0 |  |
| 124 | initializeClientModuleC | 15AC90 |  |
| 125 | initializeClientModuleCS | 165A30 |  |
| 126 | initializeServerModule | 163120 |  |
| 127 | initializeServerModuleC | 15AEC0 |  |
| 128 | initializeServerModuleCS | 165C10 |  |
| 129 | initializeServerModuleGO | 167A10 |  |
| 130 | initializeServerModuleJS | 16BC20 |  |
| 131 | inputProc | 1651E0 |  |
| 132 | presetBaseClassVersion | 162FD0 |  |
| 133 | presetBaseClassVersionC | 15AB80 |  |
| 134 | presetBaseClassVersionCS | 165980 |  |
| 135 | presetBaseClassVersionGO | 167960 |  |
| 136 | presetCommonFieldFilter | 162F70 |  |
| 137 | presetCommonFieldFilterC | 15AB00 |  |
| 138 | presetCommonFieldFilterCS | 165910 |  |
| 139 | presetCommonFieldFilterGO | 1678F0 |  |
| 140 | presetDeveloperConfig | 163050 |  |
| 141 | presetDeveloperConfigC | 15AC30 |  |
| 142 | registerKeymapInfo | 164DD0 |  |
| 143 | registerKeymapInfoCS | 167580 |  |
| 144 | registerUser | 1631B0 |  |
| 145 | registerUserC | 15B0E0 |  |
| 146 | registerUserCS | 165E20 |  |
| 147 | registerUserGO | 167C90 |  |
| 148 | setClientModuleConfig | 162770 |  |
| 149 | setClientModuleConfigC | 159E60 |  |
| 150 | setClientModuleConfigString | 1628D0 |  |
| 151 | setClientModuleConfigStringW | 162C30 |  |
| 152 | setCurrGameState | 164CD0 |  |
| 153 | setCurrGameStateCS | 1673D0 |  |
| 154 | setDevLogLevel | 164C10 |  |
| 155 | setDevLogLevelC | 15BFE0 |  |
| 156 | setDevLogLevelCS | 1673C0 |  |
| 157 | setDevLogLevelGO | 168DF0 |  |
| 158 | setExtraCommonField | 164C20 |  |
| 159 | setExtraCommonFieldC | 15BFF0 |  |
| 160 | setExtraCommonFieldCS | 166000 |  |
| 161 | setExtraCommonFieldGO | 168E00 |  |
| 162 | setInputLogLevel | 164D30 |  |
| 163 | setInputLogLevelCS | 167440 |  |
| 164 | setInputLogOption | 164E00 |  |
| 165 | setInputLogOptionCS | 167640 |  |
| 166 | setServerModuleConfig | 162780 |  |
| 167 | setServerModuleConfigC | 159FA0 |  |
| 168 | setServerModuleConfigString | 162970 |  |
| 169 | setServerModuleConfigStringW | 162CD0 |  |
| 170 | setUnitTestMode | 1651D0 |  |
| 171 | setUnitTestModeC | 15C160 |  |
| 172 | setUnitTestModeCS | 167790 |  |
| 173 | setUnitTestModeGO | 168EB0 |  |
| 174 | setUserExtraInfo | 163320 |  |
| 175 | setUserExtraInfoC | 15B2C0 |  |
| 176 | setUserExtraInfoCS | 166090 |  |
| 177 | setUserExtraInfoGO | 167E60 |  |
| 178 | setUserExtraInfoJson | 164F00 |  |
| 179 | setUserExtraInfoJsonC | 15B3F0 |  |
| 180 | setUserExtraInfoJsonCS | 166180 |  |
| 181 | setUserExtraInfoJsonGO | 167F70 |  |
| 182 | unregisterUser | 163310 |  |
| 183 | unregisterUserC | 15B260 |  |
| 184 | unregisterUserCS | 165FA0 |  |
| 185 | unregisterUserGO | 167E00 |  |
| 186 | updateUser | 1631B0 |  |
| 187 | updateUserC | 15B0E0 |  |
| 188 | updateUserCS | 165E20 |  |
| 189 | updateUserGO | 167C90 |  |

#### NexonAnalytics64.dll - imports (296 symbols)

- **KERNEL32.dll** (191): `SleepEx`, `VerifyVersionInfoW`, `TlsGetValue`, `GetSystemTimeAsFileTime`, `TlsFree`, `FormatMessageA`, `CreateIoCompletionPort`, `HeapFree`, `ReleaseSemaphore`, `WaitForSingleObjectEx`, `HeapAlloc`, `GetProcessHeap`, `CreateEventA`, `CreateDirectoryW`, `FindFirstFileW`, `FindNextFileW`, `GetModuleFileNameW`, `FindClose`, `GetFileAttributesW`, `GetModuleHandleA`, `MultiByteToWideChar`, `GetDiskFreeSpaceExW`, `GetProcAddress`, `RaiseException`, `DecodePointer`, `GetFileTime`, `MoveFileW`, `Thread32Next`, `Thread32First`, `CreateToolhelp32Snapshot`, `GetComputerNameExW`, `GetCurrentProcessId`, `WideCharToMultiByte`, `VerSetConditionMask`, `DeleteCriticalSection`, `LocalFree`, `QueueUserAPC`, `TlsAlloc`, `TerminateThread`, `SetEvent`, `FormatMessageW`, `Sleep`, `CreateEventW`, `PostQueuedCompletionStatus`, `WaitForSingleObject`, `GetQueuedCompletionStatus`, `LeaveCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `WaitForMultipleObjects`, `CreateWaitableTimerW`, `EnterCriticalSection`, `SetLastError`, `TlsSetValue`, `SetWaitableTimer`, `FlushFileBuffers`, `SetFilePointerEx`, `UnlockFile`, `CloseHandle`, `GetLastError`, `CreateFileW`, `LockFile`, `WriteFile`, `WriteConsoleW`, `HeapSize`, `GetFullPathNameW`, `GetCurrentDirectoryW`, `SetStdHandle`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `FindFirstFileExW`, `GetTimeZoneInformation`, `HeapReAlloc`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetTimeFormatW`, `GetDateFormatW`, `FlsFree`, `FlsSetValue`, `FlsGetValue`, `FlsAlloc`, `GetConsoleOutputCP`, `GetFileSizeEx`, `SetEndOfFile`, `FileTimeToSystemTime`, `SystemTimeToTzSpecificLocalTime`, `PeekNamedPipe`, `GetFileInformationByHandle`, `GetDriveTypeW`, `ExitProcess`, `SetConsoleCtrlHandler`, `RtlUnwindEx`, `UnregisterWaitEx`, `QueryDepthSList`, `InterlockedFlushSList`, `InterlockedPushEntrySList`, `ReadFile`, `DeleteFileW`, `InterlockedPopEntrySList`, `VirtualFree`, `VirtualProtect`, `VirtualAlloc`, `GetVersionExW`, `LoadLibraryExW`, `FreeLibraryAndExitThread`, `GetThreadTimes`, `UnregisterWait`, `RegisterWaitForSingleObject`, `SetThreadAffinityMask`, `GetProcessAffinityMask`, `GetNumaHighestNodeNumber`, `DeleteTimerQueueTimer`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `TryEnterCriticalSection`, `GetCurrentThreadId`, `DuplicateHandle`, `GetCurrentProcess`, `SwitchToThread`, `GetCurrentThread`, `GetExitCodeThread`, `RtlPcToFileHeader`, `EncodePointer`, `GetStringTypeW`, `GetTickCount`, `GetModuleHandleW`, `GetCPInfo`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `TerminateProcess`, `IsProcessorFeaturePresent`, `IsDebuggerPresent`, `GetStartupInfoW`, `InitializeSListHead`, `GetModuleHandleExW`, `GetStdHandle`, `GetEnvironmentVariableW`, `GetFileType`, `SwitchToFiber`, `DeleteFiber`, `CreateFiber`, `FreeLibrary`, `LoadLibraryA`, `LoadLibraryW`, `ConvertFiberToThread`, `ConvertThreadToFiber`, `GetConsoleMode`, `SetConsoleMode`, `ReadConsoleA`, `ReadConsoleW`, `GetSystemTime`, `SystemTimeToFileTime`, `ResetEvent`, `OpenEventA`, `GetLogicalProcessorInformation`, `OutputDebugStringW`, `InitializeSRWLock`, `ReleaseSRWLockExclusive`, `ReleaseSRWLockShared`, `AcquireSRWLockExclusive`, `AcquireSRWLockShared`, `InitializeCriticalSection`, `ReleaseMutex`, `CreateMutexA`, `CreateThread`, `ExitThread`, `GetThreadId`, `GetEnvironmentVariableA`, `InitializeConditionVariable`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableCS`, `CreateTimerQueue`, `SignalObjectAndWait`, `SetThreadPriority`, `GetThreadPriority`, `CreateTimerQueueTimer`, `ChangeTimerQueueTimer`, `RtlUnwind`
- **USER32.dll** (13): `GetUserObjectInformationW`, `GetAsyncKeyState`, `CallNextHookEx`, `ScreenToClient`, `UnhookWindowsHookEx`, `SetWindowsHookExW`, `GetClientRect`, `GetDesktopWindow`, `EnumThreadWindows`, `GetWindowTextW`, `MessageBoxW`, `GetProcessWindowStation`, `GetTopWindow`
- **ADVAPI32.dll** (21): `ReportEventA`, `CryptEnumProvidersW`, `CryptSignHashW`, `RegisterEventSourceW`, `ReportEventW`, `CryptAcquireContextW`, `CryptReleaseContext`, `CryptDestroyKey`, `CryptSetHashParam`, `CryptGetProvParam`, `CryptGetUserKey`, `CryptExportKey`, `CryptDecrypt`, `CryptCreateHash`, `CryptDestroyHash`, `DeregisterEventSource`, `RegOpenKeyExA`, `RegisterEventSourceA`, `RegCloseKey`, `RegQueryValueExA`, `CryptGenRandom`
- **ole32.dll** (5): `CoInitializeSecurity`, `CoCreateInstance`, `CoUninitialize`, `CoInitializeEx`, `CoSetProxyBlanket`
- **OLEAUT32.dll** (10): `VariantInit`, `SafeArrayGetUBound`, `SafeArrayGetElement`, `SafeArrayDestroy`, `SysFreeString`, `SysAllocString`, `VariantCopy`, `VariantChangeType`, `SafeArrayGetLBound`, `VariantClear`
- **WS2_32.dll** (34): `getsockname`, `connect`, `WSARecv`, `WSASendTo`, `htonl`, `freeaddrinfo`, `ioctlsocket`, `setsockopt`, `WSAGetLastError`, `getaddrinfo`, `ntohs`, `ntohl`, `inet_addr`, `getnameinfo`, `WSASocketW`, `htons`, `WSASetLastError`, `recv`, `send`, `socket`, `WSAPoll`, `listen`, `shutdown`, `select`, `WSASend`, `closesocket`, `WSAIoctl`, `bind`, `WSAStartup`, `accept`, `__WSAFDIsSet`, `WSACleanup`, `WSAAddressToStringW`, `getsockopt`
- **IPHLPAPI.DLL** (2): `GetAdaptersInfo`, `GetBestInterface`
- **WTSAPI32.dll** (4): `WTSCloseServer`, `WTSOpenServerW`, `WTSEnumerateSessionsW`, `WTSFreeMemory`
- **Secur32.dll** (9): `FreeCredentialsHandle`, `InitializeSecurityContextA`, `DeleteSecurityContext`, `QueryContextAttributesA`, `QueryCredentialsAttributesA`, `FreeContextBuffer`, `EncryptMessage`, `DecryptMessage`, `AcquireCredentialsHandleA`
- **CRYPT32.dll** (7): `CertFreeCertificateContext`, `CertDuplicateCertificateContext`, `CertGetCertificateContextProperty`, `CertOpenStore`, `CertCloseStore`, `CertEnumCertificatesInStore`, `CertFindCertificateInStore`

## GMS v270 imports and exports

#### NexonAnalytics64.dll - exports (267)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | Java_nxa_DllHelper_devGetBackupDataAfterFinalizeJV | 179FD0 |  |
| 2 | Java_nxa_DllHelper_enqueueDevLogJV | 179710 |  |
| 3 | Java_nxa_DllHelper_enqueuePrimitiveLogJV | 1797E0 |  |
| 4 | Java_nxa_DllHelper_enqueueUserLogExJV | 179700 |  |
| 5 | Java_nxa_DllHelper_enqueueUserLogJV | 178B80 |  |
| 6 | Java_nxa_DllHelper_finalizeModuleJV | 177FF0 |  |
| 7 | Java_nxa_DllHelper_getCallbackPointer | 179E70 |  |
| 8 | Java_nxa_DllHelper_getCommonFieldJV | 1798A0 |  |
| 9 | Java_nxa_DllHelper_getLastErrorJV | 16B730 |  |
| 10 | Java_nxa_DllHelper_getProcessingResultJV | 179CC0 |  |
| 11 | Java_nxa_DllHelper_getSentBytesJV | 179AB0 |  |
| 12 | Java_nxa_DllHelper_getSentCountJV | 179A60 |  |
| 13 | Java_nxa_DllHelper_getServerModuleConfigString | 177640 |  |
| 14 | Java_nxa_DllHelper_getVersionJV | 179A40 |  |
| 15 | Java_nxa_DllHelper_handleDeliveryCallbackJV | 179FC0 |  |
| 16 | Java_nxa_DllHelper_initializeServerModuleJV | 177950 |  |
| 17 | Java_nxa_DllHelper_presetBaseClassVersionJV | 177810 |  |
| 18 | Java_nxa_DllHelper_presetCommonFieldFilterJV | 177740 |  |
| 19 | Java_nxa_DllHelper_registerUserJV | 178010 |  |
| 20 | Java_nxa_DllHelper_setDevLogLevelJV | 179B00 |  |
| 21 | Java_nxa_DllHelper_setExtraCommonFieldJV | 179B10 |  |
| 22 | Java_nxa_DllHelper_setServerModuleConfigString | 177550 |  |
| 23 | Java_nxa_DllHelper_setUnitTestModeJV | 179CA0 |  |
| 24 | Java_nxa_DllHelper_setUserExtraInfoJV | 178820 |  |
| 25 | Java_nxa_DllHelper_setUserExtraInfoJsonJV | 178A40 |  |
| 26 | Java_nxa_DllHelper_unregisterUserJV | 178770 |  |
| 27 | Java_nxa_DllHelper_updateUserJV | 1783C0 |  |
| 28 | assignInfoUser | 172BB0 |  |
| 29 | assignInfoUserCS | 175400 |  |
| 30 | assignInfoUserJson | 1733F0 |  |
| 31 | assignInfoUserJsonCS | 1754B0 |  |
| 32 | cJSON_AddArrayToObject | 419740 |  |
| 33 | cJSON_AddBoolToObject | 419840 |  |
| 34 | cJSON_AddFalseToObject | 419950 |  |
| 35 | cJSON_AddItemReferenceToArray | 419A50 |  |
| 36 | cJSON_AddItemReferenceToObject | 419B40 |  |
| 37 | cJSON_AddItemToArray | 4196A0 |  |
| 38 | cJSON_AddItemToObject | 419C80 |  |
| 39 | cJSON_AddItemToObjectCS | 419D60 |  |
| 40 | cJSON_AddNullToObject | 419DF0 |  |
| 41 | cJSON_AddNumberToObject | 419EF0 |  |
| 42 | cJSON_AddObjectToObject | 419FD0 |  |
| 43 | cJSON_AddRawToObject | 41A0D0 |  |
| 44 | cJSON_AddStringToObject | 41A1B0 |  |
| 45 | cJSON_AddTrueToObject | 41A290 |  |
| 46 | cJSON_Compare | 41A390 |  |
| 47 | cJSON_CreateArray | 41A6A0 |  |
| 48 | cJSON_CreateArrayReference | 41A6F0 |  |
| 49 | cJSON_CreateBool | 41A740 |  |
| 50 | cJSON_CreateDoubleArray | 41A790 |  |
| 51 | cJSON_CreateFalse | 41A880 |  |
| 52 | cJSON_CreateFloatArray | 41A8D0 |  |
| 53 | cJSON_CreateIntArray | 41A9D0 |  |
| 54 | cJSON_CreateNull | 41AAD0 |  |
| 55 | cJSON_CreateNumber | 41AB20 |  |
| 56 | cJSON_CreateObject | 41ABC0 |  |
| 57 | cJSON_CreateObjectReference | 41AC10 |  |
| 58 | cJSON_CreateRaw | 41AC60 |  |
| 59 | cJSON_CreateString | 41AD30 |  |
| 60 | cJSON_CreateStringArray | 41AE00 |  |
| 61 | cJSON_CreateStringReference | 41AEF0 |  |
| 62 | cJSON_CreateTrue | 41AF40 |  |
| 63 | cJSON_Delete | 41AF90 |  |
| 64 | cJSON_DeleteItemFromArray | 41B010 |  |
| 65 | cJSON_DeleteItemFromObject | 41B070 |  |
| 66 | cJSON_DeleteItemFromObjectCaseSensitive | 41B090 |  |
| 67 | cJSON_DetachItemFromArray | 41B0B0 |  |
| 68 | cJSON_DetachItemFromObject | 41B0F0 |  |
| 69 | cJSON_DetachItemFromObjectCaseSensitive | 41B170 |  |
| 70 | cJSON_DetachItemViaPointer | 41B200 |  |
| 71 | cJSON_Duplicate | 41B270 |  |
| 72 | cJSON_GetArrayItem | 41B410 |  |
| 73 | cJSON_GetArraySize | 41B450 |  |
| 74 | cJSON_GetErrorPtr | 41B470 |  |
| 75 | cJSON_GetNumberValue | 41B480 |  |
| 76 | cJSON_GetObjectItem | 41B4A0 |  |
| 77 | cJSON_GetObjectItemCaseSensitive | 41B4B0 |  |
| 78 | cJSON_GetStringValue | 41B4C0 |  |
| 79 | cJSON_HasObjectItem | 41B4E0 |  |
| 80 | cJSON_InitHooks | 41B500 |  |
| 81 | cJSON_InsertItemInArray | 41B590 |  |
| 82 | cJSON_IsArray | 41B640 |  |
| 83 | cJSON_IsBool | 41B650 |  |
| 84 | cJSON_IsFalse | 41B670 |  |
| 85 | cJSON_IsInvalid | 41B680 |  |
| 86 | cJSON_IsNull | 41B690 |  |
| 87 | cJSON_IsNumber | 41B6A0 |  |
| 88 | cJSON_IsObject | 41B6B0 |  |
| 89 | cJSON_IsRaw | 41B6C0 |  |
| 90 | cJSON_IsString | 41B6D0 |  |
| 91 | cJSON_IsTrue | 41B6E0 |  |
| 92 | cJSON_Minify | 41B6F0 |  |
| 93 | cJSON_Parse | 41B840 |  |
| 94 | cJSON_ParseWithLength | 41B870 |  |
| 95 | cJSON_ParseWithLengthOpts | 41B880 |  |
| 96 | cJSON_ParseWithOpts | 41BA50 |  |
| 97 | cJSON_Print | 41BA80 |  |
| 98 | cJSON_PrintBuffered | 41BAA0 |  |
| 99 | cJSON_PrintPreallocated | 41BB50 |  |
| 100 | cJSON_PrintUnformatted | 41BBC0 |  |
| 101 | cJSON_ReplaceItemInArray | 41BBD0 |  |
| 102 | cJSON_ReplaceItemInObject | 41BC10 |  |
| 103 | cJSON_ReplaceItemInObjectCaseSensitive | 41BC20 |  |
| 104 | cJSON_ReplaceItemViaPointer | 41BC30 |  |
| 105 | cJSON_SetNumberHelper | 41BCC0 |  |
| 106 | cJSON_SetValuestring | 41BD00 |  |
| 107 | cJSON_Version | 41BDE0 |  |
| 108 | cJSON_free | 41BE20 |  |
| 109 | cJSON_malloc | 41BE30 |  |
| 110 | devGetBackupDataAfterFinalize | 1738A0 |  |
| 111 | devGetBackupDataAfterFinalizeC | 16B9C0 |  |
| 112 | devGetBackupDataAfterFinalizeCS | 175DE0 |  |
| 113 | devGetBackupDataAfterFinalizeGO | 177500 |  |
| 114 | enqueueDevLog | 172A10 |  |
| 115 | enqueueDevLogC | 16B180 |  |
| 116 | enqueueDevLogCS | 175230 |  |
| 117 | enqueueDevLogGO | 176F90 |  |
| 118 | enqueueErrorLog | 171BF0 |  |
| 119 | enqueueErrorLogCS | 1747F0 |  |
| 120 | enqueueFunnelLog | 171CE0 |  |
| 121 | enqueueFunnelLogJson | 1734D0 |  |
| 122 | enqueueFunnelLogJsonCS | 175350 |  |
| 123 | enqueueInputAction | 173280 |  |
| 124 | enqueueInputActionCS | 175980 |  |
| 125 | enqueueJsonBodyLog | 172A70 |  |
| 126 | enqueueJsonBodyUserLogEx | 1735B0 |  |
| 127 | enqueueJsonLog | 1729B0 |  |
| 128 | enqueueJsonLogCS | 1751D0 |  |
| 129 | enqueueLog | 1727F0 |  |
| 130 | enqueueLogC | 16AF20 |  |
| 131 | enqueueLogCS | 174910 |  |
| 132 | enqueueLogGO | 176550 |  |
| 133 | enqueueLogOnlyOnce | 171C50 |  |
| 134 | enqueueLogOnlyOnceCS | 174860 |  |
| 135 | enqueueNxLog | 172910 |  |
| 136 | enqueuePrimitiveLog | 172B60 |  |
| 137 | enqueuePrimitiveLogC | 16B1F0 |  |
| 138 | enqueuePrimitiveLogCS | 175290 |  |
| 139 | enqueuePrimitiveLogGO | 176FF0 |  |
| 140 | enqueueStageLog | 171B90 |  |
| 141 | enqueueStageLogCS | 174780 |  |
| 142 | enqueueSystemInfoLog | 171D60 |  |
| 143 | enqueueSystemInfoLogCS | 1752F0 |  |
| 144 | enqueueSystemInfoLogJson | 173560 |  |
| 145 | enqueueUserJsonBodyLog | 171EE0 |  |
| 146 | enqueueUserLog | 171DB0 |  |
| 147 | enqueueUserLogC | 16B060 |  |
| 148 | enqueueUserLogCS | 174A50 |  |
| 149 | enqueueUserLogExCS | 1751C0 |  |
| 150 | enqueueUserLogExGO | 176F80 |  |
| 151 | enqueueUserLogGO | 176690 |  |
| 152 | enqueueUserNxLog | 171E90 |  |
| 153 | enqueueUserNxLogEx | 1727E0 |  |
| 154 | finalizeModule | 171930 |  |
| 155 | finalizeModuleC | 16AB40 |  |
| 156 | finalizeModuleCS | 174360 |  |
| 157 | finalizeModuleGO | 1761B0 |  |
| 158 | getClientModuleConfig | 170F20 |  |
| 159 | getClientModuleConfigC | 169C30 |  |
| 160 | getClientModuleConfigString | 1711A0 |  |
| 161 | getClientModuleConfigStringCS | 173B00 |  |
| 162 | getClientModuleConfigStringW | 171500 |  |
| 163 | getCommonFieldA | 172C40 |  |
| 164 | getCommonFieldAC | 16B260 |  |
| 165 | getCommonFieldCS | 175510 |  |
| 166 | getCommonFieldGO | 177050 |  |
| 167 | getCommonFieldW | 172E80 |  |
| 168 | getCommonFieldWC | 16B4E0 |  |
| 169 | getInputLogOption | 1733A0 |  |
| 170 | getInputLogOptionCS | 175BD0 |  |
| 171 | getLastError | 16B730 |  |
| 172 | getLastErrorC | 16B730 |  |
| 173 | getLastErrorCS | 16B730 |  |
| 174 | getLastErrorGO | 16B730 |  |
| 175 | getPoolAllocator | 1738C0 |  |
| 176 | getProcessingResultA | 173880 |  |
| 177 | getProcessingResultAC | 16B9A0 |  |
| 178 | getProcessingResultCS | 175CD0 |  |
| 179 | getProcessingResultGO | 1773F0 |  |
| 180 | getProcessingResultW | 173890 |  |
| 181 | getProcessingResultWC | 16B9B0 |  |
| 182 | getSentBytes | 173100 |  |
| 183 | getSentBytesC | 16B7B0 |  |
| 184 | getSentBytesCS | 1758A0 |  |
| 185 | getSentCount | 1730B0 |  |
| 186 | getSentCountC | 16B760 |  |
| 187 | getSentCountCS | 175850 |  |
| 188 | getServerModuleConfig | 170FB0 |  |
| 189 | getServerModuleConfigC | 169FE0 |  |
| 190 | getServerModuleConfigString | 1712B0 |  |
| 191 | getServerModuleConfigStringCS | 173CB0 |  |
| 192 | getServerModuleConfigStringW | 171600 |  |
| 193 | getVersion | 16B750 |  |
| 194 | getVersionC | 16B750 |  |
| 195 | getVersionCS | 1756E0 |  |
| 196 | getVersionGO | 177230 |  |
| 197 | handleDeliveryCallback | 173700 |  |
| 198 | handleDeliveryCallbackC | 16B970 |  |
| 199 | handleDeliveryCallbackCS | 175DD0 |  |
| 200 | handleDeliveryCallbackGO | 1774F0 |  |
| 201 | initializeClientModule | 171830 |  |
| 202 | initializeClientModuleC | 16A710 |  |
| 203 | initializeClientModuleCS | 173F80 |  |
| 204 | initializeServerModule | 1718B0 |  |
| 205 | initializeServerModuleC | 16A940 |  |
| 206 | initializeServerModuleCS | 174160 |  |
| 207 | initializeServerModuleGO | 175F40 |  |
| 208 | initializeServerModuleJS | 17A0C0 |  |
| 209 | inputProc | 173720 |  |
| 210 | presetBaseClassVersion | 171760 |  |
| 211 | presetBaseClassVersionC | 16A600 |  |
| 212 | presetBaseClassVersionCS | 173ED0 |  |
| 213 | presetBaseClassVersionGO | 175E90 |  |
| 214 | presetCommonFieldFilter | 171700 |  |
| 215 | presetCommonFieldFilterC | 16A580 |  |
| 216 | presetCommonFieldFilterCS | 173E60 |  |
| 217 | presetCommonFieldFilterGO | 175E20 |  |
| 218 | presetDeveloperConfig | 1717E0 |  |
| 219 | presetDeveloperConfigC | 16A6B0 |  |
| 220 | registerKeymapInfo | 173310 |  |
| 221 | registerKeymapInfoCS | 175AB0 |  |
| 222 | registerUser | 171940 |  |
| 223 | registerUserC | 16AB60 |  |
| 224 | registerUserCS | 174370 |  |
| 225 | registerUserGO | 1761C0 |  |
| 226 | setClientModuleConfig | 170F00 |  |
| 227 | setClientModuleConfigC | 1698E0 |  |
| 228 | setClientModuleConfigString | 171060 |  |
| 229 | setClientModuleConfigStringW | 1713C0 |  |
| 230 | setCurrGameState | 173210 |  |
| 231 | setCurrGameStateCS | 175900 |  |
| 232 | setDevLogLevel | 173150 |  |
| 233 | setDevLogLevelC | 16B800 |  |
| 234 | setDevLogLevelCS | 1758F0 |  |
| 235 | setDevLogLevelGO | 177320 |  |
| 236 | setExtraCommonField | 173160 |  |
| 237 | setExtraCommonFieldC | 16B810 |  |
| 238 | setExtraCommonFieldCS | 174550 |  |
| 239 | setExtraCommonFieldGO | 177330 |  |
| 240 | setInputLogLevel | 173270 |  |
| 241 | setInputLogLevelCS | 175970 |  |
| 242 | setInputLogOption | 173340 |  |
| 243 | setInputLogOptionCS | 175B70 |  |
| 244 | setServerModuleConfig | 170F10 |  |
| 245 | setServerModuleConfigC | 169A30 |  |
| 246 | setServerModuleConfigString | 171100 |  |
| 247 | setServerModuleConfigStringW | 171460 |  |
| 248 | setUnitTestMode | 173710 |  |
| 249 | setUnitTestModeC | 16B980 |  |
| 250 | setUnitTestModeCS | 175CC0 |  |
| 251 | setUnitTestModeGO | 1773E0 |  |
| 252 | setUserExtraInfo | 171AB0 |  |
| 253 | setUserExtraInfoC | 16AD40 |  |
| 254 | setUserExtraInfoCS | 1745E0 |  |
| 255 | setUserExtraInfoGO | 176390 |  |
| 256 | setUserExtraInfoJson | 173440 |  |
| 257 | setUserExtraInfoJsonC | 16AE70 |  |
| 258 | setUserExtraInfoJsonCS | 1746D0 |  |
| 259 | setUserExtraInfoJsonGO | 1764A0 |  |
| 260 | unregisterUser | 171AA0 |  |
| 261 | unregisterUserC | 16ACE0 |  |
| 262 | unregisterUserCS | 1744F0 |  |
| 263 | unregisterUserGO | 176330 |  |
| 264 | updateUser | 171940 |  |
| 265 | updateUserC | 16AB60 |  |
| 266 | updateUserCS | 174370 |  |
| 267 | updateUserGO | 1761C0 |  |

#### NexonAnalytics64.dll - imports (295 symbols)

- **KERNEL32.dll** (192): `PostQueuedCompletionStatus`, `CreateEventW`, `Sleep`, `FormatMessageW`, `SetEvent`, `TerminateThread`, `QueueUserAPC`, `LocalFree`, `DeleteCriticalSection`, `VerSetConditionMask`, `WideCharToMultiByte`, `SleepEx`, `VerifyVersionInfoW`, `FormatMessageA`, `CreateIoCompletionPort`, `CreateDirectoryW`, `FindFirstFileW`, `FindNextFileW`, `GetModuleFileNameW`, `FindClose`, `GetFileAttributesW`, `GetModuleHandleA`, `MultiByteToWideChar`, `GetDiskFreeSpaceExW`, `GetProcAddress`, `RaiseException`, `DecodePointer`, `CreateEventA`, `GetFileTime`, `MoveFileW`, `Thread32Next`, `Thread32First`, `CreateToolhelp32Snapshot`, `GetComputerNameExW`, `GetCurrentProcessId`, `GetSystemTimeAsFileTime`, `WaitForSingleObject`, `GetQueuedCompletionStatus`, `LeaveCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `WaitForMultipleObjects`, `CreateWaitableTimerW`, `EnterCriticalSection`, `SetLastError`, `SetWaitableTimer`, `WriteConsoleW`, `GetFullPathNameW`, `GetCurrentDirectoryW`, `SetStdHandle`, `FlushFileBuffers`, `SetFilePointerEx`, `UnlockFile`, `CloseHandle`, `DeleteFileW`, `GetLastError`, `CreateFileW`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetOEMCP`, `IsValidCodePage`, `FindFirstFileExW`, `GetTimeZoneInformation`, `HeapSize`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetTimeFormatW`, `GetDateFormatW`, `FlsFree`, `FlsSetValue`, `FlsGetValue`, `FlsAlloc`, `GetConsoleOutputCP`, `GetFileSizeEx`, `HeapReAlloc`, `SetEndOfFile`, `FileTimeToSystemTime`, `SystemTimeToTzSpecificLocalTime`, `PeekNamedPipe`, `GetFileInformationByHandle`, `GetDriveTypeW`, `ExitProcess`, `SetConsoleCtrlHandler`, `RtlUnwindEx`, `UnregisterWaitEx`, `QueryDepthSList`, `InterlockedFlushSList`, `InterlockedPushEntrySList`, `InterlockedPopEntrySList`, `GetVersionExW`, `LoadLibraryExW`, `FreeLibraryAndExitThread`, `GetThreadTimes`, `UnregisterWait`, `RegisterWaitForSingleObject`, `SetThreadAffinityMask`, `GetProcessAffinityMask`, `GetNumaHighestNodeNumber`, `DeleteTimerQueueTimer`, `ChangeTimerQueueTimer`, `CreateTimerQueueTimer`, `GetThreadPriority`, `SetThreadPriority`, `SignalObjectAndWait`, `CreateTimerQueue`, `SleepConditionVariableCS`, `LockFile`, `WriteFile`, `ReadFile`, `WakeAllConditionVariable`, `WakeConditionVariable`, `InitializeConditionVariable`, `GetEnvironmentVariableA`, `GetThreadId`, `ExitThread`, `CreateThread`, `CreateMutexA`, `ReleaseMutex`, `InitializeCriticalSection`, `AcquireSRWLockShared`, `AcquireSRWLockExclusive`, `ReleaseSRWLockShared`, `ReleaseSRWLockExclusive`, `InitializeSRWLock`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `TryEnterCriticalSection`, `GetCurrentThreadId`, `DuplicateHandle`, `WaitForSingleObjectEx`, `GetCurrentProcess`, `SwitchToThread`, `GetCurrentThread`, `GetExitCodeThread`, `RtlPcToFileHeader`, `EncodePointer`, `GetStringTypeW`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `GetTickCount`, `GetModuleHandleW`, `GetCPInfo`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `TerminateProcess`, `IsProcessorFeaturePresent`, `IsDebuggerPresent`, `GetStartupInfoW`, `InitializeSListHead`, `GetModuleHandleExW`, `GetSystemDirectoryA`, `FreeLibrary`, `LoadLibraryA`, `GetEnvironmentVariableW`, `VirtualAlloc`, `VirtualProtect`, `VirtualFree`, `SwitchToFiber`, `DeleteFiber`, `CreateFiber`, `GetStdHandle`, `GetFileType`, `GetACP`, `ConvertFiberToThread`, `ConvertThreadToFiber`, `LoadLibraryW`, `GetConsoleMode`, `SetConsoleMode`, `ReadConsoleA`, `ReadConsoleW`, `GetSystemTime`, `SystemTimeToFileTime`, `HeapAlloc`, `HeapFree`, `GetProcessHeap`, `ResetEvent`, `ReleaseSemaphore`, `OpenEventA`, `GetLogicalProcessorInformation`, `OutputDebugStringW`, `RtlUnwind`
- **USER32.dll** (15): `GetClientRect`, `RegisterRawInputDevices`, `UnhookWindowsHookEx`, `GetTopWindow`, `MessageBoxW`, `GetUserObjectInformationW`, `GetProcessWindowStation`, `GetRawInputData`, `GetAsyncKeyState`, `CallNextHookEx`, `ScreenToClient`, `GetDesktopWindow`, `EnumThreadWindows`, `GetWindowTextW`, `SetWindowsHookExW`
- **ADVAPI32.dll** (11): `RegisterEventSourceW`, `CryptGenRandom`, `CryptReleaseContext`, `CryptAcquireContextW`, `DeregisterEventSource`, `ReportEventA`, `RegisterEventSourceA`, `RegCloseKey`, `RegQueryValueExA`, `RegOpenKeyExA`, `ReportEventW`
- **ole32.dll** (5): `CoInitializeEx`, `CoInitializeSecurity`, `CoUninitialize`, `CoCreateInstance`, `CoSetProxyBlanket`
- **OLEAUT32.dll** (10): `VariantInit`, `SafeArrayGetUBound`, `SafeArrayGetElement`, `SafeArrayDestroy`, `SysFreeString`, `SysAllocString`, `VariantCopy`, `VariantChangeType`, `SafeArrayGetLBound`, `VariantClear`
- **WS2_32.dll** (44): `WSAStartup`, `getsockname`, `connect`, `WSARecv`, `getsockopt`, `htonl`, `ntohl`, `ioctlsocket`, `setsockopt`, `WSAGetLastError`, `inet_addr`, `WSASocketW`, `inet_ntoa`, `gethostbyaddr`, `getservbyport`, `getservbyname`, `recv`, `send`, `socket`, `freeaddrinfo`, `WSASetLastError`, `listen`, `shutdown`, `select`, `getnameinfo`, `WSAPoll`, `WSAWaitForMultipleEvents`, `getaddrinfo`, `WSASend`, `closesocket`, `WSAIoctl`, `bind`, `accept`, `__WSAFDIsSet`, `WSACleanup`, `htons`, `WSAAddressToStringW`, `ntohs`, `gethostbyname`, `WSASendTo`, `WSAEventSelect`, `WSAEnumNetworkEvents`, `WSACreateEvent`, `WSACloseEvent`
- **IPHLPAPI.DLL** (2): `GetAdaptersInfo`, `GetBestInterface`
- **WTSAPI32.dll** (4): `WTSFreeMemory`, `WTSCloseServer`, `WTSEnumerateSessionsW`, `WTSOpenServerW`
- **Secur32.dll** (9): `FreeCredentialsHandle`, `InitializeSecurityContextA`, `DeleteSecurityContext`, `QueryContextAttributesA`, `QueryCredentialsAttributesA`, `FreeContextBuffer`, `EncryptMessage`, `DecryptMessage`, `AcquireCredentialsHandleA`
- **CRYPT32.dll** (3): `CertEnumCertificatesInStore`, `CertCloseStore`, `CertOpenStore`
