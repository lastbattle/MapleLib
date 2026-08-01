using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using System.Buffers;
using System.Text;

namespace MapleCrypto.Benchmarks;

/// <summary>
/// Focused measurements for the binary primitives used by WZ/IMG parsing and
/// writing.  The benchmark values are deliberately representative of encoded
/// names rather than arbitrary byte copies: WZ strings carry an encrypted mask,
/// a signed length marker, and (for long values) a four-byte length.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzBinaryIoBenchmarks
{
    private static readonly byte[] Iv = WzAESConstant.WZ_BMSCLASSIC;

    private string _ascii = null!;
    private string _unicode = null!;
    private byte[] _asciiEncoded = null!;
    private byte[] _unicodeEncoded = null!;
    private MemoryStream _writeStream = null!;
    private WzBinaryWriter _writer = null!;
    private WzBinaryReader _asciiReader = null!;
    private WzBinaryReader _unicodeReader = null!;
    private WzBinaryReader _offsetReader = null!;
    private long _secondStringOffset;

    [Params(32, 127, 128, 4096)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _ascii = CreateAscii(Length);
        _unicode = CreateUnicode(Length);
        _asciiEncoded = EncodeString(_ascii);
        _unicodeEncoded = EncodeString(_unicode);

        _asciiReader = CreateReader(_asciiEncoded);
        _unicodeReader = CreateReader(_unicodeEncoded);

        // Keep an offset-based string in the same stream so ReadStringAtOffset
        // can be measured while preserving the caller's current position.
        byte[] offsetData;
        using (var stream = new MemoryStream())
        {
            stream.WriteByte(0x00);
            stream.Write(_asciiEncoded);
            _secondStringOffset = stream.Position;
            stream.Write(_unicodeEncoded);
            offsetData = stream.ToArray();
        }

        _offsetReader = CreateReader(offsetData);
        _offsetReader.BaseStream.Position = 1;

        _writeStream = new MemoryStream(Math.Max(Length * 2 + 8, 32));
        _writer = new WzBinaryWriter(_writeStream, Iv, leaveOpen: true)
        {
            Header = new WzHeader { FStart = 0 }
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _asciiReader?.Dispose();
        _unicodeReader?.Dispose();
        _offsetReader?.Dispose();
        _writer?.Dispose();
        _writeStream?.Dispose();
    }

    [Benchmark]
    public int ReadAscii()
    {
        _asciiReader.BaseStream.Position = 0;
        return _asciiReader.ReadString().Length;
    }

    [Benchmark]
    public int ReadUnicode()
    {
        _unicodeReader.BaseStream.Position = 0;
        return _unicodeReader.ReadString().Length;
    }

    [Benchmark]
    public int ReadStringAtOffset()
    {
        _offsetReader.BaseStream.Position = 1;
        string value = _offsetReader.ReadStringAtOffset(_secondStringOffset);
        return value.Length + (int)_offsetReader.BaseStream.Position;
    }

    [Benchmark]
    public long WriteAscii()
    {
        ResetWriter();
        _writer.Write(_ascii);
        return _writeStream.Position;
    }

    [Benchmark]
    public long WriteUnicode()
    {
        ResetWriter();
        _writer.Write(_unicode);
        return _writeStream.Position;
    }

    [Benchmark]
    public long WriteCachedStringValue()
    {
        ResetWriter();
        _writer.WriteStringValue(_ascii, WzImage.WzImageHeaderByte_WithoutOffset,
            WzImage.WzImageHeaderByte_WithOffset);
        _writer.WriteStringValue(_ascii, WzImage.WzImageHeaderByte_WithoutOffset,
            WzImage.WzImageHeaderByte_WithOffset);
        return _writeStream.Position;
    }

    [Benchmark]
    public int EncryptString()
    {
        char[] encrypted = _writer.EncryptString(_unicode);
        return encrypted.Length;
    }

    private void ResetWriter()
    {
        _writeStream.Position = 0;
        _writeStream.SetLength(0);
        _writer.StringCache.Clear();
    }

    private static WzBinaryReader CreateReader(byte[] encoded)
    {
        return new WzBinaryReader(new MemoryStream(encoded, writable: false), Iv);
    }

    private static byte[] EncodeString(string value)
    {
        using var stream = new MemoryStream();
        using var writer = new WzBinaryWriter(stream, Iv, leaveOpen: true);
        writer.Write(value);
        return stream.ToArray();
    }

    private static string CreateAscii(int length)
    {
        if (length == 0)
            return string.Empty;

        return string.Create(length, 0, static (chars, _) =>
        {
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)('A' + (i % 26));
        });
    }

    private static string CreateUnicode(int length)
    {
        if (length == 0)
            return string.Empty;

        return string.Create(length, 0, static (chars, _) =>
        {
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)('Ā' + (i % 64));
        });
    }
}

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzMutableKeyBenchmarks
{
    private static readonly byte[] Iv = [0x4D, 0x23, 0xC7, 0x2B];
    private WzMutableKey _cachedKey = null!;

    [Params(64, 4096, 65536, 1_048_576)]
    public int KeySize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _cachedKey = WzKeyGenerator.GenerateWzKey(Iv);
        _cachedKey.EnsureKeySize(KeySize);
    }

    [Benchmark]
    public int GenerateAndEnsure()
    {
        WzMutableKey key = WzKeyGenerator.GenerateWzKey(Iv);
        key.EnsureKeySize(KeySize);
        return key[KeySize - 1];
    }

    [Benchmark]
    public int GetKeysClone()
    {
        byte[] keys = _cachedKey.GetKeys();
        return keys.Length == 0 ? 0 : keys[^1];
    }

    [Benchmark]
    public int IndexedAccess()
    {
        int checksum = 0;
        int step = Math.Max(1, KeySize / 64);
        for (int i = 0; i < KeySize; i += step)
            checksum ^= _cachedKey[i];
        return checksum;
    }
}

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzSectionReaderBenchmarks
{
    private byte[] _source = null!;
    private WzBinaryReader _reader = null!;
    private int _sectionStart;
    private int _sectionLength;

    [Params(64 * 1024, 1 * 1024 * 1024)]
    public int SectionLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _sectionStart = 128;
        _sectionLength = SectionLength;
        _source = new byte[_sectionStart + _sectionLength + 128];
        for (int i = 0; i < _source.Length; i++)
            _source[i] = (byte)((i * 31 + 17) & 0xFF);

        _reader = new WzBinaryReader(new MemoryStream(_source, writable: false), WzAESConstant.WZ_BMSCLASSIC);
        _reader.BaseStream.Position = 37;
    }

    [GlobalCleanup]
    public void Cleanup() => _reader?.Dispose();

    [Benchmark]
    public long CreateReaderForSection()
    {
        long before = _reader.BaseStream.Position;
        using WzBinaryReader section = _reader.CreateReaderForSection(_sectionStart, _sectionLength);
        return section.BaseStream.Length + before + section.BaseStream.Position;
    }
}

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class PartialStreamBenchmarks
{
    private MemoryStream _baseStream = null!;
    private PartialStream _partial = null!;
    private MemoryStream _destination = null!;
    private byte[] _buffer = null!;

    [Params(4 * 1024, 1 * 1024 * 1024)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        byte[] source = new byte[Length + 257];
        for (int i = 0; i < source.Length; i++)
            source[i] = (byte)((i * 17 + 3) & 0xFF);

        _baseStream = new MemoryStream(source, writable: true);
        _partial = new PartialStream(_baseStream, 127, Length, leaveOpen: true);
        _destination = new MemoryStream(Length);
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Min(80 * 1024, Math.Max(1, Length)));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_buffer != null)
            ArrayPool<byte>.Shared.Return(_buffer);
        _destination?.Dispose();
        _partial?.Dispose();
        _baseStream?.Dispose();
    }

    [Benchmark]
    public int ReadAll()
    {
        _partial.Position = 0;
        int total = 0;
        int checksum = 0;
        while (total < Length)
        {
            int read = _partial.Read(_buffer, 0, Math.Min(_buffer.Length, Length - total));
            if (read == 0)
                break;
            checksum ^= _buffer[0];
            checksum ^= _buffer[read - 1];
            total += read;
        }

        return total ^ checksum;
    }

    [Benchmark]
    public long CopyToMemoryStream()
    {
        _partial.Position = 0;
        _destination.Position = 0;
        _destination.SetLength(0);
        _partial.CopyTo(_destination);
        return _destination.Length + _destination.Position;
    }
}

