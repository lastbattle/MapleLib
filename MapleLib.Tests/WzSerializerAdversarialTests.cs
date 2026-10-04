using System.IO;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzSerializerAdversarialTests
{
    [Theory]
    [InlineData(0, 18, null)]
    [InlineData(600000, 600034, "4E95C25558169415FBF28B5131A57589320F0833F0270BB43CDA4DE195D00301")]
    [InlineData(1100000, 1100034, "1B7B52FA3211174F64B0159F4E54B20BC8A93021F24EDC1218F4D9BA64B938B3")]
    public void ImgFileSerializationPreservesPartialBytesAndReleasesFileOnPropertyFailure(
        int paddingLength, int expectedLength, string? expectedHash)
    {
        string path = Path.Combine(Path.GetTempPath(), $"maple-img-partial-{Guid.NewGuid():N}.img");
        using var image = new WzImage("invalid.img") { BlockSize = 123 };
        if (paddingLength > 0)
            image.AddProperty(new WzStringProperty("padding", new string('A', paddingLength)));
        // A PNG node can only be written through its owning canvas.
        image.AddProperty(new WzPngProperty());
        byte[] expected = [0x73, 0xF8, 0xFA, 0xD9, 0xC3, 0xDD, 0xCB, 0xDD, 0xC4, 0xC8,
            0, 0, 1, 0, 0xFD, 0xFA, 0xE5, 0xEB];
        try
        {
            Assert.Throws<NotImplementedException>(() =>
                new WzImgSerializer(new byte[4]).SerializeImage(image, path));
            byte[] actual = File.ReadAllBytes(path);
            Assert.Equal(expectedLength, actual.Length);
            if (expectedHash is null)
                Assert.Equal(expected, actual);
            else
                // Independent Python wire vectors include the masked ASCII
                // payload, its long-length marker, and the failing PNG name.
                Assert.Equal(expectedHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual)));
            Assert.Equal(123, image.BlockSize);
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImgFileSerializationSuppliesFileContextToCustomPropertyWriter()
    {
        string path = Path.Combine(Path.GetTempPath(), $"maple-img-context-{Guid.NewGuid():N}.img");
        using var image = new WzImage("extension.img");
        var property = new FileContextIntProperty("id", 314);
        var group = new WzSubProperty("nested");
        group.AddProperty(property);
        image.AddProperty(group);
        try
        {
            new WzImgSerializer(new byte[4]).SerializeImage(image, path);
            Assert.Equal(Path.GetFullPath(path), property.OutputPath);
            var deserializer = new WzImgDeserializer(freeResources: true);
            using var decoded = deserializer.WzImageFromIMGFile(path, new byte[4], "decoded.img", out bool parsed);
            Assert.True(parsed);
            Assert.Equal(314, Assert.IsType<WzIntProperty>(decoded.GetFromPath("nested/id")).Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class FileContextIntProperty(string name, int value) : WzIntProperty(name, value)
    {
        public string? OutputPath { get; private set; }

        public override void WriteValue(WzBinaryWriter writer)
        {
            if (writer.BaseStream is not FileStream file)
                throw new InvalidOperationException("The extension requires its file output context.");
            OutputPath = file.Name;
            base.WriteValue(writer);
        }
    }

    [Theory]
    [InlineData(WzMapleVersion.BMS)]
    [InlineData(WzMapleVersion.GMS)]
    public void ImgFileSerializationClosesCompleteNestedStringImage(WzMapleVersion version)
    {
        string path = Path.Combine(Path.GetTempPath(), $"maple-img-wire-{Guid.NewGuid():N}.img");
        byte[] iv = WzTool.GetIvByMapleVersion(version);
        using var image = new WzImage("strings.img");
        const string rootText = "ASCII and 漢字 😀\uFFFF";
        string longText = new string('漢', 127) + "😀";
        image.AddProperty(new WzStringProperty("description", rootText));
        var group = new WzSubProperty("nested");
        group.AddProperty(new WzStringProperty("description", longText));
        group.AddProperty(new WzIntProperty("value", -12345));
        image.AddProperty(group);
        try
        {
            new WzImgSerializer(iv).SerializeImage(image, path);
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            var deserializer = new WzImgDeserializer(freeResources: true);
            using WzImage decoded = deserializer.WzImageFromIMGFile(path, iv, "decoded.img", out bool parsed);
            Assert.True(parsed);
            Assert.Equal(rootText, Assert.IsType<WzStringProperty>(decoded["description"]).Value);
            WzSubProperty decodedGroup = Assert.IsType<WzSubProperty>(decoded["nested"]);
            Assert.Equal(longText, Assert.IsType<WzStringProperty>(decodedGroup["description"]).Value);
            Assert.Equal(-12345, Assert.IsType<WzIntProperty>(decodedGroup["value"]).Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("con.multi.img", "_con.multi.img")]
    [InlineData("LPT9.img", "_LPT9.img")]
    [InlineData("COM0.img", "COM0.img")]
    [InlineData("COM10.img", "COM10.img")]
    [InlineData(".CON.img", ".CON.img")]
    [InlineData("normal.multi.img. ", "normal.multi.img")]
    [InlineData("A:U?X.img", "_AUX.img")]
    [InlineData("", "_")]
    public void EscapeInvalidFilePathNames_PreservesFirstComponentDeviceRules(string value, string expected)
    {
        Assert.Equal(expected, ProgressingWzSerializer.EscapeInvalidFilePathNames(value));
    }

    [Fact]
    public void EscapeInvalidFilePathNames_RemovesPlatformInvalidCharactersAndPreservesUnicode()
    {
        string invalid = ":" + new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        Assert.Equal("Unicode_日本語", ProgressingWzSerializer.EscapeInvalidFilePathNames("Unicode_" + invalid + "日本語. "));
        Assert.Equal("_", ProgressingWzSerializer.EscapeInvalidFilePathNames(invalid + " ."));

        string longName = new string('a', 300) + invalid + "日本語.img. ";
        Assert.Equal(new string('a', 300) + "日本語.img", ProgressingWzSerializer.EscapeInvalidFilePathNames(longName));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("CON")]
    [InlineData("name. ")]
    public void EscapeInvalidFilePathNames_ProducesSafeSingleComponent(string value)
    {
        string escaped = ProgressingWzSerializer.EscapeInvalidFilePathNames(value);

        Assert.NotEqual(".", escaped);
        Assert.NotEqual("..", escaped);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, escaped);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, escaped);
        Assert.NotEqual("CON", escaped, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PngMp3Serializer_DoesNotUseRawChildNamesOrEscapeOutputRoot()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string output = Path.Combine(root, "output");
        string outside = Path.Combine(root, "escaped.img");
        Directory.CreateDirectory(output);

        try
        {
            var safeDirectory = new WzDirectory("SafeDir");
            safeDirectory.AddImage(new WzImage("safe.img"));
            new WzPngMp3Serializer().SerializeDirectory(safeDirectory, output);

            Assert.True(Directory.Exists(Path.Combine(output, "SafeDir", "safe.img")));
            Assert.False(Directory.Exists(Path.Combine(output, "SafeDir", "safe.img", "safe.img")));

            var maliciousDirectory = new WzDirectory("..");
            maliciousDirectory.AddImage(new WzImage("escaped.img"));
            new WzPngMp3Serializer().SerializeDirectory(maliciousDirectory, output);

            Assert.False(Directory.Exists(outside));
            Assert.True(Directory.Exists(Path.Combine(output, "_", "escaped.img")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClassicXmlSerializer_IntPropertyPreservesEscapingAndFormatting()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string output = Path.Combine(root, "output.xml");
        var image = new WzImage("image<&.img");
        image.AddProperty(new WzIntProperty("value<&\"'", int.MinValue));

        try
        {
            new WzClassicXmlSerializer(0, LineBreak.None, exportbase64: false)
                .SerializeImage(image, output);

            string serialized = File.ReadAllText(output);
            Assert.Equal(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><imgdir name=\"image&lt;&amp;.img\"><int name=\"value&lt;&amp;&quot;&apos;\" value=\"-2147483648\"/></imgdir>",
                serialized);
        }
        finally
        {
            image.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClassicXmlSerializer_ScalarPropertiesPreserveGoldenOutput()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string output = Path.Combine(root, "output.xml");
        var image = new WzImage("scalars.img");
        image.AddProperty(new WzShortProperty("short", short.MinValue));
        image.AddProperty(new WzLongProperty("long", long.MaxValue));
        image.AddProperty(new WzDoubleProperty("double", 1.5));
        image.AddProperty(new WzVectorProperty("vector", -2, 3));

        try
        {
            new WzClassicXmlSerializer(0, LineBreak.None, exportbase64: false)
                .SerializeImage(image, output);

            Assert.Equal(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><imgdir name=\"scalars.img\"><short name=\"short\" value=\"-32768\"/><long name=\"long\" value=\"9223372036854775807\"/><double name=\"double\" value=\"1.5\"/><vector name=\"vector\" x=\"-2\" y=\"3\"/></imgdir>",
                File.ReadAllText(output));
        }
        finally
        {
            image.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WzFileExporter_ReportsMalformedInputAndSkipsSerializer()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string input = Path.Combine(root, "malformed.wz");
        string output = Path.Combine(root, "output");
        // PKG1 avoids the list-file fast path; the zero FStart is malformed.
        File.WriteAllBytes(input, [0x50, 0x4B, 0x47, 0x31, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        var serializer = new CountingFileSerializer();

        try
        {
            bool result = WzFileExporter.RunWzFilesExtraction(
                [input], output, WzMapleVersion.BMS, serializer);

            Assert.False(result);
            Assert.Equal(0, serializer.SerializeCount);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NxSerializer_RejectsUtf8StringLengthOverflow()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string output = Path.Combine(root, "out");
        Directory.CreateDirectory(output);
        try
        {
            using var file = new WzFile(1, WzMapleVersion.BMS) { Name = "test.wz" };
            file.WzDirectory.AddImage(new WzImage(new string('x', ushort.MaxValue + 1)));

            Assert.Throws<InvalidDataException>(() => new WzToNxSerializer().SerializeFile(file, output));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NxSerializer_RejectsNodeChildCountOverflow()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string output = Path.Combine(root, "out");
        Directory.CreateDirectory(output);
        try
        {
            using var file = new WzFile(1, WzMapleVersion.BMS) { Name = "test.wz" };
            foreach (int index in Enumerable.Range(0, ushort.MaxValue + 1))
                file.WzDirectory.AddImage(new WzImage($"{index}.img"));

            Assert.Throws<InvalidDataException>(() => new WzToNxSerializer().SerializeFile(file, output));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CountingFileSerializer : IWzFileSerializer
    {
        public int SerializeCount { get; private set; }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeCount++;
        }
    }
}
