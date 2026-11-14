# CrashReporter.dll - unified dossier

**GMS v270**: 824,400 bytes, linked 2024-10-07, 208 imported / 71 exported.
**MSEA**: 824,400 bytes, linked 2024-10-07, 208 imported / 71 exported.
**Status**: identical builds.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role and loading

Nexon CrashReporter is a crash-dump, exception-reporting, and screenshot support library. Its exports cover initialization, client metadata, dump controls, startup/exception reporting, and screenshot capture. `MapleStory.exe` has a normal static import of `CrashReporter_SetClientExit`, so the Windows loader loads this DLL for the game. The import table proves this one direct API dependency; it does not show which other exported operations the game calls.

Version resource: FileVersion/ProductVersion `2.0.1.36`, CompanyName `NEXON Korea`, ProductName `CrashReporter`.

### Exports

71 exports, grouped by prefix:

- `CrashReporter_*` (41): lifecycle and report APIs, including `CrashReporter_Init`, `CrashReporter_CollectNotRespondingDump`, `CrashReporter_SendStartupInfo`, `CrashReporter_SendUnityException`, `CrashReporter_SetClientExit`, and dump/privacy/server configuration setters.
- `ScreenShot_*` (30): initialization and capture APIs, including DX11/WinAPI initialization, save-path and format setters, `ScreenShot_Shoot*`, immediate/crash capture variants, and update/cleanup functions.

### Notable imports

- `WININET.dll`: `InternetConnectW`, `HttpOpenRequestW`, `HttpSendRequestExW`, `HttpEndRequestW`, and related read/write/options calls. This supports HTTP reporting capability; it does not establish a particular destination or an observed upload.
- `KERNEL32.dll`: exception filters and vectored handlers, thread/context inspection, process/module enumeration, file and mapping operations, memory allocation, and timer/thread support.
- `USER32.dll`, `GDI32.dll`, and `gdiplus.dll`: window/DC capture and bitmap/image operations, consistent with the screenshot API surface.
- `ADVAPI32.dll`: registry query/open/create/set operations.

### Embedded endpoints and product strings

- Product/version strings are in `analysis/strings/CrashReporter.strings.txt` near PE offset `0x000C2958` (`NEXON Korea`), `0x000C2998` (`CrashReporter`), and `0x000C29D4` (`2.0.1.36`).
- `NEXON Infratech CR V` and `LiveAssist_Json` occur in the string set; a PDB path identifies `CrashReporter.pdb` under a `liveassist` build directory.
- No obvious crash-service hostname was found in the extracted strings. The embedded DigiCert OCSP/CRL URLs are certificate metadata, not evidence of the crash-report destination.

### RE notes

The supplied PE inventory identifies the direct game import as `CrashReporter_SetClientExit`. Imports and export names strongly support crash handling and screenshot capture, but static data alone does not establish whether dumps are sent, what data is included, or which server receives them. Sources: `analysis/pe_inventory.json` (`CrashReporter.dll` entry and `MapleStory.exe` imports); `analysis/strings/CrashReporter.strings.txt` at the offsets above.

## GMS v270

Bit-identical to the MSEA build; everything above applies.

## MSEA imports and exports

