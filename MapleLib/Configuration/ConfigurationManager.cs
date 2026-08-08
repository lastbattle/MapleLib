using MapleLib.MapleCryptoLib;
using MapleLib.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MapleLib.Configuration
{
    public class ConfigurationManager
    {
        private const string SETTINGS_FILE_USER = "Settings.txt";
        private const string SETTINGS_FILE_APPLICATION = "ApplicationSettings.txt";
        private const string SETTINGS_FILE_CUSTOM_KEYS = "CustomKeys.txt";
        public const string configPipeName = "HaRepacker";

        private bool loaded = false;
        private string folderPath;
        private readonly object _ioLock = new();

        private UserSettings _userSettings = new UserSettings(); // default configuration for UI designer :( 
        public UserSettings UserSettings
        {
            get { return _userSettings; }
            private set { }
        }

        private ApplicationSettings _appSettings = new ApplicationSettings(); // default configuration for UI designer :( 
        public ApplicationSettings ApplicationSettings
        {
            get { return _appSettings; }
            private set { }
        }
        
        private List<EncryptionKey> _customKeys = new List<EncryptionKey>();
        public List<EncryptionKey> CustomKeys
        {
            get { return _customKeys; }
            private set { }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public ConfigurationManager()
        {
            this.folderPath = GetLocalFolderPath();
        }

        /// <summary>
        /// Gets the local folder path
        /// </summary>
        /// <returns></returns>
        public static string GetLocalFolderPath()
        {
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string our_folder = Path.Combine(appdata, configPipeName);
            if (!Directory.Exists(our_folder))
                Directory.CreateDirectory(our_folder);
            return our_folder;
        }

        /// <summary>
        /// Load application setting from user application data 
        /// </summary>
        /// <returns></returns>
        public bool Load()
        {
            lock (_ioLock)
            {
                string userFilePath = Path.Combine(folderPath, SETTINGS_FILE_USER);
                string applicationFilePath = Path.Combine(folderPath, SETTINGS_FILE_APPLICATION);
                string customKeysFilePath = Path.Combine(folderPath, SETTINGS_FILE_CUSTOM_KEYS);

                try
                {
                    if (!(File.Exists(userFilePath) && File.Exists(applicationFilePath) && File.Exists(customKeysFilePath)))
                    {
                        ResetToDefaults();
                        return false;
                    }

                    string userFileContent = File.ReadAllText(userFilePath);
                    string applicationFileContent = File.ReadAllText(applicationFilePath);
                    string customKeysFileContent = File.ReadAllText(customKeysFilePath);

                    UserSettings userSettings = JsonSerializer.Deserialize(userFileContent, MapleJsonContext.Default.UserSettings);
                    ApplicationSettings applicationSettings = JsonSerializer.Deserialize(applicationFileContent, MapleJsonContext.Default.ApplicationSettings);
                    List<EncryptionKey> customKeys = JsonSerializer.Deserialize(customKeysFileContent, MapleJsonContext.Default.ListEncryptionKey);
                    if (userSettings == null || applicationSettings == null || customKeys == null)
                        throw new JsonException("Configuration files cannot contain JSON null.");

                    _userSettings = userSettings;
                    _appSettings = applicationSettings;
                    _customKeys = customKeys;
                    return true;
                }
                catch (Exception)
                {
                    // Remove malformed files so a later launch does not repeatedly
                    // fail on the same state, then restore in-memory defaults.
                    try
                    {
                        File.Delete(userFilePath);
                        File.Delete(applicationFilePath);
                        File.Delete(customKeysFilePath);
                    }
                    catch { }
                }
                ResetToDefaults();
                return false;
            }
        }

        /// <summary>
        /// Saves setting to user application data
        /// </summary>
        /// <returns></returns>
        public bool Save()
        {
            lock (_ioLock)
            {
                string[] tempPaths = null;
                try
                {
                    string userSettingsSerialised = JsonSerializer.Serialize(_userSettings, MapleJsonContext.Default.UserSettings);
                    string appSettingsSerialised = JsonSerializer.Serialize(_appSettings, MapleJsonContext.Default.ApplicationSettings);
                    string customKeysSerialised = JsonSerializer.Serialize(_customKeys, MapleJsonContext.Default.ListEncryptionKey);

                    string[] targetPaths =
                    [
                        Path.Combine(folderPath, SETTINGS_FILE_USER),
                        Path.Combine(folderPath, SETTINGS_FILE_APPLICATION),
                        Path.Combine(folderPath, SETTINGS_FILE_CUSTOM_KEYS)
                    ];
                    foreach (string targetPath in targetPaths)
                    {
                        if (Directory.Exists(targetPath))
                            throw new IOException($"Configuration target is a directory: {targetPath}");
                    }

                    string[] contents = [userSettingsSerialised, appSettingsSerialised, customKeysSerialised];
                    tempPaths = new string[targetPaths.Length];
                    for (int i = 0; i < targetPaths.Length; i++)
                    {
                        tempPaths[i] = $"{targetPaths[i]}.{Guid.NewGuid():N}.tmp";
                        WriteConfigTempFile(tempPaths[i], contents[i]);
                    }

                    for (int i = 0; i < targetPaths.Length; i++)
                        ReplaceConfigFile(tempPaths[i], targetPaths[i]);

                    loaded = true;
                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (tempPaths != null)
                    {
                        foreach (string tempPath in tempPaths)
                        {
                            try
                            {
                                if (tempPath != null)
                                    File.Delete(tempPath);
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private static void WriteConfigTempFile(string path, string content)
        {
            using FileStream stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.WriteThrough);
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 4096, leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
            }
            stream.Flush(flushToDisk: true);
        }

        private static void ReplaceConfigFile(string tempPath, string targetPath)
        {
            if (File.Exists(targetPath))
                File.Replace(tempPath, targetPath, destinationBackupFileName: null);
            else
                File.Move(tempPath, targetPath);
        }


        /// <summary>
        /// Gets the custom WZ IV from settings
        /// </summary>
        /// <returns></returns>
        public byte[] GetCusomWzIVEncryption()
        {
            if (!loaded)
            {
                // Preserve the failed state so a later call can retry after a
                // missing or malformed configuration has been repaired.
                loaded = Load();
            }
            if (loaded)
            {
                string storedCustomEnc = ApplicationSettings.MapleVersion_CustomEncryptionBytes;
                try
                {
                    byte[] bytes = ByteUtils.HexToBytes(storedCustomEnc ?? string.Empty);
                    if (bytes.Length == 4)
                        return bytes;
                }
                catch (FormatException) { }
            }
            return new byte[4] { 0x0, 0x0, 0x0, 0x0 }; // fallback with BMS
        }

        public void SetCustomWzUserKeyFromConfig()
        {
            string configured = ApplicationSettings?.MapleVersion_CustomAESUserKey;
            byte[] bytes;
            try
            {
                bytes = ByteUtils.HexToBytes(configured ?? string.Empty);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("The configured WZ user key is not valid hexadecimal.", ex);
            }

            if (bytes.Length == 0)
            {
                MapleCryptoConstants.UserKey_WzLib = (byte[])MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.Clone();
                return;
            }
            if (bytes.Length != 32)
                throw new InvalidDataException("The configured WZ user key must contain exactly 32 bytes.");

            byte[] expanded = new byte[MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.Length];
            for (int i = 0; i < expanded.Length; i += 4)
            {
                expanded[i] = bytes[i / 4];
            }
            MapleCryptoConstants.UserKey_WzLib = expanded;
        }

        private void ResetToDefaults()
        {
            _userSettings = new UserSettings();
            _appSettings = new ApplicationSettings();
            _customKeys = new List<EncryptionKey>();
        }
    }
}
