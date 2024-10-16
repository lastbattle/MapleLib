using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MapleLib.WzLib.Serializer
{
    /// <summary>
    /// Serialiser for Json and Bson
    /// </summary>
    public class WzJsonBsonSerializer : WzSerializer, IWzImageSerializer
    {
        private readonly bool bExportAsJson; // otherwise bson

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="indentation"></param>
        /// <param name="lineBreakType"></param>
        /// <param name="bExportBase64Data"></param>
        /// <param name="bExportAsJson"></param>
        public WzJsonBsonSerializer(int indentation, LineBreak lineBreakType, bool bExportBase64Data, bool bExportAsJson)
            : base(indentation, lineBreakType)
        {
            this.bExportBase64Data = bExportBase64Data;
            this.bExportAsJson = bExportAsJson;
        }

        /// <summary>
        /// Synchronous version of the method for backwards compatibility
        /// </summary>
        /// <param name="img"></param>
        /// <param name="path"></param>
        private void ExportInternal(WzImage img, string path)
        {
            ExportInternalAsync(img, path).GetAwaiter().GetResult();
        }

        private async Task ExportInternalAsync(WzImage img, string path)
        {
            bool parsed = img.Parsed || img.Changed;
            if (!parsed)
                img.ParseImage();
            curr++;

            var jsonObject = new Dictionary<string, object>();
            foreach (WzImageProperty property in img.WzProperties)
            {
                WritePropertyToJsonBson(jsonObject, property, path);
            }

            if (File.Exists(path))
                File.Delete(path);

            using (FileStream file = File.Create(path))
            {
                if (!bExportAsJson)
                {
                    // BSON serialization using Newtonsoft.Json
                    // as System.Text.Json doesn't support BSON natively yet.

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (BsonDataWriter writer = new BsonDataWriter(ms))
                        {
                            Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                            serializer.Serialize(writer, jsonObject);
                        }

                        byte[] bsonData = ms.ToArray();
                        await file.WriteAsync(bsonData, 0, bsonData.Length);
                    }
                }
                else // JSON serialization
                {
                    await JsonSerializer.SerializeAsync(file, jsonObject, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }
            }

            if (!parsed)
                img.UnparseImage();
        }

        private void exportDirInternal(WzDirectory dir, string path)
        {
            if (!Directory.Exists(path))
                CreateDirSafe(ref path);

            if (path.Substring(path.Length - 1) != @"\")
                path += @"\";

            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                exportDirInternal(subdir, path + EscapeInvalidFilePathNames(subdir.name) + @"\");
            }
            foreach (WzImage subimg in dir.WzImages)
            {
                ExportInternal(subimg, path + EscapeInvalidFilePathNames(subimg.Name) + (bExportAsJson ? ".json" : ".bin"));
            }
        }

        public void SerializeImage(WzImage img, string path)
        {
            total = 1;
            curr = 0;

            if (Path.GetExtension(path) != (bExportAsJson ? ".json" : ".bin"))
                path += bExportAsJson ? ".json" : ".bin";
            ExportInternal(img, path);
        }

        public void SerializeDirectory(WzDirectory dir, string path)
        {
            total = dir.CountImages();
            curr = 0;
            exportDirInternal(dir, path);
        }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeDirectory(file.WzDirectory, path);
        }
    }
}
