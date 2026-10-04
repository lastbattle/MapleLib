using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MapleLib.Helpers;
using MapleLib.MapleCryptoLib;
using MapleLib.WzLib.WzStructure.Enums;

namespace MapleLib.WzLib.Util
{
    /// <summary>
    ///  TODO : Maybe WzBinaryReader/Writer should read and contain the hash (this is probably what's going to happen)
    /// </summary>
    public class WzBinaryWriter : BinaryWriter
    {
        #region Properties
        public WzMutableKey WzKey { get; set; }
        public uint Hash { get; set; }
        public Dictionary<string, int> StringCache { get; set; }
        public WzHeader Header { get; set; }
        public bool LeaveOpen { get; internal set; }
        #endregion

        #region Constructors
        public WzBinaryWriter(Stream output, byte[] WzIv)
            : this(output, WzIv, false)
        {
            this.Hash = 0;
        }

        public WzBinaryWriter(Stream output, byte[] WzIv, uint Hash) : this(output, WzIv, false)
        {
            this.Hash = Hash;
        }

        public WzBinaryWriter(Stream output, byte[] WzIv, bool leaveOpen)
            : base(output)
        {
            WzKey = WzKeyGenerator.GenerateWzKey(WzIv);
            StringCache = [];
            this.LeaveOpen = leaveOpen;
        }
        #endregion

        #region Methods
        /// <summary>
        /// ?InternalSerializeString@@YAHPAGPAUIWzArchive@@EE@Z
        /// </summary>
        /// <param name="s"></param>
        /// <param name="withoutOffset">bExistID_0x73   0x73</param>
        /// <param name="withOffset">bNewID_0x1b  0x1B</param>
        public void WriteStringValue(string str, int withoutOffset, int withOffset)
        {
            // if length is > 4 and the string cache contains the string
            // writes the offset instead
            if (str.Length > 4 && StringCache.TryGetValue(str, out int cachedOffset))
            {
                Write((byte)withOffset);
                Write(cachedOffset);
            }
            else
            {
                Write((byte)withoutOffset);
                int sOffset = (int)this.BaseStream.Position;
                Write(str);
                StringCache.TryAdd(str, sOffset);
            }
        }

        /// <summary>
        /// Writes the Wz object value
        /// </summary>
        /// <param name="stringObjectValue"></param>
        /// <param name="type"></param>
        /// <param name="unk_GMS230"></param>
        /// <returns>true if the Wz object value is written as an offset in the Wz file, else if not</returns>
        public bool WriteWzObjectValue(string stringObjectValue, WzDirectoryType type)
        {
            string storeName = $"{(byte)type}_{stringObjectValue}";

            // if length is > 4 and the string cache contains the string
            // writes the offset instead
            if (stringObjectValue.Length > 4 && StringCache.TryGetValue(storeName, out int cachedOffset))
            {
                Write((byte)WzDirectoryType.RetrieveStringFromOffset_2); // 2
                Write(cachedOffset);

                return true;
            }
            else
            {
                int sOffset = (int)(this.BaseStream.Position - Header.FStart);
                Write((byte)type);
                Write(stringObjectValue);
                StringCache.TryAdd(storeName, sOffset);
            }
            return false;
        }

        public override void Write(string value)
        {
            if (value.Length == 0)
            {
                Write((byte)0);
                return;
            }
            bool unicode = value.AsSpan().IndexOfAnyInRange((char)(sbyte.MaxValue + 1), char.MaxValue) >= 0;

            if (unicode)
            {
                WriteUnicodeString(value);
            }
            else // ASCII
            {
                WriteAsciiString(value);
            }
        }

