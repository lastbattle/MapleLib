using MapleLib.Helpers;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class ImageFormatDetectorStatisticsTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(4, 4, 2)]
    [InlineData(17, 19, 3)]
    [InlineData(64, 64, 4)]
    public void AnalyzeImageDataMatchesFrozenStatisticsOracle(int width, int height, int seed)
    {
        byte[] pixels = new byte[width * height * 4];
        new Random(seed).NextBytes(pixels);

        Assert.Equal(FrozenAnalyze(pixels, width, height),
            ImageFormatDetector.AnalyzeImageData(pixels, width, height));
    }

    [Fact]
    public void AnalyzeImageDataCountsAllPossibleAlphaValuesExactly()
    {
        byte[] pixels = new byte[256 * 4];
        for (int alpha = 0; alpha < 256; alpha++)
        {
            int offset = alpha * 4;
            pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = (byte)alpha;
            pixels[offset + 3] = (byte)alpha;
        }

        var result = ImageFormatDetector.AnalyzeImageData(pixels, 256, 1);
        Assert.Equal(256, result.uniqueAlphaValues);
        Assert.Equal(256, result.uniqueRgbColors);
        Assert.Equal(255, result.maxAlpha);
        Assert.True(result.isGrayscale);
    }

    private static (int uniqueRgbColors, int uniqueAlphaValues, bool hasAlpha, bool hasPartialAlpha,
        byte maxAlpha, double avgAlphaGradient, double alphaVariance, bool isGrayscale)
        FrozenAnalyze(byte[] argbData, int width, int height)
    {
        bool hasAlpha = false;
        bool hasPartialAlpha = false;
        byte maxAlpha = 0;
        bool isGrayscale = true;
        HashSet<uint> rgb = [];
        HashSet<byte> alpha = [];
        long alphaSum = 0;
        long alphaSumSquares = 0;
        long alphaGradientSum = 0;
        int gradientCount = 0;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int offset = (y * width + x) * 4;
            byte a = argbData[offset + 3];
            byte r = argbData[offset + 2];
            byte g = argbData[offset + 1];
            byte b = argbData[offset];
            if (a < 255) hasAlpha = true;
            if (a > 5 && a < 250) hasPartialAlpha = true;
            maxAlpha = Math.Max(maxAlpha, a);
            alpha.Add(a);
            alphaSum += a;
            alphaSumSquares += (long)a * a;
            rgb.Add((uint)((r << 16) | (g << 8) | b));
            if (isGrayscale && (Math.Abs(r - g) > 8 || Math.Abs(g - b) > 8 || Math.Abs(r - b) > 8))
                isGrayscale = false;
            if (x > 0)
            {
                alphaGradientSum += Math.Abs(a - argbData[offset - 1]);
                gradientCount++;
            }
            if (y > 0)
            {
                alphaGradientSum += Math.Abs(a - argbData[offset - width * 4 + 3]);
                gradientCount++;
            }
        }

        int pixelCount = argbData.Length / 4;
        double meanAlpha = (double)alphaSum / pixelCount;
        return (rgb.Count, alpha.Count, hasAlpha, hasPartialAlpha, maxAlpha,
            gradientCount > 0 ? (double)alphaGradientSum / gradientCount : 0,
            (double)alphaSumSquares / pixelCount - meanAlpha * meanAlpha,
            isGrayscale);
    }
}
