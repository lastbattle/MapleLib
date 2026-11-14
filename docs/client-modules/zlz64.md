# ZLZ64.dll - unified dossier

**GMS v270**: 601,568 bytes, linked 2026-07-20, 124 imported / 3 exported.
**MSEA**: 611,808 bytes, linked 2026-09-16, 117 imported / 3 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

ZLZ64 provides `ZIStream` compression filters backed by statically linked zlib 1.2.5. The core codec is DEFLATE with the standard 32 KiB window; a nonzero factory mode adds length-prefixed payload records and an AES-OFB-style byte transform around the compressed stream. This is a stream filter, not evidence of a separate LZ-family codec.

### Key mechanisms

- **Codec configuration:** `ZLZCreateDeflator` (`0x18000ED60`) initializes zlib through `0x180006070` with method `8` (DEFLATED), `windowBits=15`, `memLevel=8`, and strategy `0` (`Z_DEFAULT_STRATEGY`). Thus the codec uses a 32 KiB history window and the zlib wrapper, rather than raw DEFLATE or gzip. The requested compression level is clamped to `0..9`; negative values become zlib's default (`-1`). `ZLZCreateInflator` (`0x18000EE60`) calls the version-checked `inflateInit_` path for zlib `1.2.5`, whose default window is also 15.
- **Stream and flush behavior:** the deflator feeds `deflate(..., 0)` for ordinary writes and uses flush value `2` (`Z_SYNC_FLUSH`) when the filter flushes (`0x18000EB10`, `0x18000E2F0`). The inflate loop at `0x18000E700` consumes the same persistent zlib stream state across input buffers. Both sides use recyclable 64 KiB buffers (`0x18000EF40`); unframed mode offers up to `0x10000` bytes per buffer.
- **Optional record framing:** the first factory argument is stored as a mode flag. With mode `0`, compressed bytes go to/from the wrapped stream directly. With a nonzero mode, the deflator's output buffer is `0xFFFF` bytes and `0x18000E420` emits each nonempty compressed segment as a 4-byte little-endian length followed by that many transformed bytes. The inflator path at `0x18000E700` reads the length, reads that payload, reverses the transform, then supplies it to zlib. The inflator's third argument is a compressed-input byte budget; zero is converted to `0xFFFFFFFF` (unbounded), and framed reads decrement it by the 4-byte header plus payload length.
- **Per-record AES-OFB-style transform:** `0x180003DA0` applies the write-side transform and `0x180003C80` reverses it; their block loops (`0x180004160` and `0x180003F30`) repeatedly AES-encrypt a mutable 16-byte state and XOR the resulting block with the data. `0x180004310` expands the 32-byte key at `0x180089030` using AES-style S-box/Rcon tables; the per-record state is initialized by repeating the little-endian dword at `0x180089010` (`0xE9637DB9`) across 16 bytes. The state and key are reinitialized for each framed payload, so this transform is per-record rather than continuous across record boundaries.

### Exports

| Name | Ordinal | VA |
| --- | ---: | ---: |
| `?ZLZCloseFilter@@YAXPEAUZIStream@@@Z` | 1 | `0x18000ED30` |
| `?ZLZCreateDeflator@@YAPEAUZIStream@@HPEAU1@H@Z` | 2 | `0x18000ED60` |
| `?ZLZCreateInflator@@YAPEAUZIStream@@HPEAU1@I@Z` | 3 | `0x18000EE60` |

### Notable imports by DLL

- `KERNEL32.dll`: `LoadLibraryA`, `GetProcAddress`, and `FreeLibrary`; heap and recyclable-buffer allocation; file/stream, synchronization, and thread-pool support. The inventory shows no external zlib DLL import.

### RE notes

- Inventory identity: x64 PE, timestamp `2026-09-16`, image base `0x180000000`.
- Strings identify both `deflate 1.2.5` and `inflate 1.2.5`; RTTI identifies `ZFilter`, `ZDeflator`, `ZInflator`, and `ZAllocBase`.
- Framed mode is sufficient to describe the wrapper shape for a decompressor: read a 32-bit length, transform exactly that payload with a fresh OFB state, then feed the resulting bytes into one zlib inflate stream. Valid writer records are at most `0xFFFF` bytes. The AES constants and state initialization above are static-code findings; no external sample was available here for a round-trip test vector.
- Export names are MSVC-decorated. The friendly names in the binary are `ZLZCloseFilter`, `ZLZCreateDeflator`, and `ZLZCreateInflator`.

## GMS v270 delta

Threadpool family plus VirtualProtect.

- +KERNEL32.dll (11): `CloseThreadpoolWork`, `CreateThreadpoolWork`, `InitializeConditionVariable`, `InitializeSRWLock`, `IsThreadAFiber`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `SubmitThreadpoolWork`, `VirtualProtect`, `WakeAllConditionVariable`, ...(11)
- -KERNEL32.dll (4): `FormatMessageA`, `GetLocaleInfoEx`, `LocalFree`, `TryAcquireSRWLockExclusive`

## MSEA imports and exports

#### ZLZ64.dll - exports (3)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | ?ZLZCloseFilter@@YAXPEAUZIStream@@@Z | ED30 |  |
| 2 | ?ZLZCreateDeflator@@YAPEAUZIStream@@HPEAU1@H@Z | ED60 |  |
| 3 | ?ZLZCreateInflator@@YAPEAUZIStream@@HPEAU1@I@Z | EE60 |  |

