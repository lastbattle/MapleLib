using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System;
using System.IO;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzExtendedBlockLegTests
{
    private const int PayloadLength = 0x30;

    [Fact]
    public void RawDataPayloadLegConfinedToDeclaredBlock()
    {
        byte[] image = ExtendedBlockImage("raw_leg", "RawData", writer =>
        {
            writer.Write((byte)0);
            writer.WriteCompressedInt(PayloadLength);
            WriteFiller(writer, PayloadLength);
        }, PayloadLength);

        InvalidDataException failure = Assert.Throws<InvalidDataException>(
            () => ParseEagerImage(image, "raw_leg_eager"));
        Assert.Contains("Raw data payload", failure.Message);
    }

    [Fact]
    public void VideoPayloadLegConfinedToDeclaredBlock()
    {
        byte[] image = ExtendedBlockImage("video_leg", "Canvas#Video", writer =>
        {
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.WriteCompressedInt(PayloadLength);
            WriteFiller(writer, PayloadLength);
        }, PayloadLength);

        InvalidDataException failure = Assert.Throws<InvalidDataException>(
            () => ParseEagerImage(image, "video_leg_eager"));
        Assert.Contains("Video data payload", failure.Message);
    }

    [Fact]
    public void RawDataDiscriminatorsConfinedToDeclaredBlock()
    {
        byte[] typeMissing = ExtendedBlockImage("raw_leg", "RawData", _ => { }, 0);
        InvalidDataException typeFailure = Assert.Throws<InvalidDataException>(
            () => ParseEagerImage(typeMissing, "raw_leg_type"));
        Assert.Contains("raw data type byte", typeFailure.Message);

        byte[] flagMissing = ExtendedBlockImage("raw_leg", "RawData", writer => writer.Write((byte)1), 0);
        InvalidDataException flagFailure = Assert.Throws<InvalidDataException>(
            () => ParseEagerImage(flagMissing, "raw_leg_flag"));
        Assert.Contains("raw data link flag", flagFailure.Message);
    }

    [Fact]
    public void VideoTypeByteConfinedToDeclaredBlock()
    {
        byte[] image = ExtendedBlockImage("video_leg", "Canvas#Video", writer =>
        {
            writer.Write((byte)0);
            writer.Write((byte)0);
        }, 0);

        InvalidDataException failure = Assert.Throws<InvalidDataException>(
            () => ParseEagerImage(image, "video_leg_type"));
        Assert.Contains("video type byte", failure.Message);
    }

    [Fact]
    public void LazyParseConfinesRawDataPayloadLeg()
    {
        byte[] image = ExtendedBlockImage("raw_leg", "RawData", writer =>
        {
            writer.Write((byte)0);
            writer.WriteCompressedInt(PayloadLength);
            WriteFiller(writer, PayloadLength);
        }, PayloadLength);

        WzImage lazy = new WzImgDeserializer(freeResources: false, calculateChecksum: false)
            .WzImageFromIMGBytes(image, WzMapleVersion.BMS, "raw_leg_lazy", freeResources: false);

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => { lazy.ParseImage(true); });
        Assert.Contains("Raw data payload", failure.Message);
    }

    [Fact]
    public void WellFormedLegsSurviveTheRoundTrip()
    {
        byte[] rawPayload = [0xCA, 0xFE, 0xBA, 0xBE];
        byte[] videoPayload = [0x11, 0x22, 0x33, 0x44];

        WzImage valid = new("extleg_valid") { Changed = true };
        valid.AddProperty(new WzRawDataProperty("raw_leg", 0, rawPayload));
        valid.AddProperty(new WzVideoProperty("video_leg", 5, videoPayload));

        byte[] validImageBytes;
        using (MemoryStream ms = new())
        {
            using WzBinaryWriter writer = new(ms, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
            valid.SaveImage(writer);
            validImageBytes = ms.ToArray();
        }

        WzImage parsed = new WzImgDeserializer(freeResources: true, calculateChecksum: false)
            .WzImageFromIMGBytes(validImageBytes, WzMapleVersion.BMS, "extleg_valid", freeResources: true);

        var parsedRawProperty = parsed.WzProperties
            .OfType<WzRawDataProperty>()
            .FirstOrDefault(property => string.Equals(property.Name, "raw_leg", StringComparison.Ordinal));
        var parsedVideoProperty = parsed.WzProperties
            .OfType<WzVideoProperty>()
            .FirstOrDefault(property => string.Equals(property.Name, "video_leg", StringComparison.Ordinal));

        Assert.NotNull(parsedRawProperty);
        Assert.Equal(rawPayload, parsedRawProperty.GetBytes(false));
        Assert.NotNull(parsedVideoProperty);
        Assert.Equal(5, parsedVideoProperty.VideoType);
        Assert.Equal(videoPayload, parsedVideoProperty.GetBytes(false));
    }

    private static void ParseEagerImage(byte[] imageBytes, string name) =>
        _ = new WzImgDeserializer(freeResources: true, calculateChecksum: false)
            .WzImageFromIMGBytes(imageBytes, WzMapleVersion.BMS, name, freeResources: true);

    private static byte[] ExtendedBlockImage(string entryName, string extName, Action<WzBinaryWriter> writeBodyAfterName, int declaredCutFromEnd)
    {
        MemoryStream stream = new();
        using (WzBinaryWriter writer = new(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS)))
        {
            writer.Write((byte)0x73); // image prologue: the property block follows inline
            writer.Write("Property");
            writer.Write((ushort)0);

            writer.Write((byte)0x01); // single entry
            writer.Write((byte)0x00); // the entry name follows inline
            writer.Write(entryName);

            writer.Write((byte)0x09); // extended property
            long lengthPosition = writer.BaseStream.Position;
            writer.Write(0);
            long bodyStart = writer.BaseStream.Position;

            writer.Write((byte)0x73); // WzImageHeaderByte_WithoutOffset: the name follows inline
            writer.Write(extName);
            writeBodyAfterName(writer);

            int bodyLength = (int)(writer.BaseStream.Position - bodyStart);
            writer.BaseStream.Position = lengthPosition;
            writer.Write(bodyLength - declaredCutFromEnd);
        }

        return stream.ToArray();
    }

    private static void WriteFiller(WzBinaryWriter writer, int count)
    {
        for (int i = 0; i < count; i++)
        {
            writer.Write((byte)(0x11 * (i + 1)));
        }
    }
}
