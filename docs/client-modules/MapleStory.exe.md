# MapleStory.exe - client executable reference

**MSEA** (audit reference build): 175,269,344 bytes, linked 2026-10-20, 30 static-import DLLs, 5 exported.
**GMS v270**: 162,627,552 bytes, linked 2026-07-21, 34 static-import DLLs, 5 exported.

## Static imports

### MSEA (30 DLLs)

`kernel32.dll`, `WINMM.dll`, `IMM32.dll`, `WzMss.dll`, `MachineIdLib.dll`, `libcurl.dll`, `CrashReporter.dll`, `nps64.dll`, `NXOVERLAY_X64.dll`, `NGClient64.aes`, `USER32.dll`, `GDI32.dll`, `ADVAPI32.dll`, `SHELL32.dll`, `ole32.dll`, `OLEAUT32.dll`, `WS2_32.dll`, `WINHTTP.dll`, `SHLWAPI.dll`, `pdh.dll`, `WININET.dll`, `ZLZ64.dll`, `tdh.dll`, `ntdll.dll`, `WTSAPI32.dll`, `SETUPAPI.dll`, `DINPUT8.dll`, `IPHLPAPI.DLL`, `VERSION.dll`, `CRYPT32.dll`

### GMS v270 (34 DLLs)

`kernel32.dll`, `WINMM.dll`, `MachineIdLib.dll`, `CrashReporter.dll`, `nps64.dll`, `libcurl.dll`, `nexon_api_x64.dll`, `steam_api64.dll`, `NGClient64.aes`, `NXOVERLAY_X64.dll`, `gamescale64.dll`, `USER32.dll`, `GDI32.dll`, `COMDLG32.dll`, `ADVAPI32.dll`, `SHELL32.dll`, `ole32.dll`, `OLEAUT32.dll`, `WS2_32.dll`, `WINHTTP.dll`, `SHLWAPI.dll`, `pdh.dll`, `WININET.dll`, `ZLZ64.dll`, `tdh.dll`, `ntdll.dll`, `WTSAPI32.dll`, `SETUPAPI.dll`, `DINPUT8.dll`, `NETAPI32.dll`, `IPHLPAPI.DLL`, `nmcogame64.dll`, `VERSION.dll`, `CRYPT32.dll`

### Delta

- GMS adds: `COMDLG32.dll`, `NETAPI32.dll`, `gamescale64.dll`, `nexon_api_x64.dll`, `nmcogame64.dll`, `steam_api64.dll`.
- MSEA-only: `IMM32.dll`, `WzMss.dll`.

## MSEA string-derived context

_From the MSEA executable's string corpus; offsets are file offsets into that binary.
The GMS executable has not been string-censused._

Derived from `analysis/strings/MapleStory.strings.txt` (272,143 lines; `HEXOFF A|W text`; offsets
below are hex string offsets). Binary: `MapleStory.exe` x64, 175 MB, timestamp 2026-10-20,
Themida-guarded.

### 1. Data container references

- WZ package set: `Base\Base.wz` (080D6450 W) with numbered split volumes `Base\Base_000.wz`
  (080D6470 W); loader format strings `%s/%s.wz` (080D66E0) and `%s/%s_%03d.wz` (080D6710), with
  `LastWzIndex` (080D6700) tracking the last volume.
- `@Data.wz` (07EA9F37), adjacent to region tags `maple_hf2_sg` (07EA9EC0) / `maple_hf_sg`
  (07EA9ED0) and the obfuscated blob `O=AC%S*etI#STm@e` (07EA9EE8); `maple_sg` also at 080CC398
  and 080D66B8 W.
- Per-archive sidecar: `Data/%s/%s.ini` (080D66F0).
- Content units are `<dir>/<name>.img` inside WZ: `UI/UIWindow2.img/...` (1066 refs), `Login.img`,
  `Sound/AnnualEvent.img/...` (080D9DC0 W), `Mob/BossMalitiaData/*.img` (080E28B0+), field nodes
  like `#InventoryData/slot` (07A583A8).
