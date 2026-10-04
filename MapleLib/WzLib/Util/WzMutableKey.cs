using System;
using System.IO;
using System.Security.Cryptography;
using MapleLib.MapleCryptoLib;
using System.Linq;
using System.Runtime.InteropServices;

#nullable enable

namespace MapleLib.WzLib.Util
{
    public sealed class WzMutableKey : IEquatable<WzMutableKey>
    {
        private static readonly int BatchSize = 4096;
        private readonly byte[] _iv;
        private readonly byte[] _aesUserKey;
        private byte[]? _keys;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="WzIv"></param>
        /// <param name="AesKey">The 32-byte AES UserKey (derived from 32 DWORD)</param>
        public WzMutableKey(byte[] WzIv, byte[] AesKey)
        {
            this._iv = WzIv;
            this._aesUserKey = AesKey;
        }

        public byte[] GetKeys() => _keys?.ToArray() ?? Array.Empty<byte>();

        internal ReadOnlySpan<byte> GetKeySpan() => _keys ?? ReadOnlySpan<byte>.Empty;

        public byte this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(index));

                if (index >= MemoryLimits.MAX_WZ_STRING_BYTES)
                    throw new InvalidDataException("WZ key index exceeds the supported limit.");

                EnsureKeySize(index + 1);
                return _keys![index];
            }
        }

        public void EnsureKeySize(int size)
        {
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size));
            if (size > MemoryLimits.MAX_WZ_STRING_BYTES)
                throw new InvalidDataException("WZ key stream exceeds the supported limit.");

            if (_keys != null && _keys.Length >= size)
            {
                return;
            }

            int batchCount = (size + BatchSize - 1) / BatchSize;

            size = batchCount * BatchSize;
            byte[] newKeys = new byte[size];

            if (BitConverter.ToInt32(this._iv, 0) == 0)
            {
                this._keys = newKeys;
                return;
            }

            int startIndex = 0;
            if (_keys != null)
            {
                _keys.CopyTo(newKeys, 0);
                startIndex = _keys.Length;
            }

            this._keys = newKeys;
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Key = _aesUserKey;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;   // Ensure no padding is added

            using var encryptor = aes.CreateEncryptor();
            for (int i = startIndex; i < size; i += 16)
            {
                if (i == 0)
                {
                    for (int j = 0; j < 16; j++)
                        newKeys[j] = _iv[j % 4];
                    encryptor.TransformBlock(newKeys, 0, 16, newKeys, 0);
                }
                else
                {
                    encryptor.TransformBlock(newKeys, i - 16, 16, newKeys, i);
                }
            }

            _keys = newKeys;
        }

        public bool Equals(WzMutableKey? other) =>
            other != null && _iv.AsSpan().SequenceEqual(other._iv) && _aesUserKey.AsSpan().SequenceEqual(other._aesUserKey);

        public override bool Equals(object? obj) =>
            ReferenceEquals(this, obj) || (obj is WzMutableKey other && Equals(other));

        public override int GetHashCode() => HashCode.Combine(MemoryMarshal.Read<int>(_iv), MemoryMarshal.Read<int>(_aesUserKey));

        public static bool operator ==(WzMutableKey? left, WzMutableKey? right) =>
           ReferenceEquals(left, right) || (left is not null && left.Equals(right));

        public static bool operator !=(WzMutableKey? left, WzMutableKey? right) => !(left == right);
    }
}
