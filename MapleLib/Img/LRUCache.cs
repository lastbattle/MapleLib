using System;
using System.Collections.Generic;
using System.Threading;

namespace MapleLib.Img
{
    /// <summary>
    /// Thread-safe LRU (Least Recently Used) cache implementation.
    /// Automatically evicts least recently used items when capacity is reached.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the cache</typeparam>
    /// <typeparam name="TValue">The type of values in the cache</typeparam>
    public class LRUCache<TKey, TValue> : IDisposable where TValue : class
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;
        private readonly ReaderWriterLockSlim _lock;
        private readonly Func<TValue, long> _sizeEstimator;
        private long _currentSize;
        private long _maxSizeBytes;
        private bool _disposed;

        // Statistics
        private long _hitCount;
        private long _missCount;
        private long _evictionCount;

        private class CacheItem
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
            public long Size { get; set; }
            public DateTime LastAccess { get; set; }
        }

        /// <summary>
        /// Creates an LRU cache with specified item capacity
        /// </summary>
        /// <param name="capacity">Maximum number of items</param>
        public LRUCache(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 100;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(_capacity);
            _lruList = new LinkedList<CacheItem>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _maxSizeBytes = long.MaxValue;
        }

        /// <summary>
        /// Creates an LRU cache with size-based eviction
        /// </summary>
        /// <param name="maxSizeBytes">Maximum total size in bytes</param>
        /// <param name="sizeEstimator">Function to estimate size of each value</param>
        public LRUCache(long maxSizeBytes, Func<TValue, long> sizeEstimator)
        {
            _capacity = int.MaxValue;
            _maxSizeBytes = maxSizeBytes > 0 ? maxSizeBytes : 512 * 1024 * 1024; // 512MB default
            _sizeEstimator = sizeEstimator ?? throw new ArgumentNullException(nameof(sizeEstimator));
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>();
            _lruList = new LinkedList<CacheItem>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        /// <summary>
        /// Gets the current number of items in cache
        /// </summary>
        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _cache.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets the current estimated size of cache in bytes
        /// </summary>
        public long CurrentSizeBytes => Interlocked.Read(ref _currentSize);

        /// <summary>
        /// Gets the cache hit count
        /// </summary>
        public long HitCount => Interlocked.Read(ref _hitCount);

        /// <summary>
        /// Gets the cache miss count
        /// </summary>
        public long MissCount => Interlocked.Read(ref _missCount);

        /// <summary>
        /// Gets the eviction count
        /// </summary>
        public long EvictionCount => Interlocked.Read(ref _evictionCount);

        /// <summary>
        /// Gets the cache hit ratio (0-1)
        /// </summary>
        public double HitRatio
        {
            get
            {
                long total = HitCount + MissCount;
                return total > 0 ? (double)HitCount / total : 0;
            }
        }

        /// <summary>
        /// Tries to get a value from the cache
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    // Move to front (most recently used)
                    _lock.EnterWriteLock();
                    try
                    {
                        _lruList.Remove(node);
                        _lruList.AddFirst(node);
                        node.Value.LastAccess = DateTime.UtcNow;
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    Interlocked.Increment(ref _hitCount);
                    value = node.Value.Value;
                    return true;
                }

                Interlocked.Increment(ref _missCount);
                value = default;
                return false;
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Gets a value from cache or creates it using the factory function
        /// </summary>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            if (TryGet(key, out var value))
                return value;

            value = factory(key);
            if (value != null)
            {
                Add(key, value);
            }
            return value;
        }

        /// <summary>
        /// Adds or updates a value in the cache
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            if (value == null)
                return;

            long itemSize = _sizeEstimator?.Invoke(value) ?? 1;

            _lock.EnterWriteLock();
            try
            {
                // Remove existing if present
                if (_cache.TryGetValue(key, out var existingNode))
                {
                    _currentSize -= existingNode.Value.Size;
                    _lruList.Remove(existingNode);
                    _cache.Remove(key);
                }

                // Evict if necessary
                while ((_cache.Count >= _capacity || _currentSize + itemSize > _maxSizeBytes) && _lruList.Count > 0)
                {
                    EvictLeastRecentlyUsed();
                }

                // Add new item
                var cacheItem = new CacheItem
                {
                    Key = key,
                    Value = value,
                    Size = itemSize,
                    LastAccess = DateTime.UtcNow
                };

                var node = new LinkedListNode<CacheItem>(cacheItem);
                _lruList.AddFirst(node);
                _cache[key] = node;
                _currentSize += itemSize;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a specific key from the cache
        /// </summary>
        public bool Remove(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    _currentSize -= node.Value.Size;
                    _lruList.Remove(node);
                    _cache.Remove(key);
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks if a key exists in the cache without affecting LRU order
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            _lock.EnterReadLock();
            try
            {
                return _cache.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets all cached items as key-value pairs without affecting LRU order.
        /// Thread-safe snapshot of current cache contents.
        /// </summary>
        public IEnumerable<KeyValuePair<TKey, TValue>> GetAllItems()
        {
            _lock.EnterReadLock();
            try
            {
                // Create a snapshot to avoid holding lock during enumeration
                var snapshot = new List<KeyValuePair<TKey, TValue>>(_cache.Count);
                foreach (var node in _lruList)
                {
                    snapshot.Add(new KeyValuePair<TKey, TValue>(node.Key, node.Value));
                }
                return snapshot;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets all cached values without affecting LRU order.
        /// Thread-safe snapshot of current cache contents.
        /// </summary>
        public IEnumerable<TValue> GetAllValues()
        {
            _lock.EnterReadLock();
            try
            {
                // Create a snapshot to avoid holding lock during enumeration
                var snapshot = new List<TValue>(_cache.Count);
                foreach (var node in _lruList)
                {
                    snapshot.Add(node.Value);
                }
                return snapshot;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _cache.Clear();
                _lruList.Clear();
                _currentSize = 0;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Resets cache statistics
        /// </summary>
        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _missCount, 0);
            Interlocked.Exchange(ref _evictionCount, 0);
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                ItemCount = Count,
                SizeBytes = CurrentSizeBytes,
                MaxSizeBytes = _maxSizeBytes,
                HitCount = HitCount,
                MissCount = MissCount,
                EvictionCount = EvictionCount,
                HitRatio = HitRatio
            };
        }

        private void EvictLeastRecentlyUsed()
        {
            var lastNode = _lruList.Last;
            if (lastNode != null)
            {
                _currentSize -= lastNode.Value.Size;
                _cache.Remove(lastNode.Value.Key);
                _lruList.RemoveLast();
                Interlocked.Increment(ref _evictionCount);

                // Don't dispose on eviction - the InfoManager caches hold references to
                // properties inside these WzImages. Disposing would invalidate those references.
                // With freeResources=true, file handles are already closed after parsing.
                // Memory will be freed by GC when no more references exist.
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _lock.EnterWriteLock();
            try
            {
                // Dispose all cached values that implement IDisposable
                foreach (var node in _lruList)
                {
                    if (node.Value is IDisposable disposable)
                    {
                        try { disposable.Dispose(); } catch { }
                    }
                }
                _cache.Clear();
                _lruList.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            _lock.Dispose();
        }
    }

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class CacheStatistics
    {
        public int ItemCount { get; set; }
        public long SizeBytes { get; set; }
        public long MaxSizeBytes { get; set; }
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long EvictionCount { get; set; }
        public double HitRatio { get; set; }

        public override string ToString()
        {
            return $"Items: {ItemCount}, Size: {SizeBytes / 1024 / 1024}MB, Hit Ratio: {HitRatio:P1}, Evictions: {EvictionCount}";
        }
    }
}
