using System;
using System.Buffers.Binary;

namespace MapleLib.Helpers
{
    /// <summary>
    /// Self-contained BC7/BPTC decoder. The decoding algorithm and partition data are adapted
    /// from bcdec (https://github.com/iOrange/bcdec), licensed under the MIT license.
    /// </summary>
    internal static class Bc7Decoder
    {
        private static readonly int[][] Weights =
        {
            null,
            null,
            new[] { 0, 21, 43, 64 },
            new[] { 0, 9, 18, 27, 37, 46, 55, 64 },
            new[] { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 }
        };

        private static readonly byte[,] ComponentBits =
        {
            { 4, 6, 5, 7, 5, 7, 7, 5 },
            { 0, 0, 0, 0, 6, 8, 7, 5 }
        };

        private const byte ModesWithPBits = 0b11001011;

        // Two-subset table followed by the three-subset table. The high bit marks a fix-up index.
        private static readonly byte[] PartitionSets = Convert.FromBase64String(
            "gAABAQAAAQEAAAEBAAABgYAAAAEAAAABAAAAAQAAAIGAAQEBAAEBAQABAQEAAQGBgAAAAQAAAQEAAAEBAAEBgYAAAAAAAAABAAAAAQAAAYGAAAEBAAEBAQABAQEBAQGBgAAAAQAAAQEAAQEBAQEBgYAAAAAAAAABAAABAQABAYGAAAAAAAAAAAAAAAEAAAGBgAABAQABAQEBAQEBAQEBgYAAAAAAAAABAAEBAQEBAYGAAAAAAAAAAAAAAAEAAQGBgAAAAQABAQEBAQEBAQEBgYAAAAAAAAAAAQEBAQEBAYGAAAAAAQEBAQEBAQEBAQGBgAAAAAAAAAAAAAAAAQEBgYAAAAABAAAAAQEBAAEBAYGAAYEBAAAAAQAAAAAAAAAAgAAAAAAAAACBAAAAAQEBAIABgQEAAAEBAAAAAQAAAACAAIEBAAAAAQAAAAAAAAAAgAAAAAEAAACBAQAAAQEBAIAAAAAAAAAAgQAAAAEBAACAAQEBAAABAQAAAQEAAACBgACBAQAAAAEAAAABAAAAAIAAAAABAAAAgQAAAAEBAACAAYEAAAEBAAABAQAAAQEAgACBAQABAQAAAQEAAQEAAIAAAAEAAQEBgQEBAAEAAACAAAAAAQEBAYEBAQEAAAAAgAGBAQAAAAEBAAAAAQEBAIAAgQEBAAABAQAAAQEBAACAAQABAAEAAQABAAEAAQCBgAAAAAEBAQEAAAAAAQEBgYABAAEBAIEAAAEAAQEAAQCAAAEBAAABAYEBAAABAQAAgACBAQEBAAAAAAEBAQEAAIABAAEAAQABgQABAAEAAQCAAQEAAQAAAQABAQABAACBgAEAAQEAAQABAAEAAAEAgYABgQEAAAEBAQEAAAEBAQCAAAABAAABAYEBAAABAAAAgACBAQAAAQAAAQAAAQEAAIAAgQEBAAEBAQEAAQEBAACAAYEAAQAAAQEAAAEAAQEAgAABAQEBAAABAQAAAAABgYABAQAAAQEAAQAAAQEAAIGAAAAAAAGBAAABAQAAAAAAgAEAAAEBgQAAAQAAAAAAAIAAgQAAAQEBAAABAAAAAACAAAAAAACBAAABAQEAAAEAgAAAAAABAACBAQEAAAEAAIABAQABAQAAAQAAAQAAAYGAAAEBAAEBAAEBAAABAACBgAGBAAAAAQEBAAABAQEAAIAAgQEBAAABAQEAAAABAQCAAQEAAQEAAAEBAAABAACBgAEBAAAAAQEAAAEBAQAAgYABAQEBAQEAAQAAAAAAAIGAAAABAQAAAAEBAQAAAQGBgAAAAAEBAQEAAAEBAAABgYAAgQEAAAEBAQEBAQAAAACAAIEAAAABAAEBAQABAQEAgAEAAAABAAAAAQEBAAEBgYAAAYEAAAEBAAICAQICAoKAAACBAAABAYICAQECAgIBgAAAAAIAAAGCAgEBAgIBgYACAoIAAAICAAABAQABAYGAAAAAAAAAAIEBAgIBAQKCgAABgQAAAQEAAAICAAACgoAAAoIAAAICAQEBAQEBAYGAAAEBAAABAYICAQECAgGBgAAAAAAAAACBAQEBAgICgoAAAAABAQEBgQEBAQICAoKAAAAAAQGBAQICAgICAgKCgAABAgAAgQIAAAECAAABgoABAQIAAYECAAEBAgABAYKAAQICAIECAgABAgIAAQKCgAABgQABAQIBAQICAQICgoAAAYECAAABggIAAAICAgCAAACBAAABAQABAQIBAQKCgAEBgQAAAQGCAAABAgIAAIAAAAABAQICgQECAgEBAoKAAAKCAAACAgAAAgIBAQGBgAEBgQABAQEAAgICAAICgoAAAIEAAAABggICAQICAgGAAAAAAACBAQABAgIAAQKCgAAAAAEBAACCAoEAAgIBAIABAoIAgQICAAABAQAAAACAAAECAAABAoEBAgICAgKCgAEBAAECggGBAgIBAAEBAIAAAAAAAYEAAQKCAQECAgGAAAICAQEAAoEBAAIAAAKCgAEBAACBAQACAAACAgICgoAAAQEAAQICAAGCAgAAAYGAAAAAAgAAAIICAQECAgKBgAAAAAAAAAKBAQICAQICgoACAoIAAAICAAABAgAAAYGAAAGBAAABAgAAAgIAAgKCgAECAACBAgAAAYIAAAECAIAAAAABAYEBAgKCAgAAAACAAQIAAQIAAYIAgQIAAQIAgAECAAIAAQKBggABAAECAIAAAQECAgAAAQGCAgAAAYGAAAEBAQGCAgICAAAAAAGBgAEAgQABAAECAgICAgICgoAAAAAAAAAAggECAQIBAoGAAAICAYECAgAAAgIBAQKCgAACggAAAQEAAAICAAABgYACAgABAoIBAAICAAECAoGAAQABAgKCAgICAgIAAQCBgAAAAAIBAgGCAQIBAgECgYABAIEAAQABAAEAAQICAoKAAgKCAAEBAQACAgIAAQGBgAAAAgGBAQIAAAACAQEBgoAAAAACgQECAgEBAgIBAYKAAgICAIEBAQABAQEAAgKCgAAAAgEBAQKBAQECAAAAgoABAQAAgQEAAAEBAAICAoKAAAAAAAAAAAIBgQICAQGCgAEBAACBAQACAgICAgICgoAAAgIAAAEBAACBAQAAAoKAAAICAQECAoEBAgIAAAKCgAAAAAAAAAAAAAAAAoEBgoAAAIIAAAABAAAAAgAAAIGAAgICAQICAgACAgKBAgKCgAEAgQICAgICAgICAgICgoABAYECAAEBggIAAQICAgA=");

