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
    /// Serialiser for XML
    /// </summary>
    public class WzClassicXmlSerializer : WzSerializer, IWzImageSerializer
    {
        public WzClassicXmlSerializer(int indentation, LineBreak lineBreakType, bool exportbase64)
            : base(indentation, lineBreakType)
        { bExportBase64Data = exportbase64; }

        private void exportXmlInternal(WzImage img, string path)
        {
            bool parsed = img.Parsed || img.Changed;
            try
            {
                if (!parsed)
                    img.ParseImage();
                curr++;

                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                // Create a copy of properties to avoid modification during enumeration
                var properties = img.WzProperties.ToList();

                using (TextWriter tw = new StreamWriter(File.Create(path)))
                {
                    tw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                    tw.Write(lineBreak);
                    tw.Write("<imgdir name=\"");
                    tw.Write(XmlUtil.SanitizeText(img.Name));
                    tw.Write("\">");
                    tw.Write(lineBreak);

                    foreach (WzImageProperty property in properties)
                    {
                        WritePropertyToXML(tw, indent, property, path);
                    }
                    tw.Write("</imgdir>");
                    tw.Write(lineBreak);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export XML for image {img.Name}", ex);
            }
            finally
            {
                if (!parsed)
                    img.UnparseImage();
            }
        }

        private void exportDirXmlInternal(WzDirectory dir, string path)
        {
            if (!Directory.Exists(path))
                CreateDirSafe(ref path);

            if (path.Substring(path.Length - 1) != @"\")
                path += @"\";

            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                exportDirXmlInternal(subdir, path + EscapeInvalidFilePathNames(subdir.name) + @"\");
            }
            foreach (WzImage subimg in dir.WzImages)
            {
                exportXmlInternal(subimg, path + EscapeInvalidFilePathNames(subimg.Name) + ".xml");
            }
        }

        public void SerializeImage(WzImage img, string path)
        {
            total = 1; curr = 0;
            if (Path.GetExtension(path) != ".xml") 
                path += ".xml";
            exportXmlInternal(img, path);
        }

        public void SerializeDirectory(WzDirectory dir, string path)
        {
            total = dir.CountImages(); curr = 0;
            exportDirXmlInternal(dir, path);
        }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeDirectory(file.WzDirectory, path);
        }
    }
}
