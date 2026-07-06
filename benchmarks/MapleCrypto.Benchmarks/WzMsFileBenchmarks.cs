using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.WzLib.MSFile;
using System.Reflection;

namespace MapleCrypto.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzMsFileBenchmarks
{
    private static readonly byte[] SnowKey = [0x42, 0x35, 0x11, 0x8A, 0x21, 0x5D, 0x77, 0x09, 0x31, 0xC4, 0xD2, 0x17, 0xA8, 0x4F, 0x66, 0x90];
    private static readonly string DefaultPackDirectory = Path.Combine(AppContext.BaseDirectory, "WzFiles", "Ms", "Packs");
    private static readonly MethodInfo DecryptDataToArrayMethod = typeof(WzMsFile).GetMethod("DecryptDataToArray", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(WzMsFile), "DecryptDataToArray");

    private PackCase _pack = null!;
    private byte[] _snowBuffer = [];
    private byte[] _finalBuffer = [];
    private WzMsEntry _recalcEntry = null!;

    [Params(24)]
    public int DecryptEntries { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pack = ResolvePackCase();
        _snowBuffer = CreateData(GetIntEnvironmentVariable("MSFILE_PERF_TRANSFORM_BYTES", 16 * 1024 * 1024));
        _finalBuffer = CreateData(1024 * 1024 + 3);
        _recalcEntry = new WzMsEntry("Mob/0100000.img", 0, 1, 0, 0, 0, 2, 0, CreateData(WzMsConstants.EntryKeySize))
        {
            Data = CreateData(1537)
        };
    }

    [Benchmark]
    public byte Snow2TransformBlock16MiB()
    {
        using var transform = new Snow2CryptoTransform(SnowKey, null, false);
        transform.TransformBlock(_snowBuffer, 0, _snowBuffer.Length, _snowBuffer, 0);
        return _snowBuffer[0];
    }

    [Benchmark]
    public int Snow2TransformFinalBlock1MiBPlus3()
    {
        using var transform = new Snow2CryptoTransform(SnowKey, null, false);
        byte[] result = transform.TransformFinalBlock(_finalBuffer, 0, _finalBuffer.Length);
        return result[0] + result[^1];
    }

    [Benchmark]
    public int ReadHeader()
    {
        using Stream stream = _pack.OpenStream();
        using var file = new WzMsFile(stream, _pack.OriginalFileName, _pack.FilePath);
        return file.Header.EntryCount;
    }

    [Benchmark]
    public int ReadEntries()
    {
        using Stream stream = _pack.OpenStream();
        using var file = new WzMsFile(stream, _pack.OriginalFileName, _pack.FilePath);
        file.ReadEntries();
        return file.Entries.Count;
    }

    [Benchmark]
    public long LoadAndDecryptEntries()
    {
        using Stream stream = _pack.OpenStream();
        using var file = new WzMsFile(stream, _pack.OriginalFileName, _pack.FilePath);
        file.ReadEntries();

        long checksum = 0;
        int count = Math.Min(DecryptEntries, file.Entries.Count);
        for (int i = 0; i < count; i++)
        {
            byte[] decrypted = (byte[])DecryptDataToArrayMethod.Invoke(file, [file.Entries[i]])!;
            checksum += decrypted.Length;
            checksum += decrypted[0];
        }

        return checksum;
    }

    [Benchmark]
    public long LoadAndDecryptFirstEntry()
    {
        using Stream stream = _pack.OpenStream();
        using var file = new WzMsFile(stream, _pack.OriginalFileName, _pack.FilePath);
        file.ReadEntries();

        byte[] decrypted = (byte[])DecryptDataToArrayMethod.Invoke(file, [file.Entries[0]])!;
        return decrypted.Length + decrypted[0] + decrypted[^1];
    }

    [Benchmark]
    public int RecalculateEntryFields100k()
    {
        Random rng = new(1234);
        int checksum = 0;
        for (int i = 0; i < 100_000; i++)
        {
            _recalcEntry.RecalculateFields(1, i, 2, rng);
            checksum += _recalcEntry.CheckSum;
        }

        return checksum;
    }

    private static PackCase ResolvePackCase()
    {
        string? requestedPackFile = Environment.GetEnvironmentVariable("MSFILE_PERF_PACK");
        if (!string.IsNullOrWhiteSpace(requestedPackFile))
        {
            if (!File.Exists(requestedPackFile))
                throw new FileNotFoundException($"MS pack file not found: {requestedPackFile}", requestedPackFile);

            foreach (string originalFileName in GetOriginalFileNameCandidates(requestedPackFile))
            {
                if (CanReadHeader(requestedPackFile, originalFileName))
                    return new PackCase(requestedPackFile, originalFileName);
            }

            throw new InvalidOperationException($"MS pack file is not readable by WzMsFile: {requestedPackFile}");
        }

        string packDirectory = Environment.GetEnvironmentVariable("MSFILE_PERF_PACK_DIR") ?? DefaultPackDirectory;
        if (!Directory.Exists(packDirectory))
            throw new DirectoryNotFoundException($"MS pack directory not found: {packDirectory}");

        foreach (string candidate in Directory.EnumerateFiles(packDirectory, "*.ms").OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (string originalFileName in GetOriginalFileNameCandidates(candidate))
            {
                if (CanReadHeader(candidate, originalFileName))
                    return new PackCase(candidate, originalFileName);
            }
        }

        throw new InvalidOperationException($"No readable MS pack found in: {packDirectory}");
    }

    private static IEnumerable<string> GetOriginalFileNameCandidates(string packFile)
    {
        string physicalName = Path.GetFileName(packFile);
        string stem = Path.GetFileNameWithoutExtension(packFile);
        string prefix = stem.Split('_')[0];
        string threeDigitStem = stem.Length >= prefix.Length + 4 ? stem[..(prefix.Length + 4)] : stem;

        string[] candidates =
        [
            physicalName,
            $"{stem}.wz",
            $"{threeDigitStem}.ms",
            $"{threeDigitStem}.wz",
            $"{prefix}.ms",
            $"{prefix}.wz"
        ];

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool CanReadHeader(string packFile, string originalFileName)
    {
        try
        {
            using FileStream stream = OpenPackFile(packFile);
            using var file = new WzMsFile(stream, originalFileName, packFile);
            return file.Header.EntryCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private static FileStream OpenPackFile(string packFile)
    {
        return new FileStream(packFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.SequentialScan);
    }

    private static byte[] CreateData(int size)
    {
        byte[] data = new byte[size];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)((i * 31 + 17) & 0xFF);
        }

        return data;
    }

    private static int GetIntEnvironmentVariable(string name, int defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out int result) && result > 0 ? result : defaultValue;
    }

    private sealed record PackCase(string FilePath, string OriginalFileName)
    {
        public FileStream OpenStream()
        {
            return OpenPackFile(FilePath);
        }
    }
}
