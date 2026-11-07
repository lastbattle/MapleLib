using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzImageAddPropertyTests
{
    [Fact]
    public void AddProperty_RejectsNullWithParameterName()
    {
        var image = new WzImage("a.img");
        var exception = Assert.Throws<ArgumentNullException>(() => image.AddProperty(null!));
        Assert.Equal("prop", exception.ParamName);
    }

    [Fact]
    public void AddProperty_RejectsDuplicateName()
    {
        var image = new WzImage("a.img");
        image.AddProperty(new WzIntProperty("a", 1));
        Assert.Throws<InvalidOperationException>(() => image.AddProperty(new WzIntProperty("a", 2)));
    }
}
