# WzLib performance optimization report

## Scope and compatibility contract

This pass covers `MapleLib/WzLib`, including the core file/directory/image/property
model, WZ binary I/O and mutable keys, MS file support, serializers, media properties,
link/path resolution, and higher-level WZ structures where they participate in hot
loading paths.

Required invariants are byte-compatible WZ/IMG/MS reads and writes, equivalent object
trees and path lookup semantics, case-insensitive lookup behavior where already
documented by tests, deterministic serializer output where the existing API promises
it, thread-safety no weaker than the current implementation, and disposal/lazy-loading
semantics that do not retain file handles unexpectedly.

Internal and public APIs, class names, ownership boundaries, and data structures may be
rewritten when benchmarks justify the change. Public adapters are retained only when
repository or plausible external callers require compatibility; migrations and any
intentional breaks are recorded here.

Primary metrics are mean latency, throughput, allocated bytes per operation, and GC
counts. Release-mode .NET 10 benchmarks use representative synthetic trees plus the
bundled WZ/MS fixtures. Improvements below 3% or within observed variance are treated
as a plateau unless they materially reduce allocation or enable a later measured gain.

## Workstreams

1. Binary I/O and key paths: `WzBinaryReader`, `WzBinaryWriter`, `PartialStream`,
   `WzMutableKey`, `WzKeyGenerator`, WZ offset/string encoding, and remaining MS crypto.
2. Core object model: `WzFile`, `WzDirectory`, `WzImage`, property collections,
   path/link resolution, lookup, enumeration, cloning, lazy parsing, and disposal.
3. Serialization and media: IMG/XML/JSON/NX serializers, PNG/canvas/binary/audio/video
   properties, compression/decompression, and export/import traversal.
4. Structured consumers: `InfoTool`, `MapInfo`, and related readers only where a
   representative end-to-end benchmark shows material cost.

## Environment

- Runtime: `net10.0-windows`
- Configuration: Release
- Benchmark tool: BenchmarkDotNet 0.15.6
- Fixtures: `MapleLib.Tests/WzFiles` and deterministic generated object trees/buffers

## Iteration record

### Iteration 0 - correctness baseline

- Code state: existing working tree before this optimization pass; unrelated edits in
  `MapleLib.csproj` and `MobData.cs` preserved.
- Command: `dotnet test MapleLib.Tests/MapleLib.Tests.csproj -c Release --no-restore`
- Result: 184 passed, 0 failed, 0 skipped; 1 second reported test duration.
- Decision: baseline accepted. No performance result has been claimed yet.

### Iteration 0b - benchmark matrix correction

- Change: first WzCoreBenchmark harness build succeeded, but its independent width
  (128/1024/4096) and depth (4/16/64) parameters formed a Cartesian product for every
  method.
- Command: `dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzCoreBenchmarks*'`
- Result: process exceeded the 240-second wall-clock limit before a completed report;
  no timing result is used.
- Decision: revise the harness into separate width/mutation and depth/path classes so
  each baseline is bounded and comparable.

### Iteration 1 - core collection lookup and mutation baseline

- Code state: benchmark harness only; no production optimization.
- Commands:
  - `dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzCoreBenchmarks.DirectoryIndexerHit*'`
  - `dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzCoreBenchmarks.ImageIndexerHit*'`
  - `dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzCoreBenchmarks.AddRemoveProperty*'`
- Correctness: benchmark startup verifies crypto, WZ string/section/key behavior,
  PartialStream range behavior, and bundled WZ fixture parsing before BenchmarkDotNet
  launches. The benchmark fixture also verifies terminal lookup and parent links.

| Operation | Width 128 | Width 1024 | Width 4096 | Allocation |
| --- | ---: | ---: | ---: | ---: |
| Directory indexer hit | 972.1 ns | 4.891 us | 19.555 us | 0 B |
| Image property indexer hit | 1.030 us | 7.452 us | 31.652 us | 0 B |
| Add then remove property | 189.0 ns | 1.744 us | 6.592 us | 80 B |

- Observation: all three operations scale linearly with collection width. The
  4,096-property image lookup is the strongest first target; repeated property adds
  also inherit the linear duplicate-name scan.
- Decision: pursue an ordered, parent-aware property collection with an
  ordinal-ignore-case name index and migrate WzImage/container callers. Preserve
  insertion order, duplicate rejection, parent assignment, removal, cloning, and lazy
  parse behavior with focused tests.

### Iteration 2 - WZ ASCII string read baseline

- Code state: benchmark harness only; no production optimization.
- Command: `dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzBinaryIoBenchmarks.ReadAscii*'`
- Correctness: startup round-trips ASCII and Unicode lengths 0/1/127/128/4096 and
  verifies offset reads, section readers, PartialStream bounds, and deterministic keys.

