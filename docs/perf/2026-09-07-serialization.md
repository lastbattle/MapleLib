# Serialization and crypto optimization report

## Scope and compatibility contract

This workstream reviews `MapleLib/WzLib/Serializer`, `MapleLib/Serialization`,
`MapleLib/MapleCryptoLib`, and the crypto-related implementation under
`MapleLib/WzLib/MSFile`. Output XML, JSON, BSON, IMG, NX, and MS bytes must retain
their established format and ordering. Parsed object trees, progress counts,
resource ownership, exception behavior, packet ciphertext, IV/header semantics,
and MS v2/v4 decryption must remain equivalent.

Primary metrics are mean latency and allocated bytes per operation. Improvements
below 3% or within observed variance are treated as a plateau. The live serializer
corpus is `<GMS-v95-corpus>`; initial fixtures are the plain/BMS
`String/Skill.img` (676,966 bytes), `String/Map.img` (291,956 bytes), and
`String/Eqp.img` (442,255 bytes). They contain approximately 14,595, 15,212, and
17,321 recursive properties respectively.

The repository-wide baseline was supplied at submodule commit
`4970d339b2ab2bc00ffc33edfbf570e87053b822`. Existing packet crypto, ChaCha20,
SNOW2, WZ binary I/O, synthetic serializer, and WZ pipeline measurements are
retained from the historical reports rather than rerun without evidence.

## Iteration record

### Iteration 0 - initial live String/Skill serializer probe

- Timestamp: 2026-09-07.
- Code state: benchmark/correctness harnesses only; no production serializer edit.
- Dataset: `<GMS-v95-corpus>`, plain/BMS IMG,
  676,966 bytes and approximately 14,595 recursive properties.
- Correctness: `dotnet test MapleLib.Tests\MapleLib.Tests.csproj -c Release
  --filter FullyQualifiedName~XmlUtilTests` passed 7/7. The Release benchmark
  project build passed with zero warnings and errors.
- Benchmark: `dotnet run -c Release --no-build --project
  benchmarks\MapleCrypto.Benchmarks\MapleCrypto.Benchmarks.csproj -- --filter
  '*WzCorpusSerializationBenchmarks*' --artifacts
  '..\docs\perf\results\serialization-baseline-skill'` with three warmups and
  five measured iterations in one launch.

| Serializer | Mean | Min | Max | Allocated | Output bytes | SHA-256 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Classic XML | 8.472 ms | 7.529 ms | 9.074 ms | 11.67 MB | 1,101,804 | `cf092b4790d824fd3d5f58903a1034fb55389b4b9b956167a897148cc4bd767b` |
| Combined XML | 5.810 ms | 3.273 ms | 7.678 ms | 11.66 MB | 1,101,821 | `6c731e8cc8f4694672c9052f5c01dc716fb504d53087ebf4a169748754cd20d7` |
| JSON | 11.470 ms | 10.727 ms | 12.031 ms | 5.13 MB | 2,082,689 | `0092d90ea80139a972e08ea56b0e31547ee2380c46a79f3b114af678fec2f3f0` |

Combined XML had one removed 12.82 ms outlier and high variance, so later comparisons
must use allocations, hashes, and repeated confirmation rather than that mean alone.
The unchanged JSON path later ran 2.46x faster, proving this process was externally
contaminated. Decision: retain output/allocation data but supersede its latency with
the controlled baseline below.

### Iteration 0b - controlled live baseline

The original `XmlUtil` implementation was restored for a fresh three-method process,
immediately followed by the keeper process under the same exclusive CPU lease.

| Serializer | Mean | Min | Max | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Classic XML | 2.974 ms | 2.953 ms | 2.987 ms | 11.67 MB |
| Combined XML | 3.082 ms | 3.079 ms | 3.084 ms | 11.66 MB |
| JSON control | 5.548 ms | 5.443 ms | 5.727 ms | 5.13 MB |

The JSON control's 2.1% standard deviation and the XML standard deviations of 0.5%
or less make this the accepted timing baseline. Raw artifacts are under
`docs/perf/results/raw/serialization-controlled-baseline-skill`.

### Iteration 1 - accelerated XML escape fast path