- No `.ms`, `%s.ms`, `.pack`, or `.pak` references (0 matches). Backslash `Data\` appears only in
  `C:\WINDOWS\Application Data\Nexon\MapleStory\MapleTV` (07F672D0-07F67328).
- `shard` hits are game data, not containers: `Energy Shard : [%d/%d]` (07D39918),
  `shardCount`/`shardIconOffset` (07D9BD80+ W). `header` hits are zlib/stream errors (`bad header`
  081EF438, `header crc mismatch` 091E1E38) and UI layout (`/headerIntervalX` 07E9E1C8 W).
- Distinct container-name patterns: `Base\Base.wz`; `..._%03d.wz` volumes via `%s/%s_%03d.wz`;
  plain `%s/%s.wz`; `@Data.wz`; `Data/%s/%s.ini`; `*.img` members.

### 2. Engine DLL loading

- A delay-load/GetProcAddress-style table at 0x0972E2xx pairs DLL names with symbols:
  `WzMss.dll`+`WzSoap_GetInspectionInfo_XML` (0972E25D/0972E269), `MachineIdLib.dll`+
  `MIDLib_GetMachineId` (0972E286/0972E299), `libcurl.dll`+`curl_easy_cleanup` (0972E2AD),
  `CrashReporter.dll`+`CrashReporter_SetClientExit` (0972E2CD), `nps64.dll`+`GetLastErrorCode`
  (0972E2FD), `NXOVERLAY_X64.dll`+`EnableChangeCursor` (0972E31A), `ZLZ64.dll`+
  `?ZLZCreateInflator...` (0972E47E), `tdh.dll`+`TdhGetEventInformation` (0972E4B9); plus
  `NexonAnalytics64.dll` (07A31E80, next to analytics fields `GameWorldID/AccountID/playtime/
  itemuid` 07A31E28+) and `jypc.dll` (080CBB50, near `iphlpapi.dll` 080CBB30 and GUID-like hashes
  080CBB60+).
- `PCOM.DLL` (07F953D0 W) with GC tags `GC:InitializePCOM` (080CC210) /
  `GC:UnInitializePCOM` (080CACB0) / `GC:DestroySecurityClient` (080CACC8).
- Only explicit LoadLibrary diagnostic: `fail to LoadLibrary 'Iphlpapi.dll'` (080D6FF8).
- Plain import-name block at 0972E220+: WINMM, IMM32, USER32, GDI32, ADVAPI32, SHELL32, ole32,
  OLEAUT32, WS2_32, WINHTTP, SHLWAPI, pdh, WININET, ntdll, WTSAPI32, SETUPAPI, DINPUT8, IPHLPAPI,
  VERSION; the wider census also shows DBGHELP, CRYPT32, wintrust, mscoree.
- No `Canvas.dll`, `NameSpace.dll`, `Sound.dll`, or `LSFG` name strings. Engine subsystems are
  addressed in-process via service IDs: `SID_NAMESPACE/_FILESYSTEM/_PACKAGE/_LA_PACKAGE`
  (07ADCC38, 07ADCD00, 07B0EA30, 07B0EB18), `SID_RESMAN` (07ADCAF0) + `CreateResMan` (07F92428) +
  `GC:InitResMan` (080CC248), `SID_GR2D_DX9/DX11` (07B0EC90/07B0ED30) + `GC:InitGr2D` (080CC258) +
  `GC:InitializeGr2DComError` (080CC420), `SID_SHAPE2D_VECTOR2D` (07C400F8) +
  `oShape2D#Vector2D` (07D0CD76 W), `SID_SOUND` family (07ACE9C0+).
  `WZ2Gr2DLayerComponent`/`IWzGr2DLayer` RTTI (091ECCA0, 09234710) confirms Gr2D linkage. Canvas
  appears only as a WZ object concept (`canvasUOL` 07D3BE98, `Effect/CanvasParticle.img/`
  079FAAD0); NameSpace as a W concept (`NameSpace#FileSystem` 07E03C98, `PcRootNameSpace`
  07F95388).
- MapleLab feature flags: `SID_MAPLE_LAB_HYBRID_CPU_ACTIVE` (07AC3708) + `HybridCore64.dll`
  (07E04CB0 W); `SID_MAPLE_LAB_UPSCALER_ACTIVE/_TYPE` (07AC37C0/07AC3838); no LSFG/DLSS/FSR
  vendor strings.

### 3. Networking

- Passport/OTP SSO: `http://%s/games/games.asmx/VerifyPassportOTP?Passport_ID=%s&OTP=%s`
  (079BB830), next to server IPs `121.52.204.66` / `121.52.204.198` (079BB808/079BB818).
- Hardcoded endpoints: `54.180.211.235:24200`, `43.200.157.16:24200` (07A2F938/07A2F950);
  `175.207.3.214/.219/.229` and `121.52.202.102-.105` (07AB0F18-07AB0F78);
  `http://127.0.0.1/` (07C64798).
