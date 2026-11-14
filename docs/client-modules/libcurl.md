# libcurl.dll - unified dossier

**GMS v270**: 571,480 bytes, linked 2024-02-06, 409 imported / 93 exported.
**MSEA**: 850,520 bytes, linked 2019-05-15, 166 imported / 74 exported.
**Status**: rebuilt.

## MSEA reference dossier

_The analysis below describes the MSEA build._

### Role and loading

This is the upstream libcurl shared library, version `7.61.0`, providing reusable URL-transfer and protocol APIs rather than a MapleStory-specific service. `MapleStory.exe` statically imports `curl_easy_cleanup`, so the game has a direct loader dependency on the DLL. That one import does not enumerate all APIs the client may resolve or call.

### Exports

74 exports, grouped by API family:

- `curl_easy_*` (13): easy-handle setup, options, perform, send/receive, cleanup, and error/info APIs; examples: `curl_easy_init`, `curl_easy_setopt`, `curl_easy_perform`.
- `curl_multi_*` (15): multi-handle and socket/poll workflow; examples: `curl_multi_init`, `curl_multi_perform`, `curl_multi_socket_action`.
- `curl_mime_*` (12): MIME form construction and lifecycle.
- `curl_form*` (3): legacy form APIs.
- `curl_share_*` (4): shared-state handle APIs.
- `curl_slist_*` (2): linked-list helpers.
- `curl_m*` formatting (10): `curl_mprintf` family and variants.
- Other curl helpers (15): global setup, escaping, date/version, free, and push-header helpers.

### Notable imports

- `WS2_32.dll`: socket creation, connect/bind/listen, send/receive, name resolution, and related Winsock operations.
- `WLDAP32.dll`: LDAP client functions (the inventory represents these imports by ordinal).
- `ADVAPI32.dll`: CryptoAPI hashing, encryption, key, and provider functions.
- `KERNEL32.dll`: file/path, locale, thread, environment, and dynamic-library support.

### Embedded endpoints and product strings

- Version resource: FileVersion/ProductVersion `7.61.0`, ProductName `The curl library`, FileDescription `libcurl Shared Library`; version string at PE offset `0x000C9820`.
- `CLIENT libcurl 7.61.0` appears in generic protocol code. Other embedded URLs point to curl documentation (`curl.haxx.se`), not a game backend.
- The string set contains generic HTTP, FTP, LDAP, cookie, upload, and proxy messages. No MapleStory-specific endpoint was identified in this DLL.

### RE notes

The presence of network and crypto imports shows transport capability, not that a request was sent. The game's direct import is only `curl_easy_cleanup`; application-specific configuration and destinations, if any, are outside this library's import/export evidence. Sources: `analysis/pe_inventory.json` (`libcurl.dll` entry and `MapleStory.exe` imports); `analysis/strings/libcurl.strings.txt` near `0x0009F1C0` and `0x000C9820`.

## GMS v270 delta

7.61 to 8.x on the bundled OpenSSL 3.