| Encoded length | Mean | Allocation |
| ---: | ---: | ---: |
| 32 | 149.0 ns | 88 B |
| 127 | 512.9 ns | 280 B |
| 128 | 540.4 ns | 280 B |
| 4096 | 15.795 us | 8,216 B |

- Observation: the reader performs a virtual byte read and a key-size guard for every
  character. The 4,096-byte result is a useful target for hoisting key growth and using
  bulk span reads while preserving masks, stream position, and output bytes.
- Decision: implement a single-read/decrypt path with scalar fallback only where the
  underlying stream cannot satisfy the span contract, then remeasure this exact matrix.

### Iteration 3 - wildcard traversal baseline

- Code state: benchmark harness only; no production optimization.
- Command: `dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzSearchBenchmarks.WildcardTraversal*'`
- Workload: deterministic synthetic WZ files with 64/256/1,024 images and eight vector
  records per image; wildcard selects the X/Y scalar terminals and verifies result count.

| Images | Mean | Allocation | Gen0/Gen1/Gen2 per 1,000 ops |
| ---: | ---: | ---: | ---: |
| 64 | 346.8 us | 3.16 MB | 65.918 / 2.441 / 0 |
| 256 | 1.350 ms | 12.64 MB | 263.672 / 35.156 / 0 |
| 1,024 | 6.123 ms | 50.55 MB | 1,078.125 / 117.188 / 31.250 |

- Observation: the current two-phase implementation materializes every full path, then
  resolves each match back through the tree. Allocation grows by roughly 49 KiB per
  image and reaches Gen2 collections at the largest fixture.
- Decision: rewrite wildcard traversal to match path segments during a single object-tree
  walk and return objects directly, preserving ordering and public wildcard semantics.

### Iteration 4 - bulk WZ string decode

- Change: `WzBinaryReader` now grows the mutable key once, reads the encrypted ASCII or
  Unicode payload into one span, and decodes that span in place instead of calling the
  virtual scalar reader and key-size guard for every character.
- Correctness: 4 focused reader tests passed, covering ASCII/Unicode round trips at
  boundary lengths, zero and nonzero IVs, offset-position restoration, exact stream
  consumption, and truncated payload exceptions.
- Benchmark command: same `WzBinaryIoBenchmarks.ReadAscii` command as iteration 2.

| Length | Baseline | Bulk decode | Speed-up | Allocation |
| ---: | ---: | ---: | ---: | ---: |
| 32 | 149.0 ns | 119.9 ns | 1.24x | unchanged, 88 B |
| 127 | 512.9 ns | 369.6 ns | 1.39x | unchanged, 280 B |
| 128 | 540.4 ns | 383.7 ns | 1.41x | unchanged, 280 B |
| 4,096 | 15.795 us | 10.733 us | 1.47x | unchanged, 8,216 B |

- Decision: keep. The exact measured ASCII matrix improved 19.5-32.0% without an
  allocation or correctness regression. Unicode uses the same bulk-read/key-hoisting
  shape and is covered by correctness tests, but no before/after performance claim is
  made until a separately recorded baseline exists.

### Iteration 5 - first indexed property collection candidate

- Change: ordered property collections gained an ordinal-ignore-case name dictionary;
  image/subproperty/convex/canvas indexers use it, and concrete plus generic mutation
  paths maintain parent links and the index.
- Correctness: full `MapleLib.Tests` run passed 193/193.
- Benchmark commands: same `ImageIndexerHit` and `AddRemoveProperty` commands as
  iteration 1.

| Width | Lookup baseline | Indexed lookup | Speed-up | Mutation baseline | Indexed mutation |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 128 | 1.030 us | 23.11 ns | 44.6x | 189.0 ns | 2.275 us |
| 1,024 | 7.452 us | 21.67 ns | 343.9x | 1.744 us | 18.103 us |
| 4,096 | 31.652 us | 21.55 ns | 1,468.8x | 6.592 us | 76.760 us |

- Decision: revise, not yet accepted. Lookup becomes constant-time with a large measured
  win, but removal rebuilds the entire index and regresses add/remove by roughly 10-12x.
  Track duplicate counts so unique-name removal is O(1); only removal of the first true
  duplicate may scan for the next ordered match.

### Iteration 6 - single-pass wildcard traversal

- Change: wildcard/regex search walks the object tree once and returns matching objects
  directly; wildcard matching is iterative instead of recursive substring backtracking.
- Correctness: three focused search tests preserve path ordering, direct object identity,
  vector terminal handling, case sensitivity, and the historical omission of root-level
  image objects. Full `MapleLib.Tests` passed 193/193 before measurement.
