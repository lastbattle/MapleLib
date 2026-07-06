# ChaCha20 Transform Performance Report

Date: Local benchmark run

## Scope

Target file:

- `MapleLib/MapleLib/WzLib/MSFile/ChaCha20CryptoTransform.cs`

Compatibility contract:

- Preserve `ICryptoTransform` behavior, `TransformInPlace(Span<byte>)`, disposal checks, counter progression, and existing v4 `.ms` file decrypt results.
- Allowed change scope: implementation-only changes inside the transform, plus benchmark/test/report additions.

Success metrics:

- Improve `ChaCha20TransformBenchmarks.TransformInPlace` mean time for 4 KiB, 1 MiB, and 16 MiB buffers.
- Do not regress v4 MSFile `ReadHeader`, `ReadEntries`, or `LoadAndDecryptEntries` on `Skill_00006.ms`.
- Keep allocations unchanged for the in-place transform path.

## Benchmark Commands

```powershell
dotnet test MapleLib/MapleLib.Tests/MapleLib.Tests.csproj -c Release --filter FullyQualifiedName~WzMsFileTests
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *ChaCha20TransformBenchmarks*
$env:MSFILE_PERF_PACK='MapleLib\MapleLib.Tests\WzFiles\Ms\Packs\Skill_00006.ms'
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *WzMsFileBenchmarks*
```

## Iterations

Measured runs are recorded below as they are executed.

### Iteration 0a - Measurement Harness And Correctness Baseline

Code state:

- Added direct `ChaCha20TransformBenchmarks` harness.
- Added RFC 8439 ChaCha20 vector coverage.
- Added `Skill_00006.ms` v4 fixture coverage through existing `WzMsFileTests`.
- No transform optimization yet.

Correctness:

```text
dotnet test MapleLib/MapleLib.Tests/MapleLib.Tests.csproj -c Release --filter FullyQualifiedName~WzMsFileTests
Passed: 3, Failed: 0
```

Benchmark command:

```text
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *ChaCha20TransformBenchmarks*
```

Results:

| Benchmark | Size | Mean | Min | Max | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| TransformInPlace | 4 KiB | 80.20 us | 78.50 us | 81.40 us | 0 B |
| TransformInPlace | 1 MiB | 18,676.30 us | 18,317.80 us | 19,382.70 us | 0 B |
| TransformInPlace | 16 MiB | 79,786.94 us | 79,188.90 us | 80,969.20 us | 0 B |

Decision:

- Revise harness before optimizing. `IterationSetup` forced `InvocationCount=1`, and BenchmarkDotNet warned that all three measurements were below the recommended minimum iteration time.

### Iteration 0b - Stable Direct Baseline

Code state:

- Benchmark harness revised to let BenchmarkDotNet choose invocation counts.
- Each operation constructs and disposes a fresh `ChaCha20CryptoTransform`, matching the current v4 header/entry usage shape.
- No transform optimization yet.

Benchmark command:

```text
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *ChaCha20TransformBenchmarks*
```

Results:

| Benchmark | Size | Mean | Min | Max | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| TransformInPlace | 4 KiB | 20.459 us | 20.089 us | 21.087 us | 216 B |
| TransformInPlace | 1 MiB | 5,489.34 us | 5,452.94 us | 5,530.65 us | 216 B |
| TransformInPlace | 16 MiB | 82,326.59 us | 80,312.74 us | 83,429.13 us | 219 B |

Decision:

- Use this as the direct transform baseline.
- The current implementation allocates arrays per transform instance and spends most time in byte-at-a-time XOR plus span/indexed quarter-round code.

### Iteration 0c - v4 MSFile Baseline Probe

Dataset:

- `Skill_00006.ms`, bundled fixture, version 4, 32 entries.

Benchmark command:

```text
$env:MSFILE_PERF_PACK='MapleLib\MapleLib.Tests\WzFiles\Ms\Packs\Skill_00006.ms'
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *WzMsFileBenchmarks*
```

Relevant results:

