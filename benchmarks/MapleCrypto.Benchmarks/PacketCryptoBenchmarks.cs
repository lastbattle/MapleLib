using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using MapleLib.MapleCryptoLib;
using MapleCryptoEngine = MapleLib.MapleCryptoLib.MapleCrypto;

namespace MapleCrypto.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class PacketCryptoBenchmarks
{
    private static readonly byte[] Iv = [0x4D, 0x23, 0xC7, 0x2B];

    private byte[] _source = null!;
    private byte[] _encrypted = null!;
    private byte[] _work = null!;
    private MapleCryptoEngine _crypto = null!;

    [Params(32, 128, 512, 1460, 8192)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = CryptoCorrectness.CreatePayload(Size);
        _encrypted = (byte[])_source.Clone();
        MapleCustomEncryption.Encrypt(_encrypted);
        _work = new byte[Size];
        _crypto = new MapleCryptoEngine((byte[])Iv.Clone(), 95);
    }

    [BenchmarkCategory("Custom")]
    [Benchmark]
    public byte CustomEncrypt()
    {
        _source.CopyTo(_work, 0);
        MapleCustomEncryption.Encrypt(_work);
        return _work[Size - 1];
    }

    [BenchmarkCategory("Custom")]
    [Benchmark]
    public byte CustomDecrypt()
    {
        _encrypted.CopyTo(_work, 0);
        MapleCustomEncryption.Decrypt(_work);
        return _work[Size - 1];
    }

    [BenchmarkCategory("AES")]
    [Benchmark]
    public byte AesCrypt()
    {
        _source.CopyTo(_work, 0);
        MapleAESEncryption.AesCrypt(Iv, _work, Size);
        return _work[Size - 1];
    }

    [BenchmarkCategory("Pipeline")]
    [Benchmark]
    public byte FullPacketCrypt()
    {
        _source.CopyTo(_work, 0);
        MapleCustomEncryption.Encrypt(_work);
        _crypto.Crypt(_work);
        return _work[Size - 1];
    }
}
