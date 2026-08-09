using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MapleLib.Img
{
    /// <summary>
    /// Manages multiple MapleStory versions stored in the IMG filesystem structure.
    /// Provides version discovery, validation, and selection capabilities.
    /// </summary>
    public class VersionManager
    {
        #region Constants
        private const string MANIFEST_FILENAME = "manifest.json";

        /// <summary>
        /// Required categories that must exist for a valid extraction
        /// </summary>
        public static readonly string[] REQUIRED_CATEGORIES = new[]
        {
            "String", "Map"
        };

        /// <summary>
        /// Standard categories expected in a typical extraction
        /// </summary>
        public static readonly string[] STANDARD_CATEGORIES = new[]
        {
            "Base", "String", "Map", "Mob", "Npc", "Reactor", "Sound", "Skill",
            "Character", "Item", "UI", "Effect", "Etc", "Quest", "Morph", "TamingMob", "List"
        };
        #endregion

        #region Fields
        private readonly string _rootPath;
        private readonly object _stateLock = new();
        private readonly List<VersionInfo> _availableVersions = new();
        private bool _scanned;

        // Hot swap
        private FileSystemWatcherService _watcherService;
        private bool _hotSwapEnabled;
        private long _watcherGeneration;
        private readonly List<string> _additionalWatchPaths = new();
        #endregion

        #region Events
        /// <summary>
        /// Raised when the list of available versions changes
        /// </summary>
        public event EventHandler<VersionsChangedEventArgs> VersionsChanged;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the root path where versions are stored
        /// </summary>
        public string RootPath => _rootPath;

        /// <summary>
        /// Gets the list of available versions
        /// </summary>
        public IReadOnlyList<VersionInfo> AvailableVersions
        {
            get
            {
                bool scanned;
                lock (_stateLock)
                {
                    scanned = _scanned;
                }

                if (!scanned)
                    ScanVersions();

                // Never expose the backing list.  A read-only wrapper around the
                // mutable list would still change underneath callers while a
                // scan or watcher callback is replacing its contents.
                lock (_stateLock)
                {
                    return _availableVersions.ToList().AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the count of available versions
        /// </summary>
        public int VersionCount => AvailableVersions.Count;

        /// <summary>
        /// Gets whether hot swap (file system watching) is enabled
        /// </summary>
        public bool HotSwapEnabled
        {
            get
            {
                lock (_stateLock)
                {
                    return _hotSwapEnabled;
                }
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new VersionManager for the specified root directory
        /// </summary>
        /// <param name="rootPath">The root directory containing version folders</param>
        public VersionManager(string rootPath)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));

            if (!Directory.Exists(rootPath))
            {
                Directory.CreateDirectory(rootPath);
            }
        }
        #endregion

        #region Version Discovery
        /// <summary>
        /// Scans the root directory for available versions
        /// </summary>
        /// <returns>List of discovered versions</returns>
        public List<VersionInfo> ScanVersions()
        {
            // Take a state snapshot before doing filesystem I/O.  In particular,
            // do not hold the state lock while manifests and directories are read.
            List<VersionInfo> baseline;
            lock (_stateLock)
            {
                baseline = _availableVersions.ToList();
            }

            var baselineSet = new HashSet<VersionInfo>(baseline);
            var externalVersions = baseline.Where(v => v.IsExternal).ToList();
            var discoveredVersions = new List<VersionInfo>();

            if (Directory.Exists(_rootPath))
            {
                foreach (var dir in HaCreatorPaths.EnumerateDirectoriesExcludingBackups(_rootPath))
                {
                    var versionInfo = LoadVersionManifest(dir);
                    if (versionInfo != null)
                    {
                        discoveredVersions.Add(versionInfo);
                    }
                }
            }

            // Preserve external versions and any versions added by a watcher while
            // this scan was reading the filesystem.  Directory checks are kept
            // outside the lock so a slow or unavailable filesystem cannot block
            // readers and hot-swap callbacks.
            List<VersionInfo> concurrentVersions;
            lock (_stateLock)
            {
                concurrentVersions = _availableVersions
                    .Where(v => !baselineSet.Contains(v))
                    .ToList();
            }

            var retainedVersions = externalVersions
                .Concat(concurrentVersions)
                .Where(v => Directory.Exists(v.DirectoryPath))
                .ToList();

            lock (_stateLock)
            {
                _availableVersions.Clear();
                _availableVersions.AddRange(discoveredVersions);

                foreach (var retainedVersion in retainedVersions)
                {
                    if (!_availableVersions.Any(v =>
                        string.Equals(v.DirectoryPath, retainedVersion.DirectoryPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        _availableVersions.Add(retainedVersion);
                    }
                }

                _availableVersions.Sort((a, b) => string.Compare(a.Version, b.Version, StringComparison.OrdinalIgnoreCase));
                _scanned = true;
                return new List<VersionInfo>(_availableVersions);
            }
        }

        /// <summary>
        /// Refreshes the version list
        /// </summary>
        public void Refresh()
        {
            lock (_stateLock)
            {
                _scanned = false;
            }
            ScanVersions();
        }

        /// <summary>
        /// Gets a version by its identifier
        /// </summary>
        public VersionInfo GetVersion(string versionId)
        {
            return AvailableVersions.FirstOrDefault(v =>
                string.Equals(v.Version, versionId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if a version exists
        /// </summary>
        public bool VersionExists(string versionId)
        {
            return GetVersion(versionId) != null;
        }
        #endregion

        #region Manifest Management
        /// <summary>
        /// Loads a version manifest from a directory
        /// </summary>
        public VersionInfo LoadVersionManifest(string versionPath)
        {
            if (HaCreatorPaths.IsBackupsDirectory(versionPath))
                return null;

            string manifestPath = Path.Combine(versionPath, MANIFEST_FILENAME);

            VersionInfo versionInfo;

            if (File.Exists(manifestPath))
            {
                try
                {
                    MemoryLimits.EnsureFileSize(manifestPath, MemoryLimits.MAX_METADATA_JSON_BYTES, "IMG version manifest");
                    string json = File.ReadAllText(manifestPath);
                    versionInfo = JsonSerializer.Deserialize(json, MapleJsonContext.Default.VersionInfo);
                    versionInfo.DirectoryPath = versionPath;
                    InferAndPersistVUpdate(versionInfo);
                }
                catch (Exception)
                {
                    // Create basic info from directory if manifest is corrupt
                    versionInfo = CreateBasicVersionInfo(versionPath);
                }
            }
            else
            {
                // Create basic info from directory structure
                versionInfo = CreateBasicVersionInfo(versionPath);
            }

            // Validate the version
            ValidateVersion(versionInfo);

            return versionInfo;
        }

        /// <summary>
        /// Creates basic version info from directory structure
        /// </summary>
        private VersionInfo CreateBasicVersionInfo(string versionPath)
        {
            string versionName = Path.GetFileName(versionPath);

            var versionInfo = new VersionInfo
            {
                Version = versionName,
                DisplayName = versionName,
                DirectoryPath = versionPath,
                ExtractedDate = Directory.GetCreationTime(versionPath),
                Encryption = WzMapleVersion.BMS.ToString()
            };
            versionInfo.IsVUpdate = DetectVUpdateFromImgDirectory(versionPath);

            // Scan for categories
            foreach (var dir in HaCreatorPaths.EnumerateDirectoriesExcludingBackups(versionPath))
            {
                string categoryName = Path.GetFileName(dir);
                int fileCount = HaCreatorPaths.EnumerateFilesExcludingBackups(
                    dir,
                    "*.img",
                    SearchOption.AllDirectories).Count();

                if (fileCount > 0)
                {
                    versionInfo.Categories[categoryName] = new CategoryInfo
                    {
                        FileCount = fileCount,
                        LastModified = Directory.GetLastWriteTime(dir)
                    };
                }
            }

            return versionInfo;
        }

        /// <summary>
        /// Saves a version manifest to disk
        /// </summary>
        public void SaveVersionManifest(VersionInfo versionInfo)
        {
            if (string.IsNullOrEmpty(versionInfo.DirectoryPath))
                throw new ArgumentException("VersionInfo must have a DirectoryPath set");

            string manifestPath = Path.Combine(versionInfo.DirectoryPath, MANIFEST_FILENAME);

            string json = JsonSerializer.Serialize(versionInfo, MapleJsonContext.Default.VersionInfo);
            File.WriteAllText(manifestPath, json);
        }

        /// <summary>
        /// Creates a new version manifest
        /// </summary>
        public VersionInfo CreateVersionManifest(
            string versionPath,
            string versionId,
            string displayName,
            WzMapleVersion encryption,
            bool is64Bit = false,
            bool isPreBB = false,
            int patchVersion = 0)
        {
            var versionInfo = new VersionInfo
            {
                Version = versionId,
                DisplayName = displayName,
                DirectoryPath = versionPath,
                ExtractedDate = DateTime.Now,
                Encryption = encryption.ToString(),
                Is64Bit = is64Bit,
                IsPreBB = isPreBB,
                PatchVersion = patchVersion
            };

            // Ensure directory exists
            if (!Directory.Exists(versionPath))
            {
                Directory.CreateDirectory(versionPath);
            }

            SaveVersionManifest(versionInfo);
            return versionInfo;
        }
        #endregion

        #region Validation
        /// <summary>
        /// Validates a version's integrity
        /// </summary>
        public bool ValidateVersion(VersionInfo versionInfo)
        {
            versionInfo.ValidationErrors.Clear();
            versionInfo.IsValid = true;

            // Check directory exists
            if (!Directory.Exists(versionInfo.DirectoryPath))
            {
                versionInfo.ValidationErrors.Add($"Directory not found: {versionInfo.DirectoryPath}");
                versionInfo.IsValid = false;
                return false;
            }

            // Check required categories
            foreach (var category in REQUIRED_CATEGORIES)
            {
                string categoryPath = Path.Combine(versionInfo.DirectoryPath, category);
                if (!Directory.Exists(categoryPath))
                {
                    versionInfo.ValidationErrors.Add($"Required category missing: {category}");
                    versionInfo.IsValid = false;
                }
                else
                {
                    // Check for at least one .img file
                    bool hasImgFiles = HaCreatorPaths.EnumerateFilesExcludingBackups(
                        categoryPath,
                        "*.img",
                        SearchOption.AllDirectories).Any();
                    if (!hasImgFiles)
                    {
                        versionInfo.ValidationErrors.Add($"Category '{category}' has no .img files");
                        versionInfo.IsValid = false;
                    }
                }
            }

            // Check for String/Map.img specifically
            string mapStringPath = Path.Combine(versionInfo.DirectoryPath, "String", "Map.img");
            if (!File.Exists(mapStringPath))
            {
                versionInfo.ValidationErrors.Add("String/Map.img not found - required for map names");
                versionInfo.IsValid = false;
            }

            return versionInfo.IsValid;
        }

        /// <summary>
        /// Validates a version by its ID
        /// </summary>
        public bool ValidateVersion(string versionId)
        {
            var version = GetVersion(versionId);
            if (version == null)
                return false;
            return ValidateVersion(version);
        }

        /// <summary>
        /// Gets a detailed validation report for a version
        /// </summary>
        public ValidationReport GetValidationReport(VersionInfo versionInfo)
        {
            var report = new ValidationReport
            {
                VersionId = versionInfo.Version,
                DirectoryPath = versionInfo.DirectoryPath,
                CheckedAt = DateTime.Now
            };

            // Check each standard category
            foreach (var category in STANDARD_CATEGORIES)
            {
                string categoryPath = Path.Combine(versionInfo.DirectoryPath, category);
                var categoryReport = new CategoryValidationResult
                {
                    CategoryName = category,
                    IsRequired = REQUIRED_CATEGORIES.Contains(category)
                };

                if (Directory.Exists(categoryPath))
                {
                    categoryReport.Exists = true;
                    categoryReport.FileCount = HaCreatorPaths.EnumerateFilesExcludingBackups(
                        categoryPath,
                        "*.img",
                        SearchOption.AllDirectories).Count();
                    categoryReport.TotalSize = HaCreatorPaths.EnumerateFilesExcludingBackups(
                                                            categoryPath,
                                                            "*.img",
                                                            SearchOption.AllDirectories)
                                                        .Sum(f => new FileInfo(f).Length);
                }

                report.Categories.Add(categoryReport);
            }

            report.IsValid = report.Categories
                .Where(c => c.IsRequired)
                .All(c => c.Exists && c.FileCount > 0);

            return report;
        }
        #endregion

        #region Version Operations
        /// <summary>
        /// Adds an external version from any path (not in the standard versions folder)
        /// </summary>
        /// <param name="versionPath">Path to the version folder</param>
        /// <returns>The added VersionInfo, or null if failed</returns>
        public VersionInfo AddExternalVersion(string versionPath)
        {
            if (!Directory.Exists(versionPath))
                return null;

            // Load the version info
            var versionInfo = LoadVersionManifest(versionPath);
            if (versionInfo == null)
                return null;

            // Mark as external
            versionInfo.IsExternal = true;

            lock (_stateLock)
            {
                // The load is intentionally outside the lock, but duplicate
                // detection and publication must be one atomic operation.
                if (_availableVersions.Any(v =>
                    string.Equals(v.DirectoryPath, versionPath, StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }

                _availableVersions.Add(versionInfo);
                _availableVersions.Sort((a, b) => string.Compare(a.Version, b.Version, StringComparison.OrdinalIgnoreCase));
            }

            return versionInfo;
        }

        /// <summary>
        /// Detects the V Update UI family in an IMG filesystem version.
        /// This keeps manifests created before the isVUpdate field was added compatible.
        /// </summary>
        public static bool DetectVUpdateFromImgDirectory(string versionPath)
        {
            if (string.IsNullOrWhiteSpace(versionPath))
                return false;

            return File.Exists(Path.Combine(versionPath, "UI", "StatusBar3.img"));
        }

        /// <summary>
        /// Upgrades an older IMG manifest when StatusBar3.img proves that it is a
        /// V Update version. Persistence is best-effort so read-only versions can
        /// still be opened with the correctly inferred in-memory flag.
        /// </summary>
        public static bool InferAndPersistVUpdate(VersionInfo versionInfo)
        {
            if (versionInfo == null || versionInfo.IsVUpdate ||
                !DetectVUpdateFromImgDirectory(versionInfo.DirectoryPath))
            {
                return false;
            }

            versionInfo.IsVUpdate = true;

            try
            {
                string manifestPath = Path.Combine(versionInfo.DirectoryPath, MANIFEST_FILENAME);
                if (File.Exists(manifestPath))
                {
                    string json = JsonSerializer.Serialize(versionInfo, MapleJsonContext.Default.VersionInfo);
                    File.WriteAllText(manifestPath, json);
                }
            }
            catch (IOException)
            {
                // Keep the inferred in-memory value when the manifest is read-only.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the inferred in-memory value when the manifest is read-only.
            }

            return true;
        }

        /// <summary>
        /// Deletes a version and all its files
        /// </summary>
        public bool DeleteVersion(string versionId)
        {
            var version = GetVersion(versionId);
            if (version == null)
                return false;

            try
            {
                string versionPath = version.DirectoryPath;
                Directory.Delete(versionPath, recursive: true);

                lock (_stateLock)
                {
                    // A concurrent refresh can replace the VersionInfo instance;
                    // remove by path as well as by reference in that case.
                    _availableVersions.RemoveAll(v =>
                        ReferenceEquals(v, version) ||
                        string.Equals(v.DirectoryPath, versionPath, StringComparison.OrdinalIgnoreCase));
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Renames a version folder and updates its manifest metadata.
        /// </summary>
        public bool RenameVersion(string oldVersionId, string newVersionId)
        {
            return RenameVersion(oldVersionId, newVersionId, newVersionId, out _);
        }

        /// <summary>
        /// Renames a version folder and updates its manifest metadata.
        /// </summary>
        public bool RenameVersion(string oldVersionId, string newVersionId, string displayName, out VersionInfo renamedVersion)
        {
            renamedVersion = null;

            var version = GetVersion(oldVersionId);
            if (version == null)
                return false;

            lock (_stateLock)
            {
                if (_availableVersions.Any(v =>
                    !ReferenceEquals(v, version) &&
                    string.Equals(v.Version, newVersionId, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            try
            {
                string parentPath = Path.GetDirectoryName(version.DirectoryPath);
                if (string.IsNullOrEmpty(parentPath))
                    return false;

                if (string.IsNullOrWhiteSpace(newVersionId) ||
                    newVersionId is "." or ".." ||
                    Path.IsPathRooted(newVersionId) ||
                    newVersionId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return false;

                string fullParentPath = Path.GetFullPath(parentPath);
                string newPath = Path.GetFullPath(Path.Combine(fullParentPath, newVersionId));
                string newParentPath = Path.GetDirectoryName(newPath);
                if (!string.Equals(fullParentPath, newParentPath, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!version.DirectoryPath.Equals(newPath, StringComparison.OrdinalIgnoreCase) && Directory.Exists(newPath))
                    return false;

                if (version.DirectoryPath.Equals(newPath, StringComparison.OrdinalIgnoreCase) &&
                    !version.DirectoryPath.Equals(newPath, StringComparison.Ordinal))
                {
                    string tempPath = Path.Combine(parentPath, $"{newVersionId}_{Guid.NewGuid():N}.tmp");
                    Directory.Move(version.DirectoryPath, tempPath);
                    Directory.Move(tempPath, newPath);
                }
                else if (!version.DirectoryPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Move(version.DirectoryPath, newPath);
                }

                version.Version = newVersionId;
                version.DisplayName = string.IsNullOrWhiteSpace(displayName) ? newVersionId : displayName;
                version.DirectoryPath = newPath;
                SaveVersionManifest(version);

                lock (_stateLock)
                {
                    _availableVersions.Sort((a, b) => string.Compare(a.Version, b.Version, StringComparison.OrdinalIgnoreCase));
                    renamedVersion = version;
                }

                // Event handlers may call back into VersionManager, so invoke
                // them only after releasing the state lock.
                OnVersionsChanged(VersionChangeType.Refreshed, version);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates an ImgFileSystemManager for a version
        /// </summary>
        public ImgFileSystemManager CreateManager(string versionId, HaCreatorConfig config = null)
        {
            var version = GetVersion(versionId);
            if (version == null)
                throw new ArgumentException($"Version not found: {versionId}");

            return new ImgFileSystemManager(version.DirectoryPath, config);
        }

        /// <summary>
        /// Creates an ImgFileSystemManager for a version
        /// </summary>
        public ImgFileSystemManager CreateManager(VersionInfo version, HaCreatorConfig config = null)
        {
            return new ImgFileSystemManager(version.DirectoryPath, config);
        }
        #endregion

        #region Hot Swap
        /// <summary>
        /// Enables or disables hot swap (file system watching) for version directories
        /// </summary>
        /// <param name="enable">True to enable, false to disable</param>
        /// <param name="debounceMs">Debounce delay in milliseconds (default 500)</param>
        /// <param name="additionalPaths">Additional paths to watch (e.g., from config.AdditionalVersionPaths)</param>
        public void EnableHotSwap(bool enable, int debounceMs = 500, IEnumerable<string> additionalPaths = null)
        {
            if (enable)
            {
                long generation;
                lock (_stateLock)
                {
                    if (_hotSwapEnabled)
                        return;

                    if (debounceMs < 0)
                        throw new ArgumentOutOfRangeException(nameof(debounceMs));

                    // Publish the desired state before doing I/O.  This gives a
                    // concurrent disable operation a linearization point and
                    // lets it invalidate an in-progress initialization.
                    _hotSwapEnabled = true;
                    generation = ++_watcherGeneration;
                }

                InitializeFileWatchers(debounceMs, additionalPaths, generation);
            }
            else
            {
                DisposeFileWatchers();
            }
        }

        /// <summary>
        /// Adds an additional path to watch for version directories
        /// </summary>
        /// <param name="path">The path to watch</param>
        public void AddWatchPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            string normalizedPath;
            try
            {
                normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            }
            catch (Exception)
            {
                return;
            }

            FileSystemWatcherService watcher;
            lock (_stateLock)
            {
                if (!_hotSwapEnabled || _watcherService == null ||
                    _additionalWatchPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
                {
                    return;
                }

                watcher = _watcherService;
                _additionalWatchPaths.Add(normalizedPath);
            }

            try
            {
                watcher.WatchPath(normalizedPath, WatchType.VersionRoot);
            }
            catch (ObjectDisposedException)
            {
                lock (_stateLock)
                {
                    if (ReferenceEquals(_watcherService, watcher))
                    {
                        _additionalWatchPaths.RemoveAll(p =>
                            p.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
        }

        /// <summary>
        /// Removes a path from watching
        /// </summary>
        /// <param name="path">The path to stop watching</param>
        public void RemoveWatchPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string normalizedPath;
            try
            {
                normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            }
            catch (Exception)
            {
                return;
            }

            FileSystemWatcherService watcher;
            lock (_stateLock)
            {
                if (!_hotSwapEnabled || _watcherService == null)
                    return;

                watcher = _watcherService;
                _additionalWatchPaths.RemoveAll(p =>
                    p.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            }

            watcher.UnwatchPath(normalizedPath);
        }

        /// <summary>
        /// Initializes file system watchers
        /// </summary>
        private void InitializeFileWatchers(int debounceMs, IEnumerable<string> additionalPaths, long generation)
        {
            var watcher = new FileSystemWatcherService(debounceMs);
            var pathsToStore = new List<string>();

            watcher.VersionDirectoryChanged += OnVersionDirectoryChanged;
            watcher.WatcherError += OnWatcherError;

            try
            {
                // Watch the root versions path
                if (Directory.Exists(_rootPath))
                {
                    watcher.WatchPath(_rootPath, WatchType.VersionRoot);
                }

                // Watch additional paths
                if (additionalPaths != null)
                {
                    foreach (var path in additionalPaths)
                    {
                        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                            continue;

                        string normalizedPath;
                        try
                        {
                            normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        if (!pathsToStore.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
                        {
                            pathsToStore.Add(normalizedPath);
                            // Watch the parent directory of each additional version path
                            string parentPath = Path.GetDirectoryName(normalizedPath);
                            if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
                            {
                                watcher.WatchPath(parentPath, WatchType.VersionRoot);
                            }
                        }
                    }
                }

                bool publish;
                lock (_stateLock)
                {
                    publish = _hotSwapEnabled && generation == _watcherGeneration &&
                        _watcherService == null;

                    if (publish)
                    {
                        _watcherService = watcher;
                        _additionalWatchPaths.Clear();
                        _additionalWatchPaths.AddRange(pathsToStore);
                    }
                }

                if (!publish)
                {
                    DisposeWatcher(watcher);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"VersionManager hot swap enabled for {watcher.WatchedPaths.Count} paths");
            }
            catch (Exception)
            {
                bool published;

                lock (_stateLock)
                {
                    published = ReferenceEquals(_watcherService, watcher);
                    if (published)
                    {
                        _watcherService = null;
                        _additionalWatchPaths.Clear();
                        _hotSwapEnabled = false;
                        ++_watcherGeneration;
                    }

                    if (generation == _watcherGeneration && !published)
                    {
                        _hotSwapEnabled = false;
                        ++_watcherGeneration;
                    }
                }

                DisposeWatcher(watcher);
            }
        }

        /// <summary>
        /// Disposes file system watchers
        /// </summary>
        private void DisposeFileWatchers()
        {
            FileSystemWatcherService watcher;
            lock (_stateLock)
            {
                if (!_hotSwapEnabled && _watcherService == null)
                    return;

                _hotSwapEnabled = false;
                ++_watcherGeneration;
                watcher = _watcherService;
                _watcherService = null;
                _additionalWatchPaths.Clear();
            }

            // Detaching and disposing can block on watcher callbacks.  Do that
            // outside the state lock so callers and callbacks cannot deadlock.
            if (watcher != null)
            {
                DisposeWatcher(watcher);
            }

            System.Diagnostics.Debug.WriteLine("VersionManager hot swap disabled");
        }

        private void DisposeWatcher(FileSystemWatcherService watcher)
        {
            watcher.VersionDirectoryChanged -= OnVersionDirectoryChanged;
            watcher.WatcherError -= OnWatcherError;
            watcher.Dispose();
        }

        /// <summary>
        /// Handles version directory change events
        /// </summary>
        private void OnVersionDirectoryChanged(object sender, VersionDirectoryChangedEventArgs e)
        {
            try
            {
                // A disposed/replaced watcher can still have a callback queued.
                // Ignore callbacks that no longer belong to the active service.
                if (!IsActiveWatcher(sender))
                    return;

                VersionChangeType? changeType = null;
                VersionInfo affectedVersion = null;

                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        // A new directory was created - check if it's a valid version
                        if (Directory.Exists(e.VersionPath))
                        {
                            // Wait a moment for files to be copied (for drag-drop scenarios)
                            System.Threading.Thread.Sleep(100);

                            // Try to load as a version
                            var newVersion = LoadVersionManifest(e.VersionPath);
                            if (newVersion != null && newVersion.IsValid)
                            {
                                bool added;
                                lock (_stateLock)
                                {
                                    added = _hotSwapEnabled && ReferenceEquals(_watcherService, sender) &&
                                        !_availableVersions.Any(v =>
                                            string.Equals(v.DirectoryPath, e.VersionPath, StringComparison.OrdinalIgnoreCase));

                                    if (added)
                                    {
                                        _availableVersions.Add(newVersion);
                                        _availableVersions.Sort((a, b) =>
                                            string.Compare(a.Version, b.Version, StringComparison.OrdinalIgnoreCase));
                                    }
                                }

                                if (added)
                                {
                                    affectedVersion = newVersion;
                                    changeType = VersionChangeType.Added;
                                }
                            }
                        }
                        break;

                    case WatcherChangeTypes.Deleted:
                        // A directory was deleted - remove from list
                        lock (_stateLock)
                        {
                            if (_hotSwapEnabled && ReferenceEquals(_watcherService, sender))
                            {
                                affectedVersion = _availableVersions.FirstOrDefault(v =>
                                    string.Equals(v.DirectoryPath, e.VersionPath, StringComparison.OrdinalIgnoreCase));

                                if (affectedVersion != null)
                                {
                                    _availableVersions.Remove(affectedVersion);
                                    changeType = VersionChangeType.Removed;
                                }
                            }
                        }
                        break;

                    case WatcherChangeTypes.Renamed:
                        // Directory was renamed - refresh the list
                        Refresh();
                        if (IsActiveWatcher(sender))
                        {
                            changeType = VersionChangeType.Refreshed;
                        }
                        break;
                }

                // Never invoke user code while holding the state lock.
                if (changeType.HasValue)
                {
                    OnVersionsChanged(changeType.Value, affectedVersion);
                }

                System.Diagnostics.Debug.WriteLine($"VersionManager hot swap: {e.ChangeType} - {e.VersionPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling version directory change: {ex.Message}");
            }
        }

        private bool IsActiveWatcher(object sender)
        {
            lock (_stateLock)
            {
                return _hotSwapEnabled && ReferenceEquals(_watcherService, sender);
            }
        }

        /// <summary>
        /// Handles watcher errors
        /// </summary>
        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"VersionManager watcher error: {e.GetException()?.Message}");
        }

        /// <summary>
        /// Raises the VersionsChanged event
        /// </summary>
        protected void OnVersionsChanged(VersionChangeType changeType, VersionInfo affectedVersion)
        {
            VersionsChanged?.Invoke(this, new VersionsChangedEventArgs(changeType, affectedVersion));
        }
        #endregion
    }

    #region Validation Report Classes
    /// <summary>
    /// Detailed validation report for a version
    /// </summary>
    public class ValidationReport
    {
        public string VersionId { get; set; }
        public string DirectoryPath { get; set; }
        public DateTime CheckedAt { get; set; }
        public bool IsValid { get; set; }
        public List<CategoryValidationResult> Categories { get; set; } = new();
    }

    /// <summary>
    /// Validation result for a single category
    /// </summary>
    public class CategoryValidationResult
    {
        public string CategoryName { get; set; }
        public bool IsRequired { get; set; }
        public bool Exists { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
    }
    #endregion

    #region Hot Swap Event Classes
    /// <summary>
    /// Specifies the type of version change
    /// </summary>
    public enum VersionChangeType
    {
        /// <summary>
        /// A new version was added
        /// </summary>
        Added,

        /// <summary>
        /// A version was removed
        /// </summary>
        Removed,

        /// <summary>
        /// A version was modified
        /// </summary>
        Modified,

        /// <summary>
        /// The version list was refreshed
        /// </summary>
        Refreshed
    }

    /// <summary>
    /// Event arguments for version list changes
    /// </summary>
    public class VersionsChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The type of change that occurred
        /// </summary>
        public VersionChangeType ChangeType { get; }

        /// <summary>
        /// The version that was affected, or null for refresh events
        /// </summary>
        public VersionInfo AffectedVersion { get; }

        public VersionsChangedEventArgs(VersionChangeType changeType, VersionInfo affectedVersion)
        {
            ChangeType = changeType;
            AffectedVersion = affectedVersion;
        }
    }
    #endregion
}
