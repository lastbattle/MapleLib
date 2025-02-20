using System.Drawing.Imaging;
using Microsoft.Xna.Framework.Graphics;

namespace MapleLib.WzLib.WzProperties
{
    public static class WzPngFormatExtensions
    {
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
                WzPngFormat.Format3 => SurfaceFormat.Bgra32,
                WzPngFormat.Format513 => SurfaceFormat.Bgr565,
                WzPngFormat.Format517 => SurfaceFormat.Bgr565,
                WzPngFormat.Format1026 => SurfaceFormat.Dxt3,
                WzPngFormat.Format2050 => SurfaceFormat.Dxt5,
                _ => SurfaceFormat.Bgra32
            };
        }

        /// <summary>
        /// Gets the size of decoded data in bytes for a given format and dimensions
        /// </summary>
        public static int GetDecodedSize(this WzPngFormat format, int width, int height)
        {
            return format switch
            {
                WzPngFormat.Format1 => width * height * 2,
                WzPngFormat.Format2 => width * height * 4,
                WzPngFormat.Format3 => width * height * 4,
                WzPngFormat.Format257 => width * height * 2,
                WzPngFormat.Format513 => width * height * 2,
                WzPngFormat.Format517 => width * height / 128,
                WzPngFormat.Format1026 => width * height * 4,
                WzPngFormat.Format2050 => width * height,
                _ => width * height * 4
            };
        }
    }
}
