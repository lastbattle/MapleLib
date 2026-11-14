# WzMss.dll - unified dossier

**GMS v270**: 344,064 bytes, linked 2025-07-07, 115 imported / 25 exported.
**MSEA**: 344,064 bytes, linked 2025-07-07, 115 imported / 25 exported.
**Status**: identical builds.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role

WzMss.dll is a MapleStory SOAP/web-service client library, not an audio or general multimedia layer. It is statically imported by MapleStory.exe, whose import table names `WzSoap_GetInspectionInfo_XML`; this makes WzMss a load-time client dependency, though the import alone does not prove that a call occurs in every run. The exports cover maintenance/inspection queries, guild-board articles and comments, notice boards, and a ConsultBoard API. Decompilation shows ATL SOAP clients, XML envelope generation, WinINet HTTP POSTs for MapleStory services, and a separate Winsock-backed client for ConsultBoard.

### Key mechanisms

- Service wrappers and BSTR marshalling: `0x180018CE0` (`WzSoap_GetInspectionInfo`) constructs a `CMapleStoryT<CSoapWininetClient>` client for `https://api.maplestory.nexon.com/soap/maplestory.asmx`, calls the inspection operation at `0x18000C670`, and copies a successful BSTR result to the caller. `0x180019310` (`WzSoap_GuildNewBoard_Article_List2`) follows the same pattern and dispatches to `0x18000D8B0` with its board/page arguments.
- SOAP serialization: `0x18000B9E0` writes a SOAP 1.1 `<soap:Envelope>` and body, selects an ATL RPC-encoded/PID/PAD generator based on operation metadata, then closes the envelope. Generated operation `0x18000D8B0` adds the `GuildNewBoard_Article_List2` SOAPAction.
- WinINet transport: `0x180014CB0` assembles the `Content-Type: text/xml; charset=utf-8` header and sends a `POST` using HTTP/1.0 through `HttpOpenRequestA` and `HttpSendRequestA`; `0x180012960` reads response bytes with `InternetReadFile`.
- ConsultBoard socket transport: `0x180018B30` (`WzSoap_ConsultWrite`) constructs `CConsultBoardT<CSoapSocketClientT<ZEvtSyncSocket>>` for `http://api.maplestory.nexon.com/Soap/ConsultBoard.asmx` and dispatches to `0x180008AD0`. Its socket path connects at `0x180008640`, sends at `0x180016A40`, and receives at `0x180012990`, using WSA events, overlapped I/O, and bounded waits.

### Exports

VAs use image base `0x180000000`.

| Ordinal | Name | VA |
| ---: | --- | ---: |
| 1 | `WzSoap_ConsultCheckAnswer` | `0x180018220` |
| 2 | `WzSoap_ConsultCheckCreate` | `0x180018390` |
| 3 | `WzSoap_ConsultDelete` | `0x180018520` |
| 4 | `WzSoap_ConsultGetContent` | `0x180018690` |
| 5 | `WzSoap_ConsultGetTitle` | `0x180018800` |
| 6 | `WzSoap_ConsultModify` | `0x180018970` |
| 7 | `WzSoap_ConsultWrite` | `0x180018B30` |
| 8 | `WzSoap_GetInspectionInfo` | `0x180018CE0` |
| 9 | `WzSoap_GetInspectionInfo_XML` | `0x180018E50` |
| 10 | `WzSoap_GuildNewBoard_Article_Create_NoImage2` | `0x180018FC0` |
| 11 | `WzSoap_GuildNewBoard_Article_Delete2` | `0x180019190` |
| 12 | `WzSoap_GuildNewBoard_Article_List2` | `0x180019310` |
| 13 | `WzSoap_GuildNewBoard_Article_List2_XML` | `0x1800194A0` |
| 14 | `WzSoap_GuildNewBoard_Article_Modify2` | `0x180019630` |
| 15 | `WzSoap_GuildNewBoard_Article_MyList2` | `0x1800197E0` |
| 16 | `WzSoap_GuildNewBoard_Article_MyList2_XML` | `0x180019960` |
| 17 | `WzSoap_GuildNewBoard_Article_Recommend2` | `0x180019AE0` |
| 18 | `WzSoap_GuildNewBoard_Article_RecommendList2` | `0x180019C70` |
| 19 | `WzSoap_GuildNewBoard_Article_RecommendList2_XML` | `0x180019DF0` |
| 20 | `WzSoap_GuildNewBoard_Comment_Create2` | `0x180019F70` |
| 21 | `WzSoap_GuildNewBoard_Comment_Delete2` | `0x18001A110` |
| 22 | `WzSoap_GuildNewBoard_Comment_List2` | `0x18001A2A0` |
| 23 | `WzSoap_GuildNewBoard_Comment_List2_XML` | `0x18001A430` |
| 24 | `WzSoap_MapleNoticeBoard_GetList_Num` | `0x18001A5C0` |
| 25 | `WzSoap_MapleTNoticeBoard_GetList_Num` | `0x18001A6F0` |

