/*  MapleLib - A general-purpose MapleStory library
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

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
        private bool _disposed;
        private bool _initialized;

        public string Name => Path.GetFileName(_wzPath);
        public bool IsInitialized => _initialized;
        public VersionInfo VersionInfo => null; // WZ files don't have version info in the same way

        /// <summary>
        /// Creates a new WzFileDataSource for a MapleStory installation directory
        /// </summary>
        public WzFileDataSource(string wzPath, HaCreatorConfig config = null)
        {
            _wzPath = wzPath;
            _config = config ?? new HaCreatorConfig();
            _wzManager = new WzFileManager(wzPath, false);
        }

        /// <summary>
        /// Initializes by loading the WZ file list (does not load WZ files themselves)
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            _wzManager.BuildWzFileList();
            _initialized = true;
        }

        public WzImage GetImage(string category, string imageName)
        {
            var dir = GetDirectory(category);
            if (dir == null) return null;

            return dir[imageName] as WzImage;
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
            return _wzManager[category.ToLower()];
        }

        public IEnumerable<WzDirectory> GetDirectories(string baseCategory)
        {
            return _wzManager.GetWzDirectoriesFromBase(baseCategory.ToLower());
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

        public void Dispose()
        {
            if (_disposed) return;
            _wzManager?.Dispose();
            _disposed = true;
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
        public HybridDataSource(string path, HaCreatorConfig config = null)
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
                    _wzSource = new WzFileDataSource(config.Legacy.WzFilePath, config);
                    _wzSource.Initialize();
                    _hasWzSource = true;
                }
                catch { }
            }
            else if (Directory.GetFiles(path, "*.wz").Any())
            {
                try
                {
                    _wzSource = new WzFileDataSource(path, config);
                    _wzSource.Initialize();
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
