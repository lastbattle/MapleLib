using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapleLib.WzLib
{
    /// <summary>
    /// Service for resolving _inlink and _outlink canvas references in WZ files.
    /// Resolves links in-place by embedding the actual bitmap data.
    /// </summary>
    public class WzLinkResolver
    {
        #region Properties
        /// <summary>
        /// Number of links successfully resolved
        /// </summary>
        public int LinksResolved { get; private set; }

        /// <summary>
        /// Number of links that failed to resolve
        /// </summary>
        public int LinksFailed { get; private set; }

        /// <summary>
        /// List of failed link paths for debugging
        /// </summary>
        public List<string> FailedLinks { get; } = new List<string>();
        #endregion

        #region Fields
        /// <summary>
        /// All loaded WZ files for the current category, used for cross-file outlink resolution
        /// </summary>
        private List<WzFile> _categoryWzFiles = new List<WzFile>();

        /// <summary>
        /// Category name (e.g., "Mob", "Npc") for path matching
        /// </summary>
        private string _categoryName;
        #endregion

        #region Public Methods
        /// <summary>
        /// Sets the WZ files for the current category to enable cross-file outlink resolution.
        /// Call this before resolving links in images.
        /// </summary>
        /// <param name="wzFiles">All WZ files for this category (e.g., Mob.wz, Mob001.wz, Mob2.wz)</param>
        /// <param name="categoryName">Category name (e.g., "Mob")</param>
        public void SetCategoryWzFiles(IEnumerable<WzFile> wzFiles, string categoryName)
        {
            _categoryWzFiles = wzFiles?.ToList() ?? new List<WzFile>();
            _categoryName = categoryName;
        }

        /// <summary>
        /// Resolves a single canvas property's _inlink/_outlink reference.
        /// Modifies the canvas in-place by embedding the linked image data and removing the link property.
        /// Uses direct compressed byte copy for efficiency (avoids bitmap decompression/recompression).
        /// </summary>
        /// <param name="canvas">The canvas property to resolve</param>
        /// <param name="inlinkOnly">If true, only resolve _inlink (faster, doesn't load external files)</param>
        /// <returns>True if a link was resolved, false if no link or resolution failed</returns>
        public static bool ResolveSingleCanvas(WzCanvasProperty canvas, bool inlinkOnly = false)
        {
            if (canvas == null)
                return false;

            bool hasInlink = canvas.ContainsInlinkProperty();
            bool hasOutlink = canvas.ContainsOutlinkProperty();

            if (!hasInlink && !hasOutlink)
                return false;

            // Skip _outlink if inlinkOnly is set (outlink requires loading external WZ files)
            if (inlinkOnly && !hasInlink)
                return false;

            try
            {
                // Get the linked target canvas
                WzImageProperty linkedTarget = canvas.GetLinkedWzImageProperty();

                // If resolution succeeded (returns different object than self)
                if (linkedTarget != null && linkedTarget != canvas && linkedTarget is WzCanvasProperty linkedCanvas)
                {
                    return CopyCanvasData(canvas, linkedCanvas, hasInlink, hasOutlink);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WzLinkResolver] Exception resolving canvas link: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Resolves all _inlink/_outlink references in a WzImage.
        /// Modifies the image in-place before serialization.
        /// </summary>
        /// <param name="image">The WzImage to process</param>
        /// <returns>Number of links resolved in this image</returns>
        public int ResolveLinksInImage(WzImage image)
        {
            if (image == null)
                return 0;

            // Parse image if not already parsed
            if (!image.Parsed)
            {
                image.ParseImage();
            }

            int resolvedCount = 0;

            // Recursively process all properties
            foreach (WzImageProperty prop in image.WzProperties)
            {
                resolvedCount += ResolveLinksInProperty(prop, image.FullPath ?? image.Name);
            }

            return resolvedCount;
        }

        /// <summary>
        /// Resets the resolver statistics for a new extraction session
        /// </summary>
        public void Reset()
        {
            LinksResolved = 0;
            LinksFailed = 0;
            FailedLinks.Clear();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Copies canvas data from source to destination
        /// </summary>
        private static bool CopyCanvasData(WzCanvasProperty destCanvas, WzCanvasProperty srcCanvas, bool hasInlink, bool hasOutlink)
        {
            // Copy the compressed image data directly (avoids bitmap decompression/recompression)
            WzPngProperty sourcePng = srcCanvas.PngProperty;
            WzPngProperty destPng = destCanvas.PngProperty;

            if (sourcePng != null && destPng != null)
            {
                // Get compressed bytes from source and set on destination
                byte[] compressedBytes = sourcePng.GetCompressedBytes(false);
                destPng.SetCompressedBytes(compressedBytes, sourcePng.Width, sourcePng.Height, sourcePng.Format);
            }

            // Remove the link property
            if (hasInlink)
            {
                destCanvas.RemoveProperty(WzCanvasProperty.InlinkPropertyName);
            }
            if (hasOutlink)
            {
                destCanvas.RemoveProperty(WzCanvasProperty.OutlinkPropertyName);
            }
            return true;
        }

        /// <summary>
        /// Recursively resolves links in a property and its children.
        /// Optimized to skip property types that cannot contain canvas properties.
        /// </summary>
        private int ResolveLinksInProperty(WzImageProperty property, string parentPath)
        {
            if (property == null)
                return 0;

            // Early exit for property types that cannot contain canvas children
            // This is critical for performance - String.wz has thousands of string properties
            // that we don't need to traverse
            switch (property.PropertyType)
            {
                case WzPropertyType.String:
                case WzPropertyType.Short:
                case WzPropertyType.Int:
                case WzPropertyType.Long:
                case WzPropertyType.Float:
                case WzPropertyType.Double:
                case WzPropertyType.Sound:
                case WzPropertyType.Null:
                case WzPropertyType.PNG:
                case WzPropertyType.UOL:
                    return 0;
            }

            int resolvedCount = 0;
            string currentPath = $"{parentPath}/{property.Name}";

            // If this is a canvas property, check for links
            if (property is WzCanvasProperty canvas)
            {
                if (TryResolveCanvasLink(canvas, currentPath))
                {
                    resolvedCount++;
                }
            }

            // Recursively process child properties (only for container types)
            var children = property.WzProperties;
            if (children != null && children.Count > 0)
            {
                foreach (WzImageProperty child in children)
                {
                    resolvedCount += ResolveLinksInProperty(child, currentPath);
                }
            }

            return resolvedCount;
        }

        /// <summary>
        /// Attempts to resolve an _inlink or _outlink in a canvas property
        /// </summary>
        /// <param name="canvas">The canvas property to resolve</param>
        /// <param name="path">The path for logging purposes</param>
        /// <returns>True if a link was resolved, false otherwise</returns>
        private bool TryResolveCanvasLink(WzCanvasProperty canvas, string path)
        {
            bool hasInlink = canvas.ContainsInlinkProperty();
            bool hasOutlink = canvas.ContainsOutlinkProperty();

            if (!hasInlink && !hasOutlink)
                return false;

            string linkType = hasInlink ? "_inlink" : "_outlink";
            string linkValue = hasInlink
                ? ((WzStringProperty)canvas[WzCanvasProperty.InlinkPropertyName])?.Value ?? "unknown"
                : ((WzStringProperty)canvas[WzCanvasProperty.OutlinkPropertyName])?.Value ?? "unknown";

            // Try to resolve _inlink first (within same image)
            if (hasInlink)
            {
                if (ResolveSingleCanvas(canvas, inlinkOnly: true))
                {
                    LinksResolved++;
                    return true;
                }
                else
                {
                    // Inlink resolution failed
                    LinksFailed++;
                    FailedLinks.Add($"{path} ({linkType}: {linkValue})");
                    Debug.WriteLine($"[WzLinkResolver] Failed to resolve {linkType} at {path} -> {linkValue}");
                    return false;
                }
            }

            // Try to resolve _outlink across all loaded WZ files
            if (hasOutlink)
            {
                if (TryResolveOutlink(canvas, linkValue, path))
                {
                    LinksResolved++;
                    return true;
                }
                else
                {
                    // Outlink resolution failed
                    LinksFailed++;
                    FailedLinks.Add($"{path} ({linkType}: {linkValue})");
                    Debug.WriteLine($"[WzLinkResolver] Failed to resolve {linkType} at {path} -> {linkValue}");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to resolve an outlink by searching across all loaded WZ files for the category
        /// </summary>
        /// <param name="canvas">The canvas with the outlink</param>
        /// <param name="outlinkPath">The outlink path (e.g., "Mob/8800141.img/attack1/0" or "Item/Consume/0243.img/123/info")</param>
        /// <param name="logPath">Path for logging</param>
        /// <returns>True if resolved successfully</returns>
        private bool TryResolveOutlink(WzCanvasProperty canvas, string outlinkPath, string logPath)
        {
            if (string.IsNullOrEmpty(outlinkPath) || _categoryWzFiles == null || _categoryWzFiles.Count == 0)
                return false;

            try
            {
                // Parse the outlink path
                // Format: "Category/[Subdirs/]ImageName.img/property/path"
                // e.g., "Mob/8800141.img/attack1/0" or "Item/Consume/0243.img/123/info"
                string[] parts = outlinkPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return false;

                string linkCategory = parts[0]; // e.g., "Mob" or "Item"

                // Check if this outlink is within our category (we can only resolve same-category outlinks)
                if (!string.Equals(linkCategory, _categoryName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[WzLinkResolver] Outlink to different category '{linkCategory}' cannot be resolved (current: {_categoryName})");
                    return false;
                }

                // Find the image name - look for .img in the path
                int imgIndex = -1;
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                    {
                        imgIndex = i;
                        break;
                    }
                }

                if (imgIndex < 0)
                    return false;

                // Build subdirectory path (parts between category and image)
                // e.g., for "Item/Consume/0243.img", subdirPath = "Consume"
                string subdirPath = imgIndex > 1
                    ? string.Join("/", parts.Skip(1).Take(imgIndex - 1))
                    : null;

                string imageName = parts[imgIndex]; // e.g., "0243.img"

                // Build the property path within the image
                string propertyPath = imgIndex + 1 < parts.Length
                    ? string.Join("/", parts.Skip(imgIndex + 1))
                    : null;

                // Search for the image across all loaded WZ files
                WzImage targetImage = null;
                foreach (var wzFile in _categoryWzFiles)
                {
                    targetImage = FindImageInDirectory(wzFile.WzDirectory, imageName, subdirPath);
                    if (targetImage != null)
                        break;
                }

                if (targetImage == null)
                {
                    string fullImagePath = string.IsNullOrEmpty(subdirPath) ? imageName : $"{subdirPath}/{imageName}";
                    Debug.WriteLine($"[WzLinkResolver] Could not find image '{fullImagePath}' in any loaded WZ file");
                    return false;
                }

                // Parse the target image if needed
                if (!targetImage.Parsed)
                {
                    targetImage.ParseImage();
                }

                // Navigate to the property within the image
                WzImageProperty targetProperty = null;
                if (!string.IsNullOrEmpty(propertyPath))
                {
                    targetProperty = targetImage.GetFromPath(propertyPath);
                }
                else
                {
                    // No property path - the outlink points to the image itself, which is unusual
                    Debug.WriteLine($"[WzLinkResolver] Outlink has no property path, only image: {imageName}");
                    return false;
                }

                if (targetProperty == null)
                {
                    Debug.WriteLine($"[WzLinkResolver] Could not find property path '{propertyPath}' in image '{imageName}'");
                    return false;
                }

                // Must be a canvas property
                if (!(targetProperty is WzCanvasProperty targetCanvas))
                {
                    Debug.WriteLine($"[WzLinkResolver] Target property is not a canvas: {targetProperty.PropertyType}");
                    return false;
                }

                // Copy the data
                return CopyCanvasData(canvas, targetCanvas, false, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WzLinkResolver] Exception resolving outlink: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Searches for an image by name in a WZ directory, optionally within a specific subdirectory path
        /// </summary>
        /// <param name="directory">The root directory to search in</param>
        /// <param name="imageName">The image name (e.g., "0243.img")</param>
        /// <param name="subdirPath">Optional subdirectory path (e.g., "Consume" or "Pet/Special")</param>
        private WzImage FindImageInDirectory(WzDirectory directory, string imageName, string subdirPath = null)
        {
            if (directory == null)
                return null;

            // If subdirectory path is specified, navigate to it first
            if (!string.IsNullOrEmpty(subdirPath))
            {
                string[] subdirs = subdirPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                WzDirectory current = directory;

                foreach (string subdir in subdirs)
                {
                    WzDirectory nextDir = null;
                    foreach (var dir in current.WzDirectories)
                    {
                        if (string.Equals(dir.Name, subdir, StringComparison.OrdinalIgnoreCase))
                        {
                            nextDir = dir;
                            break;
                        }
                    }

                    if (nextDir == null)
                        return null; // Subdirectory not found

                    current = nextDir;
                }

                // Now search for the image in the target subdirectory
                foreach (var image in current.WzImages)
                {
                    if (string.Equals(image.Name, imageName, StringComparison.OrdinalIgnoreCase))
                        return image;
                }

                return null;
            }

            // No subdirectory specified - search recursively
            // Check images in this directory
            foreach (var image in directory.WzImages)
            {
                if (string.Equals(image.Name, imageName, StringComparison.OrdinalIgnoreCase))
                    return image;
            }

            // Check subdirectories recursively
            foreach (var subDir in directory.WzDirectories)
            {
                var found = FindImageInDirectory(subDir, imageName, null);
                if (found != null)
                    return found;
            }

            return null;
        }
        #endregion
    }
}
