# Gr2D_DX11.dll - unified dossier

**GMS v270**: 6,249,952 bytes, linked 2026-07-20, 240 imported / 2 exported.
**MSEA**: 4,356,576 bytes, linked 2026-09-16, 185 imported / 2 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

`Gr2D_DX11.dll` is a 64-bit renderer backend in the September 16, 2026 client build. The game string inventory contains both `SID_GR2D_DX9` and `SID_GR2D_DX11`, consistent with selectable renderer backends; no DLL in the supplied PE inventory has a static import of `Gr2D_DX11.dll`, so its exact loading or COM activation path is not established. Its code and RTTI identify a D3D11 render context, texture/sprite resources, effect groups, and Nexon's `nfx` emitter, forcefield, particle, and trail systems. DirectComposition is used for a conditional window/composition path. Font and glyph parser strings are also present, but the game-facing text draw path is unclear and there is no DirectWrite import.

### Key mechanisms

Function addresses below are IDA virtual addresses for image base `0x153200000`; `sub_` labels are IDA-generated names.

1. **D3D11 device setup and capability probe.** `0x1532DA280` calls `D3D11CreateDevice` with an adapter and `D3D_DRIVER_TYPE_UNKNOWN`, then has an adapter-less fallback using an internally selected driver type. It stores the resulting device/context and continues into swap-chain setup. `0x15330EC40` is a separate probe that requests feature level `0xB000` (D3D 11.0), releases the returned device/context, and reports success or failure.

2. **DXGI factory and composition path.** `0x1532D8B30` loads `dxgi.dll`, resolves `CreateDXGIFactory1` with `GetProcAddress`, and creates/stores the factory. `0x1532E7690` and `0x1532EE7E0` also resolve the newer factory entry point, fall back to `CreateDXGIFactory`, and enumerate adapters. The initialization path at `0x1532DA280` calls `0x153288800` for swap-chain setup and logs `CreateSwapChain` on failure. `0x153288800` calls the imported `DCompositionCreateDevice` only under window/style conditions, establishing a conditional DirectComposition interop path. A specific frame `Present` call was not mapped.

3. **D3D11 state-object caches.** `0x15320F1F0` packs render-state descriptor fields, hashes them with an FNV-1a-style multiply (`0x100000001B3`), probes a global table, and creates a state object on a miss. `0x15320F580` lazily retrieves/binds the resulting blend state. Similarly, `0x15320FB10` hashes sampler parameters and creates a sampler state on a miss; `0x15320FD70` binds it through the immediate context. This verifies blend/sampler state caching, not a shader-bytecode cache.

4. **NFX particle and effect pipeline.** RTTI/vtable data associates emitter methods at `0x15323B460` and `0x15323BEF0`, a `forcefield_layer` method at `0x153249650`, an `effect_group` method at `0x15325C4E0`, and a `sprite` method at `0x15325F880`. The emitter routines use shared render-context and resource-binding helpers. Embedded HLSL includes `RWStructuredBuffer` data for particle life, particles, emitter state, and draw output; forcefield shader text names `forcefield_tex_v2` and `force_over_forcefield_v2`. Together these support an in-house particle/effect rendering pipeline, although its exact dispatch site and scheduling policy were not isolated.

### Exports

From `analysis/pe_inventory.json` (image base `0x153200000`):

| Name | Ordinal | RVA | VA | Forwarder |
| --- | ---: | ---: | ---: | --- |
| `DllCanUnloadNow` | 1 | `0xA23D0` | `0x1532A23D0` | None |
| `DllGetClassObject` | 2 | `0xA2370` | `0x1532A2370` | None |

### Notable imports

