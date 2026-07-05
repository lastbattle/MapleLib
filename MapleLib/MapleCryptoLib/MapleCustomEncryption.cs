/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
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

using System.Runtime.CompilerServices;

namespace MapleLib.MapleCryptoLib
{
	/// <summary>
	/// Class to handle the MapleStory Custom Encryption routines
	/// </summary>
	public class MapleCustomEncryption
	{
		private static readonly byte[] RotateLeftTable = CreateRotateTable(left: true);
		private static readonly byte[] RotateRightTable = CreateRotateTable(left: false);

		/// <summary>
		/// Encrypt data using MapleStory's Custom Encryption
		/// </summary>
		/// <param name="data">data to encrypt</param>
		/// <returns>Encrypted data</returns>
		public static void Encrypt(byte[] data)
		{
			int size = data.Length;
			byte a, c;
			for (int i = 0; i < 3; i++)
			{
				a = 0;
				for (int j = size, index = 0; j > 0; j--, index++)
				{
					c = data[index];
					c = RotateLeft3(c);
					c = (byte)(c + j);
					c ^= a;
					a = c;
					c = ror(a, j);
					c ^= 0xFF;
					c += 0x48;
					data[index] = c;
				}
				a = 0;
				for (int j = size; j > 0; j--)
				{
					c = data[j - 1];
					c = Rotate4(c);
					c = (byte)(c + j);
					c ^= a;
					a = c;
					c ^= 0x13;
					c = RotateRight3(c);
					data[j - 1] = c;
				}
			}
		}

		/// <summary>
		/// Decrypt data using MapleStory's Custom Encryption
		/// </summary>
		/// <param name="data">data to decrypt</param>
		/// <returns>Decrypted data</returns>
		public static void Decrypt(byte[] data)
		{
			int size = data.Length;
			byte a, b, c;
			for (int i = 0; i < 3; i++)
			{
				a = 0;
				b = 0;
				for (int j = size; j > 0; j--)
				{
					c = data[j - 1];
					c = RotateLeft3Lookup(c);
					c ^= 0x13;
					a = c;
					c ^= b;
					c = (byte)(c - j); // Guess this is supposed to be right?
					c = Rotate4Lookup(c);
					b = a;
					data[j - 1] = c;
				}
				a = 0;
				b = 0;
				for (int j = size, index = 0; j > 0; j--, index++)
				{
					c = data[index];
					c -= 0x48;
					c ^= 0xFF;
					c = rol(c, j);
					a = c;
					c ^= b;
					c = (byte)(c - j); // Guess this is supposed to be right?
					c = RotateRight3Lookup(c);
					b = a;
					data[index] = c;
				}
			}
		}

        /// <summary>
        /// Rolls a byte left
        /// </summary>
        /// <param name="val">input byte to roll</param>
        /// <param name="num">amount of bits to roll</param>
        /// <returns>The left rolled byte</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte rol(byte val, int num)
        {
            return RotateLeftTable[((num & 7) << 8) | val];
        }

        /// <summary>
        /// Rolls a byte right
        /// </summary>
        /// <param name="val">input byte to roll</param>
        /// <param name="num">amount of bits to roll</param>
        /// <returns>The right rolled byte</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ror(byte val, int num)
        {
            return RotateRightTable[((num & 7) << 8) | val];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RotateLeft3(byte value) => (byte)((value << 3) | (value >> 5));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RotateRight3(byte value) => (byte)((value >> 3) | (value << 5));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Rotate4(byte value) => (byte)((value << 4) | (value >> 4));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RotateLeft3Lookup(byte value) => RotateLeftTable[(3 << 8) | value];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RotateRight3Lookup(byte value) => RotateRightTable[(3 << 8) | value];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Rotate4Lookup(byte value) => RotateLeftTable[(4 << 8) | value];

        private static byte[] CreateRotateTable(bool left)
        {
            byte[] table = new byte[8 * 256];
            for (int shift = 0; shift < 8; shift++)
            {
                for (int value = 0; value < 256; value++)
                {
                    table[(shift << 8) | value] = left
                        ? (byte)((value << shift) | (value >> ((8 - shift) & 7)))
                        : (byte)((value >> shift) | (value << ((8 - shift) & 7)));
                }
            }
            return table;
        }
    }
}
