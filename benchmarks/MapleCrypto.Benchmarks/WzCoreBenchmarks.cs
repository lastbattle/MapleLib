using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace MapleCrypto.Benchmarks;

/// <summary>
/// Synthetic benchmarks for wide WZ directories/images and mutation costs.
/// The fixture is independent of WzFileManager and disk I/O so lookup and
/// collection costs can be measured without parser or crypto noise.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzCoreBenchmarks
{
    private const int MutationNameCount = 1024;

    private WzDirectory _wideDirectory = null!;
    private WzImage _wideImage = null!;
    private WzImage _mutationImage = null!;
    private string _directoryImageHitName = null!;
    private string _directoryDirectoryHitName = null!;
    private string _imagePropertyHitName = null!;
    private WzObject _directoryImageHit = null!;
    private WzObject _directoryDirectoryHit = null!;
    private WzImageProperty _imagePropertyHit = null!;
    private string[] _mutationNames = null!;
    private int _mutationIndex;

    [Params(128, 1024, 4096)]
    public int Width { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        BuildWideFixtures();
        BuildMutationFixture();
        VerifyFixtureInvariants();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _wideDirectory?.Dispose();
        _wideImage?.Dispose();
        _mutationImage?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int DirectoryIndexerHit()
    {
        return ReferenceEquals(_wideDirectory[_directoryImageHitName], _directoryImageHit) ? 1 : 0;
    }

    [Benchmark]
    public int DirectoryIndexerMiss()
    {
        return _wideDirectory["MissingDirectoryOrImage.img"] is null ? 1 : 0;
    }

    [Benchmark]
    public int DirectoryGetImageByNameHit()
    {
        return ReferenceEquals(_wideDirectory.GetImageByName(_directoryImageHitName), _directoryImageHit) ? 1 : 0;
    }

    [Benchmark]
    public int DirectoryGetDirectoryByNameHit()
    {
        return ReferenceEquals(_wideDirectory.GetDirectoryByName(_directoryDirectoryHitName), _directoryDirectoryHit) ? 1 : 0;
    }

    [Benchmark]
    public int ImageIndexerHit()
    {
        return ReferenceEquals(_wideImage[_imagePropertyHitName], _imagePropertyHit) ? 1 : 0;
    }

    [Benchmark]
    public int ImageIndexerMiss()
    {
        return _wideImage["MissingProperty"] is null ? 1 : 0;
    }

    [Benchmark]
    public int AddRemoveProperty()
    {
        string name = _mutationNames[_mutationIndex++ & (MutationNameCount - 1)];
        WzIntProperty property = new(name, _mutationIndex);
        _mutationImage.AddProperty(property);
        int count = _mutationImage.WzProperties.Count;
        _mutationImage.RemoveProperty(property);
        return count;
    }

    private void BuildWideFixtures()
    {
        _wideDirectory = new WzDirectory("WideRoot");
        for (int i = 0; i < Width; i++)
        {
            WzImage image = new($"Image{i:D6}.img");
            _wideDirectory.AddImage(image);

            WzDirectory directory = new($"Directory{i:D6}");
            _wideDirectory.AddDirectory(directory);
        }

        _directoryImageHitName = $"Image{Width - 1:D6}.img";
        _directoryDirectoryHitName = $"Directory{Width - 1:D6}";
        _directoryImageHit = _wideDirectory[_directoryImageHitName];
        _directoryDirectoryHit = _wideDirectory[_directoryDirectoryHitName];

        _wideImage = new WzImage("Wide.img");
        for (int i = 0; i < Width; i++)
        {
            // Add directly to the parent-aware collection so fixture setup
            // does not itself measure WzImage.AddProperty's duplicate scan.
            _wideImage.WzProperties.Add(new WzIntProperty($"Property{i:D6}", i));
        }

        _imagePropertyHitName = $"Property{Width - 1:D6}";
        _imagePropertyHit = _wideImage[_imagePropertyHitName];
    }

    private void BuildMutationFixture()
    {
        _mutationImage = new WzImage("Mutation.img");
        for (int i = 0; i < Width; i++)
        {
            _mutationImage.WzProperties.Add(new WzIntProperty($"Existing{i:D6}", i));
        }

        _mutationNames = new string[MutationNameCount];
        for (int i = 0; i < _mutationNames.Length; i++)
        {
            _mutationNames[i] = $"Added{i:D8}";
        }
    }

    private void VerifyFixtureInvariants()
    {
        if (_directoryImageHit is null || _directoryDirectoryHit is null || _imagePropertyHit is null)
        {
            throw new InvalidOperationException("Wide lookup fixture did not resolve its terminal nodes.");
        }

        WzIntProperty probe = new("ParentProbe", 1);
        _mutationImage.AddProperty(probe);
        if (!ReferenceEquals(probe.Parent, _mutationImage))
        {
            throw new InvalidOperationException("WzImage.AddProperty did not set Parent.");
        }
        _mutationImage.RemoveProperty(probe);
        if (probe.Parent is not null)
        {
            throw new InvalidOperationException("WzImage.RemoveProperty did not clear Parent.");
        }
    }
}

/// <summary>
/// Compares the indexed directory mutation path with the legacy list-only
/// implementation.  Both methods allocate one image per operation and remove
/// it before the next invocation, so the steady-state collection width stays
/// fixed.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzDirectoryMutationBenchmarks
{
    private const int MutationNameCount = 1024;

    [Params(128, 1024, 4096)]
    public int Width { get; set; }

    private List<WzImage> _legacyImages = null!;
    private WzDirectory _legacyParent = null!;
    private WzDirectory _indexedDirectory = null!;
    private string[] _mutationNames = null!;
    private int _mutationIndex;

    [GlobalSetup]
    public void Setup()
    {
        _legacyImages = new List<WzImage>(Width);
        _legacyParent = new WzDirectory("LegacyRoot");
        for (int i = 0; i < Width; i++)
        {
            WzImage image = new($"Image{i:D6}.img")
            {
                Parent = _legacyParent
            };
            _legacyImages.Add(image);
        }

        _indexedDirectory = new WzDirectory("IndexedRoot");
        for (int i = 0; i < Width; i++)
            _indexedDirectory.AddImage(new WzImage($"Image{i:D6}.img"));

        _mutationNames = new string[MutationNameCount];
        for (int i = 0; i < _mutationNames.Length; i++)
            _mutationNames[i] = $"Added{i:D8}.img";

        if (_legacyImages.Count != Width || _indexedDirectory.WzImages.Count != Width)
            throw new InvalidOperationException("Directory mutation benchmark fixture is invalid.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_legacyImages != null)
        {
            foreach (WzImage image in _legacyImages)
                image.Dispose();
        }

        _indexedDirectory?.Dispose();
        _legacyParent?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int LegacyListAddRemove()
    {
        string name = _mutationNames[_mutationIndex++ & (MutationNameCount - 1)];
        WzImage image = new(name);
        _legacyImages.Add(image);
        image.Parent = _legacyParent;
        int count = _legacyImages.Count;
        _legacyImages.Remove(image);
        image.Parent = null!;

        if (count != Width + 1 || _legacyImages.Count != Width || image.Parent != null)
            throw new InvalidOperationException("Legacy directory mutation invariant failed.");
        return count;
    }

    [Benchmark]
    public int IndexedDirectoryAddRemove()
    {
        string name = _mutationNames[_mutationIndex++ & (MutationNameCount - 1)];
        WzImage image = new(name);
        _indexedDirectory.AddImage(image);
        int count = _indexedDirectory.WzImages.Count;
        _indexedDirectory.RemoveImage(image);

        if (count != Width + 1 || _indexedDirectory.WzImages.Count != Width || image.Parent != null)
            throw new InvalidOperationException("Indexed directory mutation invariant failed.");
        return count;
    }
}

/// <summary>
/// Synthetic benchmarks for deep property and WzFile path traversal.
/// Kept in a separate benchmark class so Width and Depth do not form a
/// Cartesian product when BenchmarkDotNet enumerates parameters.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzPathBenchmarks
{
    private WzImage _pathImage = null!;
    private WzFile _pathFile = null!;
    private Dictionary<string, WzObject> _pathCache = null!;
    private string _deepImagePath = null!;
    private string _deepFilePath = null!;
    private WzImageProperty _deepTarget = null!;

    [Params(4, 16, 64)]
    public int Depth { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        BuildPathFixture();
        VerifyFixtureInvariants();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pathFile?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int ImageGetFromPathDeep()
    {
        return ReferenceEquals(_pathImage.GetFromPath(_deepImagePath), _deepTarget) ? 1 : 0;
    }

    [Benchmark]
    public int FileGetObjectFromPathCached()
    {
        return ReferenceEquals(_pathFile.GetObjectFromPath(_deepFilePath, checkFirstDirectoryName: false), _deepTarget) ? 1 : 0;
    }

    [Benchmark]
    public int FileGetObjectFromPathCold()
    {
        // The cache is private by design. Clearing the captured dictionary
        // keeps this benchmark focused on traversal rather than a one-time
        // cache fill while avoiding reflection in the measured call.
        _pathCache.Clear();
        return ReferenceEquals(_pathFile.GetObjectFromPath(_deepFilePath, checkFirstDirectoryName: false), _deepTarget) ? 1 : 0;
    }

    private void BuildPathFixture()
    {
        _pathImage = new WzImage("Deep.img");
        IPropertyContainer container = _pathImage;
        string[] segments = new string[Depth + 1];

        for (int i = 0; i < Depth; i++)
        {
            string segment = $"Level{i:D2}";
            segments[i] = segment;
            WzSubProperty child = new(segment);
            container.AddProperty(child);
            container = child;
        }

        segments[^1] = "Target";
        WzIntProperty target = new("Target", 42);
        container.AddProperty(target);
        _deepTarget = target;
        _deepImagePath = string.Join('/', segments);
        _deepFilePath = $"{_pathImage.Name}/{_deepImagePath}";

        _pathFile = new WzFile(95, WzMapleVersion.GMS)
        {
            Name = "Bench.wz"
        };
        _pathFile.WzDirectory.Name = "Bench.wz";
        _pathFile.WzDirectory.AddImage(_pathImage);

        FieldInfo cacheField = typeof(WzFile).GetField("_pathCache", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(WzFile).FullName, "_pathCache");
        _pathCache = (Dictionary<string, WzObject>)(cacheField.GetValue(_pathFile)
            ?? throw new InvalidOperationException("WzFile path cache was not initialized."));
    }

    private void VerifyFixtureInvariants()
    {
        if (!ReferenceEquals(_pathImage.GetFromPath(_deepImagePath), _deepTarget))
        {
            throw new InvalidOperationException("Deep image path fixture did not resolve its target.");
        }

        if (!ReferenceEquals(_pathFile.GetObjectFromPath(_deepFilePath, checkFirstDirectoryName: false), _deepTarget))
        {
            throw new InvalidOperationException("Deep file path fixture did not resolve its target.");
        }
        _pathCache.Clear();
    }
}