- +ADVAPI32.dll (1): `CryptAcquireContextW`
- +CRYPT32.dll (6): `CertCloseStore`, `CertEnumCertificatesInStore`, `CertFreeCertificateContext`, `CertGetEnhancedKeyUsage`, `CertGetIntendedKeyUsage`, `CertOpenSystemStoreA`
- +KERNEL32.dll (14): `AcquireSRWLockExclusive`, `CompareFileTime`, `CreateEventW`, `DisableThreadLibraryCalls`, `FormatMessageW`, `GetEnvironmentVariableA`, `GetSystemDirectoryW`, `InitializeCriticalSectionEx`, `LoadLibraryW`, `MoveFileExW`, ...(14)
- +VCRUNTIME140.dll (12): `__C_specific_handler`, `__current_exception`, `__current_exception_context`, `__std_type_info_destroy_list`, `memchr`, `memcmp`, `memcpy`, `memmove`, `memset`, `strchr`, ...(12)
- +WS2_32.dll (8): `WSACloseEvent`, `WSACreateEvent`, `WSAEnumNetworkEvents`, `WSAEventSelect`, `WSAResetEvent`, `WSASetEvent`, `WSAWaitForMultipleEvents`, `htonl`
- +api-ms-win-crt-convert-l1-1-0.dll (5): `atoi`, `strtol`, `strtoll`, `strtoul`, `wcstombs`
- +api-ms-win-crt-filesystem-l1-1-0.dll (4): `_fstat64`, `_unlink`, `_waccess`, `_wstat64`
- +api-ms-win-crt-heap-l1-1-0.dll (4): `calloc`, `free`, `malloc`, `realloc`
- +api-ms-win-crt-math-l1-1-0.dll (1): `_fdopen`
- +api-ms-win-crt-runtime-l1-1-0.dll (16): `__sys_errlist`, `__sys_nerr`, `_beginthreadex`, `_cexit`, `_configure_narrow_argv`, `_crt_at_quick_exit`, `_crt_atexit`, `_errno`, `_execute_onexit_table`, `_initialize_narrow_environment`, ...(16)
- +api-ms-win-crt-stdio-l1-1-0.dll (23): `__acrt_iob_func`, `__stdio_common_vsprintf`, `__stdio_common_vsscanf`, `__stdio_common_vswprintf`, `_close`, `_fileno`, `_fseeki64`, `_lseeki64`, `_read`, `_wfopen`, ...(23)
- +api-ms-win-crt-string-l1-1-0.dll (8): `_strdup`, `_wcsdup`, `strcmp`, `strcspn`, `strncmp`, `strpbrk`, `strspn`, `wcspbrk`
- +api-ms-win-crt-time-l1-1-0.dll (3): `_gmtime64`, `_time64`, `strftime`
- +api-ms-win-crt-utility-l1-1-0.dll (1): `qsort`
- +bcrypt.dll (1): `BCryptGenRandom`
- +libcrypto-3-x64.dll (162): `ASN1_STRING_get0_data`, `ASN1_STRING_length`, `ASN1_STRING_print`, `ASN1_STRING_to_UTF8`, `ASN1_STRING_type`, `ASN1_TIME_print`, `BIO_clear_flags`, `BIO_ctrl`, `BIO_free`, `BIO_get_data`, ...(162)
- +libssl-3-x64.dll (53): `OPENSSL_init_ssl`, `SSL_CIPHER_get_name`, `SSL_CTX_add_client_CA`, `SSL_CTX_check_private_key`, `SSL_CTX_ctrl`, `SSL_CTX_free`, `SSL_CTX_get_cert_store`, `SSL_CTX_new`, `SSL_CTX_sess_set_new_cb`, `SSL_CTX_set_alpn_protos`, ...(53)
- -ADVAPI32.dll (4): `CryptAcquireContextA`, `CryptDestroyKey`, `CryptEncrypt`, `CryptImportKey`
- -KERNEL32.dll (76): `CompareStringW`, `CreateFileW`, `CreateThread`, `EnumSystemLocalesW`, `ExitProcess`, `ExitThread`, `ExpandEnvironmentStringsA`, `FileTimeToSystemTime`, `FindClose`, `FindFirstFileExA`, ...(76)

## MSEA imports and exports

