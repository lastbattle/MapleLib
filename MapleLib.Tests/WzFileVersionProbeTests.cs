using System;
using System.IO;
using System.Reflection;
using System.Text;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzFileVersionProbeTests
{
    [Fact]
    public void TryDecodeWithWzVersionNumber_DoesNotParseAcceptedDirectoryTwice()
    {
        const short patchVersion = 95;
        string path = Path.Combine(Path.GetTempPath(), $"maplelib-version-probe-{Guid.NewGuid():N}.wz");

        try
        {
            Create64BitFixture(path, patchVersion);
            byte[] bytes = File.ReadAllBytes(path);
            WzHeader header = ReadHeader(bytes);

            using var stream = new ThrowAfterImageHeaderStream(bytes);
            using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.GMS));
            reader.Header = header;
            reader.BaseStream.Position = header.FStart;

            using var wzFile = new WzFile(patchVersion, WzMapleVersion.GMS)
            {
                Name = "Probe.wz"
            };
            SetIs64BitWzFile(wzFile);

            MethodInfo probe = typeof(WzFile).GetMethod(
                "TryDecodeWithWZVersionNumber",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool accepted = (bool)probe.Invoke(wzFile, new object[]
            {
                reader,
                770,
                patchVersion,
                false
            })!;

            Assert.True(accepted);
            Assert.True(stream.ImageHeaderRead,
                "The probe should inspect the first image header before accepting a version.");
            Assert.Single(wzFile.WzDirectory.WzImages);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void Create64BitFixture(string path, short patchVersion)
    {
        using var wzFile = new WzFile(patchVersion, WzMapleVersion.GMS)
        {
            Name = "Probe.wz"
        };
        wzFile.WzDirectory.AddImage(new WzImage("item.img"));
        wzFile.SaveToDisk(path, override_saveAs64BitWZ: true, savingToPreferredWzVer: WzMapleVersion.GMS);
    }

    private static WzHeader ReadHeader(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        string ident = Encoding.ASCII.GetString(reader.ReadBytes(4));
        ulong fileSize = reader.ReadUInt64();
        uint fileStart = reader.ReadUInt32();
        int copyrightLength = checked((int)fileStart - 17);
        string copyright = Encoding.ASCII.GetString(reader.ReadBytes(copyrightLength));

        return new WzHeader
        {
            Ident = ident,
            FSize = fileSize,
            FStart = fileStart,
            Copyright = copyright
        };
    }

    private static void SetIs64BitWzFile(WzFile wzFile)
    {
        FieldInfo field = typeof(WzFile).GetField(
            "wz_withEncryptVersionHeader",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(wzFile, false);
    }

    private sealed class ThrowAfterImageHeaderStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public bool ImageHeaderRead { get; private set; }

        public override int ReadByte()
        {
            if (ImageHeaderRead)
                throw new InvalidOperationException("A second directory parse was attempted after the image probe.");

            int value = base.ReadByte();
            if (value == WzImage.WzImageHeaderByte_WithoutOffset)
                ImageHeaderRead = true;
            return value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (ImageHeaderRead)
                throw new InvalidOperationException("A second directory parse was attempted after the image probe.");
            return base.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            if (ImageHeaderRead)
                throw new InvalidOperationException("A second directory parse was attempted after the image probe.");
            return base.Read(buffer);
        }
    }
}
