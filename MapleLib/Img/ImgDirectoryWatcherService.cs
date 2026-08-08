using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MapleLib.Img
{
    /// <summary>
    /// Type of change detected in an IMG file
    /// </summary>
    public enum ImgChangeType
    {
        /// <summary>
        /// No meaningful change detected
        /// </summary>
        None,

        /// <summary>
        /// File size differs from recorded state
        /// </summary>
        SizeChanged,

        /// <summary>
        /// File content hash differs (actual content change)
        /// </summary>
        ContentChanged,

        /// <summary>
        /// File was deleted
        /// </summary>
        Deleted,

        /// <summary>
        /// New file was added
        /// </summary>
        Added,

        /// <summary>
        /// File was renamed
        /// </summary>
        Renamed
    }

    /// <summary>
    /// Event arguments for IMG file modification events
    /// </summary>
    public class ImgFileModifiedEventArgs : EventArgs
    {
        /// <summary>
        /// Full path to the changed file
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Type of change that occurred
        /// </summary>
        public ImgChangeType ChangeType { get; }

        /// <summary>
        /// Old path (for rename events)
        /// </summary>
        public string OldPath { get; }

        /// <summary>
        /// Whether the file has local unsaved changes in HaRepacker
        /// </summary>
        public bool HasLocalChanges { get; set; }

        public ImgFileModifiedEventArgs(string filePath, ImgChangeType changeType, string oldPath = null)
        {
            FilePath = filePath;
            ChangeType = changeType;
            OldPath = oldPath;
        }
    }

    /// <summary>
    /// Tracks the state of a watched file
    /// </summary>
    internal class ImgFileState
    {
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime LastWriteTime { get; set; }
        public string ContentHash { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    /// <summary>
    /// Service that monitors opened .img directories for external changes.
    /// Designed specifically for HaRepacker to detect when external tools modify
    /// IMG files that are currently open.
    /// </summary>
    public class ImgDirectoryWatcherService : IDisposable
    {
        #region Fields
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ImgFileState> _fileStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ignorePaths = new();
        private readonly HashSet<string> _ignoreDirectories = new();
        private readonly object _ignorePathsLock = new();
        private readonly object _watchersLock = new();
        private readonly object _debounceLock = new();
        private readonly int _debounceMs;
        private readonly bool _trackContentHash;
        private readonly bool _recordInitialState;
        private volatile bool _disposed;
        #endregion

        #region Events
        /// <summary>
        /// Raised when an .img file is modified externally
        /// </summary>
        public event EventHandler<ImgFileModifiedEventArgs> ImgFileModified;

        /// <summary>
        /// Raised when an .img file is deleted while being watched
        /// </summary>
        public event EventHandler<ImgFileModifiedEventArgs> ImgFileDeleted;

        /// <summary>
        /// Raised when a new .img file is added to a watched directory
        /// </summary>
        public event EventHandler<ImgFileModifiedEventArgs> ImgFileAdded;

        /// <summary>
        /// Raised when an .img file is renamed
        /// </summary>
        public event EventHandler<ImgFileModifiedEventArgs> ImgFileRenamed;

        /// <summary>
        /// Raised when a watcher error occurs
        /// </summary>
        public event EventHandler<ErrorEventArgs> WatcherError;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new ImgDirectoryWatcherService
        /// </summary>
        /// <param name="debounceMs">Milliseconds to wait before processing changes (default 500ms)</param>
        /// <param name="trackContentHash">Whether to use MD5 hash for change detection (default true)</param>
        /// <param name="recordInitialState">Whether to recursively snapshot every existing IMG file when watching starts.</param>
        public ImgDirectoryWatcherService(
            int debounceMs = 500,
            bool trackContentHash = true,
            bool recordInitialState = true)
        {
            _debounceMs = Math.Max(0, debounceMs);
            _trackContentHash = trackContentHash;
            _recordInitialState = recordInitialState;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts watching an IMG directory for changes
        /// </summary>
        /// <param name="directoryPath">The directory path to watch</param>
        public void WatchDirectory(string directoryPath)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ImgDirectoryWatcherService));

            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                return;

            string normalizedPath = NormalizeDirectoryPath(directoryPath);
            FileSystemWatcher watcher = null;
            lock (_watchersLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ImgDirectoryWatcherService));

                if (_watchers.ContainsKey(normalizedPath))
                    return;

                try
                {
                    watcher = new FileSystemWatcher(normalizedPath)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                        Filter = "*.img",
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = false
                    };

                    watcher.Changed += OnFileChanged;
                    watcher.Created += OnFileCreated;
                    watcher.Deleted += OnFileDeleted;
                    watcher.Renamed += OnFileRenamed;
                    watcher.Error += OnWatcherError;

                    _watchers[normalizedPath] = watcher;
                    watcher.EnableRaisingEvents = true;
                }
                catch (Exception ex)
                {
                    watcher?.Dispose();
                    watcher = null;
                    System.Diagnostics.Debug.WriteLine($"Failed to create watcher for {directoryPath}: {ex.Message}");
                }
            }

            // Large extracted versions can contain tens of thousands of files and many gigabytes of data.
            // Callers that only need live FileSystemWatcher events can skip this recursive snapshot and
            // record state lazily when a file is opened or changed.
            if (watcher != null && _recordInitialState)
            {
                RecordDirectoryState(normalizedPath);
            }
        }

        /// <summary>
        /// Stops watching a specific directory
        /// </summary>
        /// <param name="directoryPath">The directory to stop watching</param>
        public void UnwatchDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;

            string normalizedPath = NormalizeDirectoryPath(directoryPath);

            FileSystemWatcher watcher = null;
            lock (_watchersLock)
            {
                _watchers.TryRemove(normalizedPath, out watcher);
            }
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            // Clean up file states for this directory
            var keysToRemove = _fileStates.Keys
                .Where(k => IsPathWithinDirectory(k, normalizedPath))
                .ToList();
            foreach (var key in keysToRemove)
            {
                _fileStates.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Stops all watchers
        /// </summary>
        public void UnwatchAll()
        {
            foreach (var path in _watchers.Keys.ToList())
            {
                UnwatchDirectory(path);
            }
            _fileStates.Clear();
        }

        /// <summary>
        /// Records the current state of a specific file
        /// </summary>
        /// <param name="filePath">The file path to record</param>
        public void RecordFileState(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                string fullPath = Path.GetFullPath(filePath);
                var fileInfo = new FileInfo(fullPath);
                var state = new ImgFileState
                {
                    FilePath = fullPath,
                    FileSize = fileInfo.Length,
                    LastWriteTime = fileInfo.LastWriteTimeUtc,
                    RecordedAt = DateTime.UtcNow
                };

                if (_trackContentHash)
                {
                    state.ContentHash = ComputeFileHash(fullPath);
                }

                _fileStates[fullPath] = state;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to record state for {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears tracking for a specific file
        /// </summary>
        /// <param name="filePath">The file path to stop tracking</param>
        public void ClearTracking(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                _fileStates.TryRemove(Path.GetFullPath(filePath), out _);
            }
        }

        /// <summary>
        /// Temporarily ignore changes for a specific path (use during save operations)
        /// </summary>
        /// <param name="filePath">The file path to ignore</param>
        public void IgnorePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            lock (_ignorePathsLock)
            {
                _ignorePaths.Add(Path.GetFullPath(filePath));
            }
        }

        /// <summary>
        /// Stop ignoring changes for a specific path
        /// </summary>
        /// <param name="filePath">The file path to stop ignoring</param>
        public void UnignorePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            lock (_ignorePathsLock)
            {
                _ignorePaths.Remove(Path.GetFullPath(filePath));
            }
        }

        /// <summary>
        /// Stop ignoring a path after a delay (for save operations)
        /// </summary>
        /// <param name="filePath">The file path to stop ignoring</param>
        /// <param name="delayMs">Delay in milliseconds</param>
        public async Task UnignorePathDelayed(string filePath, int delayMs = 500)
        {
            await Task.Delay(delayMs);
            UnignorePath(filePath);
        }

        /// <summary>
        /// Temporarily ignore all file changes in a directory
        /// </summary>
        /// <param name="directoryPath">The directory path to ignore</param>
        public void IgnoreDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;

            lock (_ignorePathsLock)
            {
                _ignoreDirectories.Add(NormalizeDirectoryPath(directoryPath));
            }
        }

        /// <summary>
        /// Stop ignoring changes in a directory
        /// </summary>
        /// <param name="directoryPath">The directory path to stop ignoring</param>
        public void UnignoreDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;

            lock (_ignorePathsLock)
            {
                _ignoreDirectories.Remove(NormalizeDirectoryPath(directoryPath));
            }
        }

        /// <summary>
        /// Stop ignoring a directory after a delay (for save operations)
        /// </summary>
        /// <param name="directoryPath">The directory path to stop ignoring</param>
        /// <param name="delayMs">Delay in milliseconds</param>
        public async Task UnignoreDirectoryDelayed(string directoryPath, int delayMs = 500)
        {
            await Task.Delay(delayMs);
            UnignoreDirectory(directoryPath);
        }

        /// <summary>
        /// Checks if a file has been modified since it was recorded
        /// </summary>
        /// <param name="filePath">The file path to check</param>
        /// <returns>The type of change detected, or None if unchanged</returns>
        public ImgChangeType GetChangeType(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return ImgChangeType.None;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch (ArgumentException)
            {
                return ImgChangeType.None;
            }

            if (!File.Exists(fullPath))
                return _fileStates.ContainsKey(fullPath) ? ImgChangeType.Deleted : ImgChangeType.None;

            if (!_fileStates.TryGetValue(fullPath, out var recordedState))
                return ImgChangeType.Added;

            try
            {
                var fileInfo = new FileInfo(fullPath);

                // Check size first (fast)
                if (fileInfo.Length != recordedState.FileSize)
                    return ImgChangeType.SizeChanged;

                // Check timestamp
                if (_trackContentHash)
                {
                    string currentHash = ComputeFileHash(fullPath);
                    if (!string.IsNullOrEmpty(currentHash) &&
                        !string.Equals(currentHash, recordedState.ContentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return ImgChangeType.ContentChanged;
                    }
                    return fileInfo.LastWriteTimeUtc != recordedState.LastWriteTime
                        ? ImgChangeType.ContentChanged
                        : ImgChangeType.None;
                }

                return fileInfo.LastWriteTimeUtc != recordedState.LastWriteTime
                    ? ImgChangeType.ContentChanged
                    : ImgChangeType.None;
            }
            catch
            {
                return ImgChangeType.None;
            }
        }

        /// <summary>
        /// Gets the list of currently watched directories
        /// </summary>
        public IReadOnlyCollection<string> WatchedDirectories => _watchers.Keys.ToList().AsReadOnly();

        /// <summary>
        /// Checks if a directory is being watched
        /// </summary>
        public bool IsWatching(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return false;

            string normalizedPath = NormalizeDirectoryPath(directoryPath);
            return _watchers.ContainsKey(normalizedPath);
        }
        #endregion

        #region Private Methods
        private void RecordDirectoryState(string directoryPath)
        {
            try
            {
                var imgFiles = Directory.GetFiles(directoryPath, "*.img", SearchOption.AllDirectories);
                foreach (var file in imgFiles)
                {
                    RecordFileState(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to record directory state for {directoryPath}: {ex.Message}");
            }
        }

        private string ComputeFileHash(string filePath)
        {
            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(filePath);
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        private bool IsPathIgnored(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string fullPath = Path.GetFullPath(filePath);

            lock (_ignorePathsLock)
            {
                // Check if exact path is ignored
                if (_ignorePaths.Contains(fullPath))
                    return true;

                // Check if file is in an ignored directory
                foreach (var ignoredDir in _ignoreDirectories)
                    if (IsPathWithinDirectory(fullPath, ignoredDir))
                        return true;

                return false;
            }
        }

        private bool IsFileWriteComplete(string path)
        {
            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void ProcessChangeWithDebounce(string filePath, Action<string> processAction)
        {
            Timer timer;
            lock (_debounceLock)
            {
                if (_disposed)
                    return;

                // Cancel existing timer for this file
                if (_debounceTimers.TryRemove(filePath, out var existingTimer))
                    existingTimer.Dispose();

                // Create a disabled timer, publish it, then arm it below. This
                // closes the zero-delay race where the callback could run
                // before the dictionary assignment.
                timer = null;
                timer = new Timer(_ =>
                {
                    // A disposed timer may still have a callback queued. It must
                    // not remove or execute in place of a newer timer for this
                    // path, and callbacks racing with Dispose must be harmless.
                    if (_disposed)
                        return;

                    var pair = new KeyValuePair<string, Timer>(filePath, timer);
                    if (!((ICollection<KeyValuePair<string, Timer>>)_debounceTimers).Remove(pair))
                        return;

                    try
                    {
                        if (!_disposed)
                            processAction(filePath);
                    }
                    finally
                    {
                        timer.Dispose();
                    }
                }, null, Timeout.Infinite, Timeout.Infinite);

                _debounceTimers[filePath] = timer;
            }

            try
            {
                timer.Change(_debounceMs, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                _debounceTimers.TryRemove(filePath, out _);
            }
        }

        private static string NormalizeDirectoryPath(string directoryPath)
        {
            string fullPath = Path.GetFullPath(directoryPath);
            string root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) && fullPath.Length <= root.Length)
                return fullPath;

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsPathWithinDirectory(string filePath, string directoryPath)
        {
            if (string.Equals(filePath, directoryPath, StringComparison.OrdinalIgnoreCase))
                return true;

            string separator = Path.EndsInDirectorySeparator(directoryPath)
                ? string.Empty
                : Path.DirectorySeparatorChar.ToString();
            return filePath.StartsWith(directoryPath + separator, StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region Event Handlers
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            string fullPath = Path.GetFullPath(e.FullPath);
            if (_disposed || IsPathIgnored(fullPath))
                return;

            ProcessChangeWithDebounce(fullPath, path =>
            {
                // Wait for file write to complete
                int retries = 0;
                while (!IsFileWriteComplete(path) && retries < 10)
                {
                    Thread.Sleep(100);
                    retries++;
                }

                var changeType = GetChangeType(path);
                if (changeType != ImgChangeType.None)
                {
                    ImgFileModified?.Invoke(this, new ImgFileModifiedEventArgs(path, changeType));
                }
            });
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            string fullPath = Path.GetFullPath(e.FullPath);
            if (_disposed || IsPathIgnored(fullPath))
                return;

            ProcessChangeWithDebounce(fullPath, path =>
            {
                // Wait for file write to complete
                int retries = 0;
                while (!IsFileWriteComplete(path) && retries < 10)
                {
                    Thread.Sleep(100);
                    retries++;
                }

                RecordFileState(path);
                ImgFileAdded?.Invoke(this, new ImgFileModifiedEventArgs(path, ImgChangeType.Added));
            });
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            string fullPath = Path.GetFullPath(e.FullPath);
            if (_disposed || IsPathIgnored(fullPath))
                return;

            if (_debounceTimers.TryRemove(fullPath, out var pendingTimer))
                pendingTimer.Dispose();

            // No debounce for deletes - process immediately. A file may not have a cached state when the
            // watcher uses lazy state tracking, but the FileSystemWatcher event is still authoritative.
            _fileStates.TryRemove(fullPath, out _);
            ImgFileDeleted?.Invoke(this, new ImgFileModifiedEventArgs(fullPath, ImgChangeType.Deleted));
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            string fullPath = Path.GetFullPath(e.FullPath);
            string oldFullPath = Path.GetFullPath(e.OldFullPath);
            if (_disposed || IsPathIgnored(fullPath) || IsPathIgnored(oldFullPath))
                return;

            // Update file state tracking
            if (_fileStates.TryRemove(oldFullPath, out var oldState))
            {
                oldState.FilePath = fullPath;
                _fileStates[fullPath] = oldState;
            }

            ImgFileRenamed?.Invoke(this, new ImgFileModifiedEventArgs(fullPath, ImgChangeType.Renamed, oldFullPath));
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            WatcherError?.Invoke(this, e);

            // Try to recover by re-creating the watcher
            var watcher = sender as FileSystemWatcher;
            if (watcher != null)
            {
                string path = watcher.Path;
                UnwatchDirectory(path);

                // Attempt to recreate
                try
                {
                    WatchDirectory(path);
                }
                catch
                {
                    // Failed to recover, ignore
                }
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            List<FileSystemWatcher> watchers;
            lock (_watchersLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                watchers = _watchers.Values.ToList();
                _watchers.Clear();
            }

            // Dispose all debounce timers
            lock (_debounceLock)
            {
                foreach (var timer in _debounceTimers.Values)
                    timer.Dispose();
                _debounceTimers.Clear();
            }

            // Dispose all watchers
            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _fileStates.Clear();

            lock (_ignorePathsLock)
            {
                _ignorePaths.Clear();
                _ignoreDirectories.Clear();
            }
        }
        #endregion
    }
}
