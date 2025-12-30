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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapleLib.Img
{
    /// <summary>
    /// A WzDirectory-compatible class that represents a filesystem directory.
    /// Provides the same interface as WzDirectory but loads images from the filesystem.
    /// This enables seamless integration with existing code that expects WzDirectory.
    /// </summary>
    public class VirtualWzDirectory : WzDirectory
    {
        #region Fields
        private readonly ImgFileSystemManager _manager;
        private readonly string _categoryName;
        private readonly string _filesystemPath;
        private readonly string _relativePath;

        private List<WzImage> _images;
        private List<VirtualWzDirectory> _subDirectories;
        private bool _populated;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the filesystem path this directory represents
        /// </summary>
        public string FilesystemPath => _filesystemPath;

        /// <summary>
        /// Gets the category name this directory belongs to
        /// </summary>
        public string CategoryName => _categoryName;

        /// <summary>
        /// Gets the relative path within the category
        /// </summary>
        public string RelativePath => _relativePath;

        /// <summary>
        /// VirtualWzDirectory has no WzFile parent - always returns null
        /// </summary>
        public override WzLib.WzFile WzFileParent => null;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a VirtualWzDirectory for a category root
        /// </summary>
        internal VirtualWzDirectory(ImgFileSystemManager manager, string categoryName, string filesystemPath)
            : base(categoryName)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _categoryName = categoryName;
            _filesystemPath = filesystemPath;
            _relativePath = string.Empty;
        }

        /// <summary>
        /// Creates a VirtualWzDirectory for a subdirectory
        /// </summary>
        internal VirtualWzDirectory(
            ImgFileSystemManager manager,
            string categoryName,
            string filesystemPath,
            string relativePath,
            VirtualWzDirectory parent)
            : base(Path.GetFileName(filesystemPath))
        {
            _manager = manager;
            _categoryName = categoryName;
            _filesystemPath = filesystemPath;
            _relativePath = relativePath;
            this.parent = parent;
        }
        #endregion

        #region Directory Population
        /// <summary>
        /// Populates the directory contents lazily
        /// </summary>
        private void EnsurePopulated()
        {
            if (_populated)
                return;

            PopulateDirectory();
            _populated = true;
        }

        /// <summary>
        /// Populates images and subdirectories from the filesystem
        /// </summary>
        private void PopulateDirectory()
        {
            _images = new List<WzImage>();
            _subDirectories = new List<VirtualWzDirectory>();

            if (!Directory.Exists(_filesystemPath))
                return;

            // Load .img files in this directory
            foreach (var file in Directory.EnumerateFiles(_filesystemPath, "*.img"))
            {
                string imageName = Path.GetFileName(file);
                string imageRelativePath = string.IsNullOrEmpty(_relativePath)
                    ? imageName
                    : Path.Combine(_relativePath, imageName);

                var image = _manager.LoadImage(_categoryName, imageRelativePath);
                if (image != null)
                {
                    image.Parent = this;
                    _images.Add(image);
                }
            }

            // Create VirtualWzDirectory for each subdirectory
            foreach (var dir in Directory.EnumerateDirectories(_filesystemPath))
            {
                string dirName = Path.GetFileName(dir);
                string dirRelativePath = string.IsNullOrEmpty(_relativePath)
                    ? dirName
                    : Path.Combine(_relativePath, dirName);

                var subDir = new VirtualWzDirectory(_manager, _categoryName, dir, dirRelativePath, this);
                _subDirectories.Add(subDir);
            }
        }

        /// <summary>
        /// Forces a refresh of directory contents
        /// </summary>
        public void Refresh()
        {
            _populated = false;
            _images?.Clear();
            _subDirectories?.Clear();
        }
        #endregion

        #region WzDirectory Overrides
        /// <summary>
        /// Gets the list of WzImages in this directory
        /// </summary>
        public new List<WzImage> WzImages
        {
            get
            {
                EnsurePopulated();
                return _images;
            }
        }

        /// <summary>
        /// Gets the list of subdirectories
        /// </summary>
        public new List<WzDirectory> WzDirectories
        {
            get
            {
                EnsurePopulated();
                return _subDirectories.Cast<WzDirectory>().ToList();
            }
        }

        /// <summary>
        /// Gets a WzImage or WzDirectory by name
        /// </summary>
        public new WzObject this[string name]
        {
            get
            {
                EnsurePopulated();

                string nameLower = name.ToLower();

                // Check images first
                foreach (var img in _images)
                {
                    if (img.Name.ToLower() == nameLower)
                        return img;
                }

                // Then check subdirectories
                foreach (var dir in _subDirectories)
                {
                    if (dir.Name.ToLower() == nameLower)
                        return dir;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets an image by name
        /// </summary>
        public new WzImage GetImageByName(string name)
        {
            EnsurePopulated();
            string nameLower = name.ToLower();
            return _images.FirstOrDefault(img => img.Name.ToLower() == nameLower);
        }

        /// <summary>
        /// Gets a subdirectory by name
        /// </summary>
        public new WzDirectory GetDirectoryByName(string name)
        {
            EnsurePopulated();
            string nameLower = name.ToLower();
            return _subDirectories.FirstOrDefault(dir => dir.Name.ToLower() == nameLower);
        }

        /// <summary>
        /// Counts total images including subdirectories
        /// </summary>
        public new int CountImages()
        {
            EnsurePopulated();

            int count = _images.Count;
            foreach (var subDir in _subDirectories)
            {
                count += subDir.CountImages();
            }
            return count;
        }
        #endregion

        #region Additional Methods
        /// <summary>
        /// Gets all images recursively, including those in subdirectories
        /// </summary>
        public IEnumerable<WzImage> GetAllImagesRecursive()
        {
            EnsurePopulated();

            foreach (var img in _images)
            {
                yield return img;
            }

            foreach (var subDir in _subDirectories)
            {
                foreach (var img in subDir.GetAllImagesRecursive())
                {
                    yield return img;
                }
            }
        }

        /// <summary>
        /// Finds an image by relative path within this directory
        /// </summary>
        public WzImage FindImage(string relativePath)
        {
            string[] parts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return null;

            if (parts.Length == 1)
            {
                // Direct child
                return GetImageByName(parts[0]);
            }

            // Navigate through subdirectories
            var currentDir = this;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var nextDir = currentDir.GetDirectoryByName(parts[i]) as VirtualWzDirectory;
                if (nextDir == null)
                    return null;
                currentDir = nextDir;
            }

            return currentDir.GetImageByName(parts[parts.Length - 1]);
        }

        /// <summary>
        /// Gets the full filesystem path for an image
        /// </summary>
        public string GetImageFilePath(string imageName)
        {
            if (!imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                imageName += ".img";

            return Path.Combine(_filesystemPath, imageName);
        }

        /// <summary>
        /// Checks if an image exists in this directory
        /// </summary>
        public bool ImageExists(string imageName)
        {
            return File.Exists(GetImageFilePath(imageName));
        }

        /// <summary>
        /// Gets all subdirectory names
        /// </summary>
        public IEnumerable<string> GetSubdirectoryNames()
        {
            if (!Directory.Exists(_filesystemPath))
                return Enumerable.Empty<string>();

            return Directory.EnumerateDirectories(_filesystemPath)
                           .Select(Path.GetFileName);
        }

        /// <summary>
        /// Gets all image names in this directory (not recursive)
        /// </summary>
        public IEnumerable<string> GetImageNames()
        {
            if (!Directory.Exists(_filesystemPath))
                return Enumerable.Empty<string>();

            return Directory.EnumerateFiles(_filesystemPath, "*.img")
                           .Select(Path.GetFileName);
        }
        #endregion

        #region Save Methods
        /// <summary>
        /// Saves a WzImage to its original location in the filesystem
        /// </summary>
        /// <param name="image">The image to save</param>
        /// <returns>True if saved successfully</returns>
        public bool SaveImage(WzImage image)
        {
            if (image == null)
                return false;

            string filePath = Path.Combine(_filesystemPath, image.Name);
            return _manager.SaveImageToFile(image, filePath);
        }

        /// <summary>
        /// Saves all changed images in this directory
        /// </summary>
        /// <returns>Number of images saved</returns>
        public int SaveAllChangedImages()
        {
            EnsurePopulated();

            int savedCount = 0;
            foreach (var image in _images)
            {
                if (image.Changed)
                {
                    if (SaveImage(image))
                    {
                        savedCount++;
                    }
                }
            }

            // Recursively save subdirectories
            foreach (var subDir in _subDirectories)
            {
                savedCount += subDir.SaveAllChangedImages();
            }

            return savedCount;
        }

        /// <summary>
        /// Gets the ImgFileSystemManager for this directory
        /// </summary>
        public ImgFileSystemManager Manager => _manager;
        #endregion

        #region Dispose
        public override void Dispose()
        {
            _images?.Clear();
            _subDirectories?.Clear();
            _images = null;
            _subDirectories = null;
            _populated = false;
        }
        #endregion
    }
}