internal static class WzBinaryIoCorrectness
{
    private static readonly byte[] Iv = WzAESConstant.WZ_BMSCLASSIC;

    public static void Verify()
    {
        foreach (int length in new[] { 0, 1, 127, 128, 4096 })
        {
            string ascii = CreateAscii(length);
            string unicode = CreateUnicode(length);
            AssertRoundTrip(ascii, $"ASCII {length}");
            AssertRoundTrip(unicode, $"Unicode {length}");
        }

        VerifyOffsetRead();
        VerifySectionReader();
        VerifyPartialStreamBounds();
        VerifyMutableKey();
    }

    private static void AssertRoundTrip(string expected, string operation)
    {
        byte[] encoded;
        using (var stream = new MemoryStream())
        {
            using var writer = new WzBinaryWriter(stream, Iv, leaveOpen: true);
            writer.Write(expected);
            encoded = stream.ToArray();
        }

        using var reader = new WzBinaryReader(new MemoryStream(encoded, writable: false), Iv);
        string actual = reader.ReadString();
        if (!StringComparer.Ordinal.Equals(expected, actual))
            throw new InvalidOperationException($"WZ binary correctness failed: {operation}.");
    }

    private static void VerifyOffsetRead()
    {
        byte[] first;
        byte[] second;
        using (var firstStream = new MemoryStream())
        {
            using var writer = new WzBinaryWriter(firstStream, Iv, leaveOpen: true);
            writer.Write("first");
            first = firstStream.ToArray();
        }
        using (var secondStream = new MemoryStream())
        {
            using var writer = new WzBinaryWriter(secondStream, Iv, leaveOpen: true);
            writer.Write("second");
            second = secondStream.ToArray();
        }

        byte[] data = new byte[first.Length + second.Length];
        first.CopyTo(data, 0);
        second.CopyTo(data, first.Length);
        using var reader = new WzBinaryReader(new MemoryStream(data, writable: false), Iv);
        reader.BaseStream.Position = 1;
        string value = reader.ReadStringAtOffset(first.Length);
        if (value != "second" || reader.BaseStream.Position != 1)
            throw new InvalidOperationException("WZ offset string read did not restore stream position.");
    }

