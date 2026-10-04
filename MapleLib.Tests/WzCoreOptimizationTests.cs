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
using System.Text;
using MapleLib.WzLib.Util;

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
    [DataRow(WzMapleVersion.BMS, "091B5AE23C6FA4C5B2B0B866A9C54F216D260A514CF8238C0353DE7C66DA6D45")]
    [DataRow(WzMapleVersion.GMS, "02534EE0FD07709EDAFCA0AA6DDDB2AD6364961DC7A07182B0290E03B9E87D20")]
    public void ListFileRoundTrip_DoesNotMutateInputOrHoldFileHandle(WzMapleVersion version, string expectedWireHash)
    {
        string path = Path.Combine(Path.GetTempPath(), $"wz-list-{Guid.NewGuid():N}.wz");
        var entries = new List<string>
        {
            "Effect/One.img",
            "地图/怪物/😀\uFFFF.img",
            "Mob/" + new string('x', 96) + "😀\uFFFF/LongEntry.img"
        };
        string[] original = entries.ToArray();
        try
        {
            ListFileParser.SaveToDisk(path, version, entries);
            CollectionAssert.AreEqual(original, entries);
            // Independent Python/OpenSSL wire vectors protect little-endian
            // lengths, UTF16 units, encrypted nulls, and the final slash marker.
            Assert.AreEqual(expectedWireHash,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));

            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            CollectionAssert.AreEqual(original, ListFileParser.ParseListFile(path, version));
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
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
    [DataRow("Group", "Parent",
        "Diagnostic.img/Group/Parent (_inlink: missing/source)",
        "Diagnostic.img/Group/Parent/Inner/Child (_outlink: Map/Missing.img/frame)", false)]
    [DataRow(null, null,
        "Diagnostic.img// (_inlink: missing/source)",
        "Diagnostic.img///Inner/Child (_outlink: Map/Missing.img/frame)", false)]
    [DataRow("", "",
        "Diagnostic.img// (_inlink: missing/source)",
        "Diagnostic.img///Inner/Child (_outlink: Map/Missing.img/frame)", false)]
    [DataRow("Group/Branch", "Parent/Frame",
        "Diagnostic.img/Group/Branch/Parent/Frame (_inlink: missing/source)",
        "Diagnostic.img/Group/Branch/Parent/Frame/Inner/Child (_outlink: Map/Missing.img/frame)", false)]
    [DataRow("Group", "Parent",
        "Diagnostic.img/Group/Parent (_inlink: missing/source)",
        "Diagnostic.img/Group/Parent/Inner/Child (_outlink: Map/Missing.img/frame)", true)]
    public void LinkResolver_NestedFailuresPreserveDiagnosticPathsPayloadsAndReset(
        string? groupName, string? parentName, string expectedInlinkFailure, string expectedOutlinkFailure,
        bool renameDuringTraversal)
    {
        using var image = new WzImage("Diagnostic.img");
        byte[] sourceBytes = [0x78, 0x9C, 0x52, 0xA4, 0xFE];
        byte[] parentBytes = [0x78, 0x9C, 0x11, 0xA1];
        byte[] childBytes = [0x78, 0x9C, 0x22, 0xB2];
        image.AddProperty(CreateResolverCanvas("Source", sourceBytes));
        // Public Name setters/constructors permit null, empty and slash names.
        // Logging retains these segments literally rather than normalizing them.
        var group = new RenamingResolverGroup(groupName!);
        var parent = CreateResolverCanvas(parentName!, parentBytes);
        var parentLink = new WzStringProperty(WzCanvasProperty.InlinkPropertyName, "missing/source");
        parent.AddProperty(parentLink);
        parent.AddProperty(new WzStringProperty("metadata", "unrelated"));
        var inner = new WzSubProperty("Inner");
        var child = CreateResolverCanvas("Child", childBytes);
        var childLink = new WzStringProperty(WzCanvasProperty.OutlinkPropertyName, "Map/Missing.img/frame");
        child.AddProperty(childLink);
        inner.AddProperty(child);
        parent.AddProperty(inner);
        group.AddProperty(parent);
        var success = CreateCanvas("Success");
        success.AddProperty(new WzStringProperty(WzCanvasProperty.InlinkPropertyName, "Source"));
        group.AddProperty(success);
        image.AddProperty(group);
        group.RenameWhenReadingChildren = renameDuringTraversal;
        var resolver = new WzLinkResolver();

        Assert.AreEqual(1, resolver.ResolveLinksInImage(image));
        Assert.AreEqual(1, resolver.LinksResolved);
        Assert.AreEqual(2, resolver.LinksFailed);
        CollectionAssert.AreEqual(new[] { expectedInlinkFailure, expectedOutlinkFailure }, resolver.FailedLinks);
        Assert.AreSame(parentLink, parent[WzCanvasProperty.InlinkPropertyName]);
        Assert.AreSame(childLink, child[WzCanvasProperty.OutlinkPropertyName]);
        Assert.AreEqual("missing/source", parentLink.Value);
        Assert.AreEqual("Map/Missing.img/frame", childLink.Value);
        CollectionAssert.AreEqual(parentBytes, parent.PngProperty.GetCompressedBytes(false));
        CollectionAssert.AreEqual(childBytes, child.PngProperty.GetCompressedBytes(false));
        Assert.IsFalse(success.ContainsInlinkProperty());
        CollectionAssert.AreEqual(sourceBytes, success.PngProperty.GetCompressedBytes(false));
        Assert.AreEqual(renameDuringTraversal ? "Renamed" : groupName, group.Name);

        resolver.Reset();
        Assert.AreEqual(0, resolver.LinksResolved);
        Assert.AreEqual(0, resolver.LinksFailed);
        Assert.IsEmpty(resolver.FailedLinks);
        // Reset clears session diagnostics without rewriting remaining failures.
        group.Name = groupName!;
        group.RenameWhenReadingChildren = renameDuringTraversal;
        Assert.AreEqual(0, resolver.ResolveLinksInImage(image));
        Assert.AreEqual(0, resolver.LinksResolved);
        Assert.AreEqual(2, resolver.LinksFailed);
        CollectionAssert.AreEqual(new[] { expectedInlinkFailure, expectedOutlinkFailure }, resolver.FailedLinks);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(-1)]
    [DataRow(5)]
    public void LinkResolver_NumericOutlinkFallbackUsesNestedCanvasPreorder(int index)
    {
        using var shard = new WzFile(95, WzMapleVersion.BMS)
        {
            Name = "fixture/Map/Back/_Canvas/_Canvas_000.wz"
        };
        var target = new WzImage("Shared.img");
        var branch = new WzSubProperty("branch");
        byte[][] expected =
        [
            [0x78, 0x9C, 0x10, 0xA1], [0x78, 0x9C, 0x20, 0xB2],
            [0x78, 0x9C, 0x30, 0xC3], [0x78, 0x9C, 0x40, 0xD4],
            [0x78, 0x9C, 0x50, 0xE5]
        ];
        var parent = CreateResolverCanvas("parent", expected[0]);
        parent.AddProperty(CreateResolverCanvas("child", expected[1]));
        var deeper = new WzSubProperty("deeper");
        deeper.AddProperty(CreateResolverCanvas("grandchild", expected[2]));
        parent.AddProperty(deeper);
        branch.AddProperty(parent);
        branch.AddProperty(CreateResolverCanvas("sibling", expected[3]));
        target.AddProperty(branch);
        var tail = new WzSubProperty("tail");
        tail.AddProperty(CreateResolverCanvas("last", expected[4]));
        target.AddProperty(tail);
        shard.WzDirectory.AddImage(target);

        using var linked = new WzImage("Linked.img");
        var destination = CreateCanvas("destination");
        byte[] original = [0x78, 0x9C, 0x03, 0x00];
        // No source canvas has a numeric name or the requested parent path,
        // so only the ordered numeric fallback can select a destination.
        destination.AddProperty(new WzStringProperty(WzCanvasProperty.OutlinkPropertyName,
            $"Map/Back/_Canvas/Shared.img/missing/{index}"));
        linked.AddProperty(destination);
        var resolver = new WzLinkResolver();
        resolver.SetCategoryWzFiles([shard], "Map");

        if (index < 0 || index >= expected.Length)
        {
            Assert.AreEqual(0, resolver.ResolveLinksInImage(linked));
            Assert.IsTrue(destination.ContainsOutlinkProperty());
            Assert.AreEqual(1, resolver.LinksFailed);
            CollectionAssert.AreEqual(original, destination.PngProperty.GetCompressedBytes(false));
        }
        else
        {
            Assert.AreEqual(1, resolver.ResolveLinksInImage(linked));
            Assert.IsFalse(destination.ContainsOutlinkProperty());
            CollectionAssert.AreEqual(expected[index], destination.PngProperty.GetCompressedBytes(false));
        }
    }

    [TestMethod]
    public void LinkResolver_OutOfRangeNumericOrdinalContinuesToLaterShard()
    {
        using var shortShard = new WzFile(95, WzMapleVersion.BMS)
        {
            Name = "fixture/Map/Back/_Canvas/_Canvas_000.wz"
        };
        var shortImage = new WzImage("Shared.img");
        var shortBranch = new WzSubProperty("branch");
        shortBranch.AddProperty(CreateResolverCanvas("only", [0x78, 0x9C, 0x11]));
        shortImage.AddProperty(shortBranch);
        shortShard.WzDirectory.AddImage(shortImage);

        using var adequateShard = new WzFile(95, WzMapleVersion.BMS)
        {
            Name = "fixture/Map/Back/_Canvas/_Canvas_001.wz"
        };
        byte[] expected = [0x78, 0x9C, 0x52, 0xA4, 0xFE];
        var adequateImage = new WzImage("Shared.img");
        var parent = CreateResolverCanvas("parent", [0x78, 0x9C, 0x22]);
        parent.AddProperty(CreateResolverCanvas("nested", expected));
        adequateImage.AddProperty(parent);
        adequateShard.WzDirectory.AddImage(adequateImage);

        using var linked = new WzImage("Linked.img");
        var destination = CreateCanvas("destination");
        destination.AddProperty(new WzStringProperty(WzCanvasProperty.OutlinkPropertyName,
            "Map/Back/_Canvas/Shared.img/missing/1"));
        linked.AddProperty(destination);
        var resolver = new WzLinkResolver();
        resolver.SetCategoryWzFiles([shortShard, adequateShard], "Map");

        Assert.AreEqual(1, resolver.ResolveLinksInImage(linked));
        Assert.AreEqual(0, resolver.LinksFailed);
        Assert.IsFalse(destination.ContainsOutlinkProperty());
        CollectionAssert.AreEqual(expected, destination.PngProperty.GetCompressedBytes(false));
    }

    [TestMethod]
    public void LinkResolver_SkipsForeignFolderAndEmptyShardBeforeFirstUsableCanvas()
    {
        byte[] expected = [0x78, 0x9C, 0x42, 0x91, 0xFE];
        using var foreign = CreateResolverShard("fixture/Map/Obj/_Canvas/_Canvas_000.wz", [0x78, 0x9C, 0x11]);
        using var placeholder = CreateResolverShard("fixture/Map/Back/_Canvas/_Canvas_001.wz", []);
        using var usable = CreateResolverShard("fixture/Map/Back/_Canvas/_Canvas_002.wz", expected);
        using var later = CreateResolverShard("fixture/Map/Back/_Canvas/_Canvas_003.wz", [0x78, 0x9C, 0x99]);
        using var linked = new WzImage("Linked.img");
        var destination = CreateCanvas("destination");
        destination.AddProperty(new WzStringProperty(WzCanvasProperty.OutlinkPropertyName,
            "Map/Back/_Canvas/Shared.img/group/frame"));
        linked.AddProperty(destination);
        var resolver = new WzLinkResolver();
        resolver.SetCategoryWzFiles([foreign, placeholder, usable, later], "Map");

        Assert.AreEqual(1, resolver.ResolveLinksInImage(linked));
        Assert.IsFalse(destination.ContainsOutlinkProperty());
        CollectionAssert.AreEqual(expected, destination.PngProperty.GetCompressedBytes(false));
    }

    [TestMethod]
    public void PropertyCollectionInsertRange_BatchesIndexRebuildAndPreservesOrder()
    {
        var parent = new WzSubProperty("Parent");
        var properties = new WzPropertyCollection(parent);
        var first = new WzIntProperty("First", 1);
        var duplicate = new WzIntProperty("Duplicate", 2);
        var inserted = new WzIntProperty("Inserted", 3);
        var nullNamed = new WzIntProperty(null!, 4);
        properties.Add(first);
        properties.Add(duplicate);

        properties.InsertRange(1, new[] { inserted, nullNamed, duplicate });

        Assert.HasCount(5, properties);
        Assert.AreSame(first, properties[0]);
        Assert.AreSame(inserted, properties[1]);
        Assert.AreSame(nullNamed, properties[2]);
        Assert.AreSame(duplicate, properties[3]);
        Assert.AreSame(duplicate, properties.FindByName("DUPLICATE"));
        Assert.AreSame(nullNamed, properties.FindByName(null!));
        Assert.AreSame(parent, inserted.Parent);
        Assert.AreSame(parent, nullNamed.Parent);
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
    public void NullTerminatedString_DoesNotConsumeBytesAfterTerminator()
    {
        byte[] payload = Encoding.UTF8.GetBytes("hello\0tail");
        using var reader = new WzBinaryReader(
            new MemoryStream(payload, writable: false),
            WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));

        Assert.AreEqual("hello", reader.ReadNullTerminatedString());
        Assert.AreEqual((byte)'t', reader.ReadByte());
    }

    [TestMethod]
    public void ImageChecksum_RestoresOriginalStreamPosition()
    {
        using var image = new WzImage("checksum.img");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("checksum data"), writable: false);
        stream.Position = 4;

        image.CalculateAndSetImageChecksum(stream);

        Assert.AreEqual(4, stream.Position);
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
    public void DeepClone_PreservesDuplicateSourcePropertyNames()
    {
        var source = new WzImage("100000000.img");
        source.WzProperties.Add(new WzIntProperty("wedding", 0));
        source.WzProperties.Add(new WzIntProperty("wedding", 1));

        using WzImage clone = source.DeepClone();

        Assert.HasCount(2, clone.WzProperties);
        Assert.AreEqual("wedding", clone.WzProperties[0].Name);
        Assert.AreEqual(0, clone.WzProperties[0].GetInt());
        Assert.AreEqual("wedding", clone.WzProperties[1].Name);
        Assert.AreEqual(1, clone.WzProperties[1].GetInt());
        Assert.AreSame(clone, clone.WzProperties[0].Parent);
        Assert.AreSame(clone, clone.WzProperties[1].Parent);
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
        Assert.AreSame(first, property.WzProperties.Find("Same", StringComparison.Ordinal));
        Assert.IsNull(property.WzProperties.Find("SAME", StringComparison.Ordinal));
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

    [TestMethod]
    public void DirectoryMutationRejectsCyclesAndPreservesForeignParents()
    {
        var root = new WzDirectory("Root");
        var child = new WzDirectory("Child");
        root.AddDirectory(child);

        Assert.Throws<InvalidOperationException>(() => root.AddDirectory(root));
        Assert.Throws<InvalidOperationException>(() => child.AddDirectory(root));

        var otherRoot = new WzDirectory("Other");
        Assert.Throws<InvalidOperationException>(() => otherRoot.AddDirectory(child));
        otherRoot.RemoveDirectory(child);
        Assert.AreSame(root, child.Parent);

        var image = new WzImage("item.img");
        root.AddImage(image);
        otherRoot.RemoveImage(image);
        Assert.AreSame(root, image.Parent);
    }

    private sealed class RenamingResolverGroup(string name) : WzSubProperty(name)
    {
        public bool RenameWhenReadingChildren { get; set; }

        public override WzPropertyCollection WzProperties
        {
            get
            {
                if (RenameWhenReadingChildren)
                {
                    RenameWhenReadingChildren = false;
                    Name = "Renamed";
                }
                return base.WzProperties;
            }
        }
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

    private static WzCanvasProperty CreateResolverCanvas(string name, byte[] bytes)
    {
        var canvas = new WzCanvasProperty(name) { PngProperty = new WzPngProperty() };
        canvas.PngProperty.SetCompressedBytes(bytes, 1, 1, WzPngFormat.Format2);
        return canvas;
    }

    private static WzFile CreateResolverShard(string name, byte[] bytes)
    {
        var file = new WzFile(95, WzMapleVersion.BMS) { Name = name };
        var image = new WzImage("Shared.img");
        var group = new WzSubProperty("group");
        group.AddProperty(CreateResolverCanvas("frame", bytes));
        image.AddProperty(group);
        file.WzDirectory.AddImage(image);
        return file;
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
