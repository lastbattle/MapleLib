using System;
using System.Buffers;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MapleLib.Helpers
{
    public static class ByteUtils
    {
        private static readonly SearchValues<char> PlainHexDigits = SearchValues.Create("0123456789ABCDEFabcdef");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareBytearrays(byte[] a, byte[] b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            return a.Length == b.Length && a.SequenceEqual(b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] IntegerToLittleEndian(int data)
        {
            byte[] b = BitConverter.GetBytes(data);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(b);
            }
            return b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] HexToBytes(string pValue)
        {
            ArgumentNullException.ThrowIfNull(pValue);
            if (pValue.Length == 0)
            {
                return Array.Empty<byte>();
            }

            if ((pValue.Length & 1) == 0 && pValue.AsSpan().IndexOfAnyExcept(PlainHexDigits) < 0)
                return Convert.FromHexString(pValue);

            // Keep the accepted wire notation intentionally small: optional 0x
            // prefixes, whitespace, and conventional byte separators. Any other
            // character is rejected instead of silently disappearing.
            int digitCount = 0;
            bool atTokenStart = true;
            bool sawHexDigit = false;
            for (int i = 0; i < pValue.Length; i++)
            {
                char c = pValue[i];
                if (char.IsWhiteSpace(c) || c is '-' or ':' or ',')
                {
                    atTokenStart = true;
                    continue;
                }

                if (atTokenStart && c == '0' && i + 1 < pValue.Length &&
                    (pValue[i + 1] == 'x' || pValue[i + 1] == 'X'))
                {
                    i++;
                    atTokenStart = false;
                    continue;
                }

                if (!IsHexDigit(c))
                {
                    throw new FormatException($"Invalid hexadecimal character '{c}'.");
                }

                digitCount++;
                atTokenStart = false;
                sawHexDigit = true;
            }

            if (!sawHexDigit || digitCount % 2 != 0)
            {
                throw new FormatException("The hexadecimal string must contain an even number of digits.");
            }

            byte[] bytes = new byte[digitCount / 2];
            int byteIndex = 0;
            char high = '\0';
            bool hasHigh = false;
            atTokenStart = true;
            for (int i = 0; i < pValue.Length; i++)
            {
                char c = pValue[i];
                if (char.IsWhiteSpace(c) || c is '-' or ':' or ',')
                {
                    atTokenStart = true;
                    continue;
                }

                if (atTokenStart && c == '0' && i + 1 < pValue.Length &&
                    (pValue[i + 1] == 'x' || pValue[i + 1] == 'X'))
                {
                    i++;
                    atTokenStart = false;
                    continue;
                }

                atTokenStart = false;
                if (!hasHigh)
                {
                    high = c;
                    hasHigh = true;
                    continue;
                }

                char low = c;
                if (high == '*' || low == '*')
                {
                    if (high != '*' || low != '*')
                    {
                        throw new FormatException("A wildcard byte must be written as '**'.");
                    }

                    bytes[byteIndex++] = (byte)Random.Shared.Next(0, byte.MaxValue + 1);
                }
                else
                {
                    bytes[byteIndex++] = (byte)((HexDigitValue(high) << 4) | HexDigitValue(low));
                }
                hasHigh = false;
            }

            return bytes;
        }

        /// <summary>
        /// Creates a hex-string from byte array.
        /// </summary>
        /// <param name="bytes">Input bytes.</param>
        /// <returns>String that represents the byte-array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string BytesToHex(byte[] bytes, string header = "")
        {
            ArgumentNullException.ThrowIfNull(bytes);
            string prefix = header ?? string.Empty;
            return string.Create(checked(prefix.Length + bytes.Length * 3), (bytes, prefix), static (destination, state) =>
            {
                state.prefix.AsSpan().CopyTo(destination);
                int offset = state.prefix.Length;
                foreach (byte value in state.bytes)
                {
                    destination[offset++] = ToUpperHex(value >> 4);
                    destination[offset++] = ToUpperHex(value & 0x0F);
                    destination[offset++] = ' ';
                }
            });
        }

        /// <summary>
        /// Checks if a character is a hexadecimal digit.
        /// </summary>
        /// <param name="c">The character to check</param>
        /// <returns>true if <paramref name="c"/>is a hexadecimal digit; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f') || (c == '*');
        }

        /// <summary>
        /// Convert a 2-digit hexadecimal string to a byte.
        /// </summary>
        /// <param name="hex">The hexadecimal string.</param>
        /// <returns>The byte representation of the string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte HexToByte(string hex)
        {
            if (hex == null) throw new ArgumentNullException("hex");
            if (hex.Length == 0 || 2 < hex.Length)
            {
                throw new ArgumentOutOfRangeException("hex", "The hexadecimal string must be 1 or 2 characters in length.");
            }
            byte newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            return newByte;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexDigitValue(char value)
        {
            if (value <= '9')
                return value - '0';
            return (value | 0x20) - 'a' + 10;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char ToUpperHex(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(uint x, byte n)
        {
            return (uint)(((x) << (n)) | ((x) >> (32 - (n))));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateRight(uint x, byte n)
        {
            return (uint)(((x) >> (n)) | ((x) << (32 - (n))));
        }

    }
}