#### libcurl.dll - exports (74)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | curl_easy_cleanup | 9200 |  |
| 2 | curl_easy_duphandle | 9210 |  |
| 3 | curl_easy_escape | A040 |  |
| 4 | curl_easy_getinfo | 9430 |  |
| 5 | curl_easy_init | 9460 |  |
| 6 | curl_easy_pause | 94B0 |  |
| 7 | curl_easy_perform | 9630 |  |
| 8 | curl_easy_recv | 9640 |  |
| 9 | curl_easy_reset | 96D0 |  |
| 10 | curl_easy_send | 9780 |  |
| 11 | curl_easy_setopt | 2BC30 |  |
| 12 | curl_easy_strerror | 309D0 |  |
| 13 | curl_easy_unescape | A140 |  |
| 14 | curl_escape | A1D0 |  |
| 15 | curl_formadd | B6A0 |  |
| 16 | curl_formfree | B6D0 |  |
| 17 | curl_formget | B740 |  |
| 18 | curl_free | A1E0 |  |
| 19 | curl_getdate | 23EB0 |  |
| 20 | curl_getenv | 11700 |  |
| 21 | curl_global_cleanup | 9830 |  |
| 22 | curl_global_init | 9870 |  |
| 23 | curl_global_init_mem | 9880 |  |
| 24 | curl_global_sslset | 3F0E0 |  |
| 25 | curl_maprintf | 1EFE0 |  |
| 26 | curl_mfprintf | 1F080 |  |
| 27 | curl_mime_addpart | 1DB20 |  |
| 28 | curl_mime_data | 1DBA0 |  |
| 29 | curl_mime_data_cb | 1DC80 |  |
| 30 | curl_mime_encoder | 1DCF0 |  |
| 31 | curl_mime_filedata | 1DD90 |  |
| 32 | curl_mime_filename | 1DEB0 |  |
| 33 | curl_mime_free | 1DF20 |  |
| 34 | curl_mime_headers | 1DF90 |  |
| 35 | curl_mime_init | 1E010 |  |
| 36 | curl_mime_name | 1E0B0 |  |
| 37 | curl_mime_subparts | 1E120 |  |
| 38 | curl_mime_type | 1E130 |  |
| 39 | curl_mprintf | 1F0B0 |  |
| 40 | curl_msnprintf | 1F100 |  |
| 41 | curl_msprintf | 1F120 |  |
| 42 | curl_multi_add_handle | 20BC0 |  |
| 43 | curl_multi_assign | 20D90 |  |
| 44 | curl_multi_cleanup | 20DE0 |  |
| 45 | curl_multi_fdset | 20F20 |  |
| 46 | curl_multi_info_read | 21080 |  |
| 47 | curl_multi_init | 21110 |  |
| 48 | curl_multi_perform | 21120 |  |
| 49 | curl_multi_remove_handle | 21230 |  |
| 50 | curl_multi_setopt | 21460 |  |
| 51 | curl_multi_socket | 21640 |  |
| 52 | curl_multi_socket_action | 21690 |  |
| 53 | curl_multi_socket_all | 216E0 |  |
| 54 | curl_multi_strerror | 30E20 |  |
| 55 | curl_multi_timeout | 21730 |  |
| 56 | curl_multi_wait | 21760 |  |
| 57 | curl_mvaprintf | 1F160 |  |
| 58 | curl_mvfprintf | 1F1F0 |  |
| 59 | curl_mvprintf | 1F210 |  |
| 60 | curl_mvsnprintf | 1F250 |  |
| 61 | curl_mvsprintf | 1F2B0 |  |
| 62 | curl_pushheader_byname | 13AE0 |  |
| 63 | curl_pushheader_bynum | 13AF0 |  |
| 64 | curl_share_cleanup | 2C300 |  |
| 65 | curl_share_init | 2C3C0 |  |
| 66 | curl_share_setopt | 2C410 |  |
| 67 | curl_share_strerror | 30EC0 |  |
| 68 | curl_slist_append | 2C640 |  |
| 69 | curl_slist_free_all | 2C6A0 |  |
| 70 | curl_strequal | 307C0 |  |
| 71 | curl_strnequal | 307D0 |  |
| 72 | curl_unescape | A1F0 |  |
| 73 | curl_version | 3C6E0 |  |
| 74 | curl_version_info | 3C730 |  |

#### libcurl.dll - imports (166 symbols)