- Change: `XmlUtil.SanitizeText` uses `SearchValues<char>`/`IndexOfAny` to detect
  the five XML attribute escapes. Strings without an escape return directly; the
  escaping path copies the prefix once and uses a character switch. The runtime can
  accelerate the search on supported CPUs and supplies the portable fallback.
- Correctness: focused escaping tests passed 7/7 after the change. All three live
  output lengths and SHA-256 hashes exactly match iteration 0.
- Benchmark: same Skill IMG and BDN configuration. Compact CSV/Markdown artifacts
  are at `docs/perf/results/raw/serialization-iteration1-skill`.

| Serializer | Controlled baseline | Controlled final | Delta | Allocation baseline/final |
| --- | ---: | ---: | ---: | ---: |
| Classic XML | 2.974 ms | 2.194 ms | 1.36x faster | 11.67 MB / 3.81 MB |
| Combined XML | 3.082 ms | 1.944 ms | 1.59x faster | 11.66 MB / 3.81 MB |
| JSON control | 5.548 ms | 5.500 ms | 0.9%, within noise | 5.13 MB / 5.13 MB |

The controlled final XML process was noisy (Classic 29.6% and combined 14.0% standard
deviation), but the unchanged JSON control matched the clean baseline within 0.9%.
The paired mean improves Classic XML by 26.2% and combined XML by 36.9%; their medians
improve by 37.0% and 40.9%. Three isolated keeper runs measured Classic XML at 1.659,
1.573, and 1.588 ms, all with 3.81 MB allocation. The 67.3% allocation reduction is
stable. Decision: keep and stop this XML pass rather than mixing a writer rewrite
into the proven escaping change.

Final correctness: all 366 `MapleLib.Tests` tests passed in Release. The seven focused
escape tests also passed with `DOTNET_EnableHWIntrinsic=0`, verifying the runtime's
portable fallback. Final live output lengths and hashes exactly match iteration 0.

## Review disposition

