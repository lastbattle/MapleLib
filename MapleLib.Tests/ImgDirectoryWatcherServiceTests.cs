using System;
using System.IO;
using System.Threading;
using MapleLib.Img;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class ImgDirectoryWatcherServiceTests
{
    [Fact]
    public void LazyInitialState_RecordsFilesOnlyWhenRequested()
    {
        string directory = CreateTemporaryDirectory();
        string imagePath = Path.Combine(directory, "existing.img");

        try
        {
            File.WriteAllBytes(imagePath, [1, 2, 3, 4]);
            using var watcher = new ImgDirectoryWatcherService(recordInitialState: false);
            watcher.WatchDirectory(directory);

            Assert.Equal(ImgChangeType.Added, watcher.GetChangeType(imagePath));
            watcher.RecordFileState(imagePath);
            Assert.Equal(ImgChangeType.None, watcher.GetChangeType(imagePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LazyInitialState_StillReportsDeletionOfPreExistingFile()
    {
        string directory = CreateTemporaryDirectory();
        string imagePath = Path.Combine(directory, "existing.img");

        try
        {
            File.WriteAllBytes(imagePath, [1, 2, 3, 4]);
            using var watcher = new ImgDirectoryWatcherService(recordInitialState: false);
            using var deleted = new ManualResetEventSlim();
            watcher.ImgFileDeleted += (_, args) =>
            {
                if (args.FilePath == imagePath)
                    deleted.Set();
            };

            watcher.WatchDirectory(directory);
            File.Delete(imagePath);

            Assert.True(deleted.Wait(TimeSpan.FromSeconds(5)), "Deletion event was not raised.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"MapleLib.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