- `WINMM.dll`: `timeBeginPeriod`, `timeGetTime`, `timeEndPeriod` for timer resolution and time queries.
- `d3d11.dll`: `D3D11CreateDevice`.
- `dcomp.dll`: `DCompositionCreateDevice`.
- `KERNEL32.dll`: `LoadLibraryA/W` and `GetProcAddress` (including dynamic DXGI lookup); file and mapping APIs (`CreateFileA/W`, `ReadFile`, `CreateFileMappingA`, `MapViewOfFile`, `UnmapViewOfFile`); file enumeration; threads, timing, and synchronization APIs.
- `USER32.dll`: window/style and display interaction, including `GetWindowLongPtrA`, `PostMessageA`, `SetWindowPos`, `MonitorFromWindow`, and `IsIconic`.
- `ADVAPI32.dll`: registry open/create/query/set/delete APIs.
- `ole32.dll`: `CoCreateInstance` and COM task-memory helpers.
- `OLEAUT32.dll`: `VARIANT`, `SAFEARRAY`, and automation error-info APIs.

### RE notes

The static data shows a shared `texture_pool` ownership type, a board of shared `ID3D11ShaderResourceView` objects, and static/dynamic sprite packers with skyline packing algorithms; atlas strings include `common_atlas_texture` and `static_atlas_texture`. This supports texture reuse and atlas packing, but not a specific streaming queue, residency policy, or eviction behavior. The strings also contain `CreateTexture`, `CreateBuffer`, `CreateShader`, `Present`, `ResizeBuffers`, and `MapResource` labels; these were not tied to definitive implementation VAs. Repeated embedded HLSL compiler markers indicate shader payloads, but no shader cache implementation was identified. Font/PostScript/glyph strings may come from bundled font handling; they do not identify the game's primary text renderer. This is static analysis only: no runtime device, swap-chain, presentation, or asset-loading behavior was observed. Most implementation symbols are stripped, and the COM class IDs and activation path remain unknown. Sources: `analysis/pe_inventory.json` (`Gr2D_DX11.dll` entry); `analysis/strings/Gr2D_DX11.strings.txt`; `analysis/strings/MapleStory.strings.txt`; IDA decompilation and RTTI/vtable analysis.

## GMS v270 delta

Threadpool migration; DirectComposition dropped while classic window-management arrives.

- +KERNEL32.dll (55): `CloseThreadpoolTimer`, `CloseThreadpoolWait`, `CloseThreadpoolWork`, `CopyFileW`, `CreateDirectoryExW`, `CreateDirectoryW`, `CreateEventExW`, `CreateEventW`, `CreateHardLinkW`, `CreateSemaphoreExW`, ...(55)
- +OLEAUT32.dll (1): `SysAllocString`
- +USER32.dll (6): `AdjustWindowRect`, `GetMenu`, `GetWindowLongA`, `GetWindowRect`, `SetWindowLongA`, `UnregisterClassA`
- -KERNEL32.dll (1): `DebugBreak`
- -OLEAUT32.dll (1): `SafeArrayPutElement`
- -USER32.dll (4): `GetWindowLongPtrA`, `IntersectRect`, `MonitorFromWindow`, `ShowWindow`
- -dcomp.dll (1): `DCompositionCreateDevice`

## MSEA imports and exports

#### Gr2D_DX11.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | A23D0 |  |
| 2 | DllGetClassObject | A2370 |  |

#### Gr2D_DX11.dll - imports (185 symbols)