| File / function | Disposition and measurement plan |
| --- | --- |
| `ProgressingWzSerializer.CreateDirSafe` | Cold filesystem collision loop; retain. Path substring cleanup is minor beside filesystem I/O. |
| `ProgressingWzSerializer.EscapeInvalidFilePathNames` | Retain pending a filename-heavy export profile. Regex and `Split` allocate, but this runs once per exported file rather than once per property. |
| `WzClassicXmlSerializer.exportXmlInternal`, `SerializeImage` | Benchmark on each live String IMG. The `ToList` snapshot and per-property XML writer are allocation candidates; preserve concurrent-enumeration behavior and exact bytes. |
| `WzClassicXmlSerializer.exportDirXmlInternal`, `SerializeDirectory`, `SerializeFile` | Retain until a multi-image directory export is measured; dominated by image serialization and filesystem work. |
| `WzFileExporter.RunWzFilesExtraction`, `RunWzImgDirsExtraction`, `RunWzXmlExtraction` | Orchestration and logging only; retain unless an end-to-end export profile assigns material cost here. |
| `WzImgDeserializer.WzImageFromIMGBytes`, `WzImageFromIMGFile` | Use the file method to prepare the live benchmark outside timed operations. Checksum skipping is already configurable; no speculative edit. |
| `WzImgSerializer.GetOutputIv`, `CreateForImgExtraction` | Constant-time/cold; retain. |
| `WzImgSerializer.SerializeImage` overloads | Existing WZ save pipeline measurements cover the core work. The byte-returning overload necessarily materializes output; retain pending a caller-driven stream API case. |
| `WzImgSerializer.SerializeDirectory`, `SerializeFile` | Retain until directory export is profiled. |
| `WzJsonBsonSerializer.ExportInternal`, `ExportInternalAsync` | Live benchmark target. Dictionary-tree construction dominates allocation and is a possible later streaming rewrite; first compare it with XML using exact output hashes. Ensure `UnparseImage` remains exception-safe if changed. |
| `WzJsonBsonSerializer.exportDirInternal`, public serialize methods | Thin traversal/extension wrappers; retain pending directory workload. |
| `WzNewXmlSerializer.DumpImageToXML`, `DumpDirectoryToXML`, `ExportCombinedXml` | Live benchmark target. Repeated depth concatenation and XML temporary strings are candidates; preserve combined document structure and current base64 policy. |
| `WzPngMp3Serializer.SerializeObject`, serialize wrappers, `CalculateTotal`, `ExportRecursion` | Media I/O traversal; require canvas/audio fixtures before changes. Do not mix codec cost into the first text serializer iteration. |
| `WzSerializer.WritePropertyToXML` | Primary candidate. Measure escape fast path first, then direct `TextWriter` writes to remove `object[]`, boxing, and concatenated tag strings. Require byte-identical SHA-256 outputs. |
| `WzSerializer.WritePropertyToJsonBson` | Live JSON target. Dictionary-per-property is expensive but a streaming rewrite affects public output structure; consider only after XML plateau and profile evidence. |
| Serializer interfaces, `LineBreak`, `NoBase64DataException` | API declarations/no hot implementation; retain. |
| `WzToNxSerializer.Extension.EnsureMultiple`, `SubArray` | Small allocation candidates only inside NX export; defer until an NX corpus and output verifier exist. |
| `WzToNxSerializer.SerializeFile`, `WriteNodeLevel`, `WriteNode`, `GetChildObjects`, `GetChildCount` | Likely traversal/allocation opportunity, especially breadth-level list rebuilding. Defer until a valid NX fixture and byte-layout checksum benchmark are added. |
| `WzToNxSerializer.GetCompressedBitmap`, `WriteBitmap`, `WriteMP3`, `WriteString` | Codec/data dominated. `WriteString` double-encodes and uses LINQ, but requires a string-heavy NX benchmark before editing. |
| `WzToNxSerializer.WriteUOL`; `DumpState.AddCanvas`, `AddMP3`, `AddString`, `AddNode`, `GetNodeID`, `GetNextNodeID`, `AddUOL` | Mostly dictionary/list bookkeeping. Retain until NX end-to-end profiling identifies a material share. |
| `WzXmlDeserializer.ParseXML`, `CountImgs`, `ParseXMLWzDir`, `ParseXMLWzImg`, `ParsePropertyFromXMLElement` | DOM parsing is allocation-heavy by design. Require a representative exported XML corpus and round-trip tree comparison before any streaming rewrite. |
| `MapleJsonObjectConverter.Read`, `Write`, `WriteValue` | Type-switch is allocation-light except `char.ToString`; retain unless JSON profiling identifies it. Recursive dictionary/enumerable behavior is compatibility-sensitive. |
| `MapleAESEncryption.AesCrypt` overloads | Retain prior measured keeper: reusable transform/in-place feedback shape. Existing report shows plateau and fixed ciphertext vectors. |
| `MapleCrypto.UpdateIV`, `Crypt`, `GetNewIV`, `Shuffle`, header methods, packet-length methods, packet check, multiplication methods | Retain prior measured keepers and rejected-probe conclusions. SIMD-labelled multiplication delegates to the measured correct bulk-copy implementation; no churn without a new packet workload. |
| `MapleCrypto.ValidateAndCloneIV`, `ValidatePacketSize` | Validation/cold; retain. |
| `MapleCryptoConstants.IsDefaultMapleStoryUserKey`, `GetTrimmedUserKey`, `GetTrimmedWzUserKey` | Retain cached mutable-key behavior and prior benchmarks. |
| `MapleCustomEncryption.Encrypt`, `Decrypt`, rotations, lookup-table creation | Retain prior 7x–2,000x measured gains and plateau. Output vectors already cover equivalence. |
| `ChaCha20CryptoTransform.TransformBlock`, `TransformFinalBlock`, `TransformInPlace`, `GenerateKeyBlock`, `XorKeyStream`, `QuarterRound`, disposal | Retain prior 5.4x keeper and rejected direct-block/state-cache probes. SIMD/intrinsics require a new relevant workload beating the current `ulong` XOR path with identical RFC/MS output and scalar fallback. |
| `Snow2CryptoTransform` transform methods, key setup, keystream refresh, finite-field helpers, disposal | Retain historical MSFile/SNOW2 plateau. Revisit only from an end-to-end MS workload with exact decrypt checks. |
| `WzMsEntry.RecalculateFields` | Small metadata calculation; retain unless MS save profiling makes it material. |
| `WzMsFile` key derivation and validation helpers | Fixed small loops and safety checks; retain. Avoid SIMD overhead on 16/32-byte keys. |
| `WzMsFile.ReadHeader*`, `ReadEntries*`, `ChaCha20Reader` methods | Prior v2/v4 fixture benchmarks and correctness coverage exist. Retain until a larger complete pack shows a new bottleneck. |
| `WzMsFile.AlignToPage`, `SumChars`, `SumUInt16Bytes`, `ReadCharsExactly` | Simple helpers; retain. SIMD is unjustified for typical salt/name lengths. |
| `WzMsFile.LoadAsWzFile`, `CreateHeader`, `GenerateSalt`, `Save` | Potential whole-file allocation/I/O opportunities, including buffered encrypted data and zero-padding arrays, but high format risk. Require save/round-trip benchmark fixtures before edits. |
| `WzMsFile.DeriveImgKey`, `DeriveChaCha20ImgKey`, `ValidateEntryKeyInputs` | Tiny fixed-length derivation; retain. |
| `WzMsFile.DecryptData*`, `EncryptData` | Full-entry buffers are currently part of the return/parse contract. Retain prior cipher results; consider streaming only if an MS pack benchmark shows peak-memory pressure. |
| `WzMsFile.Close`, `Dispose`; `WzMsHeader.UpdateHeader` | Cold lifetime/metadata methods; retain. |