        public static byte[] DecodeToBgra32(byte[] source, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int requiredLength = blockCountX * blockCountY * 16;
            if (source.Length < requiredLength)
                throw new ArgumentException("BC7 source data is shorter than the dimensions require.", nameof(source));

            byte[] output = new byte[width * height * 4];
            Span<byte> block = stackalloc byte[64];
            int sourceOffset = 0;

            for (int blockY = 0; blockY < blockCountY; blockY++)
            {
                for (int blockX = 0; blockX < blockCountX; blockX++)
                {
                    DecodeBlock(source.AsSpan(sourceOffset, 16), block);
                    sourceOffset += 16;

                    int copyWidth = Math.Min(4, width - blockX * 4);
                    int copyHeight = Math.Min(4, height - blockY * 4);
                    for (int y = 0; y < copyHeight; y++)
                    {
                        int destinationOffset = ((blockY * 4 + y) * width + blockX * 4) * 4;
                        block.Slice(y * 16, copyWidth * 4).CopyTo(output.AsSpan(destinationOffset));
                    }
                }
            }

            return output;
        }

        public static unsafe void DecodeToBgra32(byte[] source, int width, int height, IntPtr destination, int stride)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int requiredLength = blockCountX * blockCountY * 16;
            if (source.Length < requiredLength)
                throw new ArgumentException("BC7 source data is shorter than the dimensions require.", nameof(source));

