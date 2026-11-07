using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
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

            using (XmlReader reader = XmlReader.Create(path, readerSettings))
            {
                doc.Load(reader);
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
                        if (!element.HasAttribute("basedata"))
                            throw new NoBase64DataException("no base64 data in canvas element with name " + canvas.Name);
                        string base64Image = element.GetAttribute("basedata");
                        if (base64Image.Length > MaxImportImageBase64Chars)
                            throw new InvalidDataException($"Canvas '{canvas.Name}' image payload exceeds the supported import size.");

                        canvas.PngProperty = new WzPngProperty();
                        using (MemoryStream pngstream = new MemoryStream(Convert.FromBase64String(base64Image)))
                        using (Image decodedImage = Image.FromStream(pngstream, true, true))
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
                    if (!int.TryParse(element.GetAttribute("length"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int soundLengthMs) || soundLengthMs < 0)
                        throw new InvalidDataException($"Sound '{element.GetAttribute("name")}' has an invalid length attribute.");

                    string base64Header = element.GetAttribute("basehead");
                    string baseData = element.GetAttribute("basedata");
                    if (base64Header.Length > MaxImportSoundHeaderBase64Chars)
                        throw new InvalidDataException($"Sound '{element.GetAttribute("name")}' header payload exceeds the supported import size.");
                    if (baseData.Length > MaxImportSoundDataBase64Chars)
                        throw new InvalidDataException($"Sound '{element.GetAttribute("name")}' data payload exceeds the supported import size.");

                    WzBinaryProperty sound = new WzBinaryProperty(element.GetAttribute("name"),
                        soundLengthMs,
                        Convert.FromBase64String(base64Header),
                        Convert.FromBase64String(baseData));
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

        private static int ParseIntAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ||
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        private static short ParseShortAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (short.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out short value) ||
                short.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        private static long ParseLongAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ||
                long.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        private static float ParseFloatAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            throw new InvalidDataException(DescribeBadNumber(element, attributeName, raw));
        }

        private static double ParseDoubleAttribute(XmlElement element, string attributeName)
        {
            string raw = element.GetAttribute(attributeName);
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
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
