using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.MapleCryptoLib;
using MapleCryptoEngine = MapleLib.MapleCryptoLib.MapleCrypto;

namespace MapleCrypto.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class CryptoPlateauBenchmarks
{
    private const int Size = 512;
    private static readonly byte[] Iv = [0x4D, 0x23, 0xC7, 0x2B];

    private byte[] _source = null!;
    private byte[] _encrypted = null!;
    private byte[] _work = null!;
    private MapleCryptoEngine _crypto = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = CryptoCorrectness.CreatePayload(Size);
        _encrypted = (byte[])_source.Clone();
        MapleCustomEncryption.Encrypt(_encrypted);
        _work = new byte[Size];
        _crypto = new MapleCryptoEngine((byte[])Iv.Clone(), 95);
    }

    [Benchmark]
    public byte CustomEncrypt512()
    {
        _source.CopyTo(_work, 0);
        MapleCustomEncryption.Encrypt(_work);
        return _work[^1];
    }

    [Benchmark]
    public byte CustomDecrypt512()
    {
        _encrypted.CopyTo(_work, 0);
        MapleCustomEncryption.Decrypt(_work);
        return _work[^1];
    }

    [Benchmark]
    public byte AesCrypt512()
    {
        _source.CopyTo(_work, 0);
        MapleAESEncryption.AesCrypt(Iv, _work, Size);
        return _work[^1];
    }

    [Benchmark]
    public byte FullPacketCrypt512()
    {
        _source.CopyTo(_work, 0);
        MapleCustomEncryption.Encrypt(_work);
        _crypto.Crypt(_work);
        return _work[^1];
    }
}
