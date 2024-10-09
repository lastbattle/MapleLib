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
            if (!parsed)
                img.ParseImage();
            curr++;

            if (File.Exists(path))
                File.Delete(path);
            using (TextWriter tw = new StreamWriter(File.Create(path)))
            {
                tw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + lineBreak);
                tw.Write("<imgdir name=\"" + XmlUtil.SanitizeText(img.Name) + "\">" + lineBreak);
                foreach (WzImageProperty property in img.WzProperties)
                {
                    WritePropertyToXML(tw, indent, property, path);
                }
                tw.Write("</imgdir>" + lineBreak);
            }

            if (!parsed)
                img.UnparseImage();
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
            if (Path.GetExtension(path) != ".xml") path += ".xml";
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