- Benchmark command: same `WzSearchBenchmarks.WildcardTraversal` command as iteration 3.

| Images | Baseline | Single pass | Speed-up | Baseline allocation | Final allocation | Reduction |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 64 | 346.8 us | 43.65 us | 7.95x | 3.16 MB | 126.09 KB | 96.1% |
| 256 | 1.350 ms | 178.91 us | 7.54x | 12.64 MB | 502.64 KB | 96.1% |
| 1,024 | 6.123 ms | 857.06 us | 7.14x | 50.55 MB | 2,008.7 KB | 96.1% |

- Decision: keep. The remaining allocation is primarily construction of a candidate
  path string for each visited object; a later span/builder iteration may remove it,
  but this coherent rewrite already removes the duplicate path list and second lookup.

### Iteration 7 - duplicate-aware property index removal

- Change: name-index entries track the first ordered property and duplicate count.
  Adding/removing unique names and removing non-first duplicates update the index in
  constant time; only removing the first true duplicate scans for its successor.
- Correctness: added duplicate-count/removal coverage; 194/194 MapleLib tests passed.

| Width | Baseline mutation | Rebuild-every-remove | Duplicate-aware removal | Allocation |
| ---: | ---: | ---: | ---: | ---: |
| 128 | 189.0 ns | 2.275 us | 222.9 ns | 112 B |
| 1,024 | 1.744 us | 18.103 us | 1.848 us | 112 B |
| 4,096 | 6.592 us | 76.760 us | 6.485 us | 112 B |

- Decision: revise once more. Scaling and latency regression are resolved, but the
  heap-based name-index entry adds 32 B to every unique add/remove cycle. Store the
  entry as a dictionary value type, then confirm both latency and the original 80 B
  allocation before final acceptance.

### Iteration 8 - value-type property index entries

- Change: converted duplicate-aware index entries from heap objects to dictionary value
  types, removing the per-operation entry allocation.
- Correctness: 194/194 MapleLib tests passed after the change.
- Benchmark command: same `WzCoreBenchmarks.AddRemoveProperty` command as iteration 1.

| Width | Baseline | Final | Allocation baseline/final |
| ---: | ---: | ---: | ---: |
| 128 | 189.0 ns | 233.9 ns | 80 B / 80 B |
| 1,024 | 1.744 us | 1.751 us | 80 B / 80 B |
| 4,096 | 6.592 us | 6.381 us | 80 B / 80 B |

- Decision: keep. The large-width mutation path is slightly faster than baseline, the
  medium case is within noise, and the small-width absolute delta is ~45 ns while the
  lookup path is constant-time. The value-type layout removes the only measurable
  allocation regression.

### Iteration 9 - section-reader baseline

- Code state: accepted changes above; no section-reader optimization yet.
- Command: `dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzSectionReaderBenchmarks*'`

| Section | Mean | Allocation |
| ---: | ---: | ---: |
| 64 KiB | 1.236 us | 64.28 KB |
| 1 MiB | 49.349 us | 1,025.52 KB |

- Observation: the section buffer is required by the current API, but the constructor
  also clones the full generated WZ key and generates a throwaway replacement key.
- Decision: add an internal construction path that shares the parent `WzMutableKey`,
  exactly matching the object initializer's final ownership without redundant work.

### Iteration 10 - shared-key section-reader candidate

- Change tested: private reader constructor reused the parent's `WzMutableKey`, removing
  the cloned key and throwaway key generator.
- Correctness: 5 focused reader tests passed, including key identity and preserved source
  position/header/hash/section bytes.

| Section | Baseline | Candidate | Allocation baseline/candidate |
| ---: | ---: | ---: | ---: |
| 64 KiB | 1.236 us | 1.169 us | 64.28 KB / 64.19 KB |
| 1 MiB | 49.349 us | 54.851 us | 1,025.52 KB / 1,025.41 KB |

- Decision: revert. Saving roughly 92-112 B does not justify the measured 11.1% latency
  regression at 1 MiB; the small-buffer gain is not representative enough to retain a
  mixed result. The existing section copy remains the local plateau and broader API
  redesign would be required to remove its dominant allocation.

## Accepted plateau confirmations

The final keeper sets were rerun unchanged in isolated Release processes. Each row is a
separate BenchmarkDotNet process with three warmups and five measured iterations.

