using System;
using System.Buffers.Binary;
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

            for (int i = 0; i < data.Length; i++)
            {
                if (keyBlockOffset >= keyBlock.Length)
                    GenerateKeyBlock();

                data[i] ^= keyBlock[keyBlockOffset++];
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
            Span<uint> working = stackalloc uint[StateLength];
            state.AsSpan().CopyTo(working);

            for (int i = 0; i < 10; i++)
            {
                QuarterRound(working, 0, 4, 8, 12);
                QuarterRound(working, 1, 5, 9, 13);
                QuarterRound(working, 2, 6, 10, 14);
                QuarterRound(working, 3, 7, 11, 15);

                QuarterRound(working, 0, 5, 10, 15);
                QuarterRound(working, 1, 6, 11, 12);
                QuarterRound(working, 2, 7, 8, 13);
                QuarterRound(working, 3, 4, 9, 14);
            }

            for (int i = 0; i < StateLength; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(keyBlock.AsSpan(i * 4, 4), working[i] + state[i]);
            }

            state[12]++;
            if (state[12] == 0)
                state[13]++;

            keyBlockOffset = 0;
        }

        private static void QuarterRound(Span<uint> x, int a, int b, int c, int d)
        {
            x[a] += x[b];
            x[d] = RotateLeft(x[d] ^ x[a], 16);

            x[c] += x[d];
            x[b] = RotateLeft(x[b] ^ x[c], 12);

            x[a] += x[b];
            x[d] = RotateLeft(x[d] ^ x[a], 8);

            x[c] += x[d];
            x[b] = RotateLeft(x[b] ^ x[c], 7);
        }

        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }
    }
}
