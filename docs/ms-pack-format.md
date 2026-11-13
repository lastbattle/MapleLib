# MapleStory `.ms` pack container - format specification

Reference for the `.ms` shard containers used by modern MapleStory clients (reversed and verified on
a current retail build; the container, key schedule and quirks are shared across regions and recent
generations - only data-level values such as the salt differ per build and region). A shard is an
encrypted archive of WZ images ("entries"). Two generations exist: version 2 (Snow2, legacy) and
version 4 (ChaCha20, current). All integers are little-endian; strings are UTF-16LE unless noted.

## 1. Conventions

- The cryptographic identity of a shard is its **file name, lowercased, including the extension**
  (`skill_00000.ms`). Every sum below runs over the UTF-16 code units of that name.
- A random **salt** (printable ASCII 33..126) is embedded in each header, obfuscated by the pad
  bytes (section 3). The MapleLib writer draws 4..12 chars (`SaltMinLength/MaxLength`); retail
  corpora observe 22-char salts, and parsers should accept anything up to the pad length.
- Hard limits: entry count <= 100 000; entry name <= 1 048 576 chars; payload allocations bound by
  `MemoryLimits.MAX_WZ_PAYLOAD_BYTES`.

## 2. Container layout (version 4)

| offset | size | field |
|---|---|---|
| 0 | P | obfuscation pad; `P = sum(name) % 312 + 30` |
| P | 1 | version tag: stored byte `B`, where `B xor sar8(pad[0]) == 4` |
| P+1 | 4 | `hashedSaltLen` i32; `saltLen = lowByte(hashedSaltLen) xor sar8(pad[0])` |
| P+5 | 2*len | salt bytes (see 3.3 for the char transform) |
| P+5+2L | 8 | ChaCha20-encrypted `headerHash u32`, `entryCount i32` (header key, zero nonce, counter 0) |
| +8 | gap | `gap = (sum*3) % 212 + 64` filler bytes |
| | | entry table, encrypted (section 4); then the 1024-aligned data area (section 5) |

There are no stored table/data offsets - every position is derived. `headerHash` is NOT validated on
decode. The count must satisfy `0 <= count <= 100 000` and `count * 56` must fit the bytes remaining
at the table start.

## 3. Entry table (version 4)

Decrypted with the table key as a continuous ChaCha stream. Per record:

| off | size | field |
|---|---|---|
| 0 | 4 | `nameLen` i32 (1..1 048 576; `nameLen*2` must fit the record area) |
| 4 | 2L | name, UTF-16LE, `Category/Name.img` |
| .. | 4 | `checkSum` (see 4.1) |
| .. | 4 | `flags` (0 in the corpus) |
| .. | 4 | `relBlock` - data block index, multiplied by 1024 later |
| .. | 4 | `size` - payload bytes |
| .. | 4 | `sizeAligned` - `size` rounded up to 1024 |
| .. | 4 | `unk1`, `unk2` - reserved, 0 in the corpus |
| .. | 16 | `entryKey` - per-entry payload key material |
| .. | 4 | `unk3`, `unk4` - reserved, 0 in the corpus |

Minimum record: 56 bytes (10 ints + key). The table ends at the last record; the data area begins at
the next 1024 boundary.

4.1 Checksum. The stored `checkSum` mirrors the writer:
`flags + relBlock + size + sizeAligned + unk1 + sum(entryKey bytes)`. Readers can recompute and
compare after decryption.

4.2 Stream framing. Both writer and reader refill 64-byte blocks; after consuming exactly a whole
block the counter state resets (`state[12] = 0`) - replicate this or long tables desynchronize.

## 4. Data area

`dataStart = align1024(first byte past the table)`. Entry data sits at
`dataStart + relBlock * 1024`: `size` payload bytes followed by zero padding to `sizeAligned`. Only
the first `min(size, 1024)` bytes are encrypted (`DoubleEncryptInitialBytes`); the remainder is
stored plaintext.

## 5. Keys

5.1 Identity hash. `saltKeyHash = FNV-1a(salt)` over UTF-16 chars, seed `0x811C9DC5`, multiplier
`0x1000193`. Note: this is NOT the `headerHash` from the encrypted header blob.

5.2 Header and table keys (version 4, 32 bytes each), from `fns = lowerName + salt`:

```text
header key : k[i] = (byte)(fns[i % len] + i)                     then xor obscure[i]
table key  : k[i] = (byte)(i + (i%3+2) * fns[len-1-i%len])        then xor obscure[i]
```

Obfuscation table (`ChaCha20KeyObscure`):

```text
7B 2F 35 48 43 95 02 B9 AE 91 A6 E1 D8 D6 24 B4
33 10 1D 3D C1 BB C6 F4 A5 FE B3 69 6B 56 E4 75
```

