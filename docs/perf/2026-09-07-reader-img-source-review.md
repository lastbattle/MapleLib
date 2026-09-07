# Reader, tree, and IMG source review

This source-only review records function-level dispositions for the assigned WZ reader,
tree/search, properties, and IMG data-source/cache scope. Serialization and MS crypto
are covered by separate workstreams.

## Core WZ files reviewed

- `WzDirectory.cs`: parse/save traversal, offsets, lookup indexes, mutation, cloning,
  counting, and disposal reviewed. Name lookup is already benchmarked and indexed;
  parse/save needs an end-to-end corpus profile before further changes.
- `WzFile.cs`: parse/version probing, save/export, wildcard/regex traversal, object/path
  enumeration, cached path lookup, matching, and disposal reviewed. Wildcard and regex
  traversal now reuse a pooled candidate-path builder; measured results and compatibility
  coverage are in `2026-09-07-wz-search-residual.md`. The path cache currently mixes
  `checkFirstDirectoryName` modes and mutation lifetimes; treat that as a correctness
  prerequisite before optimizing it.
- `WzImage.cs`: construction/disposal, cloning, path lookup, mutation, checksum, parse,
  unparse, save, and export reviewed. `GetFromPath` performs ordinal segment scans and
  remains a measurable wide/deep-tree candidate.
- `WzImageProperty.cs`: property read/write, recursive parse, validation, extended-type
  dispatch, and linked-property traversal reviewed. Parsing is fixture-driven and no
  safe unmeasured local rewrite was identified.
- `WzPropertyCollection.cs`: all lookup/index maintenance and concrete/generic mutation
  surfaces reviewed; accepted indexed implementation is at its prior measured plateau.
- `WzLinkResolver.cs`: image/property recursion, path normalization, inlink/outlink
  resolution, canvas copying, and diagnostics reviewed. Several helpers repeatedly scan
  trees, but require a link-heavy real fixture before redesign.
- `WzObject.cs`, `WzListFile.cs`, `WzImageResource.cs`, `WzHeader.cs`,
  `WzMainDirectory.cs`, `IPropertyContainer.cs`, `WzExtended.cs`, enum/status/constants
  files: all members reviewed; no standalone benchmark candidate outranks traversal and
  IMG cache work.

## WZ utility files reviewed

- `WzBinaryReader.cs`: all string, compressed-number, offset, section, availability,
  and disposal methods reviewed. Bulk WZ strings are already optimized. The remaining
  bounded candidate is `ReadNullTerminatedString`, whose scalar `ReadByte` loop may be
  replaced only with exact stream-position, EOF, and maximum-length coverage.
- `PartialStream.cs`: all sync/async read/write, seek, bounds, APM, byte, and disposal
  methods reviewed; no change proposed beyond the prior plateau.
- `WzBinaryWriter.cs`, `WzKeyGenerator.cs`, `WzMutableKey.cs`, and `WzTool.cs`: all
  members reviewed for reader call-site impact; owned by the binary/crypto workstream.

## IMG files reviewed

- `LRUCache.cs`: all operations were source-reviewed and correctness-covered where
  applicable. The benchmark in `2026-09-07-lru.md` covers `TryGet` hits and misses,
  contended hits, and the read-only `ContainsKey`/`Count` workload.
- `ImgFileSystemManager.cs`: initialization/indexing, image load/save/delete, path
  containment, cache/statistics, preload, index generation/mutation, watcher handling,
  size estimation, and disposal reviewed. Cached key construction now uses one allocation
  while retaining current-culture normalization; measurements and culture coverage are
  in `2026-09-07-img-manager-cache-key.md`.
- `ImgFileSystemDataSource.cs`: all IMG, WZ, and hybrid forwarding/enumeration/save/stat
  methods reviewed. Path splitting and category lowercasing are secondary to manager
  cached-hit cost.
