using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace MapleLib.Configuration
{
    public class ApplicationSettings
    {
        #region Application Window
        [JsonPropertyName("WindowMaximized")]
        public bool WindowMaximized = false;

        [JsonPropertyName("WindowWidth")]
        public int Width = 1024;
        [JsonPropertyName("WindowHeight")]
        public int Height = 768;
        #endregion

        #region Etc
        [JsonPropertyName("FirstRun")]
        public bool FirstRun = true;

        [JsonPropertyName("LastBrowserPath")]
        public string LastBrowserPath = "";
        #endregion

        #region Encryption
        /// <summary>
        /// The MapleStory encryption to use.
        /// </summary>
        [JsonPropertyName("MapleStoryVersion")]
        [JsonConverter(typeof(JsonStringEnumConverter<WzMapleVersion>))]
        public WzMapleVersion MapleVersion = WzMapleVersion.BMS;

        /// <summary>
        /// The custom encryption name for the custom WZ encryption
        /// </summary>
        [JsonPropertyName("MapleStoryVersion_CustomEncryptionName")]
        public string MapleVersion_CustomEncryptionName = "Default";
        
        /// <summary>
        /// The custom AES user key to use when encrypting and decrypting WZ files
        /// </summary>
        [JsonPropertyName("MapleStoryVersion_CustomAESUserKey")]
        public string MapleVersion_CustomAESUserKey = string.Empty; // str empty as default.

        /// <summary>
        /// The custom IV encryption bytes to use when encrypting and decrypting WZ files
        /// </summary>
        [JsonPropertyName("MapleStoryVersion_EncryptionBytes")]
        public string MapleVersion_CustomEncryptionBytes = "0x00-0x00-0x00-0x00";
        #endregion
    }
}
