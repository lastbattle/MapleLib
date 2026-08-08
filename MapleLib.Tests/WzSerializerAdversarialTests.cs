using System.IO;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzSerializerAdversarialTests
{
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
