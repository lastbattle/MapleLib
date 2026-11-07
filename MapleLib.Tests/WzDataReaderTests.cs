using MapleLib;
using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzDataReaderTests
{
    [Fact]
    public void NullStringValue_UsesFallbacks()
    {
        var empty = new WzStringProperty("empty", null!);

        Assert.Equal(77, empty.ReadValue(77));
        Assert.Equal("fallback", empty.ReadString("fallback"));
    }

    [Fact]
    public void PercentString_StillParses()
    {
        Assert.Equal(10, new WzStringProperty("rate", "10%").ReadValue(0));
    }
}
