using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.Helpers;
using MapleLib.PacketLib;
using System.Text;

namespace MapleCrypto.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ByteHexBenchmarks
{
    [Params(16, 256, 4096)]
    public int Length { get; set; }

    private byte[] _bytes = null!;
    private string _compactHex = null!;
    private string _separatedHex = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bytes = new byte[Length];
        new Random(0x5EED).NextBytes(_bytes);
        _compactHex = Convert.ToHexString(_bytes);
        _separatedHex = string.Join(':', _bytes.Select(static value => $"0x{value:X2}"));
    }

    [Benchmark(Baseline = true)]
    public byte[] HexToBytesCompact() => ByteUtils.HexToBytes(_compactHex);

    [Benchmark]
    public byte[] HexToBytesSeparatedAndPrefixed() => ByteUtils.HexToBytes(_separatedHex);

    [Benchmark]
    public string ByteUtilsBytesToHex() => ByteUtils.BytesToHex(_bytes, "packet:");

    [Benchmark]
    public string HexToolToString() => HexTool.ToString(_bytes);

    [Benchmark]
    public string HexToolByteArrayToString() => HexTool.ByteArrayToString(_bytes);

    [Benchmark]
    public string HexEncodingAscii() => HexEncoding.ToStringFromAscii(_bytes);
}

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class HexToolOverloadBenchmarks
{
    private const int Operations = 1_024;
    private readonly byte _value = 0xAF;
    private readonly char[] _hexCandidates = ['0', '9', 'A', 'F', 'a', 'f', 'G', '*'];
    private PacketReader _reader = null!;
    private PacketWriter _writer = null!;

    [GlobalSetup]
    public void Setup()
    {
        byte[] bytes = new byte[256];
        new Random(0x5EED).NextBytes(bytes);
        _reader = new PacketReader(bytes);
        _writer = new PacketWriter();
        _writer.WriteBytes(bytes);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _reader.Dispose();
        _writer.Dispose();
    }

    [Benchmark] public string SingleByte() => HexTool.ToString(_value);
    [Benchmark] public string LegacySingleByte() => LegacyByte(_value);
    [Benchmark] public string PacketReader() => HexTool.ToString(_reader);
    [Benchmark] public string LegacyPacketReader() => LegacyBytes(_reader.ToArray());
    [Benchmark] public string PacketWriter() => HexTool.ToString(_writer);
    [Benchmark] public string LegacyPacketWriter() => LegacyBytes(_writer.ToArray());

    [Benchmark(OperationsPerInvoke = Operations)]
    public int HexEncodingDigitCheck()
    {
        int matches = 0;
        for (int index = 0; index < Operations; index++)
            if (HexEncoding.IsHexDigit(_hexCandidates[index & 7])) matches++;
        return matches;
    }

    private static string LegacyByte(byte value)
    {
        const string alphabet = "0123456789ABCDEF";
        int shifted = value << 8;
        return new string([alphabet[(shifted >> 12) & 0x0F], alphabet[(shifted >> 8) & 0x0F]]);
    }

    private static string LegacyBytes(byte[] bytes)
    {
        var builder = new StringBuilder();
        foreach (byte value in bytes)
        {
            builder.Append(LegacyByte(value));
            builder.Append(' ');
        }
        return builder.ToString();
    }
}
