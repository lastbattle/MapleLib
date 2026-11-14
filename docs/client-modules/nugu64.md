# nugu64.dll - unified dossier

**GMS v270**: 4,183,128 bytes, linked 2023-01-05, 4 imported / 3 exported.
**MSEA**: 4,301,400 bytes, linked 2024-11-25, 5 imported / 3 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

The strongest static reading is that this is NEXON Korea's NUGU device-identity/reporting helper (FileVersion 1.0.1.4), rather than a gameplay or rendering library. Its embedded payload fragments mention `hwid`, `mid`, `toyGameId`, `logType` and `logCode`; imports include display/network-adapter enumeration and WinHTTP response reading. Together these suggest client/device identification and associated reporting or trust telemetry for a Nexon component. This remains an inference: the implementation is WinLicense-protected, and the supplied `MapleStory.exe` inventory has no static import or string reference to `nugu64.dll`, so the actual caller and active use are unresolved.

### Key mechanisms

| Entry VA | Observed mechanism | Interpretation and limit |
| --- | --- | --- |
| `0x180009EE0` | Unnamed ordinal-1 export allocates a `0x232E0`-byte stack frame and jumps to `0x18075F45A` in `.winlice`. | Protected dispatch/entry stub; its operation is not recoverable from the visible stub. |
| `0x18000C160` | `GetNameA` saves incoming arguments and jumps to `0x18075FFA8` in `.winlice`. | ANSI name accessor by export name; returned value and exact meaning are hidden. |
| `0x18000C1C0` | `GetNameW` saves incoming arguments and jumps to `0x180761EC2` in `.winlice`. | Wide-character counterpart; returned value and exact meaning are hidden. |

No reliable code VA could be assigned to the `hwid`/`mid` or adapter/display collection path: the corresponding payload code is protected and the visible IDA listing has no xrefs to the five IAT entries.

### Exports

| Ordinal | Name | RVA | VA | Forwarder |
| --- | --- | --- | --- | --- |
| 1 | (unnamed) | `0x9EE0` | `0x180009EE0` | No |
| 2 | `GetNameA` | `0xC160` | `0x18000C160` | No |
| 3 | `GetNameW` | `0xC1C0` | `0x18000C1C0` | No |

### Imports

All five imports:

| DLL | Symbol | Relevant capability |
| --- | --- | --- |
| `kernel32.dll` | `GetModuleHandleA` | Module lookup |
| `kernel32.dll` | `VirtualProtect` | Change memory protection; may support the protected runtime |
| `USER32.dll` | `EnumDisplayDevicesW` | Enumerate display devices |
| `WINHTTP.dll` | `WinHttpReadData` | Read data from a WinHTTP request |
| `IPHLPAPI.DLL` | `GetAdaptersInfo` | Enumerate network adapters |

These imports indicate available capabilities, not proof that each is called on a live game path.

### Embedded data and strings

- The string at VA `0x1800206E0` contains adjacent fragments resembling `"90001","hwid":"%hs","mid":"%hs"` and `{"toyGameId":"%04d","logType":"90000","logCode":...`; its trailing bytes include a partial `\\.\PhysicalDriv` path. The bytes are concatenated/truncated, not valid complete JSON. They support an identity/logging interpretation, but do not establish a disk-serial query or final transmitted schema.
- Version-resource strings identify `NEXON Korea Corporation`, original filename `nugu.dll`, FileVersion/ProductVersion `1.0.1.4`, and Nexon copyright. The version translation key is `041204b0` (Korean language ID, Unicode code page); the extracted strings contain no Hangul text.
- RTTI includes `PeLocal` and `PeBase`, alongside standard C++ exception and `shared_ptr` implementation types. No NUGU-specific class family or descriptive application endpoint was recovered.
- The extracted strings file contains 18,603 rows. Most of its long tail consists of short opaque/binary-looking runs in the large payload region; the trailing rows include DigiCert OCSP/CRL/certificate URLs and NEXON certificate names. Treat these as packed/signature material, not application service endpoints. The only URL strings identified are certificate-validation metadata.

### RE notes

- x64 PE, image base `0x180000000`, timestamp `2024-11-25`; file size 4,301,400 bytes.
- Sections include `.vm_sec` (24 KiB), `.winlice` (7,782,400-byte virtual size, zero raw size), and `.boot` (4,091,904 bytes raw). The named protection section and all three public stubs' jumps into it indicate a WinLicense-style protected implementation; ordinary static decompilation does not expose the core behavior.
- The PE inventory reports 3 exports and 5 imports. IDA recovered the three public wrapper stubs but no references from visible code to the imported APIs or identity string. Endpoint, exact identifier derivation, response handling, and initialization semantics remain unknown.
- In the supplied `MapleStory.exe` inventory, `nugu64.dll` is neither a static import nor an embedded name string. A runtime or indirect load by another component is possible but not established here.

Sources: `analysis/pe_inventory.json` (`nugu64.dll` and `MapleStory.exe` entries); `analysis/strings/nugu64.strings.txt`; IDA static analysis of the three export RVAs.

## GMS v270 delta

Rebuild on the 2023-01-05 generation.

- +WINHTTP.dll (1): `WinHttpOpen`
- -WINHTTP.dll (1): `WinHttpReadData`
- -kernel32.dll (1): `VirtualProtect`

## MSEA imports and exports

#### nugu64.dll - exports (3)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | - | 9EE0 |  |
| 2 | GetNameA | C160 |  |
| 3 | GetNameW | C1C0 |  |

#### nugu64.dll - imports (5 symbols)

- **kernel32.dll** (2): `GetModuleHandleA`, `VirtualProtect`
- **USER32.dll** (1): `EnumDisplayDevicesW`
- **WINHTTP.dll** (1): `WinHttpReadData`
- **IPHLPAPI.DLL** (1): `GetAdaptersInfo`

## GMS v270 imports and exports

#### nugu64.dll - exports (3)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | - | 8600 |  |
| 2 | GetNameA | A850 |  |
| 3 | GetNameW | A8B0 |  |

#### nugu64.dll - imports (4 symbols)

- **kernel32.dll** (1): `GetModuleHandleA`
- **USER32.dll** (1): `EnumDisplayDevicesW`
- **WINHTTP.dll** (1): `WinHttpOpen`
- **IPHLPAPI.DLL** (1): `GetAdaptersInfo`
