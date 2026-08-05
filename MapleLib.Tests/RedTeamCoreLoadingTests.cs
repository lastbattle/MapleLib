using System;
using System.IO;
using System.Text;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class RedTeamCoreLoadingTests
{
    [Fact]
    public void WzFile_RejectsHeaderFStartBeforeReadingCopyright()
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] header = Concat(
                Encoding.ASCII.GetBytes("PKG1"),
                BitConverter.GetBytes((ulong)64),
                BitConverter.GetBytes((uint)0),
                new byte[16]);
            File.WriteAllBytes(path, header);

            using var wzFile = new WzFile(path, (short)115, WzMapleVersion.BMS);
            Assert.Throws<InvalidDataException>(() => wzFile.ParseWzFile());

            // Parse failure must release the stream even before Dispose is called.
            File.Delete(path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void WzBinaryReader_RejectsStringLengthBeyondRemainingData()
    {
        using var stream = new MemoryStream(Concat(
            [unchecked((byte)sbyte.MinValue)],
            BitConverter.GetBytes(int.MaxValue)));
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));

        Assert.Throws<InvalidDataException>(() => reader.ReadString());
    }

    [Fact]
    public void ListFileParser_RejectsLengthBombAndKeepsEmptyFinalEntrySafe()
    {
        string bombPath = Path.GetTempFileName();
        string emptyPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(bombPath, Concat(BitConverter.GetBytes(int.MaxValue), [0x41, 0x00]));
            Assert.Throws<InvalidDataException>(() => ListFileParser.ParseListFile(bombPath, WzMapleVersion.BMS));

            // len=0 followed by the encrypted null terminator decodes to an
            // empty final entry; parsing should return it unchanged.
            File.WriteAllBytes(emptyPath, new byte[sizeof(int) + sizeof(ushort)]);
            var entries = ListFileParser.ParseListFile(emptyPath, WzMapleVersion.BMS);
            Assert.Single(entries);
            Assert.Equal(string.Empty, entries[0]);
        }
        finally
        {
            if (File.Exists(bombPath))
                File.Delete(bombPath);
            if (File.Exists(emptyPath))
                File.Delete(emptyPath);
        }
    }

    [Fact]
    public void WzBinaryReader_StringOffsetRestoresPositionOnInvalidTarget()
    {
        using var stream = new MemoryStream([0x2A, 0x00]);
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
        stream.Position = 1;

        Assert.Throws<InvalidDataException>(() => reader.ReadStringAtOffset(long.MaxValue));
        Assert.Equal(1, stream.Position);
    }

    private static byte[] Concat(params byte[][] parts)
    {
        int length = 0;
        foreach (byte[] part in parts)
            length = checked(length + part.Length);

        byte[] result = new byte[length];
        int offset = 0;
        foreach (byte[] part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }
        return result;
    }
}