- **WS2_32.dll** (27): `sendto`, `recvfrom`, `send`, `select`, `__WSAFDIsSet`, `ioctlsocket`, `listen`, `accept`, `WSACleanup`, `WSAStartup`, `gethostname`, `freeaddrinfo`, `getaddrinfo`, `WSAIoctl`, `WSASetLastError`, `socket`, `setsockopt`, `recv`, `ntohs`, `htons`, `getsockopt`, `getsockname`, `getpeername`, `connect`, `closesocket`, `bind`, `WSAGetLastError`
- **WLDAP32.dll** (16): `ord#301`, `ord#200`, `ord#30`, `ord#79`, `ord#35`, `ord#33`, `ord#32`, `ord#27`, `ord#26`, `ord#22`, `ord#41`, `ord#50`, `ord#60`, `ord#211`, `ord#46`, `ord#143`
- **KERNEL32.dll** (114): `GetProcessHeap`, `SetEnvironmentVariableW`, `SetEnvironmentVariableA`, `FreeEnvironmentStringsW`, `GetEnvironmentStringsW`, `GetCommandLineW`, `GetCommandLineA`, `GetCPInfo`, `GetOEMCP`, `IsValidCodePage`, `FindNextFileW`, `FindNextFileA`, `FindFirstFileExW`, `FindFirstFileExA`, `FindClose`, `FlushFileBuffers`, `GetFullPathNameA`, `GetFullPathNameW`, `GetCurrentDirectoryW`, `SetCurrentDirectoryW`, `SetStdHandle`, `HeapReAlloc`, `GetACP`, `GetStringTypeW`, `EnumSystemLocalesW`, `GetUserDefaultLCID`, `IsValidLocale`, `GetLocaleInfoW`, `LCMapStringW`, `CompareStringW`, `SetConsoleCtrlHandler`, `GetDateFormatW`, `GetCurrentThread`, `HeapAlloc`, `HeapFree`, `InitializeCriticalSection`, `EnterCriticalSection`, `LeaveCriticalSection`, `DeleteCriticalSection`, `SleepEx`, `WaitForSingleObject`, `CloseHandle`, `ExpandEnvironmentStringsA`, `Sleep`, `GetLastError`, `SetLastError`, `FormatMessageA`, `VerSetConditionMask`, `GetProcAddress`, `LoadLibraryA`, `GetModuleHandleA`, `GetSystemDirectoryA`, `VerifyVersionInfoA`, `FreeLibrary`, `WaitForMultipleObjects`, `GetFileType`, `GetStdHandle`, `ReadFile`, `PeekNamedPipe`, `GetTickCount`, `SetEndOfFile`, `GetFileAttributesExW`, `WriteConsoleW`, `GetTimeZoneInformation`, `HeapSize`, `OutputDebugStringA`, `OutputDebugStringW`, `WaitForSingleObjectEx`, `GetTimeFormatW`, `SetUnhandledExceptionFilter`, `RtlCaptureContext`, `RtlLookupFunctionEntry`, `RtlVirtualUnwind`, `UnhandledExceptionFilter`, `RaiseException`, `GetCurrentProcess`, `TerminateProcess`, `IsProcessorFeaturePresent`, `QueryPerformanceCounter`, `GetCurrentProcessId`, `GetCurrentThreadId`, `GetSystemTimeAsFileTime`, `InitializeSListHead`, `IsDebuggerPresent`, `GetStartupInfoW`, `GetModuleHandleW`, `RtlUnwindEx`, `InterlockedPushEntrySList`, `InterlockedFlushSList`, `InitializeCriticalSectionAndSpinCount`, `TlsAlloc`, `TlsGetValue`, `TlsSetValue`, `TlsFree`, `LoadLibraryExW`, `CreateThread`, `ExitThread`, `ResumeThread`, `FreeLibraryAndExitThread`, `GetModuleHandleExW`, `SetFilePointerEx`, `CreateFileW`, `GetDriveTypeW`, `SystemTimeToTzSpecificLocalTime`, `FileTimeToSystemTime`, `ExitProcess`, `GetModuleFileNameA`, `GetModuleFileNameW`, `MultiByteToWideChar`, `WideCharToMultiByte`, `GetConsoleMode`, `ReadConsoleW`, `WriteFile`, `GetConsoleCP`
- **ADVAPI32.dll** (9): `CryptHashData`, `CryptCreateHash`, `CryptEncrypt`, `CryptImportKey`, `CryptGetHashParam`, `CryptDestroyKey`, `CryptReleaseContext`, `CryptAcquireContextA`, `CryptDestroyHash`

## GMS v270 imports and exports

#### libcurl.dll - exports (93)

