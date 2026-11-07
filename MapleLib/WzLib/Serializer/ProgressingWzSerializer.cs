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
            // A bare output name has no directory part, and a root-relative
            // path can trim down to nothing; both need a usable base for the
            // collision loop and CreateDirectory.
            if (path.Length > 0 && path[path.Length - 1] == '\\')
                path = path.Substring(0, path.Length - 1);
            if (path.Length == 0)
                path = ".";

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
            // A nameless node still needs an output stem; the empty escape
            // below yields "_".
            path ??= string.Empty;

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

        /// <summary>
        /// Composes an output path under <paramref name="baseDirectory"/> from
        /// a parser-derived name.  <see cref="EscapeInvalidFilePathNames"/>
        /// strips separators instead of enforcing containment, so the composed
        /// path is canonicalized, verified to stay below the root, and
        /// collapsed to its final segment when it does not.
        /// </summary>
        public static string GetBoundOutputPath(string baseDirectory, string fileName)
        {
            string root = Path.GetFullPath(baseDirectory);
            string candidate = Path.GetFullPath(Path.Combine(root, fileName));
            string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ||
                                root.EndsWith(Path.AltDirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;

            return candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? candidate
                : Path.Combine(root, Path.GetFileName(fileName));
        }
    }
}
