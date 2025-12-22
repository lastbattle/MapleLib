/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
 * 2018 - 2025, lastbattle
   
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
using System.Linq;
using System.Runtime.InteropServices;
using MapleLib.Helpers;
using MapleLib.WzLib.Util;
using Microsoft.Xna.Framework.Graphics;

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
            WzPngProperty clone = new()
            {
                PNG = GetImage(false)
            };
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
                    //were removing the reference to compressedBytes, so a backup for the ret value is needed
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

        internal static byte[] Decompress(byte[] compressedBuffer, int decompressedSize)
        {
            using (MemoryStream memStream = new())
            {
                memStream.Write(compressedBuffer, 2, compressedBuffer.Length - 2);
                byte[] buffer = new byte[decompressedSize];
                memStream.Position = 0;

                using (DeflateStream zip = new(memStream, CompressionMode.Decompress))
                {
                    zip.Read(buffer, 0, buffer.Length);
                    return buffer;
                }
            }
        }

        internal static byte[] Compress(byte[] decompressedBuffer)
        {
            using (MemoryStream memStream = new())
            {
                using (DeflateStream zip = new(memStream, CompressionMode.Compress, true))
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
                Bitmap bmp = new(width, height, Format.GetPixelFormat());
                Rectangle rect_ = new(0, 0, width, height);

                switch (Format)
                {
                    case WzPngFormat.Format1:
                        {
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            PngUtility.DecompressImage_PixelDataBgra4444(rawBytes.AsSpan(), width, height, bmp, bmpData);
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

                            PngUtility.DecompressImageDXT3(rawBytes, width, height, bmpData3); // FullPath = "Map.wz\\Back\\blackHeaven.img\\back\\98"
                            bmp.UnlockBits(bmpData3);
                            break;
                        }
                    case WzPngFormat.Format257: // http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/index2.html#post9053713
                        {
                            BitmapData bmpData = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);
                            // "Npc.wz\\2570101.img\\info\\illustration2\\face\\0"

                            PngUtility.CopyBmpDataWithStride(rawBytes, bmp.Width * 2, bmpData);

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

                            PngUtility.DecompressImage_PixelDataForm517(rawBytes, width, height, bmp, bmpData); // FullPath = "Map.wz\\Back\\midForest.img\\back\\0"
                            bmp.UnlockBits(bmpData);
                            break;
                        }
                    case WzPngFormat.Format1026:
                        {
                            BitmapData bmpData1026 = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            PngUtility.DecompressImageDXT3(rawBytes, width, height, bmpData1026);
                            bmp.UnlockBits(bmpData1026);
                            break;
                        }
                    case WzPngFormat.Format2050: // new
                        {
                            BitmapData bmpData2050 = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                            PngUtility.DecompressImageDXT5(rawBytes, width, height, bmpData2050);

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
            return null;
        }

        internal void CompressPng(Bitmap bmp)
        {
            width = bmp.Width;
            height = bmp.Height;

            // Lock the bitmap to access pixel data directly, improving performance over GetPixel
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixels;
            try
            {
                int expectedSize = bmp.Width * bmp.Height * 4;
                pixels = new byte[expectedSize];

                // Handle stride properly - copy row by row if stride doesn't match expected width
                if (bmpData.Stride == bmp.Width * 4)
                {
                    // No padding, direct copy
                    Marshal.Copy(bmpData.Scan0, pixels, 0, expectedSize);
                }
                else
                {
                    // Copy row by row to remove stride padding
                    int rowSize = bmp.Width * 4;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        IntPtr rowPtr = bmpData.Scan0 + y * bmpData.Stride;
                        Marshal.Copy(rowPtr, pixels, y * rowSize, rowSize);
                    }
                }
            }
            finally
            {
                // Ensure the bitmap is unlocked even if an exception occurs
                bmp.UnlockBits(bmpData);
            }

            ////// Automatically detect the suitable format for each image. See UnitTest_WzFile/UnitTest_MapleLib.cs/TestImageSurfaceFormatDetection
            SurfaceFormat suggested_surfaceFormat = ImageFormatDetector.DetermineTextureFormat(pixels, bmp.Width, bmp.Height);
            //Debug.WriteLine(string.Format("Suggested SurfaceFormat: {0}", suggested_surfaceFormat.ToString()));

            ////// Optimise the image size
            // Create an EncoderParameters object to specify the PNG encoder and the desired compression level
            //EncoderParameters encoderParameters = new(1);
            //encoderParameters.Param[0] = new EncoderParameter(Encoder.Compression, (byte) CompressionLevel.Optimal);
            // Get the PNG codec information
            //ImageCodecInfo pngCodec = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Png.Guid);
            // Save the compressed image
            /*Bitmap newBitmap;
            using (MemoryStream stream = new MemoryStream())
            {
                bmp.Save(stream, pngCodec, encoderParameters);

                newBitmap = new Bitmap(stream);
            }*/

            (WzPngFormat format, byte[] compressedBytes) = PngUtility.CompressImageToPngFormat(bmp, suggested_surfaceFormat);

            this.Format = format;
            this.compressedImageBytes = Compress(compressedBytes);

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