#### ZLZ64.dll - imports (117 symbols)

- **KERNEL32.dll** (117): `Sleep`, `FreeLibrary`, `GetProcAddress`, `LoadLibraryA`, `GetLastError`, `SetLastError`, `DisableThreadLibraryCalls`, `OutputDebugStringW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `TryAcquireSRWLockExclusive`, `GetCurrentThreadId`, `SetFileInformationByHandle`, `GetTempPathW`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `FormatMessageA`, `LocalFree`, `GetLocaleInfoEx`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `IsDebuggerPresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `InitializeSListHead`, `RtlUnwindEx`, `RtlPcToFileHeader`, `RaiseException`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `EncodePointer`, `EnterCriticalSection`, `LeaveCriticalSection`, `DeleteCriticalSection`, `InitializeCriticalSectionAndSpinCount`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `GetStdHandle`, `GetFileType`, `GetFileSizeEx`, `SetFilePointerEx`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `IsValidCodePage`, `GetACP`, `GetOEMCP`, `GetCPInfo`, `GetCommandLineA`, `GetCommandLineW`, `MultiByteToWideChar`, `WideCharToMultiByte`, `GetEnvironmentStringsW`, `FreeEnvironmentStringsW`, `SetEnvironmentVariableW`, `GetProcessHeap`, `GetStringTypeW`, `FlushFileBuffers`, `WriteFile`, `GetConsoleOutputCP`, `GetConsoleMode`, `SetStdHandle`, `HeapSize`, `HeapReAlloc`, `CloseHandle`, `ReadFile`, `ReadConsoleW`, `CreateFileW`, `WriteConsoleW`, `RtlUnwind`

## GMS v270 imports and exports

#### ZLZ64.dll - exports (3)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | ?ZLZCloseFilter@@YAXPEAUZIStream@@@Z | EB80 |  |
| 2 | ?ZLZCreateDeflator@@YAPEAUZIStream@@HPEAU1@H@Z | EBB0 |  |
| 3 | ?ZLZCreateInflator@@YAPEAUZIStream@@HPEAU1@I@Z | ECB0 |  |

#### ZLZ64.dll - imports (124 symbols)

- **KERNEL32.dll** (124): `Sleep`, `FreeLibrary`, `GetProcAddress`, `LoadLibraryA`, `GetLastError`, `SetLastError`, `DisableThreadLibraryCalls`, `WriteConsoleW`, `QueryPerformanceCounter`, `QueryPerformanceFrequency`, `SetFileInformationByHandle`, `FlsAlloc`, `FlsGetValue`, `FlsSetValue`, `FlsFree`, `InitializeSRWLock`, `ReleaseSRWLockExclusive`, `AcquireSRWLockExclusive`, `InitializeCriticalSectionEx`, `InitOnceExecuteOnce`, `InitializeConditionVariable`, `WakeConditionVariable`, `WakeAllConditionVariable`, `SleepConditionVariableCS`, `SleepConditionVariableSRW`, `CreateEventExW`, `CreateSemaphoreExW`, `FlushProcessWriteBuffers`, `GetCurrentProcessorNumber`, `GetSystemTimeAsFileTime`, `GetTickCount64`, `FreeLibraryWhenCallbackReturns`, `CreateThreadpoolWork`, `SubmitThreadpoolWork`, `CloseThreadpoolWork`, `CreateThreadpoolTimer`, `SetThreadpoolTimer`, `WaitForThreadpoolTimerCallbacks`, `CloseThreadpoolTimer`, `CreateThreadpoolWait`, `SetThreadpoolWait`, `CloseThreadpoolWait`, `GetModuleHandleW`, `GetFileInformationByHandleEx`, `CreateSymbolicLinkW`, `EnterCriticalSection`, `LeaveCriticalSection`, `DeleteCriticalSection`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `IsDebuggerPresent`, `GetStartupInfoW`, `GetCurrentProcessId`, `GetCurrentThreadId`, `InitializeSListHead`, `RtlUnwindEx`, `RtlPcToFileHeader`, `RaiseException`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `EncodePointer`, `InitializeCriticalSectionAndSpinCount`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `SetConsoleCtrlHandler`, `ExitProcess`, `GetModuleHandleExW`, `GetModuleFileNameW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `GetTempPathW`, `IsThreadAFiber`, `VirtualProtect`, `GetDateFormatW`, `GetTimeFormatW`, `CompareStringW`, `LCMapStringW`, `GetLocaleInfoW`, `IsValidLocale`, `GetUserDefaultLCID`, `EnumSystemLocalesW`, `FindClose`, `FindFirstFileExW`, `FindNextFileW`, `IsValidCodePage`, `GetACP`, `GetOEMCP`, `GetCPInfo`, `GetCommandLineA`, `GetCommandLineW`, `MultiByteToWideChar`, `WideCharToMultiByte`, `GetEnvironmentStringsW`, `FreeEnvironmentStringsW`, `SetEnvironmentVariableW`, `GetProcessHeap`, `GetStdHandle`, `GetFileType`, `GetStringTypeW`, `HeapSize`, `HeapReAlloc`, `SetStdHandle`, `FlushFileBuffers`, `WriteFile`, `GetConsoleOutputCP`, `GetConsoleMode`, `GetFileSizeEx`, `SetFilePointerEx`, `ReadFile`, `ReadConsoleW`, `OutputDebugStringW`, `CreateFileW`, `CloseHandle`, `RtlUnwind`
