using System;
using System.IO;

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
        /// Gets the application data root directory
        /// </summary>
        public static string AppDataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ApplicationName);

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
