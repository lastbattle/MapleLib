using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapleLib.Img
{
    /// <summary>
    /// Configuration for a WZ source to be converted
    /// </summary>
    public class WzSourceConfig
    {
        /// <summary>
        /// Path to the MapleStory folder containing WZ files
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// Name for the extracted version (e.g., "v83_gms")
        /// </summary>
        public string VersionName { get; set; }

        /// <summary>
        /// Display name for the version
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// WZ encryption type
        /// </summary>
        public WzMapleVersion Encryption { get; set; } = WzMapleVersion.GMS;

        /// <summary>
        /// Whether to extract all categories or just required ones
        /// </summary>
        public bool ExtractAllCategories { get; set; } = true;

        /// <summary>
        /// Specific categories to extract (if ExtractAllCategories is false)
        /// </summary>
        public string[] CategoriesToExtract { get; set; }
    }

    /// <summary>
    /// Progress information for batch conversion
    /// </summary>
    public class BatchProgress
    {
        /// <summary>
        /// Current version being processed (1-based)
        /// </summary>
        public int CurrentVersion { get; set; }

        /// <summary>
        /// Total number of versions to process
        /// </summary>
        public int TotalVersions { get; set; }

        /// <summary>
        /// Name of the current version being processed
        /// </summary>
        public string CurrentVersionName { get; set; }

        /// <summary>
        /// Current category being processed within the version
        /// </summary>
        public string CurrentCategory { get; set; }

        /// <summary>
        /// Overall progress percentage (0-100)
        /// </summary>
        public double OverallProgress { get; set; }

        /// <summary>
        /// Current version progress percentage (0-100)
        /// </summary>
        public double VersionProgress { get; set; }

        /// <summary>
        /// Status message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Whether the operation is complete
        /// </summary>
        public bool IsComplete { get; set; }
    }

    /// <summary>
    /// Result of a batch conversion operation
    /// </summary>
    public class BatchConversionResult
    {
        /// <summary>
        /// Whether all conversions succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of versions successfully converted
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of versions that failed
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Total time elapsed
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Results for each version
        /// </summary>
        public List<VersionConversionResult> VersionResults { get; set; } = new List<VersionConversionResult>();

        /// <summary>
        /// Errors that occurred during conversion
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result for a single version conversion
    /// </summary>
    public class VersionConversionResult
    {
        /// <summary>
        /// Version name
        /// </summary>
        public string VersionName { get; set; }

        /// <summary>
        /// Whether conversion succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Output path for the converted version
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Number of categories extracted
        /// </summary>
        public int CategoriesExtracted { get; set; }

        /// <summary>
        /// Number of images extracted
        /// </summary>
        public int ImagesExtracted { get; set; }

        /// <summary>
        /// Time elapsed for this version
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service for batch converting multiple WZ sources to IMG filesystem format.
    /// Wraps WzExtractionService to support multiple version conversions with progress tracking.
    /// </summary>
    public class BatchConverter
    {
        private readonly WzExtractionService _extractionService;

        /// <summary>
        /// Event fired when batch progress changes
        /// </summary>
        public event EventHandler<BatchProgress> ProgressChanged;

        /// <summary>
        /// Event fired when a version conversion starts
        /// </summary>
        public event EventHandler<string> VersionStarted;

        /// <summary>
        /// Event fired when a version conversion completes
        /// </summary>
        public event EventHandler<VersionConversionResult> VersionCompleted;

        public BatchConverter()
        {
            _extractionService = new WzExtractionService();
        }

        /// <summary>
        /// Converts multiple WZ sources to IMG filesystem format
        /// </summary>
        /// <param name="sources">Collection of WZ sources to convert</param>
        /// <param name="outputRoot">Root directory for output (versions will be created as subdirectories)</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Batch conversion result</returns>
        public async Task<BatchConversionResult> ConvertMultipleVersionsAsync(
            IEnumerable<WzSourceConfig> sources,
            string outputRoot,
            IProgress<BatchProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var sourceList = sources.ToList();
            var startTime = DateTime.Now;
            var result = new BatchConversionResult();

            // Ensure output directory exists
            Directory.CreateDirectory(outputRoot);

            int currentVersion = 0;
            foreach (var source in sourceList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                currentVersion++;
                var versionStartTime = DateTime.Now;

                var batchProgress = new BatchProgress
                {
                    CurrentVersion = currentVersion,
                    TotalVersions = sourceList.Count,
                    CurrentVersionName = source.VersionName,
                    Message = $"Starting conversion of {source.VersionName}..."
                };

                progress?.Report(batchProgress);
                VersionStarted?.Invoke(this, source.VersionName);

                var versionResult = new VersionConversionResult
                {
                    VersionName = source.VersionName,
                    OutputPath = Path.Combine(outputRoot, source.VersionName)
                };

                try
                {
                    // Create version output directory
                    Directory.CreateDirectory(versionResult.OutputPath);

                    // Create progress handler that updates batch progress
                    var extractionProgress = new Progress<ExtractionProgress>(ep =>
                    {
                        batchProgress.CurrentCategory = ep.CurrentPhase;
                        batchProgress.VersionProgress = ep.ProgressPercentage;
                        batchProgress.OverallProgress = ((currentVersion - 1) * 100.0 + ep.ProgressPercentage) / sourceList.Count;
                        batchProgress.Message = $"{ep.CurrentPhase}: {ep.CurrentFile}";
                        progress?.Report(batchProgress);
                        ProgressChanged?.Invoke(this, batchProgress);
                    });

                    // Run extraction
                    var extractionResult = await _extractionService.ExtractAsync(
                        source.SourcePath,
                        versionResult.OutputPath,
                        source.VersionName,
                        source.DisplayName ?? source.VersionName,
                        source.Encryption,
                        cancellationToken,
                        extractionProgress);

                    versionResult.Success = extractionResult.Success;
                    versionResult.CategoriesExtracted = extractionResult.CategoriesExtracted.Count;
                    versionResult.ImagesExtracted = extractionResult.TotalImagesExtracted;

                    if (!extractionResult.Success)
                    {
                        versionResult.ErrorMessage = extractionResult.ErrorMessage ?? "Unknown error";
                        result.Errors.Add($"{source.VersionName}: {versionResult.ErrorMessage}");
                        result.FailureCount++;
                    }
                    else
                    {
                        result.SuccessCount++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    versionResult.Success = false;
                    versionResult.ErrorMessage = ex.Message;
                    result.Errors.Add($"{source.VersionName}: {ex.Message}");
                    result.FailureCount++;
                }

                versionResult.ElapsedTime = DateTime.Now - versionStartTime;
                result.VersionResults.Add(versionResult);

                VersionCompleted?.Invoke(this, versionResult);
            }

            result.ElapsedTime = DateTime.Now - startTime;
            result.Success = result.FailureCount == 0;

            // Report completion
            progress?.Report(new BatchProgress
            {
                CurrentVersion = sourceList.Count,
                TotalVersions = sourceList.Count,
                OverallProgress = 100,
                IsComplete = true,
                Message = $"Batch conversion complete. {result.SuccessCount} succeeded, {result.FailureCount} failed."
            });

            return result;
        }

        /// <summary>
        /// Scans a directory for potential MapleStory installations
        /// </summary>
        /// <param name="searchPath">Path to search</param>
        /// <returns>List of detected MapleStory folders with their likely versions</returns>
        public static List<DetectedMapleInstallation> ScanForMapleInstallations(string searchPath)
        {
            var results = new List<DetectedMapleInstallation>();

            if (!Directory.Exists(searchPath))
                return results;

            // Check if this directory itself is a MapleStory folder
            if (IsMapleStoryFolder(searchPath))
            {
                results.Add(AnalyzeInstallation(searchPath));
            }

            // Check subdirectories
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(searchPath))
                {
                    if (IsMapleStoryFolder(subDir))
                    {
                        results.Add(AnalyzeInstallation(subDir));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }

            return results;
        }

        private static bool IsMapleStoryFolder(string path)
        {
            // Check for key WZ files that indicate a MapleStory installation
            return File.Exists(Path.Combine(path, "String.wz")) ||
                   File.Exists(Path.Combine(path, "Map.wz")) ||
                   File.Exists(Path.Combine(path, "Data.wz")) || // Some versions use Data.wz
                   Directory.Exists(Path.Combine(path, "Data")); // 64-bit versions use Data folder
        }

        private static DetectedMapleInstallation AnalyzeInstallation(string path)
        {
            var installation = new DetectedMapleInstallation
            {
                Path = path,
                FolderName = Path.GetFileName(path)
            };

            // Count WZ files
            installation.WzFileCount = Directory.EnumerateFiles(path, "*.wz").Count();

            // Check for 64-bit version (Data folder structure)
            var dataFolder = Path.Combine(path, "Data");
            installation.Is64Bit = Directory.Exists(dataFolder) &&
                                   Directory.EnumerateFiles(dataFolder, "*.wz", SearchOption.AllDirectories).Any();

            // Try to detect version from folder name or files
            installation.SuggestedVersionName = SuggestVersionName(path, installation.FolderName);

            // Try to detect encryption
            installation.SuggestedEncryption = DetectEncryption(path);

            return installation;
        }

        private static string SuggestVersionName(string path, string folderName)
        {
            // Try to extract version from folder name
            var lower = folderName.ToLowerInvariant();

            // Common patterns: "v83", "MapleStoryv83", "GMS v83", etc.
            if (lower.Contains("v") && char.IsDigit(lower[lower.IndexOf('v') + 1]))
            {
                int vIndex = lower.IndexOf('v');
                string version = "v";
                for (int i = vIndex + 1; i < lower.Length && (char.IsDigit(lower[i]) || lower[i] == '.'); i++)
                {
                    version += lower[i];
                }
                return version;
            }

            // Default to folder name cleaned up
            return folderName.Replace(" ", "_").Replace(".", "_");
        }

        private static WzMapleVersion DetectEncryption(string path)
        {
            // Try to detect encryption based on file contents
            // Default to GMS as it's most common
            var stringWz = Path.Combine(path, "String.wz");
            if (File.Exists(stringWz))
            {
                // Could implement actual detection here by trying to parse with different encryptions
                // For now, return GMS as default
                return WzMapleVersion.GMS;
            }

            return WzMapleVersion.GMS;
        }
    }

    /// <summary>
    /// Information about a detected MapleStory installation
    /// </summary>
    public class DetectedMapleInstallation
    {
        /// <summary>
        /// Full path to the installation
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Folder name
        /// </summary>
        public string FolderName { get; set; }

        /// <summary>
        /// Number of WZ files found
        /// </summary>
        public int WzFileCount { get; set; }

        /// <summary>
        /// Whether this appears to be a 64-bit version
        /// </summary>
        public bool Is64Bit { get; set; }

        /// <summary>
        /// Suggested version name based on folder analysis
        /// </summary>
        public string SuggestedVersionName { get; set; }

        /// <summary>
        /// Suggested encryption based on analysis
        /// </summary>
        public WzMapleVersion SuggestedEncryption { get; set; }
    }
}