## Remaining measured targets

1. Run correctness/hash probes on the other two String IMG fixtures when a broader
   serializer corpus comparison is needed; no performance claim is made for them here.
2. Measure the prepared real-canvas pixel harness before changing DXT encoders.
3. Consider a direct `TextWriter` rewrite only in a separate measured iteration; the
   escape keeper already reaches a stable allocation/speed improvement.

The harness reads a fixture selected through `WZ_SERIALIZATION_IMG`. Setting
`WZ_SERIALIZATION_KEEP_OUTPUTS=1` retains the generated files for hashing and exact
before/after comparison.

## Supplemental pixel-code review

This is a source disposition only; the serializer workstream has not run or changed
pixel code. Any future accelerated implementation must retain byte-exact decoded or
encoded output and a tested scalar fallback.

| File / function | Disposition and measurement plan |
| --- | --- |
| `PngUtility.BitmapToByteArray`, format dispatch overloads | Wrapper/codec dispatch; retain. Measure concrete formats rather than the switch. |
| `BuildBgra4444ExpandedBytes`, quantize/pack helpers | Already table-based, inlined, and shared by scalar/SIMD tails; retain unless codegen evidence accompanies a benchmark win. |
| `DecompressImageBC7` overloads | Thin validation and destination dispatch. Benchmark the decoder core. |
| `RGB565ToColor`, color/alpha/index expansion helpers | Fixed 4x4-block helpers. Possible representation improvements belong to a whole DXT benchmark, not isolated nanosecond tuning. |
| `DecompressImage_PixelDataBgra4444` | Existing SSE2 contiguous-stride path plus scalar strided fallback. Retain until a format-specific benchmark proves another ISA materially helps. |
| `DecompressImageDXT3`, `DecompressImageDXT5` | Existing parallel block-row traversal and SSE2/SSSE3 paths. Benchmark thresholds and small/large images; avoid adding parallel or SIMD complexity without exact-output gains. |
| `DecompressImage_PixelDataForm517` | Existing AVX2 16-pixel fill plus scalar partial-block fallback. Retain. |
| `CopyBmpDataWithStride`, `SetPixel` | Bulk `Marshal.Copy` path and cold helper; retain. |
| `IsGrayscaleBitmap` | Sampling heuristic affects selected output format, so behavior is compatibility-sensitive. Any vector scan must preserve the exact sampling positions and tolerance. |
| `GetPixelDataFormat1`, `GetPixelDataFormat257`, `GetPixelDataFormat513` | Existing SSE4.1 four-pixel encoders with scalar fallback. Candidate only for wider-vector benchmarking on large images; compare startup/tail costs and byte hashes. |
| `GetPixelDataFormat2` | Already bulk-copies contiguous bitmap storage; retain. |
| `GetPixelDataFormat517` | One sampled pixel per 16x16 block; memory access dominates and SIMD is not promising. Retain. |
| `CompressDXT3`, `GetPixelDataFormat2050`, block color/alpha/index helpers | Strongest future compute candidates: scalar per-block extraction, min/max, and nearest-codebook searches. Build format-specific benchmarks with decoded-image equivalence or exact encoded hashes before changing; a data-layout rewrite may outperform scattered intrinsics. |
| Dimension and bitmap validation helpers | Safety/cold paths; retain. |
| `Bc7Decoder.DecodeToBgra32` overloads | Output traversal and partial-edge copying. Benchmark array and strided destinations separately with full output hashes. |
| `Bc7Decoder.DecodeBlock` | Branch-heavy bit parsing, partition lookup, and endpoint interpolation. A mode-specialized or batched-block rewrite may be measurable; per-block SIMD is not assumed beneficial. Preserve all BC7 modes and invalid-mode clear behavior. |
| `Bc7Decoder.ExpandEndpoint`, `Interpolate`, `GetPartitionSet`, `BitStream.Read` | Tiny hot helpers likely inlining candidates only as part of a measured decoder rewrite. Validate against a diverse BC7 block corpus, not a single image. |

