using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.MapleCryptoLib;
using MapleCryptoEngine = MapleLib.MapleCryptoLib.MapleCrypto;

namespace MapleCrypto.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class CryptoPrimitiveBenchmarks
{
    private readonly byte[] _iv = [0x4D, 0x23, 0xC7, 0x2B];
    private readonly byte[] _multiplySource = [0x4D, 0x23, 0xC7, 0x2B];
    private readonly MapleCryptoEngine _crypto = new([0x4D, 0x23, 0xC7, 0x2B], 95);
    private int _packetHeader = unchecked((int)0xA1B2C3D4);

    [Benchmark]
    public byte[] TrimUserKey() =>
        MapleCryptoConstants.GetTrimmedUserKey(ref MapleCryptoConstants.UserKey_WzLib);

    [Benchmark]
    public byte[] GetNewIV() => MapleCryptoEngine.GetNewIV(_iv);

    [Benchmark]
    public byte[] GetHeaderToClient() => _crypto.GetHeaderToClient(1460);

    [Benchmark]
    public byte[] GetHeaderToServer() => _crypto.GetHeaderToServer(1460);

    [Benchmark]
    public int GetPacketLength() => MapleCryptoEngine.GetPacketLength(++_packetHeader);

    [Benchmark]
    public byte[] MultiplyBytes() => MapleCryptoEngine.MultiplyBytes(_multiplySource, 4, 4);

    [Benchmark]
    public byte[] MultiplyBytesSimd() => MapleCryptoEngine.MultiplyBytes_SIMD(_multiplySource, 4, 4);
}
