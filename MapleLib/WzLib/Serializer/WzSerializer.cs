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
                    string pngBase64 = EncodeStreamBase64(stream);
                    stream.Close();
                    tw.Write(depth);
                    tw.Write("<canvas name=\"");
                    tw.Write(XmlUtil.SanitizeText(property3.Name));
                    tw.Write("\" width=\"");
                    tw.Write(property3.PngProperty.Width);
                    tw.Write("\" height=\"");
                    tw.Write(property3.PngProperty.Height);
                    tw.Write("\" basedata=\"");
                    tw.Write(pngBase64);
                    tw.Write("\">");
                    tw.Write(lineBreak);
                }
                else
                {
                    tw.Write(depth);
                    tw.Write("<canvas name=\"");
                    tw.Write(XmlUtil.SanitizeText(property3.Name));
                    tw.Write("\" width=\"");
                    tw.Write(property3.PngProperty.Width);
                    tw.Write("\" height=\"");
                    tw.Write(property3.PngProperty.Height);
                    tw.Write("\">");
                    tw.Write(lineBreak);
                }
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
                tw.Write(depth);
                tw.Write("<int name=\"");
                tw.Write(XmlUtil.SanitizeText(property4.Name));
                tw.Write("\" value=\"");
                tw.Write(property4.Value);
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzDoubleProperty)
            {
                WzDoubleProperty property5 = (WzDoubleProperty)prop;
                tw.Write(depth);
                tw.Write("<double name=\"");
                tw.Write(XmlUtil.SanitizeText(property5.Name));
                tw.Write("\" value=\"");
                tw.Write(property5.Value);
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzNullProperty)
            {
                WzNullProperty property6 = (WzNullProperty)prop;
                tw.Write(depth);
                tw.Write("<null name=\"");
                tw.Write(XmlUtil.SanitizeText(property6.Name));
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzBinaryProperty)
            {
                WzBinaryProperty property7 = (WzBinaryProperty)prop;
                if (bExportBase64Data)
                {
                    tw.Write(depth);
                    tw.Write("<sound name=\"");
                    tw.Write(XmlUtil.SanitizeText(property7.Name));
                    tw.Write("\" length=\"");
                    tw.Write(property7.Length);
                    tw.Write("\" basehead=\"");
                    tw.Write(Convert.ToBase64String(property7.Header));
                    tw.Write("\" basedata=\"");
                    tw.Write(Convert.ToBase64String(property7.GetBytes(false)));
                    tw.Write("\"/>");
                    tw.Write(lineBreak);
                }
                else
                {
                    tw.Write(depth);
                    tw.Write("<sound name=\"");
                    tw.Write(XmlUtil.SanitizeText(property7.Name));
                    tw.Write("\"/>");
                    tw.Write(lineBreak);
                }
            }
            else if (prop is WzStringProperty)
            {
                WzStringProperty property8 = (WzStringProperty)prop;
                string str = XmlUtil.SanitizeText(property8.Value);
                tw.Write(depth);
                tw.Write("<string name=\"");
                tw.Write(XmlUtil.SanitizeText(property8.Name));
                tw.Write("\" value=\"");
                tw.Write(str);
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzSubProperty)
            {
                WzSubProperty property9 = (WzSubProperty)prop;
                tw.Write(depth);
                tw.Write("<imgdir name=\"");
                tw.Write(XmlUtil.SanitizeText(property9.Name));
                tw.Write("\">");
                tw.Write(lineBreak);
                string newDepth = depth + indent;
                foreach (WzImageProperty property in property9.WzProperties)
                {
                    WritePropertyToXML(tw, newDepth, property, exportFilePath);
                }
                tw.Write(depth);
                tw.Write("</imgdir>");
                tw.Write(lineBreak);
            }
            else if (prop is WzShortProperty)
            {
                WzShortProperty property10 = (WzShortProperty)prop;
                tw.Write(depth);
                tw.Write("<short name=\"");
                tw.Write(XmlUtil.SanitizeText(property10.Name));
                tw.Write("\" value=\"");
                tw.Write(property10.Value);
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzLongProperty)
            {
                WzLongProperty long_prop = (WzLongProperty)prop;
                tw.Write(depth);
                tw.Write("<long name=\"");
                tw.Write(XmlUtil.SanitizeText(long_prop.Name));
                tw.Write("\" value=\"");
                tw.Write(long_prop.Value);
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzUOLProperty)
            {
                WzUOLProperty property11 = (WzUOLProperty)prop;
                tw.Write(depth);
                tw.Write("<uol name=\"");
                tw.Write(XmlUtil.SanitizeText(property11.Name));
                tw.Write("\" value=\"");
                tw.Write(XmlUtil.SanitizeText(property11.Value));
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzVectorProperty)
            {
                WzVectorProperty property12 = (WzVectorProperty)prop;
                tw.Write(depth);
                tw.Write("<vector name=\"");
                tw.Write(XmlUtil.SanitizeText(property12.Name));
                tw.Write("\" x=\"");
                tw.Write(property12.X.Value);
                tw.Write("\" y=\"");
                tw.Write(property12.Y.Value);
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzFloatProperty)
            {
                WzFloatProperty property13 = (WzFloatProperty)prop;
                string str2 = Convert.ToString(property13.Value, formattingInfo);
                if (!str2.Contains("."))
                    str2 = str2 + ".0";
                tw.Write(depth);
                tw.Write("<float name=\"");
                tw.Write(XmlUtil.SanitizeText(property13.Name));
                tw.Write("\" value=\"");
                tw.Write(str2);
                tw.Write("\"/>");
                tw.Write(lineBreak);
            }
            else if (prop is WzConvexProperty)
            {
                tw.Write(depth);
                tw.Write("<extended name=\"");
                tw.Write(XmlUtil.SanitizeText(prop.Name));
                tw.Write("\">");
                tw.Write(lineBreak);

                WzConvexProperty property14 = (WzConvexProperty)prop;
                string newDepth = depth + indent;
                foreach (WzImageProperty property in property14.WzProperties)
                {
                    WritePropertyToXML(tw, newDepth, property, exportFilePath);
                }
                tw.Write(depth);
                tw.Write("</extended>");
                tw.Write(lineBreak);
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

            // Reserve room for the common type/name pair and one payload field;
            // this avoids the first resize for the common scalar properties.
            var propJson = new Dictionary<string, object>(capacity: 3)
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
                    if (bExportBase64Data)
                    {
                        using (MemoryStream stream = new MemoryStream())
                        {
                            canvasProp.PngProperty.GetImage(false)?.Save(stream, ImageFormat.Png);
                            propJson[FIELD_BASEDATA_NAME] = EncodeStreamBase64(stream);
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
                    if (bExportBase64Data)
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
                    if (bExportBase64Data)
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
            // Preserve duplicate-name behavior (the first property wins) with one
            // lookup instead of ContainsKey followed by an indexer write.
            json.TryAdd(jPropertyName, propJson);
        }

        private static string EncodeStreamBase64(MemoryStream stream)
        {
            if (stream.TryGetBuffer(out ArraySegment<byte> segment) && segment.Array != null)
                return Convert.ToBase64String(segment.Array, segment.Offset, checked((int)stream.Length));

            return Convert.ToBase64String(stream.ToArray());
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
