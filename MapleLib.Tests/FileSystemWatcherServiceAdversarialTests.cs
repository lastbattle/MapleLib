using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MapleLib.Img;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class FileSystemWatcherServiceAdversarialTests
{
    [Fact]
    public void ConstructorRejectsNegativeDebounce()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileSystemWatcherService(-1));
    }

    [Fact]
    public void PathNormalizationPreservesVolumeRoot()
    {
        string root = Path.GetPathRoot(Path.GetTempPath())!;
        MethodInfo normalize = typeof(FileSystemWatcherService).GetMethod(
            "NormalizePath",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        string actual = (string)normalize.Invoke(null, [root])!;

        Assert.Equal(root, actual, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentWatchPathCreatesOnlyOneNativeWatcher()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            using var service = new CountingWatcherService();
            using var start = new ManualResetEventSlim();
            Task[] calls = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    service.WatchPath(directory, WatchType.Category, "Map");
                }))
                .ToArray();

            start.Set();
            await Task.WhenAll(calls);

            Assert.Equal(1, service.CreateCount);
            Assert.Single(service.WatchedPaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WatcherRecoveryPreservesCategoryMetadata()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            using var service = new FileSystemWatcherService();
            service.WatchPath(directory, WatchType.Category, "Character");

            var watchers = GetField<ConcurrentDictionary<string, FileSystemWatcher>>(service, "_watchers");
            FileSystemWatcher watcher = Assert.Single(watchers.Values);
            MethodInfo recover = typeof(FileSystemWatcherService).GetMethod(
                "OnWatcherError",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            recover.Invoke(service, [watcher, new ErrorEventArgs(new InternalBufferOverflowException())]);

            var categories = GetField<ConcurrentDictionary<string, string>>(service, "_categoryPaths");
            Assert.Equal("Character", Assert.Single(categories).Value);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DisposeSuppressesQueuedDebouncedEvent()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var service = new FileSystemWatcherService(debounceMs: 100);
            int callbacks = 0;
            service.ImgFileChanged += (_, _) => Interlocked.Increment(ref callbacks);
            service.WatchPath(directory, WatchType.Category, "Map");

            var watchers = GetField<ConcurrentDictionary<string, FileSystemWatcher>>(service, "_watchers");
            FileSystemWatcher watcher = Assert.Single(watchers.Values);
            MethodInfo enqueue = typeof(FileSystemWatcherService).GetMethod(
                "OnFileSystemEvent",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            enqueue.Invoke(service,
            [
                watcher,
                new FileSystemEventArgs(WatcherChangeTypes.Changed, directory, "000000000.img")
            ]);

            service.Dispose();
            Assert.False(SpinWait.SpinUntil(() => Volatile.Read(ref callbacks) != 0, 250));
            Assert.Equal(0, callbacks);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static T GetField<T>(object instance, string name)
    {
        for (Type? type = instance.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return (T)field.GetValue(instance)!;
        }

        throw new MissingFieldException(instance.GetType().FullName, name);
    }

    private sealed class CountingWatcherService : FileSystemWatcherService
    {
        private int _createCount;

        public int CreateCount => Volatile.Read(ref _createCount);

        protected override FileSystemWatcher CreateWatcher(string path)
        {
            Interlocked.Increment(ref _createCount);
            Thread.Sleep(20);
            return base.CreateWatcher(path);
        }
    }
}
