using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapleLib.Img
{
    /// <summary>
    /// Manages loading of WzImage files from a filesystem-based IMG structure.
    /// This replaces WzFileManager for the new IMG-based architecture.
    /// </summary>
    public class ImgFileSystemManager : IDisposable
    {
        #region Constants
        /// <summary>
        /// Standard categories that should exist in a valid extraction
        /// </summary>
        public static readonly string[] STANDARD_CATEGORIES = new[]
        {
            "Base", "String", "Map", "Mob", "Npc", "Reactor", "Sound", "Skill",
            "Character", "Item", "UI", "Effect", "Etc", "Quest", "Morph", "TamingMob", "List"
        };

        private const string MANIFEST_FILENAME = "manifest.json";
        private const string INDEX_FILENAME = "index.json";
        #endregion

        #region Fields
        private readonly string _versionPath;
        private readonly HaCreatorConfig _config;
        private readonly VersionInfo _versionInfo;
        private readonly WzMapleVersion _mapleVersion;
        private readonly byte[] _wzIv;

        private readonly ReaderWriterLockSlim _cacheLock = new();
        private readonly LRUCache<string, WzImage> _imageCache;
        private readonly ConcurrentDictionary<string, VirtualWzDirectory> _directoryCache = new();
        private readonly ConcurrentDictionary<string, List<string>> _categoryIndex = new();

        // Default cache settings
        private const int DEFAULT_MAX_CACHE_ITEMS = 500;
        private const long DEFAULT_MAX_CACHE_BYTES = 512 * 1024 * 1024; // 512MB

        private bool _isInitialized;
        private bool _disposed;

        // Statistics
        private int _cacheHits;
        private int _cacheMisses;
        private int _diskReads;

        // Hot swap
        private FileSystemWatcherService _watcherService;
        private bool _hotSwapEnabled;
        #endregion

        #region Events
        /// <summary>
        /// Raised when the category index changes due to file additions, deletions, or modifications
        /// </summary>
        public event EventHandler<CategoryIndexChangedEventArgs> CategoryIndexChanged;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the root directory path for this version
        /// </summary>
        public string VersionPath => _versionPath;

        /// <summary>
        /// Gets the version information
        /// </summary>
        public VersionInfo VersionInfo => _versionInfo;

        /// <summary>
        /// Gets whether the manager is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the MapleStory version used for encryption
        /// </summary>
        public WzMapleVersion MapleVersion => _mapleVersion;

        /// <summary>
        /// Gets whether hot swap (file system watching) is enabled
        /// </summary>
        public bool HotSwapEnabled => _hotSwapEnabled;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new ImgFileSystemManager for the specified version directory
        /// </summary>
        /// <param name="versionPath">Path to the version directory</param>
        /// <param name="config">Configuration options</param>
        public ImgFileSystemManager(string versionPath, HaCreatorConfig config = null)
        {
            if (!Directory.Exists(versionPath))
                throw new DirectoryNotFoundException($"Version directory not found: {versionPath}");

            _versionPath = versionPath;
            _config = config ?? new HaCreatorConfig();

            // Initialize LRU cache with size-based eviction
            // Use config values if available, otherwise defaults
            long maxCacheBytes = _config.Cache?.MaxMemoryCacheMB > 0
                ? _config.Cache.MaxMemoryCacheMB * 1024L * 1024L
                : DEFAULT_MAX_CACHE_BYTES;
            _imageCache = new LRUCache<string, WzImage>(maxCacheBytes, EstimateWzImageSize);

            // Load version manifest
            string manifestPath = Path.Combine(versionPath, MANIFEST_FILENAME);
            if (File.Exists(manifestPath))
            {
                string json = File.ReadAllText(manifestPath);
                _versionInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionInfo>(json);
                _versionInfo.DirectoryPath = versionPath;
            }
            else
            {
                // Create default version info
                _versionInfo = new VersionInfo
                {
                    Version = Path.GetFileName(versionPath),
                    DisplayName = Path.GetFileName(versionPath),
                    DirectoryPath = versionPath,
                    ExtractedDate = DateTime.Now
                };
            }

            // IMPORTANT: Extracted .img files are ALWAYS in BMS format (unencrypted/plain)
            // The manifest's "encryption" field stores the original WZ encryption for reference only
            _mapleVersion = WzMapleVersion.BMS;
            _wzIv = WzTool.GetIvByMapleVersion(_mapleVersion);
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the manager by scanning the directory structure
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            BuildCategoryIndex();
            _isInitialized = true;
        }

        /// <summary>
        /// Initializes the manager asynchronously
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            await Task.Run(() => BuildCategoryIndex());
            _isInitialized = true;
        }

        /// <summary>
        /// Builds the index of all IMG files organized by category
        /// </summary>
        private void BuildCategoryIndex()
        {
            // Look for category directories
            foreach (var dir in Directory.EnumerateDirectories(_versionPath))
            {
                string categoryName = Path.GetFileName(dir);

                // Try to load from index file first, fall back to directory scan
                var imageFiles = LoadCategoryIndex(categoryName, dir);

                if (imageFiles.Count > 0)
                {
                    _categoryIndex[categoryName.ToLower()] = imageFiles;

                    // Update category info
                    if (!_versionInfo.Categories.ContainsKey(categoryName))
                    {
                        _versionInfo.Categories[categoryName] = new CategoryInfo
                        {
                            FileCount = imageFiles.Count,
                            LastModified = DateTime.Now
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Recursively scans a directory for IMG files
        /// </summary>
        private void ScanDirectoryForImages(string baseDir, string currentDir, List<string> imageFiles)
        {
            // Add .img files in current directory
            foreach (var file in Directory.EnumerateFiles(currentDir, "*.img"))
            {
                // Store relative path from category root
                string relativePath = file.Substring(baseDir.Length).TrimStart(Path.DirectorySeparatorChar);
                imageFiles.Add(relativePath);
            }

            // Recurse into subdirectories
            foreach (var subDir in Directory.EnumerateDirectories(currentDir))
            {
                ScanDirectoryForImages(baseDir, subDir, imageFiles);
            }
        }
        #endregion

        #region Image Loading
        /// <summary>
        /// Loads a WzImage from the filesystem
        /// </summary>
        /// <param name="category">The category (e.g., "Map", "Mob")</param>
        /// <param name="relativePath">Relative path within the category (e.g., "Map/Map0/100000000.img")</param>
        /// <returns>The loaded WzImage or null if not found</returns>
        public WzImage LoadImage(string category, string relativePath)
        {
            EnsureInitialized();

            string cacheKey = $"{category.ToLower()}/{relativePath.ToLower()}";

            // Check LRU cache first
            if (_imageCache.TryGet(cacheKey, out var cachedImage))
            {
                Interlocked.Increment(ref _cacheHits);
                return cachedImage;
            }

            Interlocked.Increment(ref _cacheMisses);

            // Build full path
            string fullPath = Path.Combine(_versionPath, category, relativePath);
            if (!fullPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                fullPath += ".img";

            if (!File.Exists(fullPath))
                return null;

            // Load the image
            WzImage image = LoadImageFromFile(fullPath, Path.GetFileName(relativePath));
            if (image != null)
            {
                // Add to LRU cache (will auto-evict if over capacity)
                _imageCache.Add(cacheKey, image);
            }

            return image;
        }

        /// <summary>
        /// Loads a WzImage directly from a file path
        /// </summary>
        private WzImage LoadImageFromFile(string filePath, string imageName)
        {
            try
            {
                Interlocked.Increment(ref _diskReads);

                // Use freeResources=true to fully parse and close file handle
                // This loads all bitmap data into memory but ensures images render correctly
                // Memory is managed by the LRU cache which evicts old images
                var deserializer = new WzImgDeserializer(true);
                WzImage image = deserializer.WzImageFromIMGFile(
                    filePath,
                    _wzIv,
                    imageName,
                    out bool success);

                if (success && image != null)
                {
                    return image;
                }
                else
                {
                    Debug.WriteLine($"[ImgFileSystemManager] Failed to parse image: {filePath} (success={success}, image={image != null})");
                    Debug.WriteLine($"[ImgFileSystemManager] Using IV: {BitConverter.ToString(_wzIv)}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImgFileSystemManager] Error loading image {filePath}: {ex.Message}");
                Debug.WriteLine($"[ImgFileSystemManager] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Loads all images in a category
        /// </summary>
        public IEnumerable<WzImage> LoadImagesInCategory(string category)
        {
            EnsureInitialized();

            string categoryLower = category.ToLower();
            if (!_categoryIndex.TryGetValue(categoryLower, out var imagePaths))
            {
                yield break;
            }

            foreach (var relativePath in imagePaths)
            {
                var image = LoadImage(category, relativePath);
                if (image != null)
                {
                    yield return image;
                }
            }
        }

        /// <summary>
        /// Loads images from a specific subdirectory within a category
        /// </summary>
        public IEnumerable<WzImage> LoadImagesInDirectory(string category, string subDirectory)
        {
            EnsureInitialized();

            string categoryLower = category.ToLower();
            if (!_categoryIndex.TryGetValue(categoryLower, out var imagePaths))
            {
                yield break;
            }

            string subDirNormalized = subDirectory.Replace('/', Path.DirectorySeparatorChar)
                                                   .Replace('\\', Path.DirectorySeparatorChar);

            foreach (var relativePath in imagePaths)
            {
                if (relativePath.StartsWith(subDirNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    var image = LoadImage(category, relativePath);
                    if (image != null)
                    {
                        yield return image;
                    }
                }
            }
        }

        /// <summary>
        /// Gets image names in a directory WITHOUT loading them.
        /// This is used for lazy loading - get the list of available images without memory cost.
        /// </summary>
        public IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory)
        {
            EnsureInitialized();

            string categoryLower = category.ToLower();
            if (!_categoryIndex.TryGetValue(categoryLower, out var imagePaths))
            {
                yield break;
            }

            string subDirNormalized = subDirectory.Replace('/', Path.DirectorySeparatorChar)
                                                   .Replace('\\', Path.DirectorySeparatorChar);

            foreach (var relativePath in imagePaths)
            {
                if (relativePath.StartsWith(subDirNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    // Return just the name without path and extension
                    string fileName = Path.GetFileName(relativePath);
                    if (fileName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return fileName.Substring(0, fileName.Length - 4);
                    }
                    else
                    {
                        yield return fileName;
                    }
                }
            }
        }

        /// <summary>
        /// Loads images in parallel for better performance
        /// </summary>
        public async Task<List<WzImage>> LoadImagesParallelAsync(string category, int maxParallelism = 4)
        {
            EnsureInitialized();

            string categoryLower = category.ToLower();
            if (!_categoryIndex.TryGetValue(categoryLower, out var imagePaths))
            {
                return new List<WzImage>();
            }

            var images = new ConcurrentBag<WzImage>();

            await Parallel.ForEachAsync(imagePaths,
                new ParallelOptions { MaxDegreeOfParallelism = maxParallelism },
                async (path, ct) =>
                {
                    var image = await Task.Run(() => LoadImage(category, path), ct);
                    if (image != null)
                    {
                        images.Add(image);
                    }
                });

            return images.ToList();
        }
        #endregion

        #region Directory Access
        /// <summary>
        /// Gets a virtual WzDirectory that provides WzDirectory-compatible access to a category
        /// </summary>
        public VirtualWzDirectory GetDirectory(string category)
        {
            EnsureInitialized();

            string categoryLower = category.ToLower();

            if (_directoryCache.TryGetValue(categoryLower, out var cached))
            {
                return cached;
            }

            string categoryPath = Path.Combine(_versionPath, category);
            if (!Directory.Exists(categoryPath))
            {
                return null;
            }

            var virtualDir = new VirtualWzDirectory(this, category, categoryPath);
            _directoryCache[categoryLower] = virtualDir;
            return virtualDir;
        }

        /// <summary>
        /// Gets all available categories
        /// </summary>
        public IEnumerable<string> GetCategories()
        {
            EnsureInitialized();
            return _categoryIndex.Keys;
        }

        /// <summary>
        /// Checks if a category exists
        /// </summary>
        public bool CategoryExists(string category)
        {
            EnsureInitialized();
            return _categoryIndex.ContainsKey(category.ToLower());
        }

        /// <summary>
        /// Checks if an image exists
        /// </summary>
        public bool ImageExists(string category, string relativePath)
        {
            string fullPath = Path.Combine(_versionPath, category, relativePath);
            if (!fullPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                fullPath += ".img";
            return File.Exists(fullPath);
        }

        /// <summary>
        /// Gets diagnostic information about an image lookup for debugging purposes
        /// </summary>
        public string GetImageDiagnostics(string category, string relativePath)
        {
            string fullPath = Path.Combine(_versionPath, category, relativePath);
            if (!fullPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                fullPath += ".img";

            string categoryPath = Path.Combine(_versionPath, category);
            bool categoryDirExists = Directory.Exists(categoryPath);
            bool fileExists = File.Exists(fullPath);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Version path: {_versionPath}");
            sb.AppendLine($"Category: {category}");
            sb.AppendLine($"Relative path: {relativePath}");
            sb.AppendLine($"Full path: {fullPath}");
            sb.AppendLine($"Category directory exists: {categoryDirExists}");
            sb.AppendLine($"File exists: {fileExists}");

            if (categoryDirExists)
            {
                var imgFiles = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .Take(10)
                    .ToList();
                sb.AppendLine($"First 10 .img files in category root: {string.Join(", ", imgFiles)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets subdirectories within a category
        /// </summary>
        public IEnumerable<string> GetSubdirectories(string category)
        {
            string categoryPath = Path.Combine(_versionPath, category);
            if (!Directory.Exists(categoryPath))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateDirectories(categoryPath, "*", SearchOption.AllDirectories)
                           .Select(d => d.Substring(categoryPath.Length).TrimStart(Path.DirectorySeparatorChar));
        }
        #endregion

        #region Image Saving
        /// <summary>
        /// Saves a WzImage to the filesystem
        /// </summary>
        /// <param name="image">The image to save</param>
        /// <param name="category">The category (e.g., "Map", "Mob")</param>
        /// <param name="relativePath">Relative path within the category</param>
        /// <returns>True if saved successfully</returns>
        public bool SaveImage(WzImage image, string category, string relativePath)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            string fullPath = Path.Combine(_versionPath, category, relativePath);
            if (!fullPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                fullPath += ".img";

            return SaveImageToFile(image, fullPath);
        }

        /// <summary>
        /// Saves a WzImage directly to a file path
        /// </summary>
        /// <param name="image">The image to save</param>
        /// <param name="filePath">Full file path</param>
        /// <returns>True if saved successfully</returns>
        public bool SaveImageToFile(WzImage image, string filePath)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save to temp file first
                string tmpPath = filePath + ".tmp";

                using (FileStream fs = File.Open(tmpPath, FileMode.Create))
                {
                    using (WzBinaryWriter writer = new WzBinaryWriter(fs, _wzIv))
                    {
                        image.SaveImage(writer, true);
                    }
                }

                // Replace original file
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tmpPath, filePath);

                // Update cache key
                string cacheKey = GetCacheKeyFromPath(filePath);
                if (cacheKey != null)
                {
                    _imageCache.Add(cacheKey, image);
                }

                image.Changed = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving image {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the cache key from a full file path
        /// </summary>
        private string GetCacheKeyFromPath(string filePath)
        {
            // Extract category and relative path from full path
            if (!filePath.StartsWith(_versionPath, StringComparison.OrdinalIgnoreCase))
                return null;

            string relative = filePath.Substring(_versionPath.Length).TrimStart(Path.DirectorySeparatorChar);
            string[] parts = relative.Split(Path.DirectorySeparatorChar, 2);
            if (parts.Length < 2)
                return null;

            return $"{parts[0].ToLower()}/{parts[1].ToLower()}";
        }

        /// <summary>
        /// Creates a new empty image file
        /// </summary>
        /// <param name="category">The category</param>
        /// <param name="relativePath">Relative path within category</param>
        /// <returns>The created WzImage or null if failed</returns>
        public WzImage CreateImage(string category, string relativePath)
        {
            string fullPath = Path.Combine(_versionPath, category, relativePath);
            if (!fullPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                fullPath += ".img";

            // Check if file already exists
            if (File.Exists(fullPath))
                return null;

            // Ensure directory exists
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create empty WzImage
            string imageName = Path.GetFileName(fullPath);
            WzImage newImage = new WzImage(imageName);
            newImage.Changed = true;

            // Save it
            if (SaveImageToFile(newImage, fullPath))
            {
                // Update category index
                string categoryLower = category.ToLower();
                if (!_categoryIndex.ContainsKey(categoryLower))
                {
                    _categoryIndex[categoryLower] = new List<string>();
                }
                _categoryIndex[categoryLower].Add(relativePath);

                return newImage;
            }

            return null;
        }

        /// <summary>
        /// Deletes an image file
        /// </summary>
        /// <param name="category">The category</param>
        /// <param name="relativePath">Relative path within category</param>
        /// <returns>True if deleted successfully</returns>
        public bool DeleteImage(string category, string relativePath)
        {
            string fullPath = Path.Combine(_versionPath, category, relativePath);
            if (!fullPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                fullPath += ".img";

            try
            {
                if (!File.Exists(fullPath))
                    return false;

                File.Delete(fullPath);

                // Remove from cache
                RemoveFromCache(category, relativePath);

                // Remove from category index
                string categoryLower = category.ToLower();
                if (_categoryIndex.TryGetValue(categoryLower, out var paths))
                {
                    paths.Remove(relativePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting image {fullPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the full filesystem path for an image
        /// </summary>
        public string GetImagePath(string category, string relativePath)
        {
            string fullPath = Path.Combine(_versionPath, category, relativePath);
            if (!fullPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                fullPath += ".img";
            return fullPath;
        }
        #endregion

        #region Cache Management
        /// <summary>
        /// Preloads all images in a category for faster subsequent access
        /// </summary>
        public void PreloadCategory(string category)
        {
            foreach (var _ in LoadImagesInCategory(category))
            {
                // Loading populates the cache
            }
        }

        /// <summary>
        /// Preloads categories asynchronously
        /// </summary>
        public async Task PreloadCategoryAsync(string category)
        {
            await LoadImagesParallelAsync(category);
        }

        /// <summary>
        /// Clears the image cache to free memory
        /// </summary>
        public void ClearCache()
        {
            // LRU cache handles disposal of IDisposable items internally
            _imageCache.Clear();
        }

        /// <summary>
        /// Removes a specific image from the cache
        /// </summary>
        public void RemoveFromCache(string category, string relativePath)
        {
            string cacheKey = $"{category.ToLower()}/{relativePath.ToLower()}";
            // LRU cache handles disposal of IDisposable items internally
            _imageCache.Remove(cacheKey);
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public DataSourceStats GetStats()
        {
            var cacheStats = _imageCache.GetStatistics();
            return new DataSourceStats
            {
                CategoryCount = _categoryIndex.Count,
                ImageCount = _categoryIndex.Values.Sum(v => v.Count),
                CachedImageCount = cacheStats.ItemCount,
                CacheHitCount = (int)cacheStats.HitCount,
                CacheMissCount = (int)cacheStats.MissCount,
                DiskReadCount = _diskReads,
                MemoryUsageBytes = cacheStats.SizeBytes
            };
        }

        /// <summary>
        /// Trims the cache to a specified number of items by removing least recently accessed.
        /// Note: With LRU cache, this is handled automatically. This method is kept for explicit trimming.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to keep - not used with LRU cache, which uses size-based eviction</param>
        public void TrimCache(int maxItems)
        {
            // LRU cache automatically evicts based on size, no manual trimming needed
            // The cache will evict least recently used items when it exceeds maxSizeBytes
            Debug.WriteLine($"TrimCache called - LRU cache auto-manages at {_imageCache.CurrentSizeBytes / 1024 / 1024}MB");
        }

        /// <summary>
        /// Gets all cached WzImages that have been modified (Changed = true).
        /// Returns key-value pairs where key is the cache key (category/relativePath) and value is the WzImage.
        /// </summary>
        /// <returns>Collection of changed images with their cache keys</returns>
        public IEnumerable<KeyValuePair<string, WzImage>> GetChangedImages()
        {
            var allItems = _imageCache.GetAllItems();
            return allItems.Where(kvp => kvp.Value != null && kvp.Value.Changed);
        }

        /// <summary>
        /// Gets the count of cached WzImages that have been modified.
        /// </summary>
        /// <returns>Number of changed images in cache</returns>
        public int GetChangedImagesCount()
        {
            return _imageCache.GetAllValues().Count(img => img != null && img.Changed);
        }

        /// <summary>
        /// Saves all changed images in the cache back to disk.
        /// </summary>
        /// <returns>Number of images saved</returns>
        public int SaveAllChangedImages()
        {
            int savedCount = 0;
            var changedImages = GetChangedImages().ToList();

            foreach (var kvp in changedImages)
            {
                // Parse cache key to get category and relative path
                // Key format is "category/relativePath"
                string cacheKey = kvp.Key;
                int separatorIndex = cacheKey.IndexOf('/');
                if (separatorIndex > 0)
                {
                    string category = cacheKey.Substring(0, separatorIndex);
                    string relativePath = cacheKey.Substring(separatorIndex + 1);

                    if (SaveImage(kvp.Value, category, relativePath))
                    {
                        savedCount++;
                    }
                }
            }

            return savedCount;
        }

        /// <summary>
        /// Gets information about changed images for display purposes.
        /// </summary>
        /// <returns>List of tuples containing (category, relativePath, imageName)</returns>
        public List<(string Category, string RelativePath, string ImageName)> GetChangedImagesInfo()
        {
            var result = new List<(string, string, string)>();
            var changedImages = GetChangedImages();

            foreach (var kvp in changedImages)
            {
                string cacheKey = kvp.Key;
                int separatorIndex = cacheKey.IndexOf('/');
                if (separatorIndex > 0)
                {
                    string category = cacheKey.Substring(0, separatorIndex);
                    string relativePath = cacheKey.Substring(separatorIndex + 1);
                    string imageName = kvp.Value?.Name ?? Path.GetFileName(relativePath);
                    result.Add((category, relativePath, imageName));
                }
            }

            return result;
        }

        /// <summary>
        /// Gets memory estimate for cached images
        /// </summary>
        public long EstimateCacheMemoryUsage()
        {
            return _imageCache.CurrentSizeBytes;
        }
        #endregion

        #region Category Index Files
        /// <summary>
        /// Generates index files for all categories for faster subsequent loading
        /// </summary>
        public void GenerateCategoryIndices()
        {
            EnsureInitialized();

            foreach (var categoryName in _categoryIndex.Keys)
            {
                GenerateCategoryIndex(categoryName);
            }
        }

        /// <summary>
        /// Generates an index file for a specific category
        /// </summary>
        public void GenerateCategoryIndex(string category)
        {
            string categoryPath = Path.Combine(_versionPath, category);
            if (!Directory.Exists(categoryPath))
                return;

            var index = CategoryIndex.BuildFromDirectory(categoryPath, category);
            string indexPath = Path.Combine(categoryPath, INDEX_FILENAME);
            index.Save(indexPath);
        }

        /// <summary>
        /// Loads category index from file if available, otherwise scans directory
        /// </summary>
        private List<string> LoadCategoryIndex(string category, string categoryPath)
        {
            string indexPath = Path.Combine(categoryPath, INDEX_FILENAME);
            var index = CategoryIndex.Load(indexPath);

            // Use index if valid and not stale
            if (index != null && !index.IsStale(categoryPath))
            {
                return index.AllImagePaths.ToList();
            }

            // Fall back to directory scan
            var imageFiles = new List<string>();
            ScanDirectoryForImages(categoryPath, categoryPath, imageFiles);
            return imageFiles;
        }

        /// <summary>
        /// Checks if category indices exist
        /// </summary>
        public bool HasCategoryIndices()
        {
            foreach (var category in STANDARD_CATEGORIES)
            {
                string indexPath = Path.Combine(_versionPath, category, INDEX_FILENAME);
                if (File.Exists(indexPath))
                    return true;
            }
            return false;
        }
        #endregion

        #region Async Preloading
        /// <summary>
        /// Preloads multiple categories in parallel
        /// </summary>
        public async Task PreloadCategoriesAsync(IEnumerable<string> categories, int maxParallelism = 2)
        {
            await Parallel.ForEachAsync(categories,
                new ParallelOptions { MaxDegreeOfParallelism = maxParallelism },
                async (category, ct) =>
                {
                    await LoadImagesParallelAsync(category);
                });
        }

        /// <summary>
        /// Preloads commonly used categories (String, Map helpers)
        /// </summary>
        public async Task PreloadCommonCategoriesAsync()
        {
            // Load String category first - most commonly accessed
            if (CategoryExists("String"))
            {
                await LoadImagesParallelAsync("String");
            }

            // Load Map helper images
            var mapHelperImage = LoadImage("Map", "MapHelper.img");

            // Load UI tooltips
            var uiTooltipImage = LoadImage("UI", "UIToolTip.img");
        }
        #endregion

        #region Hot Swap
        /// <summary>
        /// Enables or disables hot swap (file system watching)
        /// </summary>
        /// <param name="enable">True to enable, false to disable</param>
        /// <param name="debounceMs">Debounce delay in milliseconds (default 500)</param>
        public void EnableHotSwap(bool enable, int debounceMs = 500)
        {
            if (enable && !_hotSwapEnabled)
            {
                InitializeFileWatchers(debounceMs);
                _hotSwapEnabled = true;
            }
            else if (!enable && _hotSwapEnabled)
            {
                DisposeFileWatchers();
                _hotSwapEnabled = false;
            }
        }

        /// <summary>
        /// Initializes file system watchers for all category directories
        /// </summary>
        private void InitializeFileWatchers(int debounceMs)
        {
            _watcherService = new FileSystemWatcherService(debounceMs);
            _watcherService.ImgFileChanged += OnImgFileChanged;
            _watcherService.WatcherError += OnWatcherError;

            // Watch each category directory
            foreach (var category in _categoryIndex.Keys)
            {
                string categoryPath = Path.Combine(_versionPath, category);
                if (Directory.Exists(categoryPath))
                {
                    _watcherService.WatchPath(categoryPath, WatchType.Category, category);
                }
            }

            Debug.WriteLine($"Hot swap enabled for {_categoryIndex.Count} categories");
        }

        /// <summary>
        /// Disposes file system watchers
        /// </summary>
        private void DisposeFileWatchers()
        {
            if (_watcherService != null)
            {
                _watcherService.ImgFileChanged -= OnImgFileChanged;
                _watcherService.WatcherError -= OnWatcherError;
                _watcherService.Dispose();
                _watcherService = null;
            }

            Debug.WriteLine("Hot swap disabled");
        }

        /// <summary>
        /// Handles .img file change events from the file system watcher
        /// </summary>
        private void OnImgFileChanged(object sender, ImgFileChangedEventArgs e)
        {
            if (_disposed)
                return;

            try
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        AddToCategoryIndex(e.Category, e.RelativePath);
                        OnCategoryIndexChanged(e.Category, CategoryChangeType.FileAdded, e.RelativePath);
                        break;

                    case WatcherChangeTypes.Deleted:
                        RemoveFromCategoryIndex(e.Category, e.RelativePath);
                        InvalidateCache(e.Category, e.RelativePath);
                        OnCategoryIndexChanged(e.Category, CategoryChangeType.FileRemoved, e.RelativePath);
                        break;

                    case WatcherChangeTypes.Changed:
                        // Windows sometimes reports new files as "Changed" instead of "Created"
                        // Check if file exists in index; if not, treat as Added
                        bool isNewFile = !ExistsInCategoryIndex(e.Category, e.RelativePath);
                        if (isNewFile)
                        {
                            AddToCategoryIndex(e.Category, e.RelativePath);
                            OnCategoryIndexChanged(e.Category, CategoryChangeType.FileAdded, e.RelativePath);
                        }
                        else
                        {
                            InvalidateCache(e.Category, e.RelativePath);
                            OnCategoryIndexChanged(e.Category, CategoryChangeType.FileModified, e.RelativePath);
                        }
                        break;

                    case WatcherChangeTypes.Renamed:
                        // Handle rename as delete old + add new
                        if (!string.IsNullOrEmpty(e.OldPath))
                        {
                            string oldRelativePath = Path.GetFileName(e.OldPath);
                            RemoveFromCategoryIndex(e.Category, oldRelativePath);
                            InvalidateCache(e.Category, oldRelativePath);
                        }
                        AddToCategoryIndex(e.Category, e.RelativePath);
                        OnCategoryIndexChanged(e.Category, CategoryChangeType.FileRenamed, e.RelativePath);
                        break;
                }

                Debug.WriteLine($"Hot swap: {e.ChangeType} - {e.Category}/{e.RelativePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling file change: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles watcher errors
        /// </summary>
        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Debug.WriteLine($"File watcher error: {e.GetException()?.Message}");
        }

        /// <summary>
        /// Adds a file to the category index
        /// </summary>
        /// <param name="category">The category name</param>
        /// <param name="relativePath">The relative path within the category</param>
        public void AddToCategoryIndex(string category, string relativePath)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(relativePath))
                return;

            // Ensure it ends with .img
            if (!relativePath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                return;

            string categoryLower = category.ToLower();

            _cacheLock.EnterWriteLock();
            try
            {
                if (_categoryIndex.TryGetValue(categoryLower, out var files))
                {
                    if (!files.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
                    {
                        files.Add(relativePath);
                    }
                }
                else
                {
                    _categoryIndex[categoryLower] = new List<string> { relativePath };
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a file from the category index
        /// </summary>
        /// <param name="category">The category name</param>
        /// <param name="relativePath">The relative path within the category</param>
        public void RemoveFromCategoryIndex(string category, string relativePath)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(relativePath))
                return;

            string categoryLower = category.ToLower();

            _cacheLock.EnterWriteLock();
            try
            {
                if (_categoryIndex.TryGetValue(categoryLower, out var files))
                {
                    // Remove case-insensitive
                    var toRemove = files.FirstOrDefault(f =>
                        f.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                    if (toRemove != null)
                    {
                        files.Remove(toRemove);
                    }
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks if a file exists in the category index
        /// </summary>
        /// <param name="category">The category name</param>
        /// <param name="relativePath">The relative path within the category</param>
        /// <returns>True if the file exists in the index</returns>
        public bool ExistsInCategoryIndex(string category, string relativePath)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(relativePath))
                return false;

            string categoryLower = category.ToLower();

            _cacheLock.EnterReadLock();
            try
            {
                if (_categoryIndex.TryGetValue(categoryLower, out var files))
                {
                    return files.Any(f => f.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                }
                return false;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Invalidates a cache entry for a specific file
        /// </summary>
        /// <param name="category">The category name</param>
        /// <param name="relativePath">The relative path within the category</param>
        public void InvalidateCache(string category, string relativePath)
        {
            string cacheKey = $"{category.ToLower()}/{relativePath.ToLower()}";
            _imageCache.Remove(cacheKey);

            // Also invalidate the directory cache for this category
            string categoryLower = category.ToLower();
            if (_directoryCache.TryGetValue(categoryLower, out var dir))
            {
                dir.Refresh();
            }
        }

        /// <summary>
        /// Raises the CategoryIndexChanged event
        /// </summary>
        protected void OnCategoryIndexChanged(string category, CategoryChangeType changeType, string relativePath)
        {
            CategoryIndexChanged?.Invoke(this, new CategoryIndexChangedEventArgs(category, changeType, relativePath));
        }

        /// <summary>
        /// Refreshes the category index by rescanning the directory
        /// </summary>
        /// <param name="category">The category to refresh, or null to refresh all</param>
        public void RefreshCategoryIndex(string category = null)
        {
            if (category != null)
            {
                string categoryPath = Path.Combine(_versionPath, category);
                if (Directory.Exists(categoryPath))
                {
                    var imageFiles = new List<string>();
                    ScanDirectoryForImages(categoryPath, categoryPath, imageFiles);

                    _cacheLock.EnterWriteLock();
                    try
                    {
                        _categoryIndex[category.ToLower()] = imageFiles;
                    }
                    finally
                    {
                        _cacheLock.ExitWriteLock();
                    }

                    OnCategoryIndexChanged(category, CategoryChangeType.IndexRefreshed, null);
                }
            }
            else
            {
                // Refresh all categories
                BuildCategoryIndex();
                OnCategoryIndexChanged(null, CategoryChangeType.IndexRefreshed, null);
            }
        }
        #endregion

        #region Helpers
        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Estimates the memory size of a WzImage in bytes.
        /// This is used by the LRU cache for size-based eviction.
        /// </summary>
        private static long EstimateWzImageSize(WzImage image)
        {
            if (image == null)
                return 0;

            // Start with base overhead for WzImage object
            long size = 1024; // Base object overhead

            // If not parsed, use a small estimate (will grow when parsed)
            if (!image.Parsed)
            {
                return size;
            }

            try
            {
                // Use a HashSet to track visited properties and prevent infinite recursion
                var visited = new HashSet<WzImageProperty>(ReferenceEqualityComparer.Instance);

                // Iterate over WzImage's properties
                if (image.WzProperties != null)
                {
                    foreach (var prop in image.WzProperties)
                    {
                        size += EstimatePropertySize(prop, visited, 0);
                    }
                }
            }
            catch
            {
                // If estimation fails, use a conservative default
                size = 100 * 1024; // 100KB default
            }

            return size;
        }

        /// <summary>
        /// Recursively estimates the memory size of a WzImageProperty tree
        /// </summary>
        private static long EstimatePropertySize(WzImageProperty prop, HashSet<WzImageProperty> visited, int depth)
        {
            // Prevent infinite recursion from circular references or deep trees
            if (prop == null || depth > 50 || !visited.Add(prop))
                return 0;

            long size = 100; // Base per-property overhead

            switch (prop)
            {
                case WzCanvasProperty canvas:
                    // Canvas properties contain bitmap data - major memory consumer
                    var pngProp = canvas.PngProperty;
                    if (pngProp != null)
                    {
                        // ARGB = 4 bytes per pixel
                        size += pngProp.Width * pngProp.Height * 4L;
                    }
                    break;

                case WzStringProperty str:
                    size += (str.Value?.Length ?? 0) * 2; // UTF-16
                    break;

                case WzBinaryProperty binary:
                    // Sound/binary data size
                    size += binary.Length;
                    break;
            }

            // Add children
            if (prop.WzProperties != null)
            {
                foreach (var child in prop.WzProperties)
                {
                    size += EstimatePropertySize(child, visited, depth + 1);
                }
            }

            return size;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed)
                return;

            // Disable hot swap and dispose watchers
            if (_hotSwapEnabled)
            {
                DisposeFileWatchers();
                _hotSwapEnabled = false;
            }

            _imageCache?.Dispose();  // LRU cache disposes all cached WzImage objects
            _directoryCache.Clear();
            _categoryIndex.Clear();
            _cacheLock?.Dispose();

            _disposed = true;
        }
        #endregion
    }

    /// <summary>
    /// Specifies the type of change that occurred to the category index
    /// </summary>
    public enum CategoryChangeType
    {
        /// <summary>
        /// A new .img file was added
        /// </summary>
        FileAdded,

        /// <summary>
        /// An .img file was removed
        /// </summary>
        FileRemoved,

        /// <summary>
        /// An .img file was modified
        /// </summary>
        FileModified,

        /// <summary>
        /// An .img file was renamed
        /// </summary>
        FileRenamed,

        /// <summary>
        /// The entire index was refreshed
        /// </summary>
        IndexRefreshed
    }

    /// <summary>
    /// Event arguments for category index changes
    /// </summary>
    public class CategoryIndexChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The category that changed, or null if all categories changed
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// The type of change that occurred
        /// </summary>
        public CategoryChangeType ChangeType { get; }

        /// <summary>
        /// The relative path of the file that changed, or null for index refresh
        /// </summary>
        public string RelativePath { get; }

        public CategoryIndexChangedEventArgs(string category, CategoryChangeType changeType, string relativePath)
        {
            Category = category;
            ChangeType = changeType;
            RelativePath = relativePath;
        }
    }
}
