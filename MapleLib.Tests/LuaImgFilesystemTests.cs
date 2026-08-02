using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using XunitAssert = Xunit.Assert;

namespace MapleLib.Tests;

public class LuaImgFilesystemTests
{
    [Fact]
    public void EnumeratePackableImageFiles_PrefersLuaTextOverLegacyBinary()
    {
        string rootPath = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(rootPath, "BattleScene.lua"), "한글");
            File.WriteAllBytes(Path.Combine(rootPath, "BattleScene.lua.img"), [0x01]);
            File.WriteAllBytes(Path.Combine(rootPath, "Other.img"), [0x02]);

            var names = WzPackingService.EnumeratePackableImageFiles(rootPath)
                .Select(Path.GetFileName)
                .ToList();

            XunitAssert.Contains("BattleScene.lua", names);
            XunitAssert.DoesNotContain("BattleScene.lua.img", names);
            XunitAssert.Contains("Other.img", names);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void EnumeratePackableImageFiles_ExcludesLuaFilesInBackupsDirectories()
    {
        string rootPath = CreateTempDirectory();
        try
        {
            string scriptPath = Path.Combine(rootPath, "Script");
            string topLevelBackupPath = Path.Combine(rootPath, HaCreatorPaths.BackupsFolderName);
            string nestedBackupPath = Path.Combine(scriptPath, HaCreatorPaths.BackupsFolderName);
            Directory.CreateDirectory(scriptPath);
            Directory.CreateDirectory(topLevelBackupPath);
            Directory.CreateDirectory(nestedBackupPath);

            File.WriteAllText(Path.Combine(scriptPath, "BattleScene.lua"), "return 'current'");
            File.WriteAllText(Path.Combine(topLevelBackupPath, "TopLevel.lua"), "return 'backup'");
            File.WriteAllText(Path.Combine(nestedBackupPath, "Nested.lua"), "return 'backup'");

            var relativePaths = WzPackingService.EnumeratePackableImageFiles(rootPath)
                .Select(path => Path.GetRelativePath(rootPath, path))
                .ToList();

            XunitAssert.Single(relativePaths);
            XunitAssert.Equal(Path.Combine("Script", "BattleScene.lua"), relativePaths[0]);
            XunitAssert.DoesNotContain(relativePaths, HaCreatorPaths.ContainsBackupsDirectory);
            XunitAssert.Empty(WzPackingService.EnumeratePackableImageFiles(topLevelBackupPath));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PackCategory_EncodesLuaTextAsWzLuaProperty()
    {
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();
        const string script = "-- 테스트\nreturn '한글'\n";

        try
        {
            string categoryPath = Path.Combine(versionPath, "Etc", "Script");
            Directory.CreateDirectory(categoryPath);
            File.WriteAllText(Path.Combine(categoryPath, "BattleScene.lua"), script, new UTF8Encoding(false));

            var service = new WzPackingService();
            CategoryPackingResult result = await service.PackCategoryAsync(
                    versionPath,
                    outputPath,
                    "Etc",
                    WzMapleVersion.BMS,
                    patchVersion: 95,
                    saveAs64Bit: false,
                    cancellationToken: CancellationToken.None);

            XunitAssert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

            string wzPath = Path.Combine(outputPath, "Etc.wz");
            using var wzFile = new WzFile(wzPath, 95, WzMapleVersion.BMS);
            XunitAssert.Equal(WzFileParseStatus.Success, wzFile.ParseWzFile());

            WzDirectory scriptDirectory = wzFile.WzDirectory.WzDirectories.Single();
            XunitAssert.Equal("Script", scriptDirectory.Name);
            WzImage image = scriptDirectory.WzImages.Single();
            XunitAssert.Equal("BattleScene.lua", image.Name);
            WzLuaProperty luaProperty = XunitAssert.IsType<WzLuaProperty>(image.WzProperties.Single());
            XunitAssert.Equal(script, luaProperty.GetString());
        }
        finally
        {
            Directory.Delete(versionPath, recursive: true);
            Directory.Delete(outputPath, recursive: true);
        }
    }

    [Fact]
    public async Task PackCategory_ConvertsUtf16LuaTextToUtf8Payload()
    {
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();
        const string script = "return '한글'\n";

        try
        {
            string categoryPath = Path.Combine(versionPath, "Etc");
            Directory.CreateDirectory(categoryPath);
            File.WriteAllText(
                Path.Combine(categoryPath, "BattleScene.lua"),
                script,
                new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

            var service = new WzPackingService();
            CategoryPackingResult result = await service.PackCategoryAsync(
                versionPath,
                outputPath,
                "Etc",
                WzMapleVersion.BMS,
                patchVersion: 95,
                saveAs64Bit: false,
                cancellationToken: CancellationToken.None);

            XunitAssert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

            using var wzFile = new WzFile(Path.Combine(outputPath, "Etc.wz"), 95, WzMapleVersion.BMS);
            XunitAssert.Equal(WzFileParseStatus.Success, wzFile.ParseWzFile());
            WzLuaProperty luaProperty = XunitAssert.IsType<WzLuaProperty>(
                wzFile.WzDirectory.WzImages.Single().WzProperties.Single());
            XunitAssert.Equal(script, luaProperty.GetString());
        }
        finally
        {
            Directory.Delete(versionPath, recursive: true);
            Directory.Delete(outputPath, recursive: true);
        }
    }

    [Fact]
    public async Task PackCategory_DoesNotRenameLuaTextToLegacyLuaImg()
    {
        string versionPath = CreateTempDirectory();
        string outputPath = CreateTempDirectory();
        const string script = "return '한글'\n";

        try
        {
            string categoryPath = Path.Combine(versionPath, "Etc", "Script");
            Directory.CreateDirectory(categoryPath);
            File.WriteAllText(Path.Combine(categoryPath, "BattleScene.lua"), script, new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(versionPath, "Etc", ".imgcase.json"),
                "{\"Format\":\"ImgCaseMapV1\",\"Entries\":{\"script/battlescene.lua.img\":\"Script/BattleScene.lua.img\"}}",
                Encoding.UTF8);

            var service = new WzPackingService();
            CategoryPackingResult result = await service.PackCategoryAsync(
                versionPath,
                outputPath,
                "Etc",
                WzMapleVersion.BMS,
                patchVersion: 95,
                saveAs64Bit: false,
                cancellationToken: CancellationToken.None);

            XunitAssert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
            XunitAssert.False(File.Exists(Path.Combine(categoryPath, "BattleScene.lua.img")));
            using var wzFile = new WzFile(Path.Combine(outputPath, "Etc.wz"), 95, WzMapleVersion.BMS);
            XunitAssert.Equal(WzFileParseStatus.Success, wzFile.ParseWzFile());
            WzImage image = wzFile.WzDirectory.WzDirectories.Single().WzImages.Single();
            XunitAssert.Equal("BattleScene.lua", image.Name);
            XunitAssert.Equal(script, image.WzProperties.Single().GetString());
        }
        finally
        {
            Directory.Delete(versionPath, recursive: true);
            Directory.Delete(outputPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractCategory_WritesLuaImageAsUtf8Text()
    {
        string sourceRoot = CreateTempDirectory();
        string outputRoot = CreateTempDirectory();
        const string script = "-- 추출 테스트\nreturn '한글'\n";

        try
        {
            string sourcePath = Path.Combine(sourceRoot, "Etc.wz");
            using (var sourceWz = new WzFile(95, WzMapleVersion.BMS) { Name = "Etc.wz" })
            {
                var image = new WzImage("BattleScene.lua");
                var luaProperty = new WzLuaProperty("Script", Array.Empty<byte>());
                luaProperty.SetString(script);
                image.WzProperties.Add(luaProperty);
                sourceWz.WzDirectory.AddImage(image);
                sourceWz.SaveToDisk(sourcePath, false, WzMapleVersion.BMS);
            }

            var service = new WzExtractionService();
            CategoryExtractionResult result = await service.ExtractCategoryAsync(
                sourceRoot,
                outputRoot,
                "Etc",
                WzMapleVersion.BMS,
                WzMapleVersion.BMS,
                is64Bit: false,
                isPreBB: false,
                resolveLinks: false,
                listWzEntries: new HashSet<string>(),
                extractedListWzImages: new ConcurrentDictionary<string, byte>(),
                cancellationToken: CancellationToken.None);

            XunitAssert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
            string scriptPath = Path.Combine(outputRoot, "Etc", "BattleScene.lua");
            XunitAssert.True(File.Exists(scriptPath));
            XunitAssert.Equal(script, File.ReadAllText(scriptPath, Encoding.UTF8));
            XunitAssert.False(File.Exists(scriptPath + ".img"));
        }
        finally
        {
            Directory.Delete(sourceRoot, recursive: true);
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"harepacker-lua-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
