/*  MapleLib.Tests - Unit tests for MapleLib
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MapleLib.Img;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests.Img
{
    public class VersionManagerTests : IDisposable
    {
        private readonly string _testRootPath;
        private readonly VersionManager _versionManager;

        public VersionManagerTests()
        {
            // Create a temporary test directory
            _testRootPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testRootPath);
            _versionManager = new VersionManager(_testRootPath);
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testRootPath))
            {
                try
                {
                    Directory.Delete(_testRootPath, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        [Fact]
        public void Constructor_CreatesRootDirectory()
        {
            // Arrange
            string newPath = Path.Combine(Path.GetTempPath(), $"MapleLibTests_{Guid.NewGuid():N}");

            try
            {
                // Act
                var manager = new VersionManager(newPath);

                // Assert
                Assert.True(Directory.Exists(newPath));
            }
            finally
            {
                if (Directory.Exists(newPath))
                    Directory.Delete(newPath, true);
            }
        }

        [Fact]
        public void ScanVersions_EmptyDirectory_ReturnsEmptyList()
        {
            // Act
            _versionManager.ScanVersions();

            // Assert
            Assert.Empty(_versionManager.AvailableVersions);
            Assert.Equal(0, _versionManager.VersionCount);
        }

        [Fact]
        public void ScanVersions_WithValidVersion_FindsVersion()
        {
            // Arrange
            string versionPath = Path.Combine(_testRootPath, "v83");
            Directory.CreateDirectory(versionPath);
            CreateTestManifest(versionPath, "v83", "GMS v83");

            // Act
            _versionManager.ScanVersions();

            // Assert
            Assert.Single(_versionManager.AvailableVersions);
            Assert.Equal("v83", _versionManager.AvailableVersions[0].Version);
            Assert.Equal("GMS v83", _versionManager.AvailableVersions[0].DisplayName);
        }

        [Fact]
        public void ScanVersions_WithMultipleVersions_FindsAll()
        {
            // Arrange
            CreateTestVersion("v55", "Old MapleStory");
            CreateTestVersion("v83", "GMS v83");
            CreateTestVersion("v176", "Modern MS");

            // Act
            _versionManager.ScanVersions();

            // Assert
            Assert.Equal(3, _versionManager.VersionCount);
        }

        [Fact]
        public void ScanVersions_DirectoryWithoutManifest_CreatesBasicVersion()
        {
            // Arrange
            string versionPath = Path.Combine(_testRootPath, "noManifest");
            Directory.CreateDirectory(versionPath);
            Directory.CreateDirectory(Path.Combine(versionPath, "String"));

            // Act
            _versionManager.ScanVersions();

            // Assert
            Assert.Single(_versionManager.AvailableVersions);
            Assert.Equal("noManifest", _versionManager.AvailableVersions[0].Version);
        }

        [Fact]
        public void GetVersion_ExistingVersion_ReturnsVersion()
        {
            // Arrange
            CreateTestVersion("v83", "GMS v83");
            _versionManager.ScanVersions();

            // Act
            var version = _versionManager.GetVersion("v83");

            // Assert
            Assert.NotNull(version);
            Assert.Equal("v83", version.Version);
        }

        [Fact]
        public void GetVersion_NonExistingVersion_ReturnsNull()
        {
            // Arrange
            _versionManager.ScanVersions();

            // Act
            var version = _versionManager.GetVersion("nonexistent");

            // Assert
            Assert.Null(version);
        }

        [Fact]
        public void VersionExists_ExistingVersion_ReturnsTrue()
        {
            // Arrange
            CreateTestVersion("v83", "GMS v83");
            _versionManager.ScanVersions();

            // Act & Assert
            Assert.True(_versionManager.VersionExists("v83"));
        }

        [Fact]
        public void VersionExists_NonExistingVersion_ReturnsFalse()
        {
            // Arrange
            _versionManager.ScanVersions();

            // Act & Assert
            Assert.False(_versionManager.VersionExists("nonexistent"));
        }

        [Fact]
        public void DeleteVersion_ExistingVersion_DeletesDirectory()
        {
            // Arrange
            string versionPath = CreateTestVersion("v83", "GMS v83");
            _versionManager.ScanVersions();

            // Act
            bool result = _versionManager.DeleteVersion("v83");

            // Assert
            Assert.True(result);
            Assert.False(Directory.Exists(versionPath));
        }

        [Fact]
        public void DeleteVersion_NonExistingVersion_ReturnsFalse()
        {
            // Arrange
            _versionManager.ScanVersions();

            // Act
            bool result = _versionManager.DeleteVersion("nonexistent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RenameVersion_RejectsTraversalWithoutMovingVersionOutsideRoot()
        {
            string originalPath = CreateTestVersion("v83", "GMS v83");
            _versionManager.ScanVersions();
            string escapedName = $"MapleLibEscaped_{Guid.NewGuid():N}";
            string escapedPath = Path.Combine(Directory.GetParent(_testRootPath)!.FullName, escapedName);

            try
            {
                bool result = _versionManager.RenameVersion(
                    "v83",
                    Path.Combine("..", escapedName),
                    "escaped",
                    out VersionInfo? renamed);

                Assert.False(result);
                Assert.Null(renamed);
                Assert.True(Directory.Exists(originalPath));
                Assert.False(Directory.Exists(escapedPath));
            }
            finally
            {
                if (Directory.Exists(escapedPath))
                    Directory.Delete(escapedPath, recursive: true);
            }
        }

        [Fact]
        public void RenameVersion_ValidNameMovesDirectoryAndUpdatesManifest()
        {
            string originalPath = CreateTestVersion("v83", "GMS v83");
            _versionManager.ScanVersions();

            bool result = _versionManager.RenameVersion("v83", "v84", "GMS v84", out VersionInfo? renamed);

            string renamedPath = Path.Combine(_testRootPath, "v84");
            Assert.True(result);
            Assert.NotNull(renamed);
            Assert.False(Directory.Exists(originalPath));
            Assert.True(Directory.Exists(renamedPath));
            Assert.Equal("v84", renamed.Version);
            Assert.Equal(renamedPath, renamed.DirectoryPath);
            Assert.True(File.Exists(Path.Combine(renamedPath, "manifest.json")));
        }

        [Fact]
        public void AddExternalVersion_ValidPath_AddsToList()
        {
            // Arrange
            string externalPath = Path.Combine(Path.GetTempPath(), $"External_{Guid.NewGuid():N}");
            Directory.CreateDirectory(externalPath);
            CreateTestManifest(externalPath, "external", "External Version");

            try
            {
                // Act
                var version = _versionManager.AddExternalVersion(externalPath);

                // Assert
                Assert.NotNull(version);
                Assert.True(version.IsExternal);
                Assert.Equal(externalPath, version.DirectoryPath);
            }
            finally
            {
                if (Directory.Exists(externalPath))
                    Directory.Delete(externalPath, true);
            }
        }

        [Fact]
        public void Refresh_UpdatesVersionList()
        {
            // Arrange
            _versionManager.ScanVersions();
            Assert.Empty(_versionManager.AvailableVersions);

            // Add a version after initial scan
            CreateTestVersion("v83", "GMS v83");

            // Act
            _versionManager.Refresh();

            // Assert
            Assert.Single(_versionManager.AvailableVersions);
        }

        [Fact]
        public void AvailableVersions_ReturnsStableSnapshot()
        {
            CreateTestVersion("v83", "GMS v83");

            var firstSnapshot = _versionManager.AvailableVersions;
            CreateTestVersion("v84", "GMS v84");

            _versionManager.Refresh();

            // A read-only wrapper over the backing list would reflect the
            // refresh and expose callers to concurrent list mutations.
            Assert.Single(firstSnapshot);
            Assert.Equal("v83", firstSnapshot[0].Version);
            Assert.Equal(2, _versionManager.AvailableVersions.Count);
        }

        [Fact]
        public async Task AddExternalVersion_ConcurrentCalls_PublishesOnlyOneVersion()
        {
            string externalPath = Path.Combine(Path.GetTempPath(), $"External_{Guid.NewGuid():N}");
            Directory.CreateDirectory(externalPath);
            CreateTestManifest(externalPath, "external", "External Version");

            try
            {
                using var start = new ManualResetEventSlim(false);
                var tasks = Enumerable.Range(0, 32)
                    .Select(_ => Task.Run(() =>
                    {
                        start.Wait();
                        return _versionManager.AddExternalVersion(externalPath);
                    }))
                    .ToArray();

                start.Set();
                VersionInfo[] results = await Task.WhenAll(tasks);

                Assert.Equal(1, results.Count(v => v != null));
                Assert.Single(_versionManager.AvailableVersions.Where(v =>
                    v.DirectoryPath.Equals(externalPath, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                if (Directory.Exists(externalPath))
                    Directory.Delete(externalPath, true);
            }
        }

        [Fact]
        public async Task HotSwap_CallbacksAndRefreshes_AreSafeToRunConcurrently()
        {
            _versionManager.ScanVersions();
            string versionPath = CreateValidTestVersion("v84", "GMS v84");
            _versionManager.EnableHotSwap(enable: true, debounceMs: 0);

            try
            {
                var watcherField = typeof(VersionManager).GetField(
                    "_watcherService",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var watcher = watcherField?.GetValue(_versionManager);
                Assert.NotNull(watcher);

                var callback = typeof(VersionManager).GetMethod(
                    "OnVersionDirectoryChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(callback);

                using var start = new ManualResetEventSlim(false);
                var tasks = Enumerable.Range(0, 24)
                    .Select(index => Task.Run(() =>
                    {
                        start.Wait();
                        if ((index & 1) == 0)
                        {
                            callback!.Invoke(
                                _versionManager,
                                new object[]
                                {
                                    watcher,
                                    new VersionDirectoryChangedEventArgs(
                                        versionPath,
                                        WatcherChangeTypes.Created)
                                });
                        }
                        else
                        {
                            _versionManager.Refresh();
                        }
                    }))
                    .ToArray();

                start.Set();
                await Task.WhenAll(tasks);

                Assert.Single(_versionManager.AvailableVersions.Where(v =>
                    v.DirectoryPath.Equals(versionPath, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                _versionManager.EnableHotSwap(false);
            }
        }

        [Fact]
        public async Task HotSwap_EnableDisable_RacingCallsLeaveNoActiveWatcher()
        {
            using var start = new ManualResetEventSlim(false);
            var tasks = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    _versionManager.EnableHotSwap(enable: true, debounceMs: 0);
                    _versionManager.EnableHotSwap(enable: false);
                }))
                .ToArray();

            start.Set();
            await Task.WhenAll(tasks);

            _versionManager.EnableHotSwap(false);
            Assert.False(_versionManager.HotSwapEnabled);
        }

        #region Helper Methods

        private string CreateTestVersion(string versionName, string displayName)
        {
            string versionPath = Path.Combine(_testRootPath, versionName);
            Directory.CreateDirectory(versionPath);
            CreateTestManifest(versionPath, versionName, displayName);
            return versionPath;
        }

        private string CreateValidTestVersion(string versionName, string displayName)
        {
            string versionPath = CreateTestVersion(versionName, displayName);
            string stringPath = Path.Combine(versionPath, "String");
            string mapPath = Path.Combine(versionPath, "Map");
            Directory.CreateDirectory(stringPath);
            Directory.CreateDirectory(mapPath);
            File.WriteAllBytes(Path.Combine(stringPath, "Map.img"), new byte[] { 0 });
            File.WriteAllBytes(Path.Combine(mapPath, "Map.img"), new byte[] { 0 });
            return versionPath;
        }

        private void CreateTestManifest(string versionPath, string version, string displayName)
        {
            var manifest = new
            {
                version = version,
                displayName = displayName,
                extractedDate = DateTime.UtcNow.ToString("o"),
                encryption = "GMS",
                is64Bit = false,
                isPreBB = false,
                categories = new Dictionary<string, object>
                {
                    ["String"] = new { fileCount = 8, lastModified = DateTime.UtcNow.ToString("o") },
                    ["Map"] = new { fileCount = 100, lastModified = DateTime.UtcNow.ToString("o") }
                },
                features = new { }
            };

            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(versionPath, "manifest.json"), json);
        }

        #endregion
    }
}
