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

    }
}
