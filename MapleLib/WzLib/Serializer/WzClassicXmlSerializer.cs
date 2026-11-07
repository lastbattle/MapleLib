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

                // A bare file name has no directory part; CreateDirectory("")
                // throws ArgumentException.
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // Create a copy of properties to avoid modification during enumeration
                var properties = img.WzProperties.ToList();

                string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
                try
                {
                    using (TextWriter tw = new StreamWriter(File.Create(temporaryPath)))
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

                    if (File.Exists(path))
                        File.Replace(temporaryPath, path, destinationBackupFileName: null);
                    else
                        File.Move(temporaryPath, path);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    catch (IOException)
                    {
                        // Cleanup must not mask the export result.
                    }
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
                exportDirXmlInternal(subdir, GetBoundOutputPath(path, EscapeInvalidFilePathNames(subdir.name)) + @"\");
            }
            foreach (WzImage subimg in dir.WzImages)
            {
                exportXmlInternal(subimg, GetBoundOutputPath(path, EscapeInvalidFilePathNames(subimg.Name)) + ".xml");
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