| # | name | RVA | forwarder |
|---|------|-----|-----------|
| 1 | curl_easy_cleanup | 13DD0 |  |
| 2 | curl_easy_duphandle | 13E00 |  |
| 3 | curl_easy_escape | 14E60 |  |
| 4 | curl_easy_getinfo | 14070 |  |
| 5 | curl_easy_header | 1E030 |  |
| 6 | curl_easy_init | 140A0 |  |
| 7 | curl_easy_nextheader | 1E200 |  |
| 8 | curl_easy_option_by_id | 14BF0 |  |
| 9 | curl_easy_option_by_name | 14C00 |  |
| 10 | curl_easy_option_next | 14C10 |  |
| 11 | curl_easy_pause | 14110 |  |
| 12 | curl_easy_perform | 14280 |  |
| 13 | curl_easy_recv | 14290 |  |
| 14 | curl_easy_reset | 14340 |  |
| 15 | curl_easy_send | 143D0 |  |
| 16 | curl_easy_setopt | 3FDC0 |  |
| 17 | curl_easy_strerror | 45CE0 |  |
| 18 | curl_easy_unescape | 14FA0 |  |
| 19 | curl_easy_upkeep | 14440 |  |
| 20 | curl_escape | 15030 |  |
| 21 | curl_formadd | 16700 |  |
| 22 | curl_formfree | 16730 |  |
| 23 | curl_formget | 167A0 |  |
| 24 | curl_free | 15040 |  |
| 25 | curl_getdate | 37540 |  |
| 26 | curl_getenv | 1CA20 |  |
| 27 | curl_global_cleanup | 14480 |  |
| 28 | curl_global_init | 144E0 |  |
| 29 | curl_global_init_mem | 14520 |  |
| 30 | curl_global_sslset | 14610 |  |
| 31 | curl_global_trace | 14670 |  |
| 32 | curl_maprintf | 30260 |  |
| 33 | curl_mfprintf | 30290 |  |
| 34 | curl_mime_addpart | 2EC20 |  |
| 35 | curl_mime_data | 2EC90 |  |
| 36 | curl_mime_data_cb | 2ED50 |  |
| 37 | curl_mime_encoder | 2EDC0 |  |
| 38 | curl_mime_filedata | 2EE60 |  |
| 39 | curl_mime_filename | 2EFA0 |  |
| 40 | curl_mime_free | 2F020 |  |
| 41 | curl_mime_headers | 2F080 |  |
| 42 | curl_mime_init | 2F120 |  |
| 43 | curl_mime_name | 2F1B0 |  |
| 44 | curl_mime_subparts | 2F230 |  |
| 45 | curl_mime_type | 2F240 |  |
| 46 | curl_mprintf | 302C0 |  |
| 47 | curl_msnprintf | 30310 |  |
| 48 | curl_msprintf | 30330 |  |
| 49 | curl_multi_add_handle | 332E0 |  |
| 50 | curl_multi_assign | 33520 |  |
| 51 | curl_multi_cleanup | 33560 |  |
| 52 | curl_multi_fdset | 33690 |  |
| 53 | curl_multi_get_handles | 33810 |  |
| 54 | curl_multi_info_read | 33870 |  |
| 55 | curl_multi_init | 33900 |  |
| 56 | curl_multi_perform | 33920 |  |
| 57 | curl_multi_poll | 33A40 |  |
| 58 | curl_multi_remove_handle | 33A70 |  |
| 59 | curl_multi_setopt | 33C90 |  |
| 60 | curl_multi_socket | 33E00 |  |
| 61 | curl_multi_socket_action | 33E50 |  |
| 62 | curl_multi_socket_all | 33EA0 |  |
| 63 | curl_multi_strerror | 46160 |  |
| 64 | curl_multi_timeout | 33EF0 |  |
| 65 | curl_multi_wait | 33F20 |  |
| 66 | curl_multi_wakeup | 33F50 |  |
| 67 | curl_mvaprintf | 30370 |  |
| 68 | curl_mvfprintf | 30410 |  |
| 69 | curl_mvprintf | 30430 |  |
| 70 | curl_mvsnprintf | 30470 |  |
| 71 | curl_mvsprintf | 304D0 |  |
| 72 | curl_pushheader_byname | 26FE0 |  |
| 73 | curl_pushheader_bynum | 26FF0 |  |
| 74 | curl_share_cleanup | 40150 |  |
| 75 | curl_share_init | 402A0 |  |
| 76 | curl_share_setopt | 402F0 |  |
| 77 | curl_share_strerror | 46240 |  |
| 78 | curl_slist_append | 40640 |  |
| 79 | curl_slist_free_all | 406A0 |  |
| 80 | curl_strequal | 458B0 |  |
| 81 | curl_strnequal | 458D0 |  |
| 82 | curl_unescape | 15050 |  |
| 83 | curl_url | 51630 |  |
| 84 | curl_url_cleanup | 51640 |  |
| 85 | curl_url_dup | 51670 |  |
| 86 | curl_url_get | 517C0 |  |
| 87 | curl_url_set | 51FA0 |  |
| 88 | curl_url_strerror | 462B0 |  |
| 89 | curl_version | 53B50 |  |
| 90 | curl_version_info | 53C50 |  |
| 91 | curl_ws_meta | 540D0 |  |
| 92 | curl_ws_recv | 540E0 |  |
| 93 | curl_ws_send | 540F0 |  |

