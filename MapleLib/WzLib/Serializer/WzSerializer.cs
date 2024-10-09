/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System.IO;
using System.Drawing.Imaging;
using System.Globalization;
using System.Xml;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace MapleLib.WzLib.Serializer
{
    public abstract class WzSerializer : ProgressingWzSerializer
    {
        protected string indent;
        protected string lineBreak;
        public static NumberFormatInfo formattingInfo;
        protected bool bExportBase64Data = false;

        protected static char[] amp = "&amp;".ToCharArray();
        protected static char[] lt = "&lt;".ToCharArray();
        protected static char[] gt = "&gt;".ToCharArray();
        protected static char[] apos = "&apos;".ToCharArray();
        protected static char[] quot = "&quot;".ToCharArray();

        static WzSerializer()
        {
            formattingInfo = new NumberFormatInfo
            {
                NumberDecimalSeparator = ".",
                NumberGroupSeparator = ","
            };
        }

        public WzSerializer(int indentation, LineBreak lineBreakType)
        {
            switch (lineBreakType)
            {
                case LineBreak.None:
                    lineBreak = "";
                    break;
                case LineBreak.Windows:
                    lineBreak = "\r\n";
                    break;
                case LineBreak.Unix:
                    lineBreak = "\n";
                    break;
            }
            indent = new string(' ', indentation);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tw"></param>
        /// <param name="depth"></param>
        /// <param name="prop"></param>
        /// <param name="exportFilePath"></param>
        protected void WritePropertyToXML(TextWriter tw, string depth, WzImageProperty prop, string exportFilePath)
        {
            if (prop is WzCanvasProperty)
            {
                WzCanvasProperty property3 = (WzCanvasProperty)prop;
                if (bExportBase64Data)
                {
                    MemoryStream stream = new MemoryStream();
                    property3.PngProperty.GetImage(false).Save(stream, ImageFormat.Png);
                    byte[] pngbytes = stream.ToArray();
                    stream.Close();
                    tw.Write(string.Concat(new object[] { depth, "<canvas name=\"", XmlUtil.SanitizeText(property3.Name), "\" width=\"", property3.PngProperty.Width, "\" height=\"", property3.PngProperty.Height, "\" basedata=\"", Convert.ToBase64String(pngbytes), "\">" }) + lineBreak);
                }
                else
                    tw.Write(string.Concat(new object[] { depth, "<canvas name=\"", XmlUtil.SanitizeText(property3.Name), "\" width=\"", property3.PngProperty.Width, "\" height=\"", property3.PngProperty.Height, "\">" }) + lineBreak);
                string newDepth = depth + indent;
                foreach (WzImageProperty property in property3.WzProperties)
                {
                    WritePropertyToXML(tw, newDepth, property, exportFilePath);
                }
                tw.Write(depth + "</canvas>" + lineBreak);
            }
            else if (prop is WzIntProperty)
            {
                WzIntProperty property4 = (WzIntProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<int name=\"", XmlUtil.SanitizeText(property4.Name), "\" value=\"", property4.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzDoubleProperty)
            {
                WzDoubleProperty property5 = (WzDoubleProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<double name=\"", XmlUtil.SanitizeText(property5.Name), "\" value=\"", property5.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzNullProperty)
            {
                WzNullProperty property6 = (WzNullProperty)prop;
                tw.Write(depth + "<null name=\"" + XmlUtil.SanitizeText(property6.Name) + "\"/>" + lineBreak);
            }
            else if (prop is WzBinaryProperty)
            {
                WzBinaryProperty property7 = (WzBinaryProperty)prop;
                if (bExportBase64Data)
                    tw.Write(string.Concat(new object[] { depth, "<sound name=\"", XmlUtil.SanitizeText(property7.Name), "\" length=\"", property7.Length.ToString(), "\" basehead=\"", Convert.ToBase64String(property7.Header), "\" basedata=\"", Convert.ToBase64String(property7.GetBytes(false)), "\"/>" }) + lineBreak);
                else
                    tw.Write(depth + "<sound name=\"" + XmlUtil.SanitizeText(property7.Name) + "\"/>" + lineBreak);
            }
            else if (prop is WzStringProperty)
            {
                WzStringProperty property8 = (WzStringProperty)prop;
                string str = XmlUtil.SanitizeText(property8.Value);
                tw.Write(depth + "<string name=\"" + XmlUtil.SanitizeText(property8.Name) + "\" value=\"" + str + "\"/>" + lineBreak);
            }
            else if (prop is WzSubProperty)
            {
                WzSubProperty property9 = (WzSubProperty)prop;
                tw.Write(depth + "<imgdir name=\"" + XmlUtil.SanitizeText(property9.Name) + "\">" + lineBreak);
                string newDepth = depth + indent;
                foreach (WzImageProperty property in property9.WzProperties)
                {
                    WritePropertyToXML(tw, newDepth, property, exportFilePath);
                }
                tw.Write(depth + "</imgdir>" + lineBreak);
            }
            else if (prop is WzShortProperty)
            {
                WzShortProperty property10 = (WzShortProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<short name=\"", XmlUtil.SanitizeText(property10.Name), "\" value=\"", property10.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzLongProperty)
            {
                WzLongProperty long_prop = (WzLongProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<long name=\"", XmlUtil.SanitizeText(long_prop.Name), "\" value=\"", long_prop.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzUOLProperty)
            {
                WzUOLProperty property11 = (WzUOLProperty)prop;
                tw.Write(depth + "<uol name=\"" + property11.Name + "\" value=\"" + XmlUtil.SanitizeText(property11.Value) + "\"/>" + lineBreak);
            }
            else if (prop is WzVectorProperty)
            {
                WzVectorProperty property12 = (WzVectorProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<vector name=\"", XmlUtil.SanitizeText(property12.Name), "\" x=\"", property12.X.Value, "\" y=\"", property12.Y.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzFloatProperty)
            {
                WzFloatProperty property13 = (WzFloatProperty)prop;
                string str2 = Convert.ToString(property13.Value, formattingInfo);
                if (!str2.Contains("."))
                    str2 = str2 + ".0";
                tw.Write(depth + "<float name=\"" + XmlUtil.SanitizeText(property13.Name) + "\" value=\"" + str2 + "\"/>" + lineBreak);
            }
            else if (prop is WzConvexProperty)
            {
                tw.Write(depth + "<extended name=\"" + XmlUtil.SanitizeText(prop.Name) + "\">" + lineBreak);

                WzConvexProperty property14 = (WzConvexProperty)prop;
                string newDepth = depth + indent;
                foreach (WzImageProperty property in property14.WzProperties)
                {
                    WritePropertyToXML(tw, newDepth, property, exportFilePath);
                }
                tw.Write(depth + "</extended>" + lineBreak);
            }
            else if (prop is WzLuaProperty propertyLua)
            {
                string parentName = propertyLua.Parent.Name;

                tw.Write(depth);
                tw.Write(lineBreak);
                if (bExportBase64Data)
                {

                }
                // Export standalone file here
                using (TextWriter twLua = new StreamWriter(File.Create(exportFilePath.Replace(parentName + ".xml", parentName))))
                {
                    twLua.Write(propertyLua.ToString());
                }
            }
        }

        /// <summary>
        /// Writes WzImageProperty to Json
        /// </summary>
        /// <param name="json"></param>
        /// <param name="depth"></param>
        /// <param name="prop"></param>
        /// <param name="exportFilePath"></param>
        protected void WritePropertyToJsonBson(Dictionary<string, object> json, WzImageProperty prop, string exportFilePath)
        {
            const string FIELD_TYPE_NAME = "_dirType"; // avoid the same naming as anything in the WZ to avoid exceptions
            //const string FIELD_DEPTH_NAME = "_depth";
            const string FIELD_NAME_NAME = "_dirName";

            const string FIELD_WIDTH_NAME = "_width";
            const string FIELD_HEIGHT_NAME = "_height";

            const string FIELD_X_NAME = "_x";
            const string FIELD_Y_NAME = "_y";

            const string FIELD_BASEDATA_NAME = "_image";

            const string FIELD_VALUE_NAME = "_value";

            const string FIELD_LENGTH_NAME = "_length";
            const string FIELD_FILENAME_NAME = "_fileName";

            var propJson = new Dictionary<string, object>
            {
                { FIELD_NAME_NAME, prop.Name },
                { FIELD_TYPE_NAME, prop.PropertyType.ToString() }
            };

            switch (prop)
            {
                case WzCanvasProperty canvasProp:
                    propJson[FIELD_TYPE_NAME] = "canvas";
                    propJson[FIELD_WIDTH_NAME] = canvasProp.PngProperty.Width;
                    propJson[FIELD_HEIGHT_NAME] = canvasProp.PngProperty.Height;
                    if (bExportBase64Data && !propJson.ContainsKey(FIELD_BASEDATA_NAME))
                    {
                        using (MemoryStream stream = new MemoryStream())
                        {
                            canvasProp.PngProperty.GetImage(false)?.Save(stream, ImageFormat.Png);
                            propJson[FIELD_BASEDATA_NAME] = Convert.ToBase64String(stream.ToArray());
                        }
                    }
                    foreach (WzImageProperty subProp in canvasProp.WzProperties)
                    {
                        WritePropertyToJsonBson(propJson, subProp, exportFilePath);
                    }
                    break;

                case WzIntProperty intProp:
                    propJson[FIELD_VALUE_NAME] = intProp.Value;
                    break;

                case WzDoubleProperty doubleProp:
                    propJson[FIELD_VALUE_NAME] = doubleProp.Value;
                    break;

                case WzNullProperty _:
                    // No additional data needed for null property
                    break;

                case WzBinaryProperty binaryProp:
                    propJson[FIELD_TYPE_NAME] = "binary";
                    propJson[FIELD_LENGTH_NAME] = binaryProp.Length.ToString();
                    if (bExportBase64Data && !propJson.ContainsKey("basehead") && !propJson.ContainsKey("basedata"))
                    {
                        propJson["basehead"] = Convert.ToBase64String(binaryProp.Header);
                        propJson["basedata"] = Convert.ToBase64String(binaryProp.GetBytes(false));
                    }
                    break;

                case WzStringProperty stringProp:
                    propJson[FIELD_VALUE_NAME] = stringProp.Value;
                    break;

                case WzSubProperty subProp:
                    propJson[FIELD_TYPE_NAME] = "sub";
                    foreach (WzImageProperty subSubProp in subProp.WzProperties)
                    {
                        WritePropertyToJsonBson(propJson, subSubProp, exportFilePath);
                    }
                    break;

                case WzShortProperty shortProp:
                    propJson[FIELD_VALUE_NAME] = shortProp.Value;
                    break;

                case WzLongProperty longProp:
                    propJson[FIELD_VALUE_NAME] = longProp.Value;
                    break;

                case WzUOLProperty uolProp:
                    propJson[FIELD_TYPE_NAME] = "uol";
                    propJson[FIELD_VALUE_NAME] = uolProp.Value;
                    break;

                case WzVectorProperty vectorProp:
                    propJson[FIELD_TYPE_NAME] = "vector";
                    propJson[FIELD_X_NAME] = vectorProp.X.Value;
                    propJson[FIELD_Y_NAME] = vectorProp.Y.Value;
                    break;

                case WzFloatProperty floatProp:
                    propJson[FIELD_VALUE_NAME] = floatProp.Value;
                    break;

                case WzConvexProperty convexProp:
                    propJson[FIELD_TYPE_NAME] = "convex";
                    foreach (WzImageProperty subProp in convexProp.WzProperties)
                    {
                        WritePropertyToJsonBson(propJson, subProp, exportFilePath);
                    }
                    break;

                case WzLuaProperty luaProp:
                    propJson[FIELD_TYPE_NAME] = "lua";
                    propJson[FIELD_FILENAME_NAME] = luaProp.Parent.Name;
                    if (bExportBase64Data && !propJson.ContainsKey(FIELD_BASEDATA_NAME))
                    {
                        propJson[FIELD_BASEDATA_NAME] = luaProp.ToString();
                    }
                    break;

                default:
                    propJson[FIELD_VALUE_NAME] = prop.ToString();
                    break;
            }

            string jPropertyName = prop.Name;

            // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
            // FullPath = "Item.wz\\Install\\0380.img\\03800572\\info\\icon\\foothold\\foothold" <<< double 'foothold' here :( 
            if (!json.ContainsKey(jPropertyName))
            {
                json[jPropertyName] = propJson; // add this json to the main json object parent
            }
        }
    }

    public interface IWzFileSerializer
    {
        void SerializeFile(WzFile file, string path);
    }

    public interface IWzDirectorySerializer : IWzFileSerializer
    {
        void SerializeDirectory(WzDirectory dir, string path);
    }

    public interface IWzImageSerializer : IWzDirectorySerializer
    {
        void SerializeImage(WzImage img, string path);
    }

    public interface IWzObjectSerializer
    {
        void SerializeObject(WzObject file, string path);
    }

    public enum LineBreak
    {
        None,
        Windows,
        Unix
    }

    public class NoBase64DataException : Exception
    {
        public NoBase64DataException() : base() { }
        public NoBase64DataException(string message) : base(message) { }
        public NoBase64DataException(string message, Exception inner) : base(message, inner) { }
        protected NoBase64DataException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }

}
