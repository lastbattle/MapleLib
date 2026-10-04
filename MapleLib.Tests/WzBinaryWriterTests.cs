using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace MapleLib.Tests;

[TestClass]
public class WzBinaryWriterTests
{
    [TestMethod]
    [DataRow("ASCII", (byte)0xFB)]
    [DataRow("漢", (byte)0x01)]
    public void PublicStringWritePreservesFlushOverrideBeforePayload(string value, byte expectedMarker)
    {
        using var stream = new MemoryStream();
        using var writer = new RejectingFlushWriter(stream);

        Xunit.Assert.Throws<IOException>(() => writer.Write(value));

        CollectionAssert.AreEqual(new[] { expectedMarker }, stream.ToArray());
    }

    private sealed class RejectingFlushWriter(Stream stream) : WzBinaryWriter(stream, new byte[4])
    {
        public override void Flush() => throw new IOException("Flush rejected.");
    }

    [TestMethod]
    public void EncodedStringLengthPreservesUnicodeAndPrefixBoundaries()
    {
        Assert.AreEqual(1, WzTool.GetEncodedStringLength(string.Empty));
        Assert.AreEqual(4, WzTool.GetEncodedStringLength("ABC"));
        Assert.AreEqual(9, WzTool.GetEncodedStringLength("ĀABC"));
        Assert.AreEqual(128, WzTool.GetEncodedStringLength(new string('A', 127)));
        Assert.AreEqual(261, WzTool.GetEncodedStringLength(new string('Ā', 128)));
    }

    [TestMethod]
    public void EncryptHelpersPreserveNullException()
    {
        using var stream = new MemoryStream();
        using var writer = new WzBinaryWriter(stream, WzAESConstant.WZ_BMSCLASSIC);
        Assert.AreEqual("source", Xunit.Assert.Throws<ArgumentNullException>(() => writer.EncryptString(null!)).ParamName);
        Assert.AreEqual("source", Xunit.Assert.Throws<ArgumentNullException>(() => writer.EncryptNonUnicodeString(null!)).ParamName);
    }

    [TestMethod]
    public void ObjectCacheRetainsFirstOffsetAndSeparatesTypes()
    {
        using var stream = new MemoryStream();
        using var writer = new WzBinaryWriter(stream, WzAESConstant.WZ_BMSCLASSIC, leaveOpen: true)
        { Header = new WzHeader { FStart = 0 } };
        var imageType = MapleLib.WzLib.WzStructure.Enums.WzDirectoryType.WzImage_4;
        var directoryType = MapleLib.WzLib.WzStructure.Enums.WzDirectoryType.WzDirectory_3;
        Assert.IsFalse(writer.WriteWzObjectValue("MapleStory", imageType));
        long offset = stream.Position;
        Assert.IsTrue(writer.WriteWzObjectValue("MapleStory", imageType));
        Assert.AreEqual(offset + 5, stream.Position);
        Assert.AreEqual(0, BitConverter.ToInt32(stream.ToArray(), (int)offset + 1));
        Assert.IsFalse(writer.WriteWzObjectValue("MapleStory", directoryType));
        Assert.AreEqual(0, writer.StringCache["4_MapleStory"]);
    }

    [TestMethod]
    public void WriteStringValue_WritesFullValueThenCachedOffset()
    {
        const string value = "MapleStory";
        using var stream = new MemoryStream();
        using (var writer = new WzBinaryWriter(stream, WzAESConstant.WZ_BMSCLASSIC, leaveOpen: true))
        {
            writer.WriteStringValue(value, WzImage.WzImageHeaderByte_WithoutOffset,
                WzImage.WzImageHeaderByte_WithOffset);
            long firstLength = stream.Position;

            writer.WriteStringValue(value, WzImage.WzImageHeaderByte_WithoutOffset,
                WzImage.WzImageHeaderByte_WithOffset);

            Assert.AreEqual(1, writer.StringCache[value]);
            Assert.AreEqual(firstLength + 5, stream.Position);
        }

        byte[] bytes = stream.ToArray();
        Assert.AreEqual((byte)WzImage.WzImageHeaderByte_WithoutOffset, bytes[0]);
        Assert.AreEqual((byte)WzImage.WzImageHeaderByte_WithOffset, bytes[^5]);
        Assert.AreEqual(1, BitConverter.ToInt32(bytes, bytes.Length - 4));
    }

    [TestMethod]
    public void WriteStringValue_PreservesExistingCachedOffset()
    {
        const string value = "MapleStory";
        using var stream = new MemoryStream();
        using var writer = new WzBinaryWriter(stream, WzAESConstant.WZ_BMSCLASSIC, leaveOpen: true);
        writer.StringCache[value] = 1234;

        writer.WriteStringValue(value, WzImage.WzImageHeaderByte_WithoutOffset,
            WzImage.WzImageHeaderByte_WithOffset);

        Assert.AreEqual(5, stream.Length);
        Assert.AreEqual((byte)WzImage.WzImageHeaderByte_WithOffset, stream.ToArray()[0]);
        Assert.AreEqual(1234, BitConverter.ToInt32(stream.ToArray(), 1));
    }

    [TestMethod]
    public void EncryptStringMethodsMatchKeyXorOracle()
    {
        const string value = "ĀMapleStory漢字";
        using var stream = new MemoryStream();
        using var writer = new WzBinaryWriter(stream, WzAESConstant.WZ_BMSCLASSIC, leaveOpen: true);

        char[] unicode = writer.EncryptString(value);
        char[] nonUnicode = writer.EncryptNonUnicodeString(value);

        for (int i = 0; i < value.Length; i++)
        {
            Assert.AreEqual((char)(value[i] ^ ((writer.WzKey[i * 2 + 1] << 8) + writer.WzKey[i * 2])), unicode[i]);
            Assert.AreEqual((char)(value[i] ^ writer.WzKey[i]), nonUnicode[i]);
        }
    }

    [TestMethod]
    public void UnicodeStringBatchEncodingMatchesPerCharacterEncoding()
    {
        foreach (byte[] iv in new[] { WzAESConstant.WZ_BMSCLASSIC, WzAESConstant.WZ_GMSIV })
        foreach (int length in new[] { 1, 126, 127, 128, 4096 })
        {
            const string codeUnits = "Ā中\uD83D\uDE00A\uFFFF";
            string value = string.Create(length, codeUnits, static (chars, units) =>
            {
                for (int index = 0; index < chars.Length; index++)
                    chars[index] = units[index % units.Length];
            });
            using var actualStream = new MemoryStream();
            using var expectedStream = new MemoryStream();
            using var actualWriter = new WzBinaryWriter(actualStream, iv, leaveOpen: true);
            using var expectedWriter = new WzBinaryWriter(expectedStream, iv, leaveOpen: true);

            actualWriter.Write(value);
            WriteUnicodePerCharacter(expectedWriter, value);

            CollectionAssert.AreEqual(expectedStream.ToArray(), actualStream.ToArray());
        }
    }

    private static void WriteUnicodePerCharacter(WzBinaryWriter writer, string value)
    {
        if (value.Length >= sbyte.MaxValue)
        {
            writer.Write(sbyte.MaxValue);
            writer.Write(value.Length);
        }
        else
        {
            writer.Write((sbyte)value.Length);
        }

        ushort mask = 0xAAAA;
        for (int i = 0; i < value.Length; i++)
        {
            ushort encrypted = value[i];
            encrypted ^= (ushort)((writer.WzKey[i * 2 + 1] << 8) + writer.WzKey[i * 2]);
            encrypted ^= mask++;
            writer.Write(encrypted);
        }
    }
}