### Notable imports by DLL

- `WININET.dll`: `InternetOpenA`, `InternetConnectA`, `HttpOpenRequestA`, `HttpSendRequestA`, `HttpQueryInfoA`, `InternetReadFile`, `InternetSetOptionA`, `InternetCloseHandle`.
- `WS2_32.dll`: `WSASocketA`, `WSAConnect`, `WSASend`, `WSARecv`, `WSAGetOverlappedResult`, `WSAEventSelect`, `WSAEnumNetworkEvents`, `getaddrinfo`, `freeaddrinfo`, plus Winsock event and cleanup helpers.
- `ole32.dll`: `CoInitialize`, `CoUninitialize`, `CoCreateInstance`.
- `OLEAUT32.dll`: `VariantInit`, `VariantClear`, `SysAllocString`, `SysAllocStringLen`, `SysFreeString`.
- `KERNEL32.dll`: heap management; critical sections, events, and waits; resource loading; `LoadLibraryExW`/`GetProcAddress`; file handles; and character-set conversion. The full import group also contains compiler/runtime and exception-handling support.

### RE notes

- Static inventory identifies a 64-bit PE with image base `0x180000000` and size 344,064 bytes. MapleStory.exe imports `WzSoap_GetInspectionInfo_XML` from this DLL by name.
- The strings include SOAP 1.1 envelope/encoding tags, XML response names, explicit `SOAPAction` values, and the MapleStory/ConsultBoard service URLs. RTTI includes `ATL::CSoapWininetClient`, `ATL::CSoapSocketClientT<ATL::ZEvtSyncSocket>`, `MapleStory::CMapleStoryT<...>`, `ConsultBoard::CConsultBoardT<...>`, `ATL::CSoapFaultParser`, and `ATL::CSAXStringBuilder`. The embedded PDB path ends in `WzLib\Soap\x64\Soap\Release\Soap.pdb`.
- A targeted scan of the supplied strings found no `waveOut`, `mmio`, DirectSound/`DSOUND`, ACM codec, or audio/sound markers. The import table has no `WINMM.dll`, `DSOUND.dll`, or `MSACM32.dll` group. This is bounded negative static evidence, not proof that no related functionality exists outside this module.
- Static analysis does not show whether these services are reachable or succeed at runtime. The distinction between the WinINet MapleStory endpoint and the plain-HTTP ConsultBoard endpoint is present in the binary; no runtime requests were made.

## GMS v270

Bit-identical to the MSEA build; everything above applies.

## MSEA imports and exports

