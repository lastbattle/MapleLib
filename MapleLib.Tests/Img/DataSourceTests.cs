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
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests.Img
{
    public class DataSourceFactoryTests
    {
        [Fact]
        public void Create_ImgFileSystemMode_ReturnsImgFileSystemDataSource()
        {
            // Arrange
            string testPath = CreateTestVersionDirectory();

            try
            {
                // Act
                using var dataSource = DataSourceFactory.Create(
                    DataSourceMode.ImgFileSystem,
                    testPath,
                    new HaCreatorConfig());

                // Assert
                Assert.IsType<ImgFileSystemDataSource>(dataSource);
            }
            finally
            {
                CleanupTestDirectory(testPath);
            }
        }

        [Fact]
        public void Create_InvalidMode_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                DataSourceFactory.Create((DataSourceMode)999, "/path", new HaCreatorConfig());
            });
        }

        [Fact]
        public void Create_WithNullConfig_UsesDefaultConfig()
        {
            // Arrange
            string testPath = CreateTestVersionDirectory();

            try
            {
                // Act - should not throw
                using var dataSource = DataSourceFactory.Create(
                    DataSourceMode.ImgFileSystem,
                    testPath,
                    null);

                // Assert
                Assert.NotNull(dataSource);
            }
            finally
            {
                CleanupTestDirectory(testPath);
            }
        }

        #region Helper Methods

        private string CreateTestVersionDirectory()
        {
            string testPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testPath);
            Directory.CreateDirectory(Path.Combine(testPath, "String"));

            // Create manifest
            var manifest = new
            {
                version = "test",
                displayName = "Test Version",
                extractedDate = DateTime.UtcNow.ToString("o"),
                encryption = "GMS",
                is64Bit = false,
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 1, lastModified = DateTime.UtcNow.ToString("o") }
                }
            };

            string json = JsonSerializer.Serialize(manifest);
            File.WriteAllText(Path.Combine(testPath, "manifest.json"), json);

            return testPath;
        }

        private void CleanupTestDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch { }
            }
        }

        #endregion
    }

    public class HybridDataSourceTests : IDisposable
    {
        private readonly string _testPath;
        private readonly HaCreatorConfig _config;

        public HybridDataSourceTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testPath);
            Directory.CreateDirectory(Path.Combine(_testPath, "String"));

            // Create mock .img file (required for category detection)
            File.WriteAllBytes(Path.Combine(_testPath, "String", "Test.img"), Array.Empty<byte>());

            CreateTestManifest();

            _config = new HaCreatorConfig { ImgRootPath = _testPath };
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                try
                {
                    Directory.Delete(_testPath, true);
                }
                catch { }
            }
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
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 1, lastModified = DateTime.UtcNow.ToString("o") }
                }
            };

            string json = JsonSerializer.Serialize(manifest);
            File.WriteAllText(Path.Combine(_testPath, "manifest.json"), json);
        }

        [Fact]
        public void GetCategories_ReturnsCategories()
        {
            // Arrange
            using var dataSource = new HybridDataSource(_testPath, _config);

            // Act
            var categories = dataSource.GetCategories().ToList();

            // Assert - categories are stored in lowercase
            Assert.Contains(categories, c => c.Equals("string", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CategoryExists_ExistingCategory_ReturnsTrue()
        {
            // Arrange
            using var dataSource = new HybridDataSource(_testPath, _config);

            // Act & Assert
            Assert.True(dataSource.CategoryExists("String"));
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var dataSource = new HybridDataSource(_testPath, _config);

            // Act & Assert
            dataSource.Dispose();
            dataSource.Dispose();
        }
    }
}