            Span<byte> block = stackalloc byte[64];
            int sourceOffset = 0;
            byte* destinationBase = (byte*)destination;

            for (int blockY = 0; blockY < blockCountY; blockY++)
            {
                for (int blockX = 0; blockX < blockCountX; blockX++)
                {
                    DecodeBlock(source.AsSpan(sourceOffset, 16), block);
                    sourceOffset += 16;

                    int copyWidth = Math.Min(4, width - blockX * 4);
                    int copyHeight = Math.Min(4, height - blockY * 4);
                    for (int y = 0; y < copyHeight; y++)
                    {
                        byte* destinationRow = destinationBase + (blockY * 4 + y) * stride + blockX * 16;
                        block.Slice(y * 16, copyWidth * 4).CopyTo(new Span<byte>(destinationRow, copyWidth * 4));
                    }
                }
            }
        }

        private static void DecodeBlock(ReadOnlySpan<byte> source, Span<byte> output)
        {
            var bits = new BitStream(source);
            int mode = 0;
            while (mode < 8 && bits.Read(1) == 0)
                mode++;

            if (mode >= 8)
            {
                output.Clear();
                return;
            }

            int partition = 0;
            int subsetCount = 1;
            int rotation = 0;
            int indexSelection = 0;

            if (mode is 0 or 1 or 2 or 3 or 7)
            {
                subsetCount = mode is 0 or 2 ? 3 : 2;
                partition = bits.Read(mode == 0 ? 4 : 6);
            }

            if (mode is 4 or 5)
            {
                rotation = bits.Read(2);
                if (mode == 4)
                    indexSelection = bits.Read(1);
            }

            int endpointCount = subsetCount * 2;
            Span<int> endpoints = stackalloc int[24];
            int colorBits = ComponentBits[0, mode];
            int alphaBits = ComponentBits[1, mode];

            for (int component = 0; component < 3; component++)
            {
                for (int endpoint = 0; endpoint < endpointCount; endpoint++)
                    endpoints[endpoint * 4 + component] = bits.Read(colorBits);
            }

            if (alphaBits > 0)
            {
                for (int endpoint = 0; endpoint < endpointCount; endpoint++)
                    endpoints[endpoint * 4 + 3] = bits.Read(alphaBits);
            }

            bool hasPBits = (ModesWithPBits & (1 << mode)) != 0;
            if (hasPBits)
            {
                for (int endpoint = 0; endpoint < endpointCount; endpoint++)
                {
                    for (int component = 0; component < 4; component++)
                        endpoints[endpoint * 4 + component] <<= 1;
                }

                if (mode == 1)
                {
                    int p0 = bits.Read(1);
                    int p1 = bits.Read(1);
                    for (int component = 0; component < 3; component++)
                    {
                        endpoints[component] |= p0;
                        endpoints[4 + component] |= p0;
                        endpoints[8 + component] |= p1;
                        endpoints[12 + component] |= p1;
                    }
                }
                else
                {
                    for (int endpoint = 0; endpoint < endpointCount; endpoint++)
                    {
                        int p = bits.Read(1);
                        for (int component = 0; component < 4; component++)
                            endpoints[endpoint * 4 + component] |= p;
                    }
                }
            }

            int decodedColorBits = colorBits + (hasPBits ? 1 : 0);
            int decodedAlphaBits = alphaBits + (hasPBits ? 1 : 0);
            for (int endpoint = 0; endpoint < endpointCount; endpoint++)
            {
                for (int component = 0; component < 3; component++)
                    endpoints[endpoint * 4 + component] = ExpandEndpoint(endpoints[endpoint * 4 + component], decodedColorBits);

                if (alphaBits > 0)
                    endpoints[endpoint * 4 + 3] = ExpandEndpoint(endpoints[endpoint * 4 + 3], decodedAlphaBits);
                else
                    endpoints[endpoint * 4 + 3] = 255;
            }

            int primaryIndexBits = mode is 0 or 1 ? 3 : mode == 6 ? 4 : 2;
            int secondaryIndexBits = mode == 4 ? 3 : mode == 5 ? 2 : 0;
            Span<byte> primaryIndices = stackalloc byte[16];

            for (int texel = 0; texel < 16; texel++)
            {
                byte partitionSet = GetPartitionSet(subsetCount, partition, texel);
                int count = primaryIndexBits - ((partitionSet & 0x80) != 0 ? 1 : 0);
                primaryIndices[texel] = (byte)bits.Read(count);
            }

            for (int texel = 0; texel < 16; texel++)
            {
                int subset = GetPartitionSet(subsetCount, partition, texel) & 0x03;
                int primaryIndex = primaryIndices[texel];
                int secondaryIndex = secondaryIndexBits == 0
                    ? 0
                    : bits.Read(secondaryIndexBits - (texel == 0 ? 1 : 0));

                int colorIndex = secondaryIndexBits > 0 && indexSelection != 0 ? secondaryIndex : primaryIndex;
                int alphaIndex = secondaryIndexBits > 0 && indexSelection == 0 ? secondaryIndex : primaryIndex;
                int colorWeightBits = secondaryIndexBits > 0 && indexSelection != 0 ? secondaryIndexBits : primaryIndexBits;
                int alphaWeightBits = secondaryIndexBits > 0 && indexSelection == 0 ? secondaryIndexBits : primaryIndexBits;
                int endpoint0 = subset * 8;
                int endpoint1 = endpoint0 + 4;

                int r = Interpolate(endpoints[endpoint0], endpoints[endpoint1], colorIndex, colorWeightBits);
                int g = Interpolate(endpoints[endpoint0 + 1], endpoints[endpoint1 + 1], colorIndex, colorWeightBits);
                int b = Interpolate(endpoints[endpoint0 + 2], endpoints[endpoint1 + 2], colorIndex, colorWeightBits);
                int a = Interpolate(endpoints[endpoint0 + 3], endpoints[endpoint1 + 3], alphaIndex, alphaWeightBits);

                switch (rotation)
                {
                    case 1: (a, r) = (r, a); break;
                    case 2: (a, g) = (g, a); break;
                    case 3: (a, b) = (b, a); break;
                }

                int outputOffset = texel * 4;
                output[outputOffset] = (byte)b;
                output[outputOffset + 1] = (byte)g;
                output[outputOffset + 2] = (byte)r;
                output[outputOffset + 3] = (byte)a;
            }
        }

        private static int ExpandEndpoint(int value, int bitCount)
        {
            value <<= 8 - bitCount;
            return value | (value >> bitCount);
        }

        private static int Interpolate(int endpoint0, int endpoint1, int index, int precision)
        {
            int weight = Weights[precision][index];
            return (endpoint0 * (64 - weight) + endpoint1 * weight + 32) >> 6;
        }

        private static byte GetPartitionSet(int subsetCount, int partition, int texel)
        {
            if (subsetCount == 1)
                return texel == 0 ? (byte)0x80 : (byte)0;

            int tableOffset = (subsetCount - 2) * 1024;
            return PartitionSets[tableOffset + partition * 16 + texel];
        }

        private ref struct BitStream
        {
            private ulong low;
            private ulong high;

            public BitStream(ReadOnlySpan<byte> source)
            {
                low = BinaryPrimitives.ReadUInt64LittleEndian(source);
                high = BinaryPrimitives.ReadUInt64LittleEndian(source[8..]);
            }

            public int Read(int count)
            {
                ulong mask = (1UL << count) - 1;
                int value = (int)(low & mask);
                low = (low >> count) | ((high & mask) << (64 - count));
                high >>= count;
                return value;
            }
        }
    }
}