- Nexon web/API hosts: `maplestory.nexon.com/guide/viewer/1`, legacy event pages;
  `https://open.api.nexon.com/gw/maple-hands/api/maplestory/v1/external/history/cube` plus
  `test-open.api.nexon.com` staging mirror; `https://public.api.nexon.com/cem/client` and
  `.../stream-processor-proxy/ap-northeast-1/` telemetry (07A335D0); DigiCert OCSP/CRL/CRT URLs
  for pinning checks.
- No literal `NXSSO` string; SSO-adjacent markers: `ETC_OTPSecurity` (07A2D600), `inAuth`
  (080D673B), `ms_loginfo` (07A32388), `UserPingClientToGame` (07A340C8). CashShop appears only
  as game-event/telemetry tags (`Char_CashShopBuy` 07A2F4E8+) and `CashShop.img`, not URLs.

### 4. Protection stack

- Nexon NGClient: `NGClient64.aes` (0972E341), adjacent to `NXOVERLAY_X64.dll` in the dynamic
  table; `GC:DestroySecurityClient` tag (080CACC8).
- BlackCipher: `BlackCipher64.aes` (07A348A8) inside system-info/protection blocks
  (`NumberOfProcessors`, `\Processor(_TOTAL)\% Processor Time`).
- XIGNCODE3 and NGS via message SIDs: `SID_XIGNCODE_INIT_ERROR_PREFIX_FORMAT`/
  `SID_XIGNCODE_HACK_DETECTED` (07B73C30/07B73D58), `SID_NGS_UPDATE_ERROR_PREFIX_FORMAT`/
  `SID_NGS_HACK_DETECTED` (07B73A58/07B73B08); plus `nps64.dll`+`GetLastErrorCode` (0972E2FD).
- No GameGuard/nProtect strings. Supporting: wintrust `CryptCATAdminAcquireContext2`
  (07EA95C0/07EA95D0), `ETC_OTPSecurity`, and test-tool pair `AdminTestDLL.dll` +
  `InitiateTool/ShowTestTool/DestoryTestTool` (07F9BB20+).

### 5. Build identity

- VS_VERSIONINFO (0975668E): StringFileInfo `041204b0` (0975670E; Korean + unicode),
  `FileDescription/InternalName = MapleStory`, `OriginalFilename = MapleStory.exe` (09756834),
  `ProductName = Wizet MapleStory` (09756874); no numeric version in strings. Manifest (09756928):
  `requireAdministrator`, dpiAware `True/PM`, Win7/8/8.1/10 supportedOS GUIDs, assembly version
  1.0.0.0.
- Build-machine paths tag SG/MSEA: `F:\ACGame_SG_Tag\WZ2FrameworkFiles\Wvs\...` (07CE1630,
  07CE46A0, 081D9E60), `F:\ACGame_SG_Tag\Wvs\Util.cpp` (080B7760); version macro
  `MS_VER_CLIENT_MINOR` (080B7718); region ids `maple_hf2_sg`/`maple_hf_sg`/`maple_sg`. No
  literal TMS/MSEA/GMS/KMS strings.
- Renderer: DX11 path `SID_GR2D_DX11` (07B0ED30), DXGI error strings (07C5DE40+), WARP fallback
  `dx11warp`/`dx11warpH` (080CCCE8/0629A05B); legacy `SID_GR2D_DX9` (07B0EC90).
- Dates in content strings: `2026-08-13` (07A77370), `2026/02/04`/`2026/03/03` event windows
  (07A52398+); dump timestamp format `%y%m%d_%H%M%S` (080D64B8); `SID_MSG_BUILD_DATE_DIF`
  (07B3A518); settings keys `isFullScreen`/`ResolutionWidth/Height`/`WvsSetUpStep`
  (080D64F0-080D6580 W).

### 6. Configuration files

- `\MapleResource.ini` (080B7700), next to `MS_VER_CLIENT_MINOR`.
- `MapleStoryGlobalConfig.json` via `GlobalJsonMan` (07CE23A0/07CE2370).
- `./SkillCategory.json` (07D3E2E0) - console/tool path.
- `Data/%s/%s.ini` (080D66F0) - per-WZ metadata (section 1).
- Crash/report: `_ms_report.ini` (080D7B78), `_ms_callstack.ini` (080D7B88) written under
  `MaplePatcher\` (080D7BA0), tag `MaplePatcherReport` (080D7BB0).
- No `.xml`/`.dat` config filenames referenced (`s.ini` is a `%s.ini` remnant); Lua
  `LUA_PATH`/`LUA_CPATH` templates include `loadall.dll` (081F0030/081F0040/081F0060).
