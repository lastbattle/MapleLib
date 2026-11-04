using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System;
using System.IO;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzStringBlockMarkerTests
{
    [Fact]
    public void MarkerByteAtStreamEndIsRejected()
    {
        using MemoryStream stream = new();
        using WzBinaryReader reader = new(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => reader.ReadStringBlock(0));
        Assert.Contains("marker", failure.Message);
    }

    [Fact]
    public void TruncatedStringOffsetIsRejected()
    {
        byte[] body = [0x01, 0x40];
        using MemoryStream stream = new(body);
        using WzBinaryReader reader = new(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => reader.ReadStringBlock(0));
        Assert.Contains("offset", failure.Message);
    }

    [Fact]
    public void UnknownMarkerStillYieldsEmptyString()
    {
        byte[] body = [0x42];
        using MemoryStream stream = new(body);
        using WzBinaryReader reader = new(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));

        Assert.Equal(string.Empty, reader.ReadStringBlock(0));
    }

    [Fact]
    public void TrailingMarkerAfterCompleteEntryIsRejectedThroughTheParsePath()
    {
        byte[] image = StringBlockImage();
        InvalidDataException failure = Assert.Throws<InvalidDataException>(
            () => _ = new WzImgDeserializer(freeResources: true, calculateChecksum: false)
                .WzImageFromIMGBytes(image, WzMapleVersion.BMS, "strblk_eager", freeResources: true));
        Assert.Contains("marker", failure.Message);
    }

    [Fact]
    public void WellFormedStringsSurviveTheRoundTrip()
    {
        const string sharedPayload = "sample payload text";

        WzImage valid = new("strblk_valid") { Changed = true };
        valid.AddProperty(new WzIntProperty("probe_int", 0x11223344));
        valid.AddProperty(new WzStringProperty("text", sharedPayload));
        valid.AddProperty(new WzStringProperty("text_copy", sharedPayload));

        byte[] validImageBytes;
        using (MemoryStream ms = new())
        {
            using WzBinaryWriter writer = new(ms, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
            valid.SaveImage(writer);
            validImageBytes = ms.ToArray();
        }

        WzImage parsed = new WzImgDeserializer(freeResources: true, calculateChecksum: false)
            .WzImageFromIMGBytes(validImageBytes, WzMapleVersion.BMS, "strblk_valid", freeResources: true);

        Assert.Equal(0x11223344, parsed.WzProperties
            .OfType<WzIntProperty>()
            .First(property => string.Equals(property.Name, "probe_int", StringComparison.Ordinal))
            .Value);
        Assert.Equal(sharedPayload, parsed.WzProperties
            .OfType<WzStringProperty>()
            .First(property => string.Equals(property.Name, "text", StringComparison.Ordinal))
            .Value);
        Assert.Equal(sharedPayload, parsed.WzProperties
            .OfType<WzStringProperty>()
            .First(property => string.Equals(property.Name, "text_copy", StringComparison.Ordinal))
            .Value);
    }

    /// <summary>
    /// A two entry property list whose second name marker lands on the exact
    /// end of the stream: the first entry is complete, the count is plausible
    /// under the two byte per entry floor, and only the marker read escapes.
    /// </summary>
    private static byte[] StringBlockImage()
    {
        MemoryStream stream = new();
        using (WzBinaryWriter writer = new(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS)))
        {
            writer.Write((byte)0x73); // image prologue: the property block follows inline
            writer.Write("Property");
            writer.Write((ushort)0);

            writer.Write((byte)2);    // entry count: plausible under the two byte floor
            writer.Write((byte)0x00); // entry one: inline name marker
            writer.Write((byte)0x00); // empty name
            writer.Write((byte)0x03); // int property
            writer.Write((byte)0x05); // compressed value
            // entry two's name marker is the next byte and the stream ends here
        }

        return stream.ToArray();
    }
}
