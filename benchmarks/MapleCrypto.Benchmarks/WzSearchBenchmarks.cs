using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace MapleCrypto.Benchmarks;

/// <summary>
/// Measures wildcard and regex traversal over a deterministic WZ tree.  The
/// tree is registered with a lightweight in-memory WzFileManager because the
/// public search APIs resolve matches through the global manager.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzSearchBenchmarks
{
    private const int PropertiesPerImage = 8;

    private WzFileManager _manager = null!;
    private WzFile _file = null!;
    private string _wildcardPath = null!;
    private string _regexPath = null!;

    [Params(64, 256, 1_024)]
    public int ImageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _manager = new WzFileManager();
        _file = new WzFile(260, WzMapleVersion.GMS)
        {
            Name = "Synthetic"
        };
        _file.WzDirectory.Name = _file.Name;

        for (int imageIndex = 0; imageIndex < ImageCount; imageIndex++)
        {
            WzImage image = new($"Image{imageIndex:D6}.img");
            WzSubProperty root = new("root");
            image.AddProperty(root);
            for (int propertyIndex = 0; propertyIndex < PropertiesPerImage; propertyIndex++)
            {
                // Vector children are represented as explicit X/Y paths by
                // WzFile.GetPathsFromProperty, giving the search benchmark
                // terminal nodes to resolve without loading image codecs.
                root.AddProperty(new WzVectorProperty($"value_{propertyIndex:D2}", propertyIndex, propertyIndex + 1));
            }

            _file.WzDirectory.AddImage(image);
        }

        _manager.LoadWzFile("Synthetic", _file);
        RegisterSyntheticFileList(_manager);

        _wildcardPath = "Synthetic/*/*/*/*";
        _regexPath = @"^Synthetic/Image\d{6}\.img/root/value_\d{2}/[XY]$";

        // Ensure the setup exercises real matches.  This also catches changes
        // to the manager registration contract before BenchmarkDotNet starts.
        int expectedTerminalCount = ImageCount * PropertiesPerImage * 2;
        if (_file.GetObjectsFromWildcardPath(_wildcardPath).Count != expectedTerminalCount)
            throw new InvalidOperationException("Wildcard fixture did not resolve all vector terminals.");
        if (_file.GetObjectsFromRegexPath(_regexPath).Count != expectedTerminalCount)
            throw new InvalidOperationException("Regex fixture did not resolve all vector terminals.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _file?.Dispose();
        _manager?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int WildcardTraversal()
    {
        return _file.GetObjectsFromWildcardPath(_wildcardPath).Count;
    }

    [Benchmark]
    public int RegexTraversal()
    {
        return _file.GetObjectsFromRegexPath(_regexPath).Count;
    }

    private static void RegisterSyntheticFileList(WzFileManager manager)
    {
        // LoadWzFile registers the WzFile itself, while wildcard path
        // resolution also consults the list.wz-derived base-name map.  Keep
        // this setup detail here so the benchmark does not touch production
        // search code or require files on disk.
        FieldInfo listField = typeof(WzFileManager).GetField("_wzFilesList",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(WzFileManager).FullName, "_wzFilesList");
        if (listField.GetValue(manager) is not Dictionary<string, List<string>> fileList)
            throw new InvalidOperationException("WzFileManager list map is unavailable.");

        fileList["Synthetic"] = ["Synthetic"];
    }
}