#### CrashReporter.dll - exports (71)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | CrashReporter_ClearAdditionalCallStack | 110D0 |  |
| 2 | CrashReporter_ClearVectoredExceptionHandler | 11190 |  |
| 3 | CrashReporter_CollectNotRespondingDump | 111C0 |  |
| 4 | CrashReporter_GetInfoA | 11120 |  |
| 5 | CrashReporter_GetInfoW | 11100 |  |
| 6 | CrashReporter_Init | 10AA0 |  |
| 7 | CrashReporter_IsInit | 10AE0 |  |
| 8 | CrashReporter_PlayTimeRecorderInit | 10B00 |  |
| 9 | CrashReporter_RegisterCallback_BeforeDump | 10CA0 |  |
| 10 | CrashReporter_RegisterClientModule | 10D10 |  |
| 11 | CrashReporter_RegisterLogFile | 10B50 |  |
| 12 | CrashReporter_RegisterScreenShotFile | 10B70 |  |
| 13 | CrashReporter_RemoveBaseInfo | 110B0 |  |
| 14 | CrashReporter_SendStartupInfo | 10AF0 |  |
| 15 | CrashReporter_SendUnityException | 111D0 |  |
| 16 | CrashReporter_SetActivation | 10DA0 |  |
| 17 | CrashReporter_SetAdditionalCallStack | 110C0 |  |
| 18 | CrashReporter_SetBoolInfo | 10FB0 |  |
| 19 | CrashReporter_SetClientExit | 10D50 |  |
| 20 | CrashReporter_SetClientVersionInfo | 10E80 |  |
| 21 | CrashReporter_SetDoubleInfo | 11050 |  |
| 22 | CrashReporter_SetDumpFileName | 10DB0 |  |
| 23 | CrashReporter_SetFloatInfo | 11030 |  |
| 24 | CrashReporter_SetFullDumpPath | 10DC0 |  |
| 25 | CrashReporter_SetGatherCrashScreenShot | 10E00 |  |
| 26 | CrashReporter_SetInt64Info | 10FF0 |  |
| 27 | CrashReporter_SetIntInfo | 10FD0 |  |
| 28 | CrashReporter_SetJsonInfoA | 11090 |  |
| 29 | CrashReporter_SetJsonInfoW | 11070 |  |
| 30 | CrashReporter_SetPcRoomInfo | 10E50 |  |
| 31 | CrashReporter_SetPrivacyProtection | 10E20 |  |
| 32 | CrashReporter_SetReceiveServerRegion | 10D20 |  |
| 33 | CrashReporter_SetSSL | 10DD0 |  |
| 34 | CrashReporter_SetSendDump | 10D70 |  |
| 35 | CrashReporter_SetServerDump | 10B20 |  |
| 36 | CrashReporter_SetSkipAccessViolation | 11180 |  |
| 37 | CrashReporter_SetStrInfoA | 10EB0 |  |
| 38 | CrashReporter_SetStrInfoW | 10E90 |  |
| 39 | CrashReporter_SetUintInfo | 11010 |  |
| 40 | CrashReporter_SetVectoredExceptionHandler | 11140 |  |
| 41 | CrashReporter_UnityExceptionSenderInit | 111E0 |  |
| 42 | ScreenShot_Clear | 2BCF0 |  |
| 43 | ScreenShot_CustomCallBack | 2BD60 |  |
| 44 | ScreenShot_Custom_DrawBuffer | 2BDC0 |  |
| 45 | ScreenShot_Custom_GetBuffer | 2BD90 |  |
| 46 | ScreenShot_Destroy | 2BD10 |  |
| 47 | ScreenShot_GetDescriptionA | 2C1A0 |  |
| 48 | ScreenShot_GetDescriptionW | 2C180 |  |
| 49 | ScreenShot_GetFilePathA | 2C140 |  |
| 50 | ScreenShot_GetFilePathW | 2C100 |  |
| 51 | ScreenShot_Init | 2BAD0 |  |
| 52 | ScreenShot_InitDX11 | 2BB70 |  |
| 53 | ScreenShot_InitWinapi | 2BC20 |  |
| 54 | ScreenShot_IsInit | 2BC90 |  |
| 55 | ScreenShot_ResetPathConfig | 2C6B0 |  |
| 56 | ScreenShot_SetFormat | 2BF80 |  |
| 57 | ScreenShot_SetJpegQuality | 2C270 |  |
| 58 | ScreenShot_SetQuickCamActive | 2BFB0 |  |
| 59 | ScreenShot_SetSavePathA | 2C2D0 |  |
| 60 | ScreenShot_SetSavePathW | 2C280 |  |
| 61 | ScreenShot_SetVerifyLevel | 2C6A0 |  |
| 62 | ScreenShot_SetWindowHandle | 2BFC0 |  |
| 63 | ScreenShot_ShootA | 2C410 |  |
| 64 | ScreenShot_ShootW | 2C3C0 |  |
| 65 | ScreenShot_Shoot_7A | 2C040 |  |
| 66 | ScreenShot_Shoot_7W | 2C010 |  |
| 67 | ScreenShot_Shoot_ImmediateA | 2C510 |  |
| 68 | ScreenShot_Shoot_ImmediateW | 2C4C0 |  |
| 69 | ScreenShot_Shoot_Immediate_CrashA | 2C600 |  |
| 70 | ScreenShot_Shoot_Immediate_CrashW | 2C5C0 |  |
| 71 | ScreenShot_Update | 2BCC0 |  |

#### CrashReporter.dll - imports (208 symbols)