5.3 Per-payload key/nonce/counter. Mix `saltKeyHash`, the entry name and the entry key with the same
shape (32 bytes, then xor obscure). Nonce = 12 bytes: `{0, kd[0..4], kd[4..8]}` where `kd` is
`{h, h>>1, (h>>1)^0x6C}` scrambled by, for i in 0..11 with a=0 b=0 c=90 d=0:
`kd[i] ^= (byte)(d + 11*(i/11) + (c^(i>>2)) + (a^b)); --d; a+=8; b+=17; c+=43;`.
Counter = kd[8..12] as u32.

5.4 Salt recovery from any shipped shard: with `r[i] = sar8(pad[i])` and salt byte `sb[2i]`:
`a = r[i] xor sb[2i]; char[i] = ((a | 0x4B) << 1) - a - 75`. One known-good parse chain therefore
recovers the build's salt - the salt is data, not code; a rotation only changes header bytes.

5.5 Version 2 keys: 16 bytes, same shapes, NO obfuscation xor.

## 6. Entry payloads are WZ images

A decrypted payload is a WzImage block for `WzMapleVersion.BMS`:

- `0x73` - standard: WZ-encoded string `Property`, u16 0, then the property list.
- `0x1B` - with-offset variant.
- `0x01` - single Lua property.
- `0x23` - text form: literal `#Property` CRLF, then `name = { ... }` tables and `name = value`
  scalars (integers, floats, quoted or bareword strings; indentation irrelevant). Parse to a normal
  property tree - see MapleLib `WzTextImageParser`. Recipe tables use this form.

The image name is the last path segment of the entry name (`Skill/422.img` -> `422.img`).

## 7. Version 2 (legacy `Snow2`) deltas

Same skeleton; version tag decodes to 2 and the body differs:

```text
pad bytes        : used as-is (no sar); randCount identical (sum % 312 + 30)
saltLen          : lowByte(hashedSaltLen) xor pad[0]          (no sar)
salt char        : pad[i] XOR saltByte[2i]                   (plain xor)
cipher           : Snow2 stream, 16-byte keys, no nonce/counter
header keys      : v4 shapes minus the obscure xor (16 bytes)
header body      : hash i32, version u8 (=2), count i32, WITH integrity check:
                   hash == hashedSaltLen + version + count + sum(u16 salt words)
entry gap        : (sum*3) % 212 + 33                        (v4: +64)
entry record     : nameLen + 7 ints + 16B key (48B min); no unk3/unk4
payload key      : 16B mix, salt-FNV seed, no obscure xor
payload          : Snow2 over the WHOLE payload, then Snow2 again on the first 1024
```

All remaining rules (alignment, `relBlock*1024`, limits) are shared. The current-generation corpus
does not exercise this path - loaders try v2, then v4 - but older clients ship it, and MapleLib's
`Snow2CryptoTransform` implements the cipher.

## 8. Implementation notes

- Identity variants: shards in the wild include byte-copies of a predecessor whose header, keys and
  layout parameters bind to the source's name (occasionally across categories). A robust parser
  tries the own name, then the index-flipped neighbour (`00001 <-> 00000`), then sibling `.ms` names
  from the same folder.
- `Mob/BossPattern/BossFieldSetInfo.img` begins with ASCII `Root <Proper...`; a further serialized
  flavour not covered here.
- Client `NameSpace.dll` hot paths are compiler-obfuscated (`cmovz` dispatch, `imul` boolean
  idioms); constants were recovered from data references and cross-checked against a working
  implementation. The salt is data, not code - a rotation only changes header bytes.

## 9. Client provenance

All pack crypto and layout live in **`NameSpace.dll`**, the client's package/filesystem layer
(`Data/Packs`, `.ms`, `PackageLoader.inl`, `LAPacker`, `Not Suported Package Version Need Update
Package`); the exe only bootstraps `Base/Base.wz`. The ChaCha core appears as four ~16 KB bodies
(`0x1528AC750` family, sigma at `0x152A1FC50`); the FNV pair `0x811C9DC5`/`0x1000193` appears
throughout the package functions (key schedule shared by both versions); the version gate sits in
the 27 KB twins `0x15281FD20`/`0x152827600`. `NameSpace.dll` is a COM in-proc server
(`DllGetClassObject`/`DllCanUnloadNow`) - locate it by strings/constants, not addresses, which
shift per build. Checked and ruled out: the Themida-packed exe, `ResMan`, `Canvas`, `WzMss`,
`PCOM`, `Gr2D_DX11`, `Shape2D`, `Sound`, `ZLZ64` (plain zlib) and the launcher/anticheat
ancillaries.

## 10. MapleLib mapping

- `WzLib/MSFile/WzMsFile.cs` - container, both versions; v4 header parse falls back to the
  index-flipped neighbour and then sibling names (see 8).
- `WzLib/MSFile/WzMsConstants.cs` - every constant in this document.
- `WzLib/MSFile/WzTextImageParser.cs` and the `0x23` case in `WzLib/WzImage.cs` - text images.
- Verification: `dotnet test MapleLib.Tests -c Release --filter "FullyQualifiedName~WzMsFile"`.
