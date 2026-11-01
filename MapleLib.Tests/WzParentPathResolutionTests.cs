using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzParentPathResolutionTests
{
    private static WzImage CreateImage(
        out WzSubProperty data,
        out WzSubProperty deep,
        out WzConvexProperty convex,
        out WzCanvasProperty canvas)
    {
        WzImage image = new("paths.img") { Changed = true };

        data = new WzSubProperty("Data");
        data.AddProperty(new WzIntProperty("x", 5));
        deep = new WzSubProperty("Deep");
        deep.AddProperty(new WzIntProperty("y", 7));
        data.AddProperty(deep);
        image.AddProperty(data);
        data.Parent = image;
        deep.Parent = data;

        convex = new WzConvexProperty("Cv");
        convex.AddProperty(new WzSubProperty("pt"));
        image.AddProperty(convex);
        convex.Parent = image;

        canvas = new WzCanvasProperty("C");
        image.AddProperty(canvas);
        canvas.Parent = image;

        return image;
    }

    [Fact]
    public void ParentPath_FromRootLevelProperty_ResolvesThroughImage()
    {
        _ = CreateImage(out WzSubProperty data, out _, out _, out _);

        // A root-level property's Parent is the WzImage itself, not a
        // WzImageProperty; the former hard cast raised InvalidCastException
        // before the leading ".." segment was consumed.
        WzIntProperty x = Assert.IsType<WzIntProperty>(data.GetFromPath("../Data/x"));

        Assert.Equal(5, x.Value);
    }

    [Fact]
    public void ParentPath_BetweenProperties_ConsumesDotDotSegment()
    {
        _ = CreateImage(out _, out WzSubProperty deep, out _, out _);

        // The former substring keyed on the property name, so the ".." was
        // never stripped and the parent indexer searched a literal "../x"
        // child instead of resolving x under the parent.
        WzIntProperty x = Assert.IsType<WzIntProperty>(deep.GetFromPath("../x"));

        Assert.Equal(5, x.Value);
    }

    [Fact]
    public void ParentPath_BareDotDot_ReturnsParentProperty()
    {
        _ = CreateImage(out WzSubProperty data, out WzSubProperty deep, out _, out _);

        Assert.Same(data, deep.GetFromPath(".."));
    }

    [Fact]
    public void ParentPath_FromRootLevelConvex_ResolvesThroughImage()
    {
        _ = CreateImage(out _, out _, out WzConvexProperty convex, out _);

        // Convex children are extended properties only, so the probe target
        // is a nested sub property.
        Assert.IsType<WzSubProperty>(convex.GetFromPath("../Cv/pt"));
    }

    [Fact]
    public void ParentPath_FromRootLevelCanvas_ResolvesThroughImage()
    {
        _ = CreateImage(out _, out _, out _, out WzCanvasProperty canvas);

        Assert.Same(canvas, canvas.GetFromPath("../C"));
    }

    [Fact]
    public void ParentPath_MultiDotChain_CrossesImageBoundary()
    {
        _ = CreateImage(out _, out WzSubProperty deep, out _, out _);

        WzIntProperty x = Assert.IsType<WzIntProperty>(deep.GetFromPath("../../Data/x"));

        Assert.Equal(5, x.Value);
    }
}
