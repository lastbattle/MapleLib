# NxOverlay_x64.dll - unified dossier

**GMS v270**: 478,192 bytes, linked 2026-04-15, 144 imported / 18 exported.
**MSEA**: 478,200 bytes, linked 2025-10-13, 144 imported / 16 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Identity and role

Target: `<client-root>\NxOverlay_x64.dll`. PE32+ x64 (`0x8664`), image base `0x180000000`, PE timestamp `2025-10-13`, 478,200 bytes, 16 exports, 144 imports, and no delay imports of its own.

The evidence supports a Nexon `LiveAssist` embedded-browser overlay bridge. `MapleStory.exe` delay-loads `NXOVERLAY_X64.dll` with the `EnableChangeCursor` entry (EXE location about `0x0972E31A`); that named export is one cursor-control setting, not the DLL's whole role. The other exports and internal strings cover browser lifecycle, cookies, DirectX initialization, browser audio/GPU settings, and update/cleanup. The adjacent `NxOverlay` package contains `DwarfAxe.exe` and Chromium runtime files. The DLL builds and launches a `DwarfAxe.exe` process and communicates with it through pipe-based IPC, while moving browser textures through D3D9/D3D11 handlers. This verifies a browser UI integration; it does not identify a streaming provider or prove what web content is displayed at runtime.

### Key mechanisms

#### Exported browser and custom `LiveAssist` layer

- Export ordinal 1, VA `0x180009A40`, lazily initializes with the literal path `.` plus `Dwarf_x64` (`.\Dwarf_x64`) and routes arguments to the `StartBrowser` wrapper at `0x1800114A0`, then manager routine `0x180015E50`. The manager logs a D3D version/device, URL, dimensions, and callback.
- Export ordinal 2, VA `0x1800099E0`, initializes the `LiveAssist`/Dwarf manager through `0x180011250` and `0x1800126F0`.
- Export ordinal 3, VA `0x180009AB0`, gates `PreloadBrowser` at `0x1800112E0` / `0x180015660`.
- Exports 4 and 5 (VAs `0x180009AC0` and `0x180009AF0`) route to `StopBrowser_ByBrowserId` at `0x180011560` / `0x1800161B0`. Ordinal 4 first obtains an ID from an interface vtable slot; ordinal 5 is the direct ID form.
- Export ordinal 6, VA `0x180009B00`, maps to `GetDwarfError` at `0x180011190`; ordinal 7, VA `0x180009A00`, maps to `TerminateDwarf` at `0x1800115D0`.
- Export ordinal 8, VA `0x180009B10`, maps to `ClearFocusBrowser` at `0x1800110C0`; ordinal 9, VA `0x180009A30`, maps to `UpdateDwarf` at `0x180011630` / `0x1800167E0`.
- Export ordinals 10 and 11 (VAs `0x180009B20` and `0x180009B30`) map to `SetCookie` (`0x180011360` / `0x180015950`) and `DeleteCookies` (`0x180011120` / `0x180012300`). The internal command surface also includes URL/authentication token, JavaScript, resize, reload, JS-dialog, mute/volume, and browser-finish callbacks.

These are custom C++ `LiveAssist`/`Dwarf` components around the browser engine: `WebBrowserClientManager`, `DwarfCommander`, `DwarfWebBrowser`, `DwarfProcess`, and `DwarfIPCClient` names are present in strings/RTTI. The DLL imports no CEF API directly. Its process-launch and IPC code, plus the colocated CEF payload, point to a helper-process boundary rather than an in-process CEF API surface.

#### CEF/helper-process boundary

- `InitializeDwarfAxeProcess` is at `0x180012F30`; it constructs a `DwarfAxe.exe` path and calls `DwarfProcess::Execute` at `0x1800201E0`. The latter uses `CreateProcessW`.
- `DwarfIPCClient::Initialize` is at `0x18000C1A0`; it opens/retries a pipe endpoint with `CreateFileW` and `WaitNamedPipeW`. `SendInitialMessage` at `0x18000C370` creates a pipe and duplicates a handle. `SendCommand` / `SendCommandInternal` at `0x18000C5B0` / `0x18000C6B0` write a command header and payload.
- The installed `NxOverlay` directory contains `DwarfAxe.exe`, `libcef.dll`, `chrome_elf.dll`, `resources.pak`, `chrome_100_percent.pak`, `chrome_200_percent.pak`, `locales`, `icudtl.dat`, `snapshot_blob.bin`, `v8_context_snapshot.bin`, `libEGL.dll`, `libGLESv2.dll`, `d3dcompiler_47.dll`, and Vulkan/SwiftShader files. Together with the launch path this is strong package-level evidence for a Chromium-backed helper, though module loading was not observed at runtime.

