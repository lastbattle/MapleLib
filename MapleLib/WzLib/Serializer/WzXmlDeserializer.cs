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
        public static NumberFormatInfo formattingInfo;

        private readonly bool useMemorySaving;
        private readonly byte[] iv;
        private readonly WzImgDeserializer imgDeserializer = new WzImgDeserializer(false);

        public WzXmlDeserializer(bool useMemorySaving, byte[] iv)
            : base()
        {
            this.useMemorySaving = useMemorySaving;
            this.iv = iv;
        }

        #region Public Functions
        public List<WzObject> ParseXML(string path)
        {
            List<WzObject> result = new List<WzObject>();
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlElement mainElement = (XmlElement)doc.ChildNodes[1];
            curr = 0;
            if (mainElement.Name == "xmldump")
            {
                total = CountImgs(mainElement);
                foreach (XmlElement subelement in mainElement)
                {
                    if (subelement.Name == "wzdir")
                        result.Add(ParseXMLWzDir(subelement));
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
        internal int CountImgs(XmlElement element)
        {
            // Count the number of "wzimg" elements and the number of "wzdir" elements
            int wzimgCount = element.Cast<XmlElement>()
                .Count(e => e.Name == "wzimg");

            // Recursively count the number of "wzimg" elements in each "wzdir" element
            int wzimgInWzdirCount = element.Cast<XmlElement>()
                .Where(e => e.Name == "wzdir")
                .Sum(e => CountImgs(e));

            // Return the total number of "wzimg" elements
            return wzimgCount + wzimgInWzdirCount;
        }


        internal WzDirectory ParseXMLWzDir(XmlElement dirElement)
        {
            WzDirectory result = new WzDirectory(dirElement.GetAttribute("name"));
            foreach (XmlElement subelement in dirElement)
            {
                if (subelement.Name == "wzdir")
                    result.AddDirectory(ParseXMLWzDir(subelement));
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
            foreach (XmlElement subelement in imgElement)
            {
                result.WzProperties.Add(ParsePropertyFromXMLElement(subelement));
            }
            result.Changed = true;
            if (useMemorySaving)
            {
                string path = Path.GetTempFileName();
                try
                {
                    using (WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), iv))
                    {
                        result.SaveImage(wzWriter);
                        result.Dispose();
                    }

                    bool successfullyParsedImage;
                    result = imgDeserializer.WzImageFromIMGFile(path, iv, name, out successfullyParsedImage);
                }
                finally
                {
                    File.Delete(path);
                }
            }
            return result;
        }

        internal WzImageProperty ParsePropertyFromXMLElement(XmlElement element)
        {
            switch (element.Name)
            {
                case "imgdir":
                    WzSubProperty sub = new WzSubProperty(element.GetAttribute("name"));
                    foreach (XmlElement subelement in element)
                        sub.AddProperty(ParsePropertyFromXMLElement(subelement));
                    return sub;

                case "canvas":
                    WzCanvasProperty canvas = new WzCanvasProperty(element.GetAttribute("name"));
                    if (!element.HasAttribute("basedata"))
                        throw new NoBase64DataException("no base64 data in canvas element with name " + canvas.Name);
                    canvas.PngProperty = new WzPngProperty();
                    MemoryStream pngstream = new MemoryStream(Convert.FromBase64String(element.GetAttribute("basedata")));
                    canvas.PngProperty.PNG = (Bitmap)Image.FromStream(pngstream, true, true);
                    foreach (XmlElement subelement in element)
                        canvas.AddProperty(ParsePropertyFromXMLElement(subelement));
                    return canvas;

                case "int":
                    WzIntProperty compressedInt = new WzIntProperty(element.GetAttribute("name"), int.Parse(element.GetAttribute("value"), formattingInfo));
                    return compressedInt;

                case "double":
                    WzDoubleProperty doubleProp = new WzDoubleProperty(element.GetAttribute("name"), double.Parse(element.GetAttribute("value"), formattingInfo));
                    return doubleProp;

                case "null":
                    WzNullProperty nullProp = new WzNullProperty(element.GetAttribute("name"));
                    return nullProp;

                case "sound":
                    if (!element.HasAttribute("basedata") || !element.HasAttribute("basehead") || !element.HasAttribute("length")) throw new NoBase64DataException("no base64 data in sound element with name " + element.GetAttribute("name"));
                    WzBinaryProperty sound = new WzBinaryProperty(element.GetAttribute("name"),
                        int.Parse(element.GetAttribute("length")),
                        Convert.FromBase64String(element.GetAttribute("basehead")),
                        Convert.FromBase64String(element.GetAttribute("basedata")));
                    return sound;

                case "string":
                    WzStringProperty stringProp = new WzStringProperty(element.GetAttribute("name"), element.GetAttribute("value"));
                    return stringProp;

                case "short":
                    WzShortProperty shortProp = new WzShortProperty(element.GetAttribute("name"), short.Parse(element.GetAttribute("value"), formattingInfo));
                    return shortProp;

                case "long":
                    WzLongProperty longProp = new WzLongProperty(element.GetAttribute("name"), long.Parse(element.GetAttribute("value"), formattingInfo));
                    return longProp;

                case "uol":
                    WzUOLProperty uol = new WzUOLProperty(element.GetAttribute("name"), element.GetAttribute("value"));
                    return uol;

                case "vector":
                    WzVectorProperty vector = new WzVectorProperty(element.GetAttribute("name"), new WzIntProperty("x", Convert.ToInt32(element.GetAttribute("x"))), new WzIntProperty("y", Convert.ToInt32(element.GetAttribute("y"))));
                    return vector;

                case "float":
                    WzFloatProperty floatProp = new WzFloatProperty(element.GetAttribute("name"), float.Parse(element.GetAttribute("value"), formattingInfo));
                    return floatProp;

                case "extended":
                    WzConvexProperty convex = new WzConvexProperty(element.GetAttribute("name"));
                    foreach (XmlElement subelement in element)
                        convex.AddProperty(ParsePropertyFromXMLElement(subelement));
                    return convex;
            }
            throw new InvalidDataException("unknown XML prop " + element.Name);
        }
        #endregion
    }
}
