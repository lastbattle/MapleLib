/*  MapleLib.Tests - Unit tests for MapleLib
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System.IO;
using MapleLib.Img;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests.Img
{
    public class HaCreatorConfigTests : IDisposable
    {
        private readonly string _testConfigPath;

        public HaCreatorConfigTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}", "config.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_testConfigPath)!);
        }

        public void Dispose()
        {
            string? dir = Path.GetDirectoryName(_testConfigPath);
            if (dir != null && Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch { }
            }
        }

        [Fact]
        public void Load_NonExistingFile_ReturnsDefaultConfig()
        {
            // Act
            var config = HaCreatorConfig.Load("/nonexistent/path/config.json");

            // Assert
            Assert.NotNull(config);
            Assert.Equal(DataSourceMode.ImgFileSystem, config.DataSourceMode);
        }

        [Fact]
        public void EnsureDirectoriesExist_CreatesDirectories()
        {
            // Arrange
            string testRoot = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            var config = new HaCreatorConfig
            {
                ImgRootPath = testRoot
            };

            try
            {
                // Act
                config.EnsureDirectoriesExist();

                // Assert
                Assert.True(Directory.Exists(testRoot));
                Assert.True(Directory.Exists(config.VersionsPath));
                Assert.True(Directory.Exists(config.CustomPath));
            }
            finally
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, true);
            }
        }

        [Fact]
        public void VersionsPath_ReturnsCorrectPath()
        {
            // Arrange
            var config = new HaCreatorConfig
            {
                ImgRootPath = @"C:\Test\Data"
            };

            // Act
            string versionsPath = config.VersionsPath;

            // Assert
            Assert.Equal(Path.Combine(@"C:\Test\Data", "versions"), versionsPath);
        }

        [Fact]
        public void CustomPath_ReturnsCorrectPath()
        {
            // Arrange
            var config = new HaCreatorConfig
            {
                ImgRootPath = @"C:\Test\Data"
            };

            // Act
            string customPath = config.CustomPath;

            // Assert
            Assert.Equal(Path.Combine(@"C:\Test\Data", "custom"), customPath);
        }

        [Fact]
        public void SaveAndLoad_PreservesAllSettings()
        {
            // Arrange
            var config = new HaCreatorConfig
            {
                DataSourceMode = DataSourceMode.Hybrid,
                LastUsedVersion = "gms_v230",
                ImgRootPath = @"C:\CustomPath"
            };
            config.Cache.MaxMemoryCacheMB = 1024;
            config.Cache.MaxCachedImages = 2000;
            config.Legacy.WzFilePath = @"D:\MapleStory";
            config.Legacy.AllowWzFallback = true;
            config.Extraction.ParallelThreads = 8;
            config.AdditionalVersionPaths.Add(@"E:\External");

            // Act
            config.Save(_testConfigPath);
            var loaded = HaCreatorConfig.Load(_testConfigPath);

            // Assert
            Assert.Equal(DataSourceMode.Hybrid, loaded.DataSourceMode);
            Assert.Equal("gms_v230", loaded.LastUsedVersion);
            Assert.Equal(1024, loaded.Cache.MaxMemoryCacheMB);
            Assert.Equal(2000, loaded.Cache.MaxCachedImages);
            Assert.Equal(@"D:\MapleStory", loaded.Legacy.WzFilePath);
            Assert.True(loaded.Legacy.AllowWzFallback);
            Assert.Equal(8, loaded.Extraction.ParallelThreads);
            Assert.Contains(@"E:\External", loaded.AdditionalVersionPaths);
        }
    }
}
