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
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
        private readonly ConcurrentDictionary<string, ImgFileState> _fileStates = new();
        private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new();
        private readonly HashSet<string> _ignorePaths = new();
        private readonly HashSet<string> _ignoreDirectories = new();
        private readonly object _ignorePathsLock = new();
        private readonly int _debounceMs;
        private readonly bool _trackContentHash;
        private bool _disposed;
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
        public ImgDirectoryWatcherService(int debounceMs = 500, bool trackContentHash = true)
        {
            _debounceMs = debounceMs;
            _trackContentHash = trackContentHash;
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

            string normalizedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar);

            if (_watchers.ContainsKey(normalizedPath))
                return;

            try
            {
                var watcher = new FileSystemWatcher(normalizedPath)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    Filter = "*.img",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileCreated;
                watcher.Deleted += OnFileDeleted;
                watcher.Renamed += OnFileRenamed;
                watcher.Error += OnWatcherError;

                _watchers[normalizedPath] = watcher;

                // Record initial state of all .img files in the directory
                RecordDirectoryState(normalizedPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create watcher for {directoryPath}: {ex.Message}");
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

            string normalizedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar);

            if (_watchers.TryRemove(normalizedPath, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            // Clean up file states for this directory
            var keysToRemove = _fileStates.Keys.Where(k => k.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)).ToList();
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
                var fileInfo = new FileInfo(filePath);
                var state = new ImgFileState
                {
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    LastWriteTime = fileInfo.LastWriteTimeUtc,
                    RecordedAt = DateTime.UtcNow
                };

                if (_trackContentHash)
                {
                    state.ContentHash = ComputeFileHash(filePath);
                }

                _fileStates[filePath] = state;
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
                _fileStates.TryRemove(filePath, out _);
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
                _ignoreDirectories.Add(Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar));
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
                _ignoreDirectories.Remove(Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar));
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

            if (!File.Exists(filePath))
                return _fileStates.ContainsKey(filePath) ? ImgChangeType.Deleted : ImgChangeType.None;

            if (!_fileStates.TryGetValue(filePath, out var recordedState))
                return ImgChangeType.Added;

            try
            {
                var fileInfo = new FileInfo(filePath);

                // Check size first (fast)
                if (fileInfo.Length != recordedState.FileSize)
                    return ImgChangeType.SizeChanged;

                // Check timestamp
                if (fileInfo.LastWriteTimeUtc != recordedState.LastWriteTime)
                {
                    // If tracking content hash, verify actual content change
                    if (_trackContentHash)
                    {
                        string currentHash = ComputeFileHash(filePath);
                        if (currentHash != recordedState.ContentHash)
                            return ImgChangeType.ContentChanged;
                    }
                    else
                    {
                        return ImgChangeType.ContentChanged;
                    }
                }

                return ImgChangeType.None;
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

            string normalizedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar);
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
                {
                    if (fullPath.StartsWith(ignoredDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

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
            // Cancel existing timer for this file
            if (_debounceTimers.TryRemove(filePath, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            // Create new debounce timer
            var timer = new Timer(_ =>
            {
                _debounceTimers.TryRemove(filePath, out Timer _);
                processAction(filePath);
            }, null, _debounceMs, Timeout.Infinite);

            _debounceTimers[filePath] = timer;
        }
        #endregion

        #region Event Handlers
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_disposed || IsPathIgnored(e.FullPath))
                return;

            ProcessChangeWithDebounce(e.FullPath, path =>
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
            if (_disposed || IsPathIgnored(e.FullPath))
                return;

            ProcessChangeWithDebounce(e.FullPath, path =>
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
            if (_disposed || IsPathIgnored(e.FullPath))
                return;

            // No debounce for deletes - process immediately
            if (_fileStates.TryRemove(e.FullPath, out _))
            {
                ImgFileDeleted?.Invoke(this, new ImgFileModifiedEventArgs(e.FullPath, ImgChangeType.Deleted));
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_disposed || IsPathIgnored(e.FullPath) || IsPathIgnored(e.OldFullPath))
                return;

            // Update file state tracking
            if (_fileStates.TryRemove(e.OldFullPath, out var oldState))
            {
                oldState.FilePath = e.FullPath;
                _fileStates[e.FullPath] = oldState;
            }

            ImgFileRenamed?.Invoke(this, new ImgFileModifiedEventArgs(e.FullPath, ImgChangeType.Renamed, e.OldFullPath));
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
            if (_disposed)
                return;

            _disposed = true;

            // Dispose all debounce timers
            foreach (var timer in _debounceTimers.Values)
            {
                timer.Dispose();
            }
            _debounceTimers.Clear();

            // Dispose all watchers
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();

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
