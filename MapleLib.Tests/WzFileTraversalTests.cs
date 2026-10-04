using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzFileTraversalTests
{
    [Fact]
    public void GetObjectsFromDirectoryPreservesImageThenDirectoryOrder()
    {
        using var file = new WzFile(0, WzMapleVersion.GMS);
        var rootImage = new WzImage("root.img");
        rootImage.WzProperties.Add(new WzIntProperty("rootValue", 1));
        var child = new WzDirectory("child");
        var childImage = new WzImage("child.img");
        childImage.WzProperties.Add(new WzIntProperty("childValue", 2));
        child.AddImage(childImage);
        file.WzDirectory.AddImage(rootImage);
        file.WzDirectory.AddDirectory(child);

        var objects = file.GetObjectsFromDirectory(file.WzDirectory);

        Assert.Collection(objects,
            first => Assert.Equal("rootValue", first.Name),
            second => Assert.Equal("childValue", second.Name));
    }
}
