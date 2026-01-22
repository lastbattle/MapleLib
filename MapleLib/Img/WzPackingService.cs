using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
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

        /// <summary>
        /// Name of the canvas directory for 64-bit format
        /// </summary>
        public const string CANVAS_DIRECTORY_NAME = "_Canvas";

        /// <summary>
        /// Maximum size of a single WZ file in bytes for 64-bit format (150MB)
        /// When exceeded, content will be split into multiple numbered WZ files
        /// </summary>
        private const long MAX_WZ_FILE_SIZE = 150_000_000;

        /// <summary>
        /// Maximum size of a single Canvas WZ file in bytes (500MB)
        /// </summary>
        private const long MAX_CANVAS_FILE_SIZE = 500_000_000;

        /// <summary>
        /// Maximum number of images per Canvas WZ file
        /// </summary>
        private const int MAX_IMAGES_PER_CANVAS = 1000;

        /// <summary>
        /// Minimum canvas size in bytes to separate (100KB)
        /// Canvases larger than this will be moved to _Canvas folder
        /// </summary>
        private const long CANVAS_SIZE_THRESHOLD = 100_000;

        /// <summary>
        /// Maximum degree of parallelism for concurrent IMG file processing
        /// </summary>
        private static readonly int MAX_DEGREE_OF_PARALLELISM = Math.Max(1, Environment.ProcessorCount - 1);
        #endregion

        #region Helper Classes for Concurrent Processing
        /// <summary>
        /// Information about an IMG file to be processed
        /// </summary>
        private class ImgFileInfo
        {
            public string FilePath { get; set; }
            public string RelativePath { get; set; }
            public WzDirectory ParentDirectory { get; set; }
            public long FileSize { get; set; }
        }

        /// <summary>
        /// Result of processing a single IMG file
        /// </summary>
        private class ImgProcessingResult
        {
            public bool Success { get; set; }
            public WzImage Image { get; set; }
            public WzDirectory ParentDirectory { get; set; }
            public string RelativePath { get; set; }
            public string ErrorMessage { get; set; }
        }
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
        /// <param name="separateCanvas">Whether to save canvas images in separate _Canvas folders</param>
        public async Task<CategoryPackingResult> PackCategoryAsync(
            string versionPath,
            string outputPath,
            string category,
            WzMapleVersion encryption,
            short patchVersion,
            bool saveAs64Bit,
            CancellationToken cancellationToken,
            Action<int, int, string> progressCallback = null,
            bool separateCanvas = false)
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
                // Get WZ IV for encryption
                byte[] wzIv = WzTool.GetIvByMapleVersion(encryption);

                // Special handling for List.wz files (pre-Big Bang format)
                // These are stored as JSON during extraction and need to be converted back to List.wz format
                if (category.Equals("List", StringComparison.OrdinalIgnoreCase))
                {
                    string listJsonPath = Path.Combine(categoryPath, "List.json");
                    if (File.Exists(listJsonPath))
                    {
                        try
                        {
                            string jsonContent = File.ReadAllText(listJsonPath);

                            // Check if this is our JSON format (extracted List.wz)
                            if (jsonContent.TrimStart().StartsWith("{"))
                            {
                                var listData = JsonConvert.DeserializeObject<ListWzJsonFormat>(jsonContent);

                                if (listData?.Entries != null && listData.Entries.Count > 0)
                                {
                                    // Determine output path for List.wz
                                    string listWzPath;
                                    if (saveAs64Bit)
                                    {
                                        string categoryOutputPath = Path.Combine(outputPath, "Data", category);
                                        if (!Directory.Exists(categoryOutputPath))
                                        {
                                            Directory.CreateDirectory(categoryOutputPath);
                                        }
                                        listWzPath = Path.Combine(categoryOutputPath, "List_000.wz");
                                    }
                                    else
                                    {
                                        listWzPath = Path.Combine(outputPath, "List.wz");
                                    }

                                    // Use ListFileParser to save in proper List.wz format
                                    ListFileParser.SaveToDisk(listWzPath, encryption, listData.Entries);

                                    result.Success = true;
                                    result.ImagesPacked = 1;
                                    result.OutputFilePath = listWzPath;
                                    if (File.Exists(listWzPath))
                                    {
                                        result.OutputFileSize = new FileInfo(listWzPath).Length;
                                    }
                                    result.EndTime = DateTime.Now;

                                    Debug.WriteLine($"[PackCategory] List: Created List.wz from JSON format ({result.OutputFileSize} bytes)");
                                    return result;
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Not JSON format, fall through to normal processing
                            Debug.WriteLine($"[PackCategory] List.img is not in JSON format, attempting normal WZ processing");
                        }
                    }
                }

                if (saveAs64Bit)
                {
                    // 64-bit format with potential file splitting
                    string categoryOutputPath = Path.Combine(outputPath, "Data", category);
                    if (!Directory.Exists(categoryOutputPath))
                    {
                        Directory.CreateDirectory(categoryOutputPath);
                    }

                    // Collect all IMG files first to determine splitting
                    var allImgFiles = CollectImgFiles(categoryPath);
                    long totalSize = allImgFiles.Sum(f => f.FileSize);

                    Debug.WriteLine($"[PackCategory] {category}: Found {allImgFiles.Count} IMG files, total size: {totalSize / 1024 / 1024}MB");

                    // Group files into size-limited chunks
                    var chunks = GroupImgFilesBySize(allImgFiles, MAX_WZ_FILE_SIZE);
                    int lastWzIndex = chunks.Count - 1;

                    Debug.WriteLine($"[PackCategory] {category}: Split into {chunks.Count} WZ file(s)");

                    // Create .ini file with correct last index
                    string iniFilePath = Path.Combine(categoryOutputPath, $"{category}.ini");
                    File.WriteAllText(iniFilePath, $"LastWzIndex|{lastWzIndex}");

                    int totalCount = allImgFiles.Count;
                    int processedCount = 0;
                    long totalOutputSize = 0;

                    await Task.Run(() =>
                    {
                        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var chunk = chunks[chunkIndex];
                            string wzFileName = $"{category}_{chunkIndex:D3}.wz";
                            string wzFilePath = Path.Combine(categoryOutputPath, wzFileName);

                            Debug.WriteLine($"[PackCategory] {category}: Creating {wzFileName} with {chunk.Count} files");

                            using (var wzFile = new WzFile(patchVersion, encryption))
                            {
                                wzFile.Name = wzFileName;

                                // Build directory structure for this chunk's files
                                BuildDirectoryStructureForFiles(wzFile.WzDirectory, chunk, wzIv);

                                // Process IMG files concurrently
                                var processingResults = new ConcurrentBag<ImgProcessingResult>();
                                var lockObj = new object();

                                var parallelOptions = new ParallelOptions
                                {
                                    MaxDegreeOfParallelism = MAX_DEGREE_OF_PARALLELISM,
                                    CancellationToken = cancellationToken
                                };

                                Parallel.ForEach(chunk, parallelOptions, imgFileInfo =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    var processingResult = ProcessSingleImgFile(imgFileInfo, wzIv);
                                    processingResults.Add(processingResult);

                                    int currentCount = Interlocked.Increment(ref processedCount);
                                    progressCallback?.Invoke(currentCount, totalCount, imgFileInfo.RelativePath);
                                });

                                // Add processed images to parent directories
                                foreach (var processingResult in processingResults)
                                {
                                    if (processingResult.Success && processingResult.Image != null)
                                    {
                                        lock (lockObj)
                                        {
                                            processingResult.ParentDirectory.AddImage(processingResult.Image);
                                        }
                                    }
                                    else if (!processingResult.Success)
                                    {
                                        result.Errors.Add(processingResult.ErrorMessage ?? $"Failed to process: {processingResult.RelativePath}");
                                    }
                                }

                                // Handle canvas separation for 64-bit format (only for first chunk to avoid duplicates)
                                if (separateCanvas && chunkIndex == 0)
                                {
                                    string canvasBasePath = Path.Combine(outputPath, "Data");
                                    ProcessCanvasSeparation(
                                        wzFile.WzDirectory,
                                        canvasBasePath,
                                        category,
                                        encryption,
                                        patchVersion,
                                        wzIv,
                                        result);
                                }

                                // Save the WZ file
                                Debug.WriteLine($"[PackCategory] {category}: Saving {wzFileName}");
                                wzFile.SaveToDisk(wzFilePath, saveAs64Bit, encryption);

                                if (File.Exists(wzFilePath))
                                {
                                    long fileSize = new FileInfo(wzFilePath).Length;
                                    totalOutputSize += fileSize;
                                    Debug.WriteLine($"[PackCategory] {category}: Created {wzFileName} ({fileSize / 1024 / 1024}MB)");
                                }
                            }
                        }
                    }, cancellationToken);

                    result.ImagesPacked = processedCount;
                    result.OutputFilePath = categoryOutputPath; // Directory containing all WZ files
                    result.OutputFileSize = totalOutputSize;
                }
                else
                {
                    // Standard format: single Category.wz file
                    string wzFileName = $"{category}.wz";
                    string wzFilePath = Path.Combine(outputPath, wzFileName);

                    await Task.Run(() =>
                    {
                        using (var wzFile = new WzFile(patchVersion, encryption))
                        {
                            wzFile.Name = wzFileName;

                            // Build directory structure and collect IMG files
                            var imgFiles = new List<ImgFileInfo>();
                            BuildDirectoryStructureAndCollectImgFiles(
                                categoryPath,
                                wzFile.WzDirectory,
                                categoryPath,
                                wzIv,
                                imgFiles);

                            int totalCount = imgFiles.Count;
                            int processedCount = 0;

                            Debug.WriteLine($"[PackCategory] {category}: Found {totalCount} IMG files");

                            // Process IMG files concurrently
                            var processingResults = new ConcurrentBag<ImgProcessingResult>();
                            var lockObj = new object();

                            var parallelOptions = new ParallelOptions
                            {
                                MaxDegreeOfParallelism = MAX_DEGREE_OF_PARALLELISM,
                                CancellationToken = cancellationToken
                            };

                            Parallel.ForEach(imgFiles, parallelOptions, imgFileInfo =>
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var processingResult = ProcessSingleImgFile(imgFileInfo, wzIv);
                                processingResults.Add(processingResult);

                                int currentCount = Interlocked.Increment(ref processedCount);
                                progressCallback?.Invoke(currentCount, totalCount, imgFileInfo.RelativePath);
                            });

                            // Add processed images to parent directories
                            foreach (var processingResult in processingResults)
                            {
                                if (processingResult.Success && processingResult.Image != null)
                                {
                                    lock (lockObj)
                                    {
                                        processingResult.ParentDirectory.AddImage(processingResult.Image);
                                    }
                                }
                                else if (!processingResult.Success)
                                {
                                    result.Errors.Add(processingResult.ErrorMessage ?? $"Failed to process: {processingResult.RelativePath}");
                                }
                            }

                            // Save the WZ file
                            Debug.WriteLine($"[PackCategory] {category}: Saving to {wzFilePath}");
                            wzFile.SaveToDisk(wzFilePath, saveAs64Bit, encryption);

                            result.ImagesPacked = processedCount;
                            result.OutputFilePath = wzFilePath;

                            if (File.Exists(wzFilePath))
                            {
                                result.OutputFileSize = new FileInfo(wzFilePath).Length;
                                Debug.WriteLine($"[PackCategory] {category}: Created {wzFilePath} ({result.OutputFileSize / 1024 / 1024}MB)");
                            }
                        }
                    }, cancellationToken);
                }

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
        /// <param name="separateCanvas">Whether to save canvas images in separate _Canvas folders (64-bit format only)</param>
        public async Task<PackingResult> PackCategoriesAsync(
            string versionPath,
            string outputPath,
            IEnumerable<string> categories,
            bool saveAs64Bit = false,
            CancellationToken cancellationToken = default,
            IProgress<PackingProgress> progress = null,
            short overridePatchVersion = 0,
            bool separateCanvas = false)
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
                        },
                        separateCanvas);

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
        /// Recursively loads IMG files and adds them to WzDirectory.
        /// This is the legacy sequential implementation. Use BuildDirectoryStructureAndCollectImgFiles
        /// with ProcessSingleImgFile for concurrent processing instead.
        /// </summary>
        [Obsolete("Use BuildDirectoryStructureAndCollectImgFiles with Parallel.ForEach for concurrent processing")]
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

        /// <summary>
        /// Collects all IMG files from a category directory without building directory structure.
        /// Used for size-based splitting where we need to know all files first.
        /// </summary>
        private List<ImgFileInfo> CollectImgFiles(string categoryPath)
        {
            var imgFiles = new List<ImgFileInfo>();

            foreach (var imgFilePath in Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories))
            {
                string relativePath = imgFilePath.Substring(categoryPath.Length).TrimStart(Path.DirectorySeparatorChar);
                var fileInfo = new FileInfo(imgFilePath);

                imgFiles.Add(new ImgFileInfo
                {
                    FilePath = imgFilePath,
                    RelativePath = relativePath,
                    FileSize = fileInfo.Length
                });
            }

            return imgFiles;
        }

        /// <summary>
        /// Groups IMG files into chunks based on size limit.
        /// Each chunk will become a separate WZ file.
        /// </summary>
        private List<List<ImgFileInfo>> GroupImgFilesBySize(List<ImgFileInfo> imgFiles, long maxSize)
        {
            var chunks = new List<List<ImgFileInfo>>();
            var currentChunk = new List<ImgFileInfo>();
            long currentSize = 0;

            // Sort by relative path for consistent ordering
            var sortedFiles = imgFiles.OrderBy(f => f.RelativePath).ToList();

            foreach (var file in sortedFiles)
            {
                // If adding this file would exceed the limit, start a new chunk
                // (unless current chunk is empty - handles files larger than maxSize)
                if (currentSize + file.FileSize > maxSize && currentChunk.Count > 0)
                {
                    chunks.Add(currentChunk);
                    currentChunk = new List<ImgFileInfo>();
                    currentSize = 0;
                }

                currentChunk.Add(file);
                currentSize += file.FileSize;
            }

            // Add the last chunk if it has any files
            if (currentChunk.Count > 0)
            {
                chunks.Add(currentChunk);
            }

            return chunks;
        }

        /// <summary>
        /// Builds the WzDirectory structure for a specific set of IMG files.
        /// Creates necessary subdirectories based on file relative paths.
        /// </summary>
        private void BuildDirectoryStructureForFiles(
            WzDirectory rootDir,
            List<ImgFileInfo> imgFiles,
            byte[] wzIv)
        {
            foreach (var imgFile in imgFiles)
            {
                // Get the directory portion of the relative path
                string dirPath = Path.GetDirectoryName(imgFile.RelativePath);

                // Navigate/create the directory structure
                WzDirectory currentDir = rootDir;
                if (!string.IsNullOrEmpty(dirPath))
                {
                    string[] parts = dirPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    foreach (var part in parts)
                    {
                        if (string.IsNullOrEmpty(part)) continue;

                        // Look for existing directory
                        var existingDir = currentDir.WzDirectories.FirstOrDefault(d => d.Name == part);
                        if (existingDir != null)
                        {
                            currentDir = existingDir;
                        }
                        else
                        {
                            // Create new directory
                            var newDir = new WzDirectory(part) { WzIv = wzIv };
                            currentDir.AddDirectory(newDir);
                            currentDir = newDir;
                        }
                    }
                }

                // Store reference to parent directory in the ImgFileInfo
                imgFile.ParentDirectory = currentDir;
            }
        }

        /// <summary>
        /// Builds the WzDirectory structure and collects all IMG files to be processed.
        /// This phase runs sequentially to ensure directory structure is correct.
        /// </summary>
        private void BuildDirectoryStructureAndCollectImgFiles(
            string currentPath,
            WzDirectory parentDir,
            string basePath,
            byte[] wzIv,
            List<ImgFileInfo> imgFiles)
        {
            // Process subdirectories first - create WzDirectory for each
            foreach (var subDirPath in Directory.EnumerateDirectories(currentPath))
            {
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
                BuildDirectoryStructureAndCollectImgFiles(
                    subDirPath,
                    subDir,
                    basePath,
                    wzIv,
                    imgFiles);
            }

            // Collect IMG files in current directory
            foreach (var imgFilePath in Directory.EnumerateFiles(currentPath, "*.img"))
            {
                string relativePath = imgFilePath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
                var fileInfo = new FileInfo(imgFilePath);

                imgFiles.Add(new ImgFileInfo
                {
                    FilePath = imgFilePath,
                    RelativePath = relativePath,
                    ParentDirectory = parentDir,
                    FileSize = fileInfo.Length
                });
            }
        }

        /// <summary>
        /// Processes a single IMG file: reads, parses, and clones it.
        /// This method is thread-safe and can be called concurrently.
        /// </summary>
        private ImgProcessingResult ProcessSingleImgFile(ImgFileInfo imgFileInfo, byte[] wzIv)
        {
            var result = new ImgProcessingResult
            {
                RelativePath = imgFileInfo.RelativePath,
                ParentDirectory = imgFileInfo.ParentDirectory
            };

            try
            {
                string imgFileName = Path.GetFileName(imgFileInfo.FilePath);

                // Read the raw IMG file bytes
                byte[] imgBytes = File.ReadAllBytes(imgFileInfo.FilePath);

                // Create WzImage from the raw bytes
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

                        // Deep clone the image to detach from the reader
                        WzImage clonedImage = CloneWzImage(wzImage, imgFileName);
                        if (clonedImage != null)
                        {
                            result.Success = true;
                            result.Image = clonedImage;
                        }
                        else
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Failed to clone: {imgFileInfo.RelativePath}";
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Failed to parse: {imgFileInfo.RelativePath}";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error loading {imgFileInfo.RelativePath}: {ex.Message}";
                Debug.WriteLine($"Error loading IMG file {imgFileInfo.FilePath}: {ex}");
            }

            return result;
        }
        #endregion

        #region Canvas Separation Methods
        /// <summary>
        /// Information about a canvas property that needs to be separated
        /// </summary>
        private class CanvasInfo
        {
            public WzCanvasProperty Canvas { get; set; }
            public WzImage SourceImage { get; set; }
            public string PropertyPath { get; set; }
            public long EstimatedSize { get; set; }
            public string OutlinkPath { get; set; }
        }

        /// <summary>
        /// Processes canvas separation for a category, moving large canvases to _Canvas folder
        /// </summary>
        private void ProcessCanvasSeparation(
            WzDirectory directory,
            string outputPath,
            string category,
            WzMapleVersion encryption,
            short patchVersion,
            byte[] wzIv,
            CategoryPackingResult result)
        {
            try
            {
                // Find all canvas properties that exceed the size threshold
                var canvasesToSeparate = new List<CanvasInfo>();
                FindLargeCanvasProperties(directory, category, "", canvasesToSeparate);

                if (canvasesToSeparate.Count == 0)
                {
                    Debug.WriteLine($"[Canvas] No canvases to separate for category {category}");
                    return;
                }

                Debug.WriteLine($"[Canvas] Found {canvasesToSeparate.Count} canvases to separate for category {category}");

                // Create _Canvas directory
                string canvasDir = Path.Combine(outputPath, category, CANVAS_DIRECTORY_NAME);
                if (!Directory.Exists(canvasDir))
                {
                    Directory.CreateDirectory(canvasDir);
                }

                // Group canvases into Canvas files based on size limits
                var canvasGroups = GroupCanvasesIntoFiles(canvasesToSeparate);

                // Create each Canvas WZ file
                int canvasIndex = 0;
                foreach (var group in canvasGroups)
                {
                    CreateCanvasWzFile(
                        canvasDir,
                        category,
                        canvasIndex,
                        group,
                        encryption,
                        patchVersion,
                        wzIv);
                    canvasIndex++;
                }

                // Create/update the Canvas.ini index file
                UpdateCanvasIndexFile(canvasDir, canvasIndex - 1);

                // Replace original canvases with _outlink references
                foreach (var canvasInfo in canvasesToSeparate)
                {
                    ReplaceCanvasWithOutlink(canvasInfo);
                }

                Debug.WriteLine($"[Canvas] Created {canvasIndex} Canvas WZ files for category {category}");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error during canvas separation: {ex.Message}");
                Debug.WriteLine($"[Canvas] Error in ProcessCanvasSeparation: {ex}");
            }
        }

        /// <summary>
        /// Recursively finds all canvas properties that exceed the size threshold
        /// </summary>
        private void FindLargeCanvasProperties(
            WzObject node,
            string category,
            string currentPath,
            List<CanvasInfo> canvases)
        {
            if (node is WzDirectory dir)
            {
                foreach (var subDir in dir.WzDirectories)
                {
                    string path = string.IsNullOrEmpty(currentPath) ? subDir.Name : $"{currentPath}/{subDir.Name}";
                    FindLargeCanvasProperties(subDir, category, path, canvases);
                }

                foreach (var img in dir.WzImages)
                {
                    string path = string.IsNullOrEmpty(currentPath) ? img.Name : $"{currentPath}/{img.Name}";
                    FindLargeCanvasPropertiesInImage(img, category, path, canvases);
                }
            }
        }

        /// <summary>
        /// Finds large canvas properties within a WzImage
        /// </summary>
        private void FindLargeCanvasPropertiesInImage(
            WzImage image,
            string category,
            string imagePath,
            List<CanvasInfo> canvases)
        {
            foreach (var prop in image.WzProperties)
            {
                FindLargeCanvasPropertiesRecursive(prop, image, category, imagePath, prop.Name, canvases);
            }
        }

        /// <summary>
        /// Recursively searches for large canvas properties
        /// </summary>
        private void FindLargeCanvasPropertiesRecursive(
            WzImageProperty prop,
            WzImage sourceImage,
            string category,
            string imagePath,
            string propertyPath,
            List<CanvasInfo> canvases)
        {
            if (prop is WzCanvasProperty canvas)
            {
                long size = EstimateCanvasSize(canvas);
                if (size >= CANVAS_SIZE_THRESHOLD)
                {
                    // Format: Category/_Canvas/imageName.img/propertyPath
                    string outlinkPath = $"{category}/{CANVAS_DIRECTORY_NAME}/{imagePath}/{propertyPath}";

                    canvases.Add(new CanvasInfo
                    {
                        Canvas = canvas,
                        SourceImage = sourceImage,
                        PropertyPath = propertyPath,
                        EstimatedSize = size,
                        OutlinkPath = outlinkPath
                    });
                }
            }

            // Check sub-properties
            if (prop is IPropertyContainer container)
            {
                foreach (var subProp in container.WzProperties)
                {
                    string newPath = $"{propertyPath}/{subProp.Name}";
                    FindLargeCanvasPropertiesRecursive(subProp, sourceImage, category, imagePath, newPath, canvases);
                }
            }
            else if (prop is WzConvexProperty convex)
            {
                foreach (var subProp in convex.WzProperties)
                {
                    string newPath = $"{propertyPath}/{subProp.Name}";
                    FindLargeCanvasPropertiesRecursive(subProp, sourceImage, category, imagePath, newPath, canvases);
                }
            }
        }

        /// <summary>
        /// Estimates the size of a canvas property in bytes
        /// </summary>
        private long EstimateCanvasSize(WzCanvasProperty canvas)
        {
            if (canvas.PngProperty == null)
                return 0;

            // Estimate based on pixel count and format
            // Most canvas formats use 4 bytes per pixel (ARGB)
            int width = canvas.PngProperty.Width;
            int height = canvas.PngProperty.Height;
            long pixelSize = width * height * 4;

            // Add overhead for sub-properties
            if (canvas.WzProperties != null)
            {
                pixelSize += canvas.WzProperties.Count * 50; // Rough estimate for property overhead
            }

            return pixelSize;
        }

        /// <summary>
        /// Groups canvases into files based on size limits
        /// </summary>
        private List<List<CanvasInfo>> GroupCanvasesIntoFiles(List<CanvasInfo> canvases)
        {
            var result = new List<List<CanvasInfo>>();
            var currentGroup = new List<CanvasInfo>();
            long currentSize = 0;

            // Sort by size descending for better packing
            var sorted = canvases.OrderByDescending(c => c.EstimatedSize).ToList();

            foreach (var canvas in sorted)
            {
                // Check if adding this canvas exceeds limits
                if ((currentSize + canvas.EstimatedSize > MAX_CANVAS_FILE_SIZE ||
                    currentGroup.Count >= MAX_IMAGES_PER_CANVAS) && currentGroup.Count > 0)
                {
                    result.Add(currentGroup);
                    currentGroup = new List<CanvasInfo>();
                    currentSize = 0;
                }

                currentGroup.Add(canvas);
                currentSize += canvas.EstimatedSize;
            }

            if (currentGroup.Count > 0)
            {
                result.Add(currentGroup);
            }

            return result;
        }

        /// <summary>
        /// Creates a Canvas WZ file containing the specified canvas properties
        /// </summary>
        private void CreateCanvasWzFile(
            string canvasDir,
            string category,
            int canvasIndex,
            List<CanvasInfo> canvases,
            WzMapleVersion encryption,
            short patchVersion,
            byte[] wzIv)
        {
            string canvasFileName = $"{CANVAS_DIRECTORY_NAME}_{canvasIndex:D3}.wz";
            string canvasFilePath = Path.Combine(canvasDir, canvasFileName);

            using (var canvasWzFile = new WzFile(patchVersion, encryption))
            {
                canvasWzFile.Name = canvasFileName;

                // Group canvases by their source image to maintain structure
                var byImage = canvases.GroupBy(c => c.SourceImage.Name);

                foreach (var imageGroup in byImage)
                {
                    // Create a WzImage in the canvas file with same name as source
                    var canvasImage = new WzImage(imageGroup.Key)
                    {
                        Changed = true
                    };

                    foreach (var canvasInfo in imageGroup)
                    {
                        // Clone the canvas property and add to the canvas image
                        var clonedCanvas = (WzCanvasProperty)canvasInfo.Canvas.DeepClone();

                        // Navigate/create the property path
                        string[] pathParts = canvasInfo.PropertyPath.Split('/');
                        WzObject current = canvasImage;

                        // Create intermediate sub-properties if needed
                        for (int i = 0; i < pathParts.Length - 1; i++)
                        {
                            string part = pathParts[i];
                            WzImageProperty existing = null;

                            if (current is WzImage img)
                            {
                                existing = img[part];
                                if (existing == null)
                                {
                                    var subProp = new WzSubProperty(part);
                                    img.WzProperties.Add(subProp);
                                    current = subProp;
                                }
                                else
                                {
                                    current = existing;
                                }
                            }
                            else if (current is WzSubProperty subProp)
                            {
                                existing = subProp[part];
                                if (existing == null)
                                {
                                    var newSubProp = new WzSubProperty(part);
                                    subProp.AddProperty(newSubProp);
                                    current = newSubProp;
                                }
                                else
                                {
                                    current = existing;
                                }
                            }
                        }

                        // Add the canvas at the final location
                        clonedCanvas.Name = pathParts[pathParts.Length - 1];
                        if (current is WzImage targetImg)
                        {
                            targetImg.WzProperties.Add(clonedCanvas);
                        }
                        else if (current is WzSubProperty targetSub)
                        {
                            targetSub.AddProperty(clonedCanvas);
                        }
                    }

                    canvasWzFile.WzDirectory.AddImage(canvasImage);
                }

                // Save the canvas WZ file as 64-bit format
                canvasWzFile.SaveToDisk(canvasFilePath, true, encryption);
            }

            Debug.WriteLine($"[Canvas] Created {canvasFilePath} with {canvases.Count} canvases");
        }

        /// <summary>
        /// Replaces a canvas property with an _outlink reference
        /// </summary>
        private void ReplaceCanvasWithOutlink(CanvasInfo canvasInfo)
        {
            var originalCanvas = canvasInfo.Canvas;
            var parent = originalCanvas.Parent;

            // Create a placeholder canvas with _outlink
            var placeholder = new WzCanvasProperty(originalCanvas.Name);

            // Add _outlink string property pointing to _Canvas location
            placeholder.AddProperty(new WzStringProperty(WzCanvasProperty.OutlinkPropertyName, canvasInfo.OutlinkPath));

            // Create a minimal 1x1 placeholder PNG
            var placeholderBitmap = new Bitmap(1, 1);
            placeholderBitmap.SetPixel(0, 0, Color.Transparent);
            placeholder.PngProperty = new WzPngProperty();
            placeholder.PngProperty.PNG = placeholderBitmap;

            // Copy any non-canvas sub-properties (like origin, delay, etc.)
            foreach (var prop in originalCanvas.WzProperties)
            {
                if (prop.Name != WzCanvasProperty.OutlinkPropertyName &&
                    prop.Name != WzCanvasProperty.InlinkPropertyName)
                {
                    placeholder.AddProperty(prop.DeepClone());
                }
            }

            // Replace in parent
            if (parent is WzImage img)
            {
                img.WzProperties.Remove(originalCanvas);
                img.WzProperties.Add(placeholder);
            }
            else if (parent is WzSubProperty subProp)
            {
                subProp.RemoveProperty(originalCanvas);
                subProp.AddProperty(placeholder);
            }
            else if (parent is WzConvexProperty convex)
            {
                convex.WzProperties.Remove(originalCanvas);
                convex.AddProperty(placeholder);
            }
        }

        /// <summary>
        /// Creates or updates the Canvas.ini index file
        /// </summary>
        private void UpdateCanvasIndexFile(string canvasDir, int maxIndex)
        {
            string iniPath = Path.Combine(canvasDir, "Canvas.ini");

            var content = new StringBuilder();
            content.AppendLine($"LastWzIndex|{maxIndex}");

            File.WriteAllText(iniPath, content.ToString());
            Debug.WriteLine($"[Canvas] Created index file: {iniPath}");
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

    #region List.wz JSON Format
    /// <summary>
    /// JSON format used for extracted List.wz files
    /// </summary>
    internal class ListWzJsonFormat
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public List<string> Entries { get; set; }
    }
    #endregion
}
