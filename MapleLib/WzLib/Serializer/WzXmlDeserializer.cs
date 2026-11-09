using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MapleLib.WzLib.Serializer
{
    /// <summary>
    /// Deserialiser for XML
    /// </summary>
    public class WzXmlDeserializer : ProgressingWzSerializer
    {
        // The writers always emit numbers with an invariant separator. Parsing
        // through the ambient culture turned "0.25" into 25 on comma-decimal
        // locales (and "0,25" into garbage on dot-decimal ones). Invert the
        // preference: invariant first, ambient culture only as a fallback for
        // hand-edited files.
        public static NumberFormatInfo formattingInfo = NumberFormatInfo.InvariantInfo;

        private const long MaxImportImageBytes = 64L * 1024 * 1024;
        private const long MaxImportImageBase64Chars = (MaxImportImageBytes + 2) / 3 * 4;
        private const long MaxImportSoundHeaderBytes = MemoryLimits.MAX_WZ_HEADER_BYTES;
        private const long MaxImportSoundHeaderBase64Chars = (MaxImportSoundHeaderBytes + 2) / 3 * 4;
        private const long MaxImportSoundDataBytes = MemoryLimits.MAX_WZ_PAYLOAD_BYTES;
        private const long MaxImportSoundDataBase64Chars = (MaxImportSoundDataBytes + 2) / 3 * 4;

        private readonly bool useMemorySaving;
        private readonly byte[] iv;
        private readonly WzImgDeserializer imgDeserializer = new WzImgDeserializer(false);

        public WzXmlDeserializer(bool useMemorySaving, byte[] iv)
            : base()
        {
            this.useMemorySaving = useMemorySaving;
            this.iv = iv;
        }

        /// <summary>
        /// Enumerates child elements only. Imported XML may carry text nodes
        /// (stray text, or preserved whitespace) between the elements of a
        /// container; enumerating an XmlNodeList as XmlElement threw
        /// InvalidCastException on the first one instead of skipping it.
        /// </summary>
        private static IEnumerable<XmlElement> ChildElements(XmlElement element)
        {
            foreach (XmlNode node in element.ChildNodes)
            {
                if (node is XmlElement child)
                    yield return child;
            }
        }

        #region Public Functions
        public List<WzObject> ParseXML(string path)
        {
            List<WzObject> result = new List<WzObject>();
            XmlDocument doc = new XmlDocument();

            // Imported XML is untrusted. Parsing the DTD keeps internal-subset
            // entity references working, while the null resolver stops a
            // crafted external subset from touching the file system or network.
            // The character cap bounds entity amplification. The root element
            // is taken directly so a document type node cannot shift the child
            // indexes.
            XmlReaderSettings readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = null,
                MaxCharactersFromEntities = 1_000_000,
            };

            try
            {
                using XmlReader reader = XmlReader.Create(path, readerSettings);
                doc.Load(reader);
            }
            catch (ArgumentException ex)
            {
                // XmlReader validates the declaration and reports a bad
                // version as a bare ArgumentException ("Wrong XML version
                // information..."), outside the XmlException family the rest
                // of the importer uses.
                throw new InvalidDataException("XML declaration is invalid.", ex);
            }
            XmlElement mainElement = (XmlElement)doc.DocumentElement;
            curr = 0;
            if (mainElement.Name == "xmldump")
            {
                // The document element is not a directory level of its own, so
                // its children start at zero like the ParseXMLWzDir call below.
                total = CountImgs(mainElement, -1);
                foreach (XmlElement subelement in ChildElements(mainElement))
                {
                    if (subelement.Name == "wzdir")
                        result.Add(ParseXMLWzDir(subelement, 0));
                    else if (subelement.Name == "wzimg")
                        result.Add(ParseXMLWzImg(subelement));
                    else
                        throw new InvalidDataException("unknown XML prop " + subelement.Name);
                }
            }
            else if (mainElement.Name == "imgdir")
            {
                total = 1;
                result.Add(ParseXMLWzImg(mainElement));
                curr++;
            }
            else throw new InvalidDataException("unknown main XML prop " + mainElement.Name);
            return result;
        }
        #endregion

        #region Internal Functions
        internal int CountImgs(XmlElement element, int depth)
        {
            ValidateDirectoryDepth(depth);
            int count = 0;
            foreach (XmlElement child in ChildElements(element))
            {
                if (child.Name == "wzimg")
                    count++;
                else if (child.Name == "wzdir")
                    count += CountImgs(child, depth + 1);
            }

            return count;
        }


        internal WzDirectory ParseXMLWzDir(XmlElement dirElement, int depth)
        {
            ValidateDirectoryDepth(depth);
            WzDirectory result = new WzDirectory(dirElement.GetAttribute("name"));
            foreach (XmlElement subelement in ChildElements(dirElement))
            {
                if (subelement.Name == "wzdir")
                    result.AddDirectory(ParseXMLWzDir(subelement, depth + 1));
                else if (subelement.Name == "wzimg")
                    result.AddImage(ParseXMLWzImg(subelement));
                else throw new InvalidDataException("unknown XML prop " + subelement.Name);
            }
            return result;
        }

        internal WzImage ParseXMLWzImg(XmlElement imgElement)
        {
            string name = imgElement.GetAttribute("name");
            WzImage result = new WzImage(name);
            foreach (XmlElement subelement in ChildElements(imgElement))
            {
                result.WzProperties.Add(ParsePropertyFromXMLElement(subelement, 0));
            }
            result.Changed = true;
            if (useMemorySaving)
            {
                string path = Path.GetTempFileName();
                bool imageOwnsTemporaryFile = false;
                try
                {
                    using (WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), iv))
                    {
                        result.SaveImage(wzWriter);
                        result.Dispose();
                    }

                    bool successfullyParsedImage;
                    result = imgDeserializer.WzImageFromIMGFile(path, iv, name, out successfullyParsedImage);

                    if (result.ParseEverything)
                        File.Delete(path);
                    else
                    {
                        result.BackingFilePath = path;
                        imageOwnsTemporaryFile = true;
                    }
                }
                finally
                {
                    if (!imageOwnsTemporaryFile)
                        File.Delete(path);
                }
            }
            return result;
        }

        internal WzImageProperty ParsePropertyFromXMLElement(XmlElement element, int depth)
        {
            ValidatePropertyDepth(depth);
            switch (element.Name)
            {
                case "imgdir":
                    WzSubProperty sub = new WzSubProperty(element.GetAttribute("name"));
                    foreach (XmlElement subelement in ChildElements(element))
                        sub.AddProperty(ParsePropertyFromXMLElement(subelement, depth + 1));
                    return sub;

                case "canvas":
                    {
                        WzCanvasProperty canvas = new WzCanvasProperty(element.GetAttribute("name"));
                        string base64Image = element.GetAttribute("basedata");
                        canvas.PngProperty = new WzPngProperty();
                        if (base64Image.Length == 0)
                        {
                            // The exporter omits basedata when a canvas has no
                            // decodable payload (empty payload, or a link that
                            // resolved to nothing). Treat it as an empty
                            // canvas instead of rejecting the whole dump.
                            canvas.PngProperty.Width = ReadOptionalDimension(element, "width");
                            canvas.PngProperty.Height = ReadOptionalDimension(element, "height");
                        }
                        else
                        {
                            if (base64Image.Length > MaxImportImageBase64Chars)
                                throw new InvalidDataException($"Canvas '{canvas.Name}' image payload exceeds the supported import size.");

                            byte[] imagePayload;
                            try
                            {
                                imagePayload = Convert.FromBase64String(base64Image);
                            }
                            catch (FormatException ex)
                            {
                                throw new InvalidDataException($"Canvas '{canvas.Name}' basedata is not valid base64.", ex);
                            }

                            // GDI+ may still read from the stream while the
                            // decoded image is consumed below, so keep it alive
                            // for the whole scope.
                            using MemoryStream pngstream = new(imagePayload);
                            Image decodedImage;
                            try
                            {
                                decodedImage = Image.FromStream(pngstream, true, true);
                            }
                            catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
                            {
                                // GDI+ reports malformed image data as ArgumentException
                                // or ExternalException, and some corrupt streams as
                                // OutOfMemoryException.
                                throw new InvalidDataException($"Canvas '{canvas.Name}' basedata does not decode to an image.", ex);
                            }

                            using (decodedImage)
                            {
                                if (decodedImage is not Bitmap decodedBitmap)
                                    throw new InvalidDataException($"Canvas '{canvas.Name}' basedata does not decode to a bitmap image.");

                                // Imported XML is untrusted content: hold it to the same
                                // dimension and decoded-size limits the binary parser
                                // enforces before the property retains any of it.
                                long decodedBytes = (long)decodedBitmap.Width * decodedBitmap.Height * 4L;
                                if (decodedBitmap.Width <= 0 || decodedBitmap.Height <= 0 ||
                                    decodedBitmap.Width > WzPngFormatExtensions.MaxDimension ||
                                    decodedBitmap.Height > WzPngFormatExtensions.MaxDimension ||
                                    decodedBytes > WzPngFormatExtensions.MaxDecodedSize)
                                {
                                    throw new InvalidDataException($"Canvas '{canvas.Name}' dimensions {decodedBitmap.Width}x{decodedBitmap.Height} exceed the supported import limits.");
                                }

                                // The PNG setter consumes the pixels only and leaves the
                                // image unowned, so dispose it with this scope on every path.
                                canvas.PngProperty.PNG = decodedBitmap;
                            }
                        }
                        foreach (XmlElement subelement in ChildElements(element))
                            canvas.AddProperty(ParsePropertyFromXMLElement(subelement, depth + 1));
                        return canvas;
                    }

                case "int":
                    WzIntProperty compressedInt = new WzIntProperty(element.GetAttribute("name"), ParseIntAttribute(element, "value"));
                    return compressedInt;

                case "double":
                    WzDoubleProperty doubleProp = new WzDoubleProperty(element.GetAttribute("name"), ParseDoubleAttribute(element, "value"));
                    return doubleProp;

                case "null":
                    WzNullProperty nullProp = new WzNullProperty(element.GetAttribute("name"));
                    return nullProp;

                case "sound":
                    if (!element.HasAttribute("basedata") || !element.HasAttribute("basehead") || !element.HasAttribute("length")) throw new NoBase64DataException("no base64 data in sound element with name " + element.GetAttribute("name"));
                    if (!WzParse.TryInt(element.GetAttribute("length"), out int soundLengthMs) || soundLengthMs < 0)
                        throw new InvalidDataException($"Sound '{element.GetAttribute("name")}' has an invalid length attribute.");

                    string base64Header = element.GetAttribute("basehead");
                    string baseData = element.GetAttribute("basedata");
                    if (base64Header.Length > MaxImportSoundHeaderBase64Chars)
                        throw new InvalidDataException($"Sound '{element.GetAttribute("name")}' header payload exceeds the supported import size.");
                    if (baseData.Length > MaxImportSoundDataBase64Chars)
                        throw new InvalidDataException($"Sound '{element.GetAttribute("name")}' data payload exceeds the supported import size.");

                    string soundName = element.GetAttribute("name");
                    byte[] soundHeaderBytes;
                    byte[] soundDataBytes;
                    try
                    {
                        soundHeaderBytes = Convert.FromBase64String(base64Header);
                        soundDataBytes = Convert.FromBase64String(baseData);
                    }
                    catch (FormatException ex)
                    {
                        throw new InvalidDataException($"Sound '{soundName}' has base64 payload that does not decode.", ex);
                    }

                    WzBinaryProperty sound = new WzBinaryProperty(soundName,
                        soundLengthMs,
                        soundHeaderBytes,
                        soundDataBytes);
                    return sound;

                case "string":
                    WzStringProperty stringProp = new WzStringProperty(element.GetAttribute("name"), element.GetAttribute("value"));
                    return stringProp;

                case "short":
                    WzShortProperty shortProp = new WzShortProperty(element.GetAttribute("name"), ParseShortAttribute(element, "value"));
                    return shortProp;

                case "long":
                    WzLongProperty longProp = new WzLongProperty(element.GetAttribute("name"), ParseLongAttribute(element, "value"));
                    return longProp;

                case "uol":
                    WzUOLProperty uol = new WzUOLProperty(element.GetAttribute("name"), element.GetAttribute("value"));
                    return uol;

                case "lua":
                case "WzLua":
                    // "lua" is the classic dump form; "WzLua" is what
                    // WzLuaProperty.ExportXml writes. Both carry the decoded
                    // source in the value attribute.
                    WzLuaProperty lua = new(element.GetAttribute("name"), Array.Empty<byte>());
                    lua.SetString(element.GetAttribute("value"));
                    return lua;

                case "png":
                case "PNG":
                    {
                        WzPngProperty png = new();
                        string pngBase64 = element.GetAttribute("basedata");
                        int width = ReadOptionalDimension(element, "width");
                        int height = ReadOptionalDimension(element, "height");
                        WzPngFormat pngFormat = ParsePngFormatAttribute(element);
                        if (pngBase64.Length == 0)
                        {
                            png.Width = width > 0 ? width : 1;
                            png.Height = height > 0 ? height : 1;
                            png.Format = pngFormat;
                        }
                        else
                        {
                            if (pngBase64.Length > MaxImportImageBase64Chars)
                                throw new InvalidDataException($"PNG '{png.Name}' image payload exceeds the supported import size.");
                            byte[] pngBytes;
                            try
                            {
                                pngBytes = Convert.FromBase64String(pngBase64);
                            }
                            catch (FormatException ex)
                            {
                                throw new InvalidDataException($"PNG '{png.Name}' basedata is not valid base64.", ex);
                            }

                            png.SetCompressedBytes(pngBytes, width > 0 ? width : 1, height > 0 ? height : 1, pngFormat);
                        }
                        return png;
                    }

                case "vector":
                    WzVectorProperty vector = new WzVectorProperty(element.GetAttribute("name"), new WzIntProperty("x", ParseIntAttribute(element, "x")), new WzIntProperty("y", ParseIntAttribute(element, "y")));
                    return vector;

                case "float":
                    WzFloatProperty floatProp = new WzFloatProperty(element.GetAttribute("name"), ParseFloatAttribute(element, "value"));
                    return floatProp;

                case "extended":
                    WzConvexProperty convex = new WzConvexProperty(element.GetAttribute("name"));
                    foreach (XmlElement subelement in ChildElements(element))
                        convex.AddProperty(ParsePropertyFromXMLElement(subelement, depth + 1));
                    return convex;
            }
            throw new InvalidDataException("unknown XML prop " + element.Name);
        }

        private static string DescribeBadNumber(XmlElement element, string attributeName, string raw)
        {
            return $"XML {element.Name} '{element.GetAttribute("name")}' has an invalid '{attributeName}' value '{raw}'.";
        }

        private static int ReadOptionalDimension(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (WzParse.TryInt(raw, out int value) && value > 0)
                return value;

            return 0;
        }

        private static WzPngFormat ParsePngFormatAttribute(XmlElement element)
        {
            string raw = element.GetAttribute("format");
            if (WzParse.TryInt(raw, out int value) &&
                Enum.IsDefined(typeof(WzPngFormat), value))
            {
                return (WzPngFormat)value;
            }

            return WzPngFormat.Format1;
        }

        private static int ParseIntAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (WzParse.TryInt(raw, out int value) ||
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        private static short ParseShortAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (WzParse.TryShort(raw, out short value) ||
                short.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        private static long ParseLongAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (WzParse.TryLong(raw, out long value) ||
                long.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        private static float ParseFloatAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (WzParse.TryFloat(raw, out float value) ||
                float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        private static double ParseDoubleAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (WzParse.TryDouble(raw, out double value) ||
                double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        /// <summary>
        /// Imported XML nests through recursive descent, so the element tree
        /// needs the same depth budget the binary parser already applies to
        /// the relationships it mirrors. Without it a crafted dump a couple
        /// of thousand levels deep ends the process with a
        /// StackOverflowException that no caller can catch.
        /// </summary>
        private static void ValidatePropertyDepth(int depth)
        {
            if (depth > WzImageProperty.MaxPropertyNestingDepth)
                throw new InvalidDataException($"XML property nesting exceeds the supported depth of {WzImageProperty.MaxPropertyNestingDepth}.");
        }

        private static void ValidateDirectoryDepth(int depth)
        {
            if (depth > WzDirectory.MaxDirectoryDepth)
                throw new InvalidDataException($"XML directory nesting exceeds the supported depth of {WzDirectory.MaxDirectoryDepth}.");
        }
        #endregion
    }
}
