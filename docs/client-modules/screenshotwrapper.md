# ScreenShotWrapper.dll - unified dossier

**GMS v270**: 560,216 bytes, linked 2023-11-16, 154 imported / 27 exported.
**MSEA**: not shipped in the audited MSEA corpus.
**Status**: GMS-only.

## MSEA build

Not shipped in the audited MSEA corpus - GMS-only module.

## GMS v270 dossier

**Target:** `<client-root>\ScreenShotWrapper.dll`.

**Role.** A Nexon-signed capture-and-save helper, not an overlay renderer: it exposes window-capture and Direct3D back-buffer screenshot paths, with GDI+/image-encoder and file-path controls. The API/RTTI names and imported APIs describe capture and persistence; they do not establish a caller in this static corpus.

### Mechanisms

- **WinAPI window capture:** `ScreenShot_InitWinapi` (VA `0x180003330`) and `ScreenShotWrapper_ShootWinapi` (`0x180002030`) are the explicit WinAPI entry points. Imports include `EnumWindows`, `GetWindowDC`, `GetWindowRect`, `GetWindowThreadProcessId`, `IsWindowVisible`, and `PrintWindow`, plus compatible GDI DC/bitmap creation.
- **Direct3D back-buffer capture:** `ScreenShot_Init` (`0x1800032F0`), `ScreenShot_Shoot` (`0x180003A90`), and `ScreenShot_Shoot_7W` (`0x180003690`) align with `ScreenshotD3DX` RTTI and strings `GetBackBuffer` and `device->GetBackBuffer(0, 0, D3DBACKBUFFER_TYPE_MONO, ...)` (string RVAs `0x65650` / `0x656E0`). This indicates a D3D back-buffer route; no static D3D import is listed.
- **Encode and save:** `ScreenShot_SetFormat` (`0x180003650`), `ScreenShot_SetJpegQuality` (`0x1800038F0`), `ScreenShot_SetSavePathW` (`0x180003900`), and `ScreenShot_ShootW` (`0x180003A40`) sit over GDI+ image encoder/save APIs. Strings include `image/jpeg`, `JpegQuality`, `SavePath`, and `VerifyLevel`.
- **Queue/custom-buffer integration:** `ScreenShot_Update` (`0x180003390`), `ScreenShot_CustomCallBack` (`0x180003430`), `ScreenShot_Custom_GetBuffer` (`0x180003460`), and `ScreenShot_Custom_DrawBuffer` (`0x180003490`) expose update/callback/buffer hooks. `SaveQueuedScreenShot`, `SaveScreenShotThread`, and `ScreenshotHandler` RTTI indicate queued/worker handling; they do not indicate an on-screen overlay.

### Exports

Image base: `0x180000000`. VA = image base + RVA.

| Ordinal | Export | RVA | VA |
|---:|---|---:|---:|
| 1 | `ScreenShotWrapper_ShootWinapi` | `0x2030` | `0x180002030` |
| 2 | `ScreenShot_Clear` | `0x33C0` | `0x1800033C0` |
| 3 | `ScreenShot_CustomCallBack` | `0x3430` | `0x180003430` |
| 4 | `ScreenShot_Custom_DrawBuffer` | `0x3490` | `0x180003490` |
| 5 | `ScreenShot_Custom_GetBuffer` | `0x3460` | `0x180003460` |
| 6 | `ScreenShot_Destroy` | `0x33E0` | `0x1800033E0` |
| 7 | `ScreenShot_GetDescriptionA` | `0x3820` | `0x180003820` |
| 8 | `ScreenShot_GetDescriptionW` | `0x3800` | `0x180003800` |
| 9 | `ScreenShot_GetFilePathA` | `0x37C0` | `0x1800037C0` |
| 10 | `ScreenShot_GetFilePathW` | `0x3780` | `0x180003780` |
| 11 | `ScreenShot_Init` | `0x32F0` | `0x1800032F0` |
| 12 | `ScreenShot_InitWinapi` | `0x3330` | `0x180003330` |
| 13 | `ScreenShot_IsInit` | `0x3360` | `0x180003360` |
| 14 | `ScreenShot_ResetPathConfig` | `0x3C50` | `0x180003C50` |
| 15 | `ScreenShot_SetFormat` | `0x3650` | `0x180003650` |
| 16 | `ScreenShot_SetJpegQuality` | `0x38F0` | `0x1800038F0` |
| 17 | `ScreenShot_SetQuickCamActive` | `0x3680` | `0x180003680` |
| 18 | `ScreenShot_SetSavePathA` | `0x3950` | `0x180003950` |
| 19 | `ScreenShot_SetSavePathW` | `0x3900` | `0x180003900` |
| 20 | `ScreenShot_SetVerifyLevel` | `0x3C40` | `0x180003C40` |
| 21 | `ScreenShot_ShootA` | `0x3A90` | `0x180003A90` |
| 22 | `ScreenShot_ShootW` | `0x3A40` | `0x180003A40` |
| 23 | `ScreenShot_Shoot_7A` | `0x36C0` | `0x1800036C0` |
| 24 | `ScreenShot_Shoot_7W` | `0x3690` | `0x180003690` |
| 25 | `ScreenShot_Shoot_ImmediateA` | `0x3B90` | `0x180003B90` |
| 26 | `ScreenShot_Shoot_ImmediateW` | `0x3B40` | `0x180003B40` |
| 27 | `ScreenShot_Update` | `0x3390` | `0x180003390` |

