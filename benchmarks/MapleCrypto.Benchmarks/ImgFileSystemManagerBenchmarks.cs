using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.Img;
using MapleLib.WzLib;

namespace MapleCrypto.Benchmarks;

/// <summary>
/// Measures the cached LoadImage path against an extracted real-corpus IMG.
/// Set MAPLELIB_WZEXPORT_ROOT to use another extracted version directory.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ImgFileSystemManagerBenchmarks
{
    private const int OperationsPerInvoke = 256;

    private ImgFileSystemManager _manager = null!;
    private WzImage _expectedImage = null!;
    private string _scratchRoot = null!;

    [GlobalSetup]
    public void Setup()
    {
        string corpusRoot = Environment.GetEnvironmentVariable("MAPLELIB_WZEXPORT_ROOT")
            ?? throw new InvalidOperationException("Set MAPLELIB_WZEXPORT_ROOT to a GMS v95 corpus directory.");
        string fixturePath = Path.Combine(corpusRoot, "String", "Map.img");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                "The cached LoadImage benchmark requires String/Map.img in an extracted corpus. " +
                "Set MAPLELIB_WZEXPORT_ROOT to the version directory.",
                fixturePath);
        }

        // Keep the user-supplied corpus read-only. The manager can upgrade old
        // manifests during construction, so benchmark a private minimal copy.
        _scratchRoot = Directory.CreateTempSubdirectory("maplelib-img-manager-").FullName;
        string scratchStringPath = Path.Combine(_scratchRoot, "String");
        Directory.CreateDirectory(scratchStringPath);
        File.Copy(fixturePath, Path.Combine(scratchStringPath, "Map.img"));

        string manifestPath = Path.Combine(corpusRoot, "manifest.json");
        if (File.Exists(manifestPath))
            File.Copy(manifestPath, Path.Combine(_scratchRoot, "manifest.json"));

        _manager = new ImgFileSystemManager(_scratchRoot);
        _expectedImage = _manager.LoadImage("String", "Map.img")
            ?? throw new InvalidOperationException($"Could not load benchmark fixture: {fixturePath}");

        WzImage cachedImage = _manager.LoadImage("String", "Map.img")
            ?? throw new InvalidOperationException("The warmed cache lookup returned null.");
        if (!ReferenceEquals(_expectedImage, cachedImage))
            throw new InvalidOperationException("The benchmark fixture did not use the manager cache.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _manager?.Dispose();
        if (!string.IsNullOrEmpty(_scratchRoot) && Directory.Exists(_scratchRoot))
            Directory.Delete(_scratchRoot, recursive: true);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int CachedLoadImage()
    {
        int hits = 0;
        for (int operation = 0; operation < OperationsPerInvoke; operation++)
        {
            if (ReferenceEquals(_expectedImage, _manager.LoadImage("String", "Map.img")))
                hits++;
        }

        return hits;
    }
}