| Workload | Plateau run 1 | Plateau run 2 | Plateau run 3 | Allocation (all runs) |
| --- | ---: | ---: | ---: | ---: |
| ASCII read, length 4,096 | 10.733 us | 10.643 us | 10.710 us | 8,216 B |
| Wildcard, 64 images | 43.65 us | 43.54 us | 44.66 us | 126.09 KB |
| Wildcard, 256 images | 178.91 us | 183.23 us | 172.24 us | 502.64 KB |
| Wildcard, 1,024 images | 857.06 us | 808.27 us | 799.57 us | 2,008.7 KB |
| Indexed lookup, width 4,096 | 21.55 ns | 21.82 ns | 21.44 ns | 0 B |
| Add/remove, width 4,096 | 6.381 us | 6.275 us | 6.282 us | 80 B |
| Directory lookup, width 128 | 21.4 ns | 21.14 ns | 22.81 ns | 0 B |
| Directory lookup, width 1,024 | 21.5 ns | 21.22 ns | 21.59 ns | 0 B |
| Directory lookup, width 4,096 | 21.7 ns | 21.26 ns | 21.73 ns | 0 B |

The variation is consistent with the observed system noise; no run regressed the chosen
metric or allocation acceptance threshold.

### Iteration 11 - directory name indexes

- Change: `WzDirectory` now maintains case-insensitive image and child-directory
  indexes with duplicate counts. The indexes are updated on add/remove, clone, and
  dispose paths; mutable-name edits fall back to the legacy ordered scan so stale
  keys cannot change behavior.
- Correctness: the complete `MapleLib.Tests` suite passed after the change (196/196).
- Baseline command: `dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzCoreBenchmarks.DirectoryIndexerHit*'`

| Width | Lookup baseline | Indexed lookup | Speed-up | Add/remove baseline | Indexed add/remove | Lookup allocation | Add/remove allocation |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 128 | 972.1 ns | 21.4 ns | 45.4x | 48.62 ns | 89.37 ns | 0 B / 0 B | 288 B / 288 B |
| 1,024 | 4.891 us | 21.5 ns | 227.5x | 444.58 ns | 489.78 ns | 0 B / 0 B | 288 B / 288 B |
| 4,096 | 19.555 us | 21.7 ns | 901.2x | 1.773 us | 1.883 us | 0 B / 0 B | 288 B / 288 B |

- Decision: keep. Lookup becomes width-independent and removes the dominant linear
  scan cost. Mutation overhead is approximately 41-46 ns at small widths and 110 ns
  at width 4,096, with allocation unchanged; this is an acceptable trade for the
  substantially more frequent lookup path.

## Area disposition and next plan

| Area | Current disposition | Next measured target |
| --- | --- | --- |
| `WzBinaryReader` string decode | Optimized and accepted | Unicode before/after matrix; long null-terminated strings |
| `WzMutableKey` growth and clones | Not changed | Multi-growth benchmark; preserve 4 KiB rounding and thread-safety |
| `PartialStream` | Not changed | Async boundary correctness, then span/CopyTo only if workload justifies it |
| `WzPropertyCollection` / nested containers | Optimized and accepted | Unicode and mutable-name stress matrix; retain ordered semantics |
| `WzDirectory` image/directory lookup | Optimized and accepted | Large-tree parse/save regression check; monitor index rebuilds after rename-heavy workloads |
| `WzFile` wildcard/regex search | Optimized and accepted | Segment-aware wildcard matcher to remove residual candidate path strings |
| WZ parse/save pipeline | Existing prior plateau retained | Re-run large `Data.wz` parse/save after collection index to check memory impact |
| PNG/canvas/media | Existing prior plateau retained | Parallel-for threshold and format matrix; avoid regressions to prior pixel checksums |
| XML/JSON/BSON serializers | Benchmarked, no code change kept this pass | Stream/DOM allocation comparison and exception-safe unparse paths |
| NX/export and raw/video/audio/Lua | Inventory only | Add format-specific fixtures before any rewrite; preserve binary layout contracts |

Serialization probe baselines on deterministic scalar trees (100/1,000/10,000 values)
were recorded for Classic XML (121.1 us / 235.3 us / 1.068 ms; 51.51 / 450.09 /
4,498.68 KB), combined XML (107.8 us / 231.3 us / 1.100 ms; 51.62 / 450.20 /
4,498.79 KB), and JSON (313.5 us / 479.5 us / 2.348 ms; 42.7 / 389.82 /
3,855.58 KB). Fixture parse probes currently measure 29.46 us/38.59 us for GMS95
directory/full image parsing and 24.75 us/152.55 us for TMS113 item parsing; these are
stored as baselines for the next pass rather than claiming an unmeasured gain.

## Prior measured work retained

Existing reports already establish plateaus for Maple packet crypto, ChaCha20, and the
MSFile/SNOW2 path. Those implementations remain in scope for profiling, but will only
be revisited when a new end-to-end workload or candidate can improve beyond their
recorded plateau.