    private static void VerifySectionReader()
    {
        byte[] source = new byte[256];
        for (int i = 0; i < source.Length; i++)
            source[i] = (byte)i;

        using var reader = new WzBinaryReader(new MemoryStream(source, writable: false), Iv);
        reader.BaseStream.Position = 11;
        using WzBinaryReader section = reader.CreateReaderForSection(32, 64);
        if (reader.BaseStream.Position != 11 || section.BaseStream.Length != 64)
            throw new InvalidOperationException("WZ section reader changed the source stream state.");

        byte[] actual = section.ReadBytes(64);
        if (!actual.AsSpan().SequenceEqual(source.AsSpan(32, 64)))
            throw new InvalidOperationException("WZ section reader returned different bytes.");
    }

    private static void VerifyPartialStreamBounds()
    {
        byte[] source = new byte[32];
        for (int i = 0; i < source.Length; i++)
            source[i] = (byte)i;

        using var baseStream = new MemoryStream(source, writable: true);
        using var partial = new PartialStream(baseStream, 5, 10, leaveOpen: true);
        partial.Position = 0;
        byte[] buffer = new byte[32];
        int read = partial.Read(buffer, 0, buffer.Length);
        if (read != 10 || !buffer.AsSpan(0, 10).SequenceEqual(source.AsSpan(5, 10)))
            throw new InvalidOperationException("PartialStream did not clamp a read to its range.");
    }

    private static void VerifyMutableKey()
    {
        byte[] iv = [0x4D, 0x23, 0xC7, 0x2B];
        WzMutableKey first = WzKeyGenerator.GenerateWzKey(iv);
        WzMutableKey second = WzKeyGenerator.GenerateWzKey(iv);
        first.EnsureKeySize(8192);
        second.EnsureKeySize(8192);
        if (!first.GetKeys().AsSpan().SequenceEqual(second.GetKeys()))
            throw new InvalidOperationException("Mutable WZ keys are not deterministic.");

        WzMutableKey zero = WzKeyGenerator.GenerateWzKey(WzAESConstant.WZ_BMSCLASSIC);
        zero.EnsureKeySize(128);
        if (zero.GetKeys().Any(static value => value != 0))
            throw new InvalidOperationException("Zero-IV WZ key was not all zero bytes.");
    }

    private static string CreateAscii(int length)
    {
        if (length == 0)
            return string.Empty;

        return string.Create(length, 0, static (chars, _) =>
        {
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)('A' + (i % 26));
        });
    }

    private static string CreateUnicode(int length)
    {
        if (length == 0)
            return string.Empty;

        return string.Create(length, 0, static (chars, _) =>
        {
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)('\u0100' + (i % 64));
        });
    }
}
