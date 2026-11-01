using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzCanvasLinkTypeTests
{
    [Fact]
    public void CanvasLinkResolution_IgnoresNonStringLinkProperties()
    {
        var image = new WzImage("links.img") { Changed = true };
        var hostile = new WzCanvasProperty("hostile");
        hostile.AddProperty(new WzIntProperty("_inlink", 1));
        hostile.AddProperty(new WzIntProperty("_outlink", 2));
        image.AddProperty(hostile);

        // A crafted canvas that stores its links with a non-string property
        // type must resolve to itself instead of raising InvalidCastException
        // from the unchecked casts in GetLinkedWzImageProperty.
        Assert.Same(hostile, hostile.GetLinkedWzImageProperty());
    }

    [Fact]
    public void CanvasLinkResolution_StillFollowsStringInlink()
    {
        var image = new WzImage("links.img") { Changed = true };
        var target = new WzCanvasProperty("target");
        var linked = new WzCanvasProperty("linked");
        linked.AddProperty(new WzStringProperty("_inlink", "target"));
        image.AddProperty(target);
        image.AddProperty(linked);
        linked.Parent = image;

        Assert.Same(target, linked.GetLinkedWzImageProperty());
    }
}
