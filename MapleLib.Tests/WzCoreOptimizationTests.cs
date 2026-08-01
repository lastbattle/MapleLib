using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace UnitTest_WzFile;

[TestClass]
[SupportedOSPlatform("windows")]
public class WzCoreOptimizationTests
{
    private const short CanvasEraPatchVersion = 260;

    [TestMethod]
    public void DirectoryAndManagerLookups_AreCaseInsensitive()
    {
        using var file = new WzFile(95, WzMapleVersion.GMS) { Name = "Effect.wz" };
        file.WzDirectory.Name = "Effect.wz";
        var image = new WzImage("Sample.img");
        file.WzDirectory.AddImage(image);

        Assert.AreSame(image, file.WzDirectory["SAMPLE.IMG"]);
        Assert.AreSame(image, file.WzDirectory.GetImageByName("sample.IMG"));

        using var manager = new WzFileManager();
        manager.LoadWzFile(file.Name, file);
        Assert.IsTrue(manager.IsWzFileLoaded("EFFECT.WZ"));
        Assert.AreSame(file.WzDirectory, manager["effect"]);
        Assert.AreSame(file.WzDirectory, manager.GetMainDirectoryByName("Effect.WZ").MainDir);
    }

    [TestMethod]
    public void FullPathAndSpanPathLookup_PreserveHierarchy()
    {
        var root = new WzDirectory("Root");
        var child = new WzDirectory("Child");
        var image = new WzImage("Sample.img");
        var group = new WzSubProperty("Group");
        var value = new WzIntProperty("Value", 7);

        root.AddDirectory(child);
        child.AddImage(image);
        image.AddProperty(group);
        group.AddProperty(value);

        Assert.AreEqual(@"Root\Child\Sample.img\Group\Value", value.FullPath);
        Assert.AreSame(value, image.GetFromPath("/Group//Value/"));
        Assert.IsNull(image.GetFromPath("../Group/Value"));
    }

    [TestMethod]
    public void DirectoryDeepClone_DoesNotMutateSourceAndReparentsChildren()
    {
        var root = new WzDirectory("Root");
        var child = new WzDirectory("Child");
        var image = new WzImage("Sample.img");
        image.AddProperty(new WzIntProperty("Value", 42));
        root.AddDirectory(child);
        child.AddImage(image);

        WzDirectory clone = root.DeepClone();

        Assert.HasCount(1, root.WzDirectories);
        Assert.HasCount(1, clone.WzDirectories);
        Assert.AreNotSame(root.WzDirectories[0], clone.WzDirectories[0]);
        Assert.AreSame(clone, clone.WzDirectories[0].Parent);
        Assert.AreSame(clone.WzDirectories[0], clone.WzDirectories[0].WzImages[0].Parent);

        clone.ClearDirectories();
        Assert.HasCount(1, root.WzDirectories);
        Assert.IsEmpty(clone.WzDirectories);
    }

