using MapleLib.WzLib.MSFile;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace MapleLib.Tests;

[TestClass]
public class WzMsFileTests
{
    private static readonly string PackDirectory = Path.Combine(AppContext.BaseDirectory, "WzFiles", "Ms", "Packs");
    private static readonly MethodInfo DecryptDataToArrayMethod = typeof(WzMsFile).GetMethod("DecryptDataToArray", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(WzMsFile), "DecryptDataToArray");

    [TestMethod]
    [DataRow("Mob_00000.ms", 2, 24, "Mob/0000000.img", 4096, "12F51875C1545AFAC5B3BD4B3FD5131A9ED9F50FF248B84B01986D0934716F68")]
    [DataRow("Skill_00006.ms", 4, 32, "Skill/422.img", 97538, "011ACBBA435F4818706C59560DCF7AC9AF039ABC11E340DD90F4A95EF2F7D398")]
    public void ReadsVersionedPackHeaderEntriesAndEntryData(
        string fileName,
        int expectedVersion,
        int expectedEntryCount,
        string expectedFirstEntryName,
        int expectedFirstEntrySize,
        string expectedSha256)
    {
        string filePath = Path.Combine(PackDirectory, fileName);
        Assert.IsTrue(File.Exists(filePath), $"Bundled MS pack not found: {filePath}");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        using var file = new WzMsFile(stream, fileName, filePath);

        Assert.AreEqual(expectedVersion, file.Header.Version);
        Assert.AreEqual(expectedEntryCount, file.Header.EntryCount);

        file.ReadEntries();

        Assert.HasCount(expectedEntryCount, file.Entries);
        Assert.AreEqual(expectedFirstEntryName, file.Entries[0].Name);
        Assert.AreEqual(expectedFirstEntrySize, file.Entries[0].Size);

        byte[] decrypted = (byte[])DecryptDataToArrayMethod.Invoke(file, [file.Entries[0]])!;
        Assert.HasCount(expectedFirstEntrySize, decrypted);
        Assert.AreEqual(expectedSha256, Convert.ToHexString(SHA256.HashData(decrypted)));
    }
}
