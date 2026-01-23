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

        /// <summary>
        /// Temporary storage for all canvas images with matching name during resolution
        /// </summary>
        private List<WzImage> _currentCanvasImages = new List<WzImage>();
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
                // Use GetCompressedBytesForExtraction to convert listWz format to standard zlib format.
                // This is critical because SetCompressedBytes clears the wzReader reference,
                // so we must convert while the source still has access to the WzKey.
                byte[] compressedBytes = sourcePng.GetCompressedBytesForExtraction(false);
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
        /// <param name="outlinkPath">The outlink path (e.g., "Mob/8800141.img/attack1/0" or "Map/Back/_Canvas/snowyDarkrock.img/back/0")</param>
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
                // For _Canvas: "Map/Back/_Canvas/snowyDarkrock.img/back/0" or "Map/_Canvas/MapHelper.img/mark/Hilla"
                string[] parts = outlinkPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return false;

                string linkCategory = parts[0]; // e.g., "Mob" or "Map"

                // Check if this outlink is within our category (we can only resolve same-category outlinks)
                if (!string.Equals(linkCategory, _categoryName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[WzLinkResolver] Outlink to different category '{linkCategory}' cannot be resolved (current: {_categoryName})");
                    return false;
                }

                // Check if this is a _Canvas path
                bool isCanvasPath = outlinkPath.Contains("/_Canvas/", StringComparison.OrdinalIgnoreCase) ||
                                    outlinkPath.Contains("_Canvas/", StringComparison.OrdinalIgnoreCase);

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
                // e.g., for "Map/Back/_Canvas/snowyDarkrock.img", subdirPath = "Back/_Canvas"
                string subdirPath = imgIndex > 1
                    ? string.Join("/", parts.Skip(1).Take(imgIndex - 1))
                    : null;

                string imageName = parts[imgIndex]; // e.g., "0243.img" or "snowyDarkrock.img"

                // Build the property path within the image
                string propertyPath = imgIndex + 1 < parts.Length
                    ? string.Join("/", parts.Skip(imgIndex + 1))
                    : null;

                // Search for the image across all loaded WZ files
                WzImage targetImage = null;

                // For _Canvas paths, we need to search specifically in the _Canvas WZ files
                // The _Canvas WZ files contain the images directly at root level
                if (isCanvasPath)
                {
                    // Extract the path after "_Canvas/" marker
                    // e.g., "Map/Back/_Canvas/snowyDarkrock.img/back/0" -> "snowyDarkrock.img/back/0"
                    string canvasMarker = "/_Canvas/";
                    int canvasMarkerIndex = outlinkPath.IndexOf(canvasMarker, StringComparison.OrdinalIgnoreCase);
                    string pathAfterCanvas = canvasMarkerIndex >= 0
                        ? outlinkPath.Substring(canvasMarkerIndex + canvasMarker.Length)
                        : null;

                    if (!string.IsNullOrEmpty(pathAfterCanvas))
                    {
                        // Parse the path after _Canvas to get image name and property path
                        string[] canvasParts = pathAfterCanvas.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (canvasParts.Length >= 1)
                        {
                            // First part should be the image name (e.g., "snowyDarkrock.img")
                            string canvasImageName = canvasParts[0];
                            if (canvasImageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                            {
                                imageName = canvasImageName;
                                // Property path is everything after the image name
                                propertyPath = canvasParts.Length > 1
                                    ? string.Join("/", canvasParts.Skip(1))
                                    : null;
                            }
                        }
                    }

                    // Collect ALL matching images from ALL _Canvas WZ files
                    // The same image name might exist in multiple _Canvas_xxx.wz files with different frame content
                    var canvasImages = new List<WzImage>();

                    foreach (var wzFile in _categoryWzFiles)
                    {
                        bool isCanvasWzFile = wzFile.FilePath?.Contains("_Canvas", StringComparison.OrdinalIgnoreCase) == true ||
                                              wzFile.Name?.Contains("_Canvas", StringComparison.OrdinalIgnoreCase) == true;

                        if (isCanvasWzFile && wzFile.WzDirectory != null)
                        {
                            // Search at root level
                            var img = FindImageInDirectory(wzFile.WzDirectory, imageName, null);
                            if (img != null)
                                canvasImages.Add(img);

                            // Also search subdirectories
                            if (wzFile.WzDirectory.WzDirectories != null)
                            {
                                foreach (var subDir in wzFile.WzDirectory.WzDirectories)
                                {
                                    if (subDir == null) continue;
                                    img = FindImageInDirectory(subDir, imageName, null);
                                    if (img != null)
                                        canvasImages.Add(img);
                                }
                            }
                        }
                    }

                    // Use first found image for now (will search all in property resolution)
                    targetImage = canvasImages.FirstOrDefault();

                    // Store all canvas images for later search
                    _currentCanvasImages = canvasImages;
                }
                else
                {
                    // Non-canvas paths - search normally
                    foreach (var wzFile in _categoryWzFiles)
                    {
                        targetImage = FindImageInDirectory(wzFile.WzDirectory, imageName, subdirPath);
                        if (targetImage != null)
                            break;
                    }
                }

                if (targetImage == null)
                {
                    string fullImagePath = string.IsNullOrEmpty(subdirPath) ? imageName : $"{subdirPath}/{imageName}";
                    Debug.WriteLine($"[WzLinkResolver] Could not find image '{fullImagePath}' in any loaded WZ file (isCanvas: {isCanvasPath})");
                    return false;
                }

                // Parse the target image if needed
                if (!targetImage.Parsed)
                {
                    targetImage.ParseImage();
                }

                // Navigate to the property within the image
                // For _Canvas paths, search ALL canvas images with matching name (frames may be split across files)
                WzImageProperty targetProperty = null;
                var imagesToSearch = isCanvasPath && _currentCanvasImages.Count > 0
                    ? _currentCanvasImages
                    : (targetImage != null ? new List<WzImage> { targetImage } : new List<WzImage>());

                foreach (var searchImage in imagesToSearch)
                {
                    if (searchImage == null) continue;

                    // Parse image if needed
                    if (!searchImage.Parsed)
                        searchImage.ParseImage();

                    if (!string.IsNullOrEmpty(propertyPath))
                    {
                        // First try the exact path
                        targetProperty = searchImage.GetFromPath(propertyPath);

                        // If not found and this is a _Canvas path, the structure might be different
                        // _Canvas WZ files use a simplified structure: "Anims/0/..." instead of "AnimSet/activated/LayerSlots/..."
                        if (targetProperty == null && isCanvasPath)
                        {
                            string[] pathParts = propertyPath.Split('/');
                            string lastComponent = pathParts[pathParts.Length - 1];

                            // Strategy 1: _Canvas files mirror the path structure but use "Anims/0" instead of "AnimSet"
                            // Outlink: "AnimSet/activated/LayerSlots/Slot0/Segment1/AnimReference/8"
                            // _Canvas: "Anims/0/stand/LayerSlots/Slot0/Segment0/AnimReference/8"
                            // Issues: animation name may differ, segment name may differ (Segment0 vs Segment1)
                            if (targetProperty == null && pathParts.Length > 2)
                            {
                                // Build the remaining path after the animation name
                                // e.g., "LayerSlots/Slot0/Segment1/AnimReference/8"
                                string remainingPath = string.Join("/", pathParts.Skip(2));
                                string animNameFromPath = pathParts[1]; // e.g., "activated"

                                // Generate alternative paths for segment name mismatches
                                var pathsToTry = new List<string> { remainingPath };

                                // If path contains SegmentN, also try Segment0, Segment:All, etc.
                                if (remainingPath.Contains("Segment"))
                                {
                                    // Try Segment0 instead of SegmentN
                                    var segment0Path = System.Text.RegularExpressions.Regex.Replace(
                                        remainingPath, @"Segment\d+", "Segment0");
                                    if (segment0Path != remainingPath)
                                        pathsToTry.Add(segment0Path);

                                    // Try Segment:All
                                    var segmentAllPath = System.Text.RegularExpressions.Regex.Replace(
                                        remainingPath, @"Segment[^/]+", "Segment:All");
                                    if (segmentAllPath != remainingPath)
                                        pathsToTry.Add(segmentAllPath);
                                }

                                var animsNode = searchImage["Anims"];
                                if (animsNode != null && animsNode.WzProperties != null)
                                {
                                    foreach (var animSubdir in animsNode.WzProperties)
                                    {
                                        if (animSubdir.WzProperties == null) continue;

                                        // Try each animation container
                                        foreach (var animContainer in animSubdir.WzProperties)
                                        {
                                            // Try each path variant
                                            foreach (var pathToTry in pathsToTry)
                                            {
                                                targetProperty = animContainer.GetFromPath(pathToTry);
                                                if (targetProperty is WzCanvasProperty)
                                                    break;
                                                targetProperty = null;
                                            }
                                            if (targetProperty != null) break;
                                        }
                                        if (targetProperty != null) break;
                                    }
                                }
                            }

                            // Strategy 1b: Search for exact frame in AnimReference under any animation/segment
                            if (targetProperty == null && int.TryParse(lastComponent, out int frameIdx))
                            {
                                var animsNode = searchImage["Anims"];
                            if (animsNode != null && animsNode.WzProperties != null)
                            {
                                foreach (var animSubdir in animsNode.WzProperties)
                                {
                                    if (animSubdir.WzProperties == null) continue;
                                    foreach (var animContainer in animSubdir.WzProperties)
                                    {
                                        // Navigate to AnimReference under any Segment
                                        var layerSlots = animContainer["LayerSlots"];
                                        var slot0 = layerSlots?["Slot0"];
                                        if (slot0?.WzProperties != null)
                                        {
                                            foreach (var segment in slot0.WzProperties)
                                            {
                                                var animRef = segment["AnimReference"];
                                                if (animRef?.WzProperties != null)
                                                {
                                                    // Only use exact frame match - no fallbacks
                                                    var exactFrame = animRef[lastComponent];
                                                    if (exactFrame is WzCanvasProperty)
                                                    {
                                                        targetProperty = exactFrame;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        if (targetProperty != null) break;
                                    }
                                    if (targetProperty != null) break;
                                }
                            }
                        }

                        // Strategy 2: Try to find a canvas at the root level with that name
                        if (targetProperty == null)
                            targetProperty = searchImage[lastComponent];

                        // Strategy 3: Try progressively shorter paths from the end
                        // e.g., "AnimSet/stand/LayerSlots/Slot0/Segment0/AnimReference/0"
                        // Try: "Segment0/AnimReference/0", then "AnimReference/0", then "0"
                        if (targetProperty == null)
                        {
                            for (int i = pathParts.Length - 2; i >= 0 && targetProperty == null; i--)
                            {
                                string partialPath = string.Join("/", pathParts.Skip(i));
                                targetProperty = searchImage.GetFromPath(partialPath);
                            }
                        }

                        // Strategy 4: Search recursively for a canvas with the same name
                        if (targetProperty == null)
                        {
                            targetProperty = FindCanvasInImage(searchImage, lastComponent);
                        }

                        // Strategy 5: For numeric names like "0", "1", search for any canvas
                        // at the equivalent position in the image's own structure
                        if (targetProperty == null && int.TryParse(lastComponent, out int frameIndex))
                        {
                            // Try to find any canvas at root level with that index
                            var rootCanvas = FindCanvasByIndex(searchImage, frameIndex);
                            if (rootCanvas != null)
                            {
                                targetProperty = rootCanvas;
                            }
                        }

                        // Strategy 6: Try matching parent/child pattern anywhere in the image
                        // e.g., for "AnimReference/7", search for any "AnimReference" that has child "7"
                        if (targetProperty == null && pathParts.Length >= 2)
                        {
                            string parentName = pathParts[pathParts.Length - 2];
                            targetProperty = FindCanvasWithParent(searchImage, parentName, lastComponent);
                        }

                        // Strategy 7: Get all canvases and find one that might match by index
                        // This handles cases where _Canvas has flat structure with different naming
                        if (targetProperty == null && int.TryParse(lastComponent, out int idx))
                        {
                            var allCanvases = GetAllCanvasesInImage(searchImage);
                            if (idx < allCanvases.Count)
                            {
                                targetProperty = allCanvases[idx];
                            }
                        }
                        } // end isCanvasPath strategies
                    } // end if propertyPath

                    // If found, break out of image search loop
                    if (targetProperty is WzCanvasProperty)
                        break;
                } // end foreach searchImage

                if (string.IsNullOrEmpty(propertyPath))
                {
                    // No property path - the outlink points to the image itself, which is unusual
                    Debug.WriteLine($"[WzLinkResolver] Outlink has no property path, only image: {imageName}");
                    return false;
                }

                if (targetProperty == null)
                {
                    Debug.WriteLine($"[WzLinkResolver] Could not find property path '{propertyPath}' in image '{imageName}' (searched {imagesToSearch.Count} _Canvas images)");
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
                    if (current.WzDirectories == null)
                        return null;

                    WzDirectory nextDir = null;
                    foreach (var dir in current.WzDirectories)
                    {
                        if (dir != null && string.Equals(dir.Name, subdir, StringComparison.OrdinalIgnoreCase))
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
                if (current.WzImages != null)
                {
                    foreach (var image in current.WzImages)
                    {
                        if (image != null && string.Equals(image.Name, imageName, StringComparison.OrdinalIgnoreCase))
                            return image;
                    }
                }

                return null;
            }

            // No subdirectory specified - search recursively
            // Check images in this directory
            if (directory.WzImages != null)
            {
                foreach (var image in directory.WzImages)
                {
                    if (image != null && string.Equals(image.Name, imageName, StringComparison.OrdinalIgnoreCase))
                        return image;
                }
            }

            // Check subdirectories recursively
            if (directory.WzDirectories != null)
            {
                foreach (var subDir in directory.WzDirectories)
                {
                    if (subDir != null)
                    {
                        var found = FindImageInDirectory(subDir, imageName, null);
                        if (found != null)
                            return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Recursively searches for a canvas property by name within an image.
        /// Used when the exact path in _Canvas files doesn't match the outlink path.
        /// </summary>
        /// <param name="image">The WzImage to search in</param>
        /// <param name="canvasName">The name of the canvas to find</param>
        /// <returns>The canvas property if found, null otherwise</returns>
        private WzCanvasProperty FindCanvasInImage(WzImage image, string canvasName)
        {
            if (image == null || image.WzProperties == null)
                return null;

            foreach (var prop in image.WzProperties)
            {
                var result = FindCanvasInProperty(prop, canvasName);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Recursively searches for a canvas property by name within a property tree.
        /// </summary>
        private WzCanvasProperty FindCanvasInProperty(WzImageProperty property, string canvasName)
        {
            if (property == null)
                return null;

            // Check if this is the canvas we're looking for
            if (property is WzCanvasProperty canvas &&
                string.Equals(property.Name, canvasName, StringComparison.OrdinalIgnoreCase))
            {
                return canvas;
            }

            // Search children
            var children = property.WzProperties;
            if (children != null)
            {
                foreach (var child in children)
                {
                    var result = FindCanvasInProperty(child, canvasName);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Searches for a canvas property by numeric index within an image.
        /// Useful for finding frame canvases like "0", "1", "2" in animation sequences.
        /// </summary>
        /// <param name="image">The WzImage to search in</param>
        /// <param name="index">The numeric index to find</param>
        /// <returns>The canvas property if found, null otherwise</returns>
        private WzCanvasProperty FindCanvasByIndex(WzImage image, int index)
        {
            if (image == null || image.WzProperties == null)
                return null;

            string indexName = index.ToString();

            // First try direct child with that name
            var directChild = image[indexName];
            if (directChild is WzCanvasProperty directCanvas)
                return directCanvas;

            // Search through all properties for a canvas with that name
            foreach (var prop in image.WzProperties)
            {
                if (prop is WzCanvasProperty canvas && prop.Name == indexName)
                    return canvas;

                // Also check one level deep (common structure: subprop/0, subprop/1)
                var children = prop.WzProperties;
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        if (child is WzCanvasProperty childCanvas && child.Name == indexName)
                            return childCanvas;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Searches for a canvas property where the parent has a specific name and the canvas has a specific name.
        /// Useful for finding paths like "AnimReference/7" anywhere in the image structure.
        /// </summary>
        /// <param name="image">The WzImage to search in</param>
        /// <param name="parentName">The name of the parent property to match</param>
        /// <param name="childName">The name of the canvas child to find</param>
        /// <returns>The canvas property if found, null otherwise</returns>
        private WzCanvasProperty FindCanvasWithParent(WzImage image, string parentName, string childName)
        {
            if (image == null || image.WzProperties == null)
                return null;

            foreach (var prop in image.WzProperties)
            {
                var result = FindCanvasWithParentInProperty(prop, parentName, childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Recursively searches for a canvas with parent/child name pattern in a property tree.
        /// </summary>
        private WzCanvasProperty FindCanvasWithParentInProperty(WzImageProperty property, string parentName, string childName)
        {
            if (property == null)
                return null;

            // Check if this property matches the parent name and has a canvas child with the child name
            if (string.Equals(property.Name, parentName, StringComparison.OrdinalIgnoreCase))
            {
                var children = property.WzProperties;
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        if (child is WzCanvasProperty canvas &&
                            string.Equals(child.Name, childName, StringComparison.OrdinalIgnoreCase))
                        {
                            return canvas;
                        }
                    }
                }
            }

            // Continue searching in children
            var props = property.WzProperties;
            if (props != null)
            {
                foreach (var child in props)
                {
                    var result = FindCanvasWithParentInProperty(child, parentName, childName);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all canvas properties in an image in a flat list.
        /// Useful as a fallback when path-based matching fails.
        /// </summary>
        /// <param name="image">The WzImage to search in</param>
        /// <returns>List of all canvas properties in the image</returns>
        private List<WzCanvasProperty> GetAllCanvasesInImage(WzImage image)
        {
            var canvases = new List<WzCanvasProperty>();

            if (image == null || image.WzProperties == null)
                return canvases;

            foreach (var prop in image.WzProperties)
            {
                CollectCanvasesFromProperty(prop, canvases);
            }

            return canvases;
        }

        /// <summary>
        /// Recursively collects all canvas properties from a property tree.
        /// </summary>
        private void CollectCanvasesFromProperty(WzImageProperty property, List<WzCanvasProperty> canvases)
        {
            if (property == null)
                return;

            if (property is WzCanvasProperty canvas)
            {
                canvases.Add(canvas);
            }

            var children = property.WzProperties;
            if (children != null)
            {
                foreach (var child in children)
                {
                    CollectCanvasesFromProperty(child, canvases);
                }
            }
        }

        /// <summary>
        /// Debug helper to show property tree structure
        /// </summary>
        private void ShowPropertyTree(WzImageProperty property, string indent, int maxDepth)
        {
            if (property == null || maxDepth <= 0) return;

            var children = property.WzProperties;
            if (children == null || children.Count == 0)
            {
                Debug.WriteLine($"{indent}{property.Name} ({property.PropertyType})");
                return;
            }

            Debug.WriteLine($"{indent}{property.Name}/");
            foreach (var child in children.Take(3))
            {
                ShowPropertyTree(child, indent + "  ", maxDepth - 1);
            }
            if (children.Count > 3)
            {
                Debug.WriteLine($"{indent}  ... and {children.Count - 3} more");
            }
        }
        #endregion
    }
}