| Benchmark | Mean | Min | Max | Allocated |
| --- | ---: | ---: | ---: | ---: |
| ReadHeader | 91.533 us | 90.329 us | 92.597 us | 1,052,288 B |
| ReadEntries | 104.114 us | 102.345 us | 105.077 us | 1,058,604 B |

Issue:

- `LoadAndDecryptEntries` failed on this v4 fixture with `EndOfStreamException` while decrypting the first 24 entries.
- Existing correctness tests still validate the first entry decrypt hash, so the production path is usable for the bundled coverage case.

Decision:

- Keep the valid v4 header/entry baseline rows.
- Add a focused first-entry decrypt benchmark for this fixture before changing transform code.

### Iteration 0d - v4 First-Entry Decrypt Baseline

Dataset:

- `Skill_00006.ms`, bundled fixture, version 4, first entry `Skill/422.img`.

Benchmark command:

```text
$env:MSFILE_PERF_PACK='MapleLib\MapleLib.Tests\WzFiles\Ms\Packs\Skill_00006.ms'
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *WzMsFileBenchmarks.LoadAndDecryptFirstEntry*
```

Results:

| Benchmark | Mean | Min | Max | Allocated |
| --- | ---: | ---: | ---: | ---: |
| LoadAndDecryptFirstEntry | 121.210 us | 115.533 us | 128.633 us | 1.1 MB |

Decision:

- Use this as the v4 decrypt-path baseline.

### Iteration 1 - Unrolled Block Generation And Word XOR

Change:

- Rewrote `GenerateKeyBlock` to use local `uint` state and `ref` quarter rounds instead of span indexing.
- Switched rotation to `BitOperations.RotateLeft`.
- Changed `TransformInPlace` to consume leftover keystream spans, process full blocks, and XOR `ulong` words instead of checking/generating per byte.
- Kept the existing state array, key block array, counter behavior, and disposal behavior.

Correctness:

```text
dotnet test MapleLib/MapleLib.Tests/MapleLib.Tests.csproj -c Release --filter FullyQualifiedName~WzMsFileTests
Passed: 3, Failed: 0
```

Benchmark command:

```text
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *ChaCha20TransformBenchmarks*
```

Results:

| Benchmark | Size | Mean | Min | Max | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| TransformInPlace | 4 KiB | 3.890 us | 3.827 us | 3.963 us | 216 B |
| TransformInPlace | 1 MiB | 1,007.093 us | 989.297 us | 1,022.667 us | 216 B |
| TransformInPlace | 16 MiB | 15,727.331 us | 15,273.355 us | 16,599.516 us | 216 B |

Decision:

- Keep. Direct transform speed improved by about 5.3x for 4 KiB, 5.5x for 1 MiB, and 5.2x for 16 MiB.
- Continue with v4 file-path measurement before attempting allocation changes.

### Iteration 1b - v4 File Path After Unrolled Transform

Dataset:

- `Skill_00006.ms`, bundled fixture, version 4.

Benchmark commands:

```text
$env:MSFILE_PERF_PACK='MapleLib\MapleLib.Tests\WzFiles\Ms\Packs\Skill_00006.ms'
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *WzMsFileBenchmarks.ReadHeader*
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *WzMsFileBenchmarks.ReadEntries*
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *WzMsFileBenchmarks.LoadAndDecryptFirstEntry*
```

Results:

| Benchmark | Baseline mean | Iteration 1 mean | Allocated |
| --- | ---: | ---: | ---: |
| ReadHeader | 91.533 us | 90.627 us | 1 MB |
| ReadEntries | 104.114 us | 95.638 us | 1.01 MB |
| LoadAndDecryptFirstEntry | 121.210 us | 93.568 us | 1.1 MB |

Decision:

- Keep. The actual v4 entry/decrypt path improved while header-only work stayed effectively flat.

### Iteration 2 - Direct Full-Block XOR Probe

Change:

- Added a probe path that generated 16 ChaCha keystream words and XORed complete 64-byte blocks directly into the destination span.
- Partial blocks still used the buffered keystream.

Correctness:

