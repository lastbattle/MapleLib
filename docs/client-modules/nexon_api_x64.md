# nexon_api_x64.dll - unified dossier

**GMS v270**: 116,736 bytes, linked 2017-10-31, 76 imported / 20 exported.
**MSEA**: not shipped in the audited MSEA corpus.
**Status**: GMS-only.

## MSEA build

Not shipped in the audited MSEA corpus - GMS-only module.

## GMS v270 dossier

### Role and load path

This is a small native Nexon API compatibility shim: 116,736 bytes, AMD64, PE timestamp 2017-10-31, with 20 named exports. It keeps a singleton wrapper, dynamically loads the larger Nexon SDK, resolves `GetDLLInterface`, selects an API interface, and forwards getters, callbacks, patcher calls, and initialization. It is not an independent network client: the URLs and most product behavior are in `nexon_x64.dll`.

The PE inventory shows `MapleStory.exe` imports only `nxapi_shutdown` from this shim. That proves a direct dependency on the shim, but does not establish when the other exports are called or whether they are dynamically resolved. No inventoried binary statically imports `nexon_x64.dll`; the shim loads it dynamically. Its strings and IDA pseudocode include `nexon_x64.dll`, `GetDLLInterface`, and `nxl\shell\open\command`.

### Key mechanisms and VAs

Addresses use image base `0x180000000`; export RVAs are shown in the API table below.

- `nxapi_init` at `0x180002090` rejects a null argument, allocates a small wrapper, calls loader helper `sub_180001750`, and stores the resulting interface pointer in singleton `qword_18001D9C0`. It returns success only when that pointer is non-null.
- `sub_180005500` at `0x180005500` reads the command value below `HKEY_CLASSES_ROOT\nxl\shell\open\command`. `sub_1800058C0` at `0x1800058C0` constructs a candidate by appending `bin\nexon_x64.dll` to a directory derived from that NXL command. The analyzed target is described here only as `<client-root>\nexon_x64.dll`; static evidence does not establish whether the recovered candidate resolves to this file or an alternate layout.
- `sub_180001750` at `0x180001750` calls `LoadLibraryW`, resolves `GetDLLInterface` with `GetProcAddress`, invokes it, then calls the returned context through virtual slots. If the procedure lookup fails, it frees the loaded module. Normal init selects an interface using byte offset `0x78` (120); patcher init additionally invokes offset `0x110` (272).
- Steam init (`nxapi_init_steam`, `0x180002680`) takes the alternate name path and appends the string `:steam` to the API selector before dispatch. The string corpus also has a debug line, `nxapi_init_steam called.`
- `nxapi_init_nxguard` at `0x1800022F0` obtains the current executable path with `GetModuleFileNameW`, derives its directory, and forwards the caller's arguments plus that path through context vtable offset `0xE0` (224). The related error string says `error during init nxguard on nxsdk`.
- The singleton's API, user, friends, callback, patcher, product ID, device ID, and shutdown operations dispatch through context vtable offsets `0x28`, `0x38`, `0x48`, `0xC8`, `0x108`, `0x128`, `0x130`, and `0x80` respectively. These byte offsets are recovered; their target-side method names are not.
- `nxapi_patcher_progress` at `0x180002640` gets the patcher via offset `0x108`, calls patcher offset `0x08` with its progress argument, then returns 1.

### Complete export surface

RVAs come from the supplied export table. `VA = 0x180000000 + RVA`.

#### Initialization and lifecycle

| Export | RVA | Purpose / observed dispatch |
|---|---:|---|
| `nxapi_init` | `0x2090` | Initialize the common singleton and select an API interface. |
| `nxapi_init_nxguard` | `0x22F0` | Forward guard initialization and the executable directory. |
| `nxapi_init_patcher` | `0x25C0` | Common initialization with the patcher-init virtual call enabled. |
| `nxapi_init_steam` | `0x2680` | Common initialization with `:steam` selector suffix. |
| `nxapi_shutdown` | `0x2170` | Calls context virtual slot `0x80`; this is the sole shim import listed for `MapleStory.exe`. |
| `nxcapi_init` | `0x2160` | Thunk to `nxapi_init`. |

#### Interface and data getters

| Export | RVA | Purpose / observed dispatch |
|---|---:|---|
| `nxapi_get_api` | `0x21C0` | Return API object via context slot `0x28`. |
| `nxcapi_get_api` | `0x21C0` | Same RVA and implementation as `nxapi_get_api`. |
| `nxapi_get_user` | `0x21E0` | Return user object via slot `0x38`. |
| `nxapi_get_friends` | `0x2200` | Return friends object via slot `0x48`. |
| `nxapi_get_patcher` | `0x2220` | Return patcher object via slot `0x108`. |
| `nxapi_get_callback` | `0x22D0` | Return callback by caller-supplied ID via slot `0xC8`. |
| `nxapi_get_product_id` | `0x2110` | Return product ID via slot `0x128`. |
| `nxapi_get_device_id` | `0x2130` | Return device ID via slot `0x130`. |
| `nxcapi_get_product_ticket` | `0x2710` | Calls slot `0x08` on its argument; on a C++ exception returns literal `test...`. |

#### Callbacks and patcher

| Export | RVA | Purpose / observed dispatch |
|---|---:|---|
| `nxapi_register_callback` | `0x2240` | Register callback via slot `0x60`. |
| `nxapi_unregister_callback` | `0x2270` | Unregister callback via slot `0x68`. |
| `nxapi_register_apicall_handler` | `0x22A0` | Exported alias at the same RVA as unregister; implementation dispatches slot `0xA8`. |
| `nxapi_unregister_apicall_handler` | `0x22A0` | Same RVA and implementation as register; see RE note. |
| `nxapi_patcher_progress` | `0x2640` | Calls the patcher progress method with the supplied value. |

