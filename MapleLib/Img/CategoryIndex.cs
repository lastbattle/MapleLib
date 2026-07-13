using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapleLib.Img
{
    /// <summary>
    /// Index file for fast category lookups without directory scanning.
    /// Stored as index.json in each category directory.
    /// </summary>
    public class CategoryIndex
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("totalImageCount")]
        public int TotalImageCount { get; set; }

        [JsonPropertyName("totalSizeBytes")]
        public long TotalSizeBytes { get; set; }

        [JsonPropertyName("images")]
        public List<ImageIndexEntry> Images { get; set; } = new List<ImageIndexEntry>();

        [JsonPropertyName("subdirectories")]
        public List<SubdirectoryEntry> Subdirectories { get; set; } = new List<SubdirectoryEntry>();

        /// <summary>
        /// Gets all image paths recursively
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> AllImagePaths
        {
            get
            {
                foreach (var img in Images)
                    yield return img.RelativePath;

                foreach (var subdir in Subdirectories)
                {
                    foreach (var img in subdir.Images)
                        yield return Path.Combine(subdir.Name, img.RelativePath);
                }
            }
        }

        /// <summary>
        /// Builds an index from a category directory
        /// </summary>
        public static CategoryIndex BuildFromDirectory(string categoryPath, string categoryName)
        {
            var index = new CategoryIndex
            {
                Category = categoryName,
                GeneratedAt = DateTime.UtcNow
            };

            if (!Directory.Exists(categoryPath))
                return index;

            // Index root level images
            foreach (var file in Directory.EnumerateFiles(categoryPath, "*.img"))
            {
                var fileInfo = new FileInfo(file);
                index.Images.Add(new ImageIndexEntry
                {
                    Name = fileInfo.Name,
                    RelativePath = fileInfo.Name,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc
                });
                index.TotalSizeBytes += fileInfo.Length;
                index.TotalImageCount++;
            }

            // Index subdirectories
            foreach (var dir in Directory.EnumerateDirectories(categoryPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                var subdirEntry = new SubdirectoryEntry
                {
                    Name = dirInfo.Name
                };

                IndexDirectoryRecursive(dir, dirInfo.Name, subdirEntry, index);
                index.Subdirectories.Add(subdirEntry);
            }

            return index;
        }

        private static void IndexDirectoryRecursive(string dirPath, string relativePath, SubdirectoryEntry entry, CategoryIndex index)
        {
            foreach (var file in Directory.EnumerateFiles(dirPath, "*.img"))
            {
                var fileInfo = new FileInfo(file);
                var imgRelPath = Path.GetFileName(file);
                entry.Images.Add(new ImageIndexEntry
                {
                    Name = fileInfo.Name,
                    RelativePath = imgRelPath,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc
                });
                index.TotalSizeBytes += fileInfo.Length;
                index.TotalImageCount++;
            }

            foreach (var subdir in Directory.EnumerateDirectories(dirPath))
            {
                var subdirInfo = new DirectoryInfo(subdir);
                var nestedEntry = new SubdirectoryEntry
                {
                    Name = subdirInfo.Name
                };

                IndexDirectoryRecursive(subdir, Path.Combine(relativePath, subdirInfo.Name), nestedEntry, index);
                entry.Subdirectories.Add(nestedEntry);
            }
        }

        /// <summary>
        /// Saves the index to a file
        /// </summary>
        public void Save(string indexPath)
        {
            string json = JsonSerializer.Serialize(this, MapleJsonContext.Default.CategoryIndex);
            File.WriteAllText(indexPath, json);
        }

        /// <summary>
        /// Loads an index from a file
        /// </summary>
        public static CategoryIndex Load(string indexPath)
        {
            if (!File.Exists(indexPath))
                return null;

            try
            {
                string json = File.ReadAllText(indexPath);
                return JsonSerializer.Deserialize(json, MapleJsonContext.Default.CategoryIndex);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if the index is stale (directory modified after index generation)
        /// </summary>
        public bool IsStale(string categoryPath)
        {
            if (!Directory.Exists(categoryPath))
                return true;

            var dirInfo = new DirectoryInfo(categoryPath);
            return dirInfo.LastWriteTimeUtc > GeneratedAt;
        }
    }

    /// <summary>
    /// Entry for an indexed image file
    /// </summary>
    public class ImageIndexEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string RelativePath { get; set; }

        [JsonPropertyName("size")]
        public long SizeBytes { get; set; }

        [JsonPropertyName("modified")]
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Entry for an indexed subdirectory
    /// </summary>
    public class SubdirectoryEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("images")]
        public List<ImageIndexEntry> Images { get; set; } = new List<ImageIndexEntry>();

        [JsonPropertyName("subdirs")]
        public List<SubdirectoryEntry> Subdirectories { get; set; } = new List<SubdirectoryEntry>();

        [JsonIgnore]
        public int TotalImageCount => Images.Count + Subdirectories.Sum(s => s.TotalImageCount);
    }
}
