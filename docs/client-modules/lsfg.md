# LSFG.dll - unified dossier

**GMS v270**: 3,480,144 bytes, linked 2025-01-16, 79 imported / 6 exported.
**MSEA**: 3,480,144 bytes, linked 2025-01-16, 79 imported / 6 exported.
**Status**: identical builds.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role and loading

The PE version resource identifies CompanyName `THS`, ProductName/FileDescription `LSFG`, and version `2.3.0.0`. The exports `LSFG_Generate`, `LSFG_GenerateUI`, `LSFG_Process`, and `LSFG_ResizeResources`, together with embedded shader interface names such as `InputFrame1`, `InputFrame2`, `InputFlow`, `OutputFrame`, and `OutputFlow`, strongly indicate a frame-processing/interpolation or frame-generation library. The `LSFG_Library` PDB path and repeated `Microsoft (R) HLSL Shader Compiler 10.1` strings support a shader-based implementation. The exact algorithm and intended game feature are not established by these names alone.

No module in the supplied inventory statically imports `LSFG.dll`, and the `MapleStory.exe` string file has no `LSFG.dll` filename literal. Therefore, the supplied static data does not establish that MapleStory loads or uses it. It may be optional or used through a loader not represented in this inventory.

### Exports

6 exports.

| Export | Name-based interpretation |
| --- | --- |
| `LSFG_Generate` | Generate operation |
| `LSFG_GenerateUI` | UI-associated generate operation |
| `LSFG_Init` | Initialization |
| `LSFG_Process` | Processing operation |
| `LSFG_Release` | Teardown/release |
| `LSFG_ResizeResources` | Resource resizing |

### Notable imports

The import table lists only `KERNEL32.dll` (79 symbols). Relevant groups include `FindResourceW`/`LoadResource`/`LockResource`, `LoadLibraryExW`/`GetProcAddress`, and file, memory, and runtime support. No Direct3D or DXGI import is present. This could mean graphics interfaces are supplied by a caller, resolved indirectly, or otherwise hidden from the static import table; the import list does not identify which.

### Embedded endpoints and product strings

- No application endpoint or network API import was identified.
- `\Work\LSFG_Library\x64\Release\LSFG.pdb` occurs at PE offset `0x0001E2C3`.
- Shader-related strings include `InputFrame1`, `InputFlow`, `OutputFrame`, and `ResolutionInvScale`; HLSL compiler markers recur throughout the string file.
- Version strings include `2.3.0.0` at `0x00023504`; CompanyName `THS` is present in the PE version resource.

### RE notes

The best-supported classification is frame-generation/interpolation support, with medium-high confidence from the exported verb set and frame/flow shader interfaces. The most important audit caveat is load provenance: unlike the four DLLs with direct `MapleStory.exe` import edges, LSFG has no static link or filename reference in the supplied game evidence. Sources: `analysis/pe_inventory.json` (`LSFG.dll` entry and all-module imports); `analysis/strings/LSFG.strings.txt` at the offsets above.

## GMS v270

Bit-identical to the MSEA build; everything above applies.

## MSEA imports and exports

#### LSFG.dll - exports (6)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | LSFG_Generate | 6F50 |  |
| 2 | LSFG_GenerateUI | 6F80 |  |
| 3 | LSFG_Init | 6C80 |  |
| 4 | LSFG_Process | 6F20 |  |
| 5 | LSFG_Release | 6FB0 |  |
| 6 | LSFG_ResizeResources | 6E80 |  |

#### LSFG.dll - imports (79 symbols)

- **KERNEL32.dll** (79): `FindResourceW`, `GetLastError`, `LoadResource`, `SizeofResource`, `LockResource`, `FreeResource`, `WriteConsoleW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `QueryPerformanceCounter`, `GetCurrentProcessId`, `GetCurrentThreadId`, `GetSystemTimeAsFileTime`, `InitializeSListHead`, `IsDebuggerPresent`, `GetStartupInfoW`, `GetModuleHandleW`, `RtlUnwindEx`, `RtlPcToFileHeader`, `RaiseException`, `InterlockedFlushSList`, `SetLastError`, `EncodePointer`, `EnterCriticalSection`, `LeaveCriticalSection`, `DeleteCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `FreeLibrary`, `GetProcAddress`, `LoadLibraryExW`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameW`, `HeapAlloc`, `HeapFree`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `IsValidCodePage`, `GetACP`, `GetOEMCP`, `GetCPInfo`, `GetCommandLineA`, `GetCommandLineW`, `MultiByteToWideChar`, `WideCharToMultiByte`, `GetEnvironmentStringsW`, `FreeEnvironmentStringsW`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `InitializeCriticalSectionEx`, `VirtualProtect`, `LCMapStringW`, `GetProcessHeap`, `GetStdHandle`, `GetFileType`, `GetStringTypeW`, `HeapSize`, `HeapReAlloc`, `SetStdHandle`, `FlushFileBuffers`, `WriteFile`, `GetConsoleOutputCP`, `GetConsoleMode`, `SetFilePointerEx`, `CreateFileW`, `CloseHandle`

_GMS build bit-identical - the tables above cover both._
