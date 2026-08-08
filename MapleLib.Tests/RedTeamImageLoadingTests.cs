using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.Serializer;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class RedTeamImageLoadingTests
{
    [Theory]
    [InlineData(WzPngFormat.Format1, 7, 5, 70)]
    [InlineData(WzPngFormat.Format2, 7, 5, 140)]
    [InlineData(WzPngFormat.Format3, 7, 5, 64)]
    [InlineData(WzPngFormat.Format257, 7, 5, 70)]
    [InlineData(WzPngFormat.Format513, 7, 5, 70)]
    [InlineData(WzPngFormat.Format517, 32, 16, 4)]
    [InlineData(WzPngFormat.Format517, 7, 5, 2)]
    [InlineData(WzPngFormat.Format517, 17, 17, 8)]
    [InlineData(WzPngFormat.Format1026, 7, 5, 64)]
    [InlineData(WzPngFormat.Format2050, 7, 5, 64)]
    [InlineData(WzPngFormat.Format4098, 7, 5, 64)]
    public void GetDecodedSize_PreservesFormatSpecificLayout(WzPngFormat format, int width, int height, int expected)
    {
        Assert.Equal(expected, format.GetDecodedSize(width, height));
    }

    [Fact]
    public void ParseImage_RejectsPropertyCountBeforeAllocatingCollection()
    {
        byte[] imageBytes = Concat(
            [WzImage.WzImageHeaderByte_WithoutOffset],
            EncodedAscii("Property"),
            [0, 0],
            CompressedInt(int.MaxValue));

        using var stream = new MemoryStream(imageBytes);
        var image = new WzImage("test.img", stream, WzMapleVersion.BMS);

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
    }

    [Fact]
    public void ImgDeserializer_RejectsOversizedFileWithoutLeakingHandle()
    {
        string path = Path.Combine(Path.GetTempPath(), $"MapleLib-oversized-{Guid.NewGuid():N}.img");
        try
        {
            using (FileStream stream = File.Create(path))
                stream.SetLength((long)int.MaxValue + 1);

            Assert.Throws<InvalidDataException>(() => new WzImgDeserializer(freeResources: true)
                .WzImageFromIMGFile(path, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS), "bad.img", out _));

            // File.OpenRead uses FileShare.Read, so an undisposed reader prevents
            // this exclusive reopen on Windows.
            using (FileStream exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.True(exclusive.CanRead);
            }
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ParseImage_RejectsUnsupportedFloatSubtypeWithoutMarkingParsed()
    {
        byte[] imageBytes = Concat(
            [WzImage.WzImageHeaderByte_WithoutOffset],
            EncodedAscii("Property"),
            [0, 0, 1],
            NameBlock("value"),
            [4, 1]); // float property with an unsupported subtype

        using var stream = new MemoryStream(imageBytes);
        using var image = new WzImage("test.img", stream, WzMapleVersion.BMS);

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
        Assert.False(image.Parsed);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ParseImage_ConcurrentCallsDoNotDuplicateProperties()
    {
        byte[] imageBytes = Concat(
            [WzImage.WzImageHeaderByte_WithoutOffset],
            EncodedAscii("Property"),
            [0, 0, 1],
            NameBlock("x"),
            [0]);
        var firstReadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseFirstRead = new ManualResetEventSlim(false);
        using var stream = new GateStream(imageBytes, firstReadStarted, releaseFirstRead);
        using var image = new WzImage("test.img", stream, WzMapleVersion.BMS);
        stream.EnableGate();
        var firstReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> first = Task.Run(() =>
        {
            firstReady.TrySetResult(true);
            return image.ParseImage();
        });
        Task<bool> second = Task.Run(() =>
        {
            secondReady.TrySetResult(true);
            return image.ParseImage();
        });
        bool[] parseResults = null!;

        try
        {
            await Task.WhenAll(firstReady.Task, secondReady.Task).WaitAsync(TimeSpan.FromSeconds(5));
            await firstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            releaseFirstRead.Set();
            parseResults = await Task.WhenAll(first, second);
        }
        finally
        {
            releaseFirstRead.Set();
            if (!first.IsCompleted || !second.IsCompleted)
            {
                try
                {
                    await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception)
                {
                    // Preserve the original assertion or timeout from the test body.
                }
            }
        }

        Assert.True(parseResults[0]);
        Assert.True(parseResults[1]);
        Assert.Single(image.WzProperties);
    }

    [Fact]
    public void ParseImage_RejectsLuaLengthBeyondRemainingData()
    {
        byte[] imageBytes = Concat([0x01], CompressedInt(int.MaxValue), [1, 2, 3, 4]);
        using var stream = new MemoryStream(imageBytes);
        var image = new WzImage("script.lua", stream, WzMapleVersion.BMS);

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
    }

    [Fact]
    public void ParseImage_RejectsExcessiveNestedProperties()
    {
        byte[] nestedList = [0x00];
        for (int i = 0; i < 130; i++)
        {
            byte[] extendedValue = Concat(
                [WzImage.WzImageHeaderByte_WithoutOffset],
                EncodedAscii("Property"),
                [0, 0],
                nestedList);
            nestedList = Concat(
                [0x01],
                NameBlock("p"),
                [0x09],
                BitConverter.GetBytes((uint)extendedValue.Length),
                extendedValue);
        }

        byte[] imageBytes = Concat(
            [WzImage.WzImageHeaderByte_WithoutOffset],
            EncodedAscii("Property"),
            [0, 0],
            nestedList);
        using var stream = new MemoryStream(imageBytes);
        var image = new WzImage("test.img", stream, WzMapleVersion.BMS);

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
    }

    [Fact]
    public void ParseImage_RejectsExtendedPropertyEscapingDeclaredBlock()
    {
        byte[] escapedValue = Concat(
            [WzImage.WzImageHeaderByte_WithoutOffset],
            EncodedAscii("Property"),
            [0, 0, 0]);
        byte[] imageBytes = Concat(
            [WzImage.WzImageHeaderByte_WithoutOffset],
            EncodedAscii("Property"),
            [0, 0],
            [1],
            NameBlock("p"),
            [9],
            BitConverter.GetBytes(0u),
            escapedValue);

        using var stream = new MemoryStream(imageBytes);
        using var image = new WzImage("test.img", stream, WzMapleVersion.BMS)
        {
            BlockSize = imageBytes.Length
        };

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
    }

    [Fact]
    public void WzImage_DataBlockReadsDeclaredOffsetAndPreservesSharedReaderPosition()
    {
        using var stream = new MemoryStream([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
        stream.Position = 2;
        using var image = new WzImage("test.img", reader, checksum: 0)
        {
            Offset = 5,
            BlockSize = 3
        };
        stream.Position = 9;

        Assert.Equal(new byte[] { 5, 6, 7 }, image.DataBlock);
        Assert.Equal(9, stream.Position);
    }

    [Fact]
    public void ParseImage_RejectsBlockOutsideContainingStreamAndPreservesPosition()
    {
        using var stream = new MemoryStream([0, 1, 2]);
        using var image = new WzImage("test.img", stream, WzMapleVersion.BMS)
        {
            Offset = 1,
            BlockSize = 3
        };
        stream.Position = 2;

        Assert.Throws<InvalidDataException>(() => image.ParseImage());
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void LazyRawData_RejectsPostParseTruncationAndRestoresPosition()
    {
        using var stream = new MemoryStream(Concat(CompressedInt(3), [1, 2, 3]));
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
        var property = new WzRawDataProperty("raw", reader, 0);
        property.Parse(parseNow: false);
        stream.SetLength(stream.Length - 1);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => property.GetBytes(false));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task LazyRawData_ConcurrentReadsSerializeSharedReaderCursor()
    {
        byte[] bytes = Concat(CompressedInt(3), [1, 2, 3], CompressedInt(3), [4, 5, 6]);
        var firstReadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseFirstRead = new ManualResetEventSlim(false);
        using var stream = new GateStream(bytes, firstReadStarted, releaseFirstRead);
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
        var firstProperty = new WzRawDataProperty("first", reader, 0);
        firstProperty.Parse(parseNow: false);
        var secondProperty = new WzRawDataProperty("second", reader, 0);
        secondProperty.Parse(parseNow: false);
        stream.EnableGate();

        var firstReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // The first read intentionally waits on a gate while holding the
        // shared-reader lock. Use dedicated workers so a parallel test run
        // cannot starve the thread pool and make the gate timeout before the
        // test continuation gets a chance to release it.
        Task<byte[]> first = Task.Factory.StartNew(
            () =>
            {
                firstReady.TrySetResult(true);
                return firstProperty.GetBytes(saveInMemory: true);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<byte[]> second = Task.Factory.StartNew(
            () =>
            {
                secondReady.TrySetResult(true);
                return secondProperty.GetBytes(saveInMemory: true);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        try
        {
            await Task.WhenAll(firstReady.Task, secondReady.Task).WaitAsync(TimeSpan.FromSeconds(5));
            await firstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            releaseFirstRead.Set();
            byte[][] results = await Task.WhenAll(first, second);
            Assert.Equal(new byte[] { 1, 2, 3 }, results[0]);
            Assert.Equal(new byte[] { 4, 5, 6 }, results[1]);
        }
        finally
        {
            releaseFirstRead.Set();
            if (!first.IsCompleted || !second.IsCompleted)
            {
                try
                {
                    await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception)
                {
                    // Preserve the original assertion or timeout from the test body.
                }
            }
        }
    }

    [Fact]
    public void LazyVideo_RejectsPostParseTruncationAndRestoresPosition()
    {
        using var stream = new MemoryStream(Concat([7], CompressedInt(3), [1, 2, 3]));
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
        var property = new WzVideoProperty("video", reader);
        property.Parse(parseNow: false);
        stream.SetLength(stream.Length - 1);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => property.GetBytes(false));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void WzVideoProperty_DeepCloneOwnsIndependentChildrenAndBytes()
    {
        using var stream = new MemoryStream(Concat([7], CompressedInt(2), [0xAA, 0xBB]));
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
        var original = new WzVideoProperty("video", reader);
        original.Parse(parseNow: true);
        original.AddProperty(new WzIntProperty("child", 1));

        var clone = Assert.IsType<WzVideoProperty>(original.DeepClone());
        clone.WzProperties[0].Name = "changed";
        clone.GetBytes(true)[0] = 0;

        Assert.NotSame(original.WzProperties, clone.WzProperties);
        Assert.NotSame(original.WzProperties[0], clone.WzProperties[0]);
        Assert.Same(clone, clone.WzProperties[0].Parent);
        Assert.Equal("child", original.WzProperties[0].Name);
        Assert.Equal(0xAA, original.GetBytes(true)[0]);
    }

    [Fact]
    public void PngInflation_RejectsTruncatedAndExcessDecodedData()
    {
        byte[] twoBytes = WzPngProperty.Compress([1, 2]);
        byte[] threeBytes = WzPngProperty.Compress([1, 2, 3]);

        Assert.Throws<InvalidDataException>(() => WzPngProperty.Decompress(twoBytes, 3));
        Assert.Throws<InvalidDataException>(() => WzPngProperty.Decompress(threeBytes, 2));
        Assert.Throws<InvalidDataException>(() => WzPngProperty.Decompress([0x78], 1));
    }

    [Fact]
    public void Format517Decoder_FillsPartialEdgeBlockWithoutWritingPastBitmap()
    {
        using var bitmap = new Bitmap(7, 5, PixelFormat.Format16bppRgb565);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, 7, 5), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);
        try
        {
            PngUtility.DecompressImage_PixelDataForm517([0x00, 0xF8], 7, 5, bitmap, data);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        Color bottomRight = bitmap.GetPixel(6, 4);
        Assert.True(bottomRight.R > 240);
        Assert.True(bottomRight.G < 16);
        Assert.True(bottomRight.B < 16);
    }

    [Fact]
    public void BlockDecoders_RejectDimensionArithmeticOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PngUtility.DecompressImageBC7([], int.MaxValue, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngUtility.DecompressImageDXT3([], int.MaxValue, int.MaxValue, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngUtility.DecompressImageDXT5([], int.MaxValue, int.MaxValue, null!));
    }

    [Fact]
    public void Bc7PointerDecoder_RejectsOversizedDecodedBufferBeforeUnsafeWrites()
    {
        Assert.Throws<InvalidDataException>(() => Bc7Decoder.DecodeToBgra32(
            [], 8_193, 8_192, IntPtr.Zero, 0));
    }

    [Fact]
    public void BitmapStrideCopy_IgnoresExcessSourceInsteadOfOverwritingDestination()
    {
        IntPtr destination = Marshal.AllocHGlobal(8);
        try
        {
            Marshal.Copy(new byte[] { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC }, 0, destination, 8);
            var bitmapData = new BitmapData
            {
                Scan0 = destination,
                Stride = 4,
                Width = 1,
                Height = 1
            };

            PngUtility.CopyBmpDataWithStride([1, 2, 3, 4, 5, 6, 7, 8], 4, bitmapData);

            byte[] actual = new byte[8];
            Marshal.Copy(destination, actual, 0, actual.Length);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 0xCC, 0xCC, 0xCC, 0xCC }, actual);
        }
        finally
        {
            Marshal.FreeHGlobal(destination);
        }
    }

    [Fact]
    public void CyclicUolLinks_AreRejectedByLinkAndTypedResolution()
    {
        var image = new WzImage("test.img");
        var first = new WzUOLProperty("A", "B");
        var second = new WzUOLProperty("B", "A");
        image.AddProperty(first);
        image.AddProperty(second);

        Assert.Throws<InvalidDataException>(() => first.GetLinkedWzImageProperty());
        Assert.Throws<InvalidDataException>(() => first.GetString());
    }

    [Fact]
    public void BrokenUolLink_RemainsSafeToTraverse()
    {
        var image = new WzImage("test.img");
        var broken = new WzUOLProperty("broken", "missing");
        image.AddProperty(broken);

        Assert.Null(broken.LinkValue);
        Assert.Null(broken.WzProperties);
        Assert.Null(broken["child"]);
        Assert.Null(broken.GetFromPath("child"));
        Assert.Same(broken, broken.GetLinkedWzImageProperty());
    }

    [Fact]
    public void DisposingDirectoryImage_DoesNotCloseSharedReader()
    {
        using var stream = new MemoryStream([0x2A]);
        using var reader = new WzBinaryReader(stream, WzTool.GetIvByMapleVersion(WzMapleVersion.BMS));
        var first = new WzImage("first.img", reader, checksum: 0);
        var second = new WzImage("second.img", reader, checksum: 0);

        first.Dispose();

        Assert.True(stream.CanRead);
        Assert.Equal(0x2A, reader.ReadByte());
        second.Dispose();
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void LegacySerializer_EscapesUolPropertyName()
    {
        var serializer = new TestSerializer();

        string xml = serializer.Serialize(new WzUOLProperty("\"x\"><injected>", "target"));

        Assert.Contains("name=\"&quot;x&quot;&gt;&lt;injected&gt;\"", xml);
        Assert.DoesNotContain("<injected>", xml);
    }

    private static byte[] NameBlock(string value) => Concat([0x00], EncodedAscii(value));

    private static byte[] EncodedAscii(string value)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(unchecked((byte)-value.Length));
        for (int i = 0; i < value.Length; i++)
            stream.WriteByte((byte)(value[i] ^ (byte)(0xAA + i)));
        return stream.ToArray();
    }

    private static byte[] CompressedInt(int value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        if (value is >= -127 and <= 127)
            writer.Write((sbyte)value);
        else
        {
            writer.Write(sbyte.MinValue);
            writer.Write(value);
        }
        return stream.ToArray();
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

    private sealed class TestSerializer : WzSerializer
    {
        public TestSerializer() : base(0, LineBreak.None)
        {
        }

        public string Serialize(WzImageProperty property)
        {
            using var writer = new StringWriter();
            WritePropertyToXML(writer, string.Empty, property, string.Empty);
            return writer.ToString();
        }
    }

    private sealed class GateStream : Stream
    {
        private readonly MemoryStream inner;
        private readonly TaskCompletionSource<bool> firstReadStarted;
        private readonly ManualResetEventSlim releaseFirstRead;
        private int gateUsed;
        private int gateEnabled;

        public GateStream(byte[] bytes, TaskCompletionSource<bool> firstReadStarted, ManualResetEventSlim releaseFirstRead)
        {
            inner = new MemoryStream(bytes, writable: false);
            this.firstReadStarted = firstReadStarted;
            this.releaseFirstRead = releaseFirstRead;
        }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            GateFirstRead();
            return inner.Read(buffer, offset, count);
        }
        public override int Read(Span<byte> buffer)
        {
            GateFirstRead();
            return inner.Read(buffer);
        }
        public override int ReadByte()
        {
            GateFirstRead();
            return inner.ReadByte();
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }

        public void EnableGate() => Volatile.Write(ref gateEnabled, 1);

        private void GateFirstRead()
        {
            if (Volatile.Read(ref gateEnabled) == 0)
                return;
            if (Interlocked.Exchange(ref gateUsed, 1) == 0)
            {
                firstReadStarted.TrySetResult(true);
                releaseFirstRead.Wait(TimeSpan.FromSeconds(5));
            }
        }
    }
}
