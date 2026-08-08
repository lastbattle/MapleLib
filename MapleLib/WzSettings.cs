using System;
using System.Reflection;
using System.IO;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MapleLib.WzLib
{
    public class WzSettingsManager
    {
        private readonly string settingFilePath;

        private readonly Type userSettingsType;
        private readonly Type appSettingsType;
        private readonly object _ioLock = new();

        private const string USER_SETTING_JSON = "UserSettings";
        private const string APP_SETTING_JSON = "ApplicationSettings";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="wzPath"></param>
        /// <param name="userSettingsType"></param>
        /// <param name="appSettingsType"></param>
        public WzSettingsManager(string wzPath, Type userSettingsType, Type appSettingsType)
        {
            this.settingFilePath = wzPath;
            this.userSettingsType = userSettingsType;
            this.appSettingsType = appSettingsType;
        }

        #region Loading
        /// <summary>
        /// Load UserSettings and ApplicationSettings
        /// </summary>
        public void LoadSettings()
        {
            lock (_ioLock)
            {
                try
                {
                    if (!File.Exists(settingFilePath))
                        return;

                    MemoryLimits.EnsureFileSize(settingFilePath, MemoryLimits.MAX_METADATA_JSON_BYTES, "WZ settings");
                    string strJsonConfig = File.ReadAllText(settingFilePath);
                    JsonObject mainJson = JsonNode.Parse(strJsonConfig)?.AsObject()
                        ?? throw new JsonException("Settings JSON must contain an object.");

                    LoadSettingsJson(mainJson[USER_SETTING_JSON]?.AsObject(), userSettingsType);
                    LoadSettingsJson(mainJson[APP_SETTING_JSON]?.AsObject(), appSettingsType);
                }
                catch { 
                    // its fine if loading isnt possible
                    // fallback to default
                }
            }
        }

        /// <summary>
        /// Load settings json
        /// </summary>
        /// <param name="json"></param>
        /// <param name="settingsHolderType"></param>
        private void LoadSettingsJson(JsonObject json, Type settingsHolderType)
        {
            if (json == null || settingsHolderType == null)
                return;

            foreach (FieldInfo fieldInfo in settingsHolderType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                string fieldName = fieldInfo.Name;
                JsonObject jsonHoldingObject = json[fieldName]?.AsObject();

                LoadField(fieldInfo, jsonHoldingObject);
            }
        }

        /// <summary>
        /// Loads the individual field into object
        /// </summary>
        /// <param name="fieldInfo"></param>
        /// <param name="jsonHoldingObject"></param>
        /// <exception cref="Exception"></exception>
        private void LoadField(FieldInfo fieldInfo, JsonObject jsonHoldingObject)
        {
            if (jsonHoldingObject == null)
            {
                // does nothing to this field if json does not contain anything
                // fallback to default as specified in WzSettings.json
            }
            else if (fieldInfo.FieldType.BaseType != null && fieldInfo.FieldType.BaseType.FullName == "System.Enum")
                fieldInfo.SetValue(null, jsonHoldingObject["value"].GetValue<int>());
            else
            {
                string fieldType = jsonHoldingObject["type"].GetValue<string>();

                switch (fieldType)
                {
                    case "Microsoft.Xna.Framework.Color":
                        {
                            uint value = jsonHoldingObject["value"].GetValue<uint>();
                            Microsoft.Xna.Framework.Color xnaColor = new Microsoft.Xna.Framework.Color(value);
 
                            fieldInfo.SetValue(null, xnaColor);
                            break;
                        }
                    case "System.Drawing.Color":
                        {
                            int value = jsonHoldingObject["value"].GetValue<int>();
                            System.Drawing.Color color = System.Drawing.Color.FromArgb(value);

                            fieldInfo.SetValue(null, color);
                            break;
                        }
                    case "System.Int32":
                        {
                            int value = jsonHoldingObject["value"].GetValue<int>();

                            fieldInfo.SetValue(null, value);
                            break;
                        }
                    case "System.Double":
                        {
                            double value = jsonHoldingObject["value"].GetValue<double>();

                            fieldInfo.SetValue(null, value);
                            break;
                        }
                    case "System.Single":
                        {
                            float value = jsonHoldingObject["value"].GetValue<float>();

                            fieldInfo.SetValue(null, value);
                            break;
                        }
                    case "System.Drawing.Size":
                        {
                            int valueHeight = jsonHoldingObject["valueHeight"].GetValue<int>();
                            int valueWidth = jsonHoldingObject["valueWidth"].GetValue<int>();

                            System.Drawing.Size size = new System.Drawing.Size(valueWidth, valueHeight);

                            fieldInfo.SetValue(null, size);
                            break;
                        }
                    case "System.String":
                        {
                            string value = jsonHoldingObject["value"].GetValue<string>();

                            fieldInfo.SetValue(null, value);
                            break;
                        }
                    case "System.Drawing.Bitmap":
                        {
                            string base64Image = jsonHoldingObject["value"].GetValue<string>();
                            byte[] byteImage = System.Convert.FromBase64String(base64Image);

                            Bitmap bmp;
                            using (var ms = new MemoryStream(byteImage))
                            {
                                using Bitmap source = new Bitmap(ms);
                                bmp = new Bitmap(source);
                            }
                            fieldInfo.SetValue(null, bmp);
                            break;
                        }
                    case "System.Boolean":
                        {
                            bool value = jsonHoldingObject["value"].GetValue<bool>();

                            fieldInfo.SetValue(null, value);
                            break;
                        }
                    default:
                        {
                            throw new Exception("Unsupported data type for WzSettings.");
                        }
                }
            }
        }
        #endregion

        #region Saving
        public void SaveSettings()
        {
            lock (_ioLock)
            {
                JsonObject userSettingJson = SaveSettingsJson(userSettingsType);
                JsonObject appSettingJson = SaveSettingsJson(appSettingsType);

                JsonObject mainJson = new JsonObject();
                mainJson.Add(USER_SETTING_JSON, userSettingJson);
                mainJson.Add(APP_SETTING_JSON, appSettingJson);

                string tempPath = $"{settingFilePath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                        bufferSize: 4096, options: FileOptions.WriteThrough))
                    using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 4096, leaveOpen: true))
                    {
                        writer.Write(mainJson.ToJsonString(MapleJson.IndentedOptions));
                        writer.Flush();
                        stream.Flush(flushToDisk: true);
                    }

                    if (File.Exists(settingFilePath))
                        File.Replace(tempPath, settingFilePath, destinationBackupFileName: null);
                    else
                        File.Move(tempPath, settingFilePath);
                }
                finally
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        /// <summary>
        /// Saves the settings image to json
        /// </summary>
        /// <param name="settingsHolderType"></param>
        private JsonObject SaveSettingsJson(Type settingsHolderType)
        {
            JsonObject jObjHolder = new JsonObject();

            foreach (FieldInfo fieldInfo in settingsHolderType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                SaveField(fieldInfo, jObjHolder);
            }
            return jObjHolder;
        }

        /// <summary>
        /// Saves the individual field into json object
        /// </summary>
        /// <param name="fieldInfo"></param>
        /// <param name="jObjHolder"></param>
        /// <exception cref="Exception"></exception>
        private void SaveField(FieldInfo fieldInfo, JsonObject jObjHolder)
        {
            string settingName = fieldInfo.Name;

            JsonObject fieldJsonObject = new JsonObject();
            jObjHolder.Add(settingName, fieldJsonObject);

            fieldJsonObject.Add("type", JsonValue.Create(fieldInfo.FieldType.FullName)); // i.e System.Int

            if (fieldInfo.FieldType.BaseType != null && fieldInfo.FieldType.BaseType.FullName == "System.Enum")
            {
                fieldJsonObject.Add("value", JsonValue.Create((int)fieldInfo.GetValue(null)));
            }
            else
                switch (fieldInfo.FieldType.FullName)
                {
                    //case "Microsoft.Xna.Framework.Graphics.Color":
                    case "Microsoft.Xna.Framework.Color":
                        {
                            Microsoft.Xna.Framework.Color xnaColor = (Microsoft.Xna.Framework.Color) fieldInfo.GetValue(null);
                            uint uValue = xnaColor.PackedValue;

                            fieldJsonObject.Add("value", JsonValue.Create(uValue));
                            break;
                        }
                    case "System.Drawing.Color":
                        {
                            int argbColor = ((System.Drawing.Color)fieldInfo.GetValue(null)).ToArgb();

                            fieldJsonObject.Add("value", JsonValue.Create(argbColor));
                            break;
                        }
                    case "System.Int32":
                        {
                            int intVal = (int)fieldInfo.GetValue(null);

                            fieldJsonObject.Add("value", JsonValue.Create(intVal));
                            break;
                        }
                    case "System.Double":
                        {
                            double dValue = (double)fieldInfo.GetValue(null);

                            fieldJsonObject.Add("value", JsonValue.Create(dValue));
                            break;
                        }
                    case "System.Single":
                        {
                            float fValue = (float)fieldInfo.GetValue(null);

                            fieldJsonObject.Add("value", JsonValue.Create(fValue));
                            break;
                        }
                    case "System.Drawing.Size":
                        {
                            System.Drawing.Size size = (System.Drawing.Size)fieldInfo.GetValue(null);

                            fieldJsonObject.Add("valueHeight", JsonValue.Create(size.Height));
                            fieldJsonObject.Add("valueWidth", JsonValue.Create(size.Width));
                            break;
                        }
                    case "System.String":
                        {
                            string strValue = (string)fieldInfo.GetValue(null);

                            fieldJsonObject.Add("value", JsonValue.Create(strValue));
                            break;
                        }
                    case "System.Drawing.Bitmap":
                        {
                            System.Drawing.Bitmap bitmap = (System.Drawing.Bitmap) fieldInfo.GetValue(null);

                            ImageConverter converter = new ImageConverter();
                            byte[] byteImage = (byte[])converter.ConvertTo(bitmap, typeof(byte[]));
                            string base64Image = Convert.ToBase64String(byteImage, 0, byteImage.Length,Base64FormattingOptions.None);

                            fieldJsonObject.Add("value", JsonValue.Create(base64Image));
                            break;
                        }
                    case "System.Boolean":
                        {
                            bool bValue = (bool)fieldInfo.GetValue(null);

                            fieldJsonObject.Add("value", JsonValue.Create(bValue));
                            break;
                        }
                    default:
                        {
                            throw new Exception("Unsupported data type for WzSettings.");
                        }
                }
        }
        #endregion
    }
}
