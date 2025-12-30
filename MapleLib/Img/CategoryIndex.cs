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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapleLib.Img
{
    /// <summary>
    /// Index file for fast category lookups without directory scanning.
    /// Stored as index.json in each category directory.
    /// </summary>
    public class CategoryIndex
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonProperty("totalImageCount")]
        public int TotalImageCount { get; set; }

        [JsonProperty("totalSizeBytes")]
        public long TotalSizeBytes { get; set; }

        [JsonProperty("images")]
        public List<ImageIndexEntry> Images { get; set; } = new List<ImageIndexEntry>();

        [JsonProperty("subdirectories")]
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
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
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
                return JsonConvert.DeserializeObject<CategoryIndex>(json);
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
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string RelativePath { get; set; }

        [JsonProperty("size")]
        public long SizeBytes { get; set; }

        [JsonProperty("modified")]
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Entry for an indexed subdirectory
    /// </summary>
    public class SubdirectoryEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("images")]
        public List<ImageIndexEntry> Images { get; set; } = new List<ImageIndexEntry>();

        [JsonProperty("subdirs")]
        public List<SubdirectoryEntry> Subdirectories { get; set; } = new List<SubdirectoryEntry>();

        [JsonIgnore]
        public int TotalImageCount => Images.Count + Subdirectories.Sum(s => s.TotalImageCount);
    }
}