#### libcurl.dll - imports (409 symbols)

- **CRYPT32.dll** (6): `CertGetEnhancedKeyUsage`, `CertFreeCertificateContext`, `CertEnumCertificatesInStore`, `CertGetIntendedKeyUsage`, `CertOpenSystemStoreA`, `CertCloseStore`
- **WS2_32.dll** (35): `recv`, `ntohs`, `htons`, `getsockopt`, `getsockname`, `getpeername`, `connect`, `bind`, `WSAGetLastError`, `WSASetLastError`, `send`, `setsockopt`, `socket`, `WSAIoctl`, `getaddrinfo`, `freeaddrinfo`, `gethostname`, `accept`, `closesocket`, `listen`, `WSACloseEvent`, `WSACreateEvent`, `WSAEnumNetworkEvents`, `WSAEventSelect`, `WSAResetEvent`, `WSASetEvent`, `WSAWaitForMultipleEvents`, `ioctlsocket`, `htonl`, `__WSAFDIsSet`, `select`, `WSAStartup`, `WSACleanup`, `recvfrom`, `sendto`
- **WLDAP32.dll** (17): `ord#301`, `ord#133`, `ord#79`, `ord#142`, `ord#167`, `ord#127`, `ord#27`, `ord#26`, `ord#117`, `ord#41`, `ord#208`, `ord#216`, `ord#14`, `ord#46`, `ord#147`, `ord#145`, `ord#219`
- **libcrypto-3-x64.dll** (162): `UI_get_input_flags`, `UI_set_result`, `ENGINE_get_first`, `MD5_Update`, `ENGINE_by_id`, `ENGINE_ctrl`, `ENGINE_ctrl_cmd`, `ENGINE_free`, `UI_method_set_closer`, `ENGINE_init`, `ENGINE_finish`, `ENGINE_load_private_key`, `ENGINE_set_default`, `UI_method_set_reader`, `UI_method_set_writer`, `UI_method_set_opener`, `UI_destroy_method`, `UI_create_method`, `UI_OpenSSL`, `UI_get0_user_data`, `OCSP_basic_verify`, `OCSP_crl_reason_str`, `OCSP_cert_status_str`, `OCSP_response_status_str`, `OCSP_CERTID_free`, `d2i_OCSP_RESPONSE`, `OCSP_RESPONSE_free`, `OCSP_BASICRESP_free`, `UI_get_string_type`, `OCSP_check_validity`, `UI_method_get_closer`, `OCSP_resp_find_status`, `OCSP_response_get1_basic`, `OCSP_response_status`, `OCSP_cert_to_id`, `d2i_PKCS12_bio`, `ENGINE_get_next`, `PKCS12_parse`, `PKCS12_PBE_add`, `PKCS12_free`, `ERR_error_string_n`, `ERR_clear_error`, `ERR_peek_last_error`, `ERR_peek_error`, `ERR_get_error`, `X509_check_issued`, `X509V3_EXT_print`, `GENERAL_NAMES_free`, `RAND_status`, `RAND_bytes`, `PEM_read_bio_PrivateKey`, `PEM_read_bio_X509_AUX`, `PEM_write_bio_X509`, `PEM_read_bio_X509`, `PEM_X509_INFO_read_bio`, `X509_PUBKEY_get0_param`, `UI_method_get_reader`, `X509_EXTENSION_get_object`, `X509_get_ext_d2i`, `X509_NAME_ENTRY_get_data`, `X509_NAME_get_entry`, `X509_NAME_get_index_by_NID`, `X509_NAME_print_ex`, `X509_get_pubkey`, `X509_get0_pubkey`, `X509_get0_extensions`, `X509_get_X509_PUBKEY`, `X509_get0_notAfter`, `MD5_Init`, `DES_set_key_unchecked`, `DES_set_odd_parity`, `DES_ecb_encrypt`, `X509_get0_notBefore`, `X509_get_subject_name`, `X509_get_issuer_name`, `X509_get_serialNumber`, `X509_get_version`, `X509_INFO_free`, `X509_get0_signature`, `d2i_X509`, `X509_free`, `i2d_X509_PUBKEY`, `X509_ALGOR_get0`, `d2i_PrivateKey_bio`, `d2i_X509_bio`, `X509_verify_cert_error_string`, `X509_STORE_load_path`, `X509_STORE_load_file`, `X509_load_crl_file`, `X509_STORE_add_crl`, `X509_STORE_add_cert`, `X509_LOOKUP_file`, `X509_STORE_add_lookup`, `X509_STORE_set_flags`, `X509_STORE_up_ref`, `X509_STORE_free`, `RSA_flags`, `RSA_free`, `EVP_PKEY_get_group_name`, `EVP_PKEY_get_bn_param`, `EVP_PKEY_get0_type_name`, `EVP_PKEY_copy_parameters`, `EVP_PKEY_free`, `EVP_PKEY_get1_RSA`, `EVP_PKEY_get_security_bits`, `EVP_PKEY_get_bits`, `EVP_PKEY_get_id`, `EVP_sha1`, `EVP_DigestInit`, `OBJ_obj2txt`, `OBJ_nid2sn`, `ASN1_STRING_print`, `ASN1_TIME_print`, `ASN1_STRING_to_UTF8`, `i2t_ASN1_OBJECT`, `UI_method_get_writer`, `X509_EXTENSION_get_data`, `i2a_ASN1_OBJECT`, `ASN1_STRING_get0_data`, `ASN1_STRING_type`, `ASN1_STRING_length`, `BN_print`, `BN_clear_free`, `BN_num_bits`, `BIO_meth_set_destroy`, `BIO_meth_set_create`, `BIO_meth_set_ctrl`, `BIO_meth_set_read`, `BIO_meth_set_write`, `BIO_meth_free`, `BIO_meth_new`, `BIO_printf`, `BIO_new_mem_buf`, `BIO_s_mem`, `BIO_ctrl`, `BIO_puts`, `BIO_get_shutdown`, `BIO_set_shutdown`, `BIO_set_init`, `BIO_get_data`, `BIO_set_data`, `UI_method_get_opener`, `BIO_free`, `BIO_new`, `BIO_s_file`, `BIO_clear_flags`, `BIO_set_flags`, `CRYPTO_free`, `CRYPTO_malloc`, `OpenSSL_version`, `OPENSSL_sk_pop`, `OPENSSL_sk_pop_free`, `OPENSSL_sk_value`, `OPENSSL_sk_num`, `EVP_sha256`, `EVP_DigestFinal_ex`, `EVP_DigestUpdate`, `EVP_DigestInit_ex`, `EVP_MD_CTX_free`, `EVP_MD_CTX_new`, `ENGINE_get_id`, `MD5_Final`
- **libssl-3-x64.dll** (53): `OPENSSL_init_ssl`, `SSL_get_ex_data`, `SSL_set_ex_data`, `SSL_get0_verified_chain`, `SSL_get_verify_result`, `SSL_get_shutdown`, `SSL_get_privatekey`, `SSL_get_certificate`, `SSL_set_connect_state`, `SSL_CTX_add_client_CA`, `SSL_alert_desc_string_long`, `SSL_CTX_set_post_handshake_auth`, `SSL_shutdown`, `TLS_client_method`, `SSL_get_version`, `SSL_get_error`, `SSL_CTX_ctrl`, `SSL_ctrl`, `SSL_write`, `SSL_read`, `SSL_connect`, `SSL_free`, `SSL_new`, `SSL_CTX_check_private_key`, `SSL_CTX_set_default_passwd_cb_userdata`, `SSL_CTX_set_default_passwd_cb`, `SSL_get1_peer_certificate`, `SSL_CTX_use_PrivateKey`, `SSL_CTX_set_verify`, `SSL_get_peer_cert_chain`, `SSL_set_session`, `SSL_SESSION_free`, `SSL_CTX_use_certificate_chain_file`, `SSL_CTX_use_certificate_file`, `SSL_CTX_use_PrivateKey_file`, `SSL_CTX_set_ciphersuites`, `SSL_set_bio`, `SSL_pending`, `SSL_CIPHER_get_name`, `SSL_get_current_cipher`, `SSL_CTX_set_cert_store`, `SSL_CTX_get_cert_store`, `SSL_CTX_free`, `SSL_CTX_new`, `SSL_CTX_set_cipher_list`, `SSL_get_peer_signature_type_nid`, `SSL_CTX_set_keylog_callback`, `SSL_get0_alpn_selected`, `SSL_CTX_set_alpn_protos`, `SSL_CTX_sess_set_new_cb`, `SSL_CTX_set_msg_callback`, `SSL_CTX_set_options`, `SSL_CTX_use_certificate`
- **KERNEL32.dll** (52): `SetUnhandledExceptionFilter`, `UnhandledExceptionFilter`, `RtlVirtualUnwind`, `RtlLookupFunctionEntry`, `RtlCaptureContext`, `IsProcessorFeaturePresent`, `GetCurrentThreadId`, `DisableThreadLibraryCalls`, `InitializeSListHead`, `IsDebuggerPresent`, `TerminateProcess`, `GetCurrentProcess`, `GetSystemTimeAsFileTime`, `CompareFileTime`, `VerifyVersionInfoW`, `VerSetConditionMask`, `GetTickCount`, `QueryPerformanceCounter`, `WaitForMultipleObjects`, `PeekNamedPipe`, `ReadFile`, `GetFileType`, `GetStdHandle`, `LoadLibraryW`, `GetProcAddress`, `GetModuleHandleW`, `GetModuleHandleA`, `FreeLibrary`, `GetSystemDirectoryW`, `QueryPerformanceFrequency`, `FormatMessageW`, `SetLastError`, `GetLastError`, `GetCurrentProcessId`, `MoveFileExW`, `Sleep`, `GetEnvironmentVariableA`, `AcquireSRWLockExclusive`, `ReleaseSRWLockExclusive`, `WaitForSingleObjectEx`, `WideCharToMultiByte`, `SleepEx`, `MultiByteToWideChar`, `CreateEventW`, `WaitForSingleObject`, `SetEvent`, `DeleteCriticalSection`, `InitializeCriticalSectionEx`, `LeaveCriticalSection`, `EnterCriticalSection`, `CloseHandle`, `GetStartupInfoW`
- **ADVAPI32.dll** (6): `CryptAcquireContextW`, `CryptDestroyHash`, `CryptHashData`, `CryptCreateHash`, `CryptGetHashParam`, `CryptReleaseContext`
- **bcrypt.dll** (1): `BCryptGenRandom`
- **VCRUNTIME140.dll** (12): `memcpy`, `memset`, `strchr`, `memmove`, `strrchr`, `memchr`, `memcmp`, `strstr`, `__C_specific_handler`, `__std_type_info_destroy_list`, `__current_exception`, `__current_exception_context`
- **api-ms-win-crt-string-l1-1-0.dll** (8): `_strdup`, `strncmp`, `strcspn`, `strspn`, `_wcsdup`, `strcmp`, `wcspbrk`, `strpbrk`
- **api-ms-win-crt-stdio-l1-1-0.dll** (23): `setvbuf`, `_fseeki64`, `fread`, `fflush`, `_lseeki64`, `__stdio_common_vsprintf`, `fputc`, `fseek`, `_wfopen`, `_wopen`, `fclose`, `_close`, `fgets`, `_fileno`, `_read`, `_write`, `fputs`, `fwrite`, `feof`, `ftell`, `__acrt_iob_func`, `__stdio_common_vsscanf`, `__stdio_common_vswprintf`
- **api-ms-win-crt-convert-l1-1-0.dll** (5): `strtol`, `wcstombs`, `atoi`, `strtoll`, `strtoul`
- **api-ms-win-crt-time-l1-1-0.dll** (3): `_time64`, `_gmtime64`, `strftime`
- **api-ms-win-crt-runtime-l1-1-0.dll** (16): `_crt_at_quick_exit`, `_crt_atexit`, `_cexit`, `terminate`, `_execute_onexit_table`, `_register_onexit_function`, `_initialize_onexit_table`, `__sys_errlist`, `__sys_nerr`, `_errno`, `_beginthreadex`, `_initialize_narrow_environment`, `_configure_narrow_argv`, `_seh_filter_dll`, `_initterm`, `_initterm_e`
- **api-ms-win-crt-utility-l1-1-0.dll** (1): `qsort`
- **api-ms-win-crt-filesystem-l1-1-0.dll** (4): `_wstat64`, `_fstat64`, `_unlink`, `_waccess`
- **api-ms-win-crt-heap-l1-1-0.dll** (4): `malloc`, `free`, `calloc`, `realloc`
- **api-ms-win-crt-math-l1-1-0.dll** (1): `_fdopen`
