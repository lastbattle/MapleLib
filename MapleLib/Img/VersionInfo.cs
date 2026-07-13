using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MapleLib.Img
{
    /// <summary>
    /// Represents metadata about an extracted MapleStory version stored in the filesystem.
    /// This is serialized to/from manifest.json in each version directory.
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// Version identifier (e.g., "v83", "v176", "gms_v230")
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// Human-readable display name (e.g., "GMS v83 (Pre-Big Bang)")
        /// </summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        /// <summary>
        /// Source region (GMS, EMS, KMS, JMS, MSEA, etc.)
        /// </summary>
        [JsonPropertyName("sourceRegion")]
        public string SourceRegion { get; set; }

        /// <summary>
        /// Date when this version was extracted
        /// </summary>
        [JsonPropertyName("extractedDate")]
        public DateTime ExtractedDate { get; set; }

        /// <summary>
        /// The encryption type used (matches WzMapleVersion enum name)
        /// </summary>
        [JsonPropertyName("encryption")]
        public string Encryption { get; set; }

        /// <summary>
        /// Whether this is a 64-bit client version
        /// </summary>
        [JsonPropertyName("is64Bit")]
        public bool Is64Bit { get; set; }

        /// <summary>
        /// Whether this is a pre-Big Bang format (Data.wz only)
        /// </summary>
        [JsonPropertyName("isPreBB")]
        public bool IsPreBB { get; set; }

        /// <summary>
        /// Whether this is a beta MapleStory format (v0.01-v0.30) with single Data.wz containing all categories
        /// </summary>
        [JsonPropertyName("isBetaMs")]
        public bool IsBetaMs { get; set; }

        /// <summary>
        /// Whether this is a Big Bang 2 / Chaos update version (has BigBang2 marker in UIWindow2.img)
        /// </summary>
        [JsonPropertyName("isBigBang2")]
        public bool IsBigBang2 { get; set; }

        /// <summary>
        /// The original MapleStory patch version number (e.g., 83, 176, 230)
        /// </summary>
        [JsonPropertyName("patchVersion")]
        public int PatchVersion { get; set; }

        /// <summary>
        /// Category information (file counts, last modified dates)
        /// </summary>
        [JsonPropertyName("categories")]
        public Dictionary<string, CategoryInfo> Categories { get; set; } = new Dictionary<string, CategoryInfo>();

        /// <summary>
        /// Feature flags indicating what content is available
        /// </summary>
        [JsonPropertyName("features")]
        public VersionFeatures Features { get; set; } = new VersionFeatures();

        /// <summary>
        /// Full path to the version directory on disk
        /// </summary>
        [JsonIgnore]
        public string DirectoryPath { get; set; }

        /// <summary>
        /// Whether this version has been validated (all required files exist)
        /// </summary>
        [JsonIgnore]
        public bool IsValid { get; set; }

        /// <summary>
        /// Whether this version is external (not in the standard versions folder)
        /// </summary>
        [JsonIgnore]
        public bool IsExternal { get; set; }

        /// <summary>
        /// Any validation errors encountered
        /// </summary>
        [JsonIgnore]
        public List<string> ValidationErrors { get; set; } = new List<string>();

        /// <summary>
        /// Total number of IMG files in this version
        /// </summary>
        [JsonIgnore]
        public int TotalImageCount
        {
            get
            {
                int total = 0;
                foreach (var category in Categories.Values)
                {
                    total += category.FileCount;
                }
                return total;
            }
        }
    }

    /// <summary>
    /// Information about a category (String, Map, Mob, etc.)
    /// </summary>
    public class CategoryInfo
    {
        /// <summary>
        /// Number of IMG files in this category
        /// </summary>
        [JsonPropertyName("fileCount")]
        public int FileCount { get; set; }

        /// <summary>
        /// Total size of all files in bytes
        /// </summary>
        [JsonPropertyName("totalSize")]
        public long TotalSize { get; set; }

        /// <summary>
        /// Last modification date of any file in this category
        /// </summary>
        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }

        /// <summary>
        /// List of subdirectories in this category
        /// </summary>
        [JsonPropertyName("subdirectories")]
        public List<string> Subdirectories { get; set; } = new List<string>();
    }

    /// <summary>
    /// Feature flags for a version (reserved for future use)
    /// </summary>
    public class VersionFeatures
    {
    }

    /// <summary>
    /// Progress information for extraction operations
    /// </summary>
    public class ExtractionProgress
    {
        /// <summary>
        /// Current phase of extraction (e.g., "Extracting String.wz")
        /// </summary>
        public string CurrentPhase { get; set; }

        /// <summary>
        /// Current file being processed
        /// </summary>
        public string CurrentFile { get; set; }

        /// <summary>
        /// Total number of files to process
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Number of files processed so far
        /// </summary>
        public int ProcessedFiles { get; set; }

        /// <summary>
        /// Overall progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;

        /// <summary>
        /// Any errors encountered during extraction
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Whether the extraction was cancelled
        /// </summary>
        public bool IsCancelled { get; set; }
    }
}
