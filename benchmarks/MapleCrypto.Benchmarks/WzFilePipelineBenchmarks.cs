using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.WzLib;

namespace MapleCrypto.Benchmarks;

/// <summary>
/// End-to-end WZ loading measurements over the small, versioned fixtures that
/// ship with MapleLib.Tests.  Keeping this benchmark fixture based catches
/// regressions in header parsing, directory offsets, and lazy image loading
/// that binary microbenchmarks cannot expose.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzFilePipelineBenchmarks
{
    private string _path = null!;
    private short _patchVersion;
    private WzMapleVersion _mapleVersion;

    [Params(WzPipelineFixture.Gms95, WzPipelineFixture.Tms113Item)]
    public WzPipelineFixture Fixture { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (_path, _patchVersion, _mapleVersion) = ResolveFixture(Fixture);
        if (!File.Exists(_path))
            throw new FileNotFoundException($"WZ benchmark fixture not found: {_path}", _path);
    }

    [Benchmark]
    public int ParseDirectory()
    {
        using var file = new WzFile(_path, _patchVersion, _mapleVersion);
        WzFileParseStatus status = file.ParseWzFile();
        if (status != WzFileParseStatus.Success)
            throw new InvalidDataException($"Unable to parse {_path}: {status}");

        return file.WzDirectory.CountImages();
    }

    [Benchmark]
    public int ParseDirectoryAndImages()
    {
        using var file = new WzFile(_path, _patchVersion, _mapleVersion);
        WzFileParseStatus status = file.ParseWzFile();
        if (status != WzFileParseStatus.Success)
            throw new InvalidDataException($"Unable to parse {_path}: {status}");

        file.WzDirectory.ParseImages();
        return file.WzDirectory.CountImages();
    }

    internal static (string Path, short PatchVersion, WzMapleVersion MapleVersion) ResolveFixture(WzPipelineFixture fixture)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "WzFiles", "Common");
        return fixture switch
        {
            WzPipelineFixture.Gms95 =>
                (Path.Combine(root, "TamingMob_GMS_95.wz"), 95, WzMapleVersion.GMS),
            WzPipelineFixture.Tms113Item =>
                (Path.Combine(root, "TMS_113_Item.wz"), 113, WzMapleVersion.EMS),
            _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture, null)
        };
    }
}

public enum WzPipelineFixture
{
    Gms95,
    Tms113Item
}

internal static class WzFilePipelineCorrectness
{
    public static void Verify()
    {
        foreach (WzPipelineFixture fixture in Enum.GetValues<WzPipelineFixture>())
        {
            (string path, short patchVersion, WzMapleVersion mapleVersion) =
                WzFilePipelineBenchmarks.ResolveFixture(fixture);

            // The benchmark output copies fixtures next to the executable.  A
            // source-tree invocation can legitimately omit them, so defer the
            // check until the benchmark setup in that case.
            if (!File.Exists(path))
                continue;

            using var file = new WzFile(path, patchVersion, mapleVersion);
            if (file.ParseWzFile() != WzFileParseStatus.Success)
                throw new InvalidDataException($"WZ fixture failed to parse: {path}");

            int imageCount = file.WzDirectory.CountImages();
            if (imageCount <= 0)
                throw new InvalidDataException($"WZ fixture has no images: {path}");

            file.WzDirectory.ParseImages();
        }
    }
}
