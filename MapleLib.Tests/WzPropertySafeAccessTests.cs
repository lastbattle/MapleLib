using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

/// <summary>
/// Locks the safe-access contract on WzImageProperty: scalar nodes and
/// unresolvable links report no child view instead of throwing, and the
/// payload accessor used by export paths never returns null.
/// </summary>
public sealed class WzPropertySafeAccessTests
{
    [Fact]
    public void GetChildOrDefaultOnScalarReportsNoChild()
    {
        var scalar = new WzIntProperty("count", 3);

        Assert.Null(scalar.GetChildOrDefault("count"));
        Assert.Null(scalar.GetChildrenOrDefault());
    }

    [Fact]
    public void GetChildOrDefaultReadsContainerChildren()
    {
        var container = new WzSubProperty("node");
        var child = new WzIntProperty("hp", 5);
        container.AddProperty(child);

        Assert.Same(child, container.GetChildOrDefault("hp"));
        Assert.Null(container.GetChildOrDefault("missing"));
        Assert.Same(container.WzProperties, container.GetChildrenOrDefault());
    }

    [Fact]
    public void GetChildOrDefaultTreatsUnresolvableLinkAsNoChild()
    {
        var link = new WzUOLProperty("link", "missing/target");

        Assert.Null(link.GetChildOrDefault("x"));
        Assert.Null(link.GetChildrenOrDefault());
    }

    [Fact]
    public void GetBytesOrEmptyNeverReturnsNull()
    {
        var raw = new WzRawDataProperty("raw", 7, new byte[] { 1 });
        raw.ReplaceBytes(null!);
        Assert.NotNull(raw.GetBytesOrEmpty());
        Assert.Empty(raw.GetBytesOrEmpty());

        var video = new WzVideoProperty("video", 4, new byte[] { 1 });
        video.ReplaceBytes(null!);
        Assert.NotNull(video.GetBytesOrEmpty());
        Assert.Empty(video.GetBytesOrEmpty());

        Assert.Empty(new WzIntProperty("scalar", 1).GetBytesOrEmpty());
    }
}
