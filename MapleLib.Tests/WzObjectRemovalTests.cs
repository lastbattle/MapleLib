using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzObjectRemovalTests
{
    [Fact]
    public void Remove_IsNoOpForDetachedNodes()
    {
        Assert.Null(Record.Exception(() => new WzIntProperty("a", 1).DeepClone().Remove()));
        Assert.Null(Record.Exception(() => new WzDirectory("loose").Remove()));

        using var file = new WzFile(1, WzMapleVersion.BMS) { Name = "test.wz" };
        Assert.Null(Record.Exception(() => file.WzDirectory.Remove()));
    }

    [Fact]
    public void Remove_StillDetachesAttachedNodes()
    {
        using var file = new WzFile(1, WzMapleVersion.BMS) { Name = "test.wz" };
        var image = new WzImage("a.img");
        file.WzDirectory.AddImage(image);
        var property = new WzIntProperty("a", 1);
        image.AddProperty(property);
        var directory = new WzDirectory("nested");
        file.WzDirectory.AddDirectory(directory);

        property.Remove();
        directory.Remove();

        Assert.DoesNotContain(property, image.WzProperties);
        Assert.DoesNotContain(directory, file.WzDirectory.WzDirectories);
    }
}