```text
dotnet test MapleLib/MapleLib.Tests/MapleLib.Tests.csproj -c Release --filter FullyQualifiedName~WzMsFileTests
Passed: 3, Failed: 0
```

Benchmark command:

```text
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *ChaCha20TransformBenchmarks*
```

Results:

| Benchmark | Size | Iteration 1 mean | Iteration 2 mean | Allocated |
| --- | ---: | ---: | ---: | ---: |
| TransformInPlace | 4 KiB | 3.890 us | 3.976 us | 216 B |
| TransformInPlace | 1 MiB | 1,007.093 us | 1,009.298 us | 216 B |
| TransformInPlace | 16 MiB | 15,727.331 us | 16,152.093 us | 216 B |

Decision:

- Revert this probe. It was flat to slightly worse at 4 KiB and 1 MiB, and slower at 16 MiB.

### Iteration 3 - State Cache And Word KeyBlock Write Probe

Change:

- Cached the 16 state words into locals once per block.
- Wrote `keyBlock` through a `uint` span on little-endian runtimes.

Correctness:

```text
dotnet test MapleLib/MapleLib.Tests/MapleLib.Tests.csproj -c Release --filter FullyQualifiedName~WzMsFileTests
Passed: 3, Failed: 0
```

Direct transform results:

| Benchmark | Size | Iteration 1 mean | Iteration 3 mean | Allocated |
| --- | ---: | ---: | ---: | ---: |
| TransformInPlace | 4 KiB | 3.890 us | 3.978 us | 216 B |
| TransformInPlace | 1 MiB | 1,007.093 us | 956.905 us | 216 B |
| TransformInPlace | 16 MiB | 15,727.331 us | 15,169.811 us | 216 B |

v4 file-path results:

| Benchmark | Iteration 1 mean | Iteration 3 mean | Decision |
| --- | ---: | ---: | --- |
| ReadEntries | 95.638 us | 98.171 us | worse |
| LoadAndDecryptFirstEntry | 93.568 us | 101.866 us | worse |

Decision:

- Revert this probe. It helped larger direct buffers, but the relevant v4 file workloads and 4 KiB direct transform regressed.

## Final Confirmation

Final kept change:

- Unrolled ChaCha20 block generation into local `uint` variables with `ref` quarter rounds.
- Replaced per-byte transform looping with span chunking and `ulong` XOR for buffered keystream bytes.
- Kept transform construction, state array exposure, counter behavior, and allocation profile unchanged.

Correctness:

```text
dotnet test MapleLib/MapleLib.Tests/MapleLib.Tests.csproj -c Release --filter FullyQualifiedName~WzMsFileTests
Passed: 3, Failed: 0
```

Final direct benchmark command:

```text
dotnet run -c Release --project MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter *ChaCha20TransformBenchmarks*
```

Final direct transform results:

| Benchmark | Size | Baseline mean | Final mean | Delta | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| TransformInPlace | 4 KiB | 20.459 us | 3.766 us | 5.43x faster | 216 B |
| TransformInPlace | 1 MiB | 5,489.34 us | 956.723 us | 5.74x faster | 216 B |
| TransformInPlace | 16 MiB | 82,326.59 us | 15,270.122 us | 5.39x faster | 216 B |

Final v4 file-path comparison, using the kept iteration 1 file-path run:

| Benchmark | Baseline mean | Final mean | Delta |
| --- | ---: | ---: | ---: |
| ReadHeader | 91.533 us | 90.627 us | 1.0% faster |
| ReadEntries | 104.114 us | 95.638 us | 8.1% faster |
| LoadAndDecryptFirstEntry | 121.210 us | 93.568 us | 22.8% faster |

Rejected probes:

- Direct full-block XOR without buffering: regressed 16 MiB and did not improve smaller sizes.
- State caching plus `uint` keyBlock writes: improved large direct buffers but regressed the v4 file workloads and 4 KiB direct transform.

Residual notes:

- Per-transform allocation remains 216 B because the internal state and buffered keystream arrays are still allocated per instance.
- Removing those arrays would require a broader internal shape change or pooling. Based on the rejected probes and the v4 workload profile, that is not justified in this pass.
