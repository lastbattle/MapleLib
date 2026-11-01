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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MapleLib.WzLib.Spine
{
    public class WzSpineTextureLoader : TextureLoader, System.IDisposable
    {
        public WzObject ParentNode { get; private set; }
        private readonly GraphicsDevice graphicsDevice;
        private readonly HashSet<Texture2D> ownedTextures = new();

        /// <summary>
        /// Constructor for the spine texture loader
        /// </summary>
        /// <param name="ParentNode"></param>
        /// <param name="graphicsDevice"></param>
        public WzSpineTextureLoader(WzObject ParentNode, GraphicsDevice graphicsDevice)
        {
            this.ParentNode = ParentNode;
            this.graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Loads spine texture from the specified WZ path
        /// </summary>
        /// <param name="page"></param>
        /// <param name="path"></param>
        public void Load(AtlasPage page, string path)
        {
            WzObject frameNode = this.ParentNode[path];
            if (frameNode == null)
                return;

            WzCanvasProperty canvasProperty = null;

            WzImageProperty imageChild = ParentNode[path] as WzImageProperty;
            if (imageChild is WzUOLProperty uolProperty)
            {
                WzObject uolLink = uolProperty.LinkValue;

                if (uolLink is WzCanvasProperty uolPropertyLink)
                {
                    canvasProperty = uolPropertyLink;
                }
                else
                {
                    // other unimplemented prop?
                }
            }
            else if (imageChild is WzCanvasProperty property)
            {
                canvasProperty = property;
            }

            // GetLinkedWzImageProperty returns whichever property the hostile
            // link path hits and a parsed canvas always owns its png slot, but a
            // hand built canvas carries neither, so the leg degrades to a skip.
            if (canvasProperty != null)
            {
                WzCanvasProperty linkImgProperty = canvasProperty.GetLinkedWzImageProperty() as WzCanvasProperty;
                WzPngProperty pngProperty = linkImgProperty?.PngProperty;

                if (pngProperty != null && graphicsDevice != null)
                {
                    Texture2D tex = new Texture2D(graphicsDevice, pngProperty.Width, pngProperty.Height, false, WzPngFormatExtensions.GetXNASurfaceFormat(pngProperty.Format));
                    ownedTextures.Add(tex);

                    pngProperty.ParsePng(true, tex);

                    page.rendererObject = tex;
                    page.width = pngProperty.Width;
                    page.height = pngProperty.Height;
                }
            }
        }

        /// <summary>
        /// Unload texture
        /// </summary>
        /// <param name="texture"></param>
        public void Unload(object texture)
        {
            (texture as Texture2D)?.Dispose();
            if (texture is Texture2D owned) ownedTextures.Remove(owned);
        }

        public void Dispose()
        {
            foreach (Texture2D texture in ownedTextures) texture.Dispose();
            ownedTextures.Clear();
        }
    }
}
