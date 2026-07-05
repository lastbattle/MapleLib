
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
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
        private readonly object imageLock = new();
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
                width = this.width,
                height = this.height,
                format = this.format,
                listWzUsed = this.listWzUsed
            };

            // Try to copy compressed bytes directly (most efficient)
            if (compressedImageBytes != null)
            {
                clone.compressedImageBytes = (byte[])compressedImageBytes.Clone();
            }
            else if (wzReader != null)
            {
                // Try to get compressed bytes from reader
                try
                {
                    byte[] bytes = GetCompressedBytes(true);
                    if (bytes != null)
                    {
                        clone.compressedImageBytes = (byte[])bytes.Clone();
                    }
                    else
                    {
                        Debug.WriteLine($"[WzPngProperty.DeepClone] GetCompressedBytes returned null for {width}x{height} {format}");
                    }
                }
                catch (Exception ex)
                {
                    // Fall back to bitmap copy if reader fails
                    Debug.WriteLine($"[WzPngProperty.DeepClone] Reader failed, falling back to bitmap: {ex.Message}");
                    var bmp = GetImage(false);
                    if (bmp != null)
                    {
                        clone.PNG = (Bitmap)bmp.Clone();
                    }
                }
            }
            else if (png != null)
            {
                // Copy existing bitmap - this will re-compress!
                Debug.WriteLine($"[WzPngProperty.DeepClone] No compressed bytes or reader, using bitmap copy for {width}x{height}");
                clone.PNG = (Bitmap)png.Clone();
            }
            else
            {
                Debug.WriteLine($"[WzPngProperty.DeepClone] WARNING: No data available for {width}x{height} {format}");
            }

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
                lock (imageLock)
                {
                    return listWzUsed;
                }
            }
            set
            {
                lock (imageLock)
                {
                    if (value != listWzUsed)
                    {
                        listWzUsed = value;
                        CompressPng(GetImage(false));
                    }
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
                lock (imageLock)
                {
                    if (png != null && !ReferenceEquals(png, value))
                    {
                        png.Dispose();
                    }

                    CompressPng(value);
                    png = null;
                }
            }
        }

        /// <summary>
        /// Creates a blank WzPngProperty
        /// </summary>
        public WzPngProperty() { }

        /// <summary>
        /// Sets the compressed image bytes directly, along with dimensions and format.
        /// This allows copying image data from one canvas to another without decompression/recompression.
        /// </summary>
        /// <param name="bytes">The compressed image bytes</param>
        /// <param name="width">Image width</param>
        /// <param name="height">Image height</param>
        /// <param name="format">Image format</param>
        public void SetCompressedBytes(byte[] bytes, int width, int height, WzPngFormat format)
        {
            lock (imageLock)
            {
                this.compressedImageBytes = bytes;
                this.width = width;
                this.height = height;
                this.format = format;

                // Clear any cached bitmap since we're replacing the data
                if (this.png != null)
                {
                    this.png.Dispose();
                    this.png = null;
                }

                // Clear reader reference since we now have the data in memory
                this.wzReader = null;
            }
        }

        /// <summary>
        /// Creates a blank WzPngProperty 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="parseNow"></param>
        internal WzPngProperty(WzBinaryReader reader, bool parseNow)
        {
            // Keep reader available during eager parse.
            // In ParseEverything mode we decode inside this constructor, before Parent is assigned.
            // list.wz-formatted PNGs require WzKey from reader, so set it upfront.
            this.wzReader = reader;

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
            }
        }
        #endregion

        #region Parsing Methods
        public byte[] GetCompressedBytes(bool saveInMemory)
        {
            lock (imageLock)
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
        }

        /// <summary>
        /// Gets compressed bytes in standard zlib format, converting from listWz format if necessary.
        /// This is used for IMG filesystem extraction to ensure PNG data can be read without
        /// the original WZ encryption key.
        /// </summary>
        /// <param name="saveInMemory">Whether to cache the bytes</param>
        /// <returns>Compressed bytes in standard zlib format</returns>
        public byte[] GetCompressedBytesForExtraction(bool saveInMemory)
        {
            byte[] rawBytes = GetCompressedBytes(saveInMemory);
            if (rawBytes == null || rawBytes.Length < 2)
                return rawBytes;

            // Check if this is listWz format (non-standard zlib header)
            ushort header = (ushort)(rawBytes[0] | (rawBytes[1] << 8));
            bool isListWzFormat = !IsStandardZlibHeader(header);

            if (!isListWzFormat)
                return rawBytes;

            // Convert listWz format to standard zlib format by XOR decrypting
            // and re-compressing the raw pixel data
            // Get the WzKey - prefer wzReader (set during parsing), fall back to ParentImage.reader
            var wzKey = this.wzReader?.WzKey ?? ParentImage?.reader?.WzKey;
            if (wzKey == null)
                return rawBytes; // Return as-is, may fail on read

            try
            {
                byte[] decryptedBytes = new byte[rawBytes.Length];
                int decryptedLength = DecryptListWzBlocks(rawBytes, wzKey, decryptedBytes);
                if (decryptedLength <= 2)
                {
                    return rawBytes;
                }

                int uncompressedSize = GetUncompressedSize();
                byte[] decompressed = new byte[uncompressedSize];
                using (MemoryStream decryptedStream = new MemoryStream(decryptedBytes, 2, decryptedLength - 2, writable: false))
                using (DeflateStream deflate = new DeflateStream(decryptedStream, CompressionMode.Decompress))
                {
                    ReadFully(deflate, decompressed);
                }

                using (MemoryStream outputStream = new MemoryStream(decompressed.Length))
                {
                    outputStream.WriteByte(0x78);
                    outputStream.WriteByte(0x9C);

                    using (DeflateStream deflateOut = new DeflateStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        deflateOut.Write(decompressed, 0, decompressed.Length);
                    }

                    return outputStream.ToArray();
                }
            }
            catch
            {
                return rawBytes; // Return original on failure
            }
        }

        /// <summary>
        /// Gets the uncompressed size based on format and dimensions
        /// </summary>
        private int GetUncompressedSize()
        {
            return Format.GetDecodedSize(width, height);
        }

        public Bitmap GetImage(bool saveInMemory)
        {
            lock (imageLock)
            {
                if (png != null)
                {
                    if (saveInMemory)
                    {
                        return png;
                    }

                    return (Bitmap)png.Clone();
                }

                Bitmap decodedBitmap = DecodeBitmap(saveInMemory);
                if (saveInMemory)
                {
                    png = decodedBitmap;
                }

                return decodedBitmap;
            }
        }

        internal static byte[] Decompress(byte[] compressedBuffer, int decompressedSize)
        {
            using (MemoryStream memStream = new MemoryStream(compressedBuffer, 2, compressedBuffer.Length - 2, writable: false))
            using (DeflateStream zip = new DeflateStream(memStream, CompressionMode.Decompress))
            {
                byte[] buffer = new byte[decompressedSize];
                ReadFully(zip, buffer);
                return buffer;
            }
        }

        internal static byte[] Compress(byte[] decompressedBuffer)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                memStream.WriteByte(0x78);
                memStream.WriteByte(0x9C);

                using (DeflateStream zip = new DeflateStream(memStream, CompressionMode.Compress, true))
                {
                    zip.Write(decompressedBuffer, 0, decompressedBuffer.Length);
                }

                return memStream.ToArray();
            }
        }

        public void ParsePng(bool saveInMemory, Texture2D texture2d = null)
        {
            lock (imageLock)
            {
                Bitmap decodedBitmap = DecodeBitmap(saveInMemory, texture2d);

                if (saveInMemory)
                {
                    png = decodedBitmap;
                }
                else if (decodedBitmap == null)
                {
                    png = null;
                }
            }
        }

        private Bitmap DecodeBitmap(bool saveInMemory, Texture2D texture2d = null)
        {
            byte[] rawBytes = GetRawImage(saveInMemory);
            if (rawBytes == null)
            {
                return null;
            }
            try
            {
                Bitmap bmp = new(width, height, Format.GetPixelFormat());
                Rectangle rect_ = new(0, 0, width, height);
                byte[] textureBytes = rawBytes;

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

                            PngUtility.CopyBmpDataWithStride(rawBytes, bmp.Width * 2, bmpData);
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
                    case WzPngFormat.Format4098:
                        {
                            BitmapData bmpData4098 = bmp.LockBits(rect_, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                            try
                            {
                                if (texture2d == null)
                                {
                                    PngUtility.DecompressImageBC7(rawBytes, width, height, bmpData4098);
                                }
                                else
                                {
                                    textureBytes = PngUtility.DecompressImageBC7(rawBytes, width, height);
                                    PngUtility.CopyBmpDataWithStride(textureBytes, width * 4, bmpData4098);
                                }
                            }
                            finally
                            {
                                bmp.UnlockBits(bmpData4098);
                            }
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
                        texture2d.SetData(0, 0, rect, textureBytes, 0, textureBytes.Length);
                    }
                }

                return bmp;
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>
        /// Parses the raw image bytes from WZ
        /// </summary>
        /// <returns></returns>
        internal byte[] GetRawImage(bool saveInMemory)
        {
            byte[] rawImageBytes = GetCompressedBytes(saveInMemory);
            if (rawImageBytes == null || rawImageBytes.Length < 2)
            {
                return null;
            }

            ushort header = (ushort)(rawImageBytes[0] | (rawImageBytes[1] << 8));
            listWzUsed = !IsStandardZlibHeader(header);

            byte[] compressedBytes = rawImageBytes;
            int compressedLength = rawImageBytes.Length - 2;
            if (!listWzUsed)
            {
            }
            else
            {
                // Get the WzKey - prefer wzReader (set during parsing), fall back to ParentImage.reader
                var wzKey = this.wzReader?.WzKey ?? ParentImage?.reader?.WzKey;
                if (wzKey == null)
                {
                    throw new Exception("Cannot decrypt listWz format PNG - no WzKey available. " +
                        $"wzReader={this.wzReader != null}, ParentImage={ParentImage != null}");
                }

                byte[] decryptedBytes = new byte[rawImageBytes.Length];
                int decryptedLength = DecryptListWzBlocks(rawImageBytes, wzKey, decryptedBytes);
                if (decryptedLength <= 2)
                {
                    return null;
                }

                compressedBytes = decryptedBytes;
                compressedLength = decryptedLength - 2;
            }

                MemoryStream compressedStream = new MemoryStream(compressedBytes, 2, compressedLength, writable: false);
                DeflateStream zlib = new DeflateStream(compressedStream, CompressionMode.Decompress);
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

                            uncompressedSize = ((width + 3) / 4) * ((height + 3) / 4) * 16;
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
                            uncompressedSize = ((width + 3) / 4) * ((height + 3) / 4) * 16;
                            decBuf = new byte[uncompressedSize];
                            break;
                        }
                    case WzPngFormat.Format2050: // 0x800 + 2? new
                        {
                            uncompressedSize = ((width + 3) / 4) * ((height + 3) / 4) * 16;
                            decBuf = new byte[uncompressedSize];
                            break;
                        }
                    case WzPngFormat.Format4098: // 0x1000 + 2, BC7
                        {
                            uncompressedSize = ((width + 3) / 4) * ((height + 3) / 4) * 16;
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
            return null;
        }

        private static bool IsStandardZlibHeader(ushort header)
        {
            return header == 0x9C78 || header == 0xDA78 || header == 0x0178 || header == 0x5E78;
        }

        private static void ReadFully(Stream stream, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }
        }

        private static int DecryptListWzBlocks(byte[] source, WzMutableKey wzKey, byte[] destination)
        {
            int sourceOffset = 0;
            int destinationOffset = 0;
            while (sourceOffset < source.Length)
            {
                if (source.Length - sourceOffset < sizeof(int))
                {
                    throw new InvalidDataException("Invalid listWz PNG block header.");
                }

                int blockSize = BitConverter.ToInt32(source, sourceOffset);
                sourceOffset += sizeof(int);

                if (blockSize < 0 || blockSize > source.Length - sourceOffset)
                {
                    throw new InvalidDataException("Invalid listWz PNG block size.");
                }

                wzKey.EnsureKeySize(blockSize);
                for (int i = 0; i < blockSize; i++)
                {
                    destination[destinationOffset + i] = (byte)(source[sourceOffset + i] ^ wzKey[i]);
                }

                sourceOffset += blockSize;
                destinationOffset += blockSize;
            }

            return destinationOffset;
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