### Notable imports

- `USER32.dll`: `EnumWindows`, `GetWindowDC`, `GetWindowRect`, `PrintWindow`, `GetWindowThreadProcessId`, `IsWindowVisible`.
- `GDI32.dll`: `CreateCompatibleDC`, `CreateCompatibleBitmap`, `SelectObject`, `DeleteDC`.
- `gdiplus.dll`: startup/shutdown, HBITMAP-to-bitmap conversion, encoder enumeration, property/quality handling, and save-to-file/stream APIs; `ole32.dll` supplies `CreateStreamOnHGlobal`.
- Thread, timer-queue, wait, and synchronization imports support asynchronous/queued work. No overlay-composition API set is apparent.

### Embedded strings and RTTI

- RTTI includes `ScreenshotD3DX@LiveAssist`, `ScreenshotWinapi@LiveAssist`, `ScreenshotHandler@LiveAssist`, and async screenshot-info/task types.
- Capture/save traces include `ScreenShotInit Start/End`, `GetBackBuffer`, `TakeScreenShot`, `SaveQueuedScreenShot`, `SaveScreenShotThread`, `Call ScreenCapture Func`, and `Save ScreenShot FileName`.
- Format/config markers include `image/jpeg`, `JpegQuality`, `SavePath`, `VerifyLevel`, `Back buffer format`, and `Copy Buffer`.
- Embedded PDB basename: `ScreenShotWrapper.pdb` under a LiveAssist release tree. PE timestamp: `2023-11-16`. No version strings were returned by the version-resource query; the Authenticode check on the audit host reported a valid NEXON Korea Corporation signature.

### RE notes

- No module in the supplied PE import inventory statically imports `ScreenShotWrapper.dll`. `MapleStory.exe` has no direct static dependency on it either.
- `CrashReporter.dll` is an adjacent candidate, not a confirmed caller: its string dump contains many `ScreenShot_*` names and its own screenshot RTTI (including D3D11/crash-specific paths), but its import table does not name `ScreenShotWrapper.dll` and its exports include a parallel screenshot API. Those facts also fit a separately linked implementation. Dynamic loading or another caller is not excluded; caller attribution remains unresolved.
- The capture conclusion is based on imports, export names, RTTI, and strings, not decompilation or runtime observation. Source: `analysis/pe_inventory.json`, `strings/ScreenShotWrapper.strings.txt`, `strings/CrashReporter.strings.txt`, and `sections/_tables/ScreenShotWrapper.imports-exports.md`. Addresses are VAs for exports and RVAs for string-table entries.

## GMS v270 imports and exports

#### ScreenShotWrapper.dll - exports (27)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | ScreenShotWrapper_ShootWinapi | 2030 |  |
| 2 | ScreenShot_Clear | 33C0 |  |
| 3 | ScreenShot_CustomCallBack | 3430 |  |
| 4 | ScreenShot_Custom_DrawBuffer | 3490 |  |
| 5 | ScreenShot_Custom_GetBuffer | 3460 |  |
| 6 | ScreenShot_Destroy | 33E0 |  |
| 7 | ScreenShot_GetDescriptionA | 3820 |  |
| 8 | ScreenShot_GetDescriptionW | 3800 |  |
| 9 | ScreenShot_GetFilePathA | 37C0 |  |
| 10 | ScreenShot_GetFilePathW | 3780 |  |
| 11 | ScreenShot_Init | 32F0 |  |
| 12 | ScreenShot_InitWinapi | 3330 |  |
| 13 | ScreenShot_IsInit | 3360 |  |
| 14 | ScreenShot_ResetPathConfig | 3C50 |  |
| 15 | ScreenShot_SetFormat | 3650 |  |
| 16 | ScreenShot_SetJpegQuality | 38F0 |  |
| 17 | ScreenShot_SetQuickCamActive | 3680 |  |
| 18 | ScreenShot_SetSavePathA | 3950 |  |
| 19 | ScreenShot_SetSavePathW | 3900 |  |
| 20 | ScreenShot_SetVerifyLevel | 3C40 |  |
| 21 | ScreenShot_ShootA | 3A90 |  |
| 22 | ScreenShot_ShootW | 3A40 |  |
| 23 | ScreenShot_Shoot_7A | 36C0 |  |
| 24 | ScreenShot_Shoot_7W | 3690 |  |
| 25 | ScreenShot_Shoot_ImmediateA | 3B90 |  |
| 26 | ScreenShot_Shoot_ImmediateW | 3B40 |  |
| 27 | ScreenShot_Update | 3390 |  |

