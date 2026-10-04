using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using MapleLib.Helpers;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class DxtDecodeOptimizationTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 7)]
    [InlineData(9, 5)]
    [InlineData(127, 131)]
    [InlineData(1025, 1025)]
    public void Dxt3_matches_scalar_oracle_with_stride_canaries(int width, int height) => Verify(width, height, false);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 7)]
    [InlineData(9, 5)]
    [InlineData(127, 131)]
    [InlineData(1025, 1025)]
    public void Dxt5_matches_scalar_oracle_with_stride_canaries(int width, int height) => Verify(width, height, true);

    private static void Verify(int width, int height, bool dxt5)
    {
        byte[] compressed = MakeCompressed(width, height, dxt5);
        byte[] expected = DecodeScalar(compressed, width, height, dxt5);
        int rowBytes = checked(width * 4);
        int padding = 13;

        foreach (int sign in new[] { 1, -1 })
        {
            int stride = sign * (rowBytes + padding);
            int allocation = checked((rowBytes + padding) * height + 32);
            IntPtr storage = Marshal.AllocHGlobal(allocation);
            try
            {
                byte[] canary = new byte[allocation];
                Array.Fill(canary, (byte)0xCD);
                Marshal.Copy(canary, 0, storage, allocation);
                IntPtr scan0 = sign > 0 ? storage : IntPtr.Add(storage, (rowBytes + padding) * (height - 1));
                var data = new BitmapData { Scan0 = scan0, Stride = stride, Width = width, Height = height };

                if (dxt5) PngUtility.DecompressImageDXT5(compressed, width, height, data);
                else PngUtility.DecompressImageDXT3(compressed, width, height, data);

                byte[] actual = new byte[allocation];
                Marshal.Copy(storage, actual, 0, allocation);
                for (int y = 0; y < height; y++)
                {
                    int physical = sign > 0 ? y : height - 1 - y;
                    int actualOffset = physical * (rowBytes + padding);
                    Assert.Equal(expected.AsSpan(y * rowBytes, rowBytes).ToArray(), actual.AsSpan(actualOffset, rowBytes).ToArray());
                    for (int i = rowBytes; i < rowBytes + padding; i++)
                        Assert.Equal((byte)0xCD, actual[actualOffset + i]);
                }
            }
            finally { Marshal.FreeHGlobal(storage); }
        }
    }

    private static byte[] MakeCompressed(int width, int height, bool dxt5)
    {
        int blocksX = (width + 3) / 4, blocksY = (height + 3) / 4;
        byte[] result = new byte[checked(blocksX * blocksY * 16)];
        var random = new Random(0x5A17 + width * 17 + height * 31 + (dxt5 ? 1 : 0));
        random.NextBytes(result);
        for (int block = 0; block < blocksX * blocksY; block++)
        {
            int offset = block * 16;
            if (!dxt5)
            {
                result[offset + 0] = (byte)(0x10 + block % 200);
                result[offset + 1] = (byte)(0xF0 - block % 100);
            }
            else
            {
                result[offset + 0] = (byte)(block * 37 + 17);
                result[offset + 1] = (byte)(block * 19 + 91);
            }
            result[offset + 8] = (byte)(0xFF - block * 13);
            result[offset + 9] = (byte)(0x20 + block * 7);
            result[offset + 10] = (byte)(0x40 + block * 11);
        }
        return result;
    }

    private static byte[] DecodeScalar(byte[] raw, int width, int height, bool dxt5)
    {
        int blocksX = (width + 3) / 4, blocksY = (height + 3) / 4;
        byte[] output = new byte[checked(width * height * 4)];
        for (int by = 0; by < blocksY; by++) for (int bx = 0; bx < blocksX; bx++)
        {
            int off = (by * blocksX + bx) * 16;
            byte[] alpha = new byte[16];
            if (dxt5)
            {
                byte a0 = raw[off], a1 = raw[off + 1];
                byte[] table = new byte[8]; table[0] = a0; table[1] = a1;
                if (a0 > a1) for (int i = 2; i < 8; i++) table[i] = (byte)(((8 - i) * a0 + (i - 1) * a1 + 3) / 7);
                else { for (int i = 2; i < 6; i++) table[i] = (byte)(((6 - i) * a0 + (i - 1) * a1 + 2) / 5); table[6] = 0; table[7] = 255; }
                for (int i = 0; i < 16; i++) { int bit = 3 * i; int value = (raw[off + 2 + bit / 8] | (raw[off + 3 + bit / 8] << 8) | (raw[off + 4 + bit / 8] << 16)) >> (bit % 8) & 7; alpha[i] = table[value]; }
            }
            else for (int i = 0; i < 16; i++) { byte n = raw[off + i / 2]; alpha[i] = (byte)(((i & 1) == 0 ? n & 15 : n >> 4) * 17); }
            ushort c0 = BitConverter.ToUInt16(raw, off + 8), c1 = BitConverter.ToUInt16(raw, off + 10);
            Color[] colors = Colors(c0, c1); int indices = off + 12;
            for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++)
            {
                int x = bx * 4 + i, y = by * 4 + j; if (x >= width || y >= height) continue;
                int ci = (raw[indices + j] >> (2 * i)) & 3, dst = (y * width + x) * 4; Color c = colors[ci];
                output[dst] = c.B; output[dst + 1] = c.G; output[dst + 2] = c.R; output[dst + 3] = alpha[j * 4 + i];
            }
        }
        return output;
    }

    private static Color[] Colors(ushort c0, ushort c1)
    {
        Color a = ExpandRgb565(c0), b = ExpandRgb565(c1);
        if (c0 > c1) return new[] { a, b, Color.FromArgb(255, (a.R * 2 + b.R + 1) / 3, (a.G * 2 + b.G + 1) / 3, (a.B * 2 + b.B + 1) / 3), Color.FromArgb(255, (a.R + b.R * 2 + 1) / 3, (a.G + b.G * 2 + 1) / 3, (a.B + b.B * 2 + 1) / 3) };
        return new[] { a, b, Color.FromArgb(255, (a.R + b.R) / 2, (a.G + b.G) / 2, (a.B + b.B) / 2), Color.FromArgb(255, Color.Black) };
    }

    private static Color ExpandRgb565(ushort value)
    {
        int red = (value >> 11) & 0x1F;
        int green = (value >> 5) & 0x3F;
        int blue = value & 0x1F;
        return Color.FromArgb(
            (red << 3) | (red >> 2),
            (green << 2) | (green >> 4),
            (blue << 3) | (blue >> 2));
    }
}
