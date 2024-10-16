/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2015 haha01haha01 and contributors
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.IO;
using System.Security.Cryptography;
using MapleLib.MapleCryptoLib;
using Newtonsoft.Json.Linq;
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

        public byte this[int index]
        {
            get
            {
                EnsureKeySize(index + 1);
                return _keys![index];
            }
        }

        public void EnsureKeySize(int size)
        {
            if (_keys != null && _keys.Length >= size)
            {
                return;
            }

            size = (int)Math.Ceiling(1.0 * size / BatchSize) * BatchSize;
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
            using var ms = new MemoryStream(newKeys, startIndex, newKeys.Length - startIndex, true);
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);

            Span<byte> block = stackalloc byte[16];
            for (int i = startIndex; i < size; i += 16)
            {
                if (i == 0)
                {
                    for (int j = 0; j < block.Length; j++)
                        block[j] = _iv[j % 4];
                    cs.Write(block);
                }
                else
                {
                    cs.Write(newKeys.AsSpan(i - 16, 16));
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