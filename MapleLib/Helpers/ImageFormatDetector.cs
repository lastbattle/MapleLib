using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MapleLib.Helpers
{
    /// <summary>
    /// DXT3 and DXT5:
    /// These are part of the S3 Texture Compression (S3TC) family, also known as DXT (DirectX Texture Compression).
    /// DXT3: Uses fixed alpha compression. Good for sharp alpha transitions.
    /// DXT5: Uses interpolated alpha compression. Better for smooth alpha transitions and higher quality alpha.
    /// Both compress RGB data at 4:1.
    /// 
    /// BGR32 (BGRA32):
    /// Uncompressed, 32 bits/pixel (8 bits each for B, G, R, A). High quality, high memory usage.
    /// 
    /// BGR565:
    /// Uncompressed, 16 bits/pixel (5 bits B, 6 bits G, 5 bits R). No alpha, memory-efficient.
    /// 
    /// BGRA4444:
    /// 16 bits/pixel (4 bits each for B, G, R, A). Supports alpha with reduced color depth.
    /// 
    /// BGRA5551:
    /// 16 bits/pixel (5 bits B, G, R; 1 bit A). Higher color depth (32,768 colors) but binary alpha (on/off).
    /// 
    /// The detector balances image quality, memory usage, and compression based on color depth, alpha behavior, and image size.
    /// </summary>
    public class ImageFormatDetector
    {
        /// <summary>
        /// When true, DXT formats (DXT3, DXT5) will not be suggested.
        /// This is necessary for pre-Big Bang MapleStory clients that don't support these formats.
        /// Format3 (DXT3 grayscale), Format1026 (DXT3 colored), and Format2050 (DXT5)
        /// are not supported by pre-BB clients.
        /// Set this based on the target MapleStory version when loading WZ files.
        /// </summary>
        public static bool UsePreBigBangImageFormats { get; set; } = false;

        /// <summary>
        /// Determines the recommended SurfaceFormat for raw ARGB data given its width and height.
        /// </summary>
        public static SurfaceFormat DetermineTextureFormat(byte[] argbData, int width, int height)
        {
            if (argbData == null || argbData.Length == 0)
                throw new ArgumentException("Invalid argbData");

            if (argbData.Length != width * height * 4)
                throw new ArgumentException("Data length does not match dimensions");

            var (uniqueRgbColors, uniqueAlphaValues, hasAlpha, hasPartialAlpha, maxAlpha, avgAlphaGradient, alphaVariance, isGrayscale) =
                AnalyzeImageData(argbData, width, height);
            bool isSmallImage = width * height < 256 * 256; // Favor 16-bit formats for small images
            // Don't suggest DXT formats when UsePreBigBangImageFormats is true (pre-Big Bang compatibility)
            bool isDxtCandidate = !UsePreBigBangImageFormats && IsDxtCompressionCandidate(width, height);

            // Grayscale images with alpha can use DXT3 (Format3) for efficient compression
            // This is commonly used for black/white thumbnails in MapleStory
            if (isGrayscale && hasAlpha && isDxtCandidate)
            {
                return SurfaceFormat.Dxt3;
            }

            if (!hasAlpha)
            {
                if (uniqueRgbColors <= 32 * 64 * 32) // Fits BGR565 (65,536 colors)
                    return SurfaceFormat.Bgr565;
                return isSmallImage ? SurfaceFormat.Bgr565 : SurfaceFormat.Bgra32; // Memory vs. quality trade-off
            }
            else
            {
                if (uniqueRgbColors > 4096 || uniqueAlphaValues > 16) // Beyond BGRA4444's capacity
                {
                    if (isDxtCandidate)
                    {
                        // Low avgAlphaGradient indicates smooth transitions (DXT5), high indicates sharp (DXT3)
                        // DXT3: Chosen for sharp alpha transitions(high avgAlphaGradient) or binary alpha when compression is viable, leveraging its fixed alpha compression.
                        // DXT5: Selected for smooth alpha gradients(low avgAlphaGradient), utilizing its interpolated alpha for higher quality.
                        return avgAlphaGradient < 10 ? SurfaceFormat.Dxt5 : SurfaceFormat.Dxt3;
                    }
                    // For non-DXT candidates, prefer BGRA5551 for binary alpha, else BGRA4444 or BGRA32
                    // Bgra5551 and Bgra4444 are favored for small images or binary alpha to minimize memory while maintaining acceptable quality.
                    if (!hasPartialAlpha && uniqueAlphaValues <= 2 && uniqueRgbColors <= 32 * 32 * 32) // Fits BGRA5551 (32,768 colors, binary alpha)
                        return SurfaceFormat.Bgra5551;

                    return isSmallImage && !hasPartialAlpha ? SurfaceFormat.Bgra4444 : SurfaceFormat.Bgra32;
                }
                else
                {
                    // Bgra5551 and Bgra4444 are favored for small images or binary alpha to minimize memory while maintaining acceptable quality.
                    if (!hasPartialAlpha && uniqueAlphaValues <= 2 && uniqueRgbColors <= 32 * 32 * 32) // Binary alpha, fits BGRA5551
                        return SurfaceFormat.Bgra5551;

                    if (hasPartialAlpha)
                    {
                        return SurfaceFormat.Bgra4444;
                    }
                    else
                    {
                        return isDxtCandidate ? SurfaceFormat.Dxt3 : SurfaceFormat.Bgra4444;
                    }
                }
            }
        }

        /// <summary>
        /// Analyzes raw ARGB data to extract metrics for format selection.
        /// </summary>
        public static (int uniqueRgbColors, int uniqueAlphaValues, bool hasAlpha, bool hasPartialAlpha, byte maxAlpha,
            double avgAlphaGradient, double alphaVariance, bool isGrayscale) AnalyzeImageData(byte[] argbData, int width, int height)
        {
            bool hasAlpha = false;
            bool hasPartialAlpha = false;
            byte maxAlpha = 0;
            bool isGrayscale = true;
            const int grayscaleTolerance = 8; // Allow small deviation for near-grayscale images
            HashSet<uint> rgbSet = []; // Unique RGB colors
            HashSet<byte> alphaSet = []; // Unique alpha values
            long alphaSum = 0;
            long alphaSumSquares = 0;
            long alphaGradientSum = 0;
            int gradientCount = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    byte a = argbData[i + 3];
                    byte r = argbData[i + 2];
                    byte g = argbData[i + 1];
                    byte b = argbData[i];

                    // Alpha analysis with tolerance
                    if (a < 255) hasAlpha = true;
                    if (a > 5 && a < 250) hasPartialAlpha = true; // Tolerance for near-0/near-255
                    maxAlpha = Math.Max(maxAlpha, a);
                    alphaSet.Add(a);
                    alphaSum += a;
                    alphaSumSquares += (long)a * a;

                    // RGB color analysis
                    uint rgbColor = (uint)((r << 16) | (g << 8) | b);
                    rgbSet.Add(rgbColor);

                    // Grayscale detection: R, G, B should be approximately equal
                    if (isGrayscale && (Math.Abs(r - g) > grayscaleTolerance || 
   Math.Abs(g - b) > grayscaleTolerance || 
      Math.Abs(r - b) > grayscaleTolerance))
                    {
                        isGrayscale = false;
                    }

                    // Alpha gradient (horizontal and vertical)
                    if (x > 0)
                    {
                        byte prevA = argbData[i - 4 + 3];
                        alphaGradientSum += Math.Abs(a - prevA);
                        gradientCount++;
                    }
                    if (y > 0)
                    {
                        byte aboveA = argbData[(i - width * 4) + 3];
                        alphaGradientSum += Math.Abs(a - aboveA);
                        gradientCount++;
                    }
                }
            }

            int pixelCount = argbData.Length / 4;
            double meanAlpha = (double)alphaSum / pixelCount;
            double alphaVariance = ((double)alphaSumSquares / pixelCount) - (meanAlpha * meanAlpha);
            double avgAlphaGradient = gradientCount > 0 ? (double)alphaGradientSum / gradientCount : 0;

            return (rgbSet.Count, alphaSet.Count, hasAlpha, hasPartialAlpha, maxAlpha, avgAlphaGradient, alphaVariance, isGrayscale);
        }

        /// <summary>
        /// Checks if the image dimensions are suitable for DXT compression (multiples of 4, min 4x4, area >= 64x64).
        /// </summary>
        public static bool IsDxtCompressionCandidate(int width, int height)
        {
            return width % 4 == 0 && height % 4 == 0 && width >= 4 && height >= 4 && (width * height) >= 64 * 64;
        }
    }
}