#### Input, IME, and cursor integration

- Export `EnableChangeCursor` is at `0x180009B70`; its implementation `0x1800113F0` delegates to `0x180015E20`, which stores a boolean setting. In the browser-manager callback at `0x1800141C0`, when that setting is enabled, a browser cursor callback uses `GetCursorPos`, `WindowFromPoint`, `SetClassLongPtrW` (class cursor slot), and `SetCursor`.
- Browser input is sent through `DwarfCommander::SendInputEvent` (`0x18001D160`) and `DwarfWebBrowser::SendInputEvent` (`0x18001F510`); the latter posts window messages to a browser HWND with `PostMessageW`. IME commit/composition/cancel has dedicated command/browser methods and uses the IMM32 API family.
- Initialization at `0x1800126F0` obtains the active window's default IME window and sets its window procedure with `SetWindowLongPtrW(..., nIndex = -4, ...)`. This is scoped window-procedure integration, not evidence of a process-wide keyboard/mouse hook. `SetWindowsHookEx` is absent from the static import table.

#### Rendering and browser-texture path

- Export `InitNxOverlayDirectX` at `0x180009B80` reaches `InitNxOverlayDirectX_0` (`0x180011200`) and manager initialization at `0x180012F20`.
- RTTI and string references identify both `DirectX9Handler` and `DirectX11Handler`. The D3D9 path initializes at `0x180019BD0`, imports a shared browser texture at `0x18001AF50`, and updates/copies textures at `0x18001BAF0`; strings name `D3DXLoadSurfaceFromSurface`, shared-handle texture creation, and sprite support. The D3D11 shared-handle path is at `0x18001C3D0` and reports `CreateShaderResourceView` failures.
- `DwarfWebBrowser::UpdateTexture` is at `0x1800200A0`. Start-browser diagnostics carry a D3D version and device pointer, and the handler code consumes shared texture handles. The observed rendering design is a browser-surface-to-game-device texture bridge; D3D9/D3DX is loaded dynamically (`d3d9.dll`, `d3dx9_43.dll`, `GetProcAddress`) rather than represented by direct D3D9 imports.
- The remaining named controls include `UseGPUInWeb` at `0x180009B50` / `0x180011690`, `SetAudioVolume` at `0x180009B60` / `0x180011440`, and `UseModalDialog` at `0x180009B40` / `0x1800116E0`.

### Exports

VA is preferred image base plus RVA. The PE export directory names only four of the 16 entries; internal purpose labels below are mapped from decompiler flow and `LiveAssist` string cross-references, not export-name metadata.

| Ordinal | Export name | VA (RVA) | Mapped purpose |
|---:|---|---|---|
| 1 | ordinal-only | `0x180009A40` (`0x9A40`) | Lazy init plus browser start |
| 2 | ordinal-only | `0x1800099E0` (`0x99E0`) | Initialize Dwarf/manager |
| 3 | ordinal-only | `0x180009AB0` (`0x9AB0`) | Preload browser |
| 4 | ordinal-only | `0x180009AC0` (`0x9AC0`) | Browser-object to ID stop adapter |
| 5 | ordinal-only | `0x180009AF0` (`0x9AF0`) | Stop browser by ID |
| 6 | ordinal-only | `0x180009B00` (`0x9B00`) | Get Dwarf error/status |
| 7 | ordinal-only | `0x180009A00` (`0x9A00`) | Terminate Dwarf |
| 8 | ordinal-only | `0x180009B10` (`0x9B10`) | Clear focused browser |
| 9 | ordinal-only | `0x180009A30` (`0x9A30`) | Update Dwarf |
| 10 | ordinal-only | `0x180009B20` (`0x9B20`) | Set browser cookie |
| 11 | ordinal-only | `0x180009B30` (`0x9B30`) | Delete browser cookies |
| 12 | `EnableChangeCursor` | `0x180009B70` (`0x9B70`) | Enable browser cursor changes |
| 13 | ordinal-only | `0x180009B40` (`0x9B40`) | Use modal-dialog setting |
| 14 | `InitNxOverlayDirectX` | `0x180009B80` (`0x9B80`) | Initialize DirectX bridge |
| 15 | `SetAudioVolume` | `0x180009B60` (`0x9B60`) | Set browser audio volume |
| 16 | `UseGPUInWeb` | `0x180009B50` (`0x9B50`) | Set browser GPU-use option |

### Notable imports by DLL

Counts are from the supplied PE inventory; selected symbols below are the mechanisms relevant to this dossier, not a reprint of every CRT/runtime import.

