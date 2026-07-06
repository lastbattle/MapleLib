using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.WzLib.MSFile;

namespace MapleCrypto.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ChaCha20TransformBenchmarks
{
    private static readonly byte[] Key =
    [
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
        0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F
    ];

    private static readonly byte[] Nonce =
    [
        0x00, 0x00, 0x00, 0x09,
        0x00, 0x00, 0x00, 0x4A,
        0x00, 0x00, 0x00, 0x00
    ];

    private byte[] _work = [];

    [Params(4096, 1_048_576, 16_777_216)]
    public int Size { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _work = CryptoCorrectness.CreatePayload(Size);
    }

    [Benchmark]
    public byte TransformInPlace()
    {
        using var transform = new ChaCha20CryptoTransform(Key, Nonce, 1);
        transform.TransformInPlace(_work);
        return _work[^1];
    }
}
