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
    /// Serialiser for img
    /// </summary>
    public class WzImgSerializer : ProgressingWzSerializer, IWzImageSerializer
    {

        public byte[] SerializeImage(WzImage img)
        {
            total = 1; curr = 0;

            using (MemoryStream stream = new MemoryStream())
            {
                using (WzBinaryWriter wzWriter = new WzBinaryWriter(stream, ((WzDirectory)img.parent).WzIv))
                {
                    img.SaveImage(wzWriter);
                    byte[] result = stream.ToArray();

                    return result;
                }
            }
        }

        public void SerializeImage(WzImage img, string outPath)
        {
            total = 1; curr = 0;
            if (Path.GetExtension(outPath) != ".img")
            {
                outPath += ".img";
            }

            using (FileStream stream = File.Create(outPath))
            {
                using (WzBinaryWriter wzWriter = new WzBinaryWriter(stream, ((WzDirectory)img.parent).WzIv))
                {
                    img.SaveImage(wzWriter, true,
                        forceReadFromData: true // update the pos of data relative to itself, instead of the wz
                        );
                }
            }
        }

        public void SerializeDirectory(WzDirectory dir, string outPath)
        {
            total = dir.CountImages();
            curr = 0;

            if (!Directory.Exists(outPath))
                CreateDirSafe(ref outPath);

            if (outPath.Substring(outPath.Length - 1, 1) != @"\")
            {
                outPath += @"\";
            }

            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                SerializeDirectory(subdir, outPath + subdir.Name + @"\");
            }
            foreach (WzImage img in dir.WzImages)
            {
                SerializeImage(img, outPath + img.Name);
            }
        }

        public void SerializeFile(WzFile f, string outPath)
        {
            SerializeDirectory(f.WzDirectory, outPath);
        }
    }
}
