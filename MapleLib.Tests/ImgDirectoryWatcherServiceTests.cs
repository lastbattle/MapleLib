using System;
using System.IO;
using System.Reflection;
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

    [Fact]
    public void Debounce_ReplacingTimerDoesNotRunStaleCallback()
    {
        using var watcher = new ImgDirectoryWatcherService(debounceMs: 80, recordInitialState: false);
        MethodInfo method = typeof(ImgDirectoryWatcherService).GetMethod(
            "ProcessChangeWithDebounce", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int callbackCount = 0;
        Action<string> callback = _ => Interlocked.Increment(ref callbackCount);

        method.Invoke(watcher, ["same.img", callback]);
        Thread.Sleep(20);
        method.Invoke(watcher, ["same.img", callback]);

        Thread.Sleep(250);
        Assert.Equal(1, Volatile.Read(ref callbackCount));
    }

    [Fact]
    public void Debounce_DisposeSuppressesQueuedCallback()
    {
        var watcher = new ImgDirectoryWatcherService(debounceMs: 80, recordInitialState: false);
        MethodInfo method = typeof(ImgDirectoryWatcherService).GetMethod(
            "ProcessChangeWithDebounce", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int callbackCount = 0;

        method.Invoke(watcher, ["disposed.img", (Action<string>)(_ => Interlocked.Increment(ref callbackCount))]);
        watcher.Dispose();
        Thread.Sleep(250);

        Assert.Equal(0, Volatile.Read(ref callbackCount));
    }

    [Fact]
    public void GetChangeType_DetectsSameSizeContentWithRestoredTimestamp()
    {
        string directory = CreateTemporaryDirectory();
        string imagePath = Path.Combine(directory, "same-size.img");

        try
        {
            File.WriteAllBytes(imagePath, [1, 2, 3, 4]);
            using var watcher = new ImgDirectoryWatcherService(recordInitialState: false);
            watcher.RecordFileState(imagePath);
            DateTime originalTimestamp = File.GetLastWriteTimeUtc(imagePath);

            File.WriteAllBytes(imagePath, [4, 3, 2, 1]);
            File.SetLastWriteTimeUtc(imagePath, originalTimestamp);

            Assert.Equal(ImgChangeType.ContentChanged, watcher.GetChangeType(imagePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RecordFileState_CanonicalizesEquivalentPaths()
    {
        string directory = CreateTemporaryDirectory();
        string imagePath = Path.Combine(directory, "existing.img");

        try
        {
            File.WriteAllBytes(imagePath, [1, 2, 3]);
            string equivalentPath = Path.Combine(directory, ".", "existing.img");
            using var watcher = new ImgDirectoryWatcherService(recordInitialState: false);

            watcher.RecordFileState(equivalentPath);

            Assert.Equal(ImgChangeType.None, watcher.GetChangeType(imagePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UnwatchDirectory_UsesDirectoryBoundaryWhenRemovingStates()
    {
        string parent = CreateTemporaryDirectory();
        string watchedDirectory = Path.Combine(parent, "foo");
        string siblingDirectory = Path.Combine(parent, "foobar");
        Directory.CreateDirectory(watchedDirectory);
        Directory.CreateDirectory(siblingDirectory);
        string watchedImage = Path.Combine(watchedDirectory, "inside.img");
        string siblingImage = Path.Combine(siblingDirectory, "sibling.img");

        try
        {
            File.WriteAllBytes(watchedImage, [1]);
            File.WriteAllBytes(siblingImage, [1]);
            using var watcher = new ImgDirectoryWatcherService(recordInitialState: false);
            watcher.RecordFileState(watchedImage);
            watcher.RecordFileState(siblingImage);
            watcher.WatchDirectory(watchedDirectory);
            watcher.UnwatchDirectory(watchedDirectory);

            Assert.Equal(ImgChangeType.Added, watcher.GetChangeType(watchedImage));
            Assert.Equal(ImgChangeType.None, watcher.GetChangeType(siblingImage));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void WatchDirectory_PreservesFilesystemRootNormalization()
    {
        string temporaryDirectory = CreateTemporaryDirectory();
        string root = Path.GetPathRoot(Path.GetFullPath(temporaryDirectory))!;
        Directory.Delete(temporaryDirectory, recursive: true);
        using var watcher = new ImgDirectoryWatcherService(recordInitialState: false);

        watcher.WatchDirectory(root);
        try
        {
            Assert.True(watcher.IsWatching(root));
            Assert.Contains(watcher.WatchedDirectories, path =>
                string.Equals(path, root, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            watcher.UnwatchDirectory(root);
        }
    }

    [Fact]
    public void ConcurrentWatchDirectoryCallsCreateOneWatcher()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            using var watcher = new ImgDirectoryWatcherService(debounceMs: 40, recordInitialState: false);
            Parallel.For(0, 64, _ => watcher.WatchDirectory(directory));

            Assert.Single(watcher.WatchedDirectories);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Debounce_ZeroDelayStillRunsCallbackAfterPublication()
    {
        using var watcher = new ImgDirectoryWatcherService(debounceMs: 0, recordInitialState: false);
        MethodInfo method = typeof(ImgDirectoryWatcherService).GetMethod(
            "ProcessChangeWithDebounce", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var callbackCompleted = new ManualResetEventSlim();
        int callbackCount = 0;

        method.Invoke(watcher, ["zero-delay.img", (Action<string>)(_ =>
        {
            Interlocked.Increment(ref callbackCount);
            callbackCompleted.Set();
        })]);

        Assert.True(callbackCompleted.Wait(TimeSpan.FromSeconds(2)), "Zero-delay callback was lost.");
        Assert.Equal(1, Volatile.Read(ref callbackCount));
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"MapleLib.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
