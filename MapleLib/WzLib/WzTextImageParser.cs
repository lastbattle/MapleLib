using System;
using System.Text;
using System.Collections.Generic;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;

namespace MapleLib.WzLib
{
    /// <summary>
    /// Parser for the text-encoded image blocks ("#Property") shipped in newer .ms archives.
    /// Grammar: a '#Property' banner, then "name = { ... }" tables and "name = value" scalars
    /// (integers, floats, quoted or bareword strings); indentation is insignificant.
    /// </summary>
    internal static class WzTextImageParser
    {
        public static List<WzImageProperty> Parse(string text)
        {
            var root = new List<WzImageProperty>();
            var containers = new List<WzSubProperty>();
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            void Add(WzImageProperty prop)
            {
                if (containers.Count > 0)
                    containers[^1].AddProperty(prop);
                else
                    root.Add(prop);
            }

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                if (line == "}" || line == "};" || line == "},")
                {
                    if (containers.Count > 0)
                        containers.RemoveAt(containers.Count - 1);
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string name = line[..eq].Trim().Trim('"');
                string value = line[(eq + 1)..].Trim();
                if (name.Length == 0)
                    continue;

                if (value == "{")
                {
                    var sub = new WzSubProperty(name);
                    Add(sub);
                    containers.Add(sub);
                    continue;
                }

                if (value == "}" || value == "}," || value == "};")
                {
                    if (containers.Count > 0)
                        containers.RemoveAt(containers.Count - 1);
                    continue;
                }

                if (value.Length == 0)
                    continue;

                while (value.Length > 0 && (value[^1] == ',' || value[^1] == ';'))
                    value = value[..^1].TrimEnd();

                Add(ParseScalar(name, value));
            }

            return root;
        }

        public static List<WzImageProperty> ReadFrom(WzBinaryReader reader, long blockRemaining)
        {
            long avail = reader.BaseStream.Length - reader.BaseStream.Position;
            int take = (int)Math.Min(blockRemaining, avail);
            if (take <= 0)
            return new List<WzImageProperty>();

            var bytes = new byte[take];
            reader.BaseStream.ReadExactly(bytes);
            return Parse(Encoding.UTF8.GetString(bytes));
        }

        private static WzImageProperty ParseScalar(string name, string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                return new WzStringProperty(name, value[1..^1]);

            string bare = value.Trim('"');
            if (int.TryParse(bare, out int i))
                return new WzIntProperty(name, i);

            if (long.TryParse(bare, out long l))
                return new WzLongProperty(name, l);

            if (float.TryParse(bare, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
                return new WzFloatProperty(name, f);

            return new WzStringProperty(name, bare);
        }
    }
}
