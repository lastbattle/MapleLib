using System.Drawing.Imaging;
using Microsoft.Xna.Framework.Graphics;

using System;

namespace MapleLib.WzLib.WzProperties
{
    public static class WzPngFormatExtensions
    {
        // Keep decoded image allocations bounded before they reach a byte array
        // or GDI+ bitmap. Real WZ canvases are well below this limit.
        internal const int MaxDecodedSize = 256 * 1024 * 1024;
        internal const int MaxDimension = 32768;

        /// <summary>
        /// Gets the pixel format for the WZ PNG format
        /// </summary>
        public static PixelFormat GetPixelFormat(this WzPngFormat format)
        {
            return format switch
            {
                WzPngFormat.Format1 => PixelFormat.Format32bppArgb,
                WzPngFormat.Format2 => PixelFormat.Format32bppArgb,
                WzPngFormat.Format3 => PixelFormat.Format32bppArgb,
                WzPngFormat.Format257 => PixelFormat.Format16bppArgb1555,
                WzPngFormat.Format513 => PixelFormat.Format16bppRgb565,
                WzPngFormat.Format517 => PixelFormat.Format16bppRgb565,
                WzPngFormat.Format1026 => PixelFormat.Format32bppArgb,
                WzPngFormat.Format2050 => PixelFormat.Format32bppArgb,
                WzPngFormat.Format4098 => PixelFormat.Format32bppArgb,
                _ => PixelFormat.Format32bppArgb
            };
        }

        /// <summary>
        /// Gets the XNA surface format for the WZ PNG format
        /// Wz PNG format to Microsoft.Xna.Framework.Graphics.SurfaceFormat
        /// https://github.com/Kagamia/WzComparerR2/search?q=wzlibextension
        /// </summary>
        /// <param name="pngform"></param>
        /// <returns></returns>
        public static SurfaceFormat GetXNASurfaceFormat(this WzPngFormat format)
        {
            return format switch
            {
                WzPngFormat.Format1 => SurfaceFormat.Bgra4444,
                WzPngFormat.Format2 => SurfaceFormat.Bgra32,
                WzPngFormat.Format3 => SurfaceFormat.Dxt3, // DXT3 grayscale/thumbnail
                WzPngFormat.Format257 => SurfaceFormat.Bgra5551,
                WzPngFormat.Format513 => SurfaceFormat.Bgr565,
                WzPngFormat.Format517 => SurfaceFormat.Bgr565,
                WzPngFormat.Format1026 => SurfaceFormat.Dxt3,
                WzPngFormat.Format2050 => SurfaceFormat.Dxt5,
                // MonoGame does not expose BC7, so format 4098 is CPU-decoded to BGRA32.
                WzPngFormat.Format4098 => SurfaceFormat.Bgra32,
                _ => SurfaceFormat.Bgra32
            };
        }

        /// <summary>
        /// Gets the size of decoded data in bytes for a given format and dimensions
        /// </summary>
        public static int GetDecodedSize(this WzPngFormat format, int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new System.IO.InvalidDataException("PNG dimensions must be positive.");
            if (width > MaxDimension || height > MaxDimension)
                throw new System.IO.InvalidDataException("PNG dimensions exceed the supported limit.");

            long decodedSize;
            try
            {
                long pixels = checked((long)width * height);
                decodedSize = format switch
                {
                    WzPngFormat.Format1 => checked(pixels * 2),
                    WzPngFormat.Format2 => checked(pixels * 4),
                    WzPngFormat.Format3 => checked(((width + 3L) / 4) * ((height + 3L) / 4) * 16),
                    WzPngFormat.Format257 => checked(pixels * 2),
                    WzPngFormat.Format513 => checked(pixels * 2),
                    WzPngFormat.Format517 => pixels / 128,
                    WzPngFormat.Format1026 => checked(((width + 3L) / 4) * ((height + 3L) / 4) * 16),
                    WzPngFormat.Format2050 => checked(((width + 3L) / 4) * ((height + 3L) / 4) * 16),
                    WzPngFormat.Format4098 => checked(((width + 3L) / 4) * ((height + 3L) / 4) * 16),
                    _ => checked(pixels * 4)
                };
            }
            catch (OverflowException ex)
            {
                throw new System.IO.InvalidDataException("PNG decoded size is too large.", ex);
            }

            if (decodedSize < 0 || decodedSize > MaxDecodedSize || decodedSize > int.MaxValue)
                throw new System.IO.InvalidDataException("PNG decoded size exceeds the supported limit.");

            return (int)decodedSize;
        }
    }
}
