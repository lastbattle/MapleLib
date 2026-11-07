using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzUolPropertyTests
{
    [Fact]
    public void SetValue_AcceptsEditorValueShapes()
    {
        var link = new WzUOLProperty("link", "../a");

        link.SetValue("b");
        Assert.Equal("b", link.Value);

        link.SetValue(new WzStringProperty("x", "c"));
        Assert.Equal("c", link.Value);

        link.SetValue(42);
        Assert.Equal("42", link.Value);

        link.SetValue(null!);
        Assert.Null(link.Value);
    }
}
