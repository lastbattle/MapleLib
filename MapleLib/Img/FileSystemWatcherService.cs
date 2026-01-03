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
        private readonly object _timerLock = new();
        private bool _disposed;
        private bool _isProcessing;
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
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileSystemWatcherService));

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            // Normalize path
            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

            // Don't add duplicate watchers
            if (_watchers.ContainsKey(normalizedPath))
                return;

            try
            {
                var watcher = new FileSystemWatcher(normalizedPath);

                switch (watchType)
                {
                    case WatchType.Category:
                        // Watch for .img files in this category and subdirectories
                        watcher.Filter = "*.img";
                        watcher.IncludeSubdirectories = true;
                        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

                        // Store category name
                        _categoryPaths[normalizedPath] = categoryName ?? Path.GetFileName(normalizedPath);
                        break;

                    case WatchType.VersionRoot:
                        // Watch for directory changes (new/deleted versions)
                        watcher.Filter = "*";
                        watcher.IncludeSubdirectories = false;
                        watcher.NotifyFilter = NotifyFilters.DirectoryName;
                        break;
                }

                // Subscribe to events
                watcher.Created += OnFileSystemEvent;
                watcher.Deleted += OnFileSystemEvent;
                watcher.Changed += OnFileSystemEvent;
                watcher.Renamed += OnFileSystemRenamed;
                watcher.Error += OnWatcherError;

                // Enable the watcher
                watcher.EnableRaisingEvents = true;

                // Store watcher and its type
                _watchers[normalizedPath] = watcher;
                _watchTypes[normalizedPath] = watchType;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create watcher for {path}: {ex.Message}");
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

            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

            if (_watchers.TryRemove(normalizedPath, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchTypes.TryRemove(normalizedPath, out _);
            _categoryPaths.TryRemove(normalizedPath, out _);
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

            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            return _watchers.ContainsKey(normalizedPath);
        }
        #endregion

        #region Event Handlers
        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            if (_disposed)
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
            if (_disposed)
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
                    // Re-create the watcher
                    UnwatchPath(path);

                    string category = null;
                    _categoryPaths.TryGetValue(path, out category);

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
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(ProcessPendingChanges, null, _debounceMs, Timeout.Infinite);
            }
        }

        private void ProcessPendingChanges(object state)
        {
            if (_disposed || _isProcessing)
                return;

            _isProcessing = true;

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
                    .GroupBy(c => c.Path)
                    .Select(g => g.OrderByDescending(c => c.Timestamp).First())
                    .ToList();

                foreach (var change in uniqueChanges)
                {
                    RaiseAppropriateEvent(change);
                }
            }
            finally
            {
                _isProcessing = false;
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
            if (_disposed)
                return;

            _disposed = true;

            lock (_timerLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _watchTypes.Clear();
            _categoryPaths.Clear();

            // Clear pending changes
            while (_pendingChanges.TryDequeue(out _)) { }
        }
        #endregion
    }
}
