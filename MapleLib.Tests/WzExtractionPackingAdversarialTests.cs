using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using XunitAssert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzExtractionPackingAdversarialTests
{
    [Fact]
    public async Task ExtractCategory_RejectsTraversalBeforeCreatingOutput()
    {
        string sourcePath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();
        string outsidePath = Path.Combine(Path.GetDirectoryName(outputPath)!,
            $"{Path.GetFileName(outputPath)}-outside");

        try
        {
            var service = new WzExtractionService();
            CategoryExtractionResult result = await service.ExtractCategoryAsync(
                sourcePath,
                outputPath,
                $"..{Path.DirectorySeparatorChar}{Path.GetFileName(outsidePath)}",
                WzMapleVersion.BMS,
                WzMapleVersion.BMS,
                is64Bit: false,
                isPreBB: false,
                resolveLinks: false,
                listWzEntries: null!,
                extractedListWzImages: null!,
                cancellationToken: CancellationToken.None);

            XunitAssert.False(result.Success);
            XunitAssert.Contains(result.Errors, error => error.Contains("escapes", StringComparison.OrdinalIgnoreCase));
            XunitAssert.False(Directory.Exists(outsidePath));
        }
        finally
        {
            DeleteDirectory(sourcePath);
            DeleteDirectory(outputPath);
            DeleteDirectory(outsidePath);
        }
    }

    [Fact]
    public async Task PackCategory_RejectsTraversalWithoutTouchingSibling()
    {
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();
        string outsidePath = Path.Combine(Path.GetDirectoryName(versionPath)!,
            $"{Path.GetFileName(versionPath)}-outside");

        try
        {
            var service = new WzPackingService();
            CategoryPackingResult result = await service.PackCategoryAsync(
                versionPath,
                outputPath,
                $"..{Path.DirectorySeparatorChar}{Path.GetFileName(outsidePath)}",
                WzMapleVersion.BMS,
                patchVersion: 1,
                saveAs64Bit: false,
                cancellationToken: CancellationToken.None);

            XunitAssert.False(result.Success);
            XunitAssert.Contains(result.Errors, error => error.Contains("escapes", StringComparison.OrdinalIgnoreCase));
            XunitAssert.False(Directory.Exists(outsidePath));
        }
        finally
        {
            DeleteDirectory(versionPath);
            DeleteDirectory(outputPath);
            DeleteDirectory(outsidePath);
        }
    }

    [Fact]
    public async Task PackCategories_ReportsFailedCategoryAtTopLevel()
    {
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();

        try
        {
            string categoryPath = Path.Combine(versionPath, "Map");
            Directory.CreateDirectory(categoryPath);
            // Deliberately malformed IMG bytes cause ProcessSingleImgFile to
            // fail; the category and aggregate results must remain failed.
            File.WriteAllBytes(Path.Combine(categoryPath, "broken.img"), [0x01, 0x02, 0x03]);
            File.WriteAllText(
                Path.Combine(versionPath, "manifest.json"),
                "{\"version\":\"v1\",\"encryption\":\"BMS\",\"patchVersion\":1}");

            var service = new WzPackingService();
            PackingResult result = await service.PackCategoriesAsync(
                versionPath,
                outputPath,
                ["Map"],
                cancellationToken: CancellationToken.None);

            XunitAssert.False(result.Success);
            XunitAssert.True(result.CategoriesPacked.TryGetValue("Map", out CategoryPackingResult? categoryResult));
            XunitAssert.NotNull(categoryResult);
            XunitAssert.False(categoryResult!.Success);
            XunitAssert.NotEmpty(categoryResult.Errors);
        }
        finally
        {
            DeleteDirectory(versionPath);
            DeleteDirectory(outputPath);
        }
    }

    [Fact]
    public async Task PackAsync_PreCancelledTokenReturnsFailedResultWithEndTime()
    {
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(versionPath, "manifest.json"),
                "{\"version\":\"v1\",\"encryption\":\"BMS\",\"patchVersion\":1}");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            PackingResult result = await new WzPackingService().PackAsync(
                versionPath,
                outputPath,
                cancellationToken: cancellation.Token);

            XunitAssert.False(result.Success);
            XunitAssert.Equal("Packing was cancelled", result.ErrorMessage);
            XunitAssert.NotEqual(default, result.EndTime);
        }
        finally
        {
            DeleteDirectory(versionPath);
            DeleteDirectory(outputPath);
        }
    }

    [Fact]
    public async Task CaseMapTraversalCannotMoveImageOutsideCategory()
    {
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();
        string categoryPath = Path.Combine(versionPath, "Map");
        Directory.CreateDirectory(categoryPath);
        string outsideImage = Path.Combine(versionPath, "outside.img");

        try
        {
            string imagePath = Path.Combine(categoryPath, "safe.img");
            File.WriteAllBytes(imagePath, [0x01, 0x02, 0x03]);
            File.WriteAllText(
                Path.Combine(categoryPath, ".imgcase.json"),
                "{\"format\":\"ImgCaseMapV1\",\"entries\":{\"safe.img\":\"../outside.img\"}}");

            CategoryPackingResult result = await new WzPackingService().PackCategoryAsync(
                versionPath,
                outputPath,
                "Map",
                WzMapleVersion.BMS,
                patchVersion: 1,
                saveAs64Bit: false,
                cancellationToken: CancellationToken.None);

            XunitAssert.False(result.Success);
            XunitAssert.True(File.Exists(imagePath));
            XunitAssert.False(File.Exists(outsideImage));
        }
        finally
        {
            DeleteDirectory(versionPath);
            DeleteDirectory(outputPath);
        }
    }

    [Fact]
    public async Task PackCategory_RejectsOversizedImgBeforeMaterializingBytes()
    {
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();
        string categoryPath = Path.Combine(versionPath, "Map");
        Directory.CreateDirectory(categoryPath);

        try
        {
            string imagePath = Path.Combine(categoryPath, "oversized.img");
            using (var stream = new FileStream(imagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.SetLength((long)MemoryLimits.MAX_WZ_PAYLOAD_BYTES + 1);
            }

            CategoryPackingResult result = await new WzPackingService().PackCategoryAsync(
                versionPath,
                outputPath,
                "Map",
                WzMapleVersion.BMS,
                patchVersion: 1,
                saveAs64Bit: false,
                cancellationToken: CancellationToken.None);

            XunitAssert.False(result.Success);
            XunitAssert.Contains(result.Errors, error => error.Contains("too large", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(versionPath);
            DeleteDirectory(outputPath);
        }
    }

    [Fact]
    public async Task ExtractionAndPackingRejectReparsePointCategory()
    {
        string sourcePath = CreateTempDirectory();
        string extractionRoot = CreateTempDirectory();
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();
        string outsidePath = CreateTempDirectory();
        string extractionLink = Path.Combine(extractionRoot, "Link");
        string packingLink = Path.Combine(versionPath, "Link");
        bool extractionLinkCreated = false;
        bool packingLinkCreated = false;

        try
        {
            try
            {
                Directory.CreateSymbolicLink(extractionLink, outsidePath);
                extractionLinkCreated = true;
                Directory.CreateSymbolicLink(packingLink, outsidePath);
                packingLinkCreated = true;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }
            catch (PlatformNotSupportedException)
            {
                return;
            }

            CategoryExtractionResult extractionResult = await new WzExtractionService().ExtractCategoryAsync(
                sourcePath,
                extractionRoot,
                "Link",
                WzMapleVersion.BMS,
                WzMapleVersion.BMS,
                is64Bit: false,
                isPreBB: false,
                resolveLinks: false,
                listWzEntries: null!,
                extractedListWzImages: null!,
                cancellationToken: CancellationToken.None);

            CategoryPackingResult packingResult = await new WzPackingService().PackCategoryAsync(
                versionPath,
                outputPath,
                "Link",
                WzMapleVersion.BMS,
                patchVersion: 1,
                saveAs64Bit: false,
                cancellationToken: CancellationToken.None);

            XunitAssert.False(extractionResult.Success);
            XunitAssert.False(packingResult.Success);
            XunitAssert.Empty(Directory.EnumerateFiles(outsidePath, "*", SearchOption.AllDirectories));
        }
        finally
        {
            if (extractionLinkCreated && Directory.Exists(extractionLink))
                Directory.Delete(extractionLink);
            if (packingLinkCreated && Directory.Exists(packingLink))
                Directory.Delete(packingLink);
            DeleteDirectory(sourcePath);
            DeleteDirectory(extractionRoot);
            DeleteDirectory(versionPath);
            DeleteDirectory(outputPath);
            DeleteDirectory(outsidePath);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"maplelib-wz-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
