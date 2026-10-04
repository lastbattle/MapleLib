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

    [Fact]
    public void ContainerGetFromPathPreservesNestedAndEmptySegmentSemantics()
    {
        var sub = new WzSubProperty("root");
        var child = new WzSubProperty("child");
        var value = new WzIntProperty("value", 7);
        child.AddProperty(value);
        sub.AddProperty(child);

        Assert.Same(value, sub.GetFromPath("child//value///"));
        Assert.Null(sub.GetFromPath("child/missing"));
        Assert.Null(sub.GetFromPath("///"));

        var convex = new WzConvexProperty("root");
        var convexChild = new WzSubProperty("child");
        var convexValue = new WzIntProperty("value", 9);
        convexChild.AddProperty(convexValue);
        convex.AddProperty(convexChild);

        Assert.Same(convexValue, convex.GetFromPath("child//value///"));
        Assert.Null(convex.GetFromPath("child/missing"));
    }
}
