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
using System.Linq;

namespace MapleLib.WzLib.Util
{
    public sealed class WzMutableKey : IEquatable<WzMutableKey>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="WzIv"></param>
        /// <param name="AesKey">The 32-byte AES UserKey (derived from 32 DWORD)</param>
        public WzMutableKey(byte[] WzIv, byte[] AesKey)
        {
            this.IV = WzIv;
            this.AESUserKey = AesKey;
        }

        private static readonly int BatchSize = 4096;
        private readonly byte[] IV;
        private readonly byte[] AESUserKey;

        private byte[] keys;

        public byte this[int index]
        {
            get
            {
                if (keys == null || keys.Length <= index)
                {
                    EnsureKeySize(index + 1);
                }
                return this.keys[index];
            }
        }

        public void EnsureKeySize(int size)
        {
            if (keys != null && keys.Length >= size)
            {
                return;
            }

            size = (int)Math.Ceiling(1.0 * size / BatchSize) * BatchSize;
            byte[] newKeys = new byte[size];

            if (BitConverter.ToInt32(this.IV, 0) == 0)
            {
                this.keys = newKeys;
                return;
            }

            int startIndex = 0;

            if (keys != null)
            {
                Buffer.BlockCopy(keys, 0, newKeys, 0, keys.Length);
                startIndex = keys.Length;
            }

            Rijndael aes = Rijndael.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Key = AESUserKey;
            aes.Mode = CipherMode.ECB;
            MemoryStream ms = new MemoryStream(newKeys, startIndex, newKeys.Length - startIndex, true);
            CryptoStream s = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);

            for (int i = startIndex; i < size; i += 16)
            {
                if (i == 0)
                {
                    byte[] block = new byte[16];
                    for (int j = 0; j < block.Length; j++)
                    {
                        block[j] = IV[j % 4];
                    }
                    s.Write(block, 0, block.Length);
                }
                else
                {
                    s.Write(newKeys, i - 16, 16);
                }
            }

            s.Flush();
            ms.Close();
            this.keys = newKeys;
        }

        public bool Equals(WzMutableKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return IV.SequenceEqual(other.IV) && AESUserKey.SequenceEqual(other.AESUserKey);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is WzMutableKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (IV.GetHashCode() * 397) ^ AESUserKey.GetHashCode();
            }
        }

        public static bool operator ==(WzMutableKey left, WzMutableKey right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(WzMutableKey left, WzMutableKey right)
        {
            return !(left == right);
        }
    }
}