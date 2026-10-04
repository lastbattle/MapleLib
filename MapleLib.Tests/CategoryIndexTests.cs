using MapleLib.Img;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class CategoryIndexTests
{
    [Fact]
    public void SubdirectoryTotalImageCountTracksNestedImages()
    {
        var root = new SubdirectoryEntry();
        root.Images.Add(new ImageIndexEntry());
        var child = new SubdirectoryEntry();
        child.Images.Add(new ImageIndexEntry());
        root.Subdirectories.Add(child);

        Assert.Equal(2, root.TotalImageCount);

        child.Images.Add(new ImageIndexEntry());
        Assert.Equal(3, root.TotalImageCount);
    }
}
