using MapleLib.Helpers;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class ImageFormatDetectorAdversarialTests
{
    [Fact]
    public void DetermineTextureFormat_RejectsInvalidDimensionsBeforeMultiplication()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageFormatDetector.DetermineTextureFormat([0, 0, 0, 0], 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageFormatDetector.AnalyzeImageData([0, 0, 0, 0], -1, 1));
        Assert.Throws<ArgumentException>(() => ImageFormatDetector.DetermineTextureFormat([0, 0, 0, 0], 2, 2));
    }

    [Fact]
    public void DxtCandidate_UsesOverflowSafeAreaCheck()
    {
        Assert.True(ImageFormatDetector.IsDxtCompressionCandidate(1_073_741_824, 4));
        Assert.True(ImageFormatDetector.IsDxtCompressionCandidate(256, 256));
    }
}
