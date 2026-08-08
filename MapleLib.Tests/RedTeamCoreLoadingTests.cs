using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public void ListFileParser_RejectsLengthBomb()
    {
        string bombPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(bombPath, Concat(BitConverter.GetBytes(int.MaxValue), [0x41, 0x00]));
            Assert.Throws<InvalidDataException>(() => ListFileParser.ParseListFile(bombPath, WzMapleVersion.BMS));
        }
        finally
        {
            if (File.Exists(bombPath))
                File.Delete(bombPath);
        }
    }

    [Fact]
    public void ListFileParser_KeepsEmptyFinalEntrySafe()
    {
        string path = Path.GetTempFileName();
        try
        {
            // len=0 followed by the encrypted null terminator decodes to an
            // empty final entry; parsing should return it unchanged.
            File.WriteAllBytes(path, new byte[sizeof(int) + sizeof(ushort)]);

            var entries = ListFileParser.ParseListFile(path, WzMapleVersion.BMS);

            Assert.Single(entries);
            Assert.Equal(string.Empty, entries[0]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void WzBinaryReader_RejectsEncodedStringBeyondLimitBeforeReadingPayload()
    {
        int characterCount = MemoryLimits.MAX_WZ_STRING_BYTES / sizeof(ushort) + 1;
        byte[] prefix = Concat(
            [unchecked((byte)sbyte.MaxValue)],
            BitConverter.GetBytes(characterCount));
        using var stream = new PrefixOnlySparseStream(prefix, prefix.Length + (long)characterCount * sizeof(ushort));
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));

        Assert.Throws<InvalidDataException>(() => reader.ReadString());
        Assert.False(stream.PayloadReadAttempted);
    }

    [Fact]
    public void WzBinaryReader_RejectsUnterminatedMetadataStringAtLimit()
    {
        byte[] bytes = new byte[MemoryLimits.MAX_NULL_TERMINATED_STRING_BYTES + 1];
        Array.Fill(bytes, (byte)'x');
        using var stream = new MemoryStream(bytes);
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));

        Assert.Throws<InvalidDataException>(() => reader.ReadNullTerminatedString());
    }

    [Fact]
    public void WzMutableKey_RejectsGrowthBeyondStringLimit()
    {
        var key = new WzMutableKey(new byte[4], new byte[32]);

        Assert.Throws<InvalidDataException>(() => key.EnsureKeySize(MemoryLimits.MAX_WZ_STRING_BYTES + 1));
        Assert.Throws<InvalidDataException>(() => _ = key[MemoryLimits.MAX_WZ_STRING_BYTES]);
    }

    [Fact]
    public void WzFile_RejectsSparseOversizedHeaderBeforeMaterializingIt()
    {
        string path = Path.GetTempFileName();
        try
        {
            uint fStart = MemoryLimits.MAX_WZ_HEADER_BYTES + 64u;
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes("PKG1"));
                writer.Write((ulong)0);
                writer.Write(fStart);
                stream.SetLength(fStart + 2L);
            }

            using var wzFile = new WzFile(path, (short)115, WzMapleVersion.BMS);
            Assert.Throws<InvalidDataException>(() => wzFile.ParseWzFile());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void WzFile_FailedReparseKeepsPreviousDirectoryReaderUsable()
    {
        string validSource = Path.Combine(AppContext.BaseDirectory, "WzFiles", "Common", "TamingMob_GMS_95.wz");
        Assert.True(File.Exists(validSource), $"Missing bundled WZ fixture: {validSource}");

        string validPath = Path.Combine(Path.GetTempPath(), $"maplelib-valid-{Guid.NewGuid():N}.wz");
        string malformedPath = Path.Combine(Path.GetTempPath(), $"maplelib-malformed-{Guid.NewGuid():N}.wz");
        try
        {
            File.Copy(validSource, validPath);
            // Keep the malformed input in a separate path because a successfully
            // parsed WZ reader intentionally opens its source with FileShare.Read.
            File.WriteAllBytes(malformedPath, Encoding.ASCII.GetBytes("not-a-wz"));

            using var wzFile = new WzFile(validPath, (short)-1, WzMapleVersion.GMS);
            Assert.Equal(WzFileParseStatus.Success, wzFile.ParseWzFile());
            WzDirectory previousDirectory = wzFile.WzDirectory;
            Assert.NotNull(previousDirectory);

            FieldInfo pathField = typeof(WzFile).GetField("path", BindingFlags.Instance | BindingFlags.NonPublic)!;
            pathField.SetValue(wzFile, malformedPath);

            Assert.Throws<InvalidDataException>(() => wzFile.ParseWzFile());
            Assert.Same(previousDirectory, wzFile.WzDirectory);

            // The old reader remains available after the failed reparse.  Parsing
            // one lazy image exercises that reader rather than only checking the
            // directory reference.
            WzImage? image = previousDirectory.WzImages.FirstOrDefault();
            if (image != null)
                Assert.True(image.ParseImage(), $"Failed to parse retained image {image.Name} after a failed reparse.");
        }
        finally
        {
            if (File.Exists(validPath))
                File.Delete(validPath);
            if (File.Exists(malformedPath))
                File.Delete(malformedPath);
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

    [Fact]
    public void PartialStream_RejectsOutOfRangePositionAndWrites()
    {
        using var overflowBase = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialStream(overflowBase, long.MaxValue, 1));

        using var baseStream = new MemoryStream(new byte[8], writable: true);
        baseStream.Position = 2;
        using var partial = new PartialStream(baseStream, offset: 2, length: 3, leaveOpen: true);

        partial.Position = 0;
        byte[] data = [1, 2, 3, 4, 5];
        Assert.Equal(3, partial.Read(data, 0, data.Length));
        partial.Position = 0;
        IAsyncResult pendingRead = partial.BeginRead(data, 0, data.Length, callback: null!, state: null!);
        Assert.Equal(3, partial.EndRead(pendingRead));
        Assert.Throws<ArgumentOutOfRangeException>(() => partial.Position = 4);
        Assert.Throws<IOException>(() => partial.Seek(1, SeekOrigin.End));
        Assert.Throws<IOException>(() => partial.WriteByte(0xFF));

        partial.Position = 2;
        Assert.Throws<IOException>(() => partial.Write(data, 0, 2));
        partial.WriteByte(0xFE);
        Assert.Equal(0xFE, baseStream.ToArray()[4]);

        partial.Dispose();
        Assert.True(baseStream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => _ = partial.Position);
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

    private sealed class PrefixOnlySparseStream(byte[] prefix, long length) : Stream
    {
        private long position;

        public bool PayloadReadAttempted { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => position;
            set
            {
                if (value < 0 || value > length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (position >= prefix.Length)
            {
                PayloadReadAttempted = true;
                throw new InvalidOperationException("The encoded payload must not be read.");
            }

            int count = (int)Math.Min(buffer.Length, prefix.Length - position);
            prefix.AsSpan((int)position, count).CopyTo(buffer);
            position += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => position + offset,
                SeekOrigin.End => length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            Position = target;
            return position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
