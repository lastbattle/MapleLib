/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
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
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains the information for a bitmap
    /// https://docs.microsoft.com/en-us/windows/win32/direct3d9/compressed-texture-resources
    /// http://www.sjbrown.co.uk/2006/01/19/dxt-compression-techniques/
    /// https://en.wikipedia.org/wiki/S3_Texture_Compression
    /// </summary>
    public class WzPngProperty : WzImageProperty
    {
        #region Fields
        private int width, height;
        private WzPngFormat format;
        internal byte[] compressedImageBytes;
        internal Bitmap png;
        internal WzObject parent;
        //internal WzImage imgParent;
        internal bool listWzUsed = false;

        internal WzBinaryReader wzReader;
        internal long offs;
        #endregion

        #region Inherited Members
        public override void SetValue(object value)
        {
            if (value is Bitmap)
                PNG = (Bitmap)value;
            else compressedImageBytes = (byte[])value;
        }

        public override WzImageProperty DeepClone()
        {
            WzPngProperty clone = new WzPngProperty();
            clone.PNG = GetImage(false);
            return clone;
        }

        public override object WzValue { get { return GetImage(false); } }
        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get { return parent; } internal set { parent = value; } }
        /*/// <summary>
        /// The image that this property is contained in
        /// </summary>
        public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }*/
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name { get { return "PNG"; } set { } }
        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType { get { return WzPropertyType.PNG; } }
        public override void WriteValue(WzBinaryWriter writer)
        {
            throw new NotImplementedException("Cannot write a PngProperty");
        }
        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            compressedImageBytes = null;
            if (png != null)
            {
                png.Dispose();
                png = null;
            }
            //this.wzReader.Close(); // closes at WzFile
            this.wzReader = null;
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// The width of the bitmap
        /// </summary>
        public int Width { get { return width; } set { width = value; } }
        /// <summary>
        /// The height of the bitmap
        /// </summary>
        public int Height { get { return height; } set { height = value; } }
        /// <summary>
        /// The format of the bitmap
        /// </summary>
        public WzPngFormat Format
        {
            get => format;
            set => format = value;
        }

        public bool ListWzUsed
        {
            get
            {
                return listWzUsed;
            }
            set
            {
                if (value != listWzUsed)
                {
                    listWzUsed = value;
                    CompressPng(GetImage(false));
                }
            }
        }
        /// <summary>
        /// The actual bitmap
        /// </summary>
        public Bitmap PNG
        {
            set
            {
                this.png = value;
                CompressPng(this.png);
            }
        }

        /// <summary>
        /// Creates a blank WzPngProperty
        /// </summary>
        public WzPngProperty() { }

        /// <summary>
        /// Creates a blank WzPngProperty 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="parseNow"></param>
        internal WzPngProperty(WzBinaryReader reader, bool parseNow)
        {
            // Read compressed bytes
            width = reader.ReadCompressedInt();
            height = reader.ReadCompressedInt();

            int format1 = reader.ReadCompressedInt();
            int format2 = reader.ReadCompressedInt();
            // Reconstruct the original format using bit shifting
            format = (WzPngFormat)(format1 + (format2 << 8));

            reader.BaseStream.Position += 4;
            offs = reader.BaseStream.Position;
            int len = reader.ReadInt32() - 1;
            reader.BaseStream.Position += 1;

            lock (reader) // lock WzBinaryReader, allowing it to be loaded from multiple threads at once
            {
                if (len > 0)
                {
                    if (parseNow)
                    {
                        if (wzReader == null) // when saving the WZ file to a new encryption
                        {
                            compressedImageBytes = reader.ReadBytes(len);
                        }
                        else // when opening the Wz property
                        {
                            compressedImageBytes = wzReader.ReadBytes(len);
                        }
                        ParsePng(true);
                    }
                    else
                        reader.BaseStream.Position += len;
                }
                this.wzReader = reader;
            }
        }
        #endregion

        #region Parsing Methods
        public byte[] GetCompressedBytes(bool saveInMemory)
        {
            if (compressedImageBytes == null)
            {
                lock (wzReader)// lock WzBinaryReader, allowing it to be loaded from multiple threads at once
                {
                    long pos = this.wzReader.BaseStream.Position;
                    this.wzReader.BaseStream.Position = offs;
                    int len = this.wzReader.ReadInt32() - 1;
                    if (len <= 0) // possibility an image written with the wrong wzIv 
                        throw new Exception("The length of the image is negative. WzPngProperty. Wrong WzIV?");

                    this.wzReader.BaseStream.Position += 1;

                    if (len > 0)
                        compressedImageBytes = this.wzReader.ReadBytes(len);
                    this.wzReader.BaseStream.Position = pos;
                }

                if (!saveInMemory)
                {
                    //were removing the referance to compressedBytes, so a backup for the ret value is needed
                    byte[] returnBytes = compressedImageBytes;
                    compressedImageBytes = null;
                    return returnBytes;
                }
            }
            return compressedImageBytes;
        }

        public Bitmap GetImage(bool saveInMemory)
        {
            if (png == null)
            {
                ParsePng(saveInMemory);
            }
            return png;
        }

        internal byte[] Decompress(byte[] compressedBuffer, int decompressedSize)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                memStream.Write(compressedBuffer, 2, compressedBuffer.Length - 2);
                byte[] buffer = new byte[decompressedSize];
                memStream.Position = 0;

                using (DeflateStream zip = new DeflateStream(memStream, CompressionMode.Decompress))
                {
                    zip.Read(buffer, 0, buffer.Length);
                    return buffer;
                }
            }
        }

        internal byte[] Compress(byte[] decompressedBuffer)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                using (DeflateStream zip = new DeflateStream(memStream, CompressionMode.Compress, true))
                {
                    zip.Write(decompressedBuffer, 0, decompressedBuffer.Length);
                }
                memStream.Position = 0;
                byte[] buffer = new byte[memStream.Length + 2];
                memStream.Read(buffer, 2, buffer.Length - 2);

                System.Buffer.BlockCopy(new byte[] { 0x78, 0x9C }, 0, buffer, 0, 2);

                return buffer;
            }
        }

        public void ParsePng(bool saveInMemory, Texture2D texture2d = null)
        {
            byte[] rawBytes = GetRawImage(saveInMemory);
            if (rawBytes == null)
            {
                png = null;
                return;
            }
            try
            {
                Bitmap bmp = new Bitmap(width, height, Format.GetPixelFormat());
                Rectangle rect_ = new Rectangle(0, 0, width, height);

                switch (Format)
                {
                    case WzPngFormat.Format1:
                        {
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            DecompressImage_PixelDataBgra4444(rawBytes.AsSpan(), width, height, bmp, bmpData);
                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case WzPngFormat.Format2:
                        {
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            Marshal.Copy(rawBytes, 0, bmpData.Scan0, rawBytes.Length);
                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case WzPngFormat.Format3:
                        {
                            // New format 黑白缩略图
                            // thank you Elem8100, http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/ 
                            // you'll be remembered forever <3 
                            BitmapData bmpData3 = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            DecompressImageDXT3(rawBytes, width, height, bmpData3); // FullPath = "Map.wz\\Back\\blackHeaven.img\\back\\98"
                            bmp.UnlockBits(bmpData3);
                            break;
                        }
                    case WzPngFormat.Format257: // http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/index2.html#post9053713
                        {
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);
                            // "Npc.wz\\2570101.img\\info\\illustration2\\face\\0"

                            CopyBmpDataWithStride(rawBytes, bmp.Width * 2, bmpData);

                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case WzPngFormat.Format513: // nexon wizet logo
                        {
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);

                            Marshal.Copy(rawBytes, 0, bmpData.Scan0, rawBytes.Length);
                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case WzPngFormat.Format517:
                        {
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);

                            DecompressImage_PixelDataForm517(rawBytes, width, height, bmp, bmpData); // FullPath = "Map.wz\\Back\\midForest.img\\back\\0"
                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case WzPngFormat.Format1026:
                        {
                            BitmapData bmpData1026 = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            DecompressImageDXT3(rawBytes, width, height, bmpData1026);
                            bmp.UnlockBits(bmpData1026);
                            break;
                        }
                    case WzPngFormat.Format2050: // new
                        {
                            BitmapData bmpData2050 = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                            DecompressImageDXT5(rawBytes, width, height, bmpData2050);

                            bmp.UnlockBits(bmpData2050);
                            break;
                        }
                    default:
                        Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature, $"Unknown PNG format {Format}");
                        break;
                }
                if (bmp != null)
                {
                    if (texture2d != null)
                    {
                        Microsoft.Xna.Framework.Rectangle rect = new Microsoft.Xna.Framework.Rectangle(Microsoft.Xna.Framework.Point.Zero,
                            new Microsoft.Xna.Framework.Point(width, height));
                        texture2d.SetData(0, 0, rect, rawBytes, 0, rawBytes.Length);
                    }
                }

                png = bmp;
            }
            catch (InvalidDataException)
            {
                png = null;
            }
        }

        /// <summary>
        /// Parses the raw image bytes from WZ
        /// </summary>
        /// <returns></returns>
        internal byte[] GetRawImage(bool saveInMemory)
        {
            byte[] rawImageBytes = GetCompressedBytes(saveInMemory);

            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(rawImageBytes)))
                {
                    DeflateStream zlib;

                    ushort header = reader.ReadUInt16();
                    listWzUsed = header != 0x9C78 && header != 0xDA78 && header != 0x0178 && header != 0x5E78;
                    if (!listWzUsed)
                    {
                        zlib = new DeflateStream(reader.BaseStream, CompressionMode.Decompress);
                    }
                    else
                    {
                        reader.BaseStream.Position -= 2;
                        MemoryStream dataStream = new MemoryStream();
                        int blocksize = 0;
                        int endOfPng = rawImageBytes.Length;

                        // Read image into zlib
                        while (reader.BaseStream.Position < endOfPng)
                        {
                            blocksize = reader.ReadInt32();
                            for (int i = 0; i < blocksize; i++)
                            {
                                dataStream.WriteByte((byte)(reader.ReadByte() ^ ParentImage.reader.WzKey[i]));
                            }
                        }
                        dataStream.Position = 2;
                        zlib = new DeflateStream(dataStream, CompressionMode.Decompress);
                    }

                    int uncompressedSize = 0;
                    byte[] decBuf = null;

                    switch (Format)
                    {
                        case WzPngFormat.Format1: // 0x1
                            {
                                uncompressedSize = width * height * 2;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case WzPngFormat.Format2: // 0x2
                            {
                                uncompressedSize = width * height * 4;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case WzPngFormat.Format3: // 0x2 + 1?
                            {
                                // New format 黑白缩略图
                                // thank you Elem8100, http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/ 
                                // you'll be remembered forever <3 

                                uncompressedSize = width * height * 4;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case WzPngFormat.Format257: // 0x100 + 1?
                            {
                                // http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/index2.html#post9053713
                                // "Npc.wz\\2570101.img\\info\\illustration2\\face\\0"

                                uncompressedSize = width * height * 2;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case WzPngFormat.Format513: // 0x200 nexon wizet logo
                            {
                                uncompressedSize = width * height * 2;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case WzPngFormat.Format517: // 0x200 + 5
                            {
                                uncompressedSize = width * height / 128;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case WzPngFormat.Format1026: // 0x400 + 2?
                            {
                                uncompressedSize = width * height * 4;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        case WzPngFormat.Format2050: // 0x800 + 2? new
                            {
                                uncompressedSize = width * height;
                                decBuf = new byte[uncompressedSize];
                                break;
                            }
                        default:
                            Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature, string.Format("Unknown PNG format {0}", Format));
                            break;
                    }

                    if (decBuf != null)
                    {
                        using (zlib)
                        {
                            // https://learn.microsoft.com/en-us/dotnet/api/System.IO.Compression.DeflateStream.Read?view=net-8.0#system-io-compression-deflatestream-read(system-byte()-system-int32-system-int32)
                            // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/partial-byte-reads-in-streams
                            int totalRead = 0;
                            while (totalRead < decBuf.Length)
                            {
                                int bytesRead = zlib.Read(decBuf, totalRead, decBuf.Length - totalRead);
                                if (bytesRead == 0) break;
                                totalRead += bytesRead;
                            }
                            return decBuf;
                        }
                    }
                }
            }
            catch (InvalidDataException)
            {
            }
            return null;
        }

        #region Decoders
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

            Span<byte> decoded = outputSize <= WzBinaryReader.STACKALLOC_SIZE_LIMIT_L1
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
            int blockCountX = width / 4;
            int blockCountY = height / 4;

            if (Sse2.IsSupported)
            {
                // Parallelize across rows of blocks to leverage multiple CPU cores
                Parallel.For(0, blockCountY, y =>
                {
                    for (int x = 0; x < blockCountX; x += 2) // Process 2 blocks at a time
                    {
                        int offset = (y * blockCountX + x) * 16;
                        if (offset + 32 <= rawData.Length)
                        {
                            // Alpha for block 1
                            byte a0_1 = rawData[offset];
                            byte a1_1 = rawData[offset + 1];
                            byte[] alphaTable1 = new byte[8];
                            ExpandAlphaTableDXT5(alphaTable1, a0_1, a1_1);
                            int[] alphaIdxTable1 = new int[16];
                            ExpandAlphaIndexTableDXT5(alphaIdxTable1, rawData, offset + 2);

                            // Colors for block 1
                            ushort c0_1 = BitConverter.ToUInt16(rawData, offset + 8);
                            ushort c1_1 = BitConverter.ToUInt16(rawData, offset + 10);
                            Color[] colors1 = new Color[4];
                            ExpandColorTable(colors1, c0_1, c1_1);
                            int[] colorIndices1 = new int[16];
                            ExpandColorIndexTable(colorIndices1, rawData, offset + 12);

                            // Process block 1 pixels
                            for (int j = 0; j < 4; j++)
                            {
                                for (int i = 0; i < 4; i++)
                                {
                                    int pixelOffset = ((y * 4 + j) * width + (x * 4 + i)) * 4;
                                    Color c = colors1[colorIndices1[j * 4 + i]];
                                    byte alpha = alphaTable1[alphaIdxTable1[j * 4 + i]];
                                    pDecoded[pixelOffset] = c.B;
                                    pDecoded[pixelOffset + 1] = c.G;
                                    pDecoded[pixelOffset + 2] = c.R;
                                    pDecoded[pixelOffset + 3] = alpha;
                                }
                            }

                            // Repeat for block 2 if within bounds
                            if (x + 1 < blockCountX)
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

                                for (int j = 0; j < 4; j++)
                                {
                                    for (int i = 0; i < 4; i++)
                                    {
                                        int pixelOffset = ((y * 4 + j) * width + ((x + 1) * 4 + i)) * 4;
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
                for (int y = 0; y < height; y += 4)
                {
                    for (int x = 0; x < width; x += 4)
                    {
                        int off = (y * width + x) * 4 / 4;
                        byte[] alphaTable = new byte[8];
                        ExpandAlphaTableDXT5(alphaTable, rawData[off + 0], rawData[off + 1]);
                        int[] alphaIdxTable = new int[16];
                        ExpandAlphaIndexTableDXT5(alphaIdxTable, rawData, off + 2);
                        ushort u0 = BitConverter.ToUInt16(rawData, off + 8);
                        ushort u1 = BitConverter.ToUInt16(rawData, off + 10);
                        Color[] colorTable = new Color[4];
                        ExpandColorTable(colorTable, u0, u1);
                        int[] colorIdxTable = new int[16];
                        ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                int pixelOffset = ((y + j) * width + (x + i)) * 4;
                                Color c = colorTable[colorIdxTable[j * 4 + i]];
                                pDecoded[pixelOffset] = c.B;
                                pDecoded[pixelOffset + 1] = c.G;
                                pDecoded[pixelOffset + 2] = c.R;
                                pDecoded[pixelOffset + 3] = alphaTable[alphaIdxTable[j * 4 + i]];
                            }
                        }
                    }
                }
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
        private static void CopyBmpDataWithStride(byte[] source, int stride, BitmapData bmpData)
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

        #region DXT1 Color
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

        #region DXT3/DXT5 Alpha
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

        internal void CompressPng(Bitmap bmp)
        {
            Format = WzPngFormat.Format2; // Default to BGRA8888 for now. TODO: compression for every format
            width = bmp.Width;
            height = bmp.Height;

            // Lock the bitmap to access pixel data directly, improving performance over GetPixel
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                // TODO: Automatically detect the suitable format for each image. See UnitTest_WzFile/UnitTest_MapleLib.cs/TestImageSurfaceFormatDetection

                int length = bmp.Width * bmp.Height * 4; // 4 bytes per pixel (BGRA)
                byte[] buf = new byte[length];

                // Check if stride matches expected width * 4 (no padding)
                if (bmpData.Stride == bmp.Width * 4)
                {
                    // Single copy for the entire bitmap data
                    Marshal.Copy(bmpData.Scan0, buf, 0, length);
                }
                else
                {
                    // Copy row by row to handle padding in stride
                    int stride = bmpData.Stride;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        IntPtr rowPtr = bmpData.Scan0 + y * stride;
                        Marshal.Copy(rowPtr, buf, y * bmp.Width * 4, bmp.Width * 4);
                    }
                }

                compressedImageBytes = Compress(buf);
            }
            finally
            {
                // Ensure the bitmap is unlocked even if an exception occurs
                bmp.UnlockBits(bmpData);
            }

            if (listWzUsed)
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    using (WzBinaryWriter writer = new WzBinaryWriter(memStream, WzTool.GetIvByMapleVersion(WzMapleVersion.GMS)))
                    {
                        writer.Write(2);
                        for (int i = 0; i < 2; i++)
                        {
                            writer.Write((byte)(compressedImageBytes[i] ^ writer.WzKey[i]));
                        }
                        writer.Write(compressedImageBytes.Length - 2);
                        for (int i = 2; i < compressedImageBytes.Length; i++)
                            writer.Write((byte)(compressedImageBytes[i] ^ writer.WzKey[i - 2]));
                        compressedImageBytes = memStream.ToArray();
                    }
                }
            }
        }
        #endregion

        #region Cast Values

        public override Bitmap GetBitmap()
        {
            return GetImage(false);
        }
        #endregion
    }
}