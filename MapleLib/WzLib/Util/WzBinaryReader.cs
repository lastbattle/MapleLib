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
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Text;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using System.Buffers;
using System.Runtime.InteropServices;

namespace MapleLib.WzLib.Util
{
    public sealed class WzBinaryReader : BinaryReader
    {
        #region Properties
        /// <summary>
        /// The stackalloc size for decoding strings, and arrays.
        /// - Avoiding L2 Cache Spillover: Keeping allocations well below the L1 cache size (divided by number of cores) prevents unnecessary cache misses.
        /// - Stack Size: While modern systems typically have large stack sizes, it's still prudent to use stack allocations conservatively to avoid stack overflow risks.
        ///            Allocations in the 32 KB to 64 KB range are large enough to be meaningful for many operations while small enough to minimize the risk of stack overflow or significant performance penalties.
        /// - However, we also need to consider that the stack is used for other purposes too, not just our stackalloc.
        /// a conservative yet effective approach would be to use a stackalloc size that's about 1/4 to 1/2 of the L1 data cache size per core. This leaves room for other stack usage while still benefiting from L1 cache performance.
        ///
        /// AMD Ryzen 9700x, L1 Cache: 80 KB / core
        /// AMD Ryzen 5800x, L1 Cache: 64 KB / core
        /// Intel 13/14th Raptor Lake: 80 KB per P-core (32 KB instructions + 48 KB data), 96 KB per E-core(64 KB instructions + 32 KB data)
        /// </summary>
        public const int STACKALLOC_SIZE_LIMIT_L1 = 10 * 1024;  // optimal size is half of CPU's L1 cache.

        public WzMutableKey WzKey { get; init; }
        public uint Hash { get; set; }
        public WzHeader Header { get; set; }

        private readonly long startOffset; // the offset to 

        private readonly ArrayPool<byte> s_bytePool = ArrayPool<byte>.Shared;
        private readonly ArrayPool<char> s_charPool = ArrayPool<char>.Shared;

        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="input"></param>
        /// <param name="WzIv"></param>
        /// <param name="startOffset"></param>
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
            char[]? pooledArray = null;
            try
            {
                Span<char> chars = length <= STACKALLOC_SIZE_LIMIT_L1
                    ? stackalloc char[length]
                    : (pooledArray = s_charPool.Rent(length)).AsSpan(0, length);

                ushort mask = 0xAAAA;
                ref char charsRef = ref MemoryMarshal.GetReference(chars);

                for (int i = 0; i < length; i++)
                {
                    ushort encryptedChar = ReadUInt16();
                    encryptedChar ^= mask;
                    encryptedChar ^= (ushort)((WzKey[(i * 2 + 1)] << 8) + WzKey[(i * 2)]);
                    Unsafe.Add(ref charsRef, i) = (char)encryptedChar;
                    mask++;
                }

                return new string(chars);
            }
            finally
            {
                if (pooledArray != null)
                {
                    s_charPool.Return(pooledArray);
                }
            }
        }

        /// <summary>
        /// Decodes Ascii string
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string DecodeAscii(int length)
        {
            byte[]? pooledArray = null;
            try
            {
                Span<byte> bytes = length <= STACKALLOC_SIZE_LIMIT_L1
                    ? stackalloc byte[length]
                    : (pooledArray = s_bytePool.Rent(length)).AsSpan(0, length);

                byte mask = 0xAA;
                ref byte bytesRef = ref MemoryMarshal.GetReference(bytes);

                for (int i = 0; i < length; i++)
                {
                    byte encryptedChar = ReadByte();
                    encryptedChar ^= mask;
                    encryptedChar ^= (byte)WzKey[i];
                    Unsafe.Add(ref bytesRef, i) = encryptedChar;
                    mask++;
                }

                return Encoding.ASCII.GetString(bytes);
            }
            finally
            {
                if (pooledArray != null)
                {
                    s_bytePool.Return(pooledArray);
                }
            }
        }

        /// <summary>
        /// Reads an ASCII string, without decryption
        /// </summary>
        /// <param name="filePath">Length of bytes to read</param>
        public string ReadString(int length)
        {
            byte[]? pooledArray = null;
            try
            {
                Span<byte> buffer = length <= STACKALLOC_SIZE_LIMIT_L1
                    ? stackalloc byte[length]
                    : (pooledArray = s_bytePool.Rent(length)).AsSpan(0, length);

                BaseStream.Read(buffer);
                return Encoding.ASCII.GetString(buffer);
            }
            finally
            {
                if (pooledArray != null)
                {
                    s_bytePool.Return(pooledArray);
                }
            }
        }

        public string ReadNullTerminatedString()
        {
            const int initialBufferSize = 256;
            byte[]? pooledArray = null;
            try
            {
                Span<byte> buffer = stackalloc byte[initialBufferSize];
                int position = 0;
                byte b;

                while ((b = ReadByte()) != 0)
                {
                    if (position == buffer.Length)
                    {
                        // Need to expand to array pool
                        if (pooledArray == null)
                        {
                            pooledArray = s_bytePool.Rent(buffer.Length * 2);
                            buffer.CopyTo(pooledArray);
                        }
                        else
                        {
                            var newArray = s_bytePool.Rent(pooledArray.Length * 2);
                            pooledArray.AsSpan(0, position).CopyTo(newArray);
                            s_bytePool.Return(pooledArray);
                            pooledArray = newArray;
                        }
                        buffer = pooledArray;
                    }
                    buffer[position++] = b;
                }

                return Encoding.UTF8.GetString(buffer.Slice(0, position));
            }
            finally
            {
                if (pooledArray != null)
                {
                    s_bytePool.Return(pooledArray);
                }
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
            offset -= WzAESConstant.WZ_OffsetConstant;
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
        public string DecryptString(ReadOnlySpan<char> stringToDecrypt)
        {
            char[]? pooledArray = null;
            try
            {
                Span<char> outputChars = stringToDecrypt.Length <= STACKALLOC_SIZE_LIMIT_L1
                    ? stackalloc char[stringToDecrypt.Length]
                    : (pooledArray = s_charPool.Rent(stringToDecrypt.Length)).AsSpan(0, stringToDecrypt.Length);

                ref char outputRef = ref MemoryMarshal.GetReference(outputChars);
                ref char inputRef = ref MemoryMarshal.GetReference(stringToDecrypt);

                for (int i = 0; i < stringToDecrypt.Length; i++)
                {
                    Unsafe.Add(ref outputRef, i) = (char)(
                        Unsafe.Add(ref inputRef, i) ^
                        ((char)((WzKey[i * 2 + 1] << 8) + WzKey[i * 2]))
                    );
                }

                return new string(outputChars);
            }
            finally
            {
                if (pooledArray != null)
                {
                    s_charPool.Return(pooledArray);
                }
            }
        }

        public string DecryptNonUnicodeString(ReadOnlySpan<char> stringToDecrypt)
        {
            char[]? pooledArray = null;
            try
            {
                Span<char> outputChars = stringToDecrypt.Length <= STACKALLOC_SIZE_LIMIT_L1
                    ? stackalloc char[stringToDecrypt.Length]
                    : (pooledArray = s_charPool.Rent(stringToDecrypt.Length)).AsSpan(0, stringToDecrypt.Length);

                ref char outputRef = ref MemoryMarshal.GetReference(outputChars);
                ref char inputRef = ref MemoryMarshal.GetReference(stringToDecrypt);

                for (int i = 0; i < stringToDecrypt.Length; i++)
                {
                    Unsafe.Add(ref outputRef, i) = (char)(Unsafe.Add(ref inputRef, i) ^ WzKey[i]);
                }

                return new string(outputChars);
            }
            finally
            {
                if (pooledArray != null)
                {
                    s_charPool.Return(pooledArray);
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadStringBlock(long offset) => ReadByte() switch
        {
            0 or WzImage.WzImageHeaderByte_WithoutOffset => ReadString(),
            1 or WzImage.WzImageHeaderByte_WithOffset => ReadStringAtOffset(offset + ReadInt32()),
            _ => string.Empty
        };

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
            byte[]? pooledArray = null;
            try
            {
                Span<byte> buffer = numberOfBytes <= STACKALLOC_SIZE_LIMIT_L1
                    ? stackalloc byte[numberOfBytes]
                    : (pooledArray = s_bytePool.Rent(numberOfBytes)).AsSpan(0, numberOfBytes);

                BaseStream.Read(buffer);
                string hex = HexTool.ToString(buffer.ToArray());
                Debug.WriteLine(hex);

                BaseStream.Position -= numberOfBytes;
            }
            finally
            {
                if (pooledArray != null)
                {
                    s_bytePool.Return(pooledArray);
                }
            }
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