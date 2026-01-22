using MapleLib.WzLib;
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
        /// <param name="resolveLinks">Whether to resolve _inlink/_outlink canvas references (default true)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Progress reporter</param>
        /// <returns>Extraction result with statistics</returns>
        public async Task<ExtractionResult> ExtractAsync(
            string mapleStoryPath,
            string outputVersionPath,
            string versionId,
            string displayName,
            WzMapleVersion encryption,
            bool resolveLinks = true,
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
        /// <param name="resolveLinks">Whether to resolve _inlink/_outlink canvas references (default true)</param>
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
            bool resolveLinks = true,
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

                // Write filtered List.json if we had List.wz entries
                if (listWzEntries != null && listWzEntries.Count > 0)
                {
                    WriteFilteredListJson(outputVersionPath, listWzEntries, extractedListWzImages);
                }

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

            try
            {
                // Find WZ files for this category (could be multiple like Mob, Mob001, Mob2)
                var wzFilePaths = FindCategoryWzFiles(mapleStoryPath, category, is64Bit);

                // Use BMS encryption (all zeroes) for extracted IMG files - plain/unencrypted format
                var serializer = WzImgSerializer.CreateForImgExtraction();
                int processedCount = 0;

                // Load ALL WZ files for this category simultaneously so outlinks can resolve
                // across split files (e.g., Mob001.wz referencing Mob/xxx.img in Mob.wz)
                var loadedWzFiles = new List<WzFile>();

                await Task.Run(() =>
                {
                    try
                    {
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

                            loadedWzFiles.Add(wzFile);
                        }

                        // Set all loaded WZ files on the resolver for cross-file outlink resolution
                        if (linkResolver != null && loadedWzFiles.Count > 0)
                        {
                            linkResolver.SetCategoryWzFiles(loadedWzFiles, category);
                        }

                        // Now extract from all loaded WZ files (outlinks can resolve across them)
                        foreach (var wzFile in loadedWzFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            ExtractWzNode(
                                wzFile.WzDirectory,
                                categoryOutputPath,
                                category,
                                serializer,
                                linkResolver,
                                listWzEntries,
                                extractedListWzImages,
                                ref processedCount,
                                progressCallback,
                                result);
                        }
                    }
                    finally
                    {
                        // Dispose all WZ files
                        foreach (var wzFile in loadedWzFiles)
                        {
                            wzFile.Dispose();
                        }
                        loadedWzFiles.Clear();

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
            string categoryName,
            WzImgSerializer serializer,
            WzLinkResolver linkResolver,
            HashSet<string> listWzEntries,
            ConcurrentDictionary<string, byte> extractedListWzImages,
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

                ExtractWzNode(subDir, subDirPath, categoryName, serializer, linkResolver, listWzEntries, extractedListWzImages, ref processedCount, progressCallback, result);
            }

            // Extract images
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
                    serializer.SerializeImage(img, imgPath);

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