- **WINMM.dll** (3): `timeBeginPeriod`, `timeGetTime`, `timeEndPeriod`
- **d3d11.dll** (1): `D3D11CreateDevice`
- **KERNEL32.dll** (132): `MultiByteToWideChar`, `EncodePointer`, `DecodePointer`, `RaiseException`, `GetLastError`, `EnterCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionEx`, `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `FindResourceA`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `IsDebuggerPresent`, `DebugBreak`, `LoadLibraryW`, `InitOnceExecuteOnce`, `LocalFree`, `GetFileSizeEx`, `ReadFile`, `CloseHandle`, `HeapAlloc`, `HeapReAlloc`, `HeapFree`, `GetProcessHeap`, `MapViewOfFile`, `UnmapViewOfFile`, `CreateFileMappingA`, `GetConsoleOutputCP`, `WriteFile`, `FlushFileBuffers`, `SetStdHandle`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `SetFilePointerEx`, `DeleteCriticalSection`, `Sleep`, `GetConsoleMode`, `WriteConsoleW`, `DeleteFileW`, `SetFilePointer`, `CreateFileA`, `CreateFileW`, `GetFileType`, `GetStdHandle`, `HeapSize`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `FlsFree`, `FlsSetValue`, `FlsGetValue`, `FlsAlloc`, `GetModuleFileNameW`, `ExitProcess`, `VirtualQuery`, `VirtualProtect`, `VirtualAlloc`, `GetSystemInfo`, `GetModuleHandleExW`, `FreeLibraryAndExitThread`, `ExitThread`, `CreateThread`, `SetConsoleCtrlHandler`, `LoadLibraryExW`, `TlsFree`, `TlsSetValue`, `IsValidLocale`, `GetLocaleInfoW`, `LCMapStringW`, `InitOnceBeginInitialize`, `InitOnceComplete`, `WaitForSingleObjectEx`, `GetCurrentThreadId`, `GetExitCodeThread`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableSRW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `FormatMessageA`, `GetLocaleInfoEx`, `TlsGetValue`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `SetFileInformationByHandle`, `AreFileApisANSI`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `ReleaseSRWLockShared`, `AcquireSRWLockShared`, `GetStringTypeW`, `GetSystemTimeAsFileTime`, `CompareStringEx`, `GetCPInfo`, `LCMapStringEx`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedFlushSList`, `SetLastError`, `RtlUnwind`, `InitializeCriticalSectionAndSpinCount`, `TlsAlloc`, `CompareStringW`
- **USER32.dll** (12): `IntersectRect`, `PostMessageA`, `GetWindowLongPtrA`, `IsRectEmpty`, `CharNextA`, `ShowWindow`, `SetWindowPos`, `wsprintfW`, `IsProcessDPIAware`, `MonitorFromWindow`, `GetForegroundWindow`, `IsIconic`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (6): `CoTaskMemFree`, `CoTaskMemRealloc`, `CoCreateInstance`, `CoTaskMemAlloc`, `PropVariantClear`, `CoSetProxyBlanket`
- **OLEAUT32.dll** (22): `CreateErrorInfo`, `VariantCopyInd`, `SafeArrayPutElement`, `VariantChangeType`, `SafeArrayUnaccessData`, `SafeArrayAccessData`, `SafeArrayUnlock`, `SafeArrayLock`, `SafeArrayGetLBound`, `SafeArrayGetVartype`, `SafeArrayRedim`, `SafeArrayDestroy`, `SafeArrayCreate`, `SysFreeString`, `SysAllocStringLen`, `VarUI4FromStr`, `VariantCopy`, `VariantClear`, `VariantInit`, `SetErrorInfo`, `SafeArrayGetUBound`, `GetErrorInfo`
- **dcomp.dll** (1): `DCompositionCreateDevice`

## GMS v270 imports and exports

#### Gr2D_DX11.dll - exports (2)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | DllCanUnloadNow | 114610 |  |
| 2 | DllGetClassObject | 1145A0 |  |

#### Gr2D_DX11.dll - imports (240 symbols)