#### WzMss.dll - exports (25)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | WzSoap_ConsultCheckAnswer | 18220 |  |
| 2 | WzSoap_ConsultCheckCreate | 18390 |  |
| 3 | WzSoap_ConsultDelete | 18520 |  |
| 4 | WzSoap_ConsultGetContent | 18690 |  |
| 5 | WzSoap_ConsultGetTitle | 18800 |  |
| 6 | WzSoap_ConsultModify | 18970 |  |
| 7 | WzSoap_ConsultWrite | 18B30 |  |
| 8 | WzSoap_GetInspectionInfo | 18CE0 |  |
| 9 | WzSoap_GetInspectionInfo_XML | 18E50 |  |
| 10 | WzSoap_GuildNewBoard_Article_Create_NoImage2 | 18FC0 |  |
| 11 | WzSoap_GuildNewBoard_Article_Delete2 | 19190 |  |
| 12 | WzSoap_GuildNewBoard_Article_List2 | 19310 |  |
| 13 | WzSoap_GuildNewBoard_Article_List2_XML | 194A0 |  |
| 14 | WzSoap_GuildNewBoard_Article_Modify2 | 19630 |  |
| 15 | WzSoap_GuildNewBoard_Article_MyList2 | 197E0 |  |
| 16 | WzSoap_GuildNewBoard_Article_MyList2_XML | 19960 |  |
| 17 | WzSoap_GuildNewBoard_Article_Recommend2 | 19AE0 |  |
| 18 | WzSoap_GuildNewBoard_Article_RecommendList2 | 19C70 |  |
| 19 | WzSoap_GuildNewBoard_Article_RecommendList2_XML | 19DF0 |  |
| 20 | WzSoap_GuildNewBoard_Comment_Create2 | 19F70 |  |
| 21 | WzSoap_GuildNewBoard_Comment_Delete2 | 1A110 |  |
| 22 | WzSoap_GuildNewBoard_Comment_List2 | 1A2A0 |  |
| 23 | WzSoap_GuildNewBoard_Comment_List2_XML | 1A430 |  |
| 24 | WzSoap_MapleNoticeBoard_GetList_Num | 1A5C0 |  |
| 25 | WzSoap_MapleTNoticeBoard_GetList_Num | 1A6F0 |  |

#### WzMss.dll - imports (115 symbols)

- **KERNEL32.dll** (81): `GetLastError`, `HeapDestroy`, `HeapAlloc`, `HeapReAlloc`, `HeapFree`, `HeapSize`, `GetProcessHeap`, `EnterCriticalSection`, `LeaveCriticalSection`, `InitializeCriticalSectionEx`, `DeleteCriticalSection`, `WaitForSingleObject`, `FindResourceExW`, `LoadResource`, `LockResource`, `SizeofResource`, `FindResourceW`, `MultiByteToWideChar`, `WideCharToMultiByte`, `RaiseException`, `WriteConsoleW`, `FlushFileBuffers`, `SetFilePointerEx`, `GetConsoleMode`, `GetConsoleCP`, `WriteFile`, `SetStdHandle`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `IsValidCodePage`, `FindNextFileA`, `FindFirstFileExA`, `FindClose`, `LCMapStringW`, `GetFileType`, `GetStdHandle`, `GetACP`, `GetStringTypeW`, `GetModuleFileNameA`, `GetModuleHandleExW`, `IsDebuggerPresent`, `OutputDebugStringW`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `SetUnhandledExceptionFilter`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `CloseHandle`, `SetEvent`, `ResetEvent`, `WaitForSingleObjectEx`, `CreateEventW`, `GetModuleHandleW`, `GetProcAddress`, `GetStartupInfoW`, `QueryPerformanceCounter`, `GetCurrentProcessId`, `GetCurrentThreadId`, `GetSystemTimeAsFileTime`, `InitializeSListHead`, `RtlPcToFileHeader`, `EncodePointer`, `RtlUnwindEx`, `InitializeCriticalSectionAndSpinCount`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `FreeLibrary`, `LoadLibraryExW`, `InterlockedFlushSList`, `SetLastError`, `ExitProcess`, `CreateFileW`
- **ole32.dll** (3): `CoInitialize`, `CoUninitialize`, `CoCreateInstance`
- **OLEAUT32.dll** (5): `VariantClear`, `VariantInit`, `SysFreeString`, `SysAllocStringLen`, `SysAllocString`
- **WS2_32.dll** (18): `WSAEventSelect`, `WSAEnumNetworkEvents`, `WSAGetOverlappedResult`, `WSAConnect`, `WSACloseEvent`, `WSAGetLastError`, `WSASetLastError`, `WSACleanup`, `WSAStartup`, `closesocket`, `WSARecv`, `WSAResetEvent`, `WSASend`, `WSASetEvent`, `WSASocketA`, `getaddrinfo`, `freeaddrinfo`, `WSACreateEvent`
- **WININET.dll** (8): `InternetConnectA`, `InternetCloseHandle`, `InternetReadFile`, `InternetSetOptionA`, `HttpOpenRequestA`, `HttpSendRequestA`, `HttpQueryInfoA`, `InternetOpenA`

_GMS build bit-identical - the tables above cover both._
