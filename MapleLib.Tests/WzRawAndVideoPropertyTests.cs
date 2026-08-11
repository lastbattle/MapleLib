using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzRawAndVideoPropertyTests
{
    [Fact]
    public void RawDataConstructorAndReplacementOwnTheirPayloads()
    {
        byte[] constructorPayload = [1, 2, 3];
        var property = new WzRawDataProperty("raw", 7, constructorPayload);
        constructorPayload[0] = 99;

        Assert.Equal((byte)7, property.RawType);
        Assert.Equal([1, 2, 3], property.GetBytes(false));

        byte[] replacementPayload = [4, 5];
        property.ReplaceBytes(replacementPayload);
        replacementPayload[0] = 99;

        Assert.Equal([4, 5], property.GetBytes(false));
        property.ReplaceBytes(null!);
        Assert.Empty(property.GetBytes(false));
    }

    [Fact]
    public void VideoConstructorAndReplacementOwnTheirPayloads()
    {
        byte[] constructorPayload = [6, 7, 8];
        var property = new WzVideoProperty("video", 4, constructorPayload);
        constructorPayload[0] = 99;

        Assert.Equal(4, property.VideoType);
        Assert.Equal([6, 7, 8], property.GetBytes(false));

        byte[] replacementPayload = [9, 10];
        property.ReplaceBytes(replacementPayload);
        replacementPayload[0] = 99;

        Assert.Equal([9, 10], property.GetBytes(false));
        property.ReplaceBytes(null!);
        Assert.Empty(property.GetBytes(false));
    }
}
