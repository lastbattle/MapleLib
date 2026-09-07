using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;

namespace MapleCrypto.Benchmarks;

/// <summary>
/// Measures WZ pixel encoders with pixels derived from a real extracted canvas.
/// Set WZ_PIXEL_IMG to a BMS/plain IMG containing at least one canvas.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WzPixelEncodeBenchmarks
{
    private WzImage _image = null!;
    private Bitmap _fixture = null!;

    [Params(128, 1024)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string sourcePath = Environment.GetEnvironmentVariable("WZ_PIXEL_IMG")
            ?? throw new InvalidOperationException("Set WZ_PIXEL_IMG to a BMS/plain IMG containing a canvas.");
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The WZ pixel corpus IMG does not exist.", sourcePath);

        WzImgDeserializer deserializer = new(freeResources: true, calculateChecksum: false);
        _image = deserializer.WzImageFromIMGFile(
            sourcePath,
            WzTool.GetIvByMapleVersion(WzMapleVersion.BMS),
            Path.GetFileName(sourcePath),
            out bool successfullyParsedImage);
        if (!successfullyParsedImage)
            throw new InvalidDataException($"Could not parse pixel corpus IMG '{sourcePath}'.");

        string? canvasPath = Environment.GetEnvironmentVariable("WZ_PIXEL_CANVAS_PATH");
        WzCanvasProperty sourceCanvas = string.IsNullOrWhiteSpace(canvasPath)
            ? FindFirstCanvas(_image)
                ?? throw new InvalidDataException($"Pixel corpus IMG '{sourcePath}' contains no canvas.")
            : _image.GetFromPath(canvasPath) as WzCanvasProperty
                ?? throw new InvalidDataException($"Pixel corpus path '{canvasPath}' is not a canvas.");
        Bitmap decodedBitmap = sourceCanvas.GetBitmap()
            ?? throw new InvalidDataException($"Could not decode canvas '{sourceCanvas.FullPath}'.");
        using Bitmap sourceBitmap = (Bitmap)decodedBitmap.Clone();
        _fixture = TileBitmap(sourceBitmap, Size);

        Console.WriteLine(
            $"pixel-source file={sourcePath} sha256={Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(sourcePath)))} canvas={sourceCanvas.FullPath} dimensions={sourceBitmap.Width}x{sourceBitmap.Height}");
        PrintEdgeCaseDigests();
        PrintDigest("bgra4444", Encode(SurfaceFormat.Bgra4444), Size == 128
            ? "9babdd858124ced69aa3e59d618aff7f065abadcc7c7d7a84b6401a8fb1b3003"
            : "3f2d01cfa70a01a5ae9bd3c8d5a236a81baf651a720147d9c817c1c16bae4b08");
        PrintDigest("dxt3", Encode(SurfaceFormat.Dxt3), Size == 128
            ? "6c77ea4f2cbc8ce42400af72e3bf9951f288cfdf62df08e4d756d2ec85c7a509"
            : "c59c0ba1c32ddaac0ee0f24a384cb6b3566fb10be1f599e9207b254a8f3fbaa5");
        PrintDigest("dxt5", Encode(SurfaceFormat.Dxt5), Size == 128
            ? "c9c5def300df36314ebb1096cf796d557e5fb199b449e5828b638a18893ae7c4"
            : "d18b20f3de31491777b96fae4cbbeb35ff0951d9dccdb5144069eb9d24802595");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _fixture?.Dispose();
        _image?.Dispose();
    }

    [Benchmark]
    public byte[] Bgra4444Encode() => Encode(SurfaceFormat.Bgra4444);

    [Benchmark]
    public byte[] Dxt3Encode() => Encode(SurfaceFormat.Dxt3);

    [Benchmark]
    public byte[] Dxt5Encode() => Encode(SurfaceFormat.Dxt5);

    private byte[] Encode(SurfaceFormat format)
    {
        (_, byte[] bytes) = PngUtility.CompressImageToPngFormat(
            _fixture, format, isGrayscale: false);
        return bytes;
    }

    private static WzCanvasProperty? FindFirstCanvas(WzImage image)
    {
        Stack<WzImageProperty> pending = new();
        for (int index = image.WzProperties.Count - 1; index >= 0; index--)
            pending.Push(image.WzProperties[index]);
        while (pending.TryPop(out WzImageProperty? property))
        {
            if (property is WzCanvasProperty canvas)
                return canvas;

            WzPropertyCollection? children = property.WzProperties;
            if (children is null)
                continue;

            for (int index = children.Count - 1; index >= 0; index--)
                pending.Push(children[index]);
        }

        return null;
    }

    private static Bitmap TileBitmap(Bitmap source, int size)
    {
        Bitmap fixture = new(size, size, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(fixture);
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        for (int y = 0; y < size; y += source.Height)
        {
            for (int x = 0; x < size; x += source.Width)
            {
                graphics.DrawImageUnscaled(source, x, y);
            }
        }

        return fixture;
    }

    private void PrintDigest(string format, byte[] bytes, string expectedDigest)
    {
        string digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!StringComparer.Ordinal.Equals(digest, expectedDigest))
            throw new InvalidDataException($"Pixel output changed for size {Size}, format {format}: {digest}.");
        Console.WriteLine(
            $"pixel-baseline size={Size} format={format} bytes={bytes.Length} sha256={digest}");
    }

    private static void PrintEdgeCaseDigests()
    {
        foreach ((string name, Bitmap bitmap) in CreateEdgeCases())
        {
            using (bitmap)
            {
                foreach (SurfaceFormat format in new[]
                         {
                             SurfaceFormat.Bgra4444,
                             SurfaceFormat.Dxt3,
                             SurfaceFormat.Dxt5
                         })
                {
                    (_, byte[] bytes) = PngUtility.CompressImageToPngFormat(
                        bitmap, format, isGrayscale: false);
                    string digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
                    string expectedDigest = (name, format) switch
                    {
                        ("single-alpha-equal", SurfaceFormat.Bgra4444) => "595e9e4de1f21e3e5bc97747d8d1d6c8430fc5a661134441140f5f4465593774",
                        ("single-alpha-equal", SurfaceFormat.Dxt3) => "96f0e08163b58919db55171ef8bccb58c62eed30f89b21276a18c7a51966a9d3",
                        ("single-alpha-equal", SurfaceFormat.Dxt5) => "101c54a0f7a92eddc1b6384ee1c882ab78981876e7c2b5d23ab8ec8f7e5d6391",
                        ("nearest-ties", SurfaceFormat.Bgra4444) => "6b95ae0a509ee3e86a64e2927d6b6925f59cd78924b3a3c37304eeae6e6273ea",
                        ("nearest-ties", SurfaceFormat.Dxt3) => "e01451d4339a8eb39e7dfea4e43a6e98c486acb14ead8335928013c2b97a3d60",
                        ("nearest-ties", SurfaceFormat.Dxt5) => "0b6c485adc4447c9544dd518fd54c076072eda56aa1c70334da925e4c518dd45",
                        ("gradient", SurfaceFormat.Bgra4444) => "40b109239a762e816ae6a5b5979580193515a40568f63f4626ae1aa9be890ad4",
                        ("gradient", SurfaceFormat.Dxt3) => "737b14e8844be8ddec28bae0b04304281ea21675fe04af6fa8a9fadca2837111",
                        ("gradient", SurfaceFormat.Dxt5) => "1dc0ecde43462107a7a09330c6382096f1aa8a587b76b89d9d66d7d22b526381",
                        ("deterministic-random", SurfaceFormat.Bgra4444) => "906da69b7e115b6423d89913a5ca32c5ec719c8f38ed83ed782cb311bec968d1",
                        ("deterministic-random", SurfaceFormat.Dxt3) => "2c3d6f24f6cde46686dd6f6586c6a6dd6181896e528df8a904360be6bf5f7bb4",
                        ("deterministic-random", SurfaceFormat.Dxt5) => "067b6881e17f9cf9f9112b437abea8ca8813aa3834c8188f2b69ff09db24f54d",
                        _ => throw new InvalidOperationException($"Missing golden digest for {name}/{format}.")
                    };
                    if (!StringComparer.Ordinal.Equals(digest, expectedDigest))
                        throw new InvalidDataException($"Pixel edge output changed for {name}/{format}: {digest}.");
                    Console.WriteLine(
                        $"pixel-correctness case={name} format={format} bytes={bytes.Length} sha256={digest}");
                }
            }
        }
    }

    private static IEnumerable<(string Name, Bitmap Bitmap)> CreateEdgeCases()
    {
        Bitmap single = new(4, 4, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(single))
            graphics.Clear(Color.FromArgb(127, 80, 80, 80));
        yield return ("single-alpha-equal", single);

        Bitmap ties = new(4, 4, PixelFormat.Format32bppArgb);
        Color[] tiePalette =
        [
            Color.FromArgb(0, 0, 0, 0),
            Color.FromArgb(255, 255, 255, 255),
            Color.FromArgb(128, 127, 127, 127),
            Color.FromArgb(128, 128, 128, 128)
        ];
        for (int y = 0; y < ties.Height; y++)
        for (int x = 0; x < ties.Width; x++)
            ties.SetPixel(x, y, tiePalette[(y * ties.Width + x) & 3]);
        yield return ("nearest-ties", ties);

        Bitmap gradient = new(8, 8, PixelFormat.Format32bppArgb);
        for (int y = 0; y < gradient.Height; y++)
        for (int x = 0; x < gradient.Width; x++)
            gradient.SetPixel(x, y, Color.FromArgb((x * 37 + y * 19) & 255,
                x * 255 / 7, y * 255 / 7, (x + y) * 255 / 14));
        yield return ("gradient", gradient);

        Bitmap random = new(8, 8, PixelFormat.Format32bppArgb);
        uint state = 0xC001D00Du;
        for (int y = 0; y < random.Height; y++)
        for (int x = 0; x < random.Width; x++)
        {
            state = state * 1_664_525u + 1_013_904_223u;
            random.SetPixel(x, y, Color.FromArgb((int)state));
        }
        yield return ("deterministic-random", random);
    }
}