    [TestMethod]
    public void ListFileRoundTrip_DoesNotMutateInputOrHoldFileHandle()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wz-list-{Guid.NewGuid():N}.wz");
        var entries = new List<string> { "Effect/One.img", "Effect/Two.img" };
        string[] original = entries.ToArray();
        try
        {
            ListFileParser.SaveToDisk(path, WzMapleVersion.BMS, entries);
            CollectionAssert.AreEqual(original, entries);

            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            CollectionAssert.AreEqual(original, ListFileParser.ParseListFile(path, WzMapleVersion.BMS));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void ScalarReaders_HandlePercentAndInvalidValuesWithoutExceptions()
    {
        var percent = new WzStringProperty("percent", "10%");
        var invalid = new WzStringProperty("invalid", "not-a-number");

        Assert.AreEqual(10, percent.ReadValue(-1));
        Assert.AreEqual(-1, invalid.ReadValue(-1));
        Assert.AreEqual(123L, invalid.ReadLong(123));
    }

    [TestMethod]
    public void LinkResolver_CopiesCompressedCanvasDataAndRemovesInlink()
    {
        var image = new WzImage("Linked.img");
        var source = new WzCanvasProperty("Source")
        {
            PngProperty = new WzPngProperty()
        };
        source.PngProperty.SetCompressedBytes([0x78, 0x9C, 0x03, 0x00], 1, 1, WzPngFormat.Format2);

        var destination = new WzCanvasProperty("Destination")
        {
            PngProperty = new WzPngProperty()
        };
        destination.PngProperty.SetCompressedBytes([0x78, 0x9C], 1, 1, WzPngFormat.Format2);
        destination.AddProperty(new WzStringProperty(WzCanvasProperty.InlinkPropertyName, "Source"));
        image.AddProperty(source);
        image.AddProperty(destination);

        Assert.IsTrue(WzLinkResolver.ResolveSingleCanvas(destination, inlinkOnly: true));
        Assert.IsFalse(destination.ContainsInlinkProperty());
        CollectionAssert.AreEqual(
            source.PngProperty.GetCompressedBytes(saveInMemory: true),
            destination.PngProperty.GetCompressedBytes(saveInMemory: true));
    }

    [TestMethod]
    public void GetObjectFromPath_SearchesAllMatchingCanvasShardImages()
    {
        using var manager = new WzFileManager();
        RegisterWzFileList(
            manager,
            "map\\map\\map1\\_canvas",
            "_canvas_000",
            "_canvas_001");

        using var firstShard = CreateCanvasShard("map/map/map1/_canvas/_canvas_000", includeTarget: false);
        using var secondShard = CreateCanvasShard("map/map/map1/_canvas/_canvas_001", includeTarget: true);
        manager.LoadWzFile("map/map/map1/_canvas/_canvas_000", firstShard);
        manager.LoadWzFile("map/map/map1/_canvas/_canvas_001", secondShard);

        using var mainFile = CreateInMemoryCanvasEraWzFile();
        WzObject resolved = mainFile.GetObjectFromPath("Map/Map/Map1/_Canvas/010006121.img/miniMap/canvas");

        WzImage targetImage = (WzImage)secondShard.WzDirectory["010006121.img"];
        WzImageProperty targetCanvas = targetImage.GetFromPath("miniMap/canvas");
        Assert.IsNotNull(targetCanvas);
        Assert.IsNotNull(resolved);
        Assert.AreSame(targetCanvas, resolved);
    }

    [TestMethod]
    public void Dispose_IsIdempotentForNewFileTree()
    {
        var file = new WzFile(95, WzMapleVersion.GMS);
        file.WzDirectory.AddImage(new WzImage("Sample.img"));

        file.Dispose();
        file.Dispose();

        Assert.IsTrue(file.IsUnloaded);
    }

    [TestMethod]
    public void PropertyCollectionIndex_PreservesOrderDuplicatesAndParentLinks()
    {
        var image = new WzImage("Indexed.img");
        var first = new WzIntProperty("Value", 1);
        var duplicate = new WzIntProperty("value", 2);
        image.WzProperties.Add(first);
        image.WzProperties.Add(duplicate);

        Assert.AreSame(first, image["VALUE"]);
        Assert.AreSame(image, first.Parent);
        Assert.AreSame(image, duplicate.Parent);
        Assert.Throws<Exception>(() => image.AddProperty(new WzIntProperty("vAlUe", 3)));

        // A public Name setter can rename an already-indexed property.  The
        // lookup must repair itself without changing list order.
        first.Name = "Renamed";
        Assert.AreSame(first, image["renamed"]);
        Assert.AreSame(duplicate, image["VALUE"]);

        var inserted = new WzIntProperty("VALUE", 4);
        image.WzProperties.Insert(0, inserted);
        Assert.AreSame(inserted, image["value"]);
        Assert.AreSame(image, inserted.Parent);

        image.WzProperties.Remove(inserted);
        Assert.IsNull(inserted.Parent);
        Assert.AreSame(duplicate, image["VALUE"]);

        var replacement = new WzIntProperty("Replacement", 5);
        image.WzProperties[1] = replacement;
        Assert.IsNull(duplicate.Parent);
        Assert.AreSame(image, replacement.Parent);
        Assert.AreSame(replacement, image["replacement"]);
        Assert.IsNull(image["value"]);

        image.WzProperties.Clear();
        Assert.IsNull(first.Parent);
        Assert.IsNull(replacement.Parent);
        Assert.IsEmpty(image.WzProperties);
        Assert.IsNull(image["replacement"]);
    }

    [TestMethod]
    public void PropertyCollectionIndex_ReindexesReverseAndRangeMutations()
    {
        var property = new WzSubProperty("Group");
        var first = new WzIntProperty("Same", 1);
        var second = new WzIntProperty("same", 2);
        var third = new WzIntProperty("Other", 3);
        property.WzProperties.Add(first);
        property.WzProperties.Add(second);
        property.WzProperties.Add(third);

        Assert.AreSame(first, property["SAME"]);
        property.WzProperties.Reverse();
        Assert.AreSame(second, property["same"]);

        property.WzProperties.RemoveRange(1, 2);
        Assert.IsNull(first.Parent);
        Assert.IsNull(second.Parent);
        Assert.AreSame(third, property["other"]);

        var added = new WzIntProperty("Added", 4);
        property.WzProperties.AddRange(new[] { first, added });
        Assert.AreSame(property, first.Parent);
        Assert.AreSame(property, added.Parent);
        Assert.AreSame(first, property["same"]);

        int removed = property.WzProperties.RemoveAll(item => item.Name == "Added");
        Assert.AreEqual(1, removed);
        Assert.IsNull(added.Parent);
        Assert.IsNull(property["added"]);
    }

    [TestMethod]
    public void PropertyCollectionIndex_RemovalMaintainsDuplicateCounts()
    {
        var property = new WzSubProperty("Group");
        var first = new WzIntProperty("Same", 1);
        var second = new WzIntProperty("same", 2);
        var unique = new WzIntProperty("Unique", 3);
        property.WzProperties.Add(first);
        property.WzProperties.Add(second);
        property.WzProperties.Add(unique);

        // Removing a non-first duplicate should leave the indexed first item
        // in place without rebuilding the entire collection.
        property.WzProperties.RemoveAt(1);
        Assert.AreSame(first, property["SAME"]);

        // Removing the indexed first item must promote the remaining item
        // when a duplicate exists, then remove the key once it is unique.
        property.WzProperties.Add(second);
        Assert.AreSame(first, property["same"]);
        property.WzProperties.RemoveAt(0);
        Assert.AreSame(second, property["SAME"]);
        property.WzProperties.Remove(second);
        Assert.IsNull(property["same"]);
        Assert.AreSame(unique, property["unique"]);
    }

    [TestMethod]
    public void DirectoryIndex_PreservesOrderDuplicatesAndParentLinks()
    {
        var root = new WzDirectory("Root");
        var firstImage = new WzImage("Entry.img");
        var secondImage = new WzImage("entry.IMG");
        root.AddImage(firstImage);
        root.AddImage(secondImage);

        Assert.AreSame(firstImage, root["ENTRY.IMG"]);
        Assert.AreSame(root, firstImage.Parent);
        Assert.AreSame(root, secondImage.Parent);

        root.RemoveImage(firstImage);
        Assert.IsNull(firstImage.Parent);
        Assert.AreSame(secondImage, root.GetImageByName("entry.img"));

        root.RemoveImage(secondImage);
        Assert.IsNull(secondImage.Parent);
        Assert.IsNull(root.GetImageByName("entry.img"));

        var firstDirectory = new WzDirectory("Group");
        var secondDirectory = new WzDirectory("group");
        root.AddDirectory(firstDirectory);
        root.AddDirectory(secondDirectory);
        Assert.AreSame(firstDirectory, root["GROUP"]);
        root.RemoveDirectory(firstDirectory);
        Assert.IsNull(firstDirectory.Parent);
        Assert.AreSame(secondDirectory, root.GetDirectoryByName("group"));

        root.ClearDirectories();
        Assert.IsNull(secondDirectory.Parent);
        Assert.IsNull(root.GetDirectoryByName("group"));
    }

    [TestMethod]
    public void DirectoryIndex_RepairsAfterNameMutationAndListAdd()
    {
        var root = new WzDirectory("Root");
        var image = new WzImage("Original.img");
        root.AddImage(image);

        image.Name = "Renamed.img";
        Assert.AreSame(image, root.GetImageByName("renamed.IMG"));
        Assert.IsNull(root.GetImageByName("original.img"));

        // The public List surface remains available.  A direct list mutation
        // is repaired lazily on the first lookup miss.
        var directImage = new WzImage("Direct.img");
        root.WzImages.Add(directImage);
        Assert.AreSame(directImage, root["DIRECT.IMG"]);

        var directory = new WzDirectory("OriginalDir");
        root.AddDirectory(directory);
        directory.Name = "RenamedDir";
        Assert.AreSame(directory, root.GetDirectoryByName("renameddir"));
        Assert.IsNull(root.GetDirectoryByName("originaldir"));
    }

    private static WzFile CreateCanvasShard(string name, bool includeTarget)
    {
        var file = CreateInMemoryCanvasEraWzFile();
        file.Name = name;
        file.WzDirectory.Name = name;

        var image = new WzImage("010006121.img");
        if (includeTarget)
        {
            var miniMap = new WzSubProperty("miniMap");
            miniMap.AddProperty(CreateCanvas("canvas"));
            image.AddProperty(miniMap);
        }
        else
        {
            image.AddProperty(CreateCanvas("notTheRequestedCanvas"));
        }

        file.WzDirectory.AddImage(image);
        return file;
    }

    private static WzFile CreateInMemoryCanvasEraWzFile()
    {
        return new WzFile(CanvasEraPatchVersion, WzMapleVersion.GMS);
    }

    private static WzCanvasProperty CreateCanvas(string name)
    {
        var canvas = new WzCanvasProperty(name)
        {
            PngProperty = new WzPngProperty()
        };
        canvas.PngProperty.SetCompressedBytes([0x78, 0x9C, 0x03, 0x00], 1, 1, WzPngFormat.Format2);
        return canvas;
    }

    private static void RegisterWzFileList(WzFileManager manager, string baseName, params string[] fileNames)
    {
        var field = typeof(WzFileManager).GetField("_wzFilesList", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);

        var wzFilesList = (Dictionary<string, List<string>>?)field.GetValue(manager);
        Assert.IsNotNull(wzFilesList);

        wzFilesList[baseName] = fileNames.ToList();
    }
}
