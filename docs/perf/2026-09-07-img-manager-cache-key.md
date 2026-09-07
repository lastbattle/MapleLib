# IMG manager cache-key performance

## Scope and compatibility contract

This report measures the cached `ImgFileSystemManager.LoadImage` path. The retained
change replaces two `ToLower()` results plus interpolation with one `string.Create`
allocation whose spans are lowercased with `CultureInfo.CurrentCulture`. It therefore
preserves the existing culture-sensitive key identity, including Turkish-I behavior,
while avoiding intermediate strings.

The baseline already contains the retained `LRUCache` timestamp and direct-write-lock
optimizations described in `2026-09-07-lru.md`. These results isolate only cache-key
construction; they must not be combined with the separate LRU improvement as though
both were measured against the original manager.

## Workload and environment

- Each invocation performs 256 cached loads of `String/Map.img` and verifies reference
  identity with the warmed image.
- The fixture comes from `<GMS-v95-corpus>`. Setup copies
  it and the manifest into a private temporary directory, keeping the source corpus
  read-only. `MAPLELIB_WZEXPORT_ROOT` can select another corpus.
- Job configuration: one launch, three warmups, and five measured iterations.

All runs used this command from the nested `MapleLib` repository root:

```powershell
dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*ImgFileSystemManagerBenchmarks*'
```

The timestamps below are UTC benchmark start times taken from the retained
BenchmarkDotNet log names.

## Results

| Implementation | Mean per cached load | Standard deviation | Allocation |
| --- | ---: | ---: | ---: |
| Baseline: two lowercase strings plus interpolated key | 47.77 ns | 0.561 ns | 136 B |
| Initial single-allocation candidate | 34.39 ns | 0.496 ns | 56 B |
| Confirmation 1 | 33.64 ns | 0.274 ns | 56 B |
| Confirmation 2 | 34.27 ns | 0.520 ns | 56 B |
| Confirmation 3 | 33.09 ns | 0.186 ns | 56 B |

| Run | Timestamp (UTC) | Production code state | Raw export |
| --- | --- | --- | --- |
| Baseline | 2026-09-07T21:49:18Z | Retained LRU changes; original two-`ToLower()` plus interpolation manager key | `results/img-manager-baseline.csv` |
| Initial candidate | 2026-09-07T21:51:46Z | `string.Create` cache key with current-culture span lowercasing | `results/img-manager-single-allocation.csv` |
| Confirmation 1 | 2026-09-07T22:00:22Z | Same retained single-allocation implementation | `results/img-manager-confirmation-1.csv` |
| Confirmation 2 | 2026-09-07T22:00:50Z | Same retained single-allocation implementation | `results/img-manager-confirmation-2.csv` |
| Confirmation 3 | 2026-09-07T22:02:23Z | Same retained single-allocation implementation | `results/img-manager-confirmation-3.csv` |

The confirmations are 28.3-30.7% faster than baseline and allocate 80 fewer bytes per
hit, a 58.8% reduction. The three confirmation means span 1.18 ns, so the speedup is
well outside the observed run-to-run variation.

## Correctness and decision

Focused manager and LRU tests pass 46/46. Cache-key tests compare the helper with the
previous expression under `en-US`, `tr-TR`, `de-DE`, and `el-GR`, protecting exact
current-culture lowercase and separator behavior.

Decision: retain the single-allocation key builder. It improves both latency and
allocation materially without changing the manager's cache-key semantics.
