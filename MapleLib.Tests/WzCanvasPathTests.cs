using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzCanvasPathTests
{
    [Fact]
    public void CanvasGetFromPathPreservesNestedEmptyAndMissingSegments()
    {
        var canvas = new WzCanvasProperty("canvas");
        var child = new WzSubProperty("child");
        var value = new WzIntProperty("value", 7);
        child.AddProperty(value);
        canvas.AddProperty(child);

        Assert.Same(value, canvas.GetFromPath("child//value///"));
        Assert.Same(value, canvas.GetFromPath("///child//value///"));
        Assert.Null(canvas.GetFromPath("Child/value"));
        Assert.Null(canvas.GetFromPath("child/missing"));
        Assert.Null(canvas.GetFromPath("child/value/missing"));
        Assert.Null(canvas.GetFromPath("///"));
        Assert.Null(canvas.GetFromPath(""));
    }

    [Fact]
    public void CanvasGetFromPathKeepsPngShortcut()
    {
        var canvas = new WzCanvasProperty("canvas");
        var png = new WzPngProperty();
        canvas["PNG"] = png;
        var nested = new WzCanvasProperty("child");
        nested["PNG"] = new WzPngProperty();
        canvas.AddProperty(nested);

        Assert.Same(png, canvas.GetFromPath("PNG"));
        Assert.Same(png, canvas.GetFromPath("///PNG///"));
        Assert.Same(png, canvas.GetFromPath("child/PNG/ignored"));
        Assert.Null(canvas.GetFromPath("child/png"));
    }

    [Fact]
    public void CanvasGetFromPathKeepsLegacyParentSubstring()
    {
        var parent = new WzSubProperty("parent");
        var canvas = new WzCanvasProperty("abc/def");
        var value = new WzIntProperty("ource", 1);
        parent.AddProperty(canvas);
        parent.AddProperty(value);

        // The historical fallback takes its substring offset from the canvas name.
        Assert.Same(value, canvas.GetFromPath("../source"));
    }

    [Fact]
    public void CanvasGetFromPathPreservesNullFailure()
    {
        var canvas = new WzCanvasProperty("canvas");

#pragma warning disable CS8604
        Assert.Throws<NullReferenceException>(() => canvas.GetFromPath(null!));
#pragma warning restore CS8604
    }
}
