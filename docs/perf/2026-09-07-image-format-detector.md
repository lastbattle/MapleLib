# Image format detector performance

Date: 2026-09-07

This workstream targets `ImageFormatDetector.AnalyzeImageData`, especially the `HashSet<byte>` previously used to count alpha values even though the domain is exactly 256 values. The retained implementation uses four explicitly cleared, stack-resident 64-bit presence words and an integer count. RGB cardinality, every returned statistic, and texture-format selection remain unchanged.

The benchmark measures 64×64 and 256×256 opaque-color, binary-alpha, and smooth-alpha/grayscale images through both `AnalyzeImageData` and the public `DetermineTextureFormat` caller. The correctness oracle independently reproduces every returned statistic over seeded byte-exact fixtures at four dimensions and explicitly covers all 256 alpha values. Exact tuple equality protects color and alpha counts, flags, maximum alpha, gradient, variance, and grayscale classification.

| Iteration | UTC | Command/results | Correctness | Decision |
|---|---|---|---|---|
| Baseline | 22:40:46 | Full 12-case matrix; [raw summary](results/image-format-detector-baseline/results/MapleCrypto.Benchmarks.ImageFormatDetectorBenchmarks-report-github.md) | 7/7 focused tests passed | Reference |
| Uncleared stack bitmap | 22:43 | Full 12-case matrix; [raw summary](results/image-format-detector-bitset/results/MapleCrypto.Benchmarks.ImageFormatDetectorBenchmarks-report-github.md) | Rejected by review: `stackalloc` contents are not guaranteed to be zero by the C# contract | Invalid experiment; no timing claim |
| Explicitly cleared bitmap | 22:45:37 | Full 12-case matrix; [raw summary](results/image-format-detector-bitset-cleared/results/MapleCrypto.Benchmarks.ImageFormatDetectorBenchmarks-report-github.md) | Detector plus expanded enum/string suite passed 26/26 | Retain for stable alpha-heavy gains and lower allocation |
| Smooth-alpha confirmation | 22:47 | Two representative `Analyze` cases; [raw summary](results/image-format-detector-confirmation/results/MapleCrypto.Benchmarks.ImageFormatDetectorBenchmarks-report-github.md) | No code change after 26/26 pass | Confirms final 22.45 µs and 341.98 µs means |

| Analyze workload | Baseline | Final | Allocation baseline/final |
|---|---:|---:|---:|
| 64×64 opaque color | 45.24 µs | 43.94 µs | 252.32 / 252.22 KB |
| 64×64 binary alpha | 46.79 µs | 44.03 µs | 252.32 / 252.22 KB |
| 64×64 smooth alpha | 24.62 µs | 22.39 µs; confirmation 22.45 µs | 15.25 / 12.65 KB |
| 256×256 opaque color | 980.62 µs | 1,043.68 µs | 2,273.02 / 2,272.92 KB |
| 256×256 binary alpha | 1,007.70 µs | 1,093.59 µs | 2,273.02 / 2,272.92 KB |
| 256×256 smooth alpha | 357.95 µs | 342.21 µs; confirmation 341.98 µs | 25.17 / 12.65 KB |

High-color 256-square cases are dominated by the RGB `HashSet<uint>` and varied in both baseline and final runs; their 6–9% higher single-run means do not support a regression claim. Smooth-alpha cases are stable: the final and confirmation runs agree within 0.3%, improve latency by 4.4–9.1%, and remove 2.6–12.5 KB. The bounded change is retained because it removes the alpha hash table, clears the 3% threshold on stable alpha-heavy workloads, and preserves the exact result tuple.

Commands:

```powershell
dotnet test MapleLib.Tests/MapleLib.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ImageFormatDetectorStatisticsTests|FullyQualifiedName~ImageFormatDetectorAdversarialTests"
dotnet run --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -c Release --no-restore -- --filter "*ImageFormatDetectorBenchmarks*" --artifacts "docs/perf/results/image-format-detector-baseline"
dotnet run --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -c Release --no-restore -- --filter "*ImageFormatDetectorBenchmarks*" --artifacts "docs/perf/results/image-format-detector-bitset"
dotnet run --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -c Release --no-restore -- --filter "*ImageFormatDetectorBenchmarks*" --artifacts "docs/perf/results/image-format-detector-bitset-cleared"
dotnet run --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -c Release --no-restore -- --filter "*ImageFormatDetectorBenchmarks.Analyze*SmoothAlpha*" --artifacts "docs/perf/results/image-format-detector-confirmation"
```
