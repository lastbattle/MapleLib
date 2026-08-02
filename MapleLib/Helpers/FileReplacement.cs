using System;
using System.IO;

namespace MapleLib.Helpers
{
    /// <summary>
    /// Provides safe replacement of an existing file while retaining the old contents.
    /// </summary>
    public static class FileReplacement
    {
        /// <summary>
        /// Builds a timestamped backup path using the standard backup naming convention.
        /// </summary>
        public static string GetBackupFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A file path is required.", nameof(filePath));

            string backupBasePath = $"{filePath}_BAK_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}";
            string extension = Path.GetExtension(filePath);
            string backupPath = backupBasePath + extension;
            int suffix = 1;

            while (File.Exists(backupPath))
            {
                backupPath = $"{backupBasePath}_{suffix++}{extension}";
            }

            return backupPath;
        }

        /// <summary>
        /// Replaces a destination from a completed temporary file. The existing destination
        /// is copied to the supplied backup path before the atomic replacement.
        /// </summary>
        public static void ReplaceWithBackup(string temporaryFilePath, string filePath, string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(temporaryFilePath))
                throw new ArgumentException("A temporary file path is required.", nameof(temporaryFilePath));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A destination file path is required.", nameof(filePath));
            if (!File.Exists(temporaryFilePath))
                throw new FileNotFoundException("The temporary replacement file was not found.", temporaryFilePath);

            string destinationDirectory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            if (!File.Exists(filePath))
            {
                File.Move(temporaryFilePath, filePath);
                return;
            }

            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new ArgumentException("A backup file path is required when replacing an existing file.", nameof(backupFilePath));

            string backupDirectory = Path.GetDirectoryName(backupFilePath);
            if (!string.IsNullOrEmpty(backupDirectory))
                Directory.CreateDirectory(backupDirectory);

            // Copy before touching the destination. If the copy or replacement fails, the
            // original remains available at its original path and in the external backup.
            File.Copy(filePath, backupFilePath);
            File.Replace(temporaryFilePath, filePath, null);
        }
    }
}