## WZ property and Spine source disposition

This is an exhaustive source disposition for the files listed here; no production
change is authorized in this part of the review.

| File | Disposition and measurement plan |
| --- | --- |
| `WzBinaryProperty.cs` | Lazy payload reading already locks/restores the shared reader. Header parsing uses several arrays/LINQ concatenations and marshalling, but runs once per sound. Require a sound-heavy WZ parse/export profile before changing. Payload arrays are part of the API contract. |
| `WzCanvasProperty.cs` | Property lookup uses the indexed collection. Link resolution allocates path strings and may cross files/load canvas sections; benchmark a link-heavy map before any rewrite. Preserve external-resolver path losslessness. |
| `WzConvexProperty.cs` | Indexed child collection and ordinary clone/write traversal; retain. |
| `WzDoubleProperty.cs`, `WzFloatProperty.cs`, `WzIntProperty.cs`, `WzLongProperty.cs`, `WzShortProperty.cs`, `WzNullProperty.cs`, `WzStringProperty.cs`, `WzVectorProperty.cs` | Scalar value holders with small conversion, clone, write, and XML helpers. Retain; serializer-level text construction is the measured target. |
| `WzListEntry.cs` | Trivial model/accessors; retain. |
| `WzLuaProperty.cs` | `EncodeDecode` is a byte XOR loop and creates the required output array. Consider BCL vectorized XOR only with large real Lua payloads, exact encrypted bytes, UTF-8 round trips, mutable-key behavior, and scalar fallback; no evidence yet. |
| `WzPngFormat.cs`, `WzPngFormatExtensions.cs` | Enum mappings and checked size validation; retain. |
| `WzPngProperty.cs` | Compression/decompression and lazy cache ownership dominate. Existing validation/locking is compatibility-sensitive. Potential later targets are list-WZ block XOR and duplicate materialization in extraction, measured on real encrypted canvases with compressed/raw/pixel hashes. Format dispatch itself is cold. |
| `WzRawDataProperty.cs`, `WzVideoProperty.cs` | Lazy payload reads lock/restore shared readers; clone/replace APIs intentionally copy arrays. Retain unless raw/video-heavy export shows peak-memory pressure. |
| `WzSubProperty.cs` | Indexed ordered child collection; retain current measured collection keeper. |
| `WzUOLProperty.cs` | Resolution walks slash-separated paths and cycle-checks under `UOLRES`. Retain until a UOL-heavy traversal benchmark measures resolution; preserve caching/cycle semantics. |
| `WzSpineAnimationItem.cs` | One-time renderer resource setup; retain. |
| `WzSpineAtlasLoader.cs` | One-time atlas/skeleton parsing dominates. Small LINQ child searches are not a credible isolated target; retain pending a Spine load profile. |
| `WzSpineObject.cs` | Data holder only; retain. |
| `WzSpineTextureLoader.cs` | GPU texture creation/upload and PNG decode dominate. Retain; any improvement belongs to measured PNG decode/upload work. |

