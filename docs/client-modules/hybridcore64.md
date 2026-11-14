# HybridCore64.dll - unified dossier

**GMS v270**: 33,880 bytes, linked 2023-01-06, 34 imported / 1 exported.
**MSEA**: 33,880 bytes, linked 2023-01-06, 34 imported / 1 exported.
**Status**: identical builds.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

A 2023-01-06 utility image (PDB path `D:\_git\maplestorykr\Bin64\HybridCore.pdb` - built from the
KMS client tree) that answers exactly one question for the process: is this a hybrid CPU, and
which cores are efficiency cores? MapleStory.exe references it next to the
`SID_MAPLE_LAB_HYBRID_CPU_ACTIVE` feature flag (EXE string offsets 07AC3708 / 07E04CB0), i.e. it
backs the MapleLab hybrid-CPU scheduling experiment. The sole export is `GetCpuCoreInformation`
(RVA 0x1000); every other function in the image is CRT boilerplate.

### Key mechanism - `GetCpuCoreInformation` (0x180001000, 0x140 bytes)

`BOOL GetCpuCoreInformation(HANDLE* phProcess, ULONG_PTR* processAffinity, ULONG_PTR*
systemAffinity, BYTE* hasEfficiency, ULONG_PTR* effMask, DWORD* effCount, DWORD* perfCount)`

1. `GetProcessAffinityMask(*phProcess, a2, a3)` - caller receives process and system affinity
   masks; on failure the out-fields are zeroed and FALSE returned.
2. Two-phase `GetSystemCpuSetInformation` (size query, then `operator new` + real call).
3. First walk counts records via each `SYSTEM_CPU_SET_INFORMATION.Size` field (8-byte header
   stride).
4. Second walk uses a fixed 32-byte record stride and reads the byte at record + 0x12 - the
   efficiency-class field: nonzero (E-core) sets `*hasEfficiency = 1`, `++*effCount`, and sets the
   core bit via `rol` of a 1-bit mask; zero (P-core) increments `*perfCount`.
5. Frees the buffer, returns TRUE.

RE note: the counting pass honors per-record `Size` while the classification pass assumes uniform
32-byte records - a latent mismatch should Windows ever emit variable-sized entries here. The
`perfCount` name is inferred from the else-branch of the efficiency test.

### Imports

34 symbols. `KERNEL32.dll` (17): `GetProcessAffinityMask`, `GetSystemCpuSetInformation`,
`IsDebuggerPresent`, `InitializeSListHead`, `GetSystemTimeAsFileTime`, `GetCurrentThreadId`,
`GetCurrentProcessId`, `QueryPerformanceCounter` and CRT support set; plus static CRT
(`VCRUNTIME140`, `api-ms-win-crt-*`). No dynamic imports, no delay imports.

## GMS v270

Bit-identical to the MSEA build; everything above applies.

## MSEA imports and exports

#### HybridCore64.dll - exports (1)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | GetCpuCoreInformation | 1000 |  |

#### HybridCore64.dll - imports (34 symbols)

- **KERNEL32.dll** (17): `GetProcessAffinityMask`, `GetSystemCpuSetInformation`, `GetLastError`, `IsDebuggerPresent`, `InitializeSListHead`, `GetSystemTimeAsFileTime`, `GetCurrentThreadId`, `GetCurrentProcessId`, `QueryPerformanceCounter`, `IsProcessorFeaturePresent`, `TerminateProcess`, `GetCurrentProcess`, `SetUnhandledExceptionFilter`, `UnhandledExceptionFilter`, `RtlVirtualUnwind`, `RtlLookupFunctionEntry`, `RtlCaptureContext`
- **VCRUNTIME140.dll** (6): `__std_type_info_destroy_list`, `memset`, `__std_exception_copy`, `__std_exception_destroy`, `_CxxThrowException`, `__C_specific_handler`
- **api-ms-win-crt-runtime-l1-1-0.dll** (8): `_initterm_e`, `_initterm`, `_cexit`, `_configure_narrow_argv`, `_seh_filter_dll`, `_initialize_onexit_table`, `_initialize_narrow_environment`, `_execute_onexit_table`
- **api-ms-win-crt-heap-l1-1-0.dll** (3): `malloc`, `free`, `_callnewh`

_GMS build bit-identical - the tables above cover both._
