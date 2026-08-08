using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MapleLib.Img
{
    /// <summary>
    /// Specifies the type of path being watched
    /// </summary>
    public enum WatchType
    {
        /// <summary>
        /// Watch for .img files in a category directory
        /// </summary>
        Category,

        /// <summary>
        /// Watch for version directories being added/removed
        /// </summary>
        VersionRoot
    }

    /// <summary>
    /// Event arguments for .img file changes
    /// </summary>
    public class ImgFileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Full path to the changed file
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Category name (e.g., "Map", "String")
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Relative path within the category
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Type of change that occurred
        /// </summary>
        public WatcherChangeTypes ChangeType { get; }

        /// <summary>
        /// Old path (for rename events)
        /// </summary>
        public string OldPath { get; }

        public ImgFileChangedEventArgs(string filePath, string category, string relativePath, WatcherChangeTypes changeType, string oldPath = null)
        {
            FilePath = filePath;
            Category = category;
            RelativePath = relativePath;
            ChangeType = changeType;
            OldPath = oldPath;
        }
    }

    /// <summary>
    /// Event arguments for version directory changes
    /// </summary>
    public class VersionDirectoryChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Path to the version directory
        /// </summary>
        public string VersionPath { get; }

        /// <summary>
        /// Type of change that occurred
        /// </summary>
        public WatcherChangeTypes ChangeType { get; }

        public VersionDirectoryChangedEventArgs(string versionPath, WatcherChangeTypes changeType)
        {
            VersionPath = versionPath;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// Internal structure to track pending file changes for debouncing
    /// </summary>
    internal class FileChangeInfo
    {
        public string Path { get; set; }
        public string OldPath { get; set; }
        public WatcherChangeTypes ChangeType { get; set; }
        public DateTime Timestamp { get; set; }
        public WatchType WatchType { get; set; }
        public string Category { get; set; }
        public string RelativePath { get; set; }
    }

    /// <summary>
    /// Service that monitors file system changes for .img files and version directories.
    /// Provides debounced events for file additions, deletions, and modifications.
    /// </summary>
    public class FileSystemWatcherService : IDisposable
    {
        #region Fields
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
        private readonly ConcurrentDictionary<string, WatchType> _watchTypes = new();
        private readonly ConcurrentDictionary<string, string> _categoryPaths = new(); // Maps watcher path to category name
        private readonly ConcurrentQueue<FileChangeInfo> _pendingChanges = new();
        private Timer _debounceTimer;
        private readonly int _debounceMs;
        private readonly object _watcherLock = new();
        private readonly object _timerLock = new();
        private long _timerVersion;
        private int _disposed;
        private int _isProcessing;
        #endregion

        #region Events
        /// <summary>
        /// Raised when an .img file is created, deleted, modified, or renamed
        /// </summary>
        public event EventHandler<ImgFileChangedEventArgs> ImgFileChanged;

        /// <summary>
        /// Raised when a version directory is created or deleted
        /// </summary>
        public event EventHandler<VersionDirectoryChangedEventArgs> VersionDirectoryChanged;

        /// <summary>
        /// Raised when an error occurs in a file system watcher
        /// </summary>
        public event EventHandler<ErrorEventArgs> WatcherError;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new FileSystemWatcherService
        /// </summary>
        /// <param name="debounceMs">Milliseconds to wait before processing changes (default 500ms)</param>
        public FileSystemWatcherService(int debounceMs = 500)
        {
            if (debounceMs < 0)
                throw new ArgumentOutOfRangeException(nameof(debounceMs));

            _debounceMs = debounceMs;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts watching a path for changes
        /// </summary>
        /// <param name="path">The directory path to watch</param>
        /// <param name="watchType">The type of watching to perform</param>
        /// <param name="categoryName">Optional category name for Category watch type</param>
        public void WatchPath(string path, WatchType watchType, string categoryName = null)
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(FileSystemWatcherService));

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            if (!Enum.IsDefined(watchType))
                throw new ArgumentOutOfRangeException(nameof(watchType));

            // Normalize path
            string normalizedPath = NormalizePath(path);

            lock (_watcherLock)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    throw new ObjectDisposedException(nameof(FileSystemWatcherService));

                // Creating and publishing a watcher is one operation.  A
                // ContainsKey/indexer sequence leaks duplicate live watchers
                // when callers race to watch the same directory.
                if (_watchers.ContainsKey(normalizedPath))
                    return;

                FileSystemWatcher watcher = null;
                try
                {
                    watcher = CreateWatcher(normalizedPath);

                    switch (watchType)
                    {
                        case WatchType.Category:
                            watcher.Filter = "*.img";
                            watcher.IncludeSubdirectories = true;
                            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                            _categoryPaths[normalizedPath] = categoryName ?? Path.GetFileName(normalizedPath);
                            break;

                        case WatchType.VersionRoot:
                            watcher.Filter = "*";
                            watcher.IncludeSubdirectories = false;
                            watcher.NotifyFilter = NotifyFilters.DirectoryName;
                            break;
                    }

                    watcher.Created += OnFileSystemEvent;
                    watcher.Deleted += OnFileSystemEvent;
                    watcher.Changed += OnFileSystemEvent;
                    watcher.Renamed += OnFileSystemRenamed;
                    watcher.Error += OnWatcherError;

                    // Publish metadata before enabling events so an immediate
                    // callback cannot observe a half-registered watcher.
                    _watchers[normalizedPath] = watcher;
                    _watchTypes[normalizedPath] = watchType;
                    watcher.EnableRaisingEvents = true;
                }
                catch (Exception ex)
                {
                    _watchers.TryRemove(normalizedPath, out _);
                    _watchTypes.TryRemove(normalizedPath, out _);
                    _categoryPaths.TryRemove(normalizedPath, out _);
                    watcher?.Dispose();
                    System.Diagnostics.Debug.WriteLine($"Failed to create watcher for {path}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Stops watching a specific path
        /// </summary>
        /// <param name="path">The path to stop watching</param>
        public void UnwatchPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string normalizedPath = NormalizePath(path);

            lock (_watcherLock)
            {
                if (_watchers.TryRemove(normalizedPath, out var watcher))
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }

                _watchTypes.TryRemove(normalizedPath, out _);
                _categoryPaths.TryRemove(normalizedPath, out _);
            }
        }

        /// <summary>
        /// Stops all watchers
        /// </summary>
        public void UnwatchAll()
        {
            foreach (var path in _watchers.Keys.ToList())
            {
                UnwatchPath(path);
            }
        }

        /// <summary>
        /// Gets the list of currently watched paths
        /// </summary>
        public IReadOnlyCollection<string> WatchedPaths => _watchers.Keys.ToList().AsReadOnly();

        /// <summary>
        /// Checks if a path is being watched
        /// </summary>
        public bool IsWatching(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = NormalizePath(path);
            return _watchers.ContainsKey(normalizedPath);
        }

        private static string NormalizePath(string path)
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        /// <summary>
        /// Creates the underlying watcher. The hook keeps resource-publication
        /// behavior testable without changing normal construction.
        /// </summary>
        protected virtual FileSystemWatcher CreateWatcher(string path)
        {
            return new FileSystemWatcher(path);
        }
        #endregion

        #region Event Handlers
        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            var watcher = sender as FileSystemWatcher;
            if (watcher == null)
                return;

            string watcherPath = watcher.Path;
            if (!_watchTypes.TryGetValue(watcherPath, out var watchType))
                return;

            // Determine category and relative path for Category watchers
            string category = null;
            string relativePath = null;

            if (watchType == WatchType.Category)
            {
                _categoryPaths.TryGetValue(watcherPath, out category);
                relativePath = e.FullPath.Substring(watcherPath.Length).TrimStart(Path.DirectorySeparatorChar);
            }

            // Queue the change
            _pendingChanges.Enqueue(new FileChangeInfo
            {
                Path = e.FullPath,
                ChangeType = e.ChangeType,
                Timestamp = DateTime.UtcNow,
                WatchType = watchType,
                Category = category,
                RelativePath = relativePath
            });

            // Reset debounce timer
            ResetDebounceTimer();
        }

        private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            var watcher = sender as FileSystemWatcher;
            if (watcher == null)
                return;

            string watcherPath = watcher.Path;
            if (!_watchTypes.TryGetValue(watcherPath, out var watchType))
                return;

            // Determine category and relative path for Category watchers
            string category = null;
            string relativePath = null;

            if (watchType == WatchType.Category)
            {
                _categoryPaths.TryGetValue(watcherPath, out category);
                relativePath = e.FullPath.Substring(watcherPath.Length).TrimStart(Path.DirectorySeparatorChar);
            }

            // Queue the change
            _pendingChanges.Enqueue(new FileChangeInfo
            {
                Path = e.FullPath,
                OldPath = e.OldFullPath,
                ChangeType = WatcherChangeTypes.Renamed,
                Timestamp = DateTime.UtcNow,
                WatchType = watchType,
                Category = category,
                RelativePath = relativePath
            });

            // Reset debounce timer
            ResetDebounceTimer();
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            WatcherError?.Invoke(this, e);

            // Try to recover by re-creating the watcher
            var watcher = sender as FileSystemWatcher;
            if (watcher != null)
            {
                string path = watcher.Path;
                if (_watchTypes.TryGetValue(path, out var watchType))
                {
                    // Capture metadata before UnwatchPath removes it.
                    _categoryPaths.TryGetValue(path, out string category);

                    // Re-create the watcher
                    UnwatchPath(path);
                    WatchPath(path, watchType, category);
                }
            }
        }
        #endregion

        #region Debouncing
        private void ResetDebounceTimer()
        {
            lock (_timerLock)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    return;

                _debounceTimer?.Dispose();
                long version = ++_timerVersion;
                var timer = new Timer(ProcessPendingChanges, version, Timeout.Infinite, Timeout.Infinite);
                _debounceTimer = timer;
                timer.Change(_debounceMs, Timeout.Infinite);
            }
        }

        private void ProcessPendingChanges(object state)
        {
            long version = (long)state;
            lock (_timerLock)
            {
                if (Volatile.Read(ref _disposed) != 0 || version != _timerVersion)
                    return;
            }

            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
                return;

            try
            {
                var changes = new List<FileChangeInfo>();
                while (_pendingChanges.TryDequeue(out var change))
                {
                    changes.Add(change);
                }

                if (changes.Count == 0)
                    return;

                // Group by path and take the latest change for each
                var uniqueChanges = changes
                    .GroupBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(c => c.Timestamp).First())
                    .ToList();

                foreach (var change in uniqueChanges)
                {
                    if (Volatile.Read(ref _disposed) != 0)
                        break;
                    RaiseAppropriateEvent(change);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);

                // A newer callback can arrive after this callback drained the
                // queue but while it still held the processing gate.
                if (!_pendingChanges.IsEmpty && Volatile.Read(ref _disposed) == 0)
                    ResetDebounceTimer();
            }
        }

        private void RaiseAppropriateEvent(FileChangeInfo change)
        {
            try
            {
                switch (change.WatchType)
                {
                    case WatchType.Category:
                        ImgFileChanged?.Invoke(this, new ImgFileChangedEventArgs(
                            change.Path,
                            change.Category,
                            change.RelativePath,
                            change.ChangeType,
                            change.OldPath));
                        break;

                    case WatchType.VersionRoot:
                        // Only raise for directories
                        if (change.ChangeType == WatcherChangeTypes.Deleted ||
                            Directory.Exists(change.Path))
                        {
                            VersionDirectoryChanged?.Invoke(this, new VersionDirectoryChangedEventArgs(
                                change.Path,
                                change.ChangeType));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error raising file change event: {ex.Message}");
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            lock (_timerLock)
            {
                _timerVersion++;
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            lock (_watcherLock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }

                _watchers.Clear();
                _watchTypes.Clear();
                _categoryPaths.Clear();
            }

            // Clear pending changes
            while (_pendingChanges.TryDequeue(out _)) { }
        }
        #endregion
    }
}