- `KERNEL32.dll` (100): `CreateProcessW`, `CreatePipe`, `DuplicateHandle`, `CreateFileW`, `WaitNamedPipeW`, `ReadFile`, `WriteFile`, `CreateEventW`, `CreateThread`, `WaitForSingleObject`, `CreateToolhelp32Snapshot`, `Process32FirstW`, `Process32NextW`, `GetModuleFileNameA/W`, `LoadLibraryA`, `LoadLibraryExW`, `GetProcAddress`, `GetExitCodeProcess`, `TerminateProcess`.
- `USER32.dll` (25): `RegisterClassW`, `CreateWindowExW`, `SetWindowLongPtrW`, `SetClassLongPtrW`, `GetActiveWindow`, `GetFocus`, `GetClientRect`, `GetWindowRect`, `GetWindowThreadProcessId`, `EnumWindows`, `GetClassNameW`, `IsWindow`, `WindowFromPoint`, `GetCursorPos`, `SetCursor`, `PostMessageW`, `SetWindowPos`, `GetDC`, `PrintWindow`, `DefWindowProcW`, and caret APIs.
- `IMM32.dll` (8): `ImmGetContext`, `ImmReleaseContext`, `ImmGetDefaultIMEWnd`, `ImmAssociateContextEx`, `ImmGetCompositionStringW`, `ImmNotifyIME`, `ImmSetCandidateWindow`, `ImmSetCompositionWindow`.
- `dxgi.dll` (1): `CreateDXGIFactory1`.
- `GDI32.dll` (4): `GetDIBits`, `CreateCompatibleDC`, `CreateCompatibleBitmap`, `SelectObject`.
- `ADVAPI32.dll` (4): `RegOpenKeyExW`, `RegQueryValueExW/A`, `RegCloseKey`; strings include `SOFTWARE\Nexon\NxOverlay` and `SOFTWARE\Nexon\Dwarf`.
- `SHLWAPI.dll` (2): `StrCmpW`, `PathFindFileNameA`.

### RE notes

- The central classification is well supported by the export mappings, `LiveAssist` strings/RTTI, child-process/IPC code, and installed Chromium runtime files. The word `Overlay` is also present in package strings. A specific streaming service, account workflow, or actual page content is not established by these static artifacts.
- There is a path discrepancy to resolve with runtime evidence: ordinal 1 passes the literal `.\Dwarf_x64` to initialization, and manager code constructs a `DwarfAxe.exe` path; the inspected install has `NxOverlay\DwarfAxe.exe` and no sibling `Dwarf_x64` directory at the install root. The exact runtime path mapping and successful launch remain unknown.
- Internal VAs are IDA pseudocode observations at the preferred base. Most exports are unnamed ordinals, so their public ABI and parameter types remain uncertain despite the evidence-backed purpose mapping.
- Static analysis does not establish which browser URLs are requested, whether GPU mode is selected, whether DwarfAxe successfully starts, or whether the UI is visible during a game session.

### Evidence used

- Supplied inventory: `analysis/pe_inventory.json`, key `NxOverlay_x64.dll`.
- Supplied strings: `analysis/strings/NxOverlay_x64.strings.txt`; its displayed offsets are file offsets, not RVAs. IDA addresses in this dossier are VAs at the preferred image base.
- Existing EXE context: `sections/exe-context.md`, delay-load pair near `0x0972E31A`.
- Read-only IDA decompilation/xrefs for the target DLL and a directory listing of the installed `NxOverlay` package. No runtime execution was performed.

## GMS v270 delta

Same imports; two additional exports.

_none._

## MSEA imports and exports

#### NxOverlay_x64.dll - exports (16)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | - | 9A40 |  |
| 2 | - | 99E0 |  |
| 3 | - | 9AB0 |  |
| 4 | - | 9AC0 |  |
| 5 | - | 9AF0 |  |
| 6 | - | 9B00 |  |
| 7 | - | 9A00 |  |
| 8 | - | 9B10 |  |
| 9 | - | 9A30 |  |
| 10 | - | 9B20 |  |
| 11 | - | 9B30 |  |
| 12 | EnableChangeCursor | 9B70 |  |
| 13 | - | 9B40 |  |
| 14 | InitNxOverlayDirectX | 9B80 |  |
| 15 | SetAudioVolume | 9B60 |  |
| 16 | UseGPUInWeb | 9B50 |  |

#### NxOverlay_x64.dll - imports (144 symbols)

