using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MapleLib.Converters;
using MapleLib.Helpers;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace MapleLib.Helpers
{
    public class PngUtility
    {
        #region Common
        public static byte[] BitmapToByteArray(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }
        #endregion

        #region Decode
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color RGB565ToColor(ushort val)
        {
            const int rgb565_mask_r = 0xf800;
            const int rgb565_mask_g = 0x07e0;
            const int rgb565_mask_b = 0x001f;

            int r = (val & rgb565_mask_r) >> 11;
            int g = (val & rgb565_mask_g) >> 5;
            int b = (val & rgb565_mask_b);
            var c = Color.FromArgb(
                (r << 3) | (r >> 2),
                (g << 2) | (g >> 4),
                (b << 3) | (b >> 2));
            return c;
        }

        /// <summary>
        /// For debugging: an example of this image may be found at "Effect.wz\\5skill.img\\character_delayed\\0"
        /// </summary>
        /// <param name="rawData">The raw compressed image data as a Span<byte></param>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="bmp">The target Bitmap to write decompressed data to</param>
        /// <param name="bmpData">The locked BitmapData for direct memory access</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void DecompressImage_PixelDataBgra4444(Span<byte> rawData, int width, int height, Bitmap bmp, BitmapData bmpData)
        {
            int uncompressedSize = width * height * 2;
            if (rawData.Length < uncompressedSize)
                throw new ArgumentException("Raw data length is insufficient for the specified dimensions.");

            // Use Span for the decoded output, sized for 32bpp ARGB (4 bytes per pixel)
            int outputSize = uncompressedSize * 2; // BGRA4444 expands 2 bytes to 4 bytes per pixel

            Span<byte> decoded = outputSize <= MemoryLimits.STACKALLOC_SIZE_LIMIT_L1
                    ? stackalloc byte[outputSize] // Try to use stackalloc for small images to avoid heap allocation
                    : new byte[outputSize].AsSpan();  // Fallback to heap allocation for larger images

            // Process raw data with SIMD if supported
            if (Sse2.IsSupported)
            {
                int i = 0;
                fixed (byte* pRawData = rawData)
                {
                    for (i = 0; i <= rawData.Length - 16; i += 16)
                    {
                        // Load 16 bytes (8 pixels) into a 128-bit SIMD register
                        Vector128<byte> input = Sse2.LoadVector128(pRawData + i);

                        // Extract low nibbles (B or R)
                        Vector128<byte> lo = Sse2.And(input, Vector128.Create((byte)0x0F));

                        // Extract high nibbles (G or A), shifted to low position
                        Vector128<byte> hi = Sse2.And(Sse2.ShiftRightLogical(input.AsUInt32(), 4).AsByte(), Vector128.Create((byte)0x0F));

                        // Expand to 8 bits: lo | (lo << 4), hi | (hi << 4)
                        Vector128<byte> b = Sse2.Or(Sse2.ShiftLeftLogical(lo.AsUInt32(), 4).AsByte(), lo);
                        Vector128<byte> g = Sse2.Or(Sse2.ShiftLeftLogical(hi.AsUInt32(), 4).AsByte(), hi);

                        // Interleave b and g to get b0, g0, b1, g1, ..., for 32 bytes output
                        Vector128<byte> low = Sse2.UnpackLow(b, g);   // b0, g0, ..., b7, g7
                        Vector128<byte> high = Sse2.UnpackHigh(b, g); // b8, g8, ..., b15, g15

                        // Store directly into the decoded Span
                        low.CopyTo(decoded.Slice(i * 2));
                        high.CopyTo(decoded.Slice(i * 2 + 16));
                    }
                }

                // Handle remaining bytes scalarly
                for (; i < uncompressedSize; i++)
                {
                    byte byteAtPosition = rawData[i];
                    int lo = byteAtPosition & 0x0F;
                    byte b = (byte)(lo | (lo << 4));
                    decoded[i * 2] = b;
                    int hi = byteAtPosition & 0xF0;
                    byte g = (byte)(hi | (hi >> 4));
                    decoded[i * 2 + 1] = g;
                }
            }
            else
            {
                // Scalar fallback for non-SSE2 systems
                for (int i = 0; i < uncompressedSize; i++)
                {
                    byte byteAtPosition = rawData[i];
                    int lo = byteAtPosition & 0x0F;
                    byte b = (byte)(lo | (lo << 4));
                    decoded[i * 2] = b;
                    int hi = byteAtPosition & 0xF0;
                    byte g = (byte)(hi | (hi >> 4));
                    decoded[i * 2 + 1] = g;
                }
            }

            // Copy decoded data directly to BitmapData using Span
            decoded.CopyTo(new Span<byte>(bmpData.Scan0.ToPointer(), outputSize));
        }

        /// <summary>
        /// DXT3
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bmp"></param>
        /// <param name="bmpData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DecompressImageDXT3(byte[] rawData, int width, int height, BitmapData bmpData)
        {
            if (Sse2.IsSupported && Ssse3.IsSupported)
            {
                byte[] decoded = new byte[width * height * 4];

                Parallel.For(0, height / 4, blockY =>
                {
                    // Each thread gets its own temporary arrays to avoid sharing
                    Color[] colorTable = new Color[4];
                    int[] colorIdxTable = new int[16];
                    byte[] alphaTable = new byte[16];

                    for (int blockX = 0; blockX < width / 4; blockX++)
                    {
                        int x = blockX * 4;
                        int y = blockY * 4;
                        int off = x * 4 + y * width;

                        // Extract block data
                        ExpandAlphaTableDXT3(alphaTable, rawData, off);
                        ushort u0 = BitConverter.ToUInt16(rawData, off + 8);
                        ushort u1 = BitConverter.ToUInt16(rawData, off + 10);
                        ExpandColorTable(colorTable, u0, u1);
                        ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                        // Precompute color components (first 4 bytes used)
                        Vector128<byte> bVec = Vector128.Create(colorTable[0].B, colorTable[1].B, colorTable[2].B, colorTable[3].B,
                                                                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                        Vector128<byte> gVec = Vector128.Create(colorTable[0].G, colorTable[1].G, colorTable[2].G, colorTable[3].G,
                                                                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                        Vector128<byte> rVec = Vector128.Create(colorTable[0].R, colorTable[1].R, colorTable[2].R, colorTable[3].R,
                                                                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

                        // Process each row of the 4x4 block
                        for (int j = 0; j < 4; j++)
                        {
                            int baseIdx = j * 4;
                            Vector128<byte> idxVec = Vector128.Create((byte)colorIdxTable[baseIdx], (byte)colorIdxTable[baseIdx + 1],
                                                                      (byte)colorIdxTable[baseIdx + 2], (byte)colorIdxTable[baseIdx + 3],
                                                                      0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                            Vector128<byte> aVec = Vector128.Create(alphaTable[baseIdx], alphaTable[baseIdx + 1],
                                                                    alphaTable[baseIdx + 2], alphaTable[baseIdx + 3],
                                                                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

                            // Select B, G, R using indices and prepare alpha
                            Vector128<byte> b = Ssse3.Shuffle(bVec, idxVec);
                            Vector128<byte> g = Ssse3.Shuffle(gVec, idxVec);
                            Vector128<byte> r = Ssse3.Shuffle(rVec, idxVec);
                            Vector128<byte> a = aVec;

                            // Interleave into BGRA format
                            Vector128<byte> br = Sse2.UnpackLow(b, r);
                            Vector128<byte> ga = Sse2.UnpackLow(g, a);
                            Vector128<byte> bgra = Sse2.UnpackLow(br, ga);

                            // Debug: first pixel’s BGRA for verification
                            /*if (x == 4 && y == 0 && j == 0)
                            {
                                byte[] bytes = new byte[16];  // Vector128 is 16 bytes
                                bgra.CopyTo(bytes);
                                Debug.WriteLine($"BGRA: [{bytes[0]}, {bytes[1]}, {bytes[2]}, {bytes[3]}]");
                            }*/

                            // Write 4 pixels to the decoded array
                            int pos = (y + j) * width * 4 + x * 4;
                            fixed (byte* ptr = decoded)
                            {
                                Sse2.Store(ptr + pos, bgra);
                            }
                        }
                    }
                });

                // Copy the final decompressed data to the bitmap
                Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);


            }
            else
            {
                byte[] decoded = new byte[width * height * 4];

                Color[] colorTable = new Color[4];
                int[] colorIdxTable = new int[16];
                byte[] alphaTable = new byte[16];

                for (int y = 0; y < height; y += 4)
                {
                    for (int x = 0; x < width; x += 4)
                    {
                        int off = x * 4 + y * width;
                        ExpandAlphaTableDXT3(alphaTable, rawData, off);
                        ushort u0 = BitConverter.ToUInt16(rawData, off + 8);
                        ushort u1 = BitConverter.ToUInt16(rawData, off + 10);
                        ExpandColorTable(colorTable, u0, u1);
                        ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                Color color = colorTable[colorIdxTable[j * 4 + i]];
                                byte alpha = alphaTable[j * 4 + i];

                                SetPixel(decoded,
                                    x + i,
                                    y + j,
                                    width,
                                    color,
                                    alpha);

                                /*if (x == 4 && y == 0 && j == 0 && i == 0)
                                {
                                    Debug.WriteLine($"Scalar BGRA: [{color.B}, {color.G}, {color.R}, {alpha}]");
                                }*/
                            }
                        }
                    }
                }
                Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecompressImage_PixelDataForm517(byte[] rawData, int width, int height, Bitmap bmp, BitmapData bmpData)
        {
            byte[] decoded = new byte[width * height * 2];

            int lineIndex = 0;
            for (int j0 = 0, j1 = height / 16; j0 < j1; j0++)
            {
                var dstIndex = lineIndex;
                for (int i0 = 0, i1 = width / 16; i0 < i1; i0++)
                {
                    int idx = (i0 + j0 * i1) * 2;
                    byte b0 = rawData[idx];
                    byte b1 = rawData[idx + 1];
                    for (int k = 0; k < 16; k++)
                    {
                        decoded[dstIndex++] = b0;
                        decoded[dstIndex++] = b1;
                    }
                }
                for (int k = 1; k < 16; k++)
                {
                    Array.Copy(decoded, lineIndex, decoded, dstIndex, width * 2);
                    dstIndex += width * 2;
                }

                lineIndex += width * 32;
            }
            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
        }

        /// <summary>
        /// DXT5
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bmp"></param>
        /// <param name="bmpData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DecompressImageDXT5(byte[] rawData, int width, int height, BitmapData bmpData)
        {
            byte* pRawData = (byte*)rawData.AsSpan().GetPinnableReference();
            byte* pDecoded = (byte*)bmpData.Scan0;
            int blockCountX = (width + 3) / 4;  // Round up to cover partial blocks
            int blockCountY = (height + 3) / 4; // Round up to cover partial blocks
            int stride = bmpData.Stride;        // Use actual stride for offset calculation

            if (Sse2.IsSupported)
            {
                // Parallelize across rows of blocks to leverage multiple CPU cores
                Parallel.For(0, blockCountY, y =>
                {
                    for (int x = 0; x < blockCountX; x += 2) // Process 2 blocks at a time
                    {
                        int offset = (y * blockCountX + x) * 16;
                        if (offset + 16 <= rawData.Length)
                        {
                            // Process block 1
                            byte a0_1 = rawData[offset];
                            byte a1_1 = rawData[offset + 1];
                            byte[] alphaTable1 = new byte[8];
                            ExpandAlphaTableDXT5(alphaTable1, a0_1, a1_1);
                            int[] alphaIdxTable1 = new int[16];
                            ExpandAlphaIndexTableDXT5(alphaIdxTable1, rawData, offset + 2);

                            ushort c0_1 = BitConverter.ToUInt16(rawData, offset + 8);
                            ushort c1_1 = BitConverter.ToUInt16(rawData, offset + 10);
                            Color[] colors1 = new Color[4];
                            ExpandColorTable(colors1, c0_1, c1_1);
                            int[] colorIndices1 = new int[16];
                            ExpandColorIndexTable(colorIndices1, rawData, offset + 12);

                            // Write pixels for block 1
                            for (int j = 0; j < 4; j++)
                            {
                                int pixelY = y * 4 + j;
                                if (pixelY >= height) continue; // Skip if out of bounds
                                for (int i = 0; i < 4; i++)
                                {
                                    int pixelX = x * 4 + i;
                                    if (pixelX >= width) continue; // Skip if out of bounds
                                    int pixelOffset = pixelY * stride + pixelX * 4;
                                    Color c = colors1[colorIndices1[j * 4 + i]];
                                    byte alpha = alphaTable1[alphaIdxTable1[j * 4 + i]];
                                    pDecoded[pixelOffset] = c.B;
                                    pDecoded[pixelOffset + 1] = c.G;
                                    pDecoded[pixelOffset + 2] = c.R;
                                    pDecoded[pixelOffset + 3] = alpha;
                                }
                            }

                            // Process block 2 if within bounds
                            if (x + 1 < blockCountX && offset + 32 <= rawData.Length)
                            {
                                byte a0_2 = rawData[offset + 16];
                                byte a1_2 = rawData[offset + 17];
                                byte[] alphaTable2 = new byte[8];
                                ExpandAlphaTableDXT5(alphaTable2, a0_2, a1_2);
                                int[] alphaIdxTable2 = new int[16];
                                ExpandAlphaIndexTableDXT5(alphaIdxTable2, rawData, offset + 18);

                                ushort c0_2 = BitConverter.ToUInt16(rawData, offset + 24);
                                ushort c1_2 = BitConverter.ToUInt16(rawData, offset + 26);
                                Color[] colors2 = new Color[4];
                                ExpandColorTable(colors2, c0_2, c1_2);
                                int[] colorIndices2 = new int[16];
                                ExpandColorIndexTable(colorIndices2, rawData, offset + 28);

                                // Write pixels for block 2
                                for (int j = 0; j < 4; j++)
                                {
                                    int pixelY = y * 4 + j;
                                    if (pixelY >= height) continue; // Skip if out of bounds
                                    for (int i = 0; i < 4; i++)
                                    {
                                        int pixelX = (x + 1) * 4 + i;
                                        if (pixelX >= width) continue; // Skip if out of bounds
                                        int pixelOffset = pixelY * stride + pixelX * 4;
                                        Color c = colors2[colorIndices2[j * 4 + i]];
                                        byte alpha = alphaTable2[alphaIdxTable2[j * 4 + i]];
                                        pDecoded[pixelOffset] = c.B;
                                        pDecoded[pixelOffset + 1] = c.G;
                                        pDecoded[pixelOffset + 2] = c.R;
                                        pDecoded[pixelOffset + 3] = alpha;
                                    }
                                }
                            }
                        }
                    }
                });
            }
            else
            {
                // Scalar fallback
                byte[] decoded = new byte[width * height * 4];

                Color[] colorTable = new Color[4];
                int[] colorIdxTable = new int[16];
                byte[] alphaTable = new byte[8];
                int[] alphaIdxTable = new int[16];
                for (int y = 0; y < height; y += 4)
                {
                    for (int x = 0; x < width; x += 4)
                    {
                        int off = x * 4 + y * width;
                        ExpandAlphaTableDXT5(alphaTable, rawData[off + 0], rawData[off + 1]);
                        ExpandAlphaIndexTableDXT5(alphaIdxTable, rawData, off + 2);
                        ushort u0 = BitConverter.ToUInt16(rawData, off + 8);
                        ushort u1 = BitConverter.ToUInt16(rawData, off + 10);
                        ExpandColorTable(colorTable, u0, u1);
                        ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                SetPixel(decoded,
                                    x + i,
                                    y + j,
                                    width,
                                    colorTable[colorIdxTable[j * 4 + i]],
                                    alphaTable[alphaIdxTable[j * 4 + i]]);
                            }
                        }
                    }
                }
                Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetPixel(byte[] pixelData, int x, int y, int width, Color color, byte alpha)
        {
            int offset = (y * width + x) * 4;
            pixelData[offset + 0] = color.B;
            pixelData[offset + 1] = color.G;
            pixelData[offset + 2] = color.R;
            pixelData[offset + 3] = alpha;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyBmpDataWithStride(byte[] source, int stride, BitmapData bmpData)
        {
            if (bmpData.Stride == stride)
            {
                Marshal.Copy(source, 0, bmpData.Scan0, source.Length);
            }
            else
            {
                for (int y = 0; y < bmpData.Height; y++)
                {
                    Marshal.Copy(source, stride * y, bmpData.Scan0 + bmpData.Stride * y, stride);
                }
            }

        }
        #endregion

        #region DXT1 Color Decode
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandColorTable(Color[] color, ushort c0, ushort c1)
        {
            color[0] = RGB565ToColor(c0);
            color[1] = RGB565ToColor(c1);
            if (c0 > c1)
            {
                color[2] = Color.FromArgb(0xff, (color[0].R * 2 + color[1].R + 1) / 3, (color[0].G * 2 + color[1].G + 1) / 3, (color[0].B * 2 + color[1].B + 1) / 3);
                color[3] = Color.FromArgb(0xff, (color[0].R + color[1].R * 2 + 1) / 3, (color[0].G + color[1].G * 2 + 1) / 3, (color[0].B + color[1].B * 2 + 1) / 3);
            }
            else
            {
                color[2] = Color.FromArgb(0xff, (color[0].R + color[1].R) / 2, (color[0].G + color[1].G) / 2, (color[0].B + color[1].B) / 2);
                color[3] = Color.FromArgb(0xff, Color.Black);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandColorIndexTable(int[] colorIndex, byte[] rawData, int offset)
        {
            for (int i = 0; i < 16; i += 4, offset++)
            {
                colorIndex[i + 0] = (rawData[offset] & 0x03);
                colorIndex[i + 1] = (rawData[offset] & 0x0c) >> 2;
                colorIndex[i + 2] = (rawData[offset] & 0x30) >> 4;
                colorIndex[i + 3] = (rawData[offset] & 0xc0) >> 6;
            }
        }
        #endregion

        #region DXT3_DXT5 Alpha Decode
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaTableDXT3(byte[] alpha, byte[] rawData, int offset)
        {
            for (int i = 0; i < 16; i += 2, offset++)
            {
                alpha[i + 0] = (byte)(rawData[offset] & 0x0f);
                alpha[i + 1] = (byte)((rawData[offset] & 0xf0) >> 4);
            }
            for (int i = 0; i < 16; i++)
            {
                alpha[i] = (byte)(alpha[i] | (alpha[i] << 4));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaTableDXT5(byte[] alpha, byte a0, byte a1)
        {
            // get the two alpha values
            alpha[0] = a0;
            alpha[1] = a1;

            // compare the values to build the codebook
            if (a0 > a1)
            {
                for (int i = 2; i < 8; i++) // // use 7-alpha codebook
                {
                    alpha[i] = (byte)(((8 - i) * a0 + (i - 1) * a1 + 3) / 7);
                }
            }
            else
            {
                for (int i = 2; i < 6; i++) // // use 5-alpha codebook
                {
                    alpha[i] = (byte)(((6 - i) * a0 + (i - 1) * a1 + 2) / 5);
                }
                alpha[6] = 0;
                alpha[7] = 255;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaIndexTableDXT5(int[] alphaIndex, byte[] rawData, int offset)
        {
            // write out the indexed codebook values
            for (int i = 0; i < 16; i += 8, offset += 3)
            {
                int flags = rawData[offset]
                    | (rawData[offset + 1] << 8)
                    | (rawData[offset + 2] << 16);

                // unpack 8 3-bit values from it
                for (int j = 0; j < 8; j++)
                {
                    int mask = 0x07 << (3 * j);
                    alphaIndex[i + j] = (flags & mask) >> (3 * j);
                }
            }
        }
        #endregion

        #region Encode
        /// <summary>
        /// Compresses the bmp to the selected SurfaceFormat byte[].
        /// TODO: Other WzPngFormat.
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public static (WzPngFormat, byte[]) CompressImageToPngFormat(Bitmap bmp, SurfaceFormat format)
        {
            // TODO: Format2, Format513, Format517
            byte[] retPixelData;
            WzPngFormat retFormat;
            switch (format)
            {
                case SurfaceFormat.Bgra4444:
                    retPixelData = GetPixelDataFormat1(bmp);
                    retFormat = WzPngFormat.Format1;
                    break;

                case SurfaceFormat.Bgra5551:
                    retPixelData = GetPixelDataFormat257(bmp);
                    retFormat = WzPngFormat.Format257;
                    break;

                case SurfaceFormat.Dxt3 when bmp.PixelFormat == PixelFormat.Format8bppIndexed:
                    retPixelData = CompressDXT3(bmp); // Could add grayscale conversion if needed
                    retFormat = WzPngFormat.Format3;
                    break;
                case SurfaceFormat.Dxt3:
                    retPixelData = CompressDXT3(bmp);
                    retFormat = WzPngFormat.Format1026;
                    break;

                case SurfaceFormat.Dxt5:
                    retPixelData = GetPixelDataFormat2050(bmp);
                    retFormat = WzPngFormat.Format2050;
                    break;

                /*
                case WzPngFormat.Format2:
                    pixelData = GetPixelDataFormat2(bmp);
                    break;
                case WzPngFormat.Format3:
                    pixelData = GetPixelDataFormat3(bmp);
                    break;
                case WzPngFormat.Format257:
                    pixelData = GetPixelDataFormat257(bmp);
                    break;
                case WzPngFormat.Format513:
                    pixelData = GetPixelDataFormat513(bmp);
                    break;
                case WzPngFormat.Format517:
                    pixelData = GetPixelDataFormat517(bmp);
                    break;
                */

                default: // compress as standard, default to BGRA8888 for now
                    retPixelData = GetPixelDataFormat2(bmp);
                    retFormat = WzPngFormat.Format2;
                    break;

                    //throw new NotImplementedException($"Compression for SurfaceFormat {format} is not supported.");
            }
            return (retFormat, retPixelData);
        }

        /// <summary>
        /// BGRA4444
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        private static byte[] GetPixelDataFormat1(Bitmap bmp)
        {
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] buf = new byte[bmp.Width * bmp.Height * 2];
                int index = 0;
                unsafe
                {
                    byte* scan0 = (byte*)bmpData.Scan0;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        byte* row = scan0 + y * bmpData.Stride;
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            int pixel = *(int*)(row + x * 4);
                            byte b = (byte)((pixel >> 0) & 0xFF);
                            byte g = (byte)((pixel >> 8) & 0xFF);
                            byte r = (byte)((pixel >> 16) & 0xFF);
                            byte a = (byte)((pixel >> 24) & 0xFF);

                            byte b4 = (byte)(b >> 4);
                            byte g4 = (byte)(g >> 4);
                            byte r4 = (byte)(r >> 4);
                            byte a4 = (byte)(a >> 4);

                            buf[index++] = (byte)((g4 << 4) | b4); // Low byte: B4|G4<<4
                            buf[index++] = (byte)((a4 << 4) | r4); // High byte: R4|A4<<4
                        }
                    }
                }
                return buf;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }


        private static byte[] GetPixelDataFormat2(Bitmap bmp)
        {
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int length = bmp.Width * bmp.Height * 4;
                byte[] buf = new byte[length];
                if (bmpData.Stride == bmp.Width * 4)
                {
                    Marshal.Copy(bmpData.Scan0, buf, 0, length);
                }
                else
                {
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        IntPtr rowPtr = bmpData.Scan0 + y * bmpData.Stride;
                        Marshal.Copy(rowPtr, buf, y * bmp.Width * 4, bmp.Width * 4);
                    }
                }
                return buf;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        private static byte[] GetPixelDataFormat257(Bitmap bmp)
        {
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] buf = new byte[bmp.Width * bmp.Height * 2];
                int index = 0;
                unsafe
                {
                    byte* scan0 = (byte*)bmpData.Scan0;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        byte* row = scan0 + y * bmpData.Stride;
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            int pixel = *(int*)(row + x * 4);
                            byte b = (byte)((pixel >> 0) & 0xFF);
                            byte g = (byte)((pixel >> 8) & 0xFF);
                            byte r = (byte)((pixel >> 16) & 0xFF);
                            byte a = (byte)((pixel >> 24) & 0xFF);

                            byte a1 = (byte)(a >= 128 ? 1 : 0); // 1-bit alpha
                            byte r5 = (byte)((r * 31) / 255);
                            byte g5 = (byte)((g * 31) / 255);
                            byte b5 = (byte)((b * 31) / 255);

                            ushort argb1555 = (ushort)((a1 << 15) | (r5 << 10) | (g5 << 5) | b5);
                            buf[index++] = (byte)(argb1555 & 0xFF);
                            buf[index++] = (byte)((argb1555 >> 8) & 0xFF);
                        }
                    }
                }
                return buf;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        private static byte[] GetPixelDataFormat513(Bitmap bmp)
        {
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] buf = new byte[bmp.Width * bmp.Height * 2];
                int index = 0;
                unsafe
                {
                    byte* scan0 = (byte*)bmpData.Scan0;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        byte* row = scan0 + y * bmpData.Stride;
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            int pixel = *(int*)(row + x * 4);
                            byte b = (byte)((pixel >> 0) & 0xFF);
                            byte g = (byte)((pixel >> 8) & 0xFF);
                            byte r = (byte)((pixel >> 16) & 0xFF);

                            byte r5 = (byte)((r * 31) / 255);
                            byte g6 = (byte)((g * 63) / 255);
                            byte b5 = (byte)((b * 31) / 255);

                            ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);
                            buf[index++] = (byte)(rgb565 & 0xFF);
                            buf[index++] = (byte)((rgb565 >> 8) & 0xFF);
                        }
                    }
                }
                return buf;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        private static byte[] GetPixelDataFormat517(Bitmap bmp)
        {
            int blockSize = 16;
            int blockCountX = (bmp.Width + blockSize - 1) / blockSize;
            int blockCountY = (bmp.Height + blockSize - 1) / blockSize;
            byte[] buf = new byte[blockCountX * blockCountY * 2];

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int index = 0;
                unsafe
                {
                    byte* scan0 = (byte*)bmpData.Scan0;
                    for (int by = 0; by < blockCountY; by++)
                    {
                        for (int bx = 0; bx < blockCountX; bx++)
                        {
                            int x = bx * blockSize;
                            int y = by * blockSize;
                            if (x < bmp.Width && y < bmp.Height)
                            {
                                byte* row = scan0 + y * bmpData.Stride;
                                int pixel = *(int*)(row + x * 4);
                                byte b = (byte)((pixel >> 0) & 0xFF);
                                byte g = (byte)((pixel >> 8) & 0xFF);
                                byte r = (byte)((pixel >> 16) & 0xFF);

                                byte r5 = (byte)((r * 31) / 255);
                                byte g6 = (byte)((g * 63) / 255);
                                byte b5 = (byte)((b * 31) / 255);

                                ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);
                                buf[index++] = (byte)(rgb565 & 0xFF);
                                buf[index++] = (byte)((rgb565 >> 8) & 0xFF);
                            }
                            else
                            {
                                buf[index++] = 0;
                                buf[index++] = 0;
                            }
                        }
                    }
                }
                return buf;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }
        #endregion

        #region DXT3_DXT5 Encode
        /// <summary>
        /// DXT3 Compression
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        private static byte[] CompressDXT3(Bitmap bmp)
        {
            if (bmp.Width % 4 != 0 || bmp.Height % 4 != 0)
                throw new ArgumentException("DXT3 compression requires width and height to be multiples of 4.");
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int blockCountX = bmp.Width / 4;
                int blockCountY = bmp.Height / 4;
                byte[] buf = new byte[blockCountX * blockCountY * 16]; // 16 bytes per 4x4 block
                int bufIndex = 0;
                unsafe
                {
                    byte* scan0 = (byte*)bmpData.Scan0;
                    Color[] block = new Color[16]; // Reuse block buffer for all blocks
                    for (int by = 0; by < blockCountY; by++)
                    {
                        for (int bx = 0; bx < blockCountX; bx++)
                        {
                            // Extract 4x4 block pixels
                            for (int j = 0; j < 4; j++)
                            {
                                int y = by * 4 + j;
                                byte* row = scan0 + y * bmpData.Stride;
                                for (int i = 0; i < 4; i++)
                                {
                                    int x = bx * 4 + i;
                                    int pixel = *(int*)(row + x * 4);
                                    byte b = (byte)(pixel & 0xFF);
                                    byte g = (byte)((pixel >> 8) & 0xFF);
                                    byte r = (byte)((pixel >> 16) & 0xFF);
                                    byte a = (byte)((pixel >> 24) & 0xFF);
                                    block[j * 4 + i] = Color.FromArgb(a, r, g, b);
                                }
                            }

                            // Compress alpha (4 bits per pixel, 8 bytes total)
                            for (int i = 0; i < 16; i += 2)
                            {
                                byte a0 = (byte)(block[i].A >> 4);     // Low nibble
                                byte a1 = (byte)(block[i + 1].A >> 4); // High nibble
                                buf[bufIndex++] = (byte)((a1 << 4) | a0);
                            }

                            // Compress color (DXT1 style: 2 RGB565 colors + 4 indices)
                            Color[] colors = CompressBlockColors(block, out ushort c0, out ushort c1);
                            byte[] indices = ComputeColorIndices(block, colors);

                            // Write color data
                            buf[bufIndex++] = (byte)(c0 & 0xFF);
                            buf[bufIndex++] = (byte)(c0 >> 8);
                            buf[bufIndex++] = (byte)(c1 & 0xFF);
                            buf[bufIndex++] = (byte)(c1 >> 8);
                            for (int i = 0; i < 4; i++)
                            {
                                buf[bufIndex++] = indices[i];
                            }
                        }
                    }
                }
                return buf;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        /// <summary>
        /// DXT5 compression (Format2050). Encodes BGRA8888 pixel data into DXT5-compressed format.
        /// </summary>
        /// <param name="bmp">Source bitmap (Format32bppArgb)</param>
        /// <returns>DXT5 compressed byte array</returns>
        /// <exception cref="ArgumentException">Thrown if width or height is not a multiple of 4</exception>
        private static byte[] GetPixelDataFormat2050(Bitmap bmp)
        {
            if (bmp.Width % 4 != 0 || bmp.Height % 4 != 0)
                throw new ArgumentException("DXT5 compression requires width and height to be multiples of 4.");
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int blockCountX = bmp.Width / 4;
                int blockCountY = bmp.Height / 4;
                byte[] buf = new byte[blockCountX * blockCountY * 16];
                int bufIndex = 0;
                unsafe
                {
                    byte* scan0 = (byte*)bmpData.Scan0;
                    Color[] block = new Color[16]; // Reuse block buffer for all blocks
                    for (int by = 0; by < blockCountY; by++)
                    {
                        for (int bx = 0; bx < blockCountX; bx++)
                        {
                            // Extract 4x4 block pixels
                            for (int j = 0; j < 4; j++)
                            {
                                int y = by * 4 + j;
                                byte* row = scan0 + y * bmpData.Stride;
                                for (int i = 0; i < 4; i++)
                                {
                                    int x = bx * 4 + i;
                                    int pixel = *(int*)(row + x * 4);
                                    byte b = (byte)(pixel & 0xFF);
                                    byte g = (byte)((pixel >> 8) & 0xFF);
                                    byte r = (byte)((pixel >> 16) & 0xFF);
                                    byte a = (byte)((pixel >> 24) & 0xFF);
                                    block[j * 4 + i] = Color.FromArgb(a, r, g, b);
                                }
                            }

                            // Compress alpha
                            byte a0, a1;
                            int[] alphaIndices = CompressBlockAlphaDXT5(block, out a0, out a1);
                            buf[bufIndex++] = a0;
                            buf[bufIndex++] = a1;
                            long flags = 0; // 48-bit value for 16 3-bit indices
                            for (int i = 0; i < 16; i++)
                            {
                                flags |= (long)alphaIndices[i] << (i * 3);
                            }
                            buf[bufIndex++] = (byte)(flags & 0xFF);
                            buf[bufIndex++] = (byte)((flags >> 8) & 0xFF);
                            buf[bufIndex++] = (byte)((flags >> 16) & 0xFF);
                            buf[bufIndex++] = (byte)((flags >> 24) & 0xFF);
                            buf[bufIndex++] = (byte)((flags >> 32) & 0xFF);
                            buf[bufIndex++] = (byte)((flags >> 40) & 0xFF);

                            // Compress color
                            Color[] colors = CompressBlockColors(block, out ushort c0, out ushort c1);
                            byte[] indices = ComputeColorIndices(block, colors);

                            // Write color data
                            buf[bufIndex++] = (byte)(c0 & 0xFF);
                            buf[bufIndex++] = (byte)(c0 >> 8);
                            buf[bufIndex++] = (byte)(c1 & 0xFF);
                            buf[bufIndex++] = (byte)(c1 >> 8);
                            for (int i = 0; i < 4; i++)
                            {
                                buf[bufIndex++] = indices[i];
                            }
                        }
                    }
                }
                return buf;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        /// <summary>
        /// Compress 4x4 block colors to DXT1 format (used by both DXT3 and DXT5)
        /// </summary>
        /// <param name="block"></param>
        /// <param name="c0"></param>
        /// <param name="c1"></param>
        /// <returns></returns>
        private static Color[] CompressBlockColors(Color[] block, out ushort c0, out ushort c1)
        {
            // Simple min/max quantization for RGB
            int minR = 255, minG = 255, minB = 255;
            int maxR = 0, maxG = 0, maxB = 0;
            foreach (var color in block)
            {
                minR = Math.Min(minR, color.R);
                minG = Math.Min(minG, color.G);
                minB = Math.Min(minB, color.B);
                maxR = Math.Max(maxR, color.R);
                maxG = Math.Max(maxG, color.G);
                maxB = Math.Max(maxB, color.B);
            }

            // Convert to RGB565
            c0 = ColorToRGB565((byte)maxR, (byte)maxG, (byte)maxB);
            c1 = ColorToRGB565((byte)minR, (byte)minG, (byte)minB);

            // Generate color table (same as decompression)
            Color[] colors = new Color[4];
            ExpandColorTable(colors, c0, c1);
            return colors;
        }

        private static ushort ColorToRGB565(byte r, byte g, byte b)
        {
            byte r5 = (byte)((r * 31) / 255);
            byte g6 = (byte)((g * 63) / 255);
            byte b5 = (byte)((b * 31) / 255);
            return (ushort)((r5 << 11) | (g6 << 5) | b5);
        }

        private static byte[] ComputeColorIndices(Color[] block, Color[] colors)
        {
            byte[] indices = new byte[4]; // 4 bytes, one per row
            for (int j = 0; j < 4; j++)
            {
                byte row = 0;
                for (int i = 0; i < 4; i++)
                {
                    Color pixel = block[j * 4 + i];
                    int bestIndex = 0;
                    double minDist = double.MaxValue;
                    for (int k = 0; k < 4; k++)
                    {
                        double dist = ColorDistance(pixel, colors[k]);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            bestIndex = k;
                        }
                    }
                    row |= (byte)(bestIndex << (i * 2));
                }
                indices[j] = row;
            }
            return indices;
        }

        private static double ColorDistance(Color c1, Color c2)
        {
            int dr = c1.R - c2.R;
            int dg = c1.G - c2.G;
            int db = c1.B - c2.B;
            return dr * dr + dg * dg + db * db; // Simple Euclidean distance (RGB only)
        }

        /// <summary>
        /// Compress 4x4 block alpha for DXT5
        /// </summary>
        /// <param name="block"></param>
        /// <param name="a0"></param>
        /// <param name="a1"></param>
        /// <returns></returns>
        private static int[] CompressBlockAlphaDXT5(Color[] block, out byte a0, out byte a1)
        {
            // Find min/max alpha
            byte minA = 255, maxA = 0;
            foreach (var color in block)
            {
                minA = Math.Min(minA, color.A);
                maxA = Math.Max(maxA, color.A);
            }

            a0 = maxA;
            a1 = minA;

            byte[] alphaTable = new byte[8];
            ExpandAlphaTableDXT5(alphaTable, a0, a1);

            int[] indices = new int[16];
            for (int i = 0; i < 16; i++)
            {
                byte alpha = block[i].A;
                int bestIndex = 0;
                int minDiff = int.MaxValue;
                for (int j = 0; j < 8; j++)
                {
                    int diff = Math.Abs(alpha - alphaTable[j]);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestIndex = j;
                    }
                }
                indices[i] = bestIndex;
            }
            return indices;
        }
        #endregion
    }
}
