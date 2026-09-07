using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.Helpers;
using Microsoft.Xna.Framework.Graphics;

namespace MapleCrypto.Benchmarks;

public enum ImageDataPattern
{
    OpaqueColor,
    BinaryAlpha,
    SmoothAlpha
}

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ImageFormatDetectorBenchmarks
{
    [Params(64, 256)] public int Size { get; set; }
    [ParamsAllValues] public ImageDataPattern Pattern { get; set; }

    private byte[] _argb = null!;

    [GlobalSetup]
    public void Setup()
    {
        _argb = new byte[Size * Size * 4];
        var random = new Random(0x5EED);
        for (int index = 0; index < _argb.Length; index += 4)
        {
            byte color = (byte)random.Next(256);
            _argb[index] = Pattern == ImageDataPattern.SmoothAlpha ? color : (byte)random.Next(256);
            _argb[index + 1] = Pattern == ImageDataPattern.SmoothAlpha ? color : (byte)random.Next(256);
            _argb[index + 2] = Pattern == ImageDataPattern.SmoothAlpha ? color : (byte)random.Next(256);
            _argb[index + 3] = Pattern switch
            {
                ImageDataPattern.OpaqueColor => byte.MaxValue,
                ImageDataPattern.BinaryAlpha => (index & 4) == 0 ? byte.MinValue : byte.MaxValue,
                _ => (byte)((index / 4) % Size * 255 / Math.Max(1, Size - 1))
            };
        }
    }

    [Benchmark]
    public (int, int, bool, bool, byte, double, double, bool) Analyze() =>
        ImageFormatDetector.AnalyzeImageData(_argb, Size, Size);

    [Benchmark]
    public SurfaceFormat Determine() => ImageFormatDetector.DetermineTextureFormat(_argb, Size, Size);
}
