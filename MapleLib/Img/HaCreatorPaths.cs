using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapleLib.Img
{
    /// <summary>
    /// Constants for HaCreator file and directory paths
    /// </summary>
    public static class HaCreatorPaths
    {
        /// <summary>
        /// The application name used for folder creation
        /// </summary>
        public const string ApplicationName = "HaCreator";

        /// <summary>
        /// The config file name
        /// </summary>
        public const string ConfigFileName = "config.json";

        /// <summary>
        /// The data folder name
        /// </summary>
        public const string DataFolderName = "Data";

        /// <summary>
        /// The versions folder name
        /// </summary>
        public const string VersionsFolderName = "versions";

        /// <summary>
        /// The custom content folder name
        /// </summary>
        public const string CustomFolderName = "custom";

        /// <summary>
        /// The folder name used for files backed up during editing
        /// </summary>
        public const string BackupsFolderName = "Backups";

        /// <summary>
        /// Determines whether a directory name is the reserved backups directory.
        /// </summary>
        public static bool IsBackupsDirectoryName(string directoryName) =>
            string.Equals(directoryName, BackupsFolderName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Determines whether a path points to the reserved backups directory.
        /// </summary>
        public static bool IsBackupsDirectory(string directoryPath) =>
            !string.IsNullOrEmpty(directoryPath) &&
            IsBackupsDirectoryName(Path.GetFileName(
                directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

        /// <summary>
        /// Determines whether a path contains the reserved backups directory as a path segment.
        /// </summary>
        public static bool ContainsBackupsDirectory(string path) =>
            !string.IsNullOrEmpty(path) &&
            path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries)
                .Any(IsBackupsDirectoryName);

        /// <summary>
        /// Enumerates directories without descending into directories named <see cref="BackupsFolderName"/>.
        /// </summary>
        public static IEnumerable<string> EnumerateDirectoriesExcludingBackups(
            string rootPath,
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (ContainsBackupsDirectory(rootPath))
                yield break;

            foreach (string directoryPath in Directory.EnumerateDirectories(rootPath))
            {
                if (IsBackupsDirectory(directoryPath))
                    continue;

                yield return directoryPath;

                if (searchOption == SearchOption.AllDirectories)
                {
                    foreach (string nestedDirectory in EnumerateDirectoriesExcludingBackups(
                        directoryPath,
                        SearchOption.AllDirectories))
                    {
                        yield return nestedDirectory;
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates files without descending into directories named <see cref="BackupsFolderName"/>.
        /// </summary>
        public static IEnumerable<string> EnumerateFilesExcludingBackups(
            string rootPath,
            string searchPattern,
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (ContainsBackupsDirectory(rootPath))
                yield break;

            foreach (string filePath in Directory.EnumerateFiles(rootPath, searchPattern))
                yield return filePath;

            if (searchOption == SearchOption.AllDirectories)
            {
                foreach (string directoryPath in EnumerateDirectoriesExcludingBackups(
                    rootPath,
                    SearchOption.AllDirectories))
                {
                    foreach (string filePath in Directory.EnumerateFiles(directoryPath, searchPattern))
                        yield return filePath;
                }
            }
        }

        /// <summary>
        /// Gets the application data root directory
        /// </summary>
        public static string AppDataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ApplicationName);

        /// <summary>
        /// Gets the backup directory next to a version directory.
        /// </summary>
        /// <param name="versionPath">The version directory whose backups are being stored</param>
        /// <returns>A user-visible <see cref="BackupsFolderName"/> sibling directory</returns>
        public static string GetBackupsPath(string versionPath)
        {
            if (string.IsNullOrWhiteSpace(versionPath))
                throw new ArgumentException("A version path is required.", nameof(versionPath));

            string fullVersionPath = Path.GetFullPath(versionPath);
            string parentPath = Directory.GetParent(fullVersionPath)?.FullName ?? fullVersionPath;
            return Path.Combine(parentPath, BackupsFolderName);
        }

        /// <summary>
        /// Gets the default config file path
        /// </summary>
        public static string DefaultConfigPath => Path.Combine(AppDataRoot, ConfigFileName);

        /// <summary>
        /// Gets the default data directory path
        /// </summary>
        public static string DefaultDataPath => Path.Combine(AppDataRoot, DataFolderName);

        /// <summary>
        /// Gets the versions directory path for a given data root
        /// </summary>
        public static string GetVersionsPath(string dataRoot) => Path.Combine(dataRoot, VersionsFolderName);

        /// <summary>
        /// Gets the custom content directory path for a given data root
        /// </summary>
        public static string GetCustomPath(string dataRoot) => Path.Combine(dataRoot, CustomFolderName);
    }
}
