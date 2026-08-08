/*  MapleLib.Tests - Unit tests for MapleLib
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System.IO;
using System.Text.Json;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests.Img
{
    public class ImgFileSystemManagerTests : IDisposable
    {
        private readonly string _testVersionPath;
        private readonly HaCreatorConfig _config;

        public ImgFileSystemManagerTests()
        {
            // Create a temporary test directory with version structure
            _testVersionPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testVersionPath);

            _config = new HaCreatorConfig
            {
                ImgRootPath = _testVersionPath
            };

            // Create test version structure
            SetupTestVersionStructure();
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testVersionPath))
            {
                try
                {
                    Directory.Delete(_testVersionPath, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        private void SetupTestVersionStructure()
        {
            // Create category directories
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "String"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Map"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Map", "Map"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Map", "Map", "Map0"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Mob"));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, HaCreatorPaths.BackupsFolderName));
            Directory.CreateDirectory(Path.Combine(_testVersionPath, "Map", HaCreatorPaths.BackupsFolderName));

            // Create mock .img files (required for category detection)
            // The manager only recognizes categories that contain .img files
            CreateMockImgFile(Path.Combine(_testVersionPath, "String", "Test.img"));
            CreateMockImgFile(Path.Combine(_testVersionPath, "Map", "Test.img"));
            CreateMockImgFile(Path.Combine(_testVersionPath, "Mob", "Test.img"));
            CreateMockImgFile(Path.Combine(_testVersionPath, HaCreatorPaths.BackupsFolderName, "Ignored.img"));
            CreateMockImgFile(Path.Combine(_testVersionPath, "Map", HaCreatorPaths.BackupsFolderName, "Ignored.img"));

            // Create manifest
            CreateTestManifest();
        }

        private void CreateMockImgFile(string path)
        {
            // Create a minimal mock .img file (just needs to exist for directory scanning)
            File.WriteAllBytes(path, Array.Empty<byte>());
        }

        private void CreateTestManifest()
        {
            var manifest = new
            {
                version = "test",
                displayName = "Test Version",
                extractedDate = DateTime.UtcNow.ToString("o"),
                encryption = "GMS",
                is64Bit = false,
                isPreBB = false,
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 2, lastModified = DateTime.UtcNow.ToString("o") },
                    ["Map"] = new { fileCount = 5, lastModified = DateTime.UtcNow.ToString("o") },
                    ["Mob"] = new { fileCount = 3, lastModified = DateTime.UtcNow.ToString("o") }
                },
                features = new { }
            };

            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(_testVersionPath, "manifest.json"), json);
        }

        [Fact]
        public void Constructor_ValidPath_InitializesSuccessfully()
        {
            // Act
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Assert
            Assert.True(manager.IsInitialized);
            Assert.NotNull(manager.VersionInfo);
        }

        [Fact]
        public void Constructor_InvalidPath_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() =>
            {
                using var manager = new ImgFileSystemManager("/nonexistent/path", _config);
                manager.Initialize();
            });
        }

        [Fact]
        public void GetCategories_ReturnsAvailableCategories()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var categories = manager.GetCategories().ToList();

            // Assert - categories are stored in lowercase
            Assert.Contains(categories, c => c.Equals("string", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(categories, c => c.Equals("map", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(categories, c => c.Equals("mob", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(categories, c => c.Equals(HaCreatorPaths.BackupsFolderName, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CategoryExists_ExistingCategory_ReturnsTrue()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert
            Assert.True(manager.CategoryExists("String"));
            Assert.True(manager.CategoryExists("Map"));
        }

        [Fact]
        public void CategoryExists_NonExistingCategory_ReturnsFalse()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert
            Assert.False(manager.CategoryExists("NonExistent"));
        }

        [Fact]
        public void CategoryExists_CaseInsensitive_ReturnsTrue()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert
            Assert.True(manager.CategoryExists("string"));
            Assert.True(manager.CategoryExists("STRING"));
            Assert.True(manager.CategoryExists("String"));
        }

        [Fact]
        public void GetSubdirectories_ReturnsSubdirectories()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var subdirs = manager.GetSubdirectories("Map").ToList();

            // Assert
            Assert.NotEmpty(subdirs);
            Assert.Contains(subdirs, s => s.Contains("Map"));
            Assert.DoesNotContain(subdirs, s => HaCreatorPaths.ContainsBackupsDirectory(s));
        }

        [Fact]
        public void Constructor_MalformedManifest_ThrowsInvalidDataException()
        {
            File.WriteAllText(Path.Combine(_testVersionPath, "manifest.json"), "null");

            Assert.Throws<InvalidDataException>(() => new ImgFileSystemManager(_testVersionPath, _config));
        }

        [Fact]
        public void Constructor_OversizedManifestIsRejectedBeforeReading()
        {
            string manifestPath = Path.Combine(_testVersionPath, "manifest.json");
            using (FileStream stream = new FileStream(manifestPath, FileMode.Create, FileAccess.Write, FileShare.None))
                stream.SetLength(MemoryLimits.MAX_METADATA_JSON_BYTES + 1);

            Assert.Throws<InvalidDataException>(() => new ImgFileSystemManager(_testVersionPath, _config));
        }

        [Fact]
        public void CategoryIndex_AllImagePathsIncludesNestedSubdirectories()
        {
            string categoryPath = Path.Combine(_testVersionPath, "NestedCategory");
            string nestedPath = Path.Combine(categoryPath, "A", "B");
            Directory.CreateDirectory(nestedPath);
            CreateMockImgFile(Path.Combine(nestedPath, "Deep.img"));

            CategoryIndex index = CategoryIndex.BuildFromDirectory(categoryPath, "NestedCategory");

            Assert.Contains(Path.Combine("A", "B", "Deep.img"), index.AllImagePaths);
        }

        [Fact]
        public void MalformedCategoryIndexFallsBackToDirectoryScan()
        {
            string categoryPath = Path.Combine(_testVersionPath, "Map");
            var index = new CategoryIndex
            {
                Category = "Map",
                GeneratedAt = DateTime.UtcNow.AddMinutes(1),
                Images = [new ImageIndexEntry { Name = "evil.img", RelativePath = "..\\evil.img" }]
            };
            index.Save(Path.Combine(categoryPath, "index.json"));

            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            Assert.True(manager.CategoryExists("Map"));
        }

        [Fact]
        public void CategoryDirectoryApisRejectPathTraversal()
        {
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            Assert.Throws<InvalidOperationException>(() => manager.GetDirectory("..\\outside"));
            Assert.Throws<InvalidOperationException>(() => manager.GetSubdirectories("..\\outside").ToList());
            Assert.Throws<InvalidOperationException>(() => manager.GenerateCategoryIndex("..\\outside"));
        }

        [Fact]
        public void EnumerateFilesExcludingBackups_SkipsTopLevelAndNestedBackups()
        {
            var imageFiles = HaCreatorPaths.EnumerateFilesExcludingBackups(
                    _testVersionPath,
                    "*.img",
                    SearchOption.AllDirectories)
                .ToList();

            Assert.Contains(imageFiles, path => path.EndsWith(Path.Combine("String", "Test.img"), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(imageFiles, path => HaCreatorPaths.ContainsBackupsDirectory(path));
        }

        [Fact]
        public void GetDirectory_ExistingCategory_ReturnsVirtualDirectory()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var directory = manager.GetDirectory("String");

            // Assert
            Assert.NotNull(directory);
            Assert.IsType<VirtualWzDirectory>(directory);
        }

        [Fact]
        public void GetDirectory_NonExistingCategory_ReturnsNull()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var directory = manager.GetDirectory("NonExistent");

            // Assert
            Assert.Null(directory);
        }

        [Fact]
        public void LoadImage_NonExistingImage_ReturnsNull()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var image = manager.LoadImage("String", "NonExistent.img");

            // Assert
            Assert.Null(image);
        }

        [Fact]
        public void LoadImage_FallsBackToLegacyEncryption_WhenManifestEncryptionDoesNotParseImg()
        {
            // Arrange
            string mapImagePath = Path.Combine(_testVersionPath, "Map", "LegacyGms.img");
            using (var writerManager = new ImgFileSystemManager(_testVersionPath, _config, WzMapleVersion.GMS))
            {
                var image = new WzImage("LegacyGms.img");
                image.AddProperty(new WzStringProperty("value", "test"));

                Assert.True(writerManager.SaveImageToFile(image, mapImagePath));
            }

            using var manager = new ImgFileSystemManager(_testVersionPath, _config, WzMapleVersion.BMS);
            manager.Initialize();

            // Act
            var loadedImage = manager.LoadImage("Map", "LegacyGms.img");

            // Assert
            Assert.NotNull(loadedImage);
            Assert.Equal("test", Assert.IsType<WzStringProperty>(loadedImage["value"]).Value);
        }

        [Fact]
        public void SaveImageToFile_ReplacesExistingImageAndKeepsExternalBackup()
        {
            string imagePath = Path.Combine(_testVersionPath, "Map", "BackupTest.img");
            string backupDirectory = Path.Combine(
                HaCreatorPaths.GetBackupsPath(_testVersionPath),
                "IMG",
                Path.GetFileName(_testVersionPath),
                "Map");
            string backupVersionDirectory = Path.GetDirectoryName(backupDirectory) ?? string.Empty;

            try
            {
                using var manager = new ImgFileSystemManager(_testVersionPath, _config);
                WzImage firstImage = new("BackupTest.img");
                firstImage.AddProperty(new WzStringProperty("value", "before"));
                Assert.True(manager.SaveImageToFile(firstImage, imagePath));
                byte[] originalBytes = File.ReadAllBytes(imagePath);

                WzImage secondImage = new("BackupTest.img");
                secondImage.AddProperty(new WzStringProperty("value", "after"));
                Assert.True(manager.SaveImageToFile(secondImage, imagePath));

                string[] backupFiles = Directory.GetFiles(backupDirectory, "BackupTest.img_BAK_*.img");
                Assert.Single(backupFiles);
                Assert.Equal(originalBytes, File.ReadAllBytes(backupFiles[0]));
                Assert.NotEqual(Convert.ToBase64String(originalBytes), Convert.ToBase64String(File.ReadAllBytes(imagePath)));
                Assert.False(Path.GetFullPath(backupFiles[0]).StartsWith(
                    Path.GetFullPath(_testVersionPath) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (!string.IsNullOrEmpty(backupVersionDirectory) && Directory.Exists(backupVersionDirectory))
                    Directory.Delete(backupVersionDirectory, true);
            }
        }

        [Fact]
        public void ImageExists_NonExistingImage_ReturnsFalse()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert
            Assert.False(manager.ImageExists("String", "NonExistent.img"));
        }

        [Fact]
        public void GetStats_ReturnsValidStats()
        {
            // Arrange
            using var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act
            var stats = manager.GetStats();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.CategoryCount >= 3); // At least String, Map, Mob
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var manager = new ImgFileSystemManager(_testVersionPath, _config);
            manager.Initialize();

            // Act & Assert (should not throw)
            manager.Dispose();
            manager.Dispose();
        }
    }
}
