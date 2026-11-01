using System.IO;
using System.IO.Compression;
using System.Drawing;
using MapleLib.WzLib.WzProperties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest_WzFile
{
    [TestClass]
    public class WzPngUploadContractTests
    {
        private static byte[] BuildBlockPayload(int width, int height)
        {
            int blockCountX = (width + 15) / 16;
            int blockCountY = (height + 15) / 16;
            byte[] payload = new byte[blockCountX * blockCountY * 2];
            for (int block = 0; block < payload.Length / 2; block++)
            {
                payload[block * 2] = (byte)(block + 1);
                payload[block * 2 + 1] = (byte)(255 - block);
            }

            return payload;
        }

        private static WzPngProperty BuildCompressedCanvas(int width, int height, byte[] blockPayload)
        {
            using MemoryStream ms = new();
            ms.WriteByte(0x78);
            ms.WriteByte(0x9C);
            using (DeflateStream deflate = new(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(blockPayload, 0, blockPayload.Length);
            }

            WzPngProperty png = new();
            png.SetCompressedBytes(ms.ToArray(), width, height, WzPngFormat.Format517);
            return png;
        }

        [TestMethod]
        public void ExpandPixelDataForm517_MatchesSurfaceSize()
        {
            byte[] expanded = WzPngProperty.ExpandPixelDataForm517ForUpload(BuildBlockPayload(16, 16), 16, 16);

            Assert.AreEqual(16 * 16 * 2, expanded.Length);
        }

        [TestMethod]
        public void ExpandPixelDataForm517_MapsEveryPixelToItsBlock()
        {
            byte[] expanded = WzPngProperty.ExpandPixelDataForm517ForUpload(BuildBlockPayload(18, 20), 18, 20);

            Assert.AreEqual(18 * 20 * 2, expanded.Length);
            int blockCountX = (18 + 15) / 16;
            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 18; x++)
                {
                    int block = (y / 16) * blockCountX + (x / 16);
                    int index = (y * 18 + x) * 2;

                    Assert.AreEqual((byte)(block + 1), expanded[index], $"lo mismatch at {x},{y}");
                    Assert.AreEqual((byte)(255 - block), expanded[index + 1], $"hi mismatch at {x},{y}");
                }
            }
        }

        [TestMethod]
        public void ExpandPixelDataForm517_TruncatedPayloadThrowsInvalidData()
        {
            bool threw = false;
            try
            {
                WzPngProperty.ExpandPixelDataForm517ForUpload(new byte[] { 0x01 }, 16, 16);
            }
            catch (InvalidDataException)
            {
                threw = true;
            }

            Assert.IsTrue(threw);
        }

        [TestMethod]
        public void GetBitmap_FiveSeventeenCanvasDecodesThroughCpuPath()
        {
            using WzPngProperty png = BuildCompressedCanvas(16, 16, BuildBlockPayload(16, 16));

            using var bmp = png.GetBitmap();

            Assert.IsNotNull(bmp);
            Assert.AreEqual(16, bmp.Width);
            Assert.AreEqual(16, bmp.Height);
        }
    }
}