        /// <summary>
        /// Encodes unicode string
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUnicodeString(string value)
        {
            if (value.Length >= sbyte.MaxValue) // Bugfix - >= because if value.Length = MaxValue, MaxValue will be written and then treated as a long-length marker
            {
                Write(sbyte.MaxValue);
                Write(value.Length);
            }
            else
            {
                Write((sbyte)value.Length);
            }

            int byteLength = checked(value.Length * 2);
            byte[]? rented = null;
            try
            {
                Span<byte> encoded = byteLength <= 256
                    ? stackalloc byte[byteLength]
                    : (rented = ArrayPool<byte>.Shared.Rent(byteLength)).AsSpan(0, byteLength);

                WzKey.EnsureKeySize(byteLength);
                ReadOnlySpan<byte> keyBytes = WzKey.GetKeySpan();
                if (BitConverter.IsLittleEndian)
                {
                    Span<ushort> encodedChars = MemoryMarshal.Cast<byte, ushort>(encoded);
                    ReadOnlySpan<ushort> keyWords = MemoryMarshal.Cast<byte, ushort>(keyBytes);
                    ushort mask = 0xAAAA;
                    for (int i = 0; i < value.Length; i++)
                    {
                        ushort encryptedChar = value[i];
                        encryptedChar ^= keyWords[i];
                        encodedChars[i] = (ushort)(encryptedChar ^ mask++);
                    }
                }
                else
                {
                    ushort mask = 0xAAAA;
                    for (int i = 0; i < value.Length; i++)
                    {
                        ushort encryptedChar = value[i];
                        encryptedChar ^= (ushort)((keyBytes[i * 2 + 1] << 8) + keyBytes[i * 2]);
                        encryptedChar ^= mask++;
                        BinaryPrimitives.WriteUInt16LittleEndian(encoded.Slice(i * 2, 2), encryptedChar);
                    }
                }

                BaseStream.Write(encoded);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<byte>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Encodes ASCII string
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteAsciiString(string value)
        {
            if (value.Length > sbyte.MaxValue) // Note - no need for >= here because of 2's complement (MinValue == -(MaxValue + 1))
            {
                Write(sbyte.MinValue);
                Write(value.Length);
            }
            else
            {
                Write((sbyte)(-value.Length));
            }

            byte[]? rented = null;
            try
            {
                Span<byte> encoded = value.Length <= 256
                    ? stackalloc byte[value.Length]
                    : (rented = ArrayPool<byte>.Shared.Rent(value.Length)).AsSpan(0, value.Length);

                WzKey.EnsureKeySize(value.Length);
                ReadOnlySpan<byte> keyBytes = WzKey.GetKeySpan();
                byte mask = 0xAA;
                for (int i = 0; i < value.Length; i++)
                {
                    encoded[i] = (byte)((byte)value[i] ^ keyBytes[i] ^ mask++);
                }

                BaseStream.Write(encoded);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public char[] EncryptString(string stringToEncrypt)
        {
            ArgumentNullException.ThrowIfNull(stringToEncrypt, "source");
            char[] encrypted = new char[stringToEncrypt.Length];
            WzKey.EnsureKeySize(checked(stringToEncrypt.Length * 2));
            ReadOnlySpan<byte> keyBytes = WzKey.GetKeySpan();
            for (int i = 0; i < encrypted.Length; i++)
                encrypted[i] = (char)(stringToEncrypt[i] ^ ((keyBytes[i * 2 + 1] << 8) + keyBytes[i * 2]));
            return encrypted;
        }

        public char[] EncryptNonUnicodeString(string stringToEncrypt)
        {
            ArgumentNullException.ThrowIfNull(stringToEncrypt, "source");
            char[] encrypted = new char[stringToEncrypt.Length];
            WzKey.EnsureKeySize(stringToEncrypt.Length);
            ReadOnlySpan<byte> keyBytes = WzKey.GetKeySpan();
            for (int i = 0; i < encrypted.Length; i++)
                encrypted[i] = (char)(stringToEncrypt[i] ^ keyBytes[i]);
            return encrypted;
        }

        public void WriteNullTerminatedString(string value)
        {
            Write(value.AsSpan());
            Write((byte)0);
        }

        public void WriteCompressedInt(int value)
        {
            if ((uint)(value + 127) > 254u)
            {
                Write(sbyte.MinValue);
                Write(value);
            }
            else
            {
                Write((sbyte)value);
            }
        }

        public void WriteCompressedLong(long value)
        {
            if (value > sbyte.MaxValue || value <= sbyte.MinValue)
            {
                Write(sbyte.MinValue);
                Write(value);
            }
            else
            {
                Write((sbyte)value);
            }
        }

        public void WriteOffset(long value)
        {
            uint encOffset = (uint)BaseStream.Position;
            encOffset = (encOffset - Header.FStart) ^ 0xFFFFFFFF;
            encOffset *= Hash; // could this be removed? 
            encOffset -= WzAESConstant.WZ_OffsetConstant;
            encOffset = ByteUtils.RotateLeft(encOffset, (byte)(encOffset & 0x1F));
            uint writeOffset = encOffset ^ ((uint)value - (Header.FStart * 2));
            Write(writeOffset);
        }

        public override void Close()
        {
            if (!LeaveOpen)
            {
                base.Close();
            }
        }

        internal bool TryWriteRemainingMemoryStream(MemoryStream source)
        {
            if ((GetType() != typeof(WzBinaryWriter) && GetType() != typeof(WzImgFileWriter)) ||
                (OutStream.GetType() != typeof(MemoryStream) && OutStream.GetType() != typeof(FileStream)) ||
                !OutStream.CanWrite)
                return false;

            // Copy without the public writer's BaseStream.Flush callback.
            source.CopyTo(OutStream);
            return true;
        }

        #endregion
    }

    // Owned IMG export reads logical positions without flushing every string.
    // Its selected stream handles staging, seeks and final file synchronization.
    // Public writers retain their Flush callbacks.
    internal sealed class WzImgFileWriter : WzBinaryWriter
    {
        internal WzImgFileWriter(Stream output, byte[] wzIv) : base(output, wzIv)
        {
        }

        public override Stream BaseStream => OutStream;
    }
}
