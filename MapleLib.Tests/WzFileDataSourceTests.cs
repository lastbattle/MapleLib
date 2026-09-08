using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MapleLib.Img;
using MapleLib.WzLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzFileDataSourceTests
{
    [Fact]
    public void BorrowedManager_IsNotDisposedWithDataSource()
    {
        using var manager = new WzFileManager();
        manager.LoadWzFile("Map", CreateWzFile("Map", "100000.img"));
        using (var source = new WzFileDataSource(manager, ownsManager: false))
        {
            source.Dispose();
        }

        Assert.Single(manager.WzFileList);
    }

    [Fact]
    public void GetImage_SearchesEverySplitFile()
    {
        using var manager = new WzFileManager();
        var first = CreateWzFile("Mob", "100000.img");
        var second = CreateWzFile("Mob001", "200000.img");
        manager.LoadWzFile("Mob", first);
        manager.LoadWzFile("Mob001", second);
        SetSplitFileList(manager, "mob", "Mob", "Mob001");

        using var source = new WzFileDataSource(manager, ownsManager: false);

        Assert.Same(second.WzDirectory["200000.img"], source.GetImage("Mob", "200000.img"));
    }

    [Fact]
    public void GetImage_ResolvesNestedImagePath()
    {
        using var manager = new WzFileManager();
        var file = new WzFile(0, WzMapleVersion.BMS) { Name = "Character" };
        var face = new WzDirectory("Face");
        var image = new WzImage("00020000.img");
        face.AddImage(image);
        file.WzDirectory.AddDirectory(face);
        manager.LoadWzFile("Character", file);

        using var source = new WzFileDataSource(manager, ownsManager: false);

        Assert.Same(image, source.GetImage("Character", "Face/00020000.img"));
        Assert.Same(image, source.GetImageByPath("Character\\Face\\00020000.img"));
    }

    [Fact]
    public void GetImage_ResolvesNestedSplitFile()
    {
        using var manager = new WzFileManager();
        var file = CreateWzFile("Face_000", "00020000.img");
        manager.LoadWzFile("Character/Face/Face_000", file);
        SetSplitFileList(manager, "character/face", "Character/Face/Face_000");

        using var source = new WzFileDataSource(manager, ownsManager: false);

        Assert.Same(file.WzDirectory["00020000.img"],
            source.GetImage("Character", "Face/00020000.img"));
    }

    private static WzFile CreateWzFile(string name, params string[] imageNames)
    {
        var file = new WzFile(0, WzMapleVersion.BMS) { Name = name };
        foreach (string imageName in imageNames)
            file.WzDirectory.AddImage(new WzImage(imageName));
        return file;
    }

    private static void SetSplitFileList(WzFileManager manager, string baseName, params string[] files)
    {
        var field = typeof(WzFileManager).GetField("_wzFilesList", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var fileList = Assert.IsType<Dictionary<string, List<string>>>(field!.GetValue(manager));
        fileList[baseName] = new List<string>(files);
    }
}
