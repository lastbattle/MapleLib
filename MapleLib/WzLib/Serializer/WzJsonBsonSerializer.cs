using Newtonsoft.Json.Bson;
using MapleLib;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The BSON export path requires Newtonsoft.Json.Bson, which has no native AOT metadata equivalent.")]
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

            string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (FileStream file = File.Create(temporaryPath))
                {
                    if (!bExportAsJson)
                    {
                        // BSON serialization using Newtonsoft.Json
                        // as System.Text.Json doesn't support BSON natively yet.

                        using (MemoryStream ms = new MemoryStream())
                        {
                    using (BsonDataWriter writer = new BsonDataWriter(ms))
                    {
                        // Keep the stream open for the length and buffer reads
                        // that follow the writer's disposal.
                        writer.CloseOutput = false;
                        Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                        serializer.Serialize(writer, jsonObject);
                    }

                            if (ms.TryGetBuffer(out ArraySegment<byte> bsonBuffer))
                            {
                                await file.WriteAsync(bsonBuffer.AsMemory(0, checked((int)ms.Length)));
                            }
                            else
                            {
                                byte[] bsonData = ms.ToArray();
                                await file.WriteAsync(bsonData, 0, bsonData.Length);
                            }
                        }
                    }
                    else // JSON serialization
                    {
                        await JsonSerializer.SerializeAsync(file, jsonObject, MapleJsonContext.Default.DictionaryStringObject);
                    }
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
                exportDirInternal(subdir, GetBoundOutputPath(path, EscapeInvalidFilePathNames(subdir.name)) + @"\");
            }
            foreach (WzImage subimg in dir.WzImages)
            {
                ExportInternal(subimg, GetBoundOutputPath(path, EscapeInvalidFilePathNames(subimg.Name)) + (bExportAsJson ? ".json" : ".bin"));
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
