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
            foreach (byte[] iv in new[] { WzAESConstant.WZ_BMSCLASSIC, WzAESConstant.WZ_GMSIV })
                AssertRoundTrip(CreateUnicode(length), iv);
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
    public void ReadString_RejectsTruncatedAsciiAndUnicode()
    {
        foreach (string value in new[] { CreateAscii(128), CreateUnicode(128) })
        {
            byte[] encoded = Encode(value, WzAESConstant.WZ_BMSCLASSIC);
            Array.Resize(ref encoded, encoded.Length - 1);
            using var stream = new MemoryStream(encoded, writable: false);
            using var reader = new WzBinaryReader(stream, WzAESConstant.WZ_BMSCLASSIC);

            Assert.Throws<InvalidDataException>(() => reader.ReadString());
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
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bool unicode = value.Any(character => character > 127);
            if (value.Length == 0)
                writer.Write((sbyte)0);
            else if (unicode)
            {
                writer.Write(value.Length >= 127 ? (sbyte)127 : (sbyte)value.Length);
                if (value.Length >= 127)
                    writer.Write(value.Length);
                WzMutableKey key = WzKeyGenerator.GenerateWzKey(iv);
                for (int i = 0; i < value.Length; i++)
                    writer.Write((ushort)(value[i] ^ (ushort)(0xAAAA + i) ^
                        (ushort)(key[i * 2] | key[i * 2 + 1] << 8)));
            }
            else
            {
                writer.Write(value.Length >= 128 ? (sbyte)-128 : (sbyte)-value.Length);
                if (value.Length >= 128)
                    writer.Write(value.Length);
                WzMutableKey key = WzKeyGenerator.GenerateWzKey(iv);
                for (int i = 0; i < value.Length; i++)
                    writer.Write((byte)(value[i] ^ (byte)(0xAA + i) ^ key[i]));
            }
        }
        return stream.ToArray();
    }

    [TestMethod]
    public void ReadCompressedInt_RejectsTruncatedExtendedProbe()
    {
        // The 0x80 marker promotes the read to a four byte value. Every
        // geometry ending inside the marker or its value must surface the
        // house InvalidDataException contract, mirroring the string probes.
        byte[][] geometries =
        [
            [],
            [0x80],
            [0x80, 0x01],
            [0x80, 0x01, 0x02],
        ];

        foreach (byte[] geometry in geometries)
        {
            using var stream = new MemoryStream(geometry, writable: false);
            using var reader = new WzBinaryReader(stream, WzAESConstant.WZ_BMSCLASSIC);

            Assert.Throws<InvalidDataException>(() => reader.ReadCompressedInt());
        }
    }

    [TestMethod]
    public void ReadLong_RejectsTruncatedExtendedProbe()
    {
        byte[][] geometries =
        [
            [],
            [0x80],
            [0x80, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06],
        ];

        foreach (byte[] geometry in geometries)
        {
            using var stream = new MemoryStream(geometry, writable: false);
            using var reader = new WzBinaryReader(stream, WzAESConstant.WZ_BMSCLASSIC);

            Assert.Throws<InvalidDataException>(() => reader.ReadLong());
        }
    }

    [TestMethod]
    public void ReadCompressedInt_AndReadLong_ParseExtendedFormAtExactBoundary()
    {
        byte[] intEncoded = [0x80, 0x44, 0x33, 0x22, 0x11];
        using (var intStream = new MemoryStream(intEncoded, writable: false))
        using (var intReader = new WzBinaryReader(intStream, WzAESConstant.WZ_BMSCLASSIC))
        {
            Assert.AreEqual(0x11223344, intReader.ReadCompressedInt());
            Assert.AreEqual(intEncoded.Length, intStream.Position);
        }

        byte[] longEncoded = [0x80, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11];
        using (var longStream = new MemoryStream(longEncoded, writable: false))
        using (var longReader = new WzBinaryReader(longStream, WzAESConstant.WZ_BMSCLASSIC))
        {
            Assert.AreEqual(0x1122334455667788L, longReader.ReadLong());
            Assert.AreEqual(longEncoded.Length, longStream.Position);
        }
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
                chars[i] = "Ā中\uD83D\uDE00A\uFFFF"[i % 6];
        });
    }
}