- `VirtualWzDirectory.cs`: lazy population, refresh/removal, overridden lookup/count,
  recursion/path APIs, save, and disposal reviewed. `WzDirectories` allocates a casted
  list per getter, while lookup lowercases and scans linearly; add a width benchmark
  before introducing indexes or changing the exposed mutable-list behavior.
- `LazyWzImageDictionary.cs`: registration, lazy indexer, collection APIs, snapshots,
  removal, and cache clearing reviewed. Loader calls occur under its global monitor;
  a per-key loading design needs explicit factory-once and failure semantics first.
- `CategoryIndex.cs`: recursive build/enumeration, save/load, and stale checks reviewed;
  disk-bound work needs a large-category workload before changing traversal.
- `HaCreatorPaths.cs`, `IDataSource.cs`, and `ImgFileWzImageReference.cs`: all members
  reviewed; preserve containment/backups filtering and lazy-reference semantics.
- `HotSwapConstants.cs` and `VersionInfo.cs`: constants, manifest/category/feature
  members, progress members, and computed totals reviewed. They contain no material
  hot path beyond a small in-memory category sum.
- `BatchConverter.cs`: conversion, installation scan/detection, progress forwarding,
  and result aggregation method bodies reviewed. Material work is delegated to the
  extraction service, and `sources.ToList()` is needed for count and reiteration; no
  local optimization is justified without a multi-version extraction workload.
- `FileSystemWatcherService.cs`: watcher publication/recovery, path metadata, pending
  queues, debounce grouping, event dispatch, and disposal method bodies reviewed.
  This is event- and I/O-bound; benchmark a burst/coalescing workload before changing
  it, preserving latest-change selection, rename metadata, ordering, and disposal races.
- `ImgDirectoryWatcherService.cs`: watcher lifecycle, initial/lazy snapshots, hashing,
  ignore paths/directories, per-file timers, write-completion retries, change detection,
  rename/delete handling, recovery, and disposal method bodies reviewed. Hashing and
  recursive snapshots dominate; any change needs a real directory/burst workload and
  must preserve ignore boundaries, state transitions, and queued-callback behavior.
- `HaCreatorConfig.cs`: recent-path normalization, JSON load/save, directory creation,
  and configuration members reviewed. These are low-frequency filesystem operations;
  no benchmark-worthy change was identified. Preserve fallback-on-load-error and the
  current save/failure behavior if atomic persistence is considered separately.
- `VersionManager.cs`: discovery, manifest creation/load/save, validation/reporting,
  external versions, V Update inference, delete/rename, manager creation, watcher
  generation, hot-swap callbacks, and event publication method bodies reviewed.
  Directory enumeration and manifests dominate. Benchmark a many-version refresh or
  validation workload before changing repeated enumeration, while preserving external
  entries, backup filtering, path containment, state-lock boundaries, and generation
  rules.
- `WzExtractionService.cs`: both extraction entry points, category extraction, file
  discovery/counting, beta Data.wz and Packs/.ms paths, recursive image export, List.wz
  filtering, case maps, manifest/validation, containment, reparse checks, Lua output,
  link handling, progress, cleanup, and result members reviewed. The workflow parses
  inputs once for counting and again for extraction, forces collections between phases,
  and materializes `.ms` files in memory; these are candidates only for a full corpus
  extraction benchmark with output tree and byte-level equivalence.
- `WzPackingService.cs`: category discovery/counting, classic/64-bit/beta packing,
  List.wz, case/reference metadata, directory ordering, IMG collection/grouping,
  bounded parsing, Lua decoding, WZ construction, canvas separation, containment,
  progress, and result members reviewed. Repeated enumeration, whole-file reads,
  sorting/grouping, and parse/clone work need an end-to-end corpus packing benchmark;
  preserve WZ ordering, checksums, encryption/patch settings, case maps, List/Lua
  behavior, canvas outlinks, containment, and reparse protection.
