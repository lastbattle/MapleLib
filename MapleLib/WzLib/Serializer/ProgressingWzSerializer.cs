using System;
using System.Collections.Generic;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
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

        private static readonly SearchValues<char> InvalidPathCharacters = SearchValues.Create(
            (":" + new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars())).AsSpan());
        /// <summary>
        /// Escapes invalid file name and paths (if nexon uses any illegal character that causes issue during saving)
        /// </summary>
        /// <param name="path"></param>
        public static string EscapeInvalidFilePathNames(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            ReadOnlySpan<char> input = path.AsSpan();
            int invalidIndex = input.IndexOfAny(InvalidPathCharacters);
            string escaped;
            if (invalidIndex < 0)
            {
                escaped = path.Substring(0, input.TrimEnd(" .").Length);
            }
            else
            {
                Span<char> buffer = path.Length <= 256 ? stackalloc char[path.Length] : new char[path.Length];
                int written = 0;
                do
                {
                    input.Slice(0, invalidIndex).CopyTo(buffer.Slice(written));
                    written += invalidIndex;
                    input = input.Slice(invalidIndex + 1);
                    invalidIndex = input.IndexOfAny(InvalidPathCharacters);
                } while (invalidIndex >= 0);

                input.CopyTo(buffer.Slice(written));
                written += input.Length;
                escaped = new string(buffer.Slice(0, written).TrimEnd(" ."));
            }
            if (escaped.Length == 0)
                return "_";

            int dotIndex = escaped.IndexOf('.');
            ReadOnlySpan<char> deviceName = dotIndex < 0 ? escaped.AsSpan() : escaped.AsSpan(0, dotIndex);
            if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                (deviceName.Length == 4 &&
                 (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                  deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                 deviceName[3] is >= '1' and <= '9'))
            {
                escaped = "_" + escaped;
            }

            return escaped;
        }
    }
}
