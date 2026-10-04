using MapleLib.WzLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzDirectoryCountTests
{
    [Fact]
    public void CountImages_IncludesImagesAtEveryDirectoryLevel()
    {
        var root = new WzDirectory("root");
        root.AddImage(new WzImage("root.img"));
        var child = new WzDirectory("child");
        child.AddImage(new WzImage("child.img"));
        var grandchild = new WzDirectory("grandchild");
        grandchild.AddImage(new WzImage("grandchild.img"));
        child.AddDirectory(grandchild);
        root.AddDirectory(child);

        Assert.Equal(3, root.CountImages());
        Assert.Equal(2, child.CountImages());
        Assert.Equal(1, grandchild.CountImages());
    }
}
