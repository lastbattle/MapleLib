using MapleLib.WzLib.Serializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace MapleLib.Configuration
{
    public class UserSettings
    {
        public enum UserSettingsThemeColor
        {
            Dark = 0,
            Light = 1
        }

        [JsonPropertyName("Indentation")]
        public int Indentation = 0;

        [JsonPropertyName("LineBreakType")]
        [JsonConverter(typeof(JsonStringEnumConverter<LineBreak>))]
        public LineBreak LineBreakType = LineBreak.None;

        [JsonPropertyName("DefaultXmlFolder")]
        public string DefaultXmlFolder = "";

        [JsonPropertyName("UseApngIncompatibilityFrame")]
        public bool UseApngIncompatibilityFrame = true;

        [JsonPropertyName("AutoAssociate")]
        public bool AutoAssociate = true;

        [JsonPropertyName("Sort")]
        public bool Sort = false;

        [JsonPropertyName("SuppressWarnings")]
        public bool SuppressWarnings = false;

        [JsonPropertyName("ParseImagesInSearch")]
        public bool ParseImagesInSearch = false;

        [JsonPropertyName("SearchStringValues")]
        public bool SearchStringValues = true;

        // Animate
        [JsonPropertyName("DevImgSequences")]
        public bool DevImgSequences = false;

        [JsonPropertyName("CartesianPlane")]
        public bool CartesianPlane = true;

        [JsonPropertyName("DelayNextLoop")]
        public int DelayNextLoop = 60;

        [JsonPropertyName("PlanePosition")]
        public string PlanePosition = "Center";

        // Themes
        [JsonPropertyName("ThemeColor")]
        public int ThemeColor = (int) UserSettingsThemeColor.Light;//white = 1, black = 0


        // Settings not shown on the settings page
        [JsonPropertyName("EnableCrossHairDebugInformation")]
        public bool EnableCrossHairDebugInformation = true;
        [JsonPropertyName("EnableBorderDebugInformation")]
        public bool EnableBorderDebugInformation = true;

        [JsonPropertyName("ImageZoomLevel")]
        public double ImageZoomLevel = 3.0f;

        [JsonPropertyName("AutoloadRelatedWzFiles")]
        public bool AutoloadRelatedWzFiles = false;
    }
}
