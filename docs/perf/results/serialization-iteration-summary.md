# Serialization raw iteration summary

Concurrent Workstation GC. BenchmarkDotNet 0.15.6 used one launch, three warmups,
and five measured iterations. Dataset was the 676,966-byte plain/BMS
`String/Skill.img` from the GMS v95 export corpus.

| Iteration | Method | Mean | Min | Max | Allocated |
| --- | --- | ---: | ---: | ---: | ---: |
| Controlled baseline | ClassicXml | 2.974 ms | 2.953 ms | 2.987 ms | 11.67 MB |
| Controlled baseline | CombinedXml | 3.082 ms | 3.079 ms | 3.084 ms | 11.66 MB |
| Controlled baseline | Json | 5.548 ms | 5.443 ms | 5.727 ms | 5.13 MB |
| Controlled final | ClassicXml | 2.194 ms | 1.588 ms | 3.110 ms | 3.81 MB |
| Controlled final | CombinedXml | 1.944 ms | 1.711 ms | 2.380 ms | 3.81 MB |
| Controlled final | Json | 5.500 ms | 5.439 ms | 5.592 ms | 5.13 MB |
| Keeper confirmation 1 | ClassicXml | 1.659 ms | 1.630 ms | 1.673 ms | 3.81 MB |
| Keeper confirmation 2 | ClassicXml | 1.573 ms | 1.525 ms | 1.657 ms | 3.81 MB |
| Keeper confirmation 3 | ClassicXml | 1.588 ms | 1.567 ms | 1.618 ms | 3.81 MB |

Baseline hashes, preserved by iteration 1:

- Classic XML, 1,101,804 bytes:
  `cf092b4790d824fd3d5f58903a1034fb55389b4b9b956167a897148cc4bd767b`
- Combined XML, 1,101,821 bytes:
  `6c731e8cc8f4694672c9052f5c01dc716fb504d53087ebf4a169748754cd20d7`
- JSON, 2,082,689 bytes:
  `0092d90ea80139a972e08ea56b0e31547ee2380c46a79f3b114af678fec2f3f0`

An earlier probe was rejected for timing comparison because its unchanged JSON path
sped up 2.46x between processes. The controlled baseline/final pair above kept JSON
within 0.9%. The final XML process still had transient noise, so three isolated keeper
confirmations are included. Compact CSV/Markdown artifacts are retained under
`docs/perf/results/raw/serialization-*`. The original complete run directories are
archived from the outer repository under
`BenchmarkDotNet.Artifacts/maplelib-audit-2026-09-07-raw/serialization-*`.
