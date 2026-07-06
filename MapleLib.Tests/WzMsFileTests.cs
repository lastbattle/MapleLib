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
    public void ChaCha20TransformMatchesRfc8439Vector()
    {
        byte[] key =
        [
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
            0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
            0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F
        ];
        byte[] nonce =
        [
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x4A,
            0x00, 0x00, 0x00, 0x00
        ];
        byte[] plaintext =
        [
            0x4C, 0x61, 0x64, 0x69, 0x65, 0x73, 0x20, 0x61, 0x6E, 0x64, 0x20, 0x47, 0x65, 0x6E, 0x74, 0x6C,
            0x65, 0x6D, 0x65, 0x6E, 0x20, 0x6F, 0x66, 0x20, 0x74, 0x68, 0x65, 0x20, 0x63, 0x6C, 0x61, 0x73,
            0x73, 0x20, 0x6F, 0x66, 0x20, 0x27, 0x39, 0x39, 0x3A, 0x20, 0x49, 0x66, 0x20, 0x49, 0x20, 0x63,
            0x6F, 0x75, 0x6C, 0x64, 0x20, 0x6F, 0x66, 0x66, 0x65, 0x72, 0x20, 0x79, 0x6F, 0x75, 0x20, 0x6F,
            0x6E, 0x6C, 0x79, 0x20, 0x6F, 0x6E, 0x65, 0x20, 0x74, 0x69, 0x70, 0x20, 0x66, 0x6F, 0x72, 0x20,
            0x74, 0x68, 0x65, 0x20, 0x66, 0x75, 0x74, 0x75, 0x72, 0x65, 0x2C, 0x20, 0x73, 0x75, 0x6E, 0x73,
            0x63, 0x72, 0x65, 0x65, 0x6E, 0x20, 0x77, 0x6F, 0x75, 0x6C, 0x64, 0x20, 0x62, 0x65, 0x20, 0x69,
            0x74, 0x2E
        ];
        string expectedCiphertext =
            "6E2E359A2568F98041BA0728DD0D6981E97E7AEC1D4360C20A27AFCCFD9FAE0BF91B65C5524733AB8F593DABCD62B3571639D624E65152AB8F530C359F0861D807CA0DBF500D6A6156A38E088A22B65E52BC514D16CCF806818CE91AB77937365AF90BBF74A35BE6B40B8EEDF2785E42874D";

        using var transform = new ChaCha20CryptoTransform(key, nonce, 1);
        transform.TransformInPlace(plaintext);

        Assert.AreEqual(expectedCiphertext, Convert.ToHexString(plaintext));
    }

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
