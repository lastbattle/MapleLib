using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MapleLib.Img
{
    /// <summary>
    /// Defines the data source mode for HaCreator
    /// </summary>
    public enum DataSourceMode
    {
        /// <summary>
        /// Load from IMG files in the filesystem (new default)
        /// </summary>
        ImgFileSystem,

        /// <summary>
        /// Load from WZ files directly (legacy support)
        /// </summary>
        WzFiles,

        /// <summary>
        /// Try IMG filesystem first, fall back to WZ files
        /// </summary>
        Hybrid
    }

    /// <summary>
    /// Configuration for HaCreator's data loading and caching behavior
    /// </summary>
    public class HaCreatorConfig
    {

        /// <summary>
        /// The data source mode to use
        /// </summary>
        [JsonProperty("dataSourceMode")]
        public DataSourceMode DataSourceMode { get; set; } = DataSourceMode.ImgFileSystem;

        /// <summary>
        /// Root path for IMG filesystem data
        /// </summary>
        [JsonProperty("imgRootPath")]
        public string ImgRootPath { get; set; } = HaCreatorPaths.DefaultDataPath;

        /// <summary>
        /// Last used version identifier
        /// </summary>
        [JsonProperty("lastUsedVersion")]
        public string LastUsedVersion { get; set; }

        /// <summary>
        /// Cache configuration
        /// </summary>
        [JsonProperty("cache")]
        public CacheConfig Cache { get; set; } = new CacheConfig();

        /// <summary>
        /// Extraction configuration
        /// </summary>
        [JsonProperty("extraction")]
        public ExtractionConfig Extraction { get; set; } = new ExtractionConfig();

        /// <summary>
        /// Legacy WZ file configuration
        /// </summary>
        [JsonProperty("legacy")]
        public LegacyConfig Legacy { get; set; } = new LegacyConfig();

        /// <summary>
        /// Hot swap configuration for file system watching.
        /// Not persisted to config.json - uses compile-time defaults from HotSwapConstants.
        /// </summary>
        [JsonIgnore]
        public HotSwapConfig HotSwap { get; set; } = new HotSwapConfig();

        /// <summary>
        /// Additional version folder paths added via Browse
        /// These are paths outside the default versions directory
        /// </summary>
        [JsonProperty("additionalVersionPaths")]
        public List<string> AdditionalVersionPaths { get; set; } = new List<string>();

        /// <summary>
        /// History of recently selected version folder paths (most recent first).
        /// No duplicates - selecting a path moves it to the front.
        /// </summary>
        [JsonProperty("recentVersionPaths")]
        public List<string> RecentVersionPaths { get; set; } = new List<string>();

        /// <summary>
        /// Maximum number of recent version paths to keep in history
        /// </summary>
        private const int MaxRecentVersions = 20;

        /// <summary>
        /// Adds a version path to the recent history.
        /// If the path already exists, it's moved to the front.
        /// </summary>
        /// <param name="path">The path to add</param>
        public void AddToRecentVersionPaths(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalizedPath = Path.GetFullPath(path);

            // Remove existing entry if present (case-insensitive comparison)
            RecentVersionPaths.RemoveAll(p =>
                Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            // Insert at the front
            RecentVersionPaths.Insert(0, path);

            // Trim to max size
            if (RecentVersionPaths.Count > MaxRecentVersions)
            {
                RecentVersionPaths.RemoveRange(MaxRecentVersions, RecentVersionPaths.Count - MaxRecentVersions);
            }
        }

        /// <summary>
        /// Loads configuration from the default path
        /// </summary>
        public static HaCreatorConfig Load()
        {
            return Load(HaCreatorPaths.DefaultConfigPath);
        }

        /// <summary>
        /// Loads configuration from a specific path
        /// </summary>
        public static HaCreatorConfig Load(string configPath)
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    return JsonConvert.DeserializeObject<HaCreatorConfig>(json) ?? new HaCreatorConfig();
                }
            }
            catch (Exception)
            {
                // Return default config on error
            }
            return new HaCreatorConfig();
        }

        /// <summary>
        /// Saves configuration to the default path
        /// </summary>
        public void Save()
        {
            Save(HaCreatorPaths.DefaultConfigPath);
        }

        /// <summary>
        /// Saves configuration to a specific path
        /// </summary>
        public void Save(string configPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the versions directory path
        /// </summary>
        [JsonIgnore]
        public string VersionsPath => HaCreatorPaths.GetVersionsPath(ImgRootPath);

        /// <summary>
        /// Gets the custom content directory path
        /// </summary>
        [JsonIgnore]
        public string CustomPath => HaCreatorPaths.GetCustomPath(ImgRootPath);

        /// <summary>
        /// Ensures all required directories exist
        /// </summary>
        public void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(ImgRootPath))
                Directory.CreateDirectory(ImgRootPath);

            if (!Directory.Exists(VersionsPath))
                Directory.CreateDirectory(VersionsPath);

            if (!Directory.Exists(CustomPath))
                Directory.CreateDirectory(CustomPath);
        }
    }

    /// <summary>
    /// Cache configuration
    /// </summary>
    public class CacheConfig
    {
        /// <summary>
        /// Maximum memory cache size in MB
        /// </summary>
        [JsonProperty("maxMemoryCacheMB")]
        public int MaxMemoryCacheMB { get; set; } = 512;

        /// <summary>
        /// Categories to preload on startup
        /// </summary>
        [JsonProperty("preloadCategories")]
        public string[] PreloadCategories { get; set; } = new[] { "String" };

        /// <summary>
        /// Whether to use memory-mapped files for large images
        /// </summary>
        [JsonProperty("enableMemoryMappedFiles")]
        public bool EnableMemoryMappedFiles { get; set; } = true;

        /// <summary>
        /// Maximum number of images to keep in LRU cache
        /// </summary>
        [JsonProperty("maxCachedImages")]
        public int MaxCachedImages { get; set; } = 1000;
    }

    /// <summary>
    /// Extraction configuration
    /// </summary>
    public class ExtractionConfig
    {
        /// <summary>
        /// Default output path for extraction
        /// </summary>
        [JsonProperty("defaultOutputPath")]
        public string DefaultOutputPath { get; set; }

        /// <summary>
        /// Whether to generate index files during extraction
        /// </summary>
        [JsonProperty("generateIndex")]
        public bool GenerateIndex { get; set; } = true;

        /// <summary>
        /// Whether to validate after extraction
        /// </summary>
        [JsonProperty("validateAfterExtract")]
        public bool ValidateAfterExtract { get; set; } = true;

        /// <summary>
        /// Number of parallel extraction threads
        /// </summary>
        [JsonProperty("parallelThreads")]
        public int ParallelThreads { get; set; } = 4;
    }

    /// <summary>
    /// Legacy WZ file configuration
    /// </summary>
    public class LegacyConfig
    {
        /// <summary>
        /// Path to WZ files (for legacy mode)
        /// </summary>
        [JsonProperty("wzFilePath")]
        public string WzFilePath { get; set; }

        /// <summary>
        /// Whether to allow fallback to WZ files when IMG not found
        /// </summary>
        [JsonProperty("allowWzFallback")]
        public bool AllowWzFallback { get; set; } = false;

        /// <summary>
        /// Automatically convert WZ files to IMG on load
        /// </summary>
        [JsonProperty("autoConvertOnLoad")]
        public bool AutoConvertOnLoad { get; set; } = false;
    }

    /// <summary>
    /// Hot swap configuration for automatic file system monitoring.
    /// Values are defined in <see cref="HotSwapConstants"/> (not persisted to config.json).
    /// </summary>
    public class HotSwapConfig
    {
        /// <summary>
        /// Whether hot swap is enabled (file system watching)
        /// </summary>
        public bool Enabled { get; set; } = HotSwapConstants.DefaultEnabled;

        /// <summary>
        /// Debounce delay in milliseconds before processing file changes
        /// </summary>
        public int DebounceMs { get; set; } = HotSwapConstants.DefaultDebounceMs;

        /// <summary>
        /// Whether to watch version directories for new/deleted versions
        /// </summary>
        public bool WatchVersions { get; set; } = HotSwapConstants.DefaultWatchVersions;

        /// <summary>
        /// Whether to watch category directories for .img file changes
        /// </summary>
        public bool WatchCategories { get; set; } = HotSwapConstants.DefaultWatchCategories;

        /// <summary>
        /// Whether to automatically invalidate cache when files change
        /// </summary>
        public bool AutoInvalidateCache { get; set; } = HotSwapConstants.DefaultAutoInvalidateCache;

        /// <summary>
        /// Whether to auto-refresh displayed panels when assets change
        /// </summary>
        public bool AutoRefreshDisplayedAssets { get; set; } = HotSwapConstants.DefaultAutoRefreshDisplayedAssets;

        /// <summary>
        /// Whether to show confirmation dialog before refreshing placed items on the board
        /// </summary>
        public bool ConfirmRefreshPlacedItems { get; set; } = HotSwapConstants.DefaultConfirmRefreshPlacedItems;

        /// <summary>
        /// Whether to pause MapSimulator when assets change
        /// </summary>
        public bool PauseSimulatorOnAssetChange { get; set; } = HotSwapConstants.DefaultPauseSimulatorOnAssetChange;

        /// <summary>
        /// How to handle deleted assets that are in use
        /// </summary>
        public DeletedAssetBehavior DeletedAssetBehavior { get; set; } = HotSwapConstants.DefaultDeletedAssetBehavior;
    }

    /// <summary>
    /// Behavior when a deleted asset is still in use
    /// </summary>
    public enum DeletedAssetBehavior
    {
        /// <summary>
        /// Show a placeholder texture for deleted assets
        /// </summary>
        ShowPlaceholder,

        /// <summary>
        /// Remove all items using the deleted asset
        /// </summary>
        RemoveItems,

        /// <summary>
        /// Prompt the user for action
        /// </summary>
        PromptUser
    }
}
