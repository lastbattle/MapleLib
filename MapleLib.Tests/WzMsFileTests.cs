using MapleLib.WzLib.MSFile;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MapleLib.Tests;

[TestClass]
public class WzMsFileTests
{
    private static readonly string PackDirectory = Path.Combine(AppContext.BaseDirectory, "WzFiles", "Ms", "Packs");
    private static readonly MethodInfo DecryptDataToArrayMethod = typeof(WzMsFile).GetMethod("DecryptDataToArray", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(WzMsFile), "DecryptDataToArray");

    [TestMethod]
    public void HeaderUpdateAppliesNewEntryCount()
    {
        var header = new WzMsHeader("test.ms", "salt", "test.mssalt", 1, 2, 3, 4, 5);

        header.UpdateHeader(10, 11, 12, 13, 14);

        Assert.AreEqual(10, header.Hash);
        Assert.AreEqual(11, header.EntryCount);
        Assert.AreEqual(12, header.HeaderStartPosition);
        Assert.AreEqual(13, header.EntryStartPosition);
        Assert.AreEqual(14, header.DataStartPosition);
    }

    [TestMethod]
    public void LeaveOpenCloseDoesNotDisposeCallerStream()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        using (var file = new WzMsFile(stream, "test.ms", "test.ms", leaveOpen: true, isSavingFile: true))
        {
            file.Close();
        }

        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public void Version4EntryCountIsHardBoundedBeforeCapacityGrowth()
    {
        using var stream = new MemoryStream();
        using var file = new WzMsFile(stream, "test.ms", "test.ms", leaveOpen: true, isSavingFile: true);
        typeof(WzMsFile).GetProperty(nameof(WzMsFile.Header), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(file, new WzMsHeader("test.ms", "", "test.ms", 0, WzMsConstants.Version4, 100_001, 0, 0));

        Assert.Throws<InvalidDataException>(() => file.ReadEntries());
    }

    [TestMethod]
    public void Version4ReaderRejectsOversizedEntryNameBeforeRentingBuffer()
    {
        byte[] encryptedTable = EncryptVersion4Table(BitConverter.GetBytes(int.MaxValue), "test.ms");
        using var stream = new MemoryStream(encryptedTable);
        using var file = new WzMsFile(stream, "test.ms", "test.ms", leaveOpen: true, isSavingFile: true);
        typeof(WzMsFile).GetProperty(nameof(WzMsFile.Header), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(file, new WzMsHeader("test.ms", "", "test.ms", 0, WzMsConstants.Version4, 1, 0, 0));

        Assert.Throws<InvalidDataException>(() => file.ReadEntries());
    }

    [TestMethod]
    public void Version4ReaderDefersDataBoundsUntilEntryRead()
    {
        byte[] encryptedTable = BuildEncryptedVersion4EntryTable(startPos: 1, size: 1, sizeAligned: 1);
        using var stream = new MemoryStream(encryptedTable);
        using var file = new WzMsFile(stream, "test.ms", "test.ms", leaveOpen: true, isSavingFile: true);
        typeof(WzMsFile).GetProperty(nameof(WzMsFile.Header), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(file, new WzMsHeader("test.ms", "", "test.ms", 0, WzMsConstants.Version4, 1, 0, 0));

        // Version 4 metadata tables can describe data blocks that are not
        // included in metadata-only/truncated packs. Parsing the table itself
        // remains valid; bounds are enforced when that entry is selected.
        file.ReadEntries();
        Assert.HasCount(1, file.Entries);

        var exception = Assert.Throws<TargetInvocationException>(
            () => DecryptDataToArrayMethod.Invoke(file, [file.Entries[0]]));
        Assert.IsInstanceOfType<InvalidDataException>(exception.InnerException);
    }

    [TestMethod]
    public void EntryRecalculationRejectsMalformedEntryKeyLength()
    {
        var entry = new WzMsEntry("test.img", 0, 0, 0, 0, 0, 0, 0, [1, 2, 3]);
        entry.Data = [1];

        Assert.Throws<InvalidDataException>(() => entry.RecalculateFields(0, 0, 0));
    }

    private static byte[] EncryptVersion4Table(byte[] prefix, string fileNameWithSalt)
    {
        byte[] plaintext = new byte[64];
        prefix.CopyTo(plaintext, 0);
        byte[] obscure =
        [
            0x7B, 0x2F, 0x35, 0x48, 0x43, 0x95, 0x02, 0xB9,
            0xAE, 0x91, 0xA6, 0xE1, 0xD8, 0xD6, 0x24, 0xB4,
            0x33, 0x10, 0x1D, 0x3D, 0xC1, 0xBB, 0xC6, 0xF4,
            0xA5, 0xFE, 0xB3, 0x69, 0x6B, 0x56, 0xE4, 0x75
        ];
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(i + (i % 3 + 2) * fileNameWithSalt[fileNameWithSalt.Length - 1 - i % fileNameWithSalt.Length]);
            key[i] ^= obscure[i];
        }

        using var transform = new ChaCha20CryptoTransform(key, new byte[12], 0);
        transform.TransformInPlace(plaintext);
        return plaintext;
    }

    private static byte[] BuildEncryptedVersion4EntryTable(int startPos, int size, int sizeAligned)
    {
        byte[] plaintext = new byte[1024];
        using (var stream = new MemoryStream(plaintext, writable: true))
        using (var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true))
        {
            writer.Write(1); // UTF-16 entry name length
            writer.Write('x');
            writer.Write(0); // checksum
            writer.Write(0); // flags
            writer.Write(startPos);
            writer.Write(size);
            writer.Write(sizeAligned);
            writer.Write(0); // unk1
            writer.Write(0); // unk2
            writer.Write(new byte[WzMsConstants.EntryKeySize]);
            writer.Write(0); // unk3
            writer.Write(0); // unk4
        }

        byte[] obscure =
        [
            0x7B, 0x2F, 0x35, 0x48, 0x43, 0x95, 0x02, 0xB9,
            0xAE, 0x91, 0xA6, 0xE1, 0xD8, 0xD6, 0x24, 0xB4,
            0x33, 0x10, 0x1D, 0x3D, 0xC1, 0xBB, 0xC6, 0xF4,
            0xA5, 0xFE, 0xB3, 0x69, 0x6B, 0x56, 0xE4, 0x75
        ];
        byte[] key = new byte[32];
        const string fileNameWithSalt = "test.ms";
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(i + (i % 3 + 2) * fileNameWithSalt[fileNameWithSalt.Length - 1 - i % fileNameWithSalt.Length]);
            key[i] ^= obscure[i];
        }

        using var transform = new ChaCha20CryptoTransform(key, new byte[12], 0);
        transform.TransformInPlace(plaintext);
        return plaintext;
    }

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
