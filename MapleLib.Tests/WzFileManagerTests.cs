using MapleLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzFileManagerTests
{
    [Fact]
    public void GetMainDirectoryByName_ReturnsNullForUnknownArchive()
    {
        var manager = new WzFileManager();

        Assert.Null(manager.GetMainDirectoryByName("missing"));
        Assert.Null(manager.GetMainDirectoryByName(null!));
    }
}
