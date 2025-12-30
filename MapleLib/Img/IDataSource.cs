using MapleLib.WzLib;
using System;
using System.Collections.Generic;

namespace MapleLib.Img
{
    /// <summary>
    /// Abstraction interface for accessing MapleStory data.
    /// Provides a unified API regardless of whether data comes from WZ files or IMG filesystem.
    /// </summary>
    public interface IDataSource : IDisposable
    {
        /// <summary>
        /// Gets the name/identifier of this data source
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether this data source is initialized and ready for use
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Gets the version information for this data source (if available)
        /// </summary>
        VersionInfo VersionInfo { get; }

        /// <summary>
        /// Gets a single WzImage by category and name
        /// </summary>
        /// <param name="category">The category (e.g., "String", "Map", "Mob")</param>
        /// <param name="imageName">The image name (e.g., "Map.img", "100000000.img")</param>
        /// <returns>The WzImage or null if not found</returns>
        WzImage GetImage(string category, string imageName);

        /// <summary>
        /// Gets a single WzImage by full relative path
        /// </summary>
        /// <param name="relativePath">Full relative path (e.g., "Map/Map/Map0/100000000.img")</param>
        /// <returns>The WzImage or null if not found</returns>
        WzImage GetImageByPath(string relativePath);

        /// <summary>
        /// Gets all WzImages in a category
        /// </summary>
        /// <param name="category">The category (e.g., "Mob", "Npc")</param>
        /// <returns>Enumerable of WzImages in the category</returns>
        IEnumerable<WzImage> GetImagesInCategory(string category);

        /// <summary>
        /// Gets all WzImages in a specific subdirectory within a category
        /// </summary>
        /// <param name="category">The category</param>
        /// <param name="subDirectory">The subdirectory path (e.g., "Map/Map0")</param>
        /// <returns>Enumerable of WzImages</returns>
        IEnumerable<WzImage> GetImagesInDirectory(string category, string subDirectory);

        /// <summary>
        /// Gets image names in a directory WITHOUT loading them.
        /// This is used for lazy loading - get the list of available images without memory cost.
        /// </summary>
        /// <param name="category">The category</param>
        /// <param name="subDirectory">The subdirectory path (e.g., "Tile", "Obj")</param>
        /// <returns>Enumerable of image names (without .img extension)</returns>
        IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory);

        /// <summary>
        /// Checks if an image exists
        /// </summary>
        /// <param name="category">The category</param>
        /// <param name="imageName">The image name</param>
        /// <returns>True if the image exists</returns>
        bool ImageExists(string category, string imageName);

        /// <summary>
        /// Checks if a category exists
        /// </summary>
        /// <param name="category">The category name</param>
        /// <returns>True if the category exists</returns>
        bool CategoryExists(string category);

        /// <summary>
        /// Gets all available categories
        /// </summary>
        /// <returns>List of category names</returns>
        IEnumerable<string> GetCategories();

        /// <summary>
        /// Gets all subdirectories within a category
        /// </summary>
        /// <param name="category">The category</param>
        /// <returns>List of subdirectory paths</returns>
        IEnumerable<string> GetSubdirectories(string category);

        /// <summary>
        /// Gets a WzDirectory-like interface for a category
        /// </summary>
        /// <param name="category">The category</param>
        /// <returns>WzDirectory or null if not found</returns>
        WzDirectory GetDirectory(string category);

        /// <summary>
        /// Gets multiple directories for categories that span multiple WZ files (like Mob, Mob001, Mob002)
        /// </summary>
        /// <param name="baseCategory">The base category name (e.g., "mob")</param>
        /// <returns>List of WzDirectories</returns>
        IEnumerable<WzDirectory> GetDirectories(string baseCategory);

        /// <summary>
        /// Preloads images for faster access
        /// </summary>
        /// <param name="category">Category to preload</param>
        void PreloadCategory(string category);

        /// <summary>
        /// Clears cached data to free memory
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Gets statistics about the data source
        /// </summary>
        DataSourceStats GetStats();

        /// <summary>
        /// Saves a WzImage back to the data source.
        /// For IMG filesystem, this writes to disk. For WZ files, this marks the file as changed.
        /// </summary>
        /// <param name="category">The category (e.g., "String", "Map")</param>
        /// <param name="image">The WzImage to save</param>
        /// <param name="relativePath">Optional relative path within the category (e.g., "Map/Map0/100000000.img")</param>
        /// <returns>True if saved successfully</returns>
        bool SaveImage(string category, WzImage image, string relativePath = null);

        /// <summary>
        /// Marks an image as updated (for WZ file mode where saving is deferred).
        /// For IMG filesystem, this may trigger an immediate save.
        /// </summary>
        /// <param name="category">The category</param>
        /// <param name="image">The image that was updated</param>
        void MarkImageUpdated(string category, WzImage image);
    }

    /// <summary>
    /// Statistics about a data source
    /// </summary>
    public class DataSourceStats
    {
        /// <summary>
        /// Total number of categories
        /// </summary>
        public int CategoryCount { get; set; }

        /// <summary>
        /// Total number of images
        /// </summary>
        public int ImageCount { get; set; }

        /// <summary>
        /// Number of images currently cached in memory
        /// </summary>
        public int CachedImageCount { get; set; }

        /// <summary>
        /// Approximate memory usage in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Number of images loaded from disk
        /// </summary>
        public int DiskReadCount { get; set; }

        /// <summary>
        /// Number of cache hits
        /// </summary>
        public int CacheHitCount { get; set; }

        /// <summary>
        /// Number of cache misses
        /// </summary>
        public int CacheMissCount { get; set; }

        /// <summary>
        /// Cache hit ratio (0-1)
        /// </summary>
        public double CacheHitRatio =>
            CacheHitCount + CacheMissCount > 0
                ? (double)CacheHitCount / (CacheHitCount + CacheMissCount)
                : 0;
    }

    /// <summary>
    /// Factory for creating data sources
    /// </summary>
    public static class DataSourceFactory
    {
        /// <summary>
        /// Creates a data source based on the specified mode
        /// </summary>
        /// <param name="mode">The data source mode</param>
        /// <param name="path">Path to the data (version directory for IMG, MS install for WZ)</param>
        /// <param name="config">Optional configuration</param>
        /// <returns>An IDataSource instance</returns>
        public static IDataSource Create(DataSourceMode mode, string path, HaCreatorConfig config = null)
        {
            config ??= new HaCreatorConfig();

            return mode switch
            {
                DataSourceMode.ImgFileSystem => new ImgFileSystemDataSource(path, config),
                DataSourceMode.WzFiles => new WzFileDataSource(path, config),
                DataSourceMode.Hybrid => new HybridDataSource(path, config),
                _ => throw new ArgumentException($"Unknown data source mode: {mode}")
            };
        }
    }
}
