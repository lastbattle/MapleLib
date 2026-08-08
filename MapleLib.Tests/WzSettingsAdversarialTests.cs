using System.Drawing;
using System.IO;
using MapleLib.WzLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzSettingsAdversarialTests
{
    [Fact]
    public void SaveAndLoad_PreservesSizeOrientationAndDetachedBitmap()
    {
        string path = Path.Combine(Path.GetTempPath(), $"MapleLib-WzSettings-{Guid.NewGuid():N}.json");
        TestSettings.WindowSize = new Size(640, 480);
        TestSettings.Icon?.Dispose();
        TestSettings.Icon = new Bitmap(2, 3);
        TestSettings.Icon.SetPixel(1, 2, Color.Crimson);

        try
        {
            var manager = new WzSettingsManager(path, typeof(TestSettings), typeof(TestSettings));
            manager.SaveSettings();

            TestSettings.WindowSize = Size.Empty;
            TestSettings.Icon.Dispose();
            TestSettings.Icon = null;
            manager.LoadSettings();

            Assert.Equal(new Size(640, 480), TestSettings.WindowSize);
            Assert.NotNull(TestSettings.Icon);
            Assert.Equal(Color.Crimson.ToArgb(), TestSettings.Icon!.GetPixel(1, 2).ToArgb());
        }
        finally
        {
            TestSettings.Icon?.Dispose();
            TestSettings.Icon = null;
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Load_OversizedSettingsFileIsIgnoredBeforeMaterialization()
    {
        string path = Path.Combine(Path.GetTempPath(), $"MapleLib-WzSettings-{Guid.NewGuid():N}.json");
        TestSettings.WindowSize = new Size(17, 19);

        try
        {
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.SetLength(MemoryLimits.MAX_METADATA_JSON_BYTES + 1);

            new WzSettingsManager(path, typeof(TestSettings), typeof(TestSettings)).LoadSettings();

            Assert.Equal(new Size(17, 19), TestSettings.WindowSize);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static class TestSettings
    {
        public static Size WindowSize = new(17, 19);
        public static Bitmap? Icon;
    }
}
