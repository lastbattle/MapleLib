using MapleLib.WzLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        private readonly List<VersionInfo> _availableVersions = new();
        private bool _scanned;
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
                if (!_scanned)
                    ScanVersions();
                return _availableVersions.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the count of available versions
        /// </summary>
        public int VersionCount => AvailableVersions.Count;
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
            // Preserve external versions
            var externalVersions = _availableVersions.Where(v => v.IsExternal).ToList();

            _availableVersions.Clear();

            if (Directory.Exists(_rootPath))
            {
                foreach (var dir in Directory.EnumerateDirectories(_rootPath))
                {
                    var versionInfo = LoadVersionManifest(dir);
                    if (versionInfo != null)
                    {
                        _availableVersions.Add(versionInfo);
                    }
                }
            }

            // Re-add external versions that still exist
            foreach (var externalVersion in externalVersions)
            {
                if (Directory.Exists(externalVersion.DirectoryPath) &&
                    !_availableVersions.Any(v => v.DirectoryPath.Equals(externalVersion.DirectoryPath, StringComparison.OrdinalIgnoreCase)))
                {
                    _availableVersions.Add(externalVersion);
                }
            }

            // Sort by version name
            _availableVersions.Sort((a, b) => string.Compare(a.Version, b.Version, StringComparison.OrdinalIgnoreCase));

            _scanned = true;
            return _availableVersions;
        }

        /// <summary>
        /// Refreshes the version list
        /// </summary>
        public void Refresh()
        {
            _scanned = false;
            ScanVersions();
        }

        /// <summary>
        /// Gets a version by its identifier
        /// </summary>
        public VersionInfo GetVersion(string versionId)
        {
            return AvailableVersions.FirstOrDefault(v =>
                v.Version.Equals(versionId, StringComparison.OrdinalIgnoreCase));
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
            string manifestPath = Path.Combine(versionPath, MANIFEST_FILENAME);

            VersionInfo versionInfo;

            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    versionInfo = JsonConvert.DeserializeObject<VersionInfo>(json);
                    versionInfo.DirectoryPath = versionPath;
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

            // Scan for categories
            foreach (var dir in Directory.EnumerateDirectories(versionPath))
            {
                string categoryName = Path.GetFileName(dir);
                int fileCount = Directory.EnumerateFiles(dir, "*.img", SearchOption.AllDirectories).Count();

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

            string json = JsonConvert.SerializeObject(versionInfo, Formatting.Indented);
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
                    bool hasImgFiles = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Any();
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
                    categoryReport.FileCount = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Count();
                    categoryReport.TotalSize = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories)
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

            // Check if already in the list
            if (_availableVersions.Any(v => v.DirectoryPath.Equals(versionPath, StringComparison.OrdinalIgnoreCase)))
                return null;

            // Load the version info
            var versionInfo = LoadVersionManifest(versionPath);
            if (versionInfo == null)
                return null;

            // Mark as external
            versionInfo.IsExternal = true;

            // Add to the list
            _availableVersions.Add(versionInfo);

            // Sort by version name
            _availableVersions.Sort((a, b) => string.Compare(a.Version, b.Version, StringComparison.OrdinalIgnoreCase));

            return versionInfo;
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
                Directory.Delete(version.DirectoryPath, recursive: true);
                _availableVersions.Remove(version);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Renames a version
        /// </summary>
        public bool RenameVersion(string oldVersionId, string newVersionId)
        {
            var version = GetVersion(oldVersionId);
            if (version == null)
                return false;

            if (VersionExists(newVersionId))
                return false; // New name already exists

            try
            {
                string newPath = Path.Combine(_rootPath, newVersionId);
                Directory.Move(version.DirectoryPath, newPath);

                version.Version = newVersionId;
                version.DirectoryPath = newPath;
                SaveVersionManifest(version);

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
}
