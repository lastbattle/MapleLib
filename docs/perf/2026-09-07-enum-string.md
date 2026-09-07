# Enum and short string helper performance

Date: 2026-09-07

This workstream covers three integer-to-enum helpers, `InventoryTypeExtensions.GetByType`, both character-job display-name formatters, and `StringUtility.CapitalizeFirstCharacter`. The retained implementations replace reflection and enumeration with generic or direct defined-value checks, build job names in one exact-sized allocation, and capitalize directly into a new string. Tests pin valid and invalid enum fallbacks, including the current non-null `InventoryType.NONE` result for an unknown byte, representative special-case job names, progression-number behavior, legacy spacing, Unicode digits and capitalization, empty strings, same-instance behavior for an unchanged first character, and the current null exception.

The BenchmarkDotNet harness is `benchmarks/MapleCrypto.Benchmarks/EnumStringBenchmarks.cs`. Enum conversions use runtime-filled valid/invalid arrays and 1,024 operations per invocation so loop overhead and constant folding do not dominate. Allocation-producing string helpers are measured separately.

| Iteration | UTC | Code state | Results | Correctness | Decision |
|---|---|---|---|---|---|
| Baseline | 22:32:55–22:33:27 | Reflection enum checks/enumeration; LINQ/per-character job-name allocation; temporary char array plus substring capitalization | [enum](results/enum-string-baseline/results/MapleCrypto.Benchmarks.EnumConversionBenchmarks-report-github.md), [strings](results/enum-string-baseline/results/MapleCrypto.Benchmarks.StringHelperBenchmarks-report-github.md) | 16/16 focused tests passed | Reference |
| Direct | 22:36:19–22:36:53 | Generic/direct enum checks; exact-size formatting and capitalization | [enum](results/enum-string-direct/results/MapleCrypto.Benchmarks.EnumConversionBenchmarks-report-github.md), [strings](results/enum-string-direct/results/MapleCrypto.Benchmarks.StringHelperBenchmarks-report-github.md) | 16/16 focused tests passed | Retain; every measured path is materially faster and allocates less |

Representative results:

| Scenario | Baseline | Direct | Allocation change |
|---|---:|---:|---:|
| Character sub-job, invalid | 5.819 ns | 1.833 ns | 24 B to 0 B |
| Quest area, valid | 8.143 ns | 4.258 ns | 24 B to 0 B |
| Quest medal, valid | 4.834 ns | 0.802 ns | 24 B to 0 B |
| Inventory type, valid | 13.071 ns | 0.364 ns | 120 B to 0 B |
| Character job formatting | 184.07 ns | 71.42 ns | 880 B to 168 B |
| Pre-Big-Bang job formatting | 83.27 ns | 23.78 ns | 632 B to 80 B |
| Capitalize first character | 12.92 ns | 5.258 ns | 144 B to 48 B |

Commands:

```powershell
dotnet test MapleLib.Tests/MapleLib.Tests.csproj -c Release --filter "FullyQualifiedName~EnumStringOptimizationTests"
dotnet run --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -c Release --no-restore -- --filter "*EnumConversionBenchmarks*" "*StringHelperBenchmarks*" --artifacts "docs/perf/results/enum-string-baseline"
dotnet run --project benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj -c Release --no-restore -- --filter "*EnumConversionBenchmarks*" "*StringHelperBenchmarks*" --artifacts "docs/perf/results/enum-string-direct"
```
