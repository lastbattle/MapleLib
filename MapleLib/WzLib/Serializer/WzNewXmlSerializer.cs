using MapleLib.WzLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.Serializer
{

    /// <summary>
    /// Serialiser for new XML
    /// </summary>
    public class WzNewXmlSerializer : WzSerializer
    {
        public WzNewXmlSerializer(int indentation, LineBreak lineBreakType)
            : base(indentation, lineBreakType)
        { }

        internal void DumpImageToXML(TextWriter tw, string depth, WzImage img, string exportFilePath)
        {
            bool parsed = img.Parsed || img.Changed;
            if (!parsed)
                img.ParseImage();

            curr++;
            tw.Write(depth + "<wzimg name=\"" + XmlUtil.SanitizeText(img.Name) + "\">" + lineBreak);
            string newDepth = depth + indent;
            foreach (WzImageProperty property in img.WzProperties)
            {
                WritePropertyToXML(tw, newDepth, property, exportFilePath);
            }
            tw.Write(depth + "</wzimg>");
            if (!parsed)
                img.UnparseImage();
        }

        internal void DumpDirectoryToXML(TextWriter tw, string depth, WzDirectory dir, string exportFilePath)
        {
            tw.Write(depth + "<wzdir name=\"" + XmlUtil.SanitizeText(dir.Name) + "\">" + lineBreak);
            foreach (WzDirectory subdir in dir.WzDirectories)
                DumpDirectoryToXML(tw, depth + indent, subdir, exportFilePath);
            foreach (WzImage img in dir.WzImages)
            {
                DumpImageToXML(tw, depth + indent, img, exportFilePath);
            }
            tw.Write(depth + "</wzdir>" + lineBreak);
        }

        /// <summary>
        /// Export combined XML
        /// </summary>
        /// <param name="objects"></param>
        /// <param name="exportFilePath"></param>
        public void ExportCombinedXml(List<WzObject> objects, string exportFilePath)
        {
            total = 1; curr = 0;

            if (Path.GetExtension(exportFilePath) != ".xml")
                exportFilePath += ".xml";

            total += objects.OfType<WzImage>().Count();
            total += objects.OfType<WzDirectory>().Sum(d => d.CountImages());

            bExportBase64Data = true;

            using (TextWriter tw = new StreamWriter(exportFilePath))
            {
                tw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + lineBreak);
                tw.Write("<xmldump>" + lineBreak);
                foreach (WzObject obj in objects)
                {
                    if (obj is WzDirectory)
                    {
                        DumpDirectoryToXML(tw, indent, (WzDirectory)obj, exportFilePath);
                    }
                    else if (obj is WzImage)
                    {
                        DumpImageToXML(tw, indent, (WzImage)obj, exportFilePath);
                    }
                    else if (obj is WzImageProperty)
                    {
                        WritePropertyToXML(tw, indent, (WzImageProperty)obj, exportFilePath);
                    }
                }
                tw.Write("</xmldump>" + lineBreak);
            }
        }
    }
}
