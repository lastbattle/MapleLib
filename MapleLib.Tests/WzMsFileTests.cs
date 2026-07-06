using MapleLib.WzLib.MSFile;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace MapleLib.Tests;

[TestClass]
public class WzMsFileTests
{
    private static readonly string PackDirectory = Path.Combine(AppContext.BaseDirectory, "WzFiles", "Ms", "Packs");
    private static readonly MethodInfo DecryptDataToArrayMethod = typeof(WzMsFile).GetMethod("DecryptDataToArray", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(WzMsFile), "DecryptDataToArray");

    [TestMethod]
    public void ReadsBundledPackHeaderEntriesAndEntryData()
    {
        PackCase pack = ResolvePackCase();

        using Stream stream = OpenPackFile(pack.FilePath);
        using var file = new WzMsFile(stream, pack.OriginalFileName, pack.FilePath);

        if (file.Header.EntryCount <= 0)
            Assert.Fail("Expected the bundled MS pack to contain entries.");

        file.ReadEntries();
        if (file.Header.EntryCount != file.Entries.Count)
            Assert.Fail($"Expected {file.Header.EntryCount} entries, got {file.Entries.Count}.");

        byte[] decrypted = (byte[])DecryptDataToArrayMethod.Invoke(file, [file.Entries[0]])!;
        if (decrypted.Length == 0)
            Assert.Fail("Expected the first bundled MS pack entry to decrypt to data.");
    }

    private static PackCase ResolvePackCase()
    {
        if (!Directory.Exists(PackDirectory))
            Assert.Fail($"Bundled MS pack directory not found: {PackDirectory}");

        foreach (string candidate in Directory.EnumerateFiles(PackDirectory, "*.ms").OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (string originalFileName in GetOriginalFileNameCandidates(candidate))
            {
                if (CanReadHeader(candidate, originalFileName))
                    return new PackCase(candidate, originalFileName);
            }
        }

        Assert.Fail($"No readable bundled MS pack found in: {PackDirectory}");
        throw new UnreachableException();
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

    private sealed record PackCase(string FilePath, string OriginalFileName);
}
