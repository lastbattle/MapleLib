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
using MapleLib.WzLib.Util;
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
    /// Service for packing IMG files back into WZ format.
    /// This is the reverse of WzExtractionService.
    /// </summary>
    public class WzPackingService
    {
        #region Constants
        /// <summary>
        /// Standard WZ file categories to pack
        /// </summary>
        public static readonly string[] STANDARD_CATEGORIES = WzExtractionService.STANDARD_WZ_FILES;

        private const string MANIFEST_FILENAME = "manifest.json";
        #endregion

        #region Events
        /// <summary>
        /// Fired when packing progress changes
        /// </summary>
        public event EventHandler<PackingProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Fired when a category packing starts
        /// </summary>
        public event EventHandler<CategoryPackingEventArgs> CategoryStarted;

        /// <summary>
        /// Fired when a category packing completes
        /// </summary>
        public event EventHandler<CategoryPackingEventArgs> CategoryCompleted;

        /// <summary>
        /// Fired when an error occurs during packing
        /// </summary>
        public event EventHandler<PackingErrorEventArgs> ErrorOccurred;
        #endregion

        #region Public Methods
        /// <summary>
        /// Packs all IMG files from a version directory into WZ files
        /// </summary>
        /// <param name="versionPath">Path to the version directory containing IMG files</param>
        /// <param name="outputPath">Path where WZ files will be created</param>
        /// <param name="saveAs64Bit">Whether to save as 64-bit WZ format</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Progress reporter</param>
        /// <returns>Packing result with statistics</returns>
        public async Task<PackingResult> PackAsync(
            string versionPath,
            string outputPath,
            bool saveAs64Bit = false,
            CancellationToken cancellationToken = default,
            IProgress<PackingProgress> progress = null)
        {
            var result = new PackingResult
            {
                VersionPath = versionPath,
                OutputPath = outputPath,
                StartTime = DateTime.Now
            };

            var progressData = new PackingProgress
            {
                CurrentPhase = "Initializing",
                TotalFiles = 0,
                ProcessedFiles = 0
            };

            try
            {
                // Load version info from manifest
                VersionInfo versionInfo = LoadVersionInfo(versionPath);
                if (versionInfo == null)
                {
                    throw new InvalidOperationException($"No manifest.json found in {versionPath}. This directory may not be a valid IMG filesystem.");
                }

                result.VersionInfo = versionInfo;

                // Parse encryption from manifest
                WzMapleVersion encryption = WzMapleVersion.BMS;
                if (Enum.TryParse<WzMapleVersion>(versionInfo.Encryption, out var parsedEncryption))
                {
                    encryption = parsedEncryption;
                }

                // Create output directory
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // Count total images to pack
                progressData.TotalFiles = CountTotalImages(versionPath);
                progress?.Report(progressData);

                // Get all categories to pack
                var categoriesToPack = GetCategoriesToPack(versionPath);

                // Pack each category
                foreach (var category in categoriesToPack)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progressData.CurrentPhase = $"Packing {category}";
                    progressData.CurrentFile = $"{category}.wz";
                    progress?.Report(progressData);

                    OnCategoryStarted(category);

                    var categoryResult = await PackCategoryAsync(
                        versionPath,
                        outputPath,
                        category,
                        encryption,
                        (short)versionInfo.PatchVersion,
                        saveAs64Bit,
                        cancellationToken,
                        (current, total, fileName) =>
                        {
                            progressData.CurrentFile = fileName;
                            progressData.ProcessedFiles++;
                            progress?.Report(progressData);
                            OnProgressChanged(progressData);
                        });

                    result.CategoriesPacked.Add(category, categoryResult);
                    OnCategoryCompleted(category, categoryResult);
                }

                result.Success = true;
                result.EndTime = DateTime.Now;

                progressData.CurrentPhase = "Complete";
                progress?.Report(progressData);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Packing was cancelled";
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
        /// Packs a single category into a WZ file
        /// </summary>
        /// <param name="versionPath">Path to the version directory</param>
        /// <param name="outputPath">Output path for the WZ file</param>
        /// <param name="category">Category name (e.g., "String", "Map")</param>
        /// <param name="encryption">WZ encryption type</param>
        /// <param name="patchVersion">MapleStory patch version number from manifest.json</param>
        /// <param name="saveAs64Bit">Whether to save as 64-bit format</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progressCallback">Progress callback</param>
        public async Task<CategoryPackingResult> PackCategoryAsync(
            string versionPath,
            string outputPath,
            string category,
            WzMapleVersion encryption,
            short patchVersion,
            bool saveAs64Bit,
            CancellationToken cancellationToken,
            Action<int, int, string> progressCallback = null)
        {
            var result = new CategoryPackingResult
            {
                CategoryName = category,
                StartTime = DateTime.Now
            };

            string categoryPath = Path.Combine(versionPath, category);
            if (!Directory.Exists(categoryPath))
            {
                result.Success = false;
                result.Errors.Add($"Category directory not found: {categoryPath}");
                result.EndTime = DateTime.Now;
                return result;
            }

            try
            {
                // Create a new WZ file for this category
                string wzFileName = $"{category}.wz";
                string wzFilePath = Path.Combine(outputPath, wzFileName);

                // Get WZ IV for encryption
                byte[] wzIv = WzTool.GetIvByMapleVersion(encryption);

                await Task.Run(() =>
                {
                    // Create WzFile with proper initialization using patchVersion from manifest
                    using (var wzFile = new WzFile(patchVersion, encryption))
                    {
                        wzFile.Name = wzFileName;

                        // Create directory structure
                        // freeResources = true ensures the IMG content is fully parsed
                        var deserializer = new WzImgDeserializer(true);
                        int processedCount = 0;
                        int totalCount = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Count();

                        // Load all IMG files and add to WZ directory
                        LoadImagesRecursively(
                            categoryPath,
                            wzFile.WzDirectory,
                            categoryPath,
                            deserializer,
                            wzIv,
                            ref processedCount,
                            totalCount,
                            progressCallback,
                            result,
                            cancellationToken);

                        // Save the WZ file (path is passed to SaveToDisk, not set on the object)
                        wzFile.SaveToDisk(wzFilePath, saveAs64Bit, encryption);

                        result.ImagesPacked = processedCount;
                        result.OutputFilePath = wzFilePath;

                        if (File.Exists(wzFilePath))
                        {
                            result.OutputFileSize = new FileInfo(wzFilePath).Length;
                        }
                    }
                }, cancellationToken);

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
                Debug.WriteLine($"Error packing category {category}: {ex}");
            }

            result.EndTime = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Packs only specific categories
        /// </summary>
        /// <param name="versionPath">Path to the version directory</param>
        /// <param name="outputPath">Output path for the WZ files</param>
        /// <param name="categories">Categories to pack</param>
        /// <param name="saveAs64Bit">Whether to save as 64-bit format</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="overridePatchVersion">Optional patch version to override manifest value (0 or negative to use manifest)</param>
        public async Task<PackingResult> PackCategoriesAsync(
            string versionPath,
            string outputPath,
            IEnumerable<string> categories,
            bool saveAs64Bit = false,
            CancellationToken cancellationToken = default,
            IProgress<PackingProgress> progress = null,
            short overridePatchVersion = 0)
        {
            var result = new PackingResult
            {
                VersionPath = versionPath,
                OutputPath = outputPath,
                StartTime = DateTime.Now
            };

            var progressData = new PackingProgress
            {
                CurrentPhase = "Initializing",
                TotalFiles = 0,
                ProcessedFiles = 0
            };

            try
            {
                // Load version info
                VersionInfo versionInfo = LoadVersionInfo(versionPath);
                result.VersionInfo = versionInfo;

                WzMapleVersion encryption = WzMapleVersion.BMS;
                short patchVersion = 1; // Default fallback
                if (versionInfo != null)
                {
                    if (Enum.TryParse<WzMapleVersion>(versionInfo.Encryption, out var parsedEncryption))
                    {
                        encryption = parsedEncryption;
                    }
                    patchVersion = (short)versionInfo.PatchVersion;
                }

                // Use override patch version if specified (> 0)
                if (overridePatchVersion > 0)
                {
                    patchVersion = overridePatchVersion;
                }

                // Create output directory
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // Count images for selected categories
                foreach (var category in categories)
                {
                    string categoryPath = Path.Combine(versionPath, category);
                    if (Directory.Exists(categoryPath))
                    {
                        progressData.TotalFiles += Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Count();
                    }
                }
                progress?.Report(progressData);

                // Pack each category
                foreach (var category in categories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progressData.CurrentPhase = $"Packing {category}";
                    progress?.Report(progressData);

                    OnCategoryStarted(category);

                    var categoryResult = await PackCategoryAsync(
                        versionPath,
                        outputPath,
                        category,
                        encryption,
                        patchVersion,
                        saveAs64Bit,
                        cancellationToken,
                        (current, total, fileName) =>
                        {
                            progressData.CurrentFile = fileName;
                            progressData.ProcessedFiles++;
                            progress?.Report(progressData);
                        });

                    result.CategoriesPacked.Add(category, categoryResult);
                    OnCategoryCompleted(category, categoryResult);
                }

                result.Success = true;
                result.EndTime = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Packing was cancelled";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                OnErrorOccurred(ex);
            }

            return result;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Loads version info from manifest.json
        /// </summary>
        private VersionInfo LoadVersionInfo(string versionPath)
        {
            string manifestPath = Path.Combine(versionPath, MANIFEST_FILENAME);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(manifestPath);
                return JsonConvert.DeserializeObject<VersionInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Counts total IMG files in version directory
        /// </summary>
        private int CountTotalImages(string versionPath)
        {
            int count = 0;

            // Count from all subdirectories that contain .img files
            foreach (var dirPath in Directory.EnumerateDirectories(versionPath))
            {
                string dirName = Path.GetFileName(dirPath);

                // Skip manifest and other non-category files/folders
                if (dirName.StartsWith(".") || dirName.Equals("manifest", StringComparison.OrdinalIgnoreCase))
                    continue;

                count += Directory.EnumerateFiles(dirPath, "*.img", SearchOption.AllDirectories).Count();
            }

            return count;
        }

        /// <summary>
        /// Gets list of categories to pack based on existing directories
        /// </summary>
        private List<string> GetCategoriesToPack(string versionPath)
        {
            var categories = new List<string>();

            // First add standard categories in order
            foreach (var category in STANDARD_CATEGORIES)
            {
                string categoryPath = Path.Combine(versionPath, category);
                if (Directory.Exists(categoryPath) &&
                    Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Any())
                {
                    categories.Add(category);
                }
            }

            // Then add any additional categories not in standard list (like Base, List, etc.)
            foreach (var dirPath in Directory.EnumerateDirectories(versionPath))
            {
                string dirName = Path.GetFileName(dirPath);

                // Skip if already added from standard categories
                if (categories.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                    continue;

                // Skip manifest and other non-category files/folders
                if (dirName.StartsWith(".") || dirName.Equals("manifest", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check for .img files
                int imgCount = Directory.EnumerateFiles(dirPath, "*.img", SearchOption.AllDirectories).Count();

                // Also check for subdirectories (some WZ files like Base.wz only have directory structure)
                int subDirCount = Directory.EnumerateDirectories(dirPath, "*", SearchOption.AllDirectories).Count();

                Debug.WriteLine($"[GetCategoriesToPack] Checking '{dirName}': {imgCount} .img files, {subDirCount} subdirs");

                // Add if it has .img files OR subdirectories (for empty WZ structures like Base.wz)
                if (imgCount > 0 || subDirCount > 0)
                {
                    categories.Add(dirName);
                    Debug.WriteLine($"[GetCategoriesToPack] Added category: {dirName}");
                }
                else
                {
                    Debug.WriteLine($"[GetCategoriesToPack] Skipping '{dirName}' - no .img files or subdirs");
                }
            }

            Debug.WriteLine($"[GetCategoriesToPack] Total categories to pack: {string.Join(", ", categories)}");
            return categories;
        }

        /// <summary>
        /// Recursively loads IMG files and adds them to WzDirectory
        /// </summary>
        private void LoadImagesRecursively(
            string currentPath,
            WzDirectory parentDir,
            string basePath,
            WzImgDeserializer deserializer,
            byte[] wzIv,
            ref int processedCount,
            int totalCount,
            Action<int, int, string> progressCallback,
            CategoryPackingResult result,
            CancellationToken cancellationToken)
        {
            // Process subdirectories first - create WzDirectory for each
            foreach (var subDirPath in Directory.EnumerateDirectories(currentPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string subDirName = Path.GetFileName(subDirPath);

                // Skip if it's a directory that would become a WzImage (has .img extension)
                if (subDirName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Create subdirectory in WZ structure
                var subDir = new WzDirectory(subDirName) { WzIv = wzIv };
                parentDir.AddDirectory(subDir);

                // Recurse into subdirectory
                LoadImagesRecursively(
                    subDirPath,
                    subDir,
                    basePath,
                    deserializer,
                    wzIv,
                    ref processedCount,
                    totalCount,
                    progressCallback,
                    result,
                    cancellationToken);
            }

            // Process IMG files in current directory
            foreach (var imgFilePath in Directory.EnumerateFiles(currentPath, "*.img"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string imgFileName = Path.GetFileName(imgFilePath);
                string relativePath = imgFilePath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);

                try
                {
                    // Read the raw IMG file bytes
                    byte[] imgBytes = File.ReadAllBytes(imgFilePath);

                    // Create WzImage from the raw bytes
                    // This keeps the reader open until we're done saving
                    using (var memStream = new MemoryStream(imgBytes))
                    {
                        var wzReader = new WzBinaryReader(memStream, wzIv);

                        var wzImage = new WzImage(imgFileName, wzReader)
                        {
                            BlockSize = imgBytes.Length
                        };
                        wzImage.CalculateAndSetImageChecksum(imgBytes);
                        wzImage.Offset = 0;

                        // Parse the image fully
                        wzImage.ParseEverything = true;
                        bool parseSuccess = wzImage.ParseImage(true);

                        if (parseSuccess)
                        {
                            // Mark as changed so it will be re-written
                            wzImage.Changed = true;

                            // IMPORTANT: We need to keep the reader alive until save
                            // So we DON'T close it here - it will be disposed when the MemoryStream goes out of scope
                            // But we need to keep the data in memory, so copy it

                            // Deep clone the image to detach from the reader
                            WzImage clonedImage = CloneWzImage(wzImage, imgFileName);
                            if (clonedImage != null)
                            {
                                parentDir.AddImage(clonedImage);
                                processedCount++;
                                progressCallback?.Invoke(processedCount, totalCount, relativePath);
                            }
                            else
                            {
                                result.Errors.Add($"Failed to clone: {relativePath}");
                            }
                        }
                        else
                        {
                            result.Errors.Add($"Failed to parse: {relativePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error loading {relativePath}: {ex.Message}");
                    Debug.WriteLine($"Error loading IMG file {imgFilePath}: {ex}");
                }
            }
        }

        /// <summary>
        /// Deep clones a WzImage to detach it from its reader
        /// </summary>
        private WzImage CloneWzImage(WzImage original, string name)
        {
            try
            {
                var clone = new WzImage(name)
                {
                    Changed = true
                };

                foreach (var prop in original.WzProperties)
                {
                    var clonedProp = prop.DeepClone();

                    // Add directly to WzProperties collection to bypass duplicate name check
                    // This preserves the original structure even if duplicates exist
                    clone.WzProperties.Add(clonedProp);
                }

                return clone;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cloning WzImage {name}: {ex}");
                return null;
            }
        }

        #endregion

        #region Event Handlers
        protected virtual void OnProgressChanged(PackingProgress progress)
        {
            ProgressChanged?.Invoke(this, new PackingProgressEventArgs(progress));
        }

        protected virtual void OnCategoryStarted(string category)
        {
            CategoryStarted?.Invoke(this, new CategoryPackingEventArgs(category, null));
        }

        protected virtual void OnCategoryCompleted(string category, CategoryPackingResult result)
        {
            CategoryCompleted?.Invoke(this, new CategoryPackingEventArgs(category, result));
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, new PackingErrorEventArgs(ex));
        }
        #endregion
    }

    #region Result Classes
    /// <summary>
    /// Result of packing operation
    /// </summary>
    public class PackingResult
    {
        public string VersionPath { get; set; }
        public string OutputPath { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public VersionInfo VersionInfo { get; set; }
        public Dictionary<string, CategoryPackingResult> CategoriesPacked { get; set; } = new Dictionary<string, CategoryPackingResult>();

        public int TotalImagesPacked => CategoriesPacked.Values.Sum(c => c.ImagesPacked);
        public long TotalOutputSize => CategoriesPacked.Values.Sum(c => c.OutputFileSize);
    }

    /// <summary>
    /// Result of packing a single category
    /// </summary>
    public class CategoryPackingResult
    {
        public string CategoryName { get; set; }
        public bool Success { get; set; }
        public int ImagesPacked { get; set; }
        public string OutputFilePath { get; set; }
        public long OutputFileSize { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Progress information for packing operations
    /// </summary>
    public class PackingProgress
    {
        public string CurrentPhase { get; set; }
        public string CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
        public List<string> Errors { get; set; } = new List<string>();
        public bool IsCancelled { get; set; }
    }
    #endregion

    #region Event Args
    public class PackingProgressEventArgs : EventArgs
    {
        public PackingProgress Progress { get; }
        public PackingProgressEventArgs(PackingProgress progress) => Progress = progress;
    }

    public class CategoryPackingEventArgs : EventArgs
    {
        public string Category { get; }
        public CategoryPackingResult Result { get; }
        public CategoryPackingEventArgs(string category, CategoryPackingResult result)
        {
            Category = category;
            Result = result;
        }
    }

    public class PackingErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public PackingErrorEventArgs(Exception ex) => Exception = ex;
    }
    #endregion
}
