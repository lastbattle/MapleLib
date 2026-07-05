# Maple crypto optimization report

Date: 2026-07-05

## Scope and method

This pass optimized `MapleAESEncryption`, `MapleCrypto`, `MapleCryptoConstants`, and
`MapleCustomEncryption` without changing packet ciphertext or IV/header semantics.
The standalone BenchmarkDotNet project under `benchmarks/MapleCrypto.Benchmarks`
measures 32, 128, 512, 1,460, and 8,192-byte packets. Each result uses three warmups
and five measured iterations in an isolated Release-mode .NET 10 process with the
memory diagnoser enabled.

Correctness is checked before every benchmark run using round trips at every packet
size, fixed SHA-256 ciphertext vectors, the known IV shuffle result, whole-pattern
byte multiplication, and mutable custom AES keys. The workload shape follows the
ideas in [lastbattle/maplepacket-optimizations](https://github.com/lastbattle/maplepacket-optimizations),
but the implementations and measurements here are specific to .NET.

## Final results

Latency is mean time per operation. Custom encryption has no per-operation managed
allocation. Full-pipeline allocation includes AES encryption and IV replacement.

| Workload | Size | Baseline | Final | Speed-up | Baseline allocation | Final allocation |
|---|---:|---:|---:|---:|---:|---:|
| AES | 32 | 883.7 ns | 632.5 ns | 1.40x | 1,288 B | 600 B |
| AES | 128 | 1.150 us | 816.2 ns | 1.41x | 1,528 B | 600 B |
| AES | 512 | 2.434 us | 1.474 us | 1.65x | 2,488 B | 600 B |
| AES | 1,460 | 5.461 us | 3.244 us | 1.68x | 4,928 B | 600 B |
| AES | 8,192 | 26.688 us | 14.897 us | 1.79x | 22,008 B | 600 B |
| Custom encrypt | 32 | 1.212 us | 158.7 ns | 7.64x | 0 B | 0 B |
| Custom encrypt | 512 | 306.799 us | 2.368 us | 129.6x | 0 B | 0 B |
| Custom encrypt | 8,192 | 78.269 ms | 37.449 us | 2,090x | 0 B | 0 B |
| Custom decrypt | 32 | 1.254 us | 169.5 ns | 7.40x | 0 B | 0 B |
| Custom decrypt | 512 | 304.902 us | 2.409 us | 126.6x | 0 B | 0 B |
| Custom decrypt | 8,192 | 77.696 ms | 37.632 us | 2,065x | 0 B | 0 B |
| Full packet | 32 | 2.146 us | 802.2 ns | 2.68x | 1,320 B | 632 B |
| Full packet | 512 | 309.940 us | 3.838 us | 80.8x | 2,520 B | 632 B |
| Full packet | 1,460 | 2.482 ms | 9.753 us | 254.5x | 4,961 B | 632 B |
| Full packet | 8,192 | 78.448 ms | 51.851 us | 1,513x | 22,072 B | 632 B |

At 512 bytes, AES Gen0 collections fell from 0.0458 to 0.0114 per 1,000
operations. AES and full-packet allocation are now constant with packet size.

Helper results:

| Helper | Baseline | Final | Result |
|---|---:|---:|---:|
| Trim user key | 4.254 ns | 2.893 ns | 1.47x |
| Get new IV | 22.226 ns | 21.776 ns | unchanged/noise |
| Client header | 3.131 ns | 1.929 ns | 1.62x |
| Server header | 2.846 ns | 2.711 ns | unchanged/noise |
| Multiply bytes | 7.384 ns | 2.836 ns | 2.60x |

The original packet-length benchmark used a compile-time-constant input and was
invalid. The corrected varying-input benchmark measures 0.575 ns without allocation;
it is not compared with the invalid baseline.

## Iteration record

Representative values below are for 512-byte packets. Allocation is for AES/full
packet respectively. A dash means the iteration only measured the affected subset.

| Iteration | Change | Encrypt | Decrypt | AES | Full packet | Allocation | Decision |
|---:|---|---:|---:|---:|---:|---:|---|
| 0 | Original baseline | 306.799 us | 304.902 us | 2.434 us | 309.940 us | 2,488 / 2,520 B | Baseline |
| 1 | Constant-time rotations and direct indexing | 2.516 us | 2.909 us | - | - | - | Kept |
| 2 | One-shot span AES per block | - | - | 7.250 us | - | 3,120 B | Rejected: provider setup per block |
| 3 | Reusable AES transform and two feedback buffers | - | - | 1.617 us | - | 640 B | Kept |
| 4 | Header/key/multiply helpers | - | - | - | - | - | Kept, except slower IV rewrite |
| 5 | Lookup tables for all rotations | 2.531 us | 2.385 us | 1.625 us | 4.233 us | 640 / 672 B | Partial keeper |
| 6 | Direct fixed rotations for both directions | 2.370 us | 2.831 us | 1.628 us | 4.046 us | 640 / 672 B | Rejected for decrypt |
| 7 | Direct fixed encrypt; lookup fixed decrypt | 2.365 us | 2.389 us | 1.627 us | 4.044 us | 640 / 672 B | Kept |
| 8 | Unrolled AES IV fill and full-block XOR | 2.364 us | 2.391 us | 1.435 us | 3.918 us | 696 / 728 B | Kept speed, allocation addressed next |
| 9 | Cached trimmed mutable WZ key | 2.366 us | 2.388 us | 1.446 us | 3.901 us | 640 / 672 B | Kept |
| 10 | Pooled AES feedback buffers | 2.386 us | 2.402 us | 1.494 us | 3.925 us | 560 / 592 B | Rejected: slower and shared-pool cost |
| 11 | One in-place AES feedback buffer | 2.372 us | 2.390 us | 1.408 us | 3.874 us | 600 / 632 B | Kept |

The attempted `BinaryPrimitives`/`BitOperations` IV shuffle measured 29.275 ns versus
22.226 ns at baseline and was reverted. The retained IV change only replaces repeated
division/modulo byte extraction with shifts and measures 21.776 ns.

## Plateau runs

The final keeper set was run unchanged seven times, followed by one confirmation after
a no-behavior-change local cleanup. Every run contains five measured iterations. Run 5
had a system-wide isolated-metric spike, but the combined pipeline remained stable;
runs 6 and 7 were added rather than discarding it.

| Run | Encrypt | Decrypt | AES | Full packet | AES/full allocation |
|---:|---:|---:|---:|---:|---:|
| 1 | 2.391 us | 2.411 us | 1.407 us | 3.829 us | 600 / 632 B |
| 2 | 2.364 us | 2.391 us | 1.414 us | 3.983 us | 600 / 632 B |
| 3 | 2.365 us | 2.396 us | 1.403 us | 3.809 us | 600 / 632 B |
| 4 | 2.392 us | 2.384 us | 1.408 us | 3.813 us | 600 / 632 B |
| 5 | 2.444 us | 2.481 us | 1.600 us | 3.810 us | 600 / 632 B |
| 6 | 2.455 us | 2.389 us | 1.403 us | 3.887 us | 600 / 632 B |
| 7 | 2.365 us | 2.399 us | 1.406 us | 3.824 us | 600 / 632 B |
| 8 (post-cleanup) | 2.376 us | 2.401 us | 1.379 us | 3.802 us | 600 / 632 B |

No subsequent candidate improved the representative pipeline without regressing
latency, allocations, provider behavior, or maintainability. This is the local
optimization plateau; larger AES gains would require retaining cryptographic provider
state across packets and introducing an explicit disposal/lifetime contract.

## Implemented changes

- Custom encryption rotations are constant-time. Variable rotations use 8x256 lookup
  tables; fixed rotations use the measured best direct/table path for each direction.
- AES uses one reusable ECB transform and one in-place 16-byte feedback buffer per call,
  with unrolled full-block XOR and the original MapleStory chunk boundaries.
- The trimmed mutable WZ key is cached and refreshed when any effective key byte changes.
- Packet header and IV byte extraction use shifts; integer packet-length decoding avoids
  the temporary `BitConverter` array.
- Byte multiplication uses bulk span copies. The misleading SIMD entry point delegates
  to the correct implementation, fixing its whole-pattern ordering for vector-sized input.
- `MapleCrypto`'s version field is readonly.

## Reproduction

```powershell
dotnet run -c Release --project benchmarks\MapleCrypto.Benchmarks\MapleCrypto.Benchmarks.csproj -- --verify
dotnet run -c Release --project benchmarks\MapleCrypto.Benchmarks\MapleCrypto.Benchmarks.csproj -- --filter '*PacketCryptoBenchmarks*'
dotnet run -c Release --project benchmarks\MapleCrypto.Benchmarks\MapleCrypto.Benchmarks.csproj -- --filter '*CryptoPrimitiveBenchmarks*'
dotnet run -c Release --project benchmarks\MapleCrypto.Benchmarks\MapleCrypto.Benchmarks.csproj -- --filter '*CryptoPlateauBenchmarks*'
dotnet test MapleLib.Tests\MapleLib.Tests.csproj -c Release --filter 'FullyQualifiedName~MapleCryptoTests'
```