- **ADVAPI32.dll** (5): `RegQueryValueExW`, `RegCreateKeyExW`, `RegOpenKeyExW`, `RegSetValueExW`, `RegCloseKey`
- **KERNEL32.dll** (145): `CloseHandle`, `ReadFile`, `GetCurrentProcessId`, `GetLastError`, `OutputDebugStringW`, `GetCurrentProcess`, `K32GetModuleFileNameExW`, `LocalFree`, `GetOEMCP`, `WideCharToMultiByte`, `MultiByteToWideChar`, `GetModuleHandleW`, `GetProcAddress`, `FreeLibrary`, `LoadLibraryW`, `GetModuleFileNameW`, `CreateProcessW`, `WaitForSingleObject`, `CreateToolhelp32Snapshot`, `Thread32First`, `OpenThread`, `WriteFile`, `Thread32Next`, `RaiseException`, `GetThreadContext`, `SetUnhandledExceptionFilter`, `GetCurrentThreadId`, `AddVectoredExceptionHandler`, `RemoveVectoredExceptionHandler`, `CreateFileMappingW`, `MapViewOfFile`, `UnmapViewOfFile`, `K32GetProcessMemoryInfo`, `GlobalMemoryStatusEx`, `GetSystemDefaultUILanguage`, `GetLocaleInfoW`, `LocalAlloc`, `GetSystemInfo`, `GetComputerNameW`, `FormatMessageW`, `EnterCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `DeleteCriticalSection`, `CreateTimerQueueTimer`, `DeleteTimerQueueTimer`, `DeleteTimerQueueEx`, `CreateFileMappingA`, `CreateFileW`, `GetFileAttributesW`, `GetThreadTimes`, `SetEnvironmentVariableA`, `WriteConsoleW`, `SetEndOfFile`, `SetStdHandle`, `HeapReAlloc`, `VirtualProtect`, `VirtualFree`, `VirtualAlloc`, `GetVersionExW`, `UnregisterWaitEx`, `InitializeSListHead`, `ReleaseSemaphore`, `GetModuleHandleA`, `FreeLibraryAndExitThread`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `QueryPerformanceCounter`, `GetModuleFileNameA`, `ReadConsoleW`, `GetTimeZoneInformation`, `GetACP`, `IsValidCodePage`, `GetProcessHeap`, `FlushFileBuffers`, `SetFilePointerEx`, `GetConsoleMode`, `GetConsoleCP`, `GetFileType`, `GetStdHandle`, `HeapSize`, `GetModuleHandleExW`, `ExitProcess`, `GetTickCount`, `InitializeCriticalSection`, `Sleep`, `GetStringTypeW`, `GetSystemTimeAsFileTime`, `DuplicateHandle`, `GetCurrentThread`, `GetExitCodeThread`, `EncodePointer`, `DecodePointer`, `lstrlenA`, `IsProcessorFeaturePresent`, `HeapFree`, `IsDebuggerPresent`, `SetConsoleCtrlHandler`, `HeapAlloc`, `CreateThread`, `ExitThread`, `ResumeThread`, `GetCommandLineA`, `RtlPcToFileHeader`, `CreateTimerQueue`, `InterlockedPopEntrySList`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `QueryDepthSList`, `SetEvent`, `WaitForSingleObjectEx`, `SignalObjectAndWait`, `SwitchToThread`, `SetThreadPriority`, `GetThreadPriority`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `GetLogicalProcessorInformation`, `ChangeTimerQueueTimer`, `GetNumaHighestNodeNumber`, `GetProcessAffinityMask`, `SetThreadAffinityMask`, `RegisterWaitForSingleObject`, `UnregisterWait`, `RtlLookupFunctionEntry`, `RtlUnwindEx`, `GetCPInfo`, `RtlCaptureContext`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetLastError`, `CreateEventW`, `TerminateProcess`, `GetStartupInfoW`, `CreateSemaphoreW`, `LoadLibraryExW`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`
- **USER32.dll** (9): `MessageBoxW`, `GetWindowRect`, `GetWindowDC`, `GetClientRect`, `PrintWindow`, `GetDC`, `ReleaseDC`, `GetKeyboardLayoutList`, `GetWindowInfo`
- **SHELL32.dll** (1): `SHFileOperationW`
- **OLEAUT32.dll** (5): `VariantClear`, `SysStringLen`, `SysAllocStringByteLen`, `SysFreeString`, `SysAllocString`
- **WININET.dll** (12): `InternetWriteFile`, `InternetCloseHandle`, `InternetOpenW`, `InternetReadFile`, `InternetQueryDataAvailable`, `HttpQueryInfoA`, `HttpEndRequestW`, `HttpSendRequestExW`, `HttpAddRequestHeadersW`, `InternetSetOptionW`, `HttpOpenRequestW`, `InternetConnectW`
- **gdiplus.dll** (23): `GdiplusShutdown`, `GdipFree`, `GdipAlloc`, `GdipGetImageEncodersSize`, `GdipDisposeImage`, `GdipCreateBitmapFromHBITMAP`, `GdipGetImageEncoders`, `GdipSaveImageToFile`, `GdipCloneImage`, `GdiplusStartup`, `GdipCreateBitmapFromStream`, `GdipLoadImageFromFile`, `GdipGetPropertyItem`, `GdipGetPropertyItemSize`, `GdipSetPropertyItem`, `GdipSaveImageToStream`, `GdipGetImageHeight`, `GdipBitmapSetPixel`, `GdipGetImageWidth`, `GdipBitmapLockBits`, `GdipRemovePropertyItem`, `GdipBitmapUnlockBits`, `GdipBitmapGetPixel`
- **GDI32.dll** (7): `CreateCompatibleBitmap`, `CreateCompatibleDC`, `SelectObject`, `DeleteObject`, `DeleteDC`, `BitBlt`, `CreateDIBSection`
- **ole32.dll** (1): `CreateStreamOnHGlobal`

_GMS build bit-identical - the tables above cover both._