### Notable imports

The PE import table lists 76 symbols:

- `KERNEL32.dll` (73): `LoadLibraryW`, `GetProcAddress`, `FreeLibrary`; process/module enumeration, `GetModuleFileNameW`, memory/heap, thread and CRT support.
- `ADVAPI32.dll` (2): `RegOpenKeyExW`, `RegQueryValueExW`, used by the NXL command-path lookup.
- `PSAPI.DLL` (1): `GetModuleFileNameExW`.

There is no static import from `nexon_x64.dll`; module and API resolution are runtime operations in the shim. The DLL's strings do not include service URLs; endpoint strings reside in the larger SDK.

### Embedded strings and RE notes

- Loader identifiers: `nexon_x64.dll`, `GetDLLInterface`, `nxl\shell\open\command`, and `:steam`.
- Process-name strings include `nexon_client.exe` and `nexon_runtime.exe`; their presence alone does not show that this shim launches either process.
- The export table contains two same-RVA pairs: `nxapi_get_api`/`nxcapi_get_api` and `nxapi_register_apicall_handler`/`nxapi_unregister_apicall_handler`. IDA confirms the first pair shares a getter implementation and the second pair shares one virtual dispatch at slot `0xA8`. Whether the handler pair is an intentional ABI alias or an export/build defect is unresolved.
- `nxcapi_get_product_ticket` returns `test...` only through its exception handler; this is a string fallback, not evidence of a successful ticket value.
- The shim's imports show its ability to load and locate modules, but static analysis does not establish the actual selected path, whether NXGuard or Steam mode is used, or which interfaces are requested at runtime.
- Evidence sources: supplied PE inventory, import/export tables, per-DLL string corpus, and IDA pseudocode. Embedded PDB basename: `nexon_api_x64.pdb`.

## GMS v270 imports and exports

#### nexon_api_x64.dll - exports (20)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | nxapi_get_api | 21C0 |  |
| 2 | nxapi_get_callback | 22D0 |  |
| 3 | nxapi_get_device_id | 2130 |  |
| 4 | nxapi_get_friends | 2200 |  |
| 5 | nxapi_get_patcher | 2220 |  |
| 6 | nxapi_get_product_id | 2110 |  |
| 7 | nxapi_get_user | 21E0 |  |
| 8 | nxapi_init | 2090 |  |
| 9 | nxapi_init_nxguard | 22F0 |  |
| 10 | nxapi_init_patcher | 25C0 |  |
| 11 | nxapi_init_steam | 2680 |  |
| 12 | nxapi_patcher_progress | 2640 |  |
| 13 | nxapi_register_apicall_handler | 22A0 |  |
| 14 | nxapi_register_callback | 2240 |  |
| 15 | nxapi_shutdown | 2170 |  |
| 16 | nxapi_unregister_apicall_handler | 22A0 |  |
| 17 | nxapi_unregister_callback | 2270 |  |
| 18 | nxcapi_get_api | 21C0 |  |
| 19 | nxcapi_get_product_ticket | 2710 |  |
| 20 | nxcapi_init | 2160 |  |

#### nexon_api_x64.dll - imports (76 symbols)

- **PSAPI.DLL** (1): `GetModuleFileNameExW`
- **KERNEL32.dll** (73): `OutputDebugStringA`, `LoadLibraryW`, `GetProcAddress`, `FreeLibrary`, `MultiByteToWideChar`, `CreateToolhelp32Snapshot`, `GetModuleFileNameW`, `Process32FirstW`, `lstrcmpiW`, `OpenProcess`, `CloseHandle`, `Process32NextW`, `IsValidLocale`, `GetLastError`, `HeapSize`, `HeapFree`, `EncodePointer`, `DecodePointer`, `GetCurrentThreadId`, `FlsSetValue`, `GetCommandLineA`, `TerminateProcess`, `GetCurrentProcess`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `IsDebuggerPresent`, `RtlVirtualUnwind`, `RtlLookupFunctionEntry`, `RtlCaptureContext`, `HeapSetInformation`, `GetVersion`, `HeapCreate`, `HeapDestroy`, `HeapAlloc`, `RaiseException`, `RtlPcToFileHeader`, `Sleep`, `InitializeCriticalSection`, `GetModuleHandleW`, `ExitProcess`, `RtlUnwindEx`, `FlsGetValue`, `FlsFree`, `SetLastError`, `FlsAlloc`, `SetHandleCount`, `GetStdHandle`, `InitializeCriticalSectionAndSpinCount`, `GetFileType`, `GetStartupInfoW`, `DeleteCriticalSection`, `GetModuleFileNameA`, `FreeEnvironmentStringsW`, `WideCharToMultiByte`, `GetEnvironmentStringsW`, `QueryPerformanceCounter`, `GetTickCount`, `GetCurrentProcessId`, `GetSystemTimeAsFileTime`, `WriteFile`, `LeaveCriticalSection`, `EnterCriticalSection`, `HeapReAlloc`, `GetLocaleInfoW`, `GetCPInfo`, `GetACP`, `GetOEMCP`, `IsValidCodePage`, `LCMapStringW`, `GetStringTypeW`, `GetUserDefaultLCID`, `GetLocaleInfoA`, `EnumSystemLocalesA`
- **ADVAPI32.dll** (2): `RegOpenKeyExW`, `RegQueryValueExW`
