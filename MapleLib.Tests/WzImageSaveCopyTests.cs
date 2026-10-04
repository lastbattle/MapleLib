using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzImageSaveCopyTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(8)]
    [InlineData(0)]
    [InlineData(48)]
    public void UnchangedSaveCopiesAvailableDeclaredBytesAndRestoresPosition(int declaredSize)
    {
        byte[] payload = Enumerable.Range(0, 32).Select(value => (byte)(value * 7)).ToArray();
        byte[] storage = new byte[5 + 7 + payload.Length];
        payload.CopyTo(storage, 5 + 7);
        // Both the backing segment and image have nonzero origins. The source
        // does not expose its buffer, as in standalone IMG deserialization.
        using var source = new MemoryStream(storage, 5, 7 + payload.Length, writable: false);
        source.Position = 7;
        using var image = new WzImage("copy.img", source, WzMapleVersion.BMS) { BlockSize = declaredSize };
        source.Position = 2;
        using var destination = new MemoryStream();
        destination.Write([0xA1, 0xB2, 0xC3]);
        using var writer = new WzBinaryWriter(destination, new byte[4]);

        image.SaveImage(writer);

        byte[] expected = new byte[] { 0xA1, 0xB2, 0xC3 }.Concat(payload.Take(declaredSize)).ToArray();
        Assert.Equal(expected, destination.ToArray());
        Assert.Equal(expected.Length, destination.Position);
        Assert.Equal(2, source.Position);
        Assert.False(image.Changed);
        Assert.False(image.Parsed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DestinationFailureLeavesSourceAtConsumedImageEnd(bool closedDestination)
    {
        using var source = new MemoryStream(new byte[32], writable: false);
        source.Position = 7;
        using var image = new WzImage("copy.img", source, WzMapleVersion.BMS);
        source.Position = 2;
        using Stream destination = closedDestination ? new MemoryStream() : new FailingWriteStream();
        using var writer = new WzBinaryWriter(destination, new byte[4]);
        if (closedDestination)
            destination.Dispose();

        if (closedDestination)
            Assert.Throws<ObjectDisposedException>(() => image.SaveImage(writer));
        else
            Assert.Throws<IOException>(() => image.SaveImage(writer));
        Assert.Equal(32, source.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MemoryBackedUnchangedSavePersistsCompleteFileAfterWriterClose(bool useFileSerializer)
    {
        byte[] payload = Enumerable.Range(0, 4096).Select(value => (byte)(value % 251)).ToArray();
        using var source = new MemoryStream(payload, writable: false);
        using var image = new WzImage("copy.img", source, WzMapleVersion.BMS);
        source.Position = 17;
        string path = Path.Combine(Path.GetTempPath(), $"maple-save-copy-{Guid.NewGuid():N}.img");
        try
        {
            if (useFileSerializer)
            {
                // Raw-copy saves preserve the input wire bytes even when a
                // different output IV is configured; no parser is required.
                new WzImgSerializer(WzAESConstant.WZ_GMSIV).SerializeImage(image, path);
            }
            else
            {
                using (FileStream destination = File.Create(path))
                {
                    destination.Write([0xA1, 0xB2, 0xC3]);
                    using var writer = new WzBinaryWriter(destination, new byte[4]);
                    image.SaveImage(writer);
                    Assert.Equal(4099, destination.Position);
                }
            }
            Assert.Equal(17, source.Position);
            Assert.False(image.Changed);
            Assert.False(image.Parsed);
            byte[] expected = useFileSerializer ? payload : new byte[] { 0xA1, 0xB2, 0xC3 }.Concat(payload).ToArray();
            Assert.Equal(expected, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnchangedSavePreservesPublicWriterOverrideOutput()
    {
        byte[] payload = [10, 20, 30, 40];
        using var source = new MemoryStream(payload, writable: false);
        using var image = new WzImage("copy.img", source, WzMapleVersion.BMS);
        using var destination = new MemoryStream();
        using var writer = new MarkerWriter(destination);

        image.SaveImage(writer);

        Assert.Equal(new byte[] { 0xEE, 10, 20, 30, 40 }, destination.ToArray());
    }

    private sealed class MarkerWriter(Stream output) : WzBinaryWriter(output, new byte[4])
    {
        public override void Write(byte[] buffer)
        {
            base.Write((byte)0xEE);
            base.Write(buffer);
        }
    }

    private sealed class FailingWriteStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Destination failed.");
    }
}
