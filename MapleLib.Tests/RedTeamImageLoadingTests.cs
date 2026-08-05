using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.Serializer;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class RedTeamImageLoadingTests
{
    [Theory]
    [InlineData(WzPngFormat.Format1, 7, 5, 70)]
    [InlineData(WzPngFormat.Format2, 7, 5, 140)]
    [InlineData(WzPngFormat.Format3, 7, 5, 64)]
    [InlineData(WzPngFormat.Format257, 7, 5, 70)]
    [InlineData(WzPngFormat.Format513, 7, 5, 70)]
    [InlineData(WzPngFormat.Format517, 32, 16, 4)]
    [InlineData(WzPngFormat.Format1026, 7, 5, 64)]
    [InlineData(WzPngFormat.Format2050, 7, 5, 64)]
    [InlineData(WzPngFormat.Format4098, 7, 5, 64)]
    public void GetDecodedSize_PreservesFormatSpecificLayout(WzPngFormat format, int width, int height, int expected)
    {
        Assert.Equal(expected, format.GetDecodedSize(width, height));
    }

    [Fact]
    public void ParseImage_RejectsPropertyCountBeforeAllocatingCollection()
    {
        byte[] imageBytes = Concat(
            [WzImage.WzImageHeaderByte_WithoutOffset],
            EncodedAscii("Property"),
            [0, 0],
            CompressedInt(int.MaxValue));

        using var stream = new MemoryStream(imageBytes);
        var image = new WzImage("test.img", stream, WzMapleVersion.BMS);

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
    }

    [Fact]
    public void ParseImage_RejectsLuaLengthBeyondRemainingData()
    {
        byte[] imageBytes = Concat([0x01], CompressedInt(int.MaxValue), [1, 2, 3, 4]);
        using var stream = new MemoryStream(imageBytes);
        var image = new WzImage("script.lua", stream, WzMapleVersion.BMS);

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
    }

    [Fact]
    public void ParseImage_RejectsExcessiveNestedProperties()
    {
        byte[] nestedList = [0x00];
        for (int i = 0; i < 130; i++)
        {
            byte[] extendedValue = Concat(
                [WzImage.WzImageHeaderByte_WithoutOffset],
                EncodedAscii("Property"),
                [0, 0],
                nestedList);
            nestedList = Concat(
                [0x01],
                NameBlock("p"),
                [0x09],
                BitConverter.GetBytes((uint)extendedValue.Length),
                extendedValue);
        }

        byte[] imageBytes = Concat(
            [WzImage.WzImageHeaderByte_WithoutOffset],
            EncodedAscii("Property"),
            [0, 0],
            nestedList);
        using var stream = new MemoryStream(imageBytes);
        var image = new WzImage("test.img", stream, WzMapleVersion.BMS);

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
    }

    [Fact]
    public void CyclicUolLinks_AreRejectedByLinkAndTypedResolution()
    {
        var image = new WzImage("test.img");
        var first = new WzUOLProperty("A", "B");
        var second = new WzUOLProperty("B", "A");
        image.AddProperty(first);
        image.AddProperty(second);

        Assert.Throws<InvalidDataException>(() => first.GetLinkedWzImageProperty());
        Assert.Throws<InvalidDataException>(() => first.GetString());
    }

    [Fact]
    public void BrokenUolLink_RemainsSafeToTraverse()
    {
        var image = new WzImage("test.img");
        var broken = new WzUOLProperty("broken", "missing");
        image.AddProperty(broken);

        Assert.Null(broken.LinkValue);
        Assert.Null(broken.WzProperties);
        Assert.Null(broken["child"]);
        Assert.Null(broken.GetFromPath("child"));
        Assert.Same(broken, broken.GetLinkedWzImageProperty());
    }

    [Fact]
    public void DisposingDirectoryImage_DoesNotCloseSharedReader()
    {
        using var stream = new MemoryStream([0x2A]);
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
        var first = new WzImage("first.img", reader, checksum: 0);
        var second = new WzImage("second.img", reader, checksum: 0);

        first.Dispose();

        Assert.True(stream.CanRead);
        Assert.Equal(0x2A, reader.ReadByte());
        second.Dispose();
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void LegacySerializer_EscapesUolPropertyName()
    {
        var serializer = new TestSerializer();

        string xml = serializer.Serialize(new WzUOLProperty("\"x\"><injected>", "target"));

        Assert.Contains("name=\"&quot;x&quot;&gt;&lt;injected&gt;\"", xml);
        Assert.DoesNotContain("<injected>", xml);
    }

    private static byte[] NameBlock(string value) => Concat([0x00], EncodedAscii(value));

    private static byte[] EncodedAscii(string value)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(unchecked((byte)-value.Length));
        for (int i = 0; i < value.Length; i++)
            stream.WriteByte((byte)(value[i] ^ (byte)(0xAA + i)));
        return stream.ToArray();
    }

    private static byte[] CompressedInt(int value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        if (value is >= -127 and <= 127)
            writer.Write((sbyte)value);
        else
        {
            writer.Write(sbyte.MinValue);
            writer.Write(value);
        }
        return stream.ToArray();
    }

    private static byte[] Concat(params byte[][] parts)
    {
        int length = 0;
        foreach (byte[] part in parts)
            length = checked(length + part.Length);

        byte[] result = new byte[length];
        int offset = 0;
        foreach (byte[] part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }
        return result;
    }

    private sealed class TestSerializer : WzSerializer
    {
        public TestSerializer() : base(0, LineBreak.None)
        {
        }

        public string Serialize(WzImageProperty property)
        {
            using var writer = new StringWriter();
            WritePropertyToXML(writer, string.Empty, property, string.Empty);
            return writer.ToString();
        }
    }
}
