# WZ pixel encoder iteration summary

Corpus: `<GMS-v95-corpus>`, canvas
`back/4` (500x467), tiled to each benchmark size. Source SHA-256:
`944cbda97da5f490574e08a7eba597f4547e1eabbc44257ac4a43db5019d6473`.

## Baseline

| Method | Size | Mean | Allocated | Output SHA-256 |
| --- | ---: | ---: | ---: | --- |
| BGRA4444 | 128 | 129.974 us | 32.07 KB | `9babdd858124ced69aa3e59d618aff7f065abadcc7c7d7a84b6401a8fb1b3003` |
| DXT3 | 128 | 203.586 us | 16.19 KB | `6c77ea4f2cbc8ce42400af72e3bf9951f288cfdf62df08e4d756d2ec85c7a509` |
| DXT5 | 128 | 262.847 us | 16.19 KB | `c9c5def300df36314ebb1096cf796d557e5fb199b449e5828b638a18893ae7c4` |
| BGRA4444 | 1024 | 7.455 ms | 2048.28 KB | `3f2d01cfa70a01a5ae9bd3c8d5a236a81baf651a720147d9c817c1c16bae4b08` |
| DXT3 | 1024 | 14.589 ms | 1024.40 KB | `c59c0ba1c32ddaac0ee0f24a384cb6b3566fb10be1f599e9207b254a8f3fbaa5` |
| DXT5 | 1024 | 18.623 ms | 1024.39 KB | `d18b20f3de31491777b96fae4cbbeb35ff0951d9dccdb5144069eb9d24802595` |

One launch, three warmups, and five measured iterations. Raw artifacts: `docs/perf/results/raw/pixel-baseline-amoria`.

## Iteration 1: cached and unrolled color distances (kept)

`ComputeColorIndices` now reads the four palette entries' RGB channels once per
block and evaluates their squared distances in fixed order. Strict `<` comparisons
retain the lowest-index tie behavior.

| Method | Size | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: | ---: |
| DXT3 | 128 | 203.586 us | 125.821 us | -38.2% |
| DXT5 | 128 | 262.847 us | 196.587 us | -25.2% |
| DXT3 | 1024 | 14.589 ms | 9.740 ms | -33.2% |
| DXT5 | 1024 | 18.623 ms | 13.341 ms | -28.4% |

All live-corpus and deterministic edge-case hashes matched the baseline. Raw
artifacts: `docs/perf/results/raw/pixel-scalar-palette-amoria`.

## Iteration 2: cached and unrolled alpha distances (kept)

`CompressBlockAlphaDXT5` now reads the eight-entry alpha table once per block and
evaluates the absolute differences in fixed order. Strict `<` comparisons retain
the lowest-index tie behavior.

| Size | Iteration 1 | Iteration 2 | Additional change | Change from baseline |
| ---: | ---: | ---: | ---: | ---: |
| 128 | 196.587 us | 163.811 us | -16.7% | -37.7% |
| 1024 | 13.341 ms | 12.231 ms | -8.3% | -34.3% |

All golden hashes matched. Raw artifacts:
`docs/perf/results/raw/pixel-scalar-alpha-amoria`.

## Iteration 3: SSE4.1 color distances with scalar fallback (kept)

The four palette distances are computed in four 32-bit SIMD lanes when SSE4.1 is
available. Scalar ordered comparisons select the minimum, preserving lowest-index
ties. Unsupported machines use the retained iteration-1 scalar implementation.

| Size | Scalar color path | SSE4.1 color path | Additional change |
| ---: | ---: | ---: | ---: |
| 128 | 125.821 us | 92.034 us | -26.9% |
| 1024 | 9.740 ms | 7.968 ms | -18.2% |

With `DOTNET_EnableHWIntrinsic=0`, the same candidate measured 121.946 us and
9.421 ms; the pre-dispatch scalar run was 122.522 us and 9.533 ms. This confirms
that the fallback preserves the scalar plateau. All golden hashes matched on both
paths. Raw artifacts: `docs/perf/results/raw/pixel-sse41-palette-amoria`,
`docs/perf/results/raw/pixel-scalar-fallback-amoria`, and
`docs/perf/results/raw/pixel-sse41-fallback-amoria`.

## Final confirmation

| Method | Size | Original baseline | Final | Overall change |
| --- | ---: | ---: | ---: | ---: |
| DXT3 | 128 | 203.586 us | 92.421 us | -54.6% |
| DXT5 | 128 | 262.847 us | 131.062 us | -50.1% |
| DXT3 | 1024 | 14.589 ms | 8.060 ms | -44.8% |
| DXT5 | 1024 | 18.623 ms | 10.472 ms | -43.8% |

Final allocations remain output-dominated at 16.62 KB and 1024.83 KB. Two
independent seeded-oracle tests compare the production helpers with the original
ordered scalar algorithms across 2,048 color blocks and 2,048 alpha blocks,
including forced palette ties and equal alpha endpoints. Raw artifacts:
`docs/perf/results/raw/pixel-final-amoria`.
