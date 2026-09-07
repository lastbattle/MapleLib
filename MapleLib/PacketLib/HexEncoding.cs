using System;
using MapleLib.Helpers;

namespace MapleLib.PacketLib
{
	/// <summary>
	/// Class to handle Hex Encoding and Hex Conversions
	/// </summary>
	public class HexEncoding
	{

		/// <summary>
		/// Checks if a character is a hex digit
		/// </summary>
		/// <param name="c">Char to check</param>
		/// <returns>Char is a hex digit</returns>
		public static bool IsHexDigit(Char c)
		{
			int numA = Convert.ToInt32('A');
			int num1 = Convert.ToInt32('0');
			c = Char.ToUpper(c);
			int numChar = Convert.ToInt32(c);

			return (numChar >= numA && numChar < (numA + 6)) || (numChar >= num1 && numChar < (num1 + 10));
		}

		/// <summary>
		/// Convert a hex string to a byte
		/// </summary>
		/// <param name="hex">Byte as a hex string</param>
		/// <returns>Byte representation of the string</returns>
		private static byte HexToByte(string hex)
		{
			if (hex.Length > 2 || hex.Length <= 0)
				throw new ArgumentException("hex must be 1 or 2 characters in length");
			byte newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
			return newByte;
		}

		/// <summary>
		/// Convert a hex string to a byte array
		/// </summary>
		/// <param name="hex">byte array as a hex string</param>
		/// <returns>Byte array representation of the string</returns>
		public static byte[] GetBytes(string hexString)
		{
			return ByteUtils.HexToBytes(hexString);
		}

		/// <summary>
		/// Convert byte array to ASCII
		/// </summary>
		/// <param name="bytes">Bytes to convert to ASCII</param>
		/// <returns>The byte array as an ASCII string</returns>
        public static string ToStringFromAscii(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            return string.Create(bytes.Length, bytes, static (destination, source) =>
            {
                for (int index = 0; index < source.Length; index++)
                    destination[index] = source[index] < 32 ? '.' : (char)source[index];
            });
        }
    }
}
