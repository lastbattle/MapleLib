using MapleLib.WzLib.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.Serializer
{
    public class WzFileExporter
    {
        private const string WZ_EXTRACT_ERROR_FILE = "WzExtract_Errors.txt";

        /// <summary>
        /// Extracts and processes WZ files based on the specified parameters.
        /// Called when exporting WZ files to various formats (XML, IMG, BSON, etc.)
        /// </summary>
        /// <param name="wzFilesToDump">Array of WZ file paths to process</param>
        /// <param name="baseDir">Base output directory path</param>
        /// <param name="version">The MapleStory encryption key</param>
        /// <param name="serializer" Serializer to use for file processing></param>
        /// <param name="progressCallback">Optional callback to report progress (current index, total count)</param>
        public static bool RunWzFilesExtraction(string[] wzFilesToDump, string baseDir, WzMapleVersion version, IWzFileSerializer serializer,
             Action<int>? progressCallback = null)
        {
            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            foreach (string wzpath in wzFilesToDump)
            {
                if (WzTool.IsListFile(wzpath))
                {
                    //Warning.Error(string.Format(HaRepacker.Properties.Resources.MainListWzDetected, wzpath));
                    continue;
                }
                using (WzFile f = new WzFile(wzpath, version)) {

                    WzFileParseStatus parseStatus = f.ParseWzFile();

                    serializer.SerializeFile(f, Path.Combine(baseDir, f.Name));

                    // Update progress bar
                    progressCallback?.Invoke(1);
                }
            }
            MapleLib.Helpers.ErrorLogger.SaveToFile(WZ_EXTRACT_ERROR_FILE);

            return true;
        }

        /// <summary>
        /// Extracts and processes the WZ files into .img files
        /// </summary>
        /// <param name="dirsToDump"></param>
        /// <param name="imgsToDump"></param>
        /// <param name="baseDir"></param>
        /// <param name="serializer"></param>
        /// <param name="progressCallback"></param>
        public static void RunWzImgDirsExtraction(List<WzDirectory> dirsToDump, List<WzImage> imgsToDump, string baseDir, IWzImageSerializer serializer,
             Action<int>? progressCallback = null)
        {

            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            // Selected Wz Images
            foreach (WzImage img in imgsToDump)
            {
                string escapedPath = Path.Combine(baseDir, ProgressingWzSerializer.EscapeInvalidFilePathNames(img.Name));

                serializer.SerializeImage(img, escapedPath);

                // Update progress bar
                progressCallback?.Invoke(1);
            }
            // Selected Wz Dirs
            foreach (WzDirectory dir in dirsToDump)
            {
                string escapedPath = Path.Combine(baseDir, ProgressingWzSerializer.EscapeInvalidFilePathNames(dir.Name));

                serializer.SerializeDirectory(dir, escapedPath);

                // Update progress bar
                progressCallback?.Invoke(1);
            }

            // Loggers
            MapleLib.Helpers.ErrorLogger.SaveToFile(WZ_EXTRACT_ERROR_FILE);
        }

        /// <summary>
        /// Extracts and processes the WZ files into .xml files
        /// </summary>
        /// <param name="objsToDump"></param>
        /// <param name="path"></param>
        /// <param name="serializers"></param>
        /// <param name="progressCallback"></param>
        public static void RunWzXmlExtraction(List<WzObject> objsToDump, string path, ProgressingWzSerializer serializers,
            Action<bool,int>? progressCallback = null)
        {

#if DEBUG
            var watch = new Stopwatch();
            watch.Start();
#endif

            if (serializers is IWzObjectSerializer serializer)
            {
                progressCallback?.Invoke(true, objsToDump.Count);

                foreach (WzObject obj in objsToDump)
                {
                    serializer.SerializeObject(obj, path);

                    // Update progress bar
                    progressCallback?.Invoke(false, 1);
                }
            }
            else if (serializers is WzNewXmlSerializer serializer_)
            {
                progressCallback?.Invoke(true, 1);

                serializer_.ExportCombinedXml(objsToDump, path);

                progressCallback?.Invoke(false, 1);

            }
            MapleLib.Helpers.ErrorLogger.SaveToFile(WZ_EXTRACT_ERROR_FILE);
#if DEBUG
            // test benchmark
            watch.Stop();
            Debug.WriteLine($"WZ files Extracted. Execution Time: {watch.ElapsedMilliseconds} ms");
#endif
        }
    }
}
