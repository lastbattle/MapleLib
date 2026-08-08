using System.Reflection;
using System.IO;
using MapleLib.Configuration;
using MapleLib.MapleCryptoLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class ConfigurationAdversarialTests
{
    [Fact]
    public void EncryptionKey_DefaultCanGenerateWzKeyAndRejectsMalformedValues()
    {
        var key = new EncryptionKey();
        Assert.NotNull(key.WzKey);
        Assert.Throws<ArgumentNullException>(() => key.Iv = null!);
        Assert.Throws<FormatException>(() => key.Iv = "GG GG GG GG");
        Assert.Throws<ArgumentException>(() => key.AesUserKey = "00");
    }

    [Fact]
    public void CustomUserKeyValidationDoesNotCorruptExistingGlobalKey()
    {
        byte[] original = MapleCryptoConstants.UserKey_WzLib;
        try
        {
            MapleCryptoConstants.UserKey_WzLib = (byte[])MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.Clone();
            byte[] before = (byte[])MapleCryptoConstants.UserKey_WzLib.Clone();
            var manager = new ConfigurationManager();
            manager.ApplicationSettings.MapleVersion_CustomAESUserKey = "00";

            Assert.Throws<InvalidDataException>(() => manager.SetCustomWzUserKeyFromConfig());
            Assert.Equal(before, MapleCryptoConstants.UserKey_WzLib);

            manager.ApplicationSettings.MapleVersion_CustomAESUserKey = string.Empty;
            MapleCryptoConstants.UserKey_WzLib = new byte[128];
            manager.SetCustomWzUserKeyFromConfig();
            Assert.Equal(MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT, MapleCryptoConstants.UserKey_WzLib);
        }
        finally
        {
            MapleCryptoConstants.UserKey_WzLib = original;
        }
    }

    [Fact]
    public void Load_NullJsonResetsDefaultsAndReturnsFalse()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"MapleLib.Config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Settings.txt"), "null");
            File.WriteAllText(Path.Combine(directory, "ApplicationSettings.txt"), "null");
            File.WriteAllText(Path.Combine(directory, "CustomKeys.txt"), "null");
            var manager = new ConfigurationManager();
            typeof(ConfigurationManager).GetField("folderPath", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(manager, directory);

            Assert.False(manager.Load());
            Assert.NotNull(manager.UserSettings);
            Assert.NotNull(manager.ApplicationSettings);
            Assert.NotNull(manager.CustomKeys);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GetCustomIv_RetriesAfterMissingConfigurationIsRepaired()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"MapleLib.Config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var manager = CreateManagerForDirectory(directory);
            Assert.Equal(new byte[4], manager.GetCusomWzIVEncryption());

            var repaired = CreateManagerForDirectory(directory);
            repaired.ApplicationSettings.MapleVersion_CustomEncryptionBytes = "01 02 03 04";
            Assert.True(repaired.Save());

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, manager.GetCusomWzIVEncryption());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveFailureDoesNotTruncateExistingConfigurationFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"MapleLib.Config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string customKeysPath = Path.Combine(directory, "CustomKeys.txt");
        try
        {
            var manager = CreateManagerForDirectory(directory);
            Assert.True(manager.Save());
            string originalUser = File.ReadAllText(Path.Combine(directory, "Settings.txt"));
            string originalApplication = File.ReadAllText(Path.Combine(directory, "ApplicationSettings.txt"));

            File.Delete(customKeysPath);
            Directory.CreateDirectory(customKeysPath);
            manager.ApplicationSettings.FirstRun = false;

            Assert.False(manager.Save());
            Assert.Equal(originalUser, File.ReadAllText(Path.Combine(directory, "Settings.txt")));
            Assert.Equal(originalApplication, File.ReadAllText(Path.Combine(directory, "ApplicationSettings.txt")));
            Assert.True(Directory.Exists(customKeysPath));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(customKeysPath))
                Directory.Delete(customKeysPath, recursive: true);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static ConfigurationManager CreateManagerForDirectory(string directory)
    {
        var manager = new ConfigurationManager();
        typeof(ConfigurationManager).GetField("folderPath", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(manager, directory);
        return manager;
    }
}
