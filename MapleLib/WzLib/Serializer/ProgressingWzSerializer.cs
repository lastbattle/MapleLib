using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapleLib.WzLib.Serializer
{
    public abstract class ProgressingWzSerializer
    {
        protected int total = 0;
        protected int curr = 0;
        public int Total { get { return total; } }
        public int Current { get { return curr; } }

        protected static void CreateDirSafe(ref string path)
        {
            if (path.Substring(path.Length - 1, 1) == @"\")
                path = path.Substring(0, path.Length - 1);

            string basePath = path;
            int curridx = 0;
            while (Directory.Exists(path) || File.Exists(path))
            {
                curridx++;
                path = basePath + curridx;
            }
            Directory.CreateDirectory(path);
        }

        private readonly static string regexSearch = ":" + new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        private readonly static Regex regex_invalidPath = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
        /// <summary>
        /// Escapes invalid file name and paths (if nexon uses any illegal character that causes issue during saving)
        /// </summary>
        /// <param name="path"></param>
        public static string EscapeInvalidFilePathNames(string path)
        {
            return regex_invalidPath.Replace(path, "");
        }
    }
}