`WzPixelEncodeBenchmarks` is prepared for the later pixel phase. It loads the first
canvas from a real plain IMG selected by `WZ_PIXEL_IMG`, tiles those pixels into
128×128 and 1024×1024 fixtures, and measures BGRA4444, DXT3, and DXT5 encoding.
Setup prints encoded length and SHA-256 for each output. The intended first DXT
candidate is a packed-channel scalar layout that avoids repeated `Color` component
extraction in `ComputeColorIndices`; explicit vector comparison follows only if that
layout leaves a material compute bottleneck. DXT5 alpha-index search is a separate
coherent iteration.

The selected live pixel corpus is
`<GMS-v95-corpus>` (218,377 bytes), with
`WZ_PIXEL_CANVAS_PATH=back/4` selecting a 500×467 canvas. The harness records the
source IMG SHA-256, resolved canvas path, and dimensions during setup.

### Pixel encoder baseline

The baseline used one launch, three warmups, and five measured iterations. Raw BenchmarkDotNet artifacts
are under `docs/perf/results/raw/pixel-baseline-amoria`.

| Method | 128x128 mean | 1024x1024 mean | 1024x1024 allocation |
| --- | ---: | ---: | ---: |
| BGRA4444 | 129.974 us | 7.455 ms | 2048.28 KB |
| DXT3 | 203.586 us | 14.589 ms | 1024.40 KB |
| DXT5 | 262.847 us | 18.623 ms | 1024.39 KB |

The IMG SHA-256 is
`944cbda97da5f490574e08a7eba597f4547e1eabbc44257ac4a43db5019d6473`.
The 1024x1024 encoded-output hashes are BGRA4444
`3f2d01cfa70a01a5ae9bd3c8d5a236a81baf651a720147d9c817c1c16bae4b08`,
DXT3 `c59c0ba1c32ddaac0ee0f24a384cb6b3566fb10be1f599e9207b254a8f3fbaa5`,
and DXT5 `d18b20f3de31491777b96fae4cbbeb35ff0951d9dccdb5144069eb9d24802595`.
The harness also records four deterministic edge corpora covering a single color,
equal alpha endpoints, nearest-color ties, gradients, and pseudo-random pixels.

The first scalar candidate caches the four color-table entries' RGB channels once
per block and unrolls their nearest-color comparisons while retaining strict `<`
updates and therefore the original lowest-index tie choice. It reduced 1024x1024
DXT3 from 14.589 ms to 9.740 ms (33.2%) and DXT5 from 18.623 ms to 13.341 ms
(28.4%). At 128x128, DXT3 fell from 203.586 us to 125.821 us (38.2%) and DXT5
from 262.847 us to 196.587 us (25.2%). All golden live and edge-case hashes
matched. The candidate is retained.

The second scalar candidate applies the same cache-and-fixed-order approach to the
eight-entry DXT5 alpha table. DXT5 at 1024x1024 fell again from 13.341 ms to
12.231 ms (8.3% additional, 34.3% from the original baseline); 128x128 fell from
196.587 us to 163.811 us (16.7% additional, 37.7% overall). All golden hashes
matched, including equal-alpha endpoints, and the candidate is retained.

The final iteration computes the four RGB squared distances in SSE4.1 lanes and
uses the same ordered strict comparisons to choose the index. The scalar keeper is
the fallback. An isolated DXT3 comparison reduced 1024x1024 from 9.740 ms to
7.968 ms (18.2% additional) and 128x128 from 125.821 us to 92.034 us (26.9%).
With all hardware intrinsics disabled, the candidate measured 9.421 ms and
121.946 us, consistent with the pre-dispatch scalar fallback run of 9.533 ms and
122.522 us. Golden hashes matched in every run.

The combined final confirmation measured 8.060 ms for 1024x1024 DXT3 and
10.472 ms for DXT5, overall improvements of 44.8% and 43.8%. At 128x128 the
final results were 92.421 us and 131.062 us, overall improvements of 54.6% and
50.1%. Two seeded oracle tests cover 2,048 color blocks and 2,048 alpha blocks,
including forced ties and equal endpoints; both the SSE4.1 and intrinsics-disabled
paths matched the original fixed-order scalar algorithms.
