/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2024 lastbattle
   
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


using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MapleLib.Helpers {

    /// <summary>
    /// DXT3 and DXT5:
    /// These are part of the S3 Texture Compression(S3TC) family, also known as DXT(DirectX Texture Compression).
    /// DXT3: Uses a fixed alpha compression. It's good for images with sharp alpha transitions.
    /// DXT5: Uses interpolated alpha compression. It's better for smooth alpha transitions and generally provides higher quality alpha than DXT3.
    /// Both DXT3 and DXT5 compress RGB data in the same way, achieving a 4:1 compression ratio for RGB data.
    /// 
    /// BGR32:
    /// This is an uncompressed format where each pixel is represented by 32 bits
    /// B, G, and R channels each use 8 bits(1 byte)
    /// The remaining 8 bits are typically unused or used as an alpha channel(making it effectively BGRA32)
    /// It's a high-quality format but takes up more memory than compressed formats
    /// 
    /// BGR565:
    /// This is also an uncompressed format, but it uses less memory than BGR32.
    /// Blue channel: 5 bits
    /// Green channel: 6 bits(human eyes are more sensitive to green)
    /// Red channel: 5 bits
    /// Total: 16 bits(2 bytes) per pixel
    /// No alpha channel
    /// 
    /// BGRA4444:
    /// This is another 16 - bit format, but it includes an alpha channel.
    /// B, G, R, and A channels each use 4 bits
    /// Provides more color + transparency options than BGR565, but with less color depth
    /// 
    /// The main differences between these formats are:
    /// Compression: DXT3 and DXT5 are compressed, while the others are not.
    /// Color depth: BGR32 provides the highest color depth, followed by BGR565 and BGRA4444.
    /// Alpha support: DXT3, DXT5, BGR32(as BGRA32), and BGRA4444 support alpha, while BGR565 does not.
    /// Memory usage: Compressed formats(DXT) use less memory, followed by 16 - bit formats(BGR565, BGRA4444), with BGR32 using the most.
    /// 
    /// The choice between these formats depends on the specific needs of the application, balancing factors like image quality, memory usage, and processing speed.
    /// </summary>
    public class ImageFormatDetector {

        /// <summary>
        /// Determines the recommended SurfaceFormat for a given image with its raw byte[], width, and height data
        /// </summary>
        /// <param name="argbData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static SurfaceFormat DetermineTextureFormat(byte[] argbData, int width, int height) {
            if (argbData == null || argbData.Length == 0)
                throw new ArgumentException("Invalid argbData");

            if (argbData.Length != width * height * 4)
                throw new ArgumentException("Data length does not match dimensions");

            var (uniqueColors, hasAlpha, hasPartialAlpha, maxAlpha, alphaTransitions, alphaVariance) = AnalyzeImageData(argbData);

            // Decision making
            if (!hasAlpha) {
                return SurfaceFormat.Bgr565;
            }
            else {
                bool isDxtCandidate = IsDxtCompressionCandidate(width, height);

                if (uniqueColors > 4096) {
                    if (isDxtCandidate) {
                        // Differentiate between DXT3 and DXT5
                        double alphaTransitionRatio = (double)alphaTransitions / (width * height);
                        if (alphaTransitionRatio > 0.2 && alphaVariance > 5000 && alphaVariance < 10000) {
                            return SurfaceFormat.Dxt3;
                        }
                        return SurfaceFormat.Dxt5;
                    }
                    return SurfaceFormat.Bgr32;
                }
                else if (hasPartialAlpha) {
                    return SurfaceFormat.Bgra4444;
                }
                else {
                    // For binary alpha (only fully transparent or fully opaque)
                    return isDxtCandidate ? SurfaceFormat.Dxt3 : SurfaceFormat.Bgra4444;
                }
            }
        }

        public static (int uniqueColors, bool hasAlpha, bool hasPartialAlpha, byte maxAlpha, int alphaTransitions, double alphaVariance) AnalyzeImageData(byte[] argbData) {
            bool hasAlpha = false;
            bool hasPartialAlpha = false;
            byte maxAlpha = 0;
            int alphaTransitions = 0;
            byte lastAlpha = 255;
            HashSet<uint> colorSet = new HashSet<uint>();
            long alphaSum = 0;
            long alphaSumSquares = 0;

            for (int i = 0; i < argbData.Length; i += 4) {
                byte a = argbData[i + 3];
                byte r = argbData[i + 2];
                byte g = argbData[i + 1];
                byte b = argbData[i];

                if (a < 255) {
                    hasAlpha = true;
                    if (a > 0)
                        hasPartialAlpha = true;
                }

                if (a != lastAlpha) {
                    alphaTransitions++;
                    lastAlpha = a;
                }

                maxAlpha = Math.Max(maxAlpha, a);
                alphaSum += a;
                alphaSumSquares += (long)a * a;

                uint color = (uint)((a << 24) | (r << 16) | (g << 8) | b);
                colorSet.Add(color);
            }

            int pixelCount = argbData.Length / 4;
            double meanAlpha = (double)alphaSum / pixelCount;
            double alphaVariance = ((double)alphaSumSquares / pixelCount) - (meanAlpha * meanAlpha);

            return (colorSet.Count, hasAlpha, hasPartialAlpha, maxAlpha, alphaTransitions, alphaVariance);
        }

        public static bool IsDxtCompressionCandidate(int width, int height) {
            return width % 4 == 0 && height % 4 == 0 && width >= 4 && height >= 4 && (width * height) >= 64 * 64;
        }
    }
}
