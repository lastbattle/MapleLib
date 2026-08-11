using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzPropertyPathTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("///")]
    public void ContainerGetFromPathReturnsNullForEmptyPaths(string? path)
    {
#pragma warning disable CS8604 // Deliberately exercises the runtime guard for legacy callers.
        Assert.Null(new WzSubProperty("root").GetFromPath(path!));
        Assert.Null(new WzConvexProperty("root").GetFromPath(path!));
#pragma warning restore CS8604
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../../../missing")]
    public void BrokenUolResolutionReturnsNullWithoutDereferencingMissingSegments(string? value)
    {
        var image = new WzImage("test.img");
        var link = new WzUOLProperty("link", value!);
        image.AddProperty(link);

        Assert.Null(link.LinkValue);
        Assert.Null(link.WzProperties);
    }
}
