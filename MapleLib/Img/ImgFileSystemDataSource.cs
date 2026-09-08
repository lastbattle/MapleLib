using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapleLib.Img
{
    /// <summary>
    /// IDataSource implementation that loads data from an IMG filesystem structure.
    /// </summary>
    public class ImgFileSystemDataSource : IDataSource
    {
        private readonly ImgFileSystemManager _manager;
        private readonly string _versionPath;
        private bool _disposed;

        public string Name => _manager.VersionInfo?.DisplayName ?? Path.GetFileName(_versionPath);
        public bool IsInitialized => _manager.IsInitialized;
        public VersionInfo VersionInfo => _manager.VersionInfo;

        /// <summary>
        /// Creates a new ImgFileSystemDataSource for a version directory
        /// </summary>
        public ImgFileSystemDataSource(string versionPath, HaCreatorConfig config = null)
        {
            _versionPath = versionPath;
            _manager = new ImgFileSystemManager(versionPath, config);
            _manager.Initialize();
        }

        public WzImage GetImage(string category, string imageName)
        {
            return _manager.LoadImage(category, imageName);
        }

        public WzImage GetImageByPath(string relativePath)
        {
            // Parse category from path (first segment)
            string[] parts = relativePath.Split(new[] { '/', '\\' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return null;

            return _manager.LoadImage(parts[0], parts[1]);
        }

        public IEnumerable<WzImage> GetImagesInCategory(string category)
        {
            return _manager.LoadImagesInCategory(category);
        }

        public IEnumerable<WzImage> GetImagesInDirectory(string category, string subDirectory)
        {
            return _manager.LoadImagesInDirectory(category, subDirectory);
        }

        public IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory)
        {
            return _manager.GetImageNamesInDirectory(category, subDirectory);
        }

        public bool ImageExists(string category, string imageName)
        {
            return _manager.ImageExists(category, imageName);
        }

        /// <summary>
        /// Gets diagnostic information about an image lookup for debugging
        /// </summary>
        public string GetImageDiagnostics(string category, string imageName)
        {
            return _manager.GetImageDiagnostics(category, imageName);
        }

        public bool CategoryExists(string category)
        {
            return _manager.CategoryExists(category);
        }

        public IEnumerable<string> GetCategories()
        {
            return _manager.GetCategories();
        }

        public IEnumerable<string> GetSubdirectories(string category)
        {
            return _manager.GetSubdirectories(category);
        }

        public WzDirectory GetDirectory(string category)
        {
            return _manager.GetDirectory(category);
        }

        public IEnumerable<WzDirectory> GetDirectories(string baseCategory)
        {
            // For IMG filesystem, each category is a single directory
            var dir = _manager.GetDirectory(baseCategory);
            if (dir != null)
                yield return dir;
        }

        public void PreloadCategory(string category)
        {
            _manager.PreloadCategory(category);
        }

        public void ClearCache()
        {
            _manager.ClearCache();
        }

        public DataSourceStats GetStats()
        {
            return _manager.GetStats();
        }

        public bool SaveImage(string category, WzImage image, string relativePath = null)
        {
            if (image == null)
                return false;

            // Use relativePath if provided, otherwise use image.Name
            string path = relativePath ?? image.Name;
            return _manager.SaveImage(image, category, path);
        }

        public void MarkImageUpdated(string category, WzImage image)
        {
            // For IMG filesystem, save immediately when marked as updated
            SaveImage(category, image);
        }

        /// <summary>
        /// Gets the underlying ImgFileSystemManager for direct access
        /// </summary>
        public ImgFileSystemManager Manager => _manager;

        /// <summary>
        /// Gets the count of cached WzImages that have been modified.
        /// </summary>
        public int GetChangedImagesCount() => _manager.GetChangedImagesCount();

        /// <summary>
        /// Gets information about changed images for display purposes.
        /// </summary>
        public List<(string Category, string RelativePath, string ImageName)> GetChangedImagesInfo()
            => _manager.GetChangedImagesInfo();

        /// <summary>
        /// Saves all changed images in the cache back to disk.
        /// </summary>
        /// <returns>Number of images saved</returns>
        public int SaveAllChangedImages() => _manager.SaveAllChangedImages();

        /// <summary>
        /// Enables or disables hot swap (file system watching) for category directories
        /// </summary>
        /// <param name="enable">True to enable, false to disable</param>
        /// <param name="debounceMs">Debounce delay in milliseconds (default 500)</param>
        public void EnableHotSwap(bool enable, int debounceMs = 500)
        {
            _manager.EnableHotSwap(enable, debounceMs);
        }

        /// <summary>
        /// Gets whether hot swap is enabled
        /// </summary>
        public bool HotSwapEnabled => _manager.HotSwapEnabled;

        /// <summary>
        /// Event raised when the category index changes due to file additions, deletions, or modifications
        /// </summary>
        public event EventHandler<CategoryIndexChangedEventArgs> CategoryIndexChanged
        {
            add => _manager.CategoryIndexChanged += value;
            remove => _manager.CategoryIndexChanged -= value;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _manager?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// IDataSource implementation that wraps WzFileManager for legacy WZ file access.
    /// </summary>
    public class WzFileDataSource : IDataSource
    {
        private readonly WzFileManager _wzManager;
        private readonly string _wzPath;
        private readonly HaCreatorConfig _config;
        private readonly bool _ownsManager;
        private readonly object _initializationGate = new();
        private WzMapleVersion _defaultMapleVersion;
        private readonly byte[] _customIv;
        private bool _disposed;
        private bool _initialized;

        public string Name => Path.GetFileName(Path.TrimEndingDirectorySeparator(_wzPath));
        public bool IsInitialized => _initialized;
        public VersionInfo VersionInfo => new()
        {
            Version = Path.GetFileName(Path.TrimEndingDirectorySeparator(_wzPath)),
            DisplayName = Path.GetFileName(Path.TrimEndingDirectorySeparator(_wzPath)),
            Encryption = _defaultMapleVersion.ToString(),
            Is64Bit = _wzManager.Is64Bit,
            IsPreBB = _wzManager.IsPreBBDataWzFormat,
            IsPreBBDataWzFormat = _wzManager.IsPreBBDataWzFormat,
            IsBetaMs = _wzManager.IsBetaDataWzFormat,
            DirectoryPath = _wzPath
        }; // WZ files do not carry a manifest, so expose detected manager metadata.

        /// <summary>
        /// Creates a new WzFileDataSource for a MapleStory installation directory
        /// </summary>
        public WzFileDataSource(
            string wzPath,
            HaCreatorConfig config = null,
            bool registerAsGlobal = true,
            WzMapleVersion mapleVersion = WzMapleVersion.BMS,
            byte[] customIv = null)
            : this(
                new WzFileManager(wzPath, false, registerAsGlobal),
                config,
                ownsManager: true,
                mapleVersion: mapleVersion,
                customIv: customIv)
        {
        }

        /// <summary>
        /// Adapts an already initialized legacy manager without taking ownership of it.
        /// This is the bridge used by HaCreator while its editor-owned WzFileManager is
        /// still shared with other editor services.
        /// </summary>
        /// <param name="wzManager">The existing legacy manager.</param>
        /// <param name="config">Optional data-source configuration.</param>
        /// <param name="ownsManager">Set to false for a borrowed editor manager.</param>
        public WzFileDataSource(
            WzFileManager wzManager,
            HaCreatorConfig config = null,
            bool ownsManager = false,
            WzMapleVersion mapleVersion = WzMapleVersion.BMS,
            byte[] customIv = null)
        {
            _wzManager = wzManager ?? throw new ArgumentNullException(nameof(wzManager));
            _wzPath = _wzManager.BaseDirectory ?? string.Empty;
            _config = config ?? new HaCreatorConfig();
            _ownsManager = ownsManager;
            _defaultMapleVersion = mapleVersion;
            if (customIv != null && customIv.Length != 4)
                throw new ArgumentException("A WZ IV must contain exactly four bytes.", nameof(customIv));
            _customIv = customIv?.ToArray();
        }

        /// <summary>
        /// Initializes by loading the WZ file list (does not load WZ files themselves)
        /// </summary>
        public void Initialize(WzMapleVersion? mapleVersion = null)
        {
            if (_initialized) return;
            lock (_initializationGate)
            {
                if (_initialized) return;
                if (mapleVersion.HasValue)
                    _defaultMapleVersion = mapleVersion.Value;

                // An editor can adapt a manager populated in memory (for
                // example by HaRepacker or a test fixture) and with no
                // installation directory. Rebuilding the file list in that
                // case tries to enumerate an empty path and loses the
                // preloaded-manager compatibility promised by this adapter.
                if (string.IsNullOrWhiteSpace(_wzPath) && _wzManager.WzFileList.Count != 0)
                {
                    _initialized = true;
                    return;
                }

                _wzManager.BuildWzFileList();
                _initialized = true;
            }
        }

        public WzImage GetImage(string category, string imageName)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(imageName))
                return null;

            string normalizedCategory = NormalizePath(category);
            string normalizedImageName = NormalizePath(imageName);
            foreach (WzDirectory directory in GetDirectories(normalizedCategory))
            {
                if (FindImage(directory, normalizedImageName) is WzImage image)
                    return image;
            }

            // Some 64-bit clients split nested folders into their own WZ files
            // (for example Character/Face/Face_000.wz). The legacy manager keys
            // those files by the full directory prefix, while callers retain the
            // public category/image form used by Program.FindImage.
            string[] imageSegments = normalizedImageName.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int split = 1; split < imageSegments.Length; split++)
            {
                string nestedCategory = normalizedCategory + "/" + string.Join("/", imageSegments, 0, split);
                string nestedImageName = string.Join("/", imageSegments, split, imageSegments.Length - split);
                foreach (WzDirectory directory in GetDirectories(nestedCategory))
                {
                    if (FindImage(directory, nestedImageName) is WzImage image)
                        return image;
                }
            }

            return null;
        }

        public WzImage GetImageByPath(string relativePath)
        {
            string[] parts = relativePath.Split(new[] { '/', '\\' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return null;

            return GetImage(parts[0], parts[1]);
        }

        public IEnumerable<WzImage> GetImagesInCategory(string category)
        {
            var dirs = GetDirectories(category);
            foreach (var dir in dirs)
            {
                foreach (var img in dir.WzImages)
                {
                    yield return img;
                }
            }
        }

        public IEnumerable<WzImage> GetImagesInDirectory(string category, string subDirectory)
        {
            var dirs = GetDirectories(category);
            foreach (var dir in dirs)
            {
                // Navigate to subdirectory
                var current = dir;
                string[] parts = subDirectory.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    current = current[part] as WzDirectory;
                    if (current == null) break;
                }

                if (current != null)
                {
                    foreach (var img in current.WzImages)
                    {
                        yield return img;
                    }
                }
            }
        }

        public IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory)
        {
            var dirs = GetDirectories(category);
            foreach (var dir in dirs)
            {
                // Navigate to subdirectory
                var current = dir;
                string[] parts = subDirectory.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    current = current[part] as WzDirectory;
                    if (current == null) break;
                }

                if (current != null)
                {
                    foreach (var img in current.WzImages)
                    {
                        // Return just the name without .img extension
                        string name = img.Name;
                        if (name.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                            name = name.Substring(0, name.Length - 4);
                        yield return name;
                    }
                }
            }
        }

        public bool ImageExists(string category, string imageName)
        {
            return GetImage(category, imageName) != null;
        }

        public bool CategoryExists(string category)
        {
            return _wzManager[category] != null;
        }

        public IEnumerable<string> GetCategories()
        {
            // Return standard categories that exist
            foreach (var cat in ImgFileSystemManager.STANDARD_CATEGORIES)
            {
                if (_wzManager[cat.ToLower()] != null)
                    yield return cat;
            }
        }

        public IEnumerable<string> GetSubdirectories(string category)
        {
            var dir = GetDirectory(category);
            if (dir == null)
                return Enumerable.Empty<string>();

            return GetSubdirectoriesRecursive(dir, "");
        }

        private IEnumerable<string> GetSubdirectoriesRecursive(WzDirectory dir, string prefix)
        {
            foreach (var subDir in dir.WzDirectories)
            {
                string path = string.IsNullOrEmpty(prefix) ? subDir.Name : $"{prefix}/{subDir.Name}";
                yield return path;

                foreach (var nested in GetSubdirectoriesRecursive(subDir, path))
                {
                    yield return nested;
                }
            }
        }

        public WzDirectory GetDirectory(string category)
        {
            return GetDirectories(category).FirstOrDefault();
        }

        public IEnumerable<WzDirectory> GetDirectories(string baseCategory)
        {
            if (string.IsNullOrWhiteSpace(baseCategory))
                return Enumerable.Empty<WzDirectory>();

            string normalizedCategory = NormalizePath(baseCategory);
            EnsureCategoryLoaded(normalizedCategory);
            var directories = new List<WzDirectory>();
            foreach (WzDirectory directory in _wzManager.GetWzDirectoriesFromBase(normalizedCategory.ToLowerInvariant()))
            {
                if (directory != null && !directories.Contains(directory))
                    directories.Add(directory);
            }

            // A manager created by the legacy editor can have a loaded, unsplit
            // directory without an entry in _wzFilesList. Preserve the old
            // WzManager[index] lookup as a fallback for that case.
            WzDirectory directDirectory = _wzManager[normalizedCategory];
            if (directDirectory != null && !directories.Contains(directDirectory))
                directories.Add(directDirectory);

            return directories;
        }

        private void EnsureCategoryLoaded(string category)
        {
            if (!_initialized)
                Initialize();

            lock (_initializationGate)
            {
                if (_wzManager.IsBetaDataWzFormat)
                {
                    if (!_wzManager.IsWzFileLoaded("Data"))
                        _wzManager.LoadLegacyDataWzFile("Data", _defaultMapleVersion, _customIv);
                    return;
                }

                foreach (string wzName in _wzManager.GetWzFileNameListFromBase(category))
                {
                    if (string.IsNullOrWhiteSpace(wzName) || _wzManager.IsWzFileLoaded(wzName))
                        continue;
                    _wzManager.LoadWzFile(wzName, _defaultMapleVersion, _customIv);
                }
            }
        }

        public void PreloadCategory(string category)
        {
            // Force parse all images in category
            foreach (var img in GetImagesInCategory(category))
            {
                _ = img.WzProperties; // Force parse
            }
        }

        public void ClearCache()
        {
            // WzFileManager doesn't have explicit cache clearing
            // Images can be unparsed individually if needed
        }

        public DataSourceStats GetStats()
        {
            return new DataSourceStats
            {
                CategoryCount = GetCategories().Count(),
                ImageCount = _wzManager.WzFileList.Sum(wz => wz.WzDirectory?.CountImages() ?? 0)
            };
        }

        public bool SaveImage(string category, WzImage image, string relativePath = null)
        {
            // WZ files don't support immediate saving - mark as updated instead
            MarkImageUpdated(category, image);
            return true;
        }

        public void MarkImageUpdated(string category, WzImage image)
        {
            // Mark the WZ file containing this image as updated
            _wzManager.SetWzFileUpdated(category.ToLower(), image);
        }

        /// <summary>
        /// Gets the underlying WzFileManager for direct access
        /// </summary>
        public WzFileManager WzManager => _wzManager;

        /// <summary>Gets the installation root used by the wrapped manager.</summary>
        public string WzRootPath => _wzManager.BaseDirectory;

        /// <summary>Gets the encryption version used when lazily loading WZ files.</summary>
        public WzMapleVersion MapleVersion =>
            _wzManager.WzFileList.FirstOrDefault()?.MapleVersion ?? _defaultMapleVersion;

        /// <summary>
        /// Returns a copy of the explicit custom IV, or the IV captured by a
        /// borrowed CUSTOM archive when adapting an already loaded editor
        /// manager.
        /// </summary>
        public byte[] CustomIv
        {
            get
            {
                if (_customIv != null)
                    return _customIv.ToArray();
                WzFile loadedFile = _wzManager.WzFileList.FirstOrDefault();
                if (_defaultMapleVersion != WzMapleVersion.CUSTOM
                    && loadedFile?.MapleVersion != WzMapleVersion.CUSTOM)
                    return null;
                return loadedFile?.EncryptionIv;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_ownsManager)
                _wzManager.Dispose();
            _disposed = true;
        }

        private static WzObject FindImage(WzDirectory directory, string imageName)
        {
            string normalizedPath = NormalizePath(imageName);
            if (normalizedPath.Length == 0)
                return null;

            WzObject current = directory;
            foreach (string segment in normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current is not WzDirectory currentDirectory)
                    return null;

                current = currentDirectory[segment];
                if (current == null)
                    return null;
            }

            return current is WzImage ? current : null;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }
    }

    /// <summary>
    /// IDataSource implementation that tries IMG filesystem first, then falls back to WZ files.
    /// </summary>
    public class HybridDataSource : IDataSource
    {
        private readonly ImgFileSystemDataSource _imgSource;
        private readonly WzFileDataSource _wzSource;
        private readonly bool _hasImgSource;
        private readonly bool _hasWzSource;
        private bool _disposed;

        public string Name => _imgSource?.Name ?? _wzSource?.Name ?? "Hybrid";
        public bool IsInitialized => (_hasImgSource && _imgSource.IsInitialized) ||
                                      (_hasWzSource && _wzSource.IsInitialized);
        public VersionInfo VersionInfo => _imgSource?.VersionInfo ?? _wzSource?.VersionInfo;

        /// <summary>
        /// Creates a HybridDataSource that prioritizes IMG filesystem but falls back to WZ files
        /// </summary>
        public HybridDataSource(
            string path,
            HaCreatorConfig config = null,
            bool registerWzManagerAsGlobal = true,
            WzMapleVersion mapleVersion = WzMapleVersion.BMS,
            byte[] customIv = null)
        {
            config ??= new HaCreatorConfig();

            // Try to create IMG source if path looks like a version directory
            if (File.Exists(Path.Combine(path, "manifest.json")) ||
                Directory.Exists(Path.Combine(path, "String")))
            {
                try
                {
                    _imgSource = new ImgFileSystemDataSource(path, config);
                    _hasImgSource = true;
                }
                catch { }
            }

            // Try to create WZ source if path looks like a MapleStory directory
            if (!string.IsNullOrEmpty(config.Legacy.WzFilePath) &&
                Directory.Exists(config.Legacy.WzFilePath))
            {
                try
                {
                    _wzSource = new WzFileDataSource(
                        config.Legacy.WzFilePath,
                        config,
                        registerWzManagerAsGlobal,
                        mapleVersion,
                        customIv);
                    _wzSource.Initialize(mapleVersion);
                    _hasWzSource = true;
                }
                catch { }
            }
            else if (Directory.GetFiles(path, "*.wz").Any())
            {
                try
                {
                    _wzSource = new WzFileDataSource(
                        path,
                        config,
                        registerWzManagerAsGlobal,
                        mapleVersion,
                        customIv);
                    _wzSource.Initialize(mapleVersion);
                    _hasWzSource = true;
                }
                catch { }
            }
        }

        public WzImage GetImage(string category, string imageName)
        {
            if (_hasImgSource)
            {
                var img = _imgSource.GetImage(category, imageName);
                if (img != null) return img;
            }

            if (_hasWzSource)
            {
                return _wzSource.GetImage(category, imageName);
            }

            return null;
        }

        public WzImage GetImageByPath(string relativePath)
        {
            if (_hasImgSource)
            {
                var img = _imgSource.GetImageByPath(relativePath);
                if (img != null) return img;
            }

            if (_hasWzSource)
            {
                return _wzSource.GetImageByPath(relativePath);
            }

            return null;
        }

        public IEnumerable<WzImage> GetImagesInCategory(string category)
        {
            if (_hasImgSource && _imgSource.CategoryExists(category))
            {
                return _imgSource.GetImagesInCategory(category);
            }

            if (_hasWzSource)
            {
                return _wzSource.GetImagesInCategory(category);
            }

            return Enumerable.Empty<WzImage>();
        }

        public IEnumerable<WzImage> GetImagesInDirectory(string category, string subDirectory)
        {
            if (_hasImgSource && _imgSource.CategoryExists(category))
            {
                return _imgSource.GetImagesInDirectory(category, subDirectory);
            }

            if (_hasWzSource)
            {
                return _wzSource.GetImagesInDirectory(category, subDirectory);
            }

            return Enumerable.Empty<WzImage>();
        }

        public IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory)
        {
            if (_hasImgSource && _imgSource.CategoryExists(category))
            {
                return _imgSource.GetImageNamesInDirectory(category, subDirectory);
            }

            if (_hasWzSource)
            {
                return _wzSource.GetImageNamesInDirectory(category, subDirectory);
            }

            return Enumerable.Empty<string>();
        }

        public bool ImageExists(string category, string imageName)
        {
            if (_hasImgSource && _imgSource.ImageExists(category, imageName))
                return true;

            if (_hasWzSource)
                return _wzSource.ImageExists(category, imageName);

            return false;
        }

        public bool CategoryExists(string category)
        {
            if (_hasImgSource && _imgSource.CategoryExists(category))
                return true;

            if (_hasWzSource)
                return _wzSource.CategoryExists(category);

            return false;
        }

        public IEnumerable<string> GetCategories()
        {
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_hasImgSource)
            {
                foreach (var cat in _imgSource.GetCategories())
                    categories.Add(cat);
            }

            if (_hasWzSource)
            {
                foreach (var cat in _wzSource.GetCategories())
                    categories.Add(cat);
            }

            return categories;
        }

        public IEnumerable<string> GetSubdirectories(string category)
        {
            if (_hasImgSource && _imgSource.CategoryExists(category))
            {
                return _imgSource.GetSubdirectories(category);
            }

            if (_hasWzSource)
            {
                return _wzSource.GetSubdirectories(category);
            }

            return Enumerable.Empty<string>();
        }

        public WzDirectory GetDirectory(string category)
        {
            if (_hasImgSource)
            {
                var dir = _imgSource.GetDirectory(category);
                if (dir != null) return dir;
            }

            if (_hasWzSource)
            {
                return _wzSource.GetDirectory(category);
            }

            return null;
        }

        public IEnumerable<WzDirectory> GetDirectories(string baseCategory)
        {
            if (_hasImgSource && _imgSource.CategoryExists(baseCategory))
            {
                return _imgSource.GetDirectories(baseCategory);
            }

            if (_hasWzSource)
            {
                return _wzSource.GetDirectories(baseCategory);
            }

            return Enumerable.Empty<WzDirectory>();
        }

        public void PreloadCategory(string category)
        {
            if (_hasImgSource && _imgSource.CategoryExists(category))
            {
                _imgSource.PreloadCategory(category);
            }
            else if (_hasWzSource)
            {
                _wzSource.PreloadCategory(category);
            }
        }

        public void ClearCache()
        {
            _imgSource?.ClearCache();
            _wzSource?.ClearCache();
        }

        public DataSourceStats GetStats()
        {
            var stats = new DataSourceStats();

            if (_hasImgSource)
            {
                var imgStats = _imgSource.GetStats();
                stats.CategoryCount += imgStats.CategoryCount;
                stats.ImageCount += imgStats.ImageCount;
                stats.CachedImageCount += imgStats.CachedImageCount;
                stats.CacheHitCount += imgStats.CacheHitCount;
                stats.CacheMissCount += imgStats.CacheMissCount;
            }

            if (_hasWzSource)
            {
                var wzStats = _wzSource.GetStats();
                stats.CategoryCount = Math.Max(stats.CategoryCount, wzStats.CategoryCount);
                stats.ImageCount += wzStats.ImageCount;
            }

            return stats;
        }

        public bool SaveImage(string category, WzImage image, string relativePath = null)
        {
            // Prefer saving to IMG source if available
            if (_hasImgSource)
            {
                return _imgSource.SaveImage(category, image, relativePath);
            }

            if (_hasWzSource)
            {
                return _wzSource.SaveImage(category, image, relativePath);
            }

            return false;
        }

        public void MarkImageUpdated(string category, WzImage image)
        {
            // Prefer IMG source if available
            if (_hasImgSource)
            {
                _imgSource.MarkImageUpdated(category, image);
            }
            else if (_hasWzSource)
            {
                _wzSource.MarkImageUpdated(category, image);
            }
        }

        /// <summary>
        /// Gets the underlying IMG filesystem data source (if available)
        /// </summary>
        public ImgFileSystemDataSource ImgSource => _imgSource;

        /// <summary>
        /// Gets the underlying WZ file data source (if available)
        /// </summary>
        public WzFileDataSource WzSource => _wzSource;

        public void Dispose()
        {
            if (_disposed) return;
            _imgSource?.Dispose();
            _wzSource?.Dispose();
            _disposed = true;
        }
    }
}