- **IMM32.dll** (8): `ImmSetCandidateWindow`, `ImmSetCompositionWindow`, `ImmNotifyIME`, `ImmGetCompositionStringW`, `ImmAssociateContextEx`, `ImmGetDefaultIMEWnd`, `ImmReleaseContext`, `ImmGetContext`
- **dxgi.dll** (1): `CreateDXGIFactory1`
- **KERNEL32.dll** (100): `GetTimeZoneInformation`, `HeapReAlloc`, `HeapSize`, `FindFirstFileExA`, `ReadConsoleW`, `SetFilePointerEx`, `GetConsoleMode`, `FindClose`, `FindNextFileA`, `GetModuleFileNameA`, `GetLastError`, `TerminateProcess`, `QueryPerformanceCounter`, `EnterCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `DeleteCriticalSection`, `Sleep`, `GetCurrentProcess`, `InitializeCriticalSectionEx`, `RaiseException`, `DecodePointer`, `GetModuleHandleW`, `GetCurrentProcessId`, `CreateThread`, `WaitForSingleObject`, `CreateFileW`, `CloseHandle`, `GetModuleFileNameW`, `CreateToolhelp32Snapshot`, `Process32FirstW`, `Process32NextW`, `FreeLibrary`, `IsValidCodePage`, `LoadLibraryA`, `DuplicateHandle`, `GetExitCodeProcess`, `CreateProcessW`, `WaitNamedPipeW`, `CreatePipe`, `WriteFile`, `CreateEventW`, `ReadFile`, `GetConsoleCP`, `FlushFileBuffers`, `GetACP`, `GetFileType`, `GetStdHandle`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetLocaleInfoW`, `LCMapStringW`, `CompareStringW`, `HeapAlloc`, `HeapFree`, `ExitProcess`, `GetModuleHandleExW`, `LoadLibraryExW`, `TlsFree`, `TlsSetValue`, `TlsGetValue`, `TlsAlloc`, `SetLastError`, `RtlPcToFileHeader`, `InterlockedFlushSList`, `RtlUnwindEx`, `InitializeSListHead`, `GetStartupInfoW`, `IsProcessorFeaturePresent`, `SetUnhandledExceptionFilter`, `UnhandledExceptionFilter`, `RtlVirtualUnwind`, `RtlLookupFunctionEntry`, `RtlCaptureContext`, `ResetEvent`, `SetEvent`, `OutputDebugStringW`, `IsDebuggerPresent`, `GetCPInfo`, `GetOEMCP`, `GetCommandLineA`, `GetCommandLineW`, `GetEnvironmentStringsW`, `FreeEnvironmentStringsW`, `SetEnvironmentVariableA`, `RtlUnwind`, `GetProcessHeap`, `SetStdHandle`, `WriteConsoleW`, `SetEndOfFile`, `GetProcAddress`, `GetCurrentThreadId`, `MultiByteToWideChar`, `WideCharToMultiByte`, `WaitForSingleObjectEx`, `GetStringTypeW`, `EncodePointer`, `LCMapStringEx`, `GetSystemTimeAsFileTime`
- **USER32.dll** (25): `DefWindowProcW`, `SetWindowPos`, `SetCaretPos`, `DestroyCaret`, `CreateCaret`, `GetFocus`, `GetKeyboardLayoutNameW`, `GetWindowRect`, `IsWindow`, `PostMessageW`, `RegisterClassW`, `GetWindowThreadProcessId`, `GetClassNameW`, `EnumWindows`, `GetDC`, `PrintWindow`, `SetClassLongPtrW`, `SetWindowLongPtrW`, `WindowFromPoint`, `GetCursorPos`, `SetCursor`, `GetActiveWindow`, `DestroyWindow`, `GetClientRect`, `CreateWindowExW`
- **GDI32.dll** (4): `GetDIBits`, `CreateCompatibleDC`, `CreateCompatibleBitmap`, `SelectObject`
- **ADVAPI32.dll** (4): `RegQueryValueExW`, `RegOpenKeyExW`, `RegCloseKey`, `RegQueryValueExA`
- **SHLWAPI.dll** (2): `StrCmpW`, `PathFindFileNameA`

## GMS v270 imports and exports

#### NxOverlay_x64.dll - exports (18)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | - | 9A40 |  |
| 2 | - | 99E0 |  |
| 3 | - | 9AB0 |  |
| 4 | - | 9AC0 |  |
| 5 | - | 9AF0 |  |
| 6 | - | 9B00 |  |
| 7 | - | 9A00 |  |
| 8 | - | 9B10 |  |
| 9 | - | 9A30 |  |
| 10 | - | 9B20 |  |
| 11 | - | 9B30 |  |
| 12 | EnableChangeCursor | 9B70 |  |
| 13 | - | 9B40 |  |
| 14 | EnableKorean | 9B90 |  |
| 15 | InitNxOverlayDirectX | 9B80 |  |
| 16 | IsKoreanEnabled | 9BA0 |  |
| 17 | SetAudioVolume | 9B60 |  |
| 18 | UseGPUInWeb | 9B50 |  |

