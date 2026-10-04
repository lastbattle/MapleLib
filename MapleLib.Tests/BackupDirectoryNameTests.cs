using MapleLib.Img;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public class BackupDirectoryNameTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Backups", true)]
    [InlineData("backups", true)]
    [InlineData(@"C:\", false)]
    [InlineData("/", false)]
    [InlineData(@"C:\BACKUPS/\///", true)]
    [InlineData("C:Backups", true)]
    [InlineData(@"C:\Backups\child", false)]
    [InlineData(@"\\server\share\Backups\", true)]
    [InlineData("Backups.", false)]
    [InlineData("Backups ", false)]
    public void FinalSegmentMatchingPreservesPathRules(string? path, bool expected)
    {
        Assert.Equal(expected, HaCreatorPaths.IsBackupsDirectory(path!));
    }
}
