using System;

namespace MapleLib.PacketLib
{
    public static class HexTool
    {
        private const string UpperHex = "0123456789ABCDEF";
        private const string LowerHex = "0123456789abcdef";

        /// <summary>
        /// Converts a byte value to readable hex representation
        /// </summary>
        /// <param name="byteValue"></param>
        /// <returns></returns>
        public static String ToString(byte byteValue)
        {
            return string.Create(2, byteValue, static (destination, value) =>
            {
                destination[0] = UpperHex[value >> 4];
                destination[1] = UpperHex[value & 0x0F];
            });
        }

        /// <summary>
        /// Converts an array of bytes to readable hex representation
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static String ToString(byte[] bytes)
        {
            return FormatBytes(bytes, UpperHex);
        }


        /// <summary>
        /// Converts an array of bytes to readable hex representation
        /// Extension method for PacketWriter 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static String ToString(this PacketReader reader)
        {
            byte[] bytes = reader.ToArray();

            return FormatBytes(bytes, UpperHex);
        }

        /// <summary>
        /// Converts an array of bytes to readable hex representation
        /// Extension method for PacketWriter 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static String ToString(this PacketWriter writer)
        {
            byte[] bytes = writer.ToArray();

            return FormatBytes(bytes, UpperHex);
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return FormatBytes(ba, LowerHex);
        }

        private static string FormatBytes(byte[] bytes, string alphabet)
        {
            int length = bytes.Length;
            return string.Create(checked(length * 3), (bytes, alphabet), static (destination, state) =>
            {
                int offset = 0;
                foreach (byte value in state.bytes)
                {
                    destination[offset++] = state.alphabet[value >> 4];
                    destination[offset++] = state.alphabet[value & 0x0F];
                    destination[offset++] = ' ';
                }
            });
        }
    }
}
