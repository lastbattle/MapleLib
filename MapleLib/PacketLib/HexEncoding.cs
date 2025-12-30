using System;

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
			string newString = string.Empty;
			char c;
			// remove all none A-F, 0-9, characters
			for (int i = 0; i < hexString.Length; i++)
			{
				c = hexString[i];
				if (IsHexDigit(c))
					newString += c;
			}
			// if odd number of characters, discard last character
			if (newString.Length % 2 != 0)
			{
				newString = newString.Substring(0, newString.Length - 1);
			}

			int byteLength = newString.Length / 2;
			byte[] bytes = new byte[byteLength];
			string hex;
			int j = 0;
			for (int i = 0; i < bytes.Length; i++)
			{
				hex = new string(new Char[] { newString[j], newString[j + 1] });
				bytes[i] = HexToByte(hex);
				j += 2;
			}
			return bytes;
		}

		/// <summary>
		/// Convert byte array to ASCII
		/// </summary>
		/// <param name="bytes">Bytes to convert to ASCII</param>
		/// <returns>The byte array as an ASCII string</returns>
        public static string ToStringFromAscii(byte[] bytes)
        {
            char[] ret = new char[bytes.Length];
            for (int x = 0; x < bytes.Length; x++)
            {
                // Use a ternary operator to avoid an if statement
                ret[x] = (bytes[x] < 32 && bytes[x] >= 0) ? '.' : (char)((short)bytes[x] & 0xFF);
            }
            return new string(ret);
        }
    }
}