using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MapleLib.WzLib.MSFile
{
    // Credits: Elem8100
    // https://github.com/Elem8100/MapleNecrocer/blob/a1194a96ddf99e5d16225a05dfdf4d616f1fac3f/WzComparerR2.WzLib/Ms_File.cs
    public class WzMsFile : IDisposable
    {
        public WzMsFile(string fileName)
        {
            var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            this.Init(fileStream, fileName, false);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseStream"></param>
        /// <param name="originalFileName">i.e Mob_00000.ms</param>
        /// <param name="leaveOpen"></param>
        public WzMsFile(Stream baseStream, string originalFileName, bool leaveOpen = false)
        {
            this.Init(baseStream, originalFileName, leaveOpen);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseStream"></param>
        /// <param name="originalFileName">i.e Mob_00000.ms</param>
        /// <param name="leaveOpen"></param>
        /// <exception cref="ArgumentNullException"></exception>
        private void Init(Stream baseStream, string originalFileName, bool leaveOpen)
        {
            if (baseStream == null)
                throw new ArgumentNullException(nameof(baseStream));
            if (originalFileName == null)
                throw new ArgumentNullException(nameof(originalFileName));

            this.BaseStream = baseStream;
            this.leaveOpen = leaveOpen;
            this.ReadHeader(originalFileName);
            this.Entries = new List<WzMsEntry>(0);
        }

        public Stream BaseStream { get; private set; }
        public WzMsHeader Header { get; private set; }
        public List<WzMsEntry> Entries { get; private set; }
        private bool leaveOpen;

        private void ReadHeader(string fullFileName)
        {
            string fileName = Path.GetFileName(fullFileName).ToLower();
            this.BaseStream.Position = 0;
            using var bReader = new BinaryReader(this.BaseStream, Encoding.ASCII, true);

            int randByteCount = fileName.Sum(c => (int)c) % 312 + 30;
            byte[] randBytes = bReader.ReadBytes(randByteCount);

            int hashedSaltLen = bReader.ReadInt32();
            int saltLen = (byte)hashedSaltLen ^ randBytes[0];
            byte[] saltBytes = bReader.ReadBytes(saltLen * 2);
            char[] saltChars = new char[saltLen];
            for (int i = 0; i < saltLen; i++)
            {
                saltChars[i] = (char)(randBytes[i] ^ saltBytes[i * 2]);
            }
            string saltStr = new string(saltChars);

            string fileNameWithSalt = fileName + saltStr;
            Span<byte> snowCipherKey = stackalloc byte[16];
            for (int i = 0; i < snowCipherKey.Length; i++)
            {
                snowCipherKey[i] = (byte)(fileNameWithSalt[i % fileNameWithSalt.Length] + i);
            }
            long headerStartPos = this.BaseStream.Position;
            using var snowCipher = new Snow2CryptoTransform(snowCipherKey.ToArray(), null, false);
            using var snowDecoderStream = new CryptoStream(this.BaseStream, snowCipher, CryptoStreamMode.Read, true);
            using var snowReader = new BinaryReader(snowDecoderStream);
            int hash = snowReader.ReadInt32();
            byte version = snowReader.ReadByte();
            int entryCount = snowReader.ReadInt32();

            const int supportedVersion = 2;
            if (version != supportedVersion)
                throw new Exception($"Version check failed. (expected: {supportedVersion}, actual {version})");
            int actualHash = hashedSaltLen + version + entryCount;
            ReadOnlySpan<ushort> u16SaltBytes = MemoryMarshal.Cast<byte, ushort>(saltBytes);
            for (int i = 0; i < u16SaltBytes.Length; i++)
            {
                actualHash += u16SaltBytes[i];
            }
            if (hash != actualHash)
            {
                throw new Exception($"Hash check failed. (expected: {hash}, actual: {actualHash})");
            }

            long entryStartPos = headerStartPos + 9 + fileName.Select(v => (int)v * 3).Sum() % 212 + 33;
            var header = new WzMsHeader(fullFileName, saltStr, fileNameWithSalt, hash, version, entryCount, headerStartPos, entryStartPos);
            this.Header = header;
        }

        public void ReadEntries()
        {
            if (this.Header == null || this.Header.EntryCount == 0 || this.Header.EntryCount == this.Entries.Count)
                return;
            this.Entries.Clear();
            int entryCount = this.Header.EntryCount;
            if (this.Entries.Capacity < entryCount)
                this.Entries.Capacity = entryCount;

            string fileNameWithSalt = this.Header.FileNameWithSalt;
            Span<byte> snowCipherKey2 = stackalloc byte[16];
            for (int i = 0; i < snowCipherKey2.Length; i++)
            {
                snowCipherKey2[i] = (byte)(i + (i % 3 + 2) * fileNameWithSalt[fileNameWithSalt.Length - 1 - i % fileNameWithSalt.Length]);
            }
            using var snowCipher = new Snow2CryptoTransform(snowCipherKey2.ToArray(), null, false);
            this.BaseStream.Position = this.Header.EntryStartPosition;
            using var snowDecoderStream = new CryptoStream(this.BaseStream, snowCipher, CryptoStreamMode.Read);
            using var snowReader = new BinaryReader(snowDecoderStream, Encoding.Unicode, true);

            for (int i = 0; i < entryCount; i++)
            {
                int entryNameLen = snowReader.ReadInt32();
                string entryName = new string(snowReader.ReadChars(entryNameLen));
                int checkSum = snowReader.ReadInt32();
                int flags = snowReader.ReadInt32();
                int startPos = snowReader.ReadInt32();
                int size = snowReader.ReadInt32();
                int sizeAligned = snowReader.ReadInt32();
                int unk1 = snowReader.ReadInt32();
                int unk2 = snowReader.ReadInt32();
                byte[] entryKey = snowReader.ReadBytes(16);

                var entry = new WzMsEntry(entryName, checkSum, flags, startPos, size, sizeAligned, unk1, unk2, entryKey);
                entry.CalculatedCheckSum = flags + startPos + size + sizeAligned + unk1 + entryKey.Sum(b => (int)b);
                this.Entries.Add(entry);
            }

            long dataStartPos = this.BaseStream.Position;
            if ((dataStartPos & 0x3ff) != 0)
            {
                dataStartPos = dataStartPos - (dataStartPos & 0x3ff) + 0x400;
            }
            this.Header.DataStartPosition = dataStartPos;
            foreach (var entry in this.Entries)
            {
                entry.StartPos = dataStartPos + entry.StartPos * 1024;
            }
        }

        public void Close()
        {
            if (this.BaseStream != null)
            {
                if (!this.leaveOpen)
                {
                    this.BaseStream.Dispose();
                }
                this.BaseStream = null;
            }
        }

        public void Dispose()
        {
            this.Close();
        }
    }
}
