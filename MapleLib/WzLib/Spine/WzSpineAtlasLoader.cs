/*
 * Copyright (c) 2018~2020, LastBattle https://github.com/lastbattle
 * Copyright (c) 2010~2013, haha01haha http://forum.ragezone.com/f701/release-universal-harepacker-version-892005/

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using MapleLib.WzLib.WzProperties;
using Spine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static MapleLib.WzDataReader;

namespace MapleLib.WzLib.Spine
{
    public class WzSpineAtlasLoader
    {
        /// <summary>
        /// Loads skeleton 
        /// </summary>
        /// <param name="atlasNode"></param>
        /// <param name="textureLoader"></param>
        /// <returns></returns>
        public static SkeletonData LoadSkeleton(WzStringProperty atlasNode, TextureLoader textureLoader, string skeletonPropertyName = null)
        {
            string atlasData = atlasNode.GetString();
            if (string.IsNullOrEmpty(atlasData))
            {
                return null;
            }
            atlasData = NormalizeAtlasDataForSpine21(atlasData);
            StringReader atlasReader = new StringReader(atlasData);

            Atlas atlas = new Atlas(atlasReader, string.Empty, textureLoader);
            SkeletonData skeletonData;

            if (!TryLoadSkeletonJsonOrBinary(atlasNode, atlas, skeletonPropertyName, out skeletonData))
            {
                atlas.Dispose();
                return null;
            }
            return skeletonData;
        }

        /// <summary>
        /// Load skeleton data by json or binary automatically
        /// </summary>
        /// <param name="atlasNode"></param>
        /// <param name="atlas"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private static bool TryLoadSkeletonJsonOrBinary(WzImageProperty atlasNode, Atlas atlas, string skeletonPropertyName, out SkeletonData data)
        {
            data = null;

            if (atlasNode == null || atlasNode.Parent == null || atlas == null)
            {
                return false;
            }
            WzObject parent = atlasNode.Parent;

            List<WzImageProperty> childProperties;
            if (parent is WzImageProperty)
                childProperties = ((WzImageProperty)parent).WzProperties;
            else
                childProperties = ((WzImage)parent).WzProperties;


            if (childProperties != null)
            {
                WzStringProperty stringJsonProp = (WzStringProperty) childProperties.Where(child => child.Name.EndsWith(".json")).FirstOrDefault();

                if (stringJsonProp != null) // read json based 
                {
                    StringReader skeletonReader = new StringReader(stringJsonProp.GetString());
                    SkeletonJson json = new SkeletonJson(atlas);
                    data = json.ReadSkeletonData(skeletonReader);

                    return true;
                } else
                {
                    // try read binary based
                    IEnumerable<WzImageProperty> skeletonProperties = childProperties;
                    if (!string.IsNullOrEmpty(skeletonPropertyName))
                    {
                        skeletonProperties = skeletonProperties.Where(child => child.Name.Equals(skeletonPropertyName, StringComparison.OrdinalIgnoreCase));
                    }

                    foreach (WzImageProperty property in skeletonProperties)
                    {
                        WzImageProperty linkedProperty = property.GetLinkedWzImageProperty() ?? property;
                        if (linkedProperty is WzBinaryProperty binProp)
                        {
                            data = ReadBinarySkeleton(atlas, binProp.GetBytes(false));
                            return data != null;
                        }
                        if (linkedProperty is WzRawDataProperty rawProp && property.Name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase))
                        {
                            data = ReadBinarySkeleton(atlas, rawProp.GetBytes(false));
                            return data != null;
                        }
                      
                    }
                }
            }
            return false;
        }

        private static SkeletonData ReadBinarySkeleton(Atlas atlas, byte[] skeletonBytes)
        {
            if (skeletonBytes == null || skeletonBytes.Length == 0)
            {
                return null;
            }

            using (MemoryStream ms = new MemoryStream(skeletonBytes))
            {
                try
                {
                    SkeletonBinary skeletonBinary = new SkeletonBinary(atlas);
                    return skeletonBinary.ReadSkeletonData(ms);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private static string NormalizeAtlasDataForSpine21(string atlasData)
        {
            string[] rawLines = atlasData.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool usesModernBounds = rawLines.Any(line => line.TrimStart().StartsWith("bounds:", StringComparison.OrdinalIgnoreCase));
            bool hasFormatLine = rawLines.Any(line => line.TrimStart().StartsWith("format:", StringComparison.OrdinalIgnoreCase));
            if (!usesModernBounds && hasFormatLine)
            {
                return atlasData;
            }

            StringBuilder output = new StringBuilder();
            int i = 0;
            int convertedRegions = 0;

            while (i < rawLines.Length)
            {
                string pageName = rawLines[i].Trim();
                i++;
                if (pageName.Length == 0)
                {
                    continue;
                }

                if (!pageName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    output.AppendLine(pageName);
                    continue;
                }

                string sizeLine = i < rawLines.Length ? rawLines[i++].Trim() : "size:0,0";
                string filterLine = i < rawLines.Length ? rawLines[i++].Trim() : "filter:Linear,Linear";
                if (i < rawLines.Length && rawLines[i].TrimStart().StartsWith("pma:", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                }

                output.AppendLine(pageName);
                output.AppendLine(sizeLine);
                output.AppendLine("format: RGBA8888");
                output.AppendLine(filterLine);
                output.AppendLine("repeat: none");

                while (i < rawLines.Length)
                {
                    string regionName = rawLines[i].Trim();
                    if (regionName.Length == 0)
                    {
                        output.AppendLine();
                        i++;
                        break;
                    }
                    if (regionName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    i++;
                    string boundsLine = i < rawLines.Length ? rawLines[i++].Trim() : null;
                    if (boundsLine == null || !boundsLine.StartsWith("bounds:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] bounds = boundsLine.Substring(boundsLine.IndexOf(':') + 1).Split(',');
                    if (bounds.Length < 4)
                    {
                        continue;
                    }

                    bool rotate = false;
                    if (i < rawLines.Length && rawLines[i].TrimStart().StartsWith("rotate:", StringComparison.OrdinalIgnoreCase))
                    {
                        string rotateValue = rawLines[i].Substring(rawLines[i].IndexOf(':') + 1).Trim();
                        rotate = rotateValue.Equals("true", StringComparison.OrdinalIgnoreCase) || rotateValue == "90";
                        i++;
                    }

                    string x = bounds[0].Trim();
                    string y = bounds[1].Trim();
                    string width = bounds[2].Trim();
                    string height = bounds[3].Trim();

                    output.AppendLine(regionName);
                    output.AppendLine($"  rotate: {rotate.ToString().ToLowerInvariant()}");
                    output.AppendLine($"  xy: {x}, {y}");
                    output.AppendLine($"  size: {width}, {height}");
                    output.AppendLine($"  orig: {width}, {height}");
                    output.AppendLine("  offset: 0, 0");
                    output.AppendLine("  index: -1");
                    convertedRegions++;
                }
            }

            return output.ToString();
        }
    }
}
