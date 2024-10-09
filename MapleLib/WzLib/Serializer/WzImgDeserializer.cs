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
    /// Deserialiser for img
    /// </summary>
    public class WzImgDeserializer : ProgressingWzSerializer
    {
        private readonly bool freeResources;

        public WzImgDeserializer(bool freeResources)
            : base()
        {
            this.freeResources = freeResources;
        }

        public WzImage WzImageFromIMGBytes(byte[] bytes, WzMapleVersion version, string name, bool freeResources)
        {
            byte[] iv = WzTool.GetIvByMapleVersion(version);
            MemoryStream stream = new MemoryStream(bytes);
            WzBinaryReader wzReader = new WzBinaryReader(stream, iv);
            WzImage img = new WzImage(name, wzReader)
            {
                BlockSize = bytes.Length
            };
            img.CalculateAndSetImageChecksum(bytes);

            img.Offset = 0;
            if (freeResources)
            {
                img.ParseEverything = true;
                img.ParseImage(true);

                img.Changed = true;
                wzReader.Close();
            }
            return img;
        }

        /// <summary>
        /// Parse a WZ image from .img file/
        /// </summary>
        /// <param name="inPath"></param>
        /// <param name="iv"></param>
        /// <param name="name"></param>
        /// <param name="successfullyParsedImage"></param>
        /// <returns></returns>
        public WzImage WzImageFromIMGFile(string inPath, byte[] iv, string name, out bool successfullyParsedImage)
        {
            FileStream stream = File.OpenRead(inPath);
            WzBinaryReader wzReader = new WzBinaryReader(stream, iv);

            WzImage img = new WzImage(name, wzReader)
            {
                BlockSize = (int)stream.Length
            };
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            stream.Position = 0;
            img.CalculateAndSetImageChecksum(bytes);
            img.Offset = 0;

            if (freeResources)
            {
                img.ParseEverything = true;

                successfullyParsedImage = img.ParseImage(true);
                img.Changed = true;
                wzReader.Close();
            }
            else
            {
                successfullyParsedImage = true;
            }
            return img;
        }
    }

}