#### NxOverlay_x64.dll - imports (144 symbols)

- **IMM32.dll** (8): `ImmSetCandidateWindow`, `ImmSetCompositionWindow`, `ImmNotifyIME`, `ImmGetCompositionStringW`, `ImmAssociateContextEx`, `ImmGetDefaultIMEWnd`, `ImmReleaseContext`, `ImmGetContext`
- **dxgi.dll** (1): `CreateDXGIFactory1`
- **KERNEL32.dll** (100): `GetTimeZoneInformation`, `HeapReAlloc`, `HeapSize`, `FindFirstFileExA`, `ReadConsoleW`, `SetFilePointerEx`, `GetConsoleMode`, `FindClose`, `FindNextFileA`, `GetModuleFileNameA`, `GetLastError`, `TerminateProcess`, `QueryPerformanceCounter`, `EnterCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `DeleteCriticalSection`, `Sleep`, `GetCurrentProcess`, `InitializeCriticalSectionEx`, `RaiseException`, `DecodePointer`, `GetModuleHandleW`, `GetCurrentProcessId`, `CreateThread`, `WaitForSingleObject`, `CreateFileW`, `CloseHandle`, `GetModuleFileNameW`, `CreateToolhelp32Snapshot`, `Process32FirstW`, `Process32NextW`, `FreeLibrary`, `GetProcAddress`, `IsValidCodePage`, `WideCharToMultiByte`, `DuplicateHandle`, `GetExitCodeProcess`, `CreateProcessW`, `WaitNamedPipeW`, `CreatePipe`, `WriteFile`, `CreateEventW`, `ReadFile`, `GetConsoleCP`, `FlushFileBuffers`, `GetACP`, `GetFileType`, `GetStdHandle`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetLocaleInfoW`, `LCMapStringW`, `CompareStringW`, `HeapAlloc`, `HeapFree`, `ExitProcess`, `GetModuleHandleExW`, `LoadLibraryExW`, `TlsFree`, `TlsSetValue`, `TlsGetValue`, `TlsAlloc`, `SetLastError`, `RtlPcToFileHeader`, `InterlockedFlushSList`, `RtlUnwindEx`, `InitializeSListHead`, `GetStartupInfoW`, `IsProcessorFeaturePresent`, `SetUnhandledExceptionFilter`, `UnhandledExceptionFilter`, `RtlVirtualUnwind`, `RtlLookupFunctionEntry`, `RtlCaptureContext`, `ResetEvent`, `SetEvent`, `OutputDebugStringW`, `IsDebuggerPresent`, `GetCPInfo`, `GetOEMCP`, `GetCommandLineA`, `GetCommandLineW`, `GetEnvironmentStringsW`, `FreeEnvironmentStringsW`, `SetEnvironmentVariableA`, `RtlUnwind`, `GetProcessHeap`, `SetStdHandle`, `WriteConsoleW`, `SetEndOfFile`, `LoadLibraryA`, `GetCurrentThreadId`, `MultiByteToWideChar`, `WaitForSingleObjectEx`, `GetStringTypeW`, `EncodePointer`, `LCMapStringEx`, `GetSystemTimeAsFileTime`
- **USER32.dll** (25): `DefWindowProcW`, `SetWindowPos`, `SetCaretPos`, `DestroyCaret`, `CreateCaret`, `GetFocus`, `GetKeyboardLayoutNameW`, `GetWindowRect`, `IsWindow`, `PostMessageW`, `RegisterClassW`, `GetWindowThreadProcessId`, `GetClassNameW`, `EnumWindows`, `GetDC`, `PrintWindow`, `SetClassLongPtrW`, `SetWindowLongPtrW`, `WindowFromPoint`, `GetCursorPos`, `SetCursor`, `GetActiveWindow`, `DestroyWindow`, `GetClientRect`, `CreateWindowExW`
- **GDI32.dll** (4): `GetDIBits`, `CreateCompatibleDC`, `CreateCompatibleBitmap`, `SelectObject`
- **ADVAPI32.dll** (4): `RegQueryValueExW`, `RegOpenKeyExW`, `RegCloseKey`, `RegQueryValueExA`
- **SHLWAPI.dll** (2): `StrCmpW`, `PathFindFileNameA`
