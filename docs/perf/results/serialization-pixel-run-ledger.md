# Serialization and pixel benchmark run ledger


All BenchmarkDotNet commands used Release builds of
`MapleLib/benchmarks/MapleCrypto.Benchmarks/MapleCrypto.Benchmarks.csproj` with
one launch, three warmups, and five measured iterations.

Compact CSV/Markdown results are stored under `docs/perf/results/raw`. The original
complete run directories are archived in the outer repository under
`BenchmarkDotNet.Artifacts/maplelib-audit-2026-09-07-raw`.

## Serialization

Corpus: `<GMS-v95-corpus>`.

The initial baseline and iteration-1 runs were rejected because unchanged JSON
timing shifted 2.46x between processes. The valid comparison temporarily restored
the original `XmlUtil` for `serialization-controlled-baseline-skill`, immediately
restored the keeper for `serialization-controlled-final-skill`, and then recorded
two isolated keeper confirmations in `serialization-plateau1-skill` and
`serialization-plateau2-skill`. The shared invocation shape was:

`$env:WZ_SERIALIZATION_IMG='<corpus>'; dotnet run --project <benchmark-project> -c Release --no-build -- --filter '*WzCorpusSerializationBenchmarks*' --artifacts <artifact-directory>`

## Pixel encoding

Corpus: `<GMS-v95-corpus>`, canvas `back/4`.
The shared invocation shape was:

`$env:WZ_PIXEL_IMG='<corpus>'; $env:WZ_PIXEL_CANVAS_PATH='back/4'; dotnet run --project <benchmark-project> -c Release --no-build -- --filter <filter> --artifacts <artifact-directory>`

Runs, in order:

1. `pixel-baseline-amoria`, filter `*WzPixelEncodeBenchmarks*`.
2. `pixel-scalar-palette-amoria`, filter `*WzPixelEncodeBenchmarks.Dxt*`.
3. `pixel-scalar-alpha-amoria`, filter `*WzPixelEncodeBenchmarks.Dxt5*`.
4. `pixel-scalar-fallback-amoria`, DXT3 filter with `DOTNET_EnableHWIntrinsic=0`.
5. `pixel-sse41-palette-amoria`, DXT3 filter with intrinsics enabled.
6. `pixel-sse41-fallback-amoria`, DXT3 filter with `DOTNET_EnableHWIntrinsic=0`.
7. `pixel-final-amoria`, DXT3 and DXT5 filter with intrinsics enabled.

The corpus SHA-256 is
`944cbda97da5f490574e08a7eba597f4547e1eabbc44257ac4a43db5019d6473`.
Final 1024x1024 hashes are DXT3
`c59c0ba1c32ddaac0ee0f24a384cb6b3566fb10be1f599e9207b254a8f3fbaa5`
and DXT5
`d18b20f3de31491777b96fae4cbbeb35ff0951d9dccdb5144069eb9d24802595`.
The benchmark harness contains the complete 128/1024 live hashes and twelve edge
hashes and fails setup if any output changes.