#### ScreenShotWrapper.dll - imports (154 symbols)

- **KERNEL32.dll** (120): `Sleep`, `LeaveCriticalSection`, `GetProcAddress`, `EnterCriticalSection`, `MultiByteToWideChar`, `GetFileAttributesW`, `GetCurrentProcessId`, `LoadLibraryW`, `SetEndOfFile`, `WriteConsoleW`, `SetStdHandle`, `CreateFileW`, `HeapReAlloc`, `InitializeCriticalSection`, `GetTickCount`, `K32GetModuleFileNameExW`, `GetCurrentProcess`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `VirtualProtect`, `VirtualFree`, `VirtualAlloc`, `GetVersionExW`, `UnregisterWaitEx`, `InitializeSListHead`, `ReleaseSemaphore`, `GetModuleHandleA`, `FreeLibraryAndExitThread`, `FreeLibrary`, `GetThreadTimes`, `OutputDebugStringW`, `GetConsoleCP`, `FlushFileBuffers`, `GetTimeZoneInformation`, `SetFilePointerEx`, `ReadConsoleW`, `GetConsoleMode`, `ReadFile`, `GetModuleFileNameW`, `WriteFile`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `QueryPerformanceCounter`, `GetCurrentThreadId`, `WideCharToMultiByte`, `GetSystemTimeAsFileTime`, `CloseHandle`, `DuplicateHandle`, `GetCurrentThread`, `DeleteCriticalSection`, `EncodePointer`, `DecodePointer`, `GetStringTypeW`, `GetCommandLineA`, `GetLastError`, `HeapFree`, `HeapAlloc`, `CreateThread`, `ExitThread`, `ResumeThread`, `RtlPcToFileHeader`, `RaiseException`, `CreateTimerQueue`, `IsProcessorFeaturePresent`, `InterlockedPopEntrySList`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `QueryDepthSList`, `SetEvent`, `WaitForSingleObjectEx`, `SignalObjectAndWait`, `SwitchToThread`, `SetThreadPriority`, `GetThreadPriority`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `GetLogicalProcessorInformation`, `CreateTimerQueueTimer`, `ChangeTimerQueueTimer`, `DeleteTimerQueueTimer`, `GetModuleHandleW`, `GetNumaHighestNodeNumber`, `GetProcessAffinityMask`, `SetThreadAffinityMask`, `RegisterWaitForSingleObject`, `UnregisterWait`, `IsDebuggerPresent`, `RtlLookupFunctionEntry`, `RtlUnwindEx`, `GetCPInfo`, `RtlCaptureContext`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `SetLastError`, `InitializeCriticalSectionAndSpinCount`, `CreateEventW`, `TerminateProcess`, `GetStartupInfoW`, `CreateSemaphoreW`, `LoadLibraryExW`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `ExitProcess`, `GetModuleHandleExW`, `HeapSize`, `GetProcessHeap`, `GetStdHandle`, `GetFileType`, `GetModuleFileNameA`, `SetEnvironmentVariableA`
- **USER32.dll** (6): `EnumWindows`, `GetWindowDC`, `GetWindowRect`, `PrintWindow`, `GetWindowThreadProcessId`, `IsWindowVisible`
- **GDI32.dll** (4): `SelectObject`, `CreateCompatibleDC`, `CreateCompatibleBitmap`, `DeleteDC`
- **ole32.dll** (1): `CreateStreamOnHGlobal`
- **gdiplus.dll** (23): `GdipSaveImageToStream`, `GdipCreateBitmapFromHBITMAP`, `GdiplusStartup`, `GdipCloneImage`, `GdipSetPropertyItem`, `GdipSaveImageToFile`, `GdipGetPropertyItemSize`, `GdipGetImageEncoders`, `GdipGetImageHeight`, `GdipGetImageEncodersSize`, `GdipAlloc`, `GdipGetPropertyItem`, `GdipLoadImageFromFile`, `GdipCreateBitmapFromStream`, `GdipFree`, `GdiplusShutdown`, `GdipBitmapSetPixel`, `GdipRemovePropertyItem`, `GdipBitmapUnlockBits`, `GdipBitmapGetPixel`, `GdipBitmapLockBits`, `GdipGetImageWidth`, `GdipDisposeImage`
