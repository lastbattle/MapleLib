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
            MemoryStream stream = null;
            WzBinaryReader wzReader = null;
            WzImage img = null;
            try
            {
                stream = new MemoryStream(bytes);
                wzReader = new WzBinaryReader(stream, iv);
                img = new WzImage(name, wzReader)
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
            catch
            {
                // The image owns the reader once constructed.  Before that
                // point, close whichever lower-level resource was acquired.
                if (img != null)
                    img.Dispose();
                else if (wzReader != null)
                    wzReader.Dispose();
                else
                    stream?.Dispose();
                throw;
            }
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
            FileStream stream = null;
            WzBinaryReader wzReader = null;
            WzImage img = null;
            try
            {
                stream = File.OpenRead(inPath);
                if (stream.Length > int.MaxValue)
                    throw new InvalidDataException("IMG files larger than 2 GiB are not supported.");

                wzReader = new WzBinaryReader(stream, iv);
                img = new WzImage(name, wzReader)
                {
                    BlockSize = checked((int)stream.Length)
                };

                img.CalculateAndSetImageChecksum(stream);
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
            catch
            {
                if (img != null)
                    img.Dispose();
                else
                {
                    if (wzReader != null)
                        wzReader.Dispose();
                    else
                        stream?.Dispose();
                }
                throw;
            }
        }
    }

}
