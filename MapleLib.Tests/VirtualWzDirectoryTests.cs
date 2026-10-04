using System.IO;
using MapleLib.Img;
using MapleLib.WzLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class VirtualWzDirectoryTests
{
    [Fact]
    public void WzDirectories_ReturnsSnapshotThatCallersCannotMutate()
    {
        string root = Directory.CreateTempSubdirectory("maplelib-virtual-test-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "Test", "Child"));
        try
        {
            using var manager = new ImgFileSystemManager(root);
            VirtualWzDirectory directory = manager.GetDirectory("Test")!;

            var snapshot = directory.WzDirectories;
            Assert.Single(snapshot);
            snapshot.Clear();

            Assert.Single(directory.WzDirectories);
            Assert.NotNull(directory.GetDirectoryByName("Child"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RemoveImage_MatchesCaseInsensitivelyAndDisposesOnlyRemovedImage()
    {
        string root = Directory.CreateTempSubdirectory("maplelib-virtual-test-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "Test"));
        try
        {
            using var manager = new ImgFileSystemManager(root);
            VirtualWzDirectory directory = manager.GetDirectory("Test")!;
            var first = new WzImage("entry.img");
            var second = new WzImage("ENTRY.IMG");
            directory.WzImages.Add(first);
            directory.WzImages.Add(second);

            Assert.True(directory.RemoveImage("EnTrY.ImG"));
            Assert.Null(first.Name);
            Assert.Equal("ENTRY.IMG", second.Name);
            Assert.Single(directory.WzImages);
            Assert.Same(second, directory.WzImages[0]);
            Assert.False(directory.RemoveImage("missing.img"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
