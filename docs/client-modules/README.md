# MapleStory client modules - unified dossier index

Per-module reference for the MapleStory clients, combining the MSEA and GMS v270
audit dossiers: per-build facts, analysis, and import/export tables. The client
executable has its own dossier: [MapleStory.exe.md](MapleStory.exe.md). The pack
key schedule is documented in [`../ms-pack-format.md`](../ms-pack-format.md); the
build deltas live in the audit unified reference.

Generated from the audit corpora by `unified_sections.py`; regenerate rather than
hand-edit.

| Module | Status | MSEA | GMS | File |
|---|---|---|---|---|
| PCOM.dll | rebuilt | 2026-09-16 | 2026-07-20 | [pcom.md]( pcom.md ) |
| NameSpace.dll | rebuilt | 2026-09-16 | 2026-07-20 | [namespace.md]( namespace.md ) |
| ResMan.dll | rebuilt | 2026-09-16 | 2026-07-20 | [resman.md]( resman.md ) |
| ZLZ64.dll | rebuilt | 2026-09-16 | 2026-07-20 | [zlz64.md]( zlz64.md ) |
| Canvas.dll | rebuilt | 2026-09-16 | 2026-11-09 | [canvas.md]( canvas.md ) |
| Gr2D_DX11.dll | rebuilt | 2026-09-16 | 2026-07-20 | [gr2d_dx11.md]( gr2d_dx11.md ) |
| Shape2D.dll | rebuilt | 2026-09-16 | 2026-07-20 | [shape2d.md]( shape2d.md ) |
| Sound.dll | rebuilt | 2026-09-16 | 2026-07-20 | [sound.md]( sound.md ) |
| nexon_x64.dll | GMS-only | - | 2017-10-27 | [nexon_x64.md]( nexon_x64.md ) |
| nexon_api_x64.dll | GMS-only | - | 2017-10-31 | [nexon_api_x64.md]( nexon_api_x64.md ) |
| nmcogame64.dll | GMS-only | - | 2021-10-26 | [nmcogame64.md]( nmcogame64.md ) |
| nmconew64.dll | GMS-only | - | 2021-09-07 | [nmconew64.md]( nmconew64.md ) |
| ScreenShotWrapper.dll | GMS-only | - | 2023-11-16 | [screenshotwrapper.md]( screenshotwrapper.md ) |
| gamescale64.dll | GMS-only | - | 2025-12-15 | [gamescale64.md]( gamescale64.md ) |
| gamescale.nm64.dll | GMS-only | - | 2025-08-18 | [gamescale.nm64.md]( gamescale.nm64.md ) |
| steam_api64.dll | GMS-only | - | 2012-03-15 | [steam_api64.md]( steam_api64.md ) |
| sdkencryptedappticket64.dll | GMS-only | - | 2012-03-15 | [sdkencryptedappticket64.md]( sdkencryptedappticket64.md ) |
| discord_partner_sdk.dll | GMS-only | - | 2025-10-15 | [discord_partner_sdk.md]( discord_partner_sdk.md ) |
| d3dx9_31.dll | GMS-only | - | 2006-09-28 | [d3dx9_31.md]( d3dx9_31.md ) |
| D3DX9_43.dll | GMS-only | - | 2010-05-21 | [d3dx9_43.md]( d3dx9_43.md ) |
| libssl-3-x64.dll | GMS-only | - | 2024-02-06 | [libssl-3-x64.md]( libssl-3-x64.md ) |
| libcrypto-3-x64.dll | GMS-only | - | 2024-02-06 | [libcrypto-3-x64.md]( libcrypto-3-x64.md ) |
| NexonAnalytics64.dll | rebuilt | 2026-08-10 | 2026-03-11 | [nexonanalytics64.md]( nexonanalytics64.md ) |
| NxOverlay_x64.dll | rebuilt | 2025-10-13 | 2026-04-15 | [nxoverlay_x64.md]( nxoverlay_x64.md ) |
| nugu64.dll | rebuilt | 2024-11-25 | 2023-01-05 | [nugu64.md]( nugu64.md ) |
| libcurl.dll | rebuilt | 2019-05-15 | 2024-02-06 | [libcurl.md]( libcurl.md ) |
| CrashReporter.dll | identical builds | 2024-10-07 | 2024-10-07 | [crashreporter.md]( crashreporter.md ) |
| HybridCore64.dll | identical builds | 2023-01-06 | 2023-01-06 | [hybridcore64.md]( hybridcore64.md ) |
| LSFG.dll | identical builds | 2025-01-16 | 2025-01-16 | [lsfg.md]( lsfg.md ) |
| MachineIdLib.dll | identical builds | 2019-02-28 | 2019-02-28 | [machineidlib.md]( machineidlib.md ) |
| WzMss.dll | identical builds | 2025-07-07 | 2025-07-07 | [wzmss.md]( wzmss.md ) |
| jypc.dll | identical builds | 2021-10-20 | 2021-10-20 | [jypc.md]( jypc.md ) |
| nps64.dll | identical builds | 2021-04-19 | 2021-04-19 | [nps64.md]( nps64.md ) |

Client executable: [MapleStory.exe.md](MapleStory.exe.md) - static imports,
the MSEA/GMS delta, and the MSEA string-derived context.
