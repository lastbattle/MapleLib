using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MapleLib.WzLib.MSFile
{
    internal sealed class ChaCha20CryptoTransform : ICryptoTransform
    {
        private const int StateLength = 16;
        private readonly uint[] state = new uint[StateLength];
        private readonly byte[] keyBlock = new byte[WzMsConstants.ChaCha20BlockSize];
        private int keyBlockOffset = WzMsConstants.ChaCha20BlockSize;
        private bool disposed;

        public ChaCha20CryptoTransform(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint counter)
        {
            if (key.Length != WzMsConstants.ChaCha20KeyLength)
                throw new ArgumentException($"Key length must be {WzMsConstants.ChaCha20KeyLength}.", nameof(key));
            if (nonce.Length != WzMsConstants.ChaCha20NonceLength)
                throw new ArgumentException($"Nonce length must be {WzMsConstants.ChaCha20NonceLength}.", nameof(nonce));

            state[0] = 0x61707865;
            state[1] = 0x3320646e;
            state[2] = 0x79622d32;
            state[3] = 0x6b206574;

            for (int i = 0; i < 8; i++)
            {
                state[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
            }

            state[12] = counter;
            state[13] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(0, 4));
            state[14] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(4, 4));
            state[15] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(8, 4));
        }

        public int InputBlockSize => WzMsConstants.ChaCha20BlockSize;
        public int OutputBlockSize => WzMsConstants.ChaCha20BlockSize;
        public bool CanTransformMultipleBlocks => true;
        public bool CanReuseTransform => false;
        public uint[] State => state;

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            ArgumentNullException.ThrowIfNull(inputBuffer);
            ArgumentNullException.ThrowIfNull(outputBuffer);
            if ((uint)inputOffset > inputBuffer.Length || (uint)inputCount > inputBuffer.Length - inputOffset)
                throw new ArgumentOutOfRangeException(nameof(inputCount));
            if ((uint)outputOffset > outputBuffer.Length || (uint)inputCount > outputBuffer.Length - outputOffset)
                throw new ArgumentOutOfRangeException(nameof(outputBuffer));

            Span<byte> output = outputBuffer.AsSpan(outputOffset, inputCount);
            inputBuffer.AsSpan(inputOffset, inputCount).CopyTo(output);
            TransformInPlace(output);
            return inputCount;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            ArgumentNullException.ThrowIfNull(inputBuffer);
            if ((uint)inputOffset > inputBuffer.Length || (uint)inputCount > inputBuffer.Length - inputOffset)
                throw new ArgumentOutOfRangeException(nameof(inputCount));

            byte[] output = inputBuffer.AsSpan(inputOffset, inputCount).ToArray();
            TransformInPlace(output);
            return output;
        }

        public void TransformInPlace(Span<byte> data)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ChaCha20CryptoTransform));

            if (data.IsEmpty)
                return;

            if (keyBlockOffset < keyBlock.Length)
            {
                int count = Math.Min(data.Length, keyBlock.Length - keyBlockOffset);
                XorKeyStream(data[..count], keyBlock.AsSpan(keyBlockOffset, count));
                keyBlockOffset += count;
                data = data[count..];
            }

            while (data.Length >= keyBlock.Length)
            {
                GenerateKeyBlock();
                XorKeyStream(data[..keyBlock.Length], keyBlock);
                keyBlockOffset = keyBlock.Length;
                data = data[keyBlock.Length..];
            }

            if (!data.IsEmpty)
            {
                GenerateKeyBlock();
                XorKeyStream(data, keyBlock);
                keyBlockOffset = data.Length;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Array.Clear(state);
            Array.Clear(keyBlock);
            keyBlockOffset = keyBlock.Length;
            disposed = true;
        }

        private void GenerateKeyBlock()
        {
            uint x0 = state[0];
            uint x1 = state[1];
            uint x2 = state[2];
            uint x3 = state[3];
            uint x4 = state[4];
            uint x5 = state[5];
            uint x6 = state[6];
            uint x7 = state[7];
            uint x8 = state[8];
            uint x9 = state[9];
            uint x10 = state[10];
            uint x11 = state[11];
            uint x12 = state[12];
            uint x13 = state[13];
            uint x14 = state[14];
            uint x15 = state[15];

            for (int i = 0; i < 10; i++)
            {
                QuarterRound(ref x0, ref x4, ref x8, ref x12);
                QuarterRound(ref x1, ref x5, ref x9, ref x13);
                QuarterRound(ref x2, ref x6, ref x10, ref x14);
                QuarterRound(ref x3, ref x7, ref x11, ref x15);

                QuarterRound(ref x0, ref x5, ref x10, ref x15);
                QuarterRound(ref x1, ref x6, ref x11, ref x12);
                QuarterRound(ref x2, ref x7, ref x8, ref x13);
                QuarterRound(ref x3, ref x4, ref x9, ref x14);
            }

            Span<byte> output = keyBlock;
            BinaryPrimitives.WriteUInt32LittleEndian(output[..4], x0 + state[0]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(4, 4), x1 + state[1]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(8, 4), x2 + state[2]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(12, 4), x3 + state[3]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(16, 4), x4 + state[4]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(20, 4), x5 + state[5]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(24, 4), x6 + state[6]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(28, 4), x7 + state[7]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(32, 4), x8 + state[8]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(36, 4), x9 + state[9]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(40, 4), x10 + state[10]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(44, 4), x11 + state[11]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(48, 4), x12 + state[12]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(52, 4), x13 + state[13]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(56, 4), x14 + state[14]);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(60, 4), x15 + state[15]);

            state[12]++;
            if (state[12] == 0)
                state[13]++;

            keyBlockOffset = 0;
        }

        private static void XorKeyStream(Span<byte> data, ReadOnlySpan<byte> keyStream)
        {
            int wordBytes = data.Length & ~(sizeof(ulong) - 1);
            Span<ulong> dataWords = MemoryMarshal.Cast<byte, ulong>(data[..wordBytes]);
            ReadOnlySpan<ulong> keyWords = MemoryMarshal.Cast<byte, ulong>(keyStream[..wordBytes]);
            for (int i = 0; i < dataWords.Length; i++)
            {
                dataWords[i] ^= keyWords[i];
            }

            for (int i = wordBytes; i < data.Length; i++)
            {
                data[i] ^= keyStream[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a += b;
            d = BitOperations.RotateLeft(d ^ a, 16);

            c += d;
            b = BitOperations.RotateLeft(b ^ c, 12);

            a += b;
            d = BitOperations.RotateLeft(d ^ a, 8);

            c += d;
            b = BitOperations.RotateLeft(b ^ c, 7);
        }
    }
}
