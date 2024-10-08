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

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;

namespace MapleLib.WzLib.Util
{
    public class WzBinaryReader : BinaryReader
    {
        #region Properties
        public WzMutableKey WzKey { get; set; }
        public uint Hash { get; set; }
        public WzHeader Header { get; set; }

        private readonly long startOffset; // the offset to 
        #endregion

        #region Constructors
        public WzBinaryReader(Stream input, byte[] WzIv, long startOffset = 0)
            : base(input)
        {
            WzKey = WzKeyGenerator.GenerateWzKey(WzIv);
            this.startOffset = startOffset;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Sets the base stream position to the header FStart + offset
        /// </summary>
        /// <param name="offset"></param>
        public void SetOffsetFromFStartToPosition(int offset)
        {
            BaseStream.Position = (Header.FStart + offset) - startOffset;
        }

        public void RollbackStreamPosition(int byOffset)
        {
            if ((BaseStream.Position - startOffset) < byOffset)
                throw new Exception("Cant rollback stream position below 0");

            BaseStream.Position -= byOffset;
        }

        public string ReadStringAtOffset(long Offset)
        {
            return ReadStringAtOffset(Offset, false);
        }

        public string ReadStringAtOffset(long Offset, bool readByte)
        {
            long CurrentOffset = BaseStream.Position;
            BaseStream.Position = Offset - startOffset;
            if (readByte)
            {
                ReadByte();
            }
            string ReturnString = ReadString();
            BaseStream.Position = CurrentOffset;
            return ReturnString;
        }

        /// <summary>
        /// Reads a string from the buffer
        /// </summary>
        /// <returns></returns>
        public override string ReadString()
        {
            sbyte smallLength = base.ReadSByte();
            if (smallLength == 0)
                return string.Empty;

            int length;
            if (smallLength > 0) // Unicode
                length = smallLength == sbyte.MaxValue ? ReadInt32() : smallLength;
            else // ASCII
                length = smallLength == sbyte.MinValue ? ReadInt32() : -smallLength;

            if (length <= 0)
                return string.Empty;

            if (smallLength > 0) // Unicode
                return DecodeUnicode(length);
            else
                return DecodeAscii(length);
        }

        /// <summary>
        /// Decodes unicode string
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string DecodeUnicode(int length)
        {
            Span<char> chars = length <= 1024 ? stackalloc char[length] : new char[length];
            ushort mask = 0xAAAA;
            
            for (int i = 0; i < length; i++)
            {
                ushort encryptedChar = ReadUInt16();
                encryptedChar ^= mask;
                encryptedChar ^= (ushort)((WzKey[(i * 2 + 1)] << 8) + WzKey[(i * 2)]);
                chars[i] = (char)encryptedChar;
                mask++;
            }
            return new string(chars);
        }

        /// <summary>
        /// Decodes Ascii string
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string DecodeAscii(int length)
        {
            Span<byte> bytes = length <= 1024 ? stackalloc byte[length] : new byte[length];
            byte mask = 0xAA;
            
            for (int i = 0; i < length; i++)
            {
                byte encryptedChar = ReadByte();
                encryptedChar ^= mask;
                encryptedChar ^= (byte)WzKey[i];
                bytes[i] = encryptedChar;
                mask++;
            }
            return Encoding.ASCII.GetString(bytes);
        }

        /// <summary>
        /// Reads an ASCII string, without decryption
        /// </summary>
        /// <param name="filePath">Length of bytes to read</param>
        public string ReadString(int length)
        {
            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        public string ReadNullTerminatedString()
        {
            using (var memoryStream = new MemoryStream())
            {
                byte b;
                while ((b = ReadByte()) != 0)
                {
                    memoryStream.WriteByte(b);
                }
                return Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            }
        }

        public int ReadCompressedInt()
        {
            sbyte sb = base.ReadSByte();
            if (sb == sbyte.MinValue)
            {
                return ReadInt32();
            }
            return sb;
        }

        public long ReadLong()
        {
            sbyte sb = base.ReadSByte();
            if (sb == sbyte.MinValue)
            {
                return ReadInt64();
            }
            return sb;
        }

        /// <summary>
        /// The amount of bytes available remaining in the stream
        /// </summary>
        /// <returns></returns>
        public long Available()
        {
            return BaseStream.Length - BaseStream.Position;
        }

        public long ReadOffset()
        {
            uint offset = (uint)BaseStream.Position;
            offset = (offset - Header.FStart) ^ uint.MaxValue;
            offset *= Hash;
            offset -= MapleCryptoConstants.WZ_OffsetConstant;
            offset = WzTool.RotateLeft(offset, (byte)(offset & 0x1F));
            uint encryptedOffset = ReadUInt32();
            offset ^= encryptedOffset;
            offset += Header.FStart * 2;

            return (offset + startOffset);
        }

        /// <summary>
        /// Decrypts List.wz string without mask
        /// </summary>
        /// <param name="stringToDecrypt"></param>
        /// <returns></returns>
        public string DecryptString(char[] stringToDecrypt)
        {
            Span<char> outputChars = stringToDecrypt.Length <= 1024
                    ? stackalloc char[stringToDecrypt.Length]
                    : new char[stringToDecrypt.Length];

            for (int i = 0; i < stringToDecrypt.Length; i++)
            {
                outputChars[i] = (char)(stringToDecrypt[i] ^ ((char)((WzKey[i * 2 + 1] << 8) + WzKey[i * 2])));
            }

            return new string(outputChars);
        }


        public string DecryptNonUnicodeString(char[] stringToDecrypt)
        {
            Span<char> outputChars = stringToDecrypt.Length <= 1024
                   ? stackalloc char[stringToDecrypt.Length]
                   : new char[stringToDecrypt.Length];

            for (int i = 0; i < stringToDecrypt.Length; i++)
            {
                outputChars[i] = (char)(stringToDecrypt[i] ^ WzKey[i]);
            }

            return new string(outputChars);
        }

        public string ReadStringBlock(long offset)
        {
            switch (ReadByte())
            {
                case 0:
                case WzImage.WzImageHeaderByte_WithoutOffset:
                    return ReadString();
                case 1:
                case WzImage.WzImageHeaderByte_WithOffset:
                    return ReadStringAtOffset(offset + ReadInt32());
                default:
                    return "";
            }
        }

        #endregion

        #region Tools
        /// <summary>
        /// Cuts out a section of the stream, and creates a new WzBinaryReader object to allow 
        /// for concurrent file i/o reading.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public WzBinaryReader CreateReaderForSection(long start, int length)
        {
            if (start < 0 || start >= BaseStream.Length)
                throw new ArgumentOutOfRangeException(nameof(start));

            if (length <= 0 || start + length > BaseStream.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            byte[] buffer = new byte[length];

            lock (this)
            {
                long startPositionBeforeRead = this.BaseStream.Position; // get pos before

                // read the entire region
                BaseStream.Seek(start, SeekOrigin.Begin);
                BaseStream.Read(buffer, 0, length);

                this.BaseStream.Position = startPositionBeforeRead; // reset stream pos
            }

            MemoryStream memoryStream = new MemoryStream(buffer);
            return new WzBinaryReader(memoryStream, WzKey.GetKeys(), start)
            {
                WzKey = this.WzKey,
                Hash = this.Hash,
                Header = this.Header
            };
        }
        #endregion

        #region Debugging Methods
        /// <summary>
        /// Prints the next numberOfBytes in the stream in the system debug console.
        /// </summary>
        /// <param name="numberOfBytes"></param>
        public void PrintHexBytes(int numberOfBytes)
        {
#if DEBUG // only debug
            string hex = HexTool.ToString(ReadBytes(numberOfBytes));
            Debug.WriteLine(hex);

            this.BaseStream.Position -= numberOfBytes;
#endif
        }
        #endregion

        #region Overrides
        public override void Close()
        {
            // debug here
            base.Close();
        }
        #endregion
    }
}