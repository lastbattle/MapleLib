using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;

namespace MapleCrypto.Benchmarks;

/// <summary>
/// Measures serializer traversal and text generation over a real extracted IMG.
/// Set WZ_SERIALIZATION_IMG to a BMS/plain IMG path before running this benchmark.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzCorpusSerializationBenchmarks
{
    private WzImage _image = null!;
    private List<WzObject> _objects = null!;
    private string _outputDirectory = null!;
    private string _classicXmlPath = null!;
    private string _combinedXmlPath = null!;
    private string _jsonPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        string sourcePath = Environment.GetEnvironmentVariable("WZ_SERIALIZATION_IMG")
            ?? throw new InvalidOperationException("Set WZ_SERIALIZATION_IMG to a BMS/plain IMG file.");
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The WZ serialization corpus IMG does not exist.", sourcePath);

        WzImgDeserializer deserializer = new(freeResources: true, calculateChecksum: false);
        _image = deserializer.WzImageFromIMGFile(
            sourcePath,
            WzTool.GetIvByMapleVersion(WzMapleVersion.BMS),
            Path.GetFileName(sourcePath),
            out bool successfullyParsedImage);
        if (!successfullyParsedImage)
            throw new InvalidDataException($"Could not parse serialization corpus IMG '{sourcePath}'.");

        _objects = [_image];
        _outputDirectory = Path.Combine(Path.GetTempPath(), $"maple-corpus-serialization-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDirectory);
        _classicXmlPath = Path.Combine(_outputDirectory, "classic.xml");
        _combinedXmlPath = Path.Combine(_outputDirectory, "combined.xml");
        _jsonPath = Path.Combine(_outputDirectory, "image.json");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _image?.Dispose();
        if (!StringComparer.Ordinal.Equals(
                Environment.GetEnvironmentVariable("WZ_SERIALIZATION_KEEP_OUTPUTS"), "1") &&
            _outputDirectory is not null && Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
        else if (_outputDirectory is not null)
        {
            Console.WriteLine($"Serialization outputs: {_outputDirectory}");
        }
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
        serializer.ExportCombinedXml(_objects, _combinedXmlPath);
        return new FileInfo(_combinedXmlPath).Length;
    }

    [Benchmark]
    public long Json()
    {
        WzJsonBsonSerializer serializer = new(
            0, LineBreak.None, bExportBase64Data: false, bExportAsJson: true);
        serializer.SerializeImage(_image, _jsonPath);
        return new FileInfo(_jsonPath).Length;
    }
}
