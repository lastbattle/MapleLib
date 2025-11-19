using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using MapleLib.WzLib;

namespace MapleLib.WzLib.MSFile
{
    // Credits: Elem8100
    // https://github.com/Elem8100/MapleNecrocer/blob/a1194a96ddf99e5d16225a05dfdf4d616f1fac3f/WzComparerR2.WzLib/Ms_File.cs
    public class WzMsFile : IDisposable
    {
        private string originalFileName;
        private string msFilePath;

        public WzMsFile(Stream baseStream, string originalFileName, string msFilePath, bool leaveOpen = false)
        {
            this.Init(baseStream, originalFileName, msFilePath, leaveOpen);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseStream"></param>
        /// <param name="originalFileName">i.e Mob_00000.ms</param>
        /// <param name="msFilePath"></param>Full path to .ms file</param>
        /// <param name="leaveOpen"></param>
        /// <exception cref="ArgumentNullException"></exception>
        private void Init(Stream baseStream, string originalFileName, string msFilePath, bool leaveOpen)
        {
            if (baseStream == null)
                throw new ArgumentNullException(nameof(baseStream));
            if (originalFileName == null)
                throw new ArgumentNullException(nameof(originalFileName));

            this.BaseStream = baseStream;
            this.leaveOpen = leaveOpen;
            this.msFilePath = msFilePath;
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
            var snowDecoderStream = new CryptoStream(this.BaseStream, snowCipher, CryptoStreamMode.Read);
            var snowReader = new BinaryReader(snowDecoderStream, Encoding.Unicode, true);

            for (int i = 0; i < entryCount; i++)
            {
                int entryNameLen = snowReader.ReadInt32();
                string entryName = new string(snowReader.ReadChars(entryNameLen)); // "Mob/0100000.img"
                int checkSum = snowReader.ReadInt32();
                int flags = snowReader.ReadInt32();
                int startPos = snowReader.ReadInt32();
                int size = snowReader.ReadInt32();
                int sizeAligned = snowReader.ReadInt32();
                int unk1 = snowReader.ReadInt32();
                int unk2 = snowReader.ReadInt32();
                byte[] entryKey = snowReader.ReadBytes(16);

                var entry = new WzMsEntry(entryName, checkSum, flags, startPos, size, sizeAligned, unk1, unk2, entryKey);
                // CalculatedCheckSum is set in constructor or via RecalculateFields, no need to set here
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

        public void Save(string saltStr = null)
        {
            if (!BaseStream.CanWrite)
                throw new InvalidOperationException("Stream not opened for writing");

            Random rng = new Random();

            string fileName = this.originalFileName.ToLower();

            int randByteCount = fileName.Sum(c => (int)c) % 312 + 30;

            byte[] randBytes = new byte[randByteCount];

            rng.NextBytes(randBytes);

            if (string.IsNullOrEmpty(saltStr))
            {
                int saltLenGen = rng.Next(10, 20); // Renamed to avoid shadowing
                char[] saltChars = new char[saltLenGen];
                for (int i = 0; i < saltLenGen; i++)
                {
                    saltChars[i] = (char)rng.Next(32, 127);
                }
                saltStr = new string(saltChars);
            }

            int saltLen = saltStr.Length;

            int hashedSaltLen = saltLen ^ randBytes[0];

            byte[] saltBytes = new byte[saltLen * 2];

            for (int i = 0; i < saltLen; i++)
            {
                saltBytes[i * 2] = (byte)(randBytes[i] ^ (byte)saltStr[i]);
                saltBytes[i * 2 + 1] = (byte)rng.Next(256);
            }

            string fileNameWithSalt = fileName + saltStr;

            byte[] snowCipherKey = new byte[16];

            for (int i = 0; i < 16; i++)
            {
                snowCipherKey[i] = (byte)(fileNameWithSalt[i % fileNameWithSalt.Length] + i);
            }

            byte version = 2;

            int entryCount = this.Entries.Count;

            ReadOnlySpan<byte> saltBytesSpan = saltBytes;

            ReadOnlySpan<ushort> u16SaltBytes = MemoryMarshal.Cast<byte, ushort>(saltBytesSpan);

            int sumSalt = 0;

            for (int i = 0; i < u16SaltBytes.Length; i++)
            {
                sumSalt += u16SaltBytes[i];
            }

            int hash = hashedSaltLen + version + entryCount + sumSalt;

            using (var bWriter = new BinaryWriter(this.BaseStream, Encoding.ASCII, true))
            {
                bWriter.Write(randBytes);
                bWriter.Write(hashedSaltLen);
                bWriter.Write(saltBytes);
            }

            long headerStartPos = this.BaseStream.Position;

            using (var snowCipher = new Snow2CryptoTransform(snowCipherKey, null, true))
            {
                using (var snowEncoderStream = new CryptoStream(this.BaseStream, snowCipher, CryptoStreamMode.Write, true))
                {
                    using (var snowWriter = new BinaryWriter(snowEncoderStream))
                    {
                        snowWriter.Write(hash);
                        snowWriter.Write(version);
                        snowWriter.Write(entryCount);
                    }
                }
            }

            int sumName = fileName.Sum(c => (int)c * 3);

            int padSize = sumName % 212 + 33;

            byte[] pad = new byte[padSize];

            rng.NextBytes(pad);

            this.BaseStream.Write(pad, 0, padSize);

            long entryStartPos = this.BaseStream.Position;

            byte[] snowCipherKey2 = new byte[16];

            for (int i = 0; i < 16; i++)
            {
                int lastIndex = fileNameWithSalt.Length - 1 - i % fileNameWithSalt.Length;

                snowCipherKey2[i] = (byte)(i + (i % 3 + 2) * fileNameWithSalt[lastIndex]);
            }

            // compute for entries

            int currentBlock = 0;

            foreach (var entry in this.Entries)
            {
                if (entry.Data == null)
                    throw new Exception("Data not set for entry");

                entry.RecalculateFields(entry.Flags, currentBlock, entry.Unk1, rng);
                entry.StartPos = currentBlock;
                currentBlock += entry.SizeAligned / 1024;
            }

            using (var snowEntryCipher = new Snow2CryptoTransform(snowCipherKey2, null, true))
            {
                using (var snowEntryStream = new CryptoStream(this.BaseStream, snowEntryCipher, CryptoStreamMode.Write))
                {
                    using (var snowEntryWriter = new BinaryWriter(snowEntryStream, Encoding.Unicode, true))
                    {
                        for (int i = 0; i < entryCount; i++)
                        {
                            var entry = this.Entries[i];
                            int entryNameLen = entry.Name.Length;
                            snowEntryWriter.Write(entryNameLen);
                            snowEntryWriter.Write(entry.Name.ToCharArray());
                            snowEntryWriter.Write(entry.CheckSum);
                            snowEntryWriter.Write(entry.Flags);
                            snowEntryWriter.Write((int)entry.StartPos);
                            snowEntryWriter.Write(entry.Size);
                            snowEntryWriter.Write(entry.SizeAligned);
                            snowEntryWriter.Write(entry.Unk1);
                            snowEntryWriter.Write(entry.Unk2);
                            snowEntryWriter.Write(entry.EntryKey);
                        }
                    }
                }
            }

            long dataStartPos = this.BaseStream.Position;

            if ((dataStartPos & 0x3ff) != 0)
            {
                int padding = (int)(0x400 - (dataStartPos & 0x3ff));
                byte[] padData = new byte[padding];
                rng.NextBytes(padData);
                this.BaseStream.Write(padData, 0, padding);
                dataStartPos += padding;
            }

            // write data

            for (int i = 0; i < entryCount; i++)
            {
                var entry = this.Entries[i];
                long expectedPos = dataStartPos + entry.StartPos * 1024;
                long currentPos = this.BaseStream.Position;
                if (currentPos < expectedPos)
                {
                    int padLen = (int)(expectedPos - currentPos);
                    byte[] fillPad = new byte[padLen];
                    rng.NextBytes(fillPad);
                    this.BaseStream.Write(fillPad, 0, padLen);
                }

                using (var dataCipher = new Snow2CryptoTransform(entry.EntryKey, null, true))
                {
                    using (var dataStream = new CryptoStream(this.BaseStream, dataCipher, CryptoStreamMode.Write))
                    {
                        dataStream.Write(entry.Data, 0, entry.Data.Length);
                    }
                }

                int padBytes = entry.SizeAligned - entry.Size;
                if (padBytes > 0)
                {
                    byte[] dataPad = new byte[padBytes];
                    rng.NextBytes(dataPad);
                    this.BaseStream.Write(dataPad, 0, padBytes);
                }

                // update StartPos to full
                entry.StartPos = dataStartPos + entry.StartPos * 1024;
            }

            this.Header = new WzMsHeader(this.originalFileName, saltStr, fileNameWithSalt, hash, version, entryCount, headerStartPos, entryStartPos)
            {
                DataStartPosition = dataStartPos
            };
        }

        /// <summary>
        /// Loads a .ms file and returns a WzFile containing all WzImages from this .ms file.
        /// </summary>
        /// <param name="msFilePath">Path to the .ms file</param>
        /// <returns>WzFile containing all images from the .ms file</returns>
        public WzFile LoadAsWzFile()
        {
            this.ReadEntries();

            var wzFile = new WzFile(-1, WzMapleVersion.BMS);
            wzFile.Name = Path.GetFileName(msFilePath.Replace(".ms", ".wz"));
            wzFile.path = msFilePath;
            var wzDir = wzFile.WzDirectory;

            foreach (var entry in Entries)
            {
                if (entry.Data == null)
                {
                    BaseStream.Position = entry.StartPos;
                    var buffer = new byte[entry.Size];
                    BaseStream.Read(buffer, 0, entry.Size);
                    entry.Data = buffer;
                }
                var dataStream = new MemoryStream(entry.Data, writable: false);
                var wzImage = new WzImage(Path.GetFileName(entry.Name), dataStream, WzMapleVersion.BMS);
                wzImage.ParseImage();
                wzDir.AddImage(wzImage);
            }
            return wzFile;
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
