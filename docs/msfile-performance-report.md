# MSFile Performance Optimization Report

Date: 2026-07-06

## Scope

Optimized and benchmarked the MapleLib MSFile hot path:

- `MapleLib/MapleLib/WzLib/MSFile/Snow2CryptoTransform.cs`
- `MapleLib/MapleLib/WzLib/MSFile/WzMsConstants.cs`
- `MapleLib/MapleLib/WzLib/MSFile/WzMsEntry.cs`
- `MapleLib/MapleLib/WzLib/MSFile/WzMsFile.cs`
- `UnitTest_Perf/WzMsLivePerfTests.cs`

## Benchmark Setup

Command:

```powershell
$env:RUN_MSFILE_LIVE_PERF='1'
$env:MSFILE_PERF_RUNS='5'
$env:MSFILE_PERF_DECRYPT_ENTRIES='8'
dotnet test UnitTest_Perf/UnitTest_Perf.csproj -c Release --no-build --filter Name=RunLiveMsFilePerfBenchmarks --logger "console;verbosity=minimal"
```

The harness probes the pack directory configured by `MSFILE_PERF_PACK_DIR`. Local absolute paths are intentionally omitted from this report.
`Mob_00003.ms` is skipped.

The current reader did not validate a physical SEA pack with the tested logical filename candidates, so the benchmark falls back to a synthetic valid MS wrapper built from live bytes read out of `Mob_00000.ms`. Each generated benchmark report records this as:

```text
Pack: <local pack directory>\Mob_00000.ms (synthetic MS wrapper)
Synthetic wrapper: True
```

The final harness validates synthetic plaintext size and hash before recording metrics, then uses 32 warmup iterations per metric before the measured 5 iterations. This avoids tiered-JIT startup noise that showed up in earlier runs.

## Metrics

Each report records:

- Mean, min, and max milliseconds.
- Allocated bytes on the current thread.
- Gen0, Gen1, and Gen2 collections.
- A sink value to prevent dead-code removal.

Benchmarked operations:

- `Snow2.TransformBlock.16MiB`
- `Snow2.TransformFinalBlock.1MiB+3`
- `WzMsFile.ReadHeader`
- `WzMsFile.ReadEntries`
- `WzMsFile.LoadAndDecryptFirst8`
- `WzMsEntry.RecalculateFields.100k`

Reports are written to:

```text
UnitTest_Perf/bin/Release/net10.0-windows/msfile-perf/msfile-live-perf-*.md
```

## Optimization Changes

`Snow2CryptoTransform`:

- Added `TransformInPlace(Span<byte>)` for callers that already own mutable buffers.
- Removed the extra padded allocation in `TransformFinalBlock`; final partial words now use `stackalloc`.
- Removed the finalizer, leaving normal `IDisposable` cleanup.
- Made `encrypting` readonly.
- Changed the full-word transform loop to process `uint` spans instead of slicing every 4-byte word.

`WzMsEntry`:

- Replaced division-based alignment with the existing page mask.
- Replaced LINQ byte summing with a direct loop, removing per-call enumerable allocation.

`WzMsFile`:

- Derives header, entry-table, and image keys into caller-provided stack spans instead of returning new arrays.
- Replaced LINQ character and salt sums with direct loops.
- Reused `WzMsConstants` page masks and padding constants in read/write code.
- Pre-sized encrypted data storage during save.
- Pooled entry-name character buffers while reading entries.
- Changed entry encrypt/decrypt to in-memory SNOW passes over buffers, avoiding nested `CryptoStream` overhead in the hot path.

`UnitTest_Perf`:

- Added a gated live/synthetic MSFile benchmark harness.
- Made the older BenchmarkDotNet array benchmark opt-in via `RUN_ARRAY_BENCHMARKS=1` so filtered MSFile test runs do not launch unrelated benchmarks.
- Added synthetic plaintext hash validation and explicit warmup iterations to make plateau runs stable.

## Iteration History

All times are mean milliseconds for the generated report. Early rows before warmup are retained for chronology, but final scoring uses the warmed plateau.

| Execution | Change point | Snow block | Snow final | Header | Entries | Decrypt 8 | Recalc 100k |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | Baseline harness | 39.861 | 0.915 | 0.017 | 0.047 | 1.003 | 2.279 |
| 2 | First allocation pass | 45.219 | 2.808 | 0.010 | 0.043 | 2.526 | 0.524 |
| 3 | SNOW loop experiment | 27.736 | 0.790 | 0.010 | 0.045 | 0.785 | 0.444 |
| 4 | Entry-name pooling pass | 40.381 | 0.824 | 0.010 | 0.093 | 0.786 | 0.450 |
| 5 | Plateau probe 1 | 44.399 | 2.983 | 0.009 | 0.047 | 2.578 | 0.494 |
| 6 | Plateau probe 2 | 44.914 | 2.896 | 0.020 | 0.046 | 2.745 | 0.459 |
| 7 | Plateau probe 3 | 45.251 | 2.929 | 0.012 | 0.049 | 2.639 | 0.498 |
| 8 | Segmented-loop probe | 43.291 | 2.728 | 0.014 | 0.045 | 2.474 | 0.462 |
| 9 | Hybrid decrypt probe | 29.624 | 0.851 | 0.010 | 0.045 | 0.834 | 0.467 |
| 10 | Unwarmed plateau 1 | 43.657 | 2.747 | 0.017 | 0.042 | 2.776 | 0.487 |
| 11 | Unwarmed plateau 2 | 43.036 | 2.905 | 0.011 | 0.042 | 2.599 | 0.454 |
| 12 | Unwarmed plateau 3 | 43.607 | 2.807 | 0.008 | 0.045 | 2.633 | 0.457 |
| 13 | Direct decrypt, no warmup 1 | 41.954 | 0.817 | 0.008 | 0.044 | 0.826 | 0.451 |
| 14 | Direct decrypt, no warmup 2 | 43.954 | 2.811 | 0.009 | 0.046 | 2.746 | 0.473 |
| 15 | Direct decrypt, no warmup 3 | 44.823 | 2.888 | 0.029 | 0.045 | 2.574 | 0.444 |
| 16 | Direct decrypt, no warmup 4 | 45.065 | 2.831 | 0.014 | 0.041 | 2.661 | 0.454 |
| 17 | Direct decrypt, no warmup 5 | 43.587 | 3.038 | 0.012 | 0.042 | 2.564 | 0.655 |
| 18 | Warmed final 1 | 12.364 | 0.989 | 0.014 | 0.040 | 0.807 | 0.440 |
| 19 | Warmed final 2 | 12.585 | 0.811 | 0.007 | 0.064 | 0.910 | 0.542 |
| 20 | Warmed final 3 | 12.464 | 0.843 | 0.024 | 0.046 | 0.888 | 0.498 |
| 21 | Warmed final 4 | 13.162 | 0.826 | 0.010 | 0.088 | 1.110 | 0.448 |
| 22 | Warmed final 5 | 12.537 | 0.989 | 0.020 | 0.050 | 0.866 | 0.579 |
| 23 | Validated warmed final 1 | 12.445 | 0.812 | 0.030 | 0.055 | 0.911 | 0.607 |
| 24 | Validated warmed final 2 | 12.382 | 1.010 | 0.020 | 0.053 | 0.889 | 0.500 |
| 25 | Validated warmed final 3 | 12.505 | 1.025 | 0.025 | 0.048 | 1.047 | 0.440 |
| 26 | Validated warmed final 4 | 12.340 | 0.933 | 0.013 | 0.046 | 1.027 | 0.442 |
| 27 | Validated warmed final 5 | 12.511 | 0.870 | 0.024 | 0.059 | 0.970 | 0.544 |

## Final Plateau

Mean of the five validated warmed final report means:

| Metric | Mean ms | Min run mean | Max run mean | Allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Snow2.TransformBlock.16MiB | 12.437 | 12.340 | 12.511 | 280 |
| Snow2.TransformFinalBlock.1MiB+3 | 0.930 | 0.812 | 1.025 | 1,048,888 |
| WzMsFile.ReadHeader | 0.022 | 0.013 | 0.030 | 1,424 |
| WzMsFile.ReadEntries | 0.052 | 0.046 | 0.059 | 5,032 |
| WzMsFile.LoadAndDecryptFirst8 | 0.969 | 0.889 | 1.047 | 1,795,880 |
| WzMsEntry.RecalculateFields.100k | 0.507 | 0.440 | 0.607 | 344 |

No measured final plateau run triggered Gen0, Gen1, or Gen2 collections for these metrics.

## Result Summary

Compared with the baseline report:

- `Snow2.TransformBlock.16MiB`: 39.861 ms to 12.437 ms, about 3.2x faster.
- `WzMsEntry.RecalculateFields.100k`: 2.279 ms to 0.507 ms, about 4.5x faster.
- `WzMsEntry.RecalculateFields.100k` allocation: 3,200,344 bytes to 344 bytes.
- `WzMsFile.LoadAndDecryptFirst8`: 1.003 ms to 0.969 ms, with allocation reduced from 1,801,784 bytes to 1,795,880 bytes.
- `WzMsFile.ReadEntries` allocation: 6,328 bytes to 5,032 bytes.
- `Snow2.TransformFinalBlock.1MiB+3` allocation: 3,146,104 bytes to 1,048,888 bytes.

The final plateau is stable across five warmed runs. Further gains would likely require a deeper rewrite of the SNOW2 state machine or a broader MSFile API change to avoid returning decrypted payloads as fresh `MemoryStream` instances.

## Further Optimization Investigation

This section records the follow-up investigation into deeper SNOW2 state-machine changes and broader MSFile API changes. These runs use the same live-byte synthetic wrapper, 32 warmup iterations, 5 measured iterations, and synthetic plaintext hash validation.

| Execution | Change point | Snow block | Snow final | Header | Entries | Decrypt 8 | Recalc 100k | Decrypt alloc |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 28 | Fresh follow-up baseline | 12.963 | 0.824 | 0.013 | 0.068 | 0.905 | 0.452 | 1,795,880 |
| 29 | Decrypt directly to byte array from archive stream | 17.385 | 1.392 | 0.022 | 0.067 | 0.823 | 0.433 | 902,248 |
| 30 | Pool SNOW2 keystream buffer | 12.713 | 0.824 | 0.030 | 0.060 | 0.868 | 0.442 | 900,664 |
| 31 | Batch all full SNOW2 keystream blocks | 11.402 | 1.445 | 0.022 | 0.070 | 1.070 | 0.446 | 900,664 |
| 32 | Batch only large SNOW2 buffers | 11.241 | 1.012 | 0.032 | 0.108 | 0.930 | 0.437 | 900,664 |
| 33 | Batch only large SNOW2 buffers repeat | 11.364 | 0.975 | 0.039 | 0.091 | 0.972 | 0.438 | 900,664 |
| 34 | Cache per-file image-key salt hash | 12.701 | 0.876 | 0.015 | 0.047 | 0.921 | 0.478 | 900,664 |
| 35 | Cache per-file image-key salt hash repeat | 12.472 | 0.819 | 0.029 | 0.054 | 0.895 | 0.642 | 900,664 |
| 36 | Final kept plateau 1 | 12.510 | 0.815 | 0.017 | 0.050 | 0.909 | 0.435 | 900,664 |
| 37 | Final kept plateau 2 | 12.499 | 0.851 | 0.012 | 0.053 | 0.807 | 0.445 | 900,664 |
| 38 | Final kept plateau 3 | 12.780 | 0.821 | 0.013 | 0.045 | 0.816 | 0.468 | 900,664 |
| 39 | Final kept plateau 4 | 12.399 | 0.864 | 0.016 | 0.058 | 0.841 | 0.504 | 900,664 |
| 40 | Final kept plateau 5 | 13.884 | 0.834 | 0.023 | 0.060 | 0.814 | 0.448 | 900,664 |

Follow-up final plateau, mean of executions 36-40:

| Metric | Mean ms | Min run mean | Max run mean | Allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Snow2.TransformBlock.16MiB | 12.814 | 12.399 | 13.884 | 192 |
| Snow2.TransformFinalBlock.1MiB+3 | 0.837 | 0.815 | 0.864 | 1,048,800 |
| WzMsFile.ReadHeader | 0.016 | 0.012 | 0.023 | 1,336 |
| WzMsFile.ReadEntries | 0.053 | 0.045 | 0.060 | 4,856 |
| WzMsFile.LoadAndDecryptFirst8 | 0.837 | 0.807 | 0.909 | 900,664 |
| WzMsEntry.RecalculateFields.100k | 0.460 | 0.435 | 0.504 | 344 |

Follow-up decisions:

- Kept direct archive-stream decrypt to a plaintext byte array. This avoids staging encrypted `entry.Data` in the load/decrypt path and changed `WzMsFile.LoadAndDecryptFirst8` allocation from 1,795,880 bytes at execution 28 to 900,664 bytes in the final plateau.
- Kept pooled SNOW2 keystream storage. This changed `Snow2.TransformBlock.16MiB` allocation from 280 bytes at execution 28 to 192 bytes in the final plateau, and similarly reduced MSFile header/entry/decrypt allocations.
- Rejected the deeper batched SNOW2 transform loop. It improved the isolated 16 MiB transform, but it consistently regressed smaller `TransformFinalBlock` and MSFile decrypt workloads.
- Rejected per-file image-key salt-hash caching. It did not produce a stable measured win and added state to `WzMsFile`.
