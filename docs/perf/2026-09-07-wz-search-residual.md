# WZ wildcard and regex traversal performance

## Scope and compatibility contract

This report measures the residual candidate-path allocation in `WzFile` wildcard and
regex traversal. The retained implementation uses one `ArrayPool<char>`-backed path
builder per search, appends and restores segments during recursion, and passes the
resulting `ReadOnlySpan<char>` directly to matching. It removes `string.Join` from
every visited candidate.

Required behavior includes traversal order, case sensitivity, matching `*` across `/`,
root-image omission, vector X/Y object identity, null failure behavior, empty and null
path segments, sibling path restoration, and paths longer than the initial pooled
buffer.

## Workload and environment

- The deterministic in-memory tree contains 256 images. Each image contains eight
  vector properties, yielding 4,096 matching X/Y terminal paths.
- Matching and no-match wildcard and regex searches exercise the public `WzFile`
  APIs. Job configuration is one launch, three warmups, and five measured iterations.

The baseline and full candidate runs used this command from the nested `MapleLib`
repository root:

```powershell
dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzSearchBenchmarks*'
```

The two final wildcard confirmations used:

```powershell
dotnet run -c Release --no-build --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -- --filter '*WzSearchBenchmarks.WildcardTraversal*'
```

The timestamps below are UTC benchmark start times taken from the retained
BenchmarkDotNet log names.

## Results

| Workload | Baseline | Pre-fix shared buffer | Allocation before | Candidate allocation |
| --- | ---: | ---: | ---: | ---: |
| Wildcard, 4,096 matches | 173.0 us | 108.81 us | 502.64 KB | 66,072 B |
| Regex, 4,096 matches | 311.1 us | 241.29 us | 505.88 KB | 69,384 B |
| Wildcard, no matches | 181.7 us | 96.53 us | 438.38 KB | 272 B |
| Regex, no matches | 718.8 us | 676.84 us | 440.66 KB | 2,600 B |

The full shared-buffer column was measured before the final null/empty-name and null
wildcard compatibility fixes. Those fixes affect edge-case path construction and the
string overload's exception behavior; the ordinary benchmark fixture contains no null
or empty names. The final code state was then measured by two compact wildcard
confirmations. Regex was not rerun after the compatibility fixes, so its candidate rows
are retained as directional evidence and are not presented as final-code timing.

Two compact wildcard confirmations, run after the compatibility fixes, measured
124.25 us and 124.51 us for the matching workload and 102.24 us and 108.46 us for the
no-match workload. Allocations remained 66,072 B and 272 B respectively. Against the
173.0 us baseline, the stable matching confirmations are about 28% faster. Against the
181.7 us no-match baseline, they are about 40-44% faster. Allocations fall about 87%
for matching wildcard traversal and more than 99.9% for no-match traversal.

The baseline no-match wildcard row had a 12.70 us standard deviation and a wide error
interval caused by an outlier. Regex no-match also varied substantially, so the regex
rows support the allocation result but are not used for a strong timing claim.

| Run | Timestamp (UTC) | Production code state | Raw export |
| --- | --- | --- | --- |
| Baseline | 2026-09-07T21:52:43Z | Per-candidate `List<string>` plus `string.Join` | `results/wz-search-residual-baseline.csv` |
| Rejected stack/span | 2026-09-07T21:54:48Z | Per-node span copying; reduced allocation without reliable matched speedup | `results/wz-search-stack-span.csv` |
| Shared buffer, full matrix | 2026-09-07T21:59:13Z | Pooled shared builder before null/empty-name compatibility fixes | `results/wz-search-shared-buffer.csv` |
| Final wildcard confirmation 1 | 2026-09-07T22:02:56Z | Retained builder with null/empty-name, sibling restoration, long-path, and null matcher compatibility fixes | `results/wz-search-shared-buffer-confirmation-1.csv` |
| Final wildcard confirmation 2 | 2026-09-07T22:03:35Z | Same final retained implementation | `results/wz-search-shared-buffer-confirmation-2.csv` |

## Correctness and decision

Focused WZ search tests pass 7/7 and cover every compatibility requirement above,
including direct object identities and ordering. Buffer return is protected by
`finally`, and long paths grow the rented buffer.

Decision: retain the shared builder. The confirmed wildcard improvement clears the
3% threshold by a wide margin, while candidate-path allocations are almost entirely
removed on no-match traversals.
