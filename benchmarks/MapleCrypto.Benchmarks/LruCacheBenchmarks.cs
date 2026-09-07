using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.Img;

namespace MapleCrypto.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class LruCacheBenchmarks
{
    private const int ItemCount = 512;
    private const int OperationsPerWorker = 1_024;

    private LRUCache<string, CacheValue> _cache = null!;
    private ReadOnlyReaderLockCache _readerLockCache = null!;
    private string[] _keys = null!;
    private string[] _missingKeys = null!;

    [Params(1, 4, 16)]
    public int WorkerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _keys = Enumerable.Range(0, ItemCount)
            .Select(static index => $"category/path/{index:D4}.img")
            .ToArray();
        _missingKeys = Enumerable.Range(0, 16)
            .Select(static index => $"missing/{index}")
            .ToArray();
        _cache = new LRUCache<string, CacheValue>(ItemCount);
        _readerLockCache = new ReadOnlyReaderLockCache(ItemCount);
        for (int index = 0; index < _keys.Length; index++)
        {
            _cache.Add(_keys[index], new CacheValue(index));
            _readerLockCache.Add(_keys[index]);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
        _readerLockCache.Dispose();
    }

    [Benchmark(OperationsPerInvoke = OperationsPerWorker)]
    public int HotHitSingleThread()
    {
        int checksum = 0;
        for (int operation = 0; operation < OperationsPerWorker; operation++)
        {
            _cache.TryGet(_keys[operation & (ItemCount - 1)], out CacheValue? value);
            checksum += value!.Value;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = OperationsPerWorker)]
    public int HotMissSingleThread()
    {
        int misses = 0;
        for (int operation = 0; operation < OperationsPerWorker; operation++)
        {
            if (!_cache.TryGet(_missingKeys[operation & 15], out _))
                misses++;
        }
        return misses;
    }

    [Benchmark]
    public int ContendedHits()
    {
        int checksum = 0;
        Parallel.For(0, WorkerCount,
            () => 0,
            (worker, _, localChecksum) =>
            {
                int offset = worker * 17;
                for (int operation = 0; operation < OperationsPerWorker; operation++)
                {
                    _cache.TryGet(_keys[(operation + offset) & (ItemCount - 1)], out CacheValue? value);
                    localChecksum += value!.Value;
                }
                return localChecksum;
            },
            localChecksum => Interlocked.Add(ref checksum, localChecksum));
        return checksum;
    }

    [Benchmark]
    public int ContendedReadOnly()
        => RunReadOnlyWorkload(
            key => _cache.ContainsKey(key),
            () => _cache.Count);

    [Benchmark(Baseline = true)]
    public int ReaderLockContendedReadOnly()
        => RunReadOnlyWorkload(
            key => _readerLockCache.ContainsKey(key),
            () => _readerLockCache.Count);

    private int RunReadOnlyWorkload(Func<string, bool> containsKey, Func<int> getCount)
    {
        int checksum = 0;
        Parallel.For(0, WorkerCount,
            () => 0,
            (worker, _, localChecksum) =>
            {
                int offset = worker * 17;
                for (int operation = 0; operation < OperationsPerWorker; operation++)
                {
                    if (containsKey(_keys[(operation + offset) & (ItemCount - 1)]))
                        localChecksum++;
                    localChecksum += getCount();
                }
                return localChecksum;
            },
            localChecksum => Interlocked.Add(ref checksum, localChecksum));
        return checksum;
    }

    private sealed record CacheValue(int Value);

    private sealed class ReadOnlyReaderLockCache : IDisposable
    {
        private readonly Dictionary<string, byte> _items;
        private readonly ReaderWriterLockSlim _lock = new();

        internal ReadOnlyReaderLockCache(int capacity) => _items = new(capacity);

        internal void Add(string key) => _items.Add(key, 0);

        internal bool ContainsKey(string key)
        {
            _lock.EnterReadLock();
            try { return _items.ContainsKey(key); }
            finally { _lock.ExitReadLock(); }
        }

        internal int Count
        {
            get
            {
                _lock.EnterReadLock();
                try { return _items.Count; }
                finally { _lock.ExitReadLock(); }
            }
        }

        public void Dispose() => _lock.Dispose();
    }
}
