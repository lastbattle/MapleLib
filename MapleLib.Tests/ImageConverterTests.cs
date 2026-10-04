using MapleLib.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace MapleLib.Tests;

[TestClass]
public class ImageConverterTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ToWpfBitmap_PreservesArgbRowsAfterSourceDisposal(bool negativeStride)
    {
        const int width = 3;
        const int height = 2;
        const int storageStride = 16;
        // BGRA bytes specify independently known colors, including non-opaque
        // alpha. Distinct rows expose orientation and padding mistakes.
        byte[] expected =
        [
            30, 20, 10, 255, 60, 50, 40, 128, 90, 80, 70, 64,
            120, 110, 100, 192, 150, 140, 130, 255, 180, 170, 160, 0
        ];

        IntPtr storage = Marshal.AllocHGlobal(storageStride * height);
        BitmapSource converted;
        try
        {
            byte[] initial = new byte[storageStride * height];
            Array.Fill(initial, (byte)0xCD);
            Marshal.Copy(initial, 0, storage, initial.Length);

            int stride = negativeStride ? -storageStride : storageStride;
            IntPtr scan0 = negativeStride
                ? IntPtr.Add(storage, storageStride * (height - 1))
                : storage;
            for (int y = 0; y < height; y++)
                Marshal.Copy(expected, y * width * 4, IntPtr.Add(scan0, y * stride), width * 4);

            using var bitmap = new Bitmap(width, height, stride, PixelFormat.Format32bppArgb, scan0);
            // Verify that GDI+ recognizes the externally backed fixture's rows
            // before testing conversion, particularly for negative stride.
            Assert.AreEqual(Color.FromArgb(255, 10, 20, 30).ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
            Assert.AreEqual(Color.FromArgb(192, 100, 110, 120).ToArgb(), bitmap.GetPixel(0, 1).ToArgb());
            converted = bitmap.ToWpfBitmap();
        }
        finally
        {
            Marshal.FreeHGlobal(storage);
        }

        Assert.AreEqual(width, converted.PixelWidth);
        Assert.AreEqual(height, converted.PixelHeight);
        Assert.IsTrue(converted.IsFrozen, "The result must support use on another thread.");

        // Copy after both the GDI+ image and its original unmanaged storage have
        // been disposed. Moving this work to another thread also exercises the
        // public frozen-result contract used by background image loaders.
        byte[] actual = Task.Run(() =>
        {
            BitmapSource readable = converted.Format == PixelFormats.Bgra32
                ? converted
                : new FormatConvertedBitmap(converted, PixelFormats.Bgra32, null, 0);
            var bytes = new byte[width * height * 4];
            readable.CopyPixels(bytes, width * 4, 0);
            return bytes;
        }).GetAwaiter().GetResult();

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(96f, 96f, 95.98660278320312, 95.98660278320312)]
    [DataRow(120f, 144f, 119.9896011352539, 143.9925994873047)]
    [DataRow(72.5f, 110.25f, 72.4916000366211, 110.23600006103516)]
    [DataRow(300f, 300f, 299.9993896484375, 299.9993896484375)]
    public void ToWpfBitmap_PreservesPngResolutionQuantization(float x, float y, double expectedX, double expectedY)
    {
        using var bitmap = new Bitmap(3, 2, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(x, y);
        BitmapSource converted = bitmap.ToWpfBitmap();
        Assert.AreEqual(expectedX, converted.DpiX);
        Assert.AreEqual(expectedY, converted.DpiY);
        Assert.AreEqual(PixelFormats.Bgra32, converted.Format);
    }
}
