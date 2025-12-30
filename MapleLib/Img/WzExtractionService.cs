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
using MapleLib.WzLib.Serializer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapleLib.Img
{
    /// <summary>
    /// Service for extracting WZ files to the IMG filesystem structure.
    /// Provides progress reporting, cancellation support, and manifest generation.
    /// </summary>
    public class WzExtractionService
    {
        #region Constants
        /// <summary>
        /// Standard WZ file categories to extract
        /// </summary>
        public static readonly string[] STANDARD_WZ_FILES = new[]
        {
            "Base", "String", "Map", "Mob", "Npc", "Reactor", "Sound", "Skill",
            "Character", "Item", "UI", "Effect", "Etc", "Quest", "Morph", "TamingMob", "List"
        };

        /// <summary>
        /// Required WZ files for a minimal extraction
        /// </summary>
        public static readonly string[] REQUIRED_WZ_FILES = new[]
        {
            "String", "Map"
        };

        private const string MANIFEST_FILENAME = "manifest.json";
        #endregion

        #region Events
        /// <summary>
        /// Fired when extraction progress changes
        /// </summary>
        public event EventHandler<ExtractionProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Fired when a category extraction starts
        /// </summary>
        public event EventHandler<CategoryExtractionEventArgs> CategoryStarted;

        /// <summary>
        /// Fired when a category extraction completes
        /// </summary>
        public event EventHandler<CategoryExtractionEventArgs> CategoryCompleted;

        /// <summary>
        /// Fired when an error occurs during extraction
        /// </summary>
        public event EventHandler<ExtractionErrorEventArgs> ErrorOccurred;
        #endregion

        #region Public Methods
        /// <summary>
        /// Extracts all WZ files from a MapleStory installation to the IMG filesystem structure
        /// </summary>
        /// <param name="mapleStoryPath">Path to MapleStory installation</param>
        /// <param name="outputVersionPath">Path where the version will be extracted</param>
        /// <param name="versionId">Version identifier (e.g., "v83")</param>
        /// <param name="displayName">Human-readable display name</param>
        /// <param name="encryption">WZ encryption version</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Progress reporter</param>
        /// <returns>Extraction result with statistics</returns>
        public async Task<ExtractionResult> ExtractAsync(
            string mapleStoryPath,
            string outputVersionPath,
            string versionId,
            string displayName,
            WzMapleVersion encryption,
            CancellationToken cancellationToken = default,
            IProgress<ExtractionProgress> progress = null)
        {
            var result = new ExtractionResult
            {
                VersionId = versionId,
                OutputPath = outputVersionPath,
                StartTime = DateTime.Now
            };

            var progressData = new ExtractionProgress
            {
                CurrentPhase = "Initializing",
                TotalFiles = 0,
                ProcessedFiles = 0
            };

            try
            {
                // Detect WZ format
                bool is64Bit = WzFileManager.Detect64BitDirectoryWzFileFormat(mapleStoryPath);
                bool isPreBB = WzFileManager.DetectIsPreBBDataWZFileFormat(mapleStoryPath);

                // Create output directory
                if (!Directory.Exists(outputVersionPath))
                {
                    Directory.CreateDirectory(outputVersionPath);
                }

                // Find all WZ files to extract
                var wzFilesToExtract = DiscoverWzFiles(mapleStoryPath, is64Bit, isPreBB);
                progressData.TotalFiles = CountTotalImages(wzFilesToExtract, mapleStoryPath, encryption, is64Bit);

                progress?.Report(progressData);

                // Extract each WZ file
                foreach (var wzCategory in wzFilesToExtract)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progressData.CurrentPhase = $"Extracting {wzCategory}";
                    progressData.CurrentFile = $"{wzCategory}.wz";
                    progress?.Report(progressData);

                    OnCategoryStarted(wzCategory);

                    var categoryResult = await ExtractCategoryAsync(
                        mapleStoryPath,
                        outputVersionPath,
                        wzCategory,
                        encryption,
                        is64Bit,
                        isPreBB,
                        cancellationToken,
                        (current, total, fileName) =>
                        {
                            progressData.CurrentFile = fileName;
                            progressData.ProcessedFiles++;
                            progress?.Report(progressData);
                            OnProgressChanged(progressData);
                        });

                    result.CategoriesExtracted.Add(wzCategory, categoryResult);
                    OnCategoryCompleted(wzCategory, categoryResult);
                }

                // Generate manifest
                progressData.CurrentPhase = "Generating manifest";
                progress?.Report(progressData);

                var versionInfo = CreateVersionInfo(
                    versionId,
                    displayName,
                    encryption,
                    is64Bit,
                    isPreBB,
                    outputVersionPath,
                    result.CategoriesExtracted);

                SaveManifest(outputVersionPath, versionInfo);
                result.VersionInfo = versionInfo;

                // Validate extraction
                progressData.CurrentPhase = "Validating";
                progress?.Report(progressData);

                var validationResult = ValidateExtraction(outputVersionPath);
                result.ValidationResult = validationResult;

                result.Success = validationResult.IsValid;
                result.EndTime = DateTime.Now;

                progressData.CurrentPhase = "Complete";
                progress?.Report(progressData);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Extraction was cancelled";
                progressData.IsCancelled = true;
                progress?.Report(progressData);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                progressData.Errors.Add(ex.Message);
                progress?.Report(progressData);
                OnErrorOccurred(ex);
            }

            return result;
        }

        /// <summary>
        /// Extracts a single WZ category
        /// </summary>
        public async Task<CategoryExtractionResult> ExtractCategoryAsync(
            string mapleStoryPath,
            string outputVersionPath,
            string category,
            WzMapleVersion encryption,
            bool is64Bit,
            bool isPreBB,
            CancellationToken cancellationToken,
            Action<int, int, string> progressCallback = null)
        {
            var result = new CategoryExtractionResult
            {
                CategoryName = category,
                StartTime = DateTime.Now
            };

            string categoryOutputPath = Path.Combine(outputVersionPath, category);
            if (!Directory.Exists(categoryOutputPath))
            {
                Directory.CreateDirectory(categoryOutputPath);
            }

            try
            {
                // Find WZ files for this category (could be multiple like Mob, Mob001, Mob2)
                var wzFiles = FindCategoryWzFiles(mapleStoryPath, category, is64Bit);

                var serializer = new WzImgSerializer();
                int processedCount = 0;

                foreach (var wzFilePath in wzFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!File.Exists(wzFilePath))
                        continue;

                    await Task.Run(() =>
                    {
                        using (var wzFile = new WzFile(wzFilePath, encryption))
                        {
                            var parseStatus = wzFile.ParseWzFile();
                            if (parseStatus != WzFileParseStatus.Success)
                            {
                                result.Errors.Add($"Failed to parse {wzFilePath}: {parseStatus}");
                                return;
                            }

                            // Extract directories and images
                            ExtractWzNode(
                                wzFile.WzDirectory,
                                categoryOutputPath,
                                serializer,
                                ref processedCount,
                                progressCallback,
                                result);
                        }
                    }, cancellationToken);
                }

                result.Success = true;
                result.ImagesExtracted = processedCount;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
            }

            result.EndTime = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Validates an extraction by checking required files exist
        /// </summary>
        public ExtractionValidationResult ValidateExtraction(string versionPath)
        {
            var result = new ExtractionValidationResult
            {
                VersionPath = versionPath,
                CheckedAt = DateTime.Now
            };

            // Check String category exists with Map.img
            string stringPath = Path.Combine(versionPath, "String");
            if (!Directory.Exists(stringPath))
            {
                result.Errors.Add("String category is missing");
            }
            else
            {
                string mapStringPath = Path.Combine(stringPath, "Map.img");
                if (!File.Exists(mapStringPath))
                {
                    result.Errors.Add("String/Map.img is missing");
                }
            }

            // Check Map category exists with at least some maps
            string mapPath = Path.Combine(versionPath, "Map");
            if (!Directory.Exists(mapPath))
            {
                result.Errors.Add("Map category is missing");
            }
            else
            {
                int mapCount = Directory.EnumerateFiles(mapPath, "*.img", SearchOption.AllDirectories).Count();
                if (mapCount == 0)
                {
                    result.Errors.Add("Map category has no .img files");
                }
                result.TotalImageCount += mapCount;
            }

            // Count total images
            foreach (var category in STANDARD_WZ_FILES)
            {
                string categoryPath = Path.Combine(versionPath, category);
                if (Directory.Exists(categoryPath))
                {
                    int count = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Count();
                    result.CategoryImageCounts[category] = count;
                    if (category != "Map") // Already counted
                    {
                        result.TotalImageCount += count;
                    }
                }
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Discovers WZ files available for extraction
        /// </summary>
        private List<string> DiscoverWzFiles(string mapleStoryPath, bool is64Bit, bool isPreBB)
        {
            var categories = new List<string>();

            if (isPreBB)
            {
                // Pre-BB has everything in Data.wz
                // We'll extract to standard categories based on directory structure inside
                categories.Add("Data");
            }
            else if (is64Bit)
            {
                // 64-bit client has Data folder with subfolders
                string dataPath = Path.Combine(mapleStoryPath, "Data");
                if (Directory.Exists(dataPath))
                {
                    // First add standard categories in order
                    foreach (var category in STANDARD_WZ_FILES)
                    {
                        string categoryPath = Path.Combine(dataPath, category);
                        if (Directory.Exists(categoryPath))
                        {
                            categories.Add(category);
                        }
                    }

                    // Then add any additional categories (like Base, List, etc.)
                    foreach (var dir in Directory.EnumerateDirectories(dataPath))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (!categories.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                        {
                            categories.Add(dirName);
                        }
                    }
                }
            }
            else
            {
                // Standard format - first check for standard WZ files in order
                foreach (var category in STANDARD_WZ_FILES)
                {
                    string wzPath = Path.Combine(mapleStoryPath, $"{category}.wz");
                    if (File.Exists(wzPath))
                    {
                        categories.Add(category);
                    }
                }

                // Then scan for any other WZ files (like Base.wz, List.wz, etc.)
                foreach (var wzFile in Directory.EnumerateFiles(mapleStoryPath, "*.wz"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(wzFile);

                    // Skip if already added or if it's a numbered variant (like Mob001, Map002)
                    if (categories.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                        continue;

                    // Check if it's a numbered variant of a standard category
                    bool isNumberedVariant = false;
                    foreach (var stdCat in categories)
                    {
                        if (fileName.StartsWith(stdCat, StringComparison.OrdinalIgnoreCase) &&
                            fileName.Length > stdCat.Length &&
                            char.IsDigit(fileName[stdCat.Length]))
                        {
                            isNumberedVariant = true;
                            break;
                        }
                    }

                    if (!isNumberedVariant)
                    {
                        categories.Add(fileName);
                    }
                }
            }

            return categories;
        }

        /// <summary>
        /// Finds all WZ files for a category (handles split files like Mob, Mob001, Mob2)
        /// </summary>
        private List<string> FindCategoryWzFiles(string mapleStoryPath, string category, bool is64Bit)
        {
            var files = new List<string>();

            if (is64Bit)
            {
                string dataPath = Path.Combine(mapleStoryPath, "Data", category);
                if (Directory.Exists(dataPath))
                {
                    files.AddRange(Directory.EnumerateFiles(dataPath, "*.wz", SearchOption.AllDirectories));
                }
            }
            else
            {
                // Look for base file and numbered variants
                string baseWz = Path.Combine(mapleStoryPath, $"{category}.wz");
                if (File.Exists(baseWz))
                {
                    files.Add(baseWz);
                }

                // Look for numbered variants (Mob001.wz, Mob2.wz, etc.)
                var pattern = new System.Text.RegularExpressions.Regex(
                    $@"^{System.Text.RegularExpressions.Regex.Escape(category)}\d+\.wz$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                foreach (var file in Directory.EnumerateFiles(mapleStoryPath, "*.wz"))
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName) && !files.Contains(file))
                    {
                        files.Add(file);
                    }
                }
            }

            return files;
        }

        /// <summary>
        /// Counts total images to be extracted for progress reporting
        /// </summary>
        private int CountTotalImages(
            List<string> categories,
            string mapleStoryPath,
            WzMapleVersion encryption,
            bool is64Bit)
        {
            int total = 0;

            foreach (var category in categories)
            {
                var wzFiles = FindCategoryWzFiles(mapleStoryPath, category, is64Bit);
                foreach (var wzFilePath in wzFiles)
                {
                    if (!File.Exists(wzFilePath))
                        continue;

                    try
                    {
                        using (var wzFile = new WzFile(wzFilePath, encryption))
                        {
                            var parseStatus = wzFile.ParseWzFile();
                            if (parseStatus == WzFileParseStatus.Success)
                            {
                                total += wzFile.WzDirectory.CountImages();
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be opened
                    }
                }
            }

            return total;
        }

        /// <summary>
        /// Recursively extracts a WZ node (directory or image)
        /// </summary>
        private void ExtractWzNode(
            WzDirectory directory,
            string outputPath,
            WzImgSerializer serializer,
            ref int processedCount,
            Action<int, int, string> progressCallback,
            CategoryExtractionResult result)
        {
            // Extract subdirectories first
            foreach (var subDir in directory.WzDirectories)
            {
                string subDirPath = Path.Combine(outputPath, EscapeFileName(subDir.Name));
                if (!Directory.Exists(subDirPath))
                {
                    Directory.CreateDirectory(subDirPath);
                }

                ExtractWzNode(subDir, subDirPath, serializer, ref processedCount, progressCallback, result);
            }

            // Extract images
            foreach (var img in directory.WzImages)
            {
                try
                {
                    string imgPath = Path.Combine(outputPath, EscapeFileName(img.Name));
                    serializer.SerializeImage(img, imgPath);

                    processedCount++;
                    result.TotalSize += new FileInfo(imgPath).Length;

                    progressCallback?.Invoke(processedCount, 0, img.Name);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to extract {img.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates version info from extraction results
        /// </summary>
        private VersionInfo CreateVersionInfo(
            string versionId,
            string displayName,
            WzMapleVersion encryption,
            bool is64Bit,
            bool isPreBB,
            string outputPath,
            Dictionary<string, CategoryExtractionResult> categories)
        {
            var versionInfo = new VersionInfo
            {
                Version = versionId,
                DisplayName = displayName,
                Encryption = encryption.ToString(),
                Is64Bit = is64Bit,
                IsPreBB = isPreBB,
                ExtractedDate = DateTime.Now,
                DirectoryPath = outputPath
            };

            // Populate category info
            foreach (var kvp in categories)
            {
                string categoryPath = Path.Combine(outputPath, kvp.Key);
                if (Directory.Exists(categoryPath))
                {
                    var categoryInfo = new CategoryInfo
                    {
                        FileCount = kvp.Value.ImagesExtracted,
                        TotalSize = kvp.Value.TotalSize,
                        LastModified = DateTime.Now
                    };

                    // Get subdirectories
                    categoryInfo.Subdirectories = Directory.EnumerateDirectories(categoryPath)
                        .Select(Path.GetFileName)
                        .ToList();

                    versionInfo.Categories[kvp.Key] = categoryInfo;
                }
            }

            // Detect features based on available content
            DetectFeatures(versionInfo, outputPath);

            return versionInfo;
        }

        /// <summary>
        /// Detects version features based on extracted content
        /// </summary>
        private void DetectFeatures(VersionInfo versionInfo, string outputPath)
        {
            // Check for pets
            string petPath = Path.Combine(outputPath, "Item", "Pet");
            versionInfo.Features.HasPets = Directory.Exists(petPath) &&
                Directory.EnumerateFiles(petPath, "*.img").Any();

            // Check for mounts
            string tamingMobPath = Path.Combine(outputPath, "TamingMob");
            versionInfo.Features.HasMount = Directory.Exists(tamingMobPath) &&
                Directory.EnumerateFiles(tamingMobPath, "*.img", SearchOption.AllDirectories).Any();

            // Check for androids
            string androidPath = Path.Combine(outputPath, "Character", "Android");
            versionInfo.Features.HasAndroid = Directory.Exists(androidPath) &&
                Directory.EnumerateFiles(androidPath, "*.img").Any();

            // Check for familiars (mob familiar folder)
            string familiarPath = Path.Combine(outputPath, "Etc", "Familiar");
            versionInfo.Features.HasFamiliar = Directory.Exists(familiarPath);

            // Infer max level from skill structure (5th job skills start at ID 400000000+)
            string skillPath = Path.Combine(outputPath, "Skill");
            if (Directory.Exists(skillPath))
            {
                var skillFiles = Directory.EnumerateFiles(skillPath, "*.img");
                bool has5thJob = skillFiles.Any(f =>
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    return int.TryParse(name, out int id) && id >= 400;
                });
                versionInfo.Features.HasV5thJob = has5thJob;
                versionInfo.Features.MaxLevel = has5thJob ? 300 : 250;
            }
        }

        /// <summary>
        /// Saves the manifest file
        /// </summary>
        private void SaveManifest(string outputPath, VersionInfo versionInfo)
        {
            string manifestPath = Path.Combine(outputPath, MANIFEST_FILENAME);
            string json = JsonConvert.SerializeObject(versionInfo, Formatting.Indented);
            File.WriteAllText(manifestPath, json);
        }

        /// <summary>
        /// Escapes invalid filename characters
        /// </summary>
        private string EscapeFileName(string fileName)
        {
            return ProgressingWzSerializer.EscapeInvalidFilePathNames(fileName);
        }

        #endregion

        #region Event Invokers
        protected virtual void OnProgressChanged(ExtractionProgress progress)
        {
            ProgressChanged?.Invoke(this, new ExtractionProgressEventArgs(progress));
        }

        protected virtual void OnCategoryStarted(string category)
        {
            CategoryStarted?.Invoke(this, new CategoryExtractionEventArgs(category, null));
        }

        protected virtual void OnCategoryCompleted(string category, CategoryExtractionResult result)
        {
            CategoryCompleted?.Invoke(this, new CategoryExtractionEventArgs(category, result));
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, new ExtractionErrorEventArgs(ex));
        }
        #endregion
    }

    #region Result Classes
    /// <summary>
    /// Result of a full extraction operation
    /// </summary>
    public class ExtractionResult
    {
        public bool Success { get; set; }
        public string VersionId { get; set; }
        public string OutputPath { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public VersionInfo VersionInfo { get; set; }
        public ExtractionValidationResult ValidationResult { get; set; }
        public Dictionary<string, CategoryExtractionResult> CategoriesExtracted { get; set; } = new();

        public int TotalImagesExtracted =>
            CategoriesExtracted.Values.Sum(c => c.ImagesExtracted);

        public long TotalSize =>
            CategoriesExtracted.Values.Sum(c => c.TotalSize);
    }

    /// <summary>
    /// Result of extracting a single category
    /// </summary>
    public class CategoryExtractionResult
    {
        public string CategoryName { get; set; }
        public bool Success { get; set; }
        public int ImagesExtracted { get; set; }
        public long TotalSize { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Result of validation after extraction
    /// </summary>
    public class ExtractionValidationResult
    {
        public string VersionPath { get; set; }
        public bool IsValid { get; set; }
        public DateTime CheckedAt { get; set; }
        public int TotalImageCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, int> CategoryImageCounts { get; set; } = new();
    }
    #endregion

    #region Event Args
    public class ExtractionProgressEventArgs : EventArgs
    {
        public ExtractionProgress Progress { get; }
        public ExtractionProgressEventArgs(ExtractionProgress progress)
        {
            Progress = progress;
        }
    }

    public class CategoryExtractionEventArgs : EventArgs
    {
        public string Category { get; }
        public CategoryExtractionResult Result { get; }
        public CategoryExtractionEventArgs(string category, CategoryExtractionResult result)
        {
            Category = category;
            Result = result;
        }
    }

    public class ExtractionErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public ExtractionErrorEventArgs(Exception ex)
        {
            Exception = ex;
        }
    }
    #endregion
}
