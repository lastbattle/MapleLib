using MapleLib.ClientLib;
using MapleLib.WzLib;
using MapleLib.WzLib.MSFile;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Newtonsoft.Json;
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
        private const string IMG_CASE_MAP_FILENAME = ".imgcase.json";

        /// <summary>
        /// Maximum degree of parallelism for concurrent WZ file extraction.
        /// Each WZ category (Map.wz, Mob.wz, etc.) can be extracted independently.
        /// </summary>
        private static readonly int MAX_EXTRACTION_PARALLELISM = Math.Max(1, Environment.ProcessorCount - 1);
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
        /// <param name="resolveLinks">Whether to resolve _inlink/_outlink canvas references (default false for 1:1 repack fidelity)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Progress reporter</param>
        /// <returns>Extraction result with statistics</returns>
        public async Task<ExtractionResult> ExtractAsync(
            string mapleStoryPath,
            string outputVersionPath,
            string versionId,
            string displayName,
            WzMapleVersion encryption,
            bool resolveLinks = false,
            CancellationToken cancellationToken = default,
            IProgress<ExtractionProgress> progress = null)
        {
            // Discover all WZ files and extract them
            bool is64Bit = WzFileManager.Detect64BitDirectoryWzFileFormat(mapleStoryPath);
            bool isPreBB = WzFileManager.DetectIsPreBBDataWZFileFormat(mapleStoryPath, encryption);
            var allCategories = DiscoverWzFiles(mapleStoryPath, is64Bit, isPreBB);

            return await ExtractAsync(
                mapleStoryPath,
                outputVersionPath,
                versionId,
                displayName,
                encryption,
                allCategories,
                resolveLinks,
                cancellationToken,
                progress);
        }

        /// <summary>
        /// Extracts specified WZ file categories to an IMG filesystem structure
        /// </summary>
        /// <param name="mapleStoryPath">Path to MapleStory installation</param>
        /// <param name="outputVersionPath">Output path for the version</param>
        /// <param name="versionId">Version identifier</param>
        /// <param name="displayName">Display name for the version</param>
        /// <param name="encryption">WZ encryption version</param>
        /// <param name="categoriesToExtract">List of category names to extract (e.g., "Map", "Mob", "String")</param>
        /// <param name="resolveLinks">Whether to resolve _inlink/_outlink canvas references (default false for 1:1 repack fidelity)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Progress reporter</param>
        /// <returns>Extraction result with statistics</returns>
        public async Task<ExtractionResult> ExtractAsync(
            string mapleStoryPath,
            string outputVersionPath,
            string versionId,
            string displayName,
            WzMapleVersion encryption,
            IEnumerable<string> categoriesToExtract,
            bool resolveLinks = false,
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
                bool isPreBB = WzFileManager.DetectIsPreBBDataWZFileFormat(mapleStoryPath, encryption);
                bool isBetaMs = WzFileManager.DetectBetaDataWzFormat(mapleStoryPath);
                bool isBigBang2 = WzFileManager.DetectBigBang2Format(mapleStoryPath, encryption, is64Bit);

                // Create output directory
                if (!Directory.Exists(outputVersionPath))
                {
                    Directory.CreateDirectory(outputVersionPath);
                }

                // Pre-parse List.wz if it exists (for tracking decrypted images)
                HashSet<string> listWzEntries = null;
                ConcurrentDictionary<string, byte> extractedListWzImages = null;
                string listWzFilePath = Path.Combine(mapleStoryPath, "List.wz");

                if (File.Exists(listWzFilePath) && WzTool.IsListFile(listWzFilePath))
                {
                    try
                    {
                        var entries = ListFileParser.ParseListFile(listWzFilePath, encryption);
                        listWzEntries = new HashSet<string>(entries, StringComparer.OrdinalIgnoreCase);
                        extractedListWzImages = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
                        Debug.WriteLine($"[WzExtraction] Pre-parsed List.wz with {listWzEntries.Count} entries");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WzExtraction] Failed to pre-parse List.wz: {ex.Message}");
                    }
                }

                // Use the provided categories
                var wzFilesToExtract = categoriesToExtract.ToList();

                // If Packs is selected along with other categories, skip categories covered by .ms files
                // to avoid duplicate extraction (Packs contains the complete image data)
                if (wzFilesToExtract.Contains("Packs", StringComparer.OrdinalIgnoreCase))
                {
                    var categoriesInPacks = GetCategoriesInPacks(mapleStoryPath);
                    var skippedCategories = new List<string>();

                    foreach (var packCategory in categoriesInPacks)
                    {
                        if (wzFilesToExtract.Contains(packCategory, StringComparer.OrdinalIgnoreCase) &&
                            !packCategory.Equals("Packs", StringComparison.OrdinalIgnoreCase))
                        {
                            wzFilesToExtract.RemoveAll(c => c.Equals(packCategory, StringComparison.OrdinalIgnoreCase));
                            skippedCategories.Add(packCategory);
                        }
                    }

                    if (skippedCategories.Count > 0)
                    {
                        Debug.WriteLine($"[WzExtraction] Skipping {skippedCategories.Count} categories covered by Packs: {string.Join(", ", skippedCategories)}");
                    }
                }

                progressData.TotalFiles = CountTotalImages(wzFilesToExtract, mapleStoryPath, encryption, is64Bit);

                // Force GC after counting to release any memory allocated during counting
                // This ensures extraction starts with minimal memory footprint
                GC.Collect();
                GC.WaitForPendingFinalizers();

                progress?.Report(progressData);

                // Extract WZ files concurrently - each category is independent
                Debug.WriteLine($"[WzExtraction] Starting concurrent extraction of {wzFilesToExtract.Count} categories with parallelism {MAX_EXTRACTION_PARALLELISM}");

                var extractionResults = new ConcurrentDictionary<string, CategoryExtractionResult>();
                var activeCategories = new ConcurrentDictionary<string, byte>(); // Track active categories
                int processedFiles = 0;
                int completedCategories = 0;
                int totalCategories = wzFilesToExtract.Count;

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = MAX_EXTRACTION_PARALLELISM,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(wzFilesToExtract, parallelOptions, async (wzCategory, ct) =>
                {
                    ct.ThrowIfCancellationRequested();

                    // Track this category as active
                    activeCategories.TryAdd(wzCategory, 0);
                    Debug.WriteLine($"[WzExtraction] Starting extraction of {wzCategory}");
                    OnCategoryStarted(wzCategory);

                    var categoryResult = await ExtractCategoryAsync(
                        mapleStoryPath,
                        outputVersionPath,
                        wzCategory,
                        encryption,
                        is64Bit,
                        isPreBB,
                        resolveLinks,
                        listWzEntries,
                        extractedListWzImages,
                        ct,
                        (current, total, fileName) =>
                        {
                            // Thread-safe progress update
                            int currentTotal = Interlocked.Increment(ref processedFiles);
                            progressData.ProcessedFiles = currentTotal;

                            // Show aggregate status instead of jumping file names
                            int activeCount = activeCategories.Count;
                            int completed = completedCategories;
                            progressData.CurrentPhase = $"Extracting ({activeCount} active, {completed}/{totalCategories} complete)";
                            progressData.CurrentFile = string.Join(", ", activeCategories.Keys.Take(4)) + (activeCount > 4 ? $" +{activeCount - 4} more" : "");

                            progress?.Report(progressData);
                            OnProgressChanged(progressData);
                        });

                    // Mark category as completed
                    activeCategories.TryRemove(wzCategory, out _);
                    Interlocked.Increment(ref completedCategories);

                    extractionResults.TryAdd(wzCategory, categoryResult);
                    OnCategoryCompleted(wzCategory, categoryResult);
                    Debug.WriteLine($"[WzExtraction] Completed extraction of {wzCategory}: {categoryResult.ImagesExtracted} images");
                });

                // Copy results to the result dictionary
                foreach (var kvp in extractionResults)
                {
                    result.CategoriesExtracted.Add(kvp.Key, kvp.Value);
                }

                // Generate manifest
                progressData.CurrentPhase = "Generating manifest";
                progress?.Report(progressData);

                // Get the detected patch version and locale from extracted categories (use first non-zero/non-empty value found)
                short detectedPatchVersion = 0;
                string detectedLocale = null;
                foreach (var categoryResult in result.CategoriesExtracted.Values)
                {
                    if (detectedPatchVersion == 0 && categoryResult.DetectedPatchVersion > 0)
                    {
                        detectedPatchVersion = categoryResult.DetectedPatchVersion;
                    }
                    if (string.IsNullOrEmpty(detectedLocale) && !string.IsNullOrEmpty(categoryResult.DetectedLocale))
                    {
                        detectedLocale = categoryResult.DetectedLocale;
                    }
                    // Break if we have both values
                    if (detectedPatchVersion > 0 && !string.IsNullOrEmpty(detectedLocale))
                    {
                        break;
                    }
                }

                var versionInfo = CreateVersionInfo(
                    versionId,
                    displayName,
                    encryption,
                    is64Bit,
                    isPreBB,
                    isBetaMs,
                    isBigBang2,
                    outputVersionPath,
                    result.CategoriesExtracted,
                    detectedPatchVersion,
                    detectedLocale);

                SaveManifest(outputVersionPath, versionInfo);
                result.VersionInfo = versionInfo;

                // Validate extraction
                progressData.CurrentPhase = "Validating";
                progress?.Report(progressData);

                var validationResult = ValidateExtraction(outputVersionPath, result.CategoriesExtracted.Keys.ToList());
                result.ValidationResult = validationResult;

                // Write filtered List.json if we had List.wz entries
                if (listWzEntries != null && listWzEntries.Count > 0)
                {
                    WriteFilteredListJson(outputVersionPath, listWzEntries, extractedListWzImages);
                }

                // Success if at least some images were extracted
                result.Success = result.TotalImagesExtracted > 0;
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
            bool resolveLinks,
            HashSet<string> listWzEntries,
            ConcurrentDictionary<string, byte> extractedListWzImages,
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

            // Create link resolver if enabled
            WzLinkResolver linkResolver = resolveLinks ? new WzLinkResolver() : null;

            // Check if this is beta Data.wz format
            bool isBetaDataWz = WzFileManager.DetectBetaDataWzFormat(mapleStoryPath);

            try
            {
                // Find WZ files for this category (could be multiple like Mob, Mob001, Mob2)
                // For Packs category, this returns .ms files instead
                // For beta Data.wz, this returns Data.wz
                var wzFilePaths = FindCategoryWzFiles(mapleStoryPath, category, is64Bit, isBetaDataWz);

                // Check if this is the Packs category with .ms files
                bool isPacksCategory = category.Equals("Packs", StringComparison.OrdinalIgnoreCase);

                // Use BMS encryption (all zeroes) for extracted IMG files - plain/unencrypted format
                var serializer = WzImgSerializer.CreateForImgExtraction();
                int processedCount = 0;
                var extractedImageCaseMap = new Dictionary<string, string>(StringComparer.Ordinal);

                // Load ALL WZ files for this category simultaneously so outlinks can resolve
                // across split files (e.g., Mob001.wz referencing Mob/xxx.img in Mob.wz)
                // _Canvas WZ files are loaded for link resolution but NOT extracted separately
                var loadedWzFiles = new List<(WzFile wzFile, bool isCanvasFile, string relativePath)>();
                var loadedMsFiles = new List<WzMsFile>();

                // Get category root path for calculating relative paths (64-bit clients)
                string categoryRootPath = is64Bit ? Path.Combine(mapleStoryPath, "Data", category) : null;

                await Task.Run(() =>
                {
                    try
                    {
                        if (isPacksCategory)
                        {
                            // Handle Packs category with .ms files
                            ExtractPacksMsFiles(
                                wzFilePaths,
                                outputVersionPath,
                                encryption,
                                serializer,
                                linkResolver,
                                listWzEntries,
                                extractedListWzImages,
                                loadedMsFiles,
                                ref processedCount,
                                progressCallback,
                                result,
                                cancellationToken);
                        }
                        else if (isBetaDataWz)
                        {
                            // Handle beta Data.wz format - extract specific subdirectory from Data.wz
                            ExtractBetaDataWzCategory(
                                wzFilePaths,
                                category,
                                categoryOutputPath,
                                encryption,
                                serializer,
                                linkResolver,
                                listWzEntries,
                                extractedListWzImages,
                                extractedImageCaseMap,
                                loadedWzFiles,
                                ref processedCount,
                                progressCallback,
                                result,
                                cancellationToken);
                        }
                        else
                        {
                            // Standard WZ file extraction
                            // First, load and parse all WZ files for this category
                            foreach (var wzFilePath in wzFilePaths)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                if (!File.Exists(wzFilePath))
                                    continue;

                                // Handle List.wz files specially - they have a different format (pre-Big Bang)
                                // List.wz is pre-parsed and List.json is written at the end with only remaining entries
                                if (WzTool.IsListFile(wzFilePath))
                                {
                                    // List.wz is handled at extraction end - skip here
                                    // The entries were pre-parsed and will be filtered based on what was decrypted
                                    result.ImagesExtracted++;
                                    continue;
                                }

                                var wzFile = new WzFile(wzFilePath, encryption);
                                var parseStatus = wzFile.ParseWzFile();

                                if (parseStatus != WzFileParseStatus.Success)
                                {
                                    result.Errors.Add($"Failed to parse {wzFilePath}: {parseStatus}");
                                    wzFile.Dispose();
                                    continue;
                                }

                                // Capture the detected patch version from the first successfully parsed WZ file
                                if (result.DetectedPatchVersion == 0 && wzFile.Version > 0)
                                {
                                    result.DetectedPatchVersion = wzFile.Version;
                                    Debug.WriteLine($"[WzExtraction] Detected patch version {wzFile.Version} from {wzFilePath}");
                                }

                                // Capture the detected locale if available
                                if (string.IsNullOrEmpty(result.DetectedLocale) &&
                                    wzFile.MapleLocaleVersion != MapleStoryLocalisation.Not_Known)
                                {
                                    result.DetectedLocale = wzFile.MapleLocaleVersion.ToString();
                                    Debug.WriteLine($"[WzExtraction] Detected locale {result.DetectedLocale} from {wzFilePath}");
                                }

                                // Check if this is a _Canvas WZ file
                                // _Canvas files are used for link resolution but NOT extracted separately
                                // Their canvas data will be embedded into the main .img files via link resolution
                                bool isCanvasFile = wzFilePath.Contains("_Canvas", StringComparison.OrdinalIgnoreCase);

                                // Calculate relative path from category root (for 64-bit clients with nested WZ files)
                                // e.g., if wzFilePath is "Data/Map/Map/Map0/Map0_000.wz", relative path is "Map/Map0"
                                string relativePath = "";
                                if (categoryRootPath != null)
                                {
                                    string wzDir = Path.GetDirectoryName(wzFilePath);
                                    if (wzDir.StartsWith(categoryRootPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        relativePath = wzDir.Substring(categoryRootPath.Length).TrimStart(Path.DirectorySeparatorChar);
                                    }
                                }

                                loadedWzFiles.Add((wzFile, isCanvasFile, relativePath));
                            }

                            // Set all loaded WZ files on the resolver for cross-file outlink resolution
                            // This includes _Canvas files so their canvas data can be resolved
                            if (linkResolver != null && loadedWzFiles.Count > 0)
                            {
                                linkResolver.SetCategoryWzFiles(loadedWzFiles.Select(x => x.wzFile).ToList(), category);
                            }

                            // Now extract from non-Canvas WZ files only
                            // _Canvas files are NOT extracted - their data is embedded via link resolution
                            foreach (var (wzFile, isCanvasFile, relativePath) in loadedWzFiles)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                // Skip _Canvas files - they are only used for link resolution
                                // The canvas images will be embedded into the main .img files
                                if (isCanvasFile)
                                {
                                    continue;
                                }

                                // Determine output path - include relative path for nested WZ files
                                string outputPath = categoryOutputPath;
                                if (!string.IsNullOrEmpty(relativePath))
                                {
                                    outputPath = Path.Combine(categoryOutputPath, relativePath);
                                    if (!Directory.Exists(outputPath))
                                    {
                                        Directory.CreateDirectory(outputPath);
                                    }
                                }

                                ExtractWzNode(
                                    wzFile.WzDirectory,
                                    outputPath,
                                    categoryOutputPath,
                                    category,
                                    serializer,
                                    linkResolver,
                                    listWzEntries,
                                    extractedListWzImages,
                                    extractedImageCaseMap,
                                    ref processedCount,
                                    progressCallback,
                                    result);
                            }
                        }
                    }
                    finally
                    {
                        // Dispose all WZ files
                        foreach (var (wzFile, _, _) in loadedWzFiles)
                        {
                            wzFile.Dispose();
                        }
                        loadedWzFiles.Clear();

                        // Dispose all MS files
                        foreach (var msFile in loadedMsFiles)
                        {
                            msFile.Dispose();
                        }
                        loadedMsFiles.Clear();

                        // Force garbage collection after category extraction
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }, cancellationToken);

                result.Success = true;
                result.ImagesExtracted = processedCount;

                // Copy link resolution statistics
                if (linkResolver != null)
                {
                    result.LinksResolved = linkResolver.LinksResolved;
                    result.LinksFailed = linkResolver.LinksFailed;
                    if (linkResolver.FailedLinks.Count > 0)
                    {
                        foreach (var failedLink in linkResolver.FailedLinks.Take(10))
                        {
                            result.Errors.Add($"Broken link (missing data in original WZ): {failedLink}");
                        }
                        if (linkResolver.FailedLinks.Count > 10)
                        {
                            result.Errors.Add($"... and {linkResolver.FailedLinks.Count - 10} more broken links (missing data in original WZ files)");
                        }
                    }
                }

                // Persist exact image filename casing so packer can restore canonical names
                // on case-insensitive filesystems if files are later edited/replaced.
                if (!isPacksCategory && extractedImageCaseMap.Count > 0)
                {
                    SaveImageCaseMap(categoryOutputPath, extractedImageCaseMap);
                }
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
        /// <param name="versionPath">Path to the extracted version</param>
        /// <param name="extractedCategories">Categories that were extracted (optional - if null, checks all required)</param>
        public ExtractionValidationResult ValidateExtraction(string versionPath, List<string> extractedCategories = null)
        {
            var result = new ExtractionValidationResult
            {
                VersionPath = versionPath,
                CheckedAt = DateTime.Now
            };

            // Only check for required categories if they were in the extraction list (or if extracting all)
            bool checkString = extractedCategories == null || extractedCategories.Contains("String", StringComparer.OrdinalIgnoreCase);
            bool checkMap = extractedCategories == null || extractedCategories.Contains("Map", StringComparer.OrdinalIgnoreCase);

            // Check String category exists with Map.img (only if String was extracted)
            if (checkString)
            {
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
            }

            // Check Map category exists with at least some maps (only if Map was extracted)
            if (checkMap)
            {
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
            }

            // Count total images in all categories that exist
            foreach (var category in STANDARD_WZ_FILES)
            {
                string categoryPath = Path.Combine(versionPath, category);
                if (Directory.Exists(categoryPath))
                {
                    int count = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Count();
                    result.CategoryImageCounts[category] = count;
                    if (category != "Map") // Already counted above if Map was checked
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

            // Check if this is specifically beta Data.wz format (single Data.wz with all categories)
            bool isBetaDataWz = WzFileManager.DetectBetaDataWzFormat(mapleStoryPath);

            if (isBetaDataWz)
            {
                // Beta Data.wz format - add "Data" as a single category for extraction
                // The actual subdirectories will be extracted by ExtractBetaDataWzCategory
                // Note: For UI selection, use PopulateWzFileList which lists subdirectories
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
                        // Skip Packs - it will be added separately if .ms files exist
                        if (dirName.Equals("Packs", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!categories.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                        {
                            categories.Add(dirName);
                        }
                    }

                    // Check for Packs folder - add it if the folder exists
                    // (extraction will handle .ms files within)
                    string packsPath = Path.Combine(dataPath, "Packs");
                    if (Directory.Exists(packsPath))
                    {
                        categories.Add("Packs");
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
        /// Also handles Packs category with .ms files and beta Data.wz format
        /// </summary>
        private List<string> FindCategoryWzFiles(string mapleStoryPath, string category, bool is64Bit, bool isBetaDataWz = false)
        {
            var files = new List<string>();

            // Special handling for beta Data.wz format - all categories come from single Data.wz
            if (isBetaDataWz)
            {
                string dataWzPath = Path.Combine(mapleStoryPath, "Data.wz");
                if (File.Exists(dataWzPath))
                {
                    files.Add(dataWzPath);
                }
                return files;
            }

            // Special handling for Packs category with .ms files (search recursively)
            if (category.Equals("Packs", StringComparison.OrdinalIgnoreCase))
            {
                string packsPath = Path.Combine(mapleStoryPath, "Data", "Packs");
                if (Directory.Exists(packsPath))
                {
                    files.AddRange(Directory.EnumerateFiles(packsPath, "*.ms", SearchOption.AllDirectories));
                }
                return files;
            }

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
        /// Gets the list of categories covered by .ms files in the Packs folder.
        /// Used to avoid duplicate extraction when both Packs and individual categories are selected.
        /// </summary>
        private HashSet<string> GetCategoriesInPacks(string mapleStoryPath)
        {
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string packsPath = Path.Combine(mapleStoryPath, "Data", "Packs");
            if (!Directory.Exists(packsPath))
                return categories;

            // Scan .ms files and extract category names from filenames
            // Format: "Mob_00000.ms" -> "Mob"
            foreach (var msFile in Directory.EnumerateFiles(packsPath, "*.ms", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(msFile);
                int underscoreIndex = fileName.IndexOf('_');
                if (underscoreIndex > 0)
                {
                    string category = fileName.Substring(0, underscoreIndex);
                    categories.Add(category);
                }
            }

            return categories;
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

            // Check for beta Data.wz format
            bool isBetaDataWz = WzFileManager.DetectBetaDataWzFormat(mapleStoryPath);

            // For beta format, we need to load Data.wz once and count from specific subdirectories
            WzFile betaDataWzFile = null;
            if (isBetaDataWz)
            {
                string dataWzPath = Path.Combine(mapleStoryPath, "Data.wz");
                if (File.Exists(dataWzPath))
                {
                    try
                    {
                        betaDataWzFile = new WzFile(dataWzPath, encryption);
                        betaDataWzFile.ParseWzFile();
                    }
                    catch
                    {
                        betaDataWzFile?.Dispose();
                        betaDataWzFile = null;
                    }
                }
            }

            try
            {
                foreach (var category in categories)
                {
                    // Handle beta Data.wz format - count from specific subdirectory
                    if (isBetaDataWz && betaDataWzFile != null)
                    {
                        // Handle special "_Root" category
                        if (category.Equals("_Root", StringComparison.OrdinalIgnoreCase))
                        {
                            total += betaDataWzFile.WzDirectory.WzImages?.Count ?? 0;
                            continue;
                        }

                        // Find the category subdirectory
                        foreach (var dir in betaDataWzFile.WzDirectory.WzDirectories)
                        {
                            if (dir.Name.Equals(category, StringComparison.OrdinalIgnoreCase))
                            {
                                total += dir.CountImages();
                                break;
                            }
                        }
                        continue;
                    }

                    var wzFiles = FindCategoryWzFiles(mapleStoryPath, category, is64Bit, isBetaDataWz);

                    // Handle Packs category with .ms files
                    if (category.Equals("Packs", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var msFilePath in wzFiles)
                        {
                            if (!File.Exists(msFilePath))
                                continue;

                            try
                            {
                                var fileStream = File.OpenRead(msFilePath);
                                var memoryStream = new MemoryStream();
                                fileStream.CopyTo(memoryStream);
                                fileStream.Close();
                                memoryStream.Position = 0;

                                using (var msFile = new WzMsFile(memoryStream, Path.GetFileName(msFilePath), msFilePath, true))
                                {
                                    msFile.ReadEntries();
                                    total += msFile.Entries.Count;
                                }
                            }
                            catch
                            {
                                // Skip files that can't be opened
                            }
                        }
                        continue;
                    }

                    foreach (var wzFilePath in wzFiles)
                    {
                        if (!File.Exists(wzFilePath))
                            continue;

                        // Count List.wz as 1 image (it will be extracted as JSON)
                        if (WzTool.IsListFile(wzFilePath))
                        {
                            total++;
                            continue;
                        }

                        try
                        {
                            using (var wzFile = new WzFile(wzFilePath, encryption))
                            {
                                var parseStatus = wzFile.ParseWzFile();
                                if (parseStatus == WzFileParseStatus.Success && wzFile.WzDirectory != null)
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
            }
            finally
            {
                betaDataWzFile?.Dispose();
            }

            return total;
        }

        /// <summary>
        /// Recursively extracts a WZ node (directory or image)
        /// </summary>
        private void ExtractWzNode(
            WzDirectory directory,
            string outputPath,
            string categoryOutputRootPath,
            string categoryName,
            WzImgSerializer serializer,
            WzLinkResolver linkResolver,
            HashSet<string> listWzEntries,
            ConcurrentDictionary<string, byte> extractedListWzImages,
            Dictionary<string, string> extractedImageCaseMap,
            ref int processedCount,
            Action<int, int, string> progressCallback,
            CategoryExtractionResult result)
        {
            if (directory == null)
                return;

            // Extract subdirectories first
            if (directory.WzDirectories != null)
            {
                foreach (var subDir in directory.WzDirectories)
                {
                    if (subDir == null) continue;

                    string subDirPath = Path.Combine(outputPath, EscapeFileName(subDir.Name));
                    if (!Directory.Exists(subDirPath))
                    {
                        Directory.CreateDirectory(subDirPath);
                    }

                    ExtractWzNode(
                        subDir,
                        subDirPath,
                        categoryOutputRootPath,
                        categoryName,
                        serializer,
                        linkResolver,
                        listWzEntries,
                        extractedListWzImages,
                        extractedImageCaseMap,
                        ref processedCount,
                        progressCallback,
                        result);
                }
            }

            // Extract images
            if (directory.WzImages == null)
                return;

            foreach (var img in directory.WzImages)
            {
                try
                {
                    // Resolve _inlink/_outlink references before serialization
                    if (linkResolver != null)
                    {
                        linkResolver.ResolveLinksInImage(img);
                    }

                    string imgPath = Path.Combine(outputPath, EscapeFileName(img.Name));
                    EnsureExactFilePathCase(imgPath);
                    serializer.SerializeImage(img, imgPath);
                    TryRecordImageCaseMap(categoryOutputRootPath, imgPath, extractedImageCaseMap);

                    processedCount++;
                    result.TotalSize += new FileInfo(imgPath).Length;

                    // Track if this image was in List.wz (for filtering decrypted entries)
                    if (listWzEntries != null && extractedListWzImages != null)
                    {
                        // Build the List.wz style path: Category/SubDir/.../ImageName.img
                        string listWzPath = BuildListWzPath(categoryName, directory, img.Name);
                        if (listWzEntries.Contains(listWzPath))
                        {
                            extractedListWzImages.TryAdd(listWzPath, 0);
                        }
                    }

                    progressCallback?.Invoke(processedCount, 0, img.Name);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to extract {img.Name}: {ex.Message}");
                }
                finally
                {
                    // CRITICAL: Release parsed image data to prevent memory leak
                    // After serialization, the image's properties (including bitmaps)
                    // would otherwise stay in memory until WzFile.Dispose() is called.
                    // This caused massive memory accumulation when extracting large WZ files.
                    img.UnparseImage();
                }
            }
        }

        /// <summary>
        /// Builds the List.wz style path for an image (e.g., "Mob/0100100.img")
        /// </summary>
        private string BuildListWzPath(string categoryName, WzDirectory directory, string imageName)
        {
            // Build path from directory hierarchy
            var pathParts = new List<string> { categoryName };

            // Walk up the directory tree to build the full path
            var current = directory;
            var dirPath = new Stack<string>();
            while (current != null && current.Parent is WzDirectory parentDir)
            {
                dirPath.Push(current.Name);
                current = parentDir;
            }
            while (dirPath.Count > 0)
            {
                pathParts.Add(dirPath.Pop());
            }

            pathParts.Add(imageName);
            return string.Join("/", pathParts);
        }

        /// <summary>
        /// Extracts a specific category subdirectory from beta Data.wz file.
        /// Beta MapleStory (v0.01-v0.30) stores all data in a single Data.wz with subdirectories.
        /// </summary>
        private void ExtractBetaDataWzCategory(
            List<string> wzFilePaths,
            string category,
            string categoryOutputPath,
            WzMapleVersion encryption,
            WzImgSerializer serializer,
            WzLinkResolver linkResolver,
            HashSet<string> listWzEntries,
            ConcurrentDictionary<string, byte> extractedListWzImages,
            Dictionary<string, string> extractedImageCaseMap,
            List<(WzFile wzFile, bool isCanvasFile, string relativePath)> loadedWzFiles,
            ref int processedCount,
            Action<int, int, string> progressCallback,
            CategoryExtractionResult result,
            CancellationToken cancellationToken)
        {
            if (wzFilePaths.Count == 0)
            {
                result.Errors.Add("No Data.wz file found for beta extraction");
                return;
            }

            string dataWzPath = wzFilePaths[0];
            if (!File.Exists(dataWzPath))
            {
                result.Errors.Add($"Data.wz file not found: {dataWzPath}");
                return;
            }

            var wzFile = new WzFile(dataWzPath, encryption);
            var parseStatus = wzFile.ParseWzFile();

            if (parseStatus != WzFileParseStatus.Success)
            {
                result.Errors.Add($"Failed to parse Data.wz: {parseStatus}");
                wzFile.Dispose();
                return;
            }

            // Capture the detected patch version from Data.wz
            if (wzFile.Version > 0)
            {
                result.DetectedPatchVersion = wzFile.Version;
                Debug.WriteLine($"[WzExtraction] Detected patch version {wzFile.Version} from beta Data.wz");
            }

            // Capture the detected locale if available
            if (wzFile.MapleLocaleVersion != MapleStoryLocalisation.Not_Known)
            {
                result.DetectedLocale = wzFile.MapleLocaleVersion.ToString();
                Debug.WriteLine($"[WzExtraction] Detected locale {result.DetectedLocale} from beta Data.wz");
            }

            loadedWzFiles.Add((wzFile, false, ""));

            try
            {
                // Find the category subdirectory within Data.wz
                WzDirectory categoryDir = null;

                // Handle special "_Root" category for images at root level
                if (category.Equals("_Root", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract images directly from root of Data.wz
                    if (wzFile.WzDirectory.WzImages != null && wzFile.WzDirectory.WzImages.Count > 0)
                    {
                        foreach (var img in wzFile.WzDirectory.WzImages)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                if (linkResolver != null)
                                {
                                    linkResolver.ResolveLinksInImage(img);
                                }

                                string imgPath = Path.Combine(categoryOutputPath, ProgressingWzSerializer.EscapeInvalidFilePathNames(img.Name));
                                EnsureExactFilePathCase(imgPath);
                                serializer.SerializeImage(img, imgPath);
                                TryRecordImageCaseMap(categoryOutputPath, imgPath, extractedImageCaseMap);

                                processedCount++;
                                result.TotalSize += new FileInfo(imgPath).Length;
                                progressCallback?.Invoke(processedCount, 0, img.Name);
                            }
                            catch (Exception ex)
                            {
                                result.Errors.Add($"Failed to extract {img.Name}: {ex.Message}");
                            }
                            finally
                            {
                                img.UnparseImage();
                            }
                        }
                    }
                    return;
                }

                // Look for the category directory in Data.wz
                foreach (var dir in wzFile.WzDirectory.WzDirectories)
                {
                    if (dir.Name.Equals(category, StringComparison.OrdinalIgnoreCase))
                    {
                        categoryDir = dir;
                        break;
                    }
                }

                if (categoryDir == null)
                {
                    result.Errors.Add($"Category '{category}' not found in Data.wz");
                    return;
                }

                // Set up link resolver with the entire Data.wz for cross-category link resolution
                if (linkResolver != null)
                {
                    linkResolver.SetCategoryWzFiles(new List<WzFile> { wzFile }, category);
                }

                // Extract the category directory
                ExtractWzNode(
                    categoryDir,
                    categoryOutputPath,
                    categoryOutputPath,
                    category,
                    serializer,
                    linkResolver,
                    listWzEntries,
                    extractedListWzImages,
                    extractedImageCaseMap,
                    ref processedCount,
                    progressCallback,
                    result);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error extracting category '{category}' from Data.wz: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts .ms files from the Packs folder.
        /// Each .ms file contains images that belong to different categories (e.g., Mob/0100000.img)
        /// </summary>
        private void ExtractPacksMsFiles(
            List<string> msFilePaths,
            string outputVersionPath,
            WzMapleVersion encryption,
            WzImgSerializer serializer,
            WzLinkResolver linkResolver,
            HashSet<string> listWzEntries,
            ConcurrentDictionary<string, byte> extractedListWzImages,
            List<WzMsFile> loadedMsFiles,
            ref int processedCount,
            Action<int, int, string> progressCallback,
            CategoryExtractionResult result,
            CancellationToken cancellationToken)
        {
            // Group .ms files by category (e.g., "Mob_00000.ms" -> "Mob")
            // This allows us to load _Canvas WZ files once per category
            var msFilesByCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var msFilePath in msFilePaths)
            {
                string msFileName = Path.GetFileNameWithoutExtension(msFilePath);
                // Extract category from filename: "Mob_00000" -> "Mob"
                int underscoreIndex = msFileName.IndexOf('_');
                string category = underscoreIndex > 0 ? msFileName.Substring(0, underscoreIndex) : msFileName;

                if (!msFilesByCategory.ContainsKey(category))
                    msFilesByCategory[category] = new List<string>();
                msFilesByCategory[category].Add(msFilePath);
            }

            // Get the MapleStory base path from the first .ms file path
            string mapleStoryPath = null;
            if (msFilePaths.Count > 0)
            {
                // Path is like: D:\...\Data\Packs\Mob_00000.ms
                // We need: D:\...
                string packsPath = Path.GetDirectoryName(msFilePaths[0]);
                string dataPath = Path.GetDirectoryName(packsPath);
                mapleStoryPath = Path.GetDirectoryName(dataPath);
            }

            // Track loaded _Canvas WZ files for disposal
            var loadedCanvasFiles = new List<WzFile>();

            try
            {
                // Process each category
                foreach (var categoryGroup in msFilesByCategory)
                {
                    string category = categoryGroup.Key;
                    var categoryMsFiles = categoryGroup.Value;

                    // Load _Canvas WZ files for this category if link resolution is enabled
                    // Search recursively for ALL _Canvas folders (they can be in subdirectories like Roguelike/Skill/_Canvas)
                    if (linkResolver != null && mapleStoryPath != null)
                    {
                        string categoryPath = Path.Combine(mapleStoryPath, "Data", category);
                        if (Directory.Exists(categoryPath))
                        {
                            var categoryWzFiles = new List<WzFile>();

                            // Find all _Canvas directories recursively
                            var canvasDirs = Directory.EnumerateDirectories(categoryPath, "_Canvas", SearchOption.AllDirectories).ToList();

                            foreach (var canvasDir in canvasDirs)
                            {
                                var canvasWzFiles = Directory.EnumerateFiles(canvasDir, "*.wz", SearchOption.AllDirectories).ToList();

                                foreach (var canvasWzPath in canvasWzFiles)
                                {
                                    try
                                    {
                                        // Use the same encryption as the main extraction
                                        var canvasWzFile = new WzFile(canvasWzPath, encryption);
                                        var parseStatus = canvasWzFile.ParseWzFile();
                                        if (parseStatus == WzFileParseStatus.Success)
                                        {
                                            categoryWzFiles.Add(canvasWzFile);
                                            loadedCanvasFiles.Add(canvasWzFile);
                                        }
                                        else
                                        {
                                            // Log the failure for debugging
                                            Debug.WriteLine($"[WzExtraction] Failed to parse _Canvas file {canvasWzPath}: {parseStatus}");
                                            canvasWzFile.Dispose();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[WzExtraction] Exception loading _Canvas file {canvasWzPath}: {ex.Message}");
                                    }
                                }
                            }

                            if (categoryWzFiles.Count > 0)
                            {
                                linkResolver.SetCategoryWzFiles(categoryWzFiles, category);
                            }
                        }
                    }

                    // Process all .ms files for this category
                    foreach (var msFilePath in categoryMsFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!File.Exists(msFilePath))
                            continue;

                        string msFileName = Path.GetFileName(msFilePath);

                        try
                        {
                            // Load the .ms file
                            var fileStream = File.OpenRead(msFilePath);
                            var memoryStream = new MemoryStream();
                            fileStream.CopyTo(memoryStream);
                            fileStream.Close();
                            memoryStream.Position = 0;

                            var msFile = new WzMsFile(memoryStream, msFileName, msFilePath, true);
                            msFile.ReadEntries();
                            loadedMsFiles.Add(msFile);

                            // Load as WzFile for extraction
                            var wzFile = msFile.LoadAsWzFile();

                            // Extract each image to its proper category folder
                            // Entry names are like "Mob/0100000.img" - parse to get category and image name
                            foreach (var entry in msFile.Entries)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                try
                                {
                                    // Parse entry name: "Category/ImageName.img" or "Category/SubDir/ImageName.img"
                                    string entryName = entry.Name;
                                    int firstSlash = entryName.IndexOf('/');
                                    if (firstSlash < 0)
                                    {
                                        // No category prefix, skip
                                        result.Errors.Add($"Invalid .ms entry name (no category): {entryName}");
                                        continue;
                                    }

                                    string entryCategory = entryName.Substring(0, firstSlash);
                                    string remainingPath = entryName.Substring(firstSlash + 1);

                                    // Create category output folder if needed
                                    string categoryOutputPath = Path.Combine(outputVersionPath, entryCategory);
                                    if (!Directory.Exists(categoryOutputPath))
                                    {
                                        Directory.CreateDirectory(categoryOutputPath);
                                    }

                                    // Handle subdirectory paths like "Map/Map/Map0/000010000.img"
                                    string imgOutputPath;
                                    int lastSlash = remainingPath.LastIndexOf('/');
                                    if (lastSlash >= 0)
                                    {
                                        string subDir = remainingPath.Substring(0, lastSlash);
                                        string imgName = remainingPath.Substring(lastSlash + 1);
                                        string subDirPath = Path.Combine(categoryOutputPath, subDir);
                                        if (!Directory.Exists(subDirPath))
                                        {
                                            Directory.CreateDirectory(subDirPath);
                                        }
                                        imgOutputPath = Path.Combine(subDirPath, EscapeFileName(imgName));
                                    }
                                    else
                                    {
                                        imgOutputPath = Path.Combine(categoryOutputPath, EscapeFileName(remainingPath));
                                    }

                                    // Find the corresponding WzImage in the loaded WzFile
                                    string imgBaseName = Path.GetFileName(entryName);
                                    var wzImage = wzFile.WzDirectory.WzImages.FirstOrDefault(img => img.Name == imgBaseName);

                                    if (wzImage == null)
                                    {
                                        result.Errors.Add($"WzImage not found for .ms entry: {entryName}");
                                        continue;
                                    }

                                    // Resolve links if enabled
                                    if (linkResolver != null)
                                    {
                                        linkResolver.ResolveLinksInImage(wzImage);
                                    }

                                    // Serialize the image
                                    EnsureExactFilePathCase(imgOutputPath);
                                    serializer.SerializeImage(wzImage, imgOutputPath);

                                    processedCount++;
                                    result.TotalSize += new FileInfo(imgOutputPath).Length;

                                    // Track if this image was in List.wz
                                    if (listWzEntries != null && extractedListWzImages != null)
                                    {
                                        if (listWzEntries.Contains(entryName))
                                        {
                                            extractedListWzImages.TryAdd(entryName, 0);
                                        }
                                    }

                                    progressCallback?.Invoke(processedCount, 0, imgBaseName);
                                }
                                catch (Exception ex)
                                {
                                    result.Errors.Add($"Failed to extract .ms entry {entry.Name}: {ex.Message}");
                                }
                            }

                            wzFile.Dispose();
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Failed to load .ms file {msFileName}: {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                // Dispose all loaded _Canvas WZ files
                foreach (var canvasFile in loadedCanvasFiles)
                {
                    canvasFile.Dispose();
                }
                loadedCanvasFiles.Clear();
            }
        }

        /// <summary>
        /// Writes List.json with only entries that were NOT successfully decrypted
        /// </summary>
        private void WriteFilteredListJson(
            string outputVersionPath,
            HashSet<string> listWzEntries,
            ConcurrentDictionary<string, byte> extractedListWzImages)
        {
            // Create List directory
            string listOutputPath = Path.Combine(outputVersionPath, "List");
            if (!Directory.Exists(listOutputPath))
            {
                Directory.CreateDirectory(listOutputPath);
            }

            // Filter out successfully decrypted entries
            var remainingEntries = listWzEntries
                .Where(entry => !extractedListWzImages.ContainsKey(entry))
                .ToList();

            var decryptedEntries = extractedListWzImages.Keys.ToList();

            // Create the JSON structure
            var listData = new
            {
                Type = "ListWz",
                Description = "Pre-Big Bang List.wz - images with different encryption",
                OriginalCount = listWzEntries.Count,
                DecryptedCount = decryptedEntries.Count,
                RemainingCount = remainingEntries.Count,
                // Keep "Entries" for backward compatibility with older packers.
                Entries = remainingEntries,
                DecryptedEntries = decryptedEntries,
                RemainingEntries = remainingEntries
            };

            string json = JsonConvert.SerializeObject(listData, Formatting.Indented);
            File.WriteAllText(Path.Combine(listOutputPath, "List.json"), json);

            Debug.WriteLine($"[WzExtraction] List.wz: {listWzEntries.Count} total, {decryptedEntries.Count} decrypted, {remainingEntries.Count} remaining");
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
            bool isBetaMs,
            bool isBigBang2,
            string outputPath,
            Dictionary<string, CategoryExtractionResult> categories,
            short detectedPatchVersion = 0,
            string detectedLocale = null)
        {
            var versionInfo = new VersionInfo
            {
                Version = versionId,
                DisplayName = displayName,
                Encryption = encryption.ToString(),
                Is64Bit = is64Bit,
                IsPreBB = isPreBB,
                IsBetaMs = isBetaMs,
                IsBigBang2 = isBigBang2,
                ExtractedDate = DateTime.Now,
                DirectoryPath = outputPath,
                PatchVersion = detectedPatchVersion,
                SourceRegion = detectedLocale
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

            return versionInfo;
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

        /// <summary>
        /// Ensures existing files use the exact requested casing on case-insensitive filesystems.
        /// </summary>
        private void EnsureExactFilePathCase(string desiredFilePath)
        {
            string directoryPath = Path.GetDirectoryName(desiredFilePath);
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            string desiredFileName = Path.GetFileName(desiredFilePath);
            string existingFilePath = Directory.EnumerateFiles(directoryPath)
                .FirstOrDefault(path =>
                    string.Equals(Path.GetFileName(path), desiredFileName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(existingFilePath))
            {
                return;
            }

            string existingFileName = Path.GetFileName(existingFilePath);
            if (string.Equals(existingFileName, desiredFileName, StringComparison.Ordinal))
            {
                return;
            }

            string tempPath = Path.Combine(
                directoryPath,
                $"__casefix_{Guid.NewGuid():N}{Path.GetExtension(desiredFileName)}");

            File.Move(existingFilePath, tempPath);
            File.Move(tempPath, Path.Combine(directoryPath, desiredFileName));
        }

        /// <summary>
        /// Records canonical casing for extracted IMG filenames.
        /// Only entries with uppercase characters are tracked to keep metadata compact.
        /// </summary>
        private void TryRecordImageCaseMap(
            string categoryOutputRootPath,
            string imageOutputPath,
            Dictionary<string, string> imageCaseMap)
        {
            if (imageCaseMap == null ||
                string.IsNullOrEmpty(categoryOutputRootPath) ||
                string.IsNullOrEmpty(imageOutputPath))
            {
                return;
            }

            string relativePath = Path.GetRelativePath(categoryOutputRootPath, imageOutputPath)
                .Replace(Path.DirectorySeparatorChar, '/');

            string fileName = Path.GetFileName(relativePath);
            if (!fileName.Any(char.IsUpper))
            {
                return;
            }

            string lowerKey = relativePath.ToLowerInvariant();
            if (!imageCaseMap.ContainsKey(lowerKey))
            {
                imageCaseMap[lowerKey] = relativePath;
            }
        }

        /// <summary>
        /// Saves extracted filename case metadata for packer-side restoration.
        /// </summary>
        private void SaveImageCaseMap(string categoryOutputPath, Dictionary<string, string> imageCaseMap)
        {
            try
            {
                var payload = new ImageCaseMapData
                {
                    Format = "ImgCaseMapV1",
                    Entries = imageCaseMap
                        .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal)
                };

                string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                string mapPath = Path.Combine(categoryOutputPath, IMG_CASE_MAP_FILENAME);
                File.WriteAllText(mapPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WzExtraction] Failed to save {IMG_CASE_MAP_FILENAME}: {ex.Message}");
            }
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

        public int TotalLinksResolved =>
            CategoriesExtracted.Values.Sum(c => c.LinksResolved);

        public int TotalLinksFailed =>
            CategoriesExtracted.Values.Sum(c => c.LinksFailed);
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
        public int LinksResolved { get; set; }
        public int LinksFailed { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// The detected MapleStory patch version from the WZ file (e.g., 83, 176, 230)
        /// </summary>
        public short DetectedPatchVersion { get; set; }

        /// <summary>
        /// The detected MapleStory locale/region from MapleStory.exe (e.g., GMS, KMS, MSEA)
        /// </summary>
        public string DetectedLocale { get; set; }
    }

    internal class ImageCaseMapData
    {
        public string Format { get; set; } = "ImgCaseMapV1";
        public Dictionary<string, string> Entries { get; set; } = new(StringComparer.Ordinal);
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