- **WINMM.dll** (3): `timeBeginPeriod`, `timeEndPeriod`, `timeGetTime`
- **d3d11.dll** (1): `D3D11CreateDevice`
- **KERNEL32.dll** (186): `MultiByteToWideChar`, `EncodePointer`, `DecodePointer`, `RaiseException`, `GetLastError`, `EnterCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionEx`, `FreeLibrary`, `GetModuleFileNameA`, `GetModuleHandleA`, `GetProcAddress`, `LoadLibraryExA`, `LoadResource`, `SizeofResource`, `lstrcmpiA`, `FindResourceA`, `WideCharToMultiByte`, `IsDBCSLeadByte`, `LoadLibraryA`, `LocalAlloc`, `FormatMessageA`, `LoadLibraryW`, `InitOnceExecuteOnce`, `CreateFileA`, `LocalFree`, `ReadFile`, `CloseHandle`, `HeapAlloc`, `HeapReAlloc`, `HeapFree`, `GetProcessHeap`, `MapViewOfFile`, `UnmapViewOfFile`, `CreateFileMappingA`, `GetTimeFormatW`, `GetConsoleMode`, `GetConsoleOutputCP`, `WriteFile`, `FlushFileBuffers`, `SetStdHandle`, `SetEnvironmentVariableW`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetOEMCP`, `GetACP`, `IsValidCodePage`, `GetTimeZoneInformation`, `HeapQueryInformation`, `DeleteCriticalSection`, `Sleep`, `ReadConsoleW`, `WriteConsoleW`, `DeleteFileW`, `SetFilePointer`, `CompareStringW`, `GetFileSizeEx`, `GetFileAttributesExW`, `GetFileType`, `GetStdHandle`, `HeapSize`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetDateFormatW`, `IsThreadAFiber`, `GetCurrentThread`, `GetModuleFileNameW`, `ExitProcess`, `VirtualQuery`, `VirtualProtect`, `VirtualAlloc`, `GetSystemInfo`, `GetModuleHandleExW`, `FreeLibraryAndExitThread`, `ResumeThread`, `ExitThread`, `CreateThread`, `SetConsoleCtrlHandler`, `LoadLibraryExW`, `TlsFree`, `TlsSetValue`, `TlsGetValue`, `TlsAlloc`, `GetLocaleInfoW`, `InitOnceBeginInitialize`, `InitOnceComplete`, `WaitForSingleObjectEx`, `SwitchToThread`, `GetCurrentThreadId`, `GetExitCodeThread`, `GetNativeSystemInfo`, `InitializeSRWLock`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryEnterCriticalSection`, `InitializeConditionVariable`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `SetCurrentDirectoryW`, `GetCurrentDirectoryW`, `CreateDirectoryW`, `CreateFileW`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `GetDiskFreeSpaceExW`, `GetFileAttributesW`, `RtlUnwind`, `GetFileInformationByHandle`, `GetFinalPathNameByHandleW`, `GetFullPathNameW`, `SetEndOfFile`, `SetFileAttributesW`, `SetFileInformationByHandle`, `SetFilePointerEx`, `SetFileTime`, `GetTempPathW`, `AreFileApisANSI`, `DeviceIoControl`, `CreateDirectoryExW`, `CopyFileW`, `MoveFileExW`, `CreateHardLinkW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `GetStringTypeW`, `ReleaseSRWLockShared`, `AcquireSRWLockShared`, `TryAcquireSRWLockExclusive`, `TryAcquireSRWLockShared`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolWork`, `SubmitThreadpoolWork`, `CloseThreadpoolWork`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `CompareStringEx`, `GetCPInfo`, `LCMapStringEx`, `GetLocaleInfoEx`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `InitializeCriticalSectionAndSpinCount`, `SetEvent`, `ResetEvent`, `CreateEventW`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlPcToFileHeader`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `SetLastError`, `LCMapStringW`
- **USER32.dll** (14): `IsRectEmpty`, `CharNextA`, `GetMenu`, `AdjustWindowRect`, `GetWindowLongA`, `wsprintfW`, `UnregisterClassA`, `IsIconic`, `IsProcessDPIAware`, `SetWindowLongA`, `GetWindowRect`, `GetForegroundWindow`, `SetWindowPos`, `PostMessageA`
- **ADVAPI32.dll** (8): `RegQueryInfoKeyW`, `RegOpenKeyExA`, `RegEnumKeyExA`, `RegDeleteValueA`, `RegDeleteKeyA`, `RegCreateKeyExA`, `RegCloseKey`, `RegSetValueExA`
- **ole32.dll** (6): `CoTaskMemRealloc`, `CoSetProxyBlanket`, `CoTaskMemFree`, `PropVariantClear`, `CoTaskMemAlloc`, `CoCreateInstance`
- **OLEAUT32.dll** (22): `CreateErrorInfo`, `VariantCopyInd`, `VariantChangeType`, `SafeArrayUnaccessData`, `SafeArrayAccessData`, `SafeArrayUnlock`, `SafeArrayLock`, `SafeArrayGetLBound`, `SafeArrayGetUBound`, `SafeArrayGetVartype`, `SafeArrayDestroy`, `SafeArrayCreate`, `SysFreeString`, `SysAllocStringLen`, `VarUI4FromStr`, `VariantCopy`, `VariantClear`, `VariantInit`, `SetErrorInfo`, `SafeArrayRedim`, `GetErrorInfo`, `SysAllocString`
