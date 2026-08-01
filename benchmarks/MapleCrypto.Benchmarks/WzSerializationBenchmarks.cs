using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.WzProperties;

namespace MapleCrypto.Benchmarks;

/// <summary>
/// Measures the allocation and wall-clock cost of the WZ XML and JSON
/// serializers over a deterministic in-memory image tree.  The fixture uses
/// only scalar and nested properties so the benchmark isolates traversal and
/// text generation from image codecs and WZ parsing.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzSerializationBenchmarks
{
    private WzImage _image = null!;
    private string _outputDirectory = null!;
    private string _classicXmlPath = null!;
    private string _combinedXmlPath = null!;
    private string _jsonPath = null!;

    [Params(100, 1_000, 10_000)]
    public int PropertyCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _image = CreateFixture(PropertyCount);

        _outputDirectory = Path.Combine(Path.GetTempPath(), $"maple-serialization-bench-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDirectory);
        _classicXmlPath = Path.Combine(_outputDirectory, "classic.xml");
        _combinedXmlPath = Path.Combine(_outputDirectory, "combined.xml");
        _jsonPath = Path.Combine(_outputDirectory, "image.json");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _image?.Dispose();
        if (_outputDirectory is not null && Directory.Exists(_outputDirectory))
            Directory.Delete(_outputDirectory, recursive: true);
    }

    [Benchmark(Baseline = true)]
    public long ClassicXml()
    {
        WzClassicXmlSerializer serializer = new(0, LineBreak.None, exportbase64: false);
        serializer.SerializeImage(_image, _classicXmlPath);
        return new FileInfo(_classicXmlPath).Length;
    }

    [Benchmark]
    public long CombinedXml()
    {
        WzNewXmlSerializer serializer = new(0, LineBreak.None);
        serializer.ExportCombinedXml([_image], _combinedXmlPath);
        return new FileInfo(_combinedXmlPath).Length;
    }

    [Benchmark]
    public long Json()
    {
        WzJsonBsonSerializer serializer = new(0, LineBreak.None,
            bExportBase64Data: false, bExportAsJson: true);
        serializer.SerializeImage(_image, _jsonPath);
        return new FileInfo(_jsonPath).Length;
    }

    private static WzImage CreateFixture(int propertyCount)
    {
        WzImage image = new("Synthetic.img");
        WzSubProperty root = new("root");
        image.AddProperty(root);

        // Keep names and values stable between benchmark processes.  A small
        // nested branch every 64 entries exercises recursive serializer paths
        // without making the fixture dependent on random data.
        WzSubProperty? branch = null;
        for (int index = 0; index < propertyCount; index++)
        {
            if ((index & 63) == 0)
            {
                branch = new WzSubProperty($"branch_{index / 64:D4}");
                root.AddProperty(branch);
            }

            branch!.AddProperty(new WzIntProperty($"value_{index:D6}", index));
        }

        return image;
    }
}
