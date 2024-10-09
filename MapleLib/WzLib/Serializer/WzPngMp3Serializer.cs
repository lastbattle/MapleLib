using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.Serializer
{
    /// <summary>
    /// Serialiser for MP3
    /// </summary>
    public class WzPngMp3Serializer : ProgressingWzSerializer, IWzImageSerializer, IWzObjectSerializer
    {
        //List<WzImage> imagesToUnparse = new List<WzImage>();
        private string outPath;

        public void SerializeObject(WzObject obj, string outPath)
        {
            //imagesToUnparse.Clear();
            total = 0; curr = 0;
            this.outPath = outPath;
            if (!Directory.Exists(outPath))
            {
                CreateDirSafe(ref outPath);
            }

            if (outPath.Substring(outPath.Length - 1, 1) != @"\")
                outPath += @"\";

            total = CalculateTotal(obj);
            ExportRecursion(obj, outPath);
            /*foreach (WzImage img in imagesToUnparse)
                img.UnparseImage();
            imagesToUnparse.Clear();*/
        }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeObject(file, path);
        }

        public void SerializeDirectory(WzDirectory file, string path)
        {
            SerializeObject(file, path);
        }

        public void SerializeImage(WzImage file, string path)
        {
            SerializeObject(file, path);
        }

        private int CalculateTotal(WzObject currObj)
        {
            int result = 0;
            if (currObj is WzFile file)
            {
                result += file.WzDirectory.CountImages();
            }
            else if (currObj is WzDirectory directory)
            {
                result += directory.CountImages();
            }
            return result;
        }

        private void ExportRecursion(WzObject currObj, string outPath)
        {
            if (currObj is WzFile wzFile)
            {
                ExportRecursion(wzFile.WzDirectory, outPath);
            }
            else if (currObj is WzDirectory directoryProperty)
            {
                outPath += EscapeInvalidFilePathNames(currObj.Name) + @"\";
                if (!Directory.Exists(outPath))
                    Directory.CreateDirectory(outPath);

                foreach (WzDirectory subdir in directoryProperty.WzDirectories)
                {
                    ExportRecursion(subdir, outPath + subdir.Name + @"\");
                }
                foreach (WzImage subimg in directoryProperty.WzImages)
                {
                    ExportRecursion(subimg, outPath + subimg.Name + @"\");
                }
            }
            else if (currObj is WzCanvasProperty canvasProperty)
            {
                Bitmap bmp = canvasProperty.GetLinkedWzCanvasBitmap();

                string path = outPath + EscapeInvalidFilePathNames(currObj.Name) + ".png";

                bmp.Save(path);
                //curr++;
            }
            else if (currObj is WzBinaryProperty binProperty)
            {
                string path = outPath + EscapeInvalidFilePathNames(currObj.Name) + ".mp3";

                binProperty.SaveToFile(path);
            }
            else if (currObj is WzImage wzImage)
            {
                outPath += EscapeInvalidFilePathNames(currObj.Name) + @"\";
                if (!Directory.Exists(outPath))

                    Directory.CreateDirectory(outPath);

                bool parse = wzImage.Parsed || wzImage.Changed;
                if (!parse)
                {
                    wzImage.ParseImage();
                }
                foreach (WzImageProperty subprop in wzImage.WzProperties)
                {
                    ExportRecursion(subprop, outPath);
                }
                if (!parse)
                {
                    wzImage.UnparseImage();
                }
                curr++;
            }
            else if (currObj is IPropertyContainer container)
            {
                outPath += EscapeInvalidFilePathNames(currObj.Name) + ".";

                foreach (WzImageProperty subprop in container.WzProperties)
                {
                    ExportRecursion(subprop, outPath);
                }
            }
            else if (currObj is WzUOLProperty property)
            {
                WzObject linkValue = property.LinkValue;

                if (linkValue is WzCanvasProperty canvas)
                {
                    ExportRecursion(canvas, outPath);
                }
            }
        }
    }
}
