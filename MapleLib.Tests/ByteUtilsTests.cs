using MapleLib.Helpers;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class ByteUtilsTests
{
    [Fact]
    public void HexToBytes_ParsesCanonicalFormsAndWildcards()
    {
        Assert.Equal(new byte[] { 0x01, 0xAF, 0x00, 0xFF },
            ByteUtils.HexToBytes("0x01:AF, 00-ff"));

        byte[] wildcard = ByteUtils.HexToBytes("**");
        Assert.Single(wildcard);
        // The wildcard denotes any complete byte, including 0xFF.
        Assert.InRange(wildcard[0], byte.MinValue, byte.MaxValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0x")]
    [InlineData("A")]
    [InlineData("0x0")]
    [InlineData("GG")]
    [InlineData("0x12Z4")]
    [InlineData("*0")]
    [InlineData("0*")]
    public void HexToBytes_RejectsMalformedInput(string? value)
    {
        if (value is null)
        {
            Assert.Throws<ArgumentNullException>(() => ByteUtils.HexToBytes(value!));
        }
        else
        {
            Assert.Throws<FormatException>(() => ByteUtils.HexToBytes(value));
        }
    }

    [Fact]
    public void BytesToHex_RejectsNullAndFormatsBytes()
    {
        Assert.Throws<ArgumentNullException>(() => ByteUtils.BytesToHex(null!));
        Assert.Equal("prefix01 AF FF ", ByteUtils.BytesToHex([0x01, 0xAF, 0xFF], "prefix"));
    }

    [Fact]
    public void CompareBytearrays_RejectsNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() => ByteUtils.CompareBytearrays(null!, []));
        Assert.Throws<ArgumentNullException>(() => ByteUtils.CompareBytearrays([], null!));
    }
}
