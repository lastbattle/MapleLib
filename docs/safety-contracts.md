# MapleLib safety and API contracts

The invariants every parser, serializer and tree walker in this library
follows, and the tests that keep them true. Read this before adding a node
type, a serializer leg or a consumer walk.

## The three rules

1. Parse normalizes: anything a writer relies on later (names, collection
   presence, payload envelopes) is materialized or coerced where content is
   read, at the single cheapest boundary.

2. Accessors tolerate: the tree is authored content. Any property type may
   appear under any name, links may be broken or cyclic, and backing readers
   may be closed. Consume trees through the tolerant surface instead of
   casting and dereferencing.

3. Writers assume: serialization may assume the normalized state that parsing
   and the safe accessors produce; it must not re-derive it.

## Tree navigation and payloads

- `WzImageProperty.WzProperties` is null on non-container properties, and the
  child indexer and `GetFromPath` raise `InvalidDataException` for a UOL with
  a broken or cyclic link.

- Navigate with `GetChildOrDefault(name)` and `GetChildrenOrDefault()`: on
  scalar properties and unresolvable links they report no child view instead
  of throwing.

- Read export payload bytes through `GetBytesOrEmpty()`. A lazy payload whose
  backing reader is closed reports as empty, never null; the raw
  `GetBytes(bool)` contract stays nullable by design.

## Lazy payload lifetime

`WzImage` may parse eagerly and close its reader (`freeResources`). Payload
properties treat a null or disposed reader as no payload. Never retain a
reader reference outside the owning property and never read twice after
dispose; the properties' own getters are the single place that owns that
state.

## Exception families

Content errors surface as documented families, pinned by seeded mutation
tests in `MapleLib.Tests/WzMutationContractTests.cs`:

| Boundary | Documented family |
| --- | --- |
| WZ/IMG container and image parse | `InvalidDataException`, `EndOfStreamException`, `IOException`, `NotImplementedException`, `ArgumentException`, `OverflowException` |
| XML import (`WzXmlDeserializer.ParseXML`) | the container family plus `XmlException` and `NoBase64DataException` |

Anything else escaping a public parse entry is a bug; extend the mutation
tests when adding an entry point.

## Numbers, paths and execution

- Parse content numbers culture-invariantly through
  `MapleLib.WzLib.Util.WzParse` (`TryInt`, `TryShort`, ...). The
  ambient-culture fallback in the XML importer for hand edited dumps is the
  documented exception; a comma-decimal host locale must never corrupt
  values.

- Compose export paths only through `WzTool.EscapeInvalidFilePathNames` and
  the empty-path-tolerant directory helpers. Node names are content.

- Extraction, packing and serialization reject path traversal
  (`WzExtractionPackingAdversarialTests`).

- Content-driven construction stays inside reviewed surfaces: HaCreator's
  `SerializationManager` validates the type name, the `ISerializable`
  contract and the constructor template before invoking anything. Process
  launches stay user-driven. Archive data is data, never a command.

## Nullable annotations

The project builds with `Nullable=annotations`; files opt into warnings with
a file-top `#nullable enable`, and new `WzLib` files start enabled.
`WzLib/WzImageProperty.cs` is done, which also required annotating
`WzObject.WzFileParent` as nullable because detached nodes have no file.
Remaining baseline, measured 2026-11-09 as warnings under a full enable:

| File | Warnings |
| --- | --- |
| WzLib/WzStructure/Data/MobStructure/MobData.cs | 172 |
| Img/WzPackingService.cs | 112 |
| WzLib/WzFile.cs | 100 |
| Img/WzExtractionService.cs | 92 |
| WzLib/WzLinkResolver.cs | 88 |
| WzLib/WzDirectory.cs | 80 |

Enable a file in the same change that touches it.

## Verification

    dotnet test MapleLib/MapleLib.Tests/MapleLib.Tests.csproj
    dotnet test MapleLib/MapleLib.Tests/MapleLib.Tests.csproj --filter FullyQualifiedName~WzMutationContract
