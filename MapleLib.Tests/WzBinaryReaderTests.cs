using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace MapleLib.Tests;

[TestClass]
public class WzBinaryReaderTests
{
    [TestMethod]
    public void ReadString_RoundTripsAsciiAndUnicodeAtLengthMarkers()
    {
        foreach (int length in new[] { 0, 1, 126, 127, 128, 4096 })
        {
            AssertRoundTrip(CreateAscii(length), WzAESConstant.WZ_BMSCLASSIC);
            AssertRoundTrip(CreateUnicode(length), WzAESConstant.WZ_GMSIV);
        }
    }

    [TestMethod]
    public void ReadString_ConsumesExactlyEncodedPayload()
    {
        const string value = "MapleStory";
        byte[] encoded = Encode(value, WzAESConstant.WZ_BMSCLASSIC);
        using var stream = new MemoryStream(encoded, writable: false);
        using var reader = new WzBinaryReader(stream, WzAESConstant.WZ_BMSCLASSIC);

        Assert.AreEqual(value, reader.ReadString());
        Assert.AreEqual(encoded.Length, stream.Position);
    }

    [TestMethod]
    public void ReadStringAtOffset_RestoresPositionAfterBulkDecode()
    {
        byte[] first = Encode("first", WzAESConstant.WZ_BMSCLASSIC);
        byte[] second = Encode(CreateUnicode(512), WzAESConstant.WZ_BMSCLASSIC);
        byte[] combined = new byte[first.Length + second.Length];
        first.CopyTo(combined, 0);
        second.CopyTo(combined, first.Length);

        using var stream = new MemoryStream(combined, writable: false);
        using var reader = new WzBinaryReader(stream, WzAESConstant.WZ_BMSCLASSIC);
        stream.Position = 1;

        string value = reader.ReadStringAtOffset(first.Length);

        Assert.AreEqual(CreateUnicode(512), value);
        Assert.AreEqual(1, stream.Position);
    }

    [TestMethod]
    public void ReadString_ThrowsEndOfStreamForTruncatedAsciiAndUnicode()
    {
        foreach (string value in new[] { CreateAscii(128), CreateUnicode(128) })
        {
            byte[] encoded = Encode(value, WzAESConstant.WZ_BMSCLASSIC);
            Array.Resize(ref encoded, encoded.Length - 1);
            using var stream = new MemoryStream(encoded, writable: false);
            using var reader = new WzBinaryReader(stream, WzAESConstant.WZ_BMSCLASSIC);

            Assert.Throws<EndOfStreamException>(() => reader.ReadString());
        }
    }

    private static void AssertRoundTrip(string expected, byte[] iv)
    {
        byte[] encoded = Encode(expected, iv);
        using var reader = new WzBinaryReader(new MemoryStream(encoded, writable: false), iv);
        Assert.AreEqual(expected, reader.ReadString());
    }

    private static byte[] Encode(string value, byte[] iv)
    {
        using var stream = new MemoryStream();
        using (var writer = new WzBinaryWriter(stream, iv, leaveOpen: true))
            writer.Write(value);
        return stream.ToArray();
    }

    private static string CreateAscii(int length)
    {
        if (length == 0)
            return string.Empty;

        return string.Create(length, 0, static (chars, _) =>
        {
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)('A' + (i % 26));
        });
    }

    private static string CreateUnicode(int length)
    {
        if (length == 0)
            return string.Empty;

        return string.Create(length, 0, static (chars, _) =>
        {
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)('\u0100' + (i % 64));
        });
    }
}
