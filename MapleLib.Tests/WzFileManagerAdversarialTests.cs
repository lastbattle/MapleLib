using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MapleLib.WzLib;
using MapleLib.WzLib.MSFile;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzFileManagerAdversarialTests
{
    [Fact]
    public void Dispose_ReleasesImagesAndMsFilesAndClearsRegistries()
    {
        var manager = new WzFileManager();
        using var imageStream = new MemoryStream([0x01, 0x02]);
        var image = new WzImage("test.img", imageStream, WzMapleVersion.BMS);
        var msStream = new MemoryStream([0x03, 0x04]);
        var msFile = new WzMsFile(msStream, "test.ms", "test.ms", leaveOpen: false, isSavingFile: true);

        try
        {
            GetPrivateDictionary<string, WzImage>(manager, "_wzImages")["test"] = image;
            GetPrivateDictionary<string, WzMsFile>(manager, "_msFiles")["test"] = msFile;

            manager.Dispose();

            Assert.Empty(manager.WzImagesList);
            Assert.False(msStream.CanRead);
            Assert.False(imageStream.CanRead);
        }
        finally
        {
            // Dispose is idempotent; this also cleans up if an assertion fails.
            manager.Dispose();
        }
    }

    [Fact]
    public void LoadWzFile_RejectsDuplicateLogicalNameWithoutReplacingOriginal()
    {
        using var manager = new WzFileManager();
        var first = new WzFile(0, WzMapleVersion.BMS);
        var second = new WzFile(0, WzMapleVersion.BMS);

        manager.LoadWzFile("Map.wz", first);
        Assert.Throws<InvalidOperationException>(() => manager.LoadWzFile("map", second));

        Assert.Same(first, manager.WzFileList[0]);
        second.Dispose();
    }

    private static Dictionary<TKey, TValue> GetPrivateDictionary<TKey, TValue>(WzFileManager manager, string fieldName)
        where TKey : notnull
    {
        return (Dictionary<TKey, TValue>)typeof(WzFileManager)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(manager)!;
    }
}
