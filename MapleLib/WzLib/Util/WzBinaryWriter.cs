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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MapleLib.Helpers;
using MapleLib.MapleCryptoLib;
using MapleLib.WzLib.WzStructure.Enums;

namespace MapleLib.WzLib.Util
{
	/*
	   TODO : Maybe WzBinaryReader/Writer should read and contain the hash (this is probably what's going to happen)
	*/
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

		public WzBinaryWriter(Stream output, byte[] WzIv, uint Hash)
			: this(output, WzIv, false) {
			this.Hash = Hash;
		}

		public WzBinaryWriter(Stream output, byte[] WzIv, bool leaveOpen)
			: base(output)
		{
			WzKey = WzKeyGenerator.GenerateWzKey(WzIv);
            StringCache = new Dictionary<string, int>();
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
		public void WriteStringValue(string s, int withoutOffset, int withOffset)
		{
			// if length is > 4 and the string cache contains the string
			// writes the offset instead
			if (s.Length > 4 && StringCache.ContainsKey(s))
			{
				Write((byte)withOffset);
				Write((int)StringCache[s]);
			}
			else
			{
				Write((byte)withoutOffset);
				int sOffset = (int)this.BaseStream.Position;
				Write(s);
				if (!StringCache.ContainsKey(s))
				{
					StringCache[s] = sOffset;
				}
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
			string storeName = string.Format("{0}_{1}", (byte) type, stringObjectValue);

			// if length is > 4 and the string cache contains the string
			// writes the offset instead
			if (stringObjectValue.Length > 4 && StringCache.ContainsKey(storeName))
			{
				Write((byte)WzDirectoryType.RetrieveStringFromOffset_2); // 2
				Write((int)StringCache[storeName]);

				return true;
			}
			else
			{
				int sOffset = (int)(this.BaseStream.Position - Header.FStart);
				Write((byte)type);
				Write(stringObjectValue);
                StringCache[storeName] = sOffset;
            }
			return false;
		}

		public override void Write(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				Write((byte)0);
				return;
			}
			bool unicode = value.Any(c => c > sbyte.MaxValue);
			
			if (unicode)
				WriteUnicodeString(value);
			else // ASCII
				WriteAsciiString(value);
		}

		/// <summary>
		/// Encodes unicode string
		/// </summary>
		/// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUnicodeString(string value)
        {
            ushort mask = 0xAAAA;

            WriteStringLength(value.Length, false);

            for (int i = 0; i < value.Length; i++)
            {
                ushort encryptedChar = (ushort)value[i];
                encryptedChar ^= (ushort)((WzKey[i * 2 + 1] << 8) + WzKey[i * 2]);
                encryptedChar ^= mask;
                mask++;
                Write(encryptedChar);
            }
        }

		/// <summary>
		/// Encodes Ascii string
		/// </summary>
		/// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteAsciiString(string value)
        {
            byte mask = 0xAA;

            WriteStringLength(value.Length, true);

            for (int i = 0; i < value.Length; i++)
            {
                byte encryptedChar = (byte)value[i];
                encryptedChar ^= WzKey[i];
                encryptedChar ^= mask;
                mask++;
                Write(encryptedChar);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringLength(int length, bool isAscii)
        {
            // Bugfix - >= because if value.Length = MaxValue, MaxValue will be written and then treated as a long-length marker
            // Note - no need for >= here because of 2's complement (MinValue == -(MaxValue + 1))
            if (length > sbyte.MaxValue)
            {
                Write(sbyte.MinValue);
                Write(length);
            }
            else
            {
				if (isAscii)
                    Write((sbyte)(-length));
                else
                    Write((sbyte)length);
            }
        }

        public void Write(string value, int length)
        {
            int writeLength = Math.Min(value.Length, length);
            Write(value.AsSpan(0, writeLength));
            for (int i = writeLength; i < length; i++)
            {
                Write((byte)0);
            }
        }

        public char[] EncryptString(string stringToEncrypt)
        {
            return stringToEncrypt.Select((c, i) => (char)(c ^ ((WzKey[i * 2 + 1] << 8) + WzKey[i * 2]))).ToArray();
        }

        public char[] EncryptNonUnicodeString(string stringToEncrypt)
        {
            return stringToEncrypt.Select((c, i) => (char)(c ^ WzKey[i])).ToArray();
        }

        public void WriteNullTerminatedString(string value)
        {
            Write(value.AsSpan());
            Write((byte)0);
        }

        public void WriteCompressedInt(int value) => WriteCompressed(value);

        public void WriteCompressedLong(long value) => WriteCompressed(value);

        private void WriteCompressed<T>(T value) where T : IConvertible
        {
            long longValue = value.ToInt64(null);
            if (longValue > sbyte.MaxValue || longValue <= sbyte.MinValue)
            {
                Write(sbyte.MinValue);
                switch (Type.GetTypeCode(typeof(T)))
                {
                    case TypeCode.Int32:
                        Write(value.ToInt32(null));
                        break;
                    case TypeCode.Int64:
                        Write(value.ToInt64(null));
                        break;
                    default:
                        throw new ArgumentException($"Unsupported type for compressed writing: {typeof(T)}");
                }
            }
            else
            {
                Write(value.ToSByte(null));
            }
        }

        public void WriteOffset(long value)
		{
			uint encOffset = (uint)BaseStream.Position;
			encOffset = (encOffset - Header.FStart) ^ 0xFFFFFFFF;
			encOffset *= Hash; // could this be removed? 
			encOffset -= MapleCryptoConstants.WZ_OffsetConstant;
			encOffset = ByteUtils.RotateLeft(encOffset, (byte)(encOffset & 0x1F));
			uint writeOffset = encOffset ^ ((uint) value - (Header.FStart * 2));
			Write(writeOffset);
		}

		public override void Close()
		{
			if (!LeaveOpen)
			{
				base.Close();
			}
		}

		#endregion
	}
}