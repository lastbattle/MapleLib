using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Microsoft.VisualBasic;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;

namespace MapleLib.WzLib.MSFile
{
    /// <summary>
    /// Represents a MapleStory .ms file handler for reading, writing, and converting .ms files.
    // Credits: Elem8100
    // https://github.com/Elem8100/MapleNecrocer/blob/a1194a96ddf99e5d16225a05dfdf4d616f1fac3f/WzComparerR2.WzLib/Ms_File.cs
    //
    /// <para>
    /// The .ms file format is a custom encrypted archive used to store WZ images. Encryption and decryption are performed using the Snow2 or ChaCha20 stream cipher, with keys derived from the file name and a randomly generated salt.
    /// </para>
    /// <para>
    /// <b>Encryption/Decryption Process:</b>
    /// <list type="number">
    /// <item>Random bytes and a salt are generated and written to the file header. The salt is obfuscated using XOR with the random bytes.</item>
    /// <item>The file name and salt are concatenated to form a base string for key derivation.</item>
    /// <item>Header and entry data are encrypted/decrypted using a format-version-specific stream cipher, with keys derived from the base string and entry-specific data.</item>
    /// <item>Each entry's actual data is encrypted with a unique key stored in the entry metadata.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>How Snow2CryptoTransform is used:</b>
    /// <list type="bullet">
    /// <item>Snow2CryptoTransform implements the Snow2 stream cipher and is used as a .NET ICryptoTransform for streaming encryption/decryption.</item>
    /// <item>Different keys are used for the header, entry table, and each entry's data, making each file and entry uniquely encrypted.</item>
    /// <item>When reading, the file is decrypted in stages using the appropriate keys to reconstruct the original data.</item>
    /// <item>When writing, new random values and keys are generated, and all data is encrypted before being written to the stream.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Features:</b>
    /// <list type="bullet">
    /// <item>Reads and validates .ms file headers and entries using custom cryptography.</item>
    /// <item>Extracts and loads contained images as WzImage objects.</item>
    /// <item>Saves new or modified .ms files with correct structure and encryption.</item>
    /// <item>Implements IDisposable for proper stream management.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// 
    public class WzMsFile : IDisposable
    {
        private readonly string originalFileName;
        private readonly string msFilePath;
        private static readonly byte[] ChaCha20KeyObscure =
        [
            0x7B, 0x2F, 0x35, 0x48, 0x43, 0x95, 0x02, 0xB9,
            0xAE, 0x91, 0xA6, 0xE1, 0xD8, 0xD6, 0x24, 0xB4,
            0x33, 0x10, 0x1D, 0x3D, 0xC1, 0xBB, 0xC6, 0xF4,
            0xA5, 0xFE, 0xB3, 0x69, 0x6B, 0x56, 0xE4, 0x75
        ];

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseStream"></param>
        /// <param name="originalFileName">i.e Mob_00000.ms</param>
        /// <param name="msFilePath">Full path to .ms file</param>
        /// <param name="leaveOpen"></param>
        /// <param name="isSavingFile">Set to true if creating a new .ms file for saving</param>
        /// <exception cref="ArgumentNullException"></exception>
        public WzMsFile(Stream baseStream, string originalFileName, string msFilePath, bool leaveOpen = false, bool isSavingFile = false)
        {
            ArgumentNullException.ThrowIfNull(baseStream);
            ArgumentNullException.ThrowIfNull(originalFileName);
            if (string.IsNullOrWhiteSpace(msFilePath))
                throw new ArgumentException("Path cannot be null or empty.", nameof(msFilePath));

            this.BaseStream = baseStream;
            this.leaveOpen = leaveOpen;
            this.msFilePath = msFilePath;
            this.originalFileName = originalFileName;
            this.Entries = [];

            if (!isSavingFile)
                this.ReadHeader(originalFileName);
        }

        public Stream BaseStream { get; private set; }
        public WzMsHeader Header { get; private set; }
        public List<WzMsEntry> Entries { get; private set; }
        private bool leaveOpen;

        #region Common
        /// <summary>
        /// Derives the Snow2 encryption key from the file name and salt.
        /// </summary>
        /// <param name="fileNameWithSalt">The concatenated file name and salt.</param>
        /// <param name="isEntryKey">If true, uses the alternate derivation formula for entry encryption.</param>
        private static void DeriveSnowKey(ReadOnlySpan<char> fileNameWithSalt, Span<byte> key, bool isEntryKey = false)
        {
            if (!isEntryKey)
            {
                // Header key: char + index
                for (int i = 0; i < key.Length; i++)
                {
                    key[i] = (byte)(fileNameWithSalt[i % fileNameWithSalt.Length] + i);
                }
            }
            else
            {
                // Entry key: index + multiplier * reversed char
                for (int i = 0; i < key.Length; i++)
                {
                    key[i] = (byte)(i + (i % 3 + 2) * fileNameWithSalt[fileNameWithSalt.Length - 1 - i % fileNameWithSalt.Length]);
                }
            }
        }

        private static void DeriveChaCha20Key(ReadOnlySpan<char> fileNameWithSalt, Span<byte> key, bool isEntryKey)
        {
            if (!isEntryKey)
            {
                for (int i = 0; i < key.Length; i++)
                {
                    key[i] = (byte)(fileNameWithSalt[i % fileNameWithSalt.Length] + i);
                    key[i] ^= ChaCha20KeyObscure[i];
                }
            }
            else
            {
                for (int i = 0; i < key.Length; i++)
                {
                    key[i] = (byte)(i + (i % 3 + 2) * fileNameWithSalt[fileNameWithSalt.Length - 1 - i % fileNameWithSalt.Length]);
                    key[i] ^= ChaCha20KeyObscure[i];
                }
            }
        }
        #endregion

        #region Read file
        /// <summary>
        /// Reads and parses the .ms file header from the provided stream.
        /// </summary>
        /// <param name="fullFileName"></param>
        /// <exception cref="Exception"></exception>
        private void ReadHeader(string fullFileName)
        {
            Exception version2Exception;
            try
            {
                ReadHeaderVersion2(fullFileName);
                return;
            }
            catch (Exception ex)
            {
                version2Exception = ex;
            }

            try
            {
                ReadHeaderVersion4(fullFileName);
            }
            catch (Exception version4Exception)
            {
                throw new InvalidDataException(
                    $"Unable to read MS file header as version {WzMsConstants.Version2} or {WzMsConstants.Version4}. " +
                    $"Version {WzMsConstants.Version2}: {version2Exception.Message}; " +
                    $"Version {WzMsConstants.Version4}: {version4Exception.Message}",
                    version4Exception);
            }
        }

        private void ReadHeaderVersion2(string fullFileName)
        {
            string fileName = Path.GetFileName(fullFileName).ToLower();
            this.BaseStream.Position = 0;
            using var bReader = new BinaryReader(this.BaseStream, Encoding.ASCII, true);

            int fileNameCharSum = SumChars(fileName);
            int randByteCount = fileNameCharSum % WzMsConstants.RandByteMod + WzMsConstants.RandByteOffset;
            byte[] randBytes = bReader.ReadBytes(randByteCount);

            int hashedSaltLen = bReader.ReadInt32();
            int saltLen = (byte)hashedSaltLen ^ randBytes[0];
            byte[] saltBytes = bReader.ReadBytes(saltLen * 2);
            char[] saltChars = new char[saltLen];
            for (int i = 0; i < saltLen; i++)
            {
                saltChars[i] = (char)(randBytes[i] ^ saltBytes[i * 2]);
            }
            string saltStr = new(saltChars);

            string fileNameWithSalt = fileName + saltStr;
            Span<byte> snowCipherKey = stackalloc byte[WzMsConstants.SnowKeyLength];
            DeriveSnowKey(fileNameWithSalt, snowCipherKey, false);

            long headerStartPos = this.BaseStream.Position;
            using var snowCipher = new Snow2CryptoTransform(snowCipherKey, null, false);
            using var snowDecoderStream = new CryptoStream(this.BaseStream, snowCipher, CryptoStreamMode.Read, true);
            using (var snowReader = new BinaryReader(snowDecoderStream))
            {
                //byte[] last4 = snowReader.ReadBytes(16);
                //Debug.WriteLine($"Last 16 bytes: {BitConverter.ToString(last4)}");

                var (entryCount, headerHash) = ReadAndValidateHeader(snowReader, hashedSaltLen, saltBytes, saltLen);
                int padAmount = (fileNameCharSum * 3) % WzMsConstants.HeaderPadMod + WzMsConstants.HeaderPadOffset;
                long entryStartPos = headerStartPos + 9 + padAmount;

                var header = new WzMsHeader(fullFileName, saltStr, fileNameWithSalt, headerHash, WzMsConstants.Version2, entryCount, headerStartPos, entryStartPos);
                this.Header = header;
            }
        }

        private void ReadHeaderVersion4(string fullFileName)
        {
            string fileName = Path.GetFileName(fullFileName).ToLower();
            this.BaseStream.Position = 0;
            using var bReader = new BinaryReader(this.BaseStream, Encoding.ASCII, true);

            int fileNameCharSum = SumChars(fileName);
            int randByteCount = fileNameCharSum % WzMsConstants.RandByteMod + WzMsConstants.RandByteOffset;
            byte[] randBytes = bReader.ReadBytes(randByteCount);
            if (randBytes.Length != randByteCount)
                throw new EndOfStreamException();

            for (int i = 0; i < randBytes.Length; i++)
            {
                randBytes[i] = (byte)((sbyte)randBytes[i] >> 1);
            }

            byte version = (byte)(bReader.ReadByte() ^ randBytes[0]);
            if (version != WzMsConstants.Version4)
                throw new Exception($"Unsupported version: expected {WzMsConstants.Version4}, got {version}");

            int hashedSaltLen = bReader.ReadInt32();
            int saltLen = ((byte)hashedSaltLen) ^ randBytes[0];
            if (saltLen <= 0 || saltLen > randBytes.Length)
                throw new InvalidDataException($"Invalid version {WzMsConstants.Version4} salt length: {saltLen}");

            byte[] saltBytes = bReader.ReadBytes(saltLen * 2);
            if (saltBytes.Length != saltLen * 2)
                throw new EndOfStreamException();

            char[] saltChars = new char[saltLen];
            for (int i = 0; i < saltLen; i++)
            {
                int a = randBytes[i] ^ saltBytes[i * 2];
                int b = ((a | 0x4B) << 1) - a - 75;
                saltChars[i] = (char)b;
            }

            string saltStr = new(saltChars);
            string fileNameWithSalt = fileName + saltStr;

            Span<byte> chacha20Key = stackalloc byte[WzMsConstants.ChaCha20KeyLength];
            DeriveChaCha20Key(fileNameWithSalt, chacha20Key, false);

            long headerStartPos = this.BaseStream.Position;
            Span<byte> headerBytes = stackalloc byte[8];
            this.BaseStream.ReadExactly(headerBytes);
            Span<byte> emptyNonce = stackalloc byte[WzMsConstants.ChaCha20NonceLength];
            using (var chacha20 = new ChaCha20CryptoTransform(chacha20Key, emptyNonce, 0))
            {
                chacha20.TransformInPlace(headerBytes);
            }

            int headerHash = BinaryPrimitives.ReadInt32LittleEndian(headerBytes[..4]);
            int entryCount = BinaryPrimitives.ReadInt32LittleEndian(headerBytes[4..]);
            if (entryCount < 0 || entryCount > this.BaseStream.Length / 32)
                throw new InvalidDataException($"Invalid version {WzMsConstants.Version4} entry count: {entryCount}");

            int padAmount = (fileNameCharSum * 3) % WzMsConstants.HeaderPadMod + 64;
            long entryStartPos = headerStartPos + 8 + padAmount;
            if (entryStartPos >= this.BaseStream.Length)
                throw new InvalidDataException($"Invalid version {WzMsConstants.Version4} entry start position: {entryStartPos}");

            var header = new WzMsHeader(fullFileName, saltStr, fileNameWithSalt, headerHash, WzMsConstants.Version4, entryCount, headerStartPos, entryStartPos);
            this.Header = header;
        }

        /// <summary>
        /// Reads header fields and validates version/hash. Returns the expected hash for header init.
        /// </summary>
        /// <param name="snowReader">The reader for encrypted header.</param>
        /// <param name="hashedSaltLen">The hashed salt length.</param>
        /// <param name="saltBytes">Raw salt bytes for hash calc.</param>
        /// <param name="saltLen">Actual salt length.</param>
        /// <returns>A tuple containing (EntryCount, CalculatedHash).</returns>
        /// <exception cref="Exception">Thrown on version or hash mismatch.</exception>
        private static (int EntryCount, int CalculatedHash) ReadAndValidateHeader(BinaryReader snowReader, int hashedSaltLen, byte[] saltBytes, int saltLen)
        {
            int hash = snowReader.ReadInt32();
            byte version = snowReader.ReadByte();
            int entryCount = snowReader.ReadInt32();

            if (version != WzMsConstants.Version2)
                throw new Exception($"Unsupported version: expected {WzMsConstants.Version2}, got {version}");

            int actualHash = hashedSaltLen + version + entryCount;
            ReadOnlySpan<ushort> u16SaltBytes = MemoryMarshal.Cast<byte, ushort>(saltBytes.AsSpan(0, saltLen * 2));
            for (int i = 0; i < u16SaltBytes.Length; i++)
            {
                actualHash += u16SaltBytes[i];
            }

            if (hash != actualHash)
                throw new Exception($"Header hash mismatch: expected {actualHash}, got {hash}");

            return (entryCount, actualHash);
        }

        /// <summary>
        /// Reads all entry metadata from the underlying stream and populates the Entries collection based on the
        /// current header information.
        /// </summary>
        /// <remarks>This method clears the existing Entries collection and reloads it from the stream if
        /// the header indicates that entries are present and have not already been loaded. The method updates each
        /// entry's start position relative to the data section. If the header is null, contains no entries, or the
        /// entries are already loaded, the method performs no action.</remarks>
        public void ReadEntries()
        {
            if (this.Header == null || this.Header.EntryCount == 0 || this.Header.EntryCount == this.Entries.Count)
                return;
            this.Entries.Clear();

            if (this.Header.Version == WzMsConstants.Version4)
            {
                ReadEntriesVersion4();
                return;
            }

            ReadEntriesVersion2();
        }

        private void ReadEntriesVersion2()
        {
            int entryCount = this.Header.EntryCount;
            if (this.Entries.Capacity < entryCount)
                this.Entries.Capacity = entryCount;

            string fileNameWithSalt = this.Header.FileNameWithSalt;
            Span<byte> snowKey = stackalloc byte[WzMsConstants.SnowKeyLength];
            DeriveSnowKey(fileNameWithSalt, snowKey, true);

            using var snowCipher = new Snow2CryptoTransform(snowKey, null, false);
            this.BaseStream.Position = this.Header.EntryStartPosition;
            var snowDecoderStream = new CryptoStream(this.BaseStream, snowCipher, CryptoStreamMode.Read);
            var snowReader = new BinaryReader(snowDecoderStream, Encoding.Unicode, true);

            for (int i = 0; i < entryCount; i++)
            {
                int entryNameLen = snowReader.ReadInt32();
                char[] entryNameBuffer = ArrayPool<char>.Shared.Rent(entryNameLen);
                string entryName;
                try
                {
                    ReadCharsExactly(snowReader, entryNameBuffer, entryNameLen);
                    entryName = new string(entryNameBuffer, 0, entryNameLen); // "Mob/0100000.img"
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(entryNameBuffer);
                }

                int checkSum = snowReader.ReadInt32();
                int flags = snowReader.ReadInt32();
                int startPos = snowReader.ReadInt32();
                int size = snowReader.ReadInt32();
                int sizeAligned = snowReader.ReadInt32();
                int unk1 = snowReader.ReadInt32();
                int unk2 = snowReader.ReadInt32();
                byte[] entryKey = snowReader.ReadBytes(WzMsConstants.SnowKeyLength);

                var entry = new WzMsEntry(entryName, checkSum, flags, startPos, size, sizeAligned, unk1, unk2, entryKey);
                // CalculatedCheckSum is set in constructor or via RecalculateFields, no need to set here
                this.Entries.Add(entry);
            }

            long dataStartPos = this.BaseStream.Position;
            dataStartPos = AlignToPage(dataStartPos); // Extracted helper
            this.Header.DataStartPosition = dataStartPos; // skip the random padding after entries

            foreach (var entry in this.Entries)
            {
                entry.StartPos = dataStartPos + entry.StartPos * WzMsConstants.BlockAlignment;
            }
        }

        private void ReadEntriesVersion4()
        {
            int entryCount = this.Header.EntryCount;
            if (this.Entries.Capacity < entryCount)
                this.Entries.Capacity = entryCount;

            string fileNameWithSalt = this.Header.FileNameWithSalt;
            Span<byte> chacha20Key = stackalloc byte[WzMsConstants.ChaCha20KeyLength];
            DeriveChaCha20Key(fileNameWithSalt, chacha20Key, true);

            Span<byte> emptyNonce = stackalloc byte[WzMsConstants.ChaCha20NonceLength];
            this.BaseStream.Position = this.Header.EntryStartPosition;
            using var chacha20Reader = new ChaCha20Reader(this.BaseStream, chacha20Key, emptyNonce, true);

            for (int i = 0; i < entryCount; i++)
            {
                string entryName = chacha20Reader.ReadString();
                int checkSum = chacha20Reader.ReadInt32();
                int flags = chacha20Reader.ReadInt32();
                int startPos = chacha20Reader.ReadInt32();
                int size = chacha20Reader.ReadInt32();
                int sizeAligned = chacha20Reader.ReadInt32();
                int unk1 = chacha20Reader.ReadInt32();
                int unk2 = chacha20Reader.ReadInt32();
                byte[] entryKey = chacha20Reader.ReadBytes(WzMsConstants.SnowKeyLength);
                int unk3 = chacha20Reader.ReadInt32();
                int unk4 = chacha20Reader.ReadInt32();

                var entry = new WzMsEntry(entryName, checkSum, flags, startPos, size, sizeAligned, unk1, unk2, entryKey, unk3, unk4);
                this.Entries.Add(entry);
            }

            long dataStartPos = AlignToPage(this.BaseStream.Position);
            this.Header.DataStartPosition = dataStartPos;

            foreach (var entry in this.Entries)
            {
                entry.StartPos = dataStartPos + entry.StartPos * WzMsConstants.BlockAlignment;
            }
        }

        /// <summary>
        /// Aligns position to the next 1024-byte page boundary.
        /// </summary>
        /// <param name="pos">Current position.</param>
        /// <returns>Aligned position.</returns>
        private static long AlignToPage(long pos) => (pos + WzMsConstants.PageAlignmentMask) & ~WzMsConstants.PageAlignmentMask;

        private static int SumChars(ReadOnlySpan<char> chars)
        {
            int sum = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                sum += chars[i];
            }

            return sum;
        }

        private static int SumUInt16Bytes(ReadOnlySpan<byte> bytes)
        {
            int sum = 0;
            ReadOnlySpan<ushort> values = MemoryMarshal.Cast<byte, ushort>(bytes);
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }

            return sum;
        }

        private static void ReadCharsExactly(BinaryReader reader, char[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = reader.Read(buffer, offset, count - offset);
                if (read == 0)
                    throw new EndOfStreamException();

                offset += read;
            }
        }

        private sealed class ChaCha20Reader : IDisposable
        {
            private readonly Stream baseStream;
            private readonly ChaCha20CryptoTransform chacha20Cipher;
            private readonly bool leaveOpen;
            private byte[] buffer;
            private int readOffset;

            public ChaCha20Reader(Stream baseStream, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, bool leaveOpen = false)
            {
                this.baseStream = baseStream;
                this.leaveOpen = leaveOpen;
                this.chacha20Cipher = new ChaCha20CryptoTransform(key, nonce, 0);
                this.buffer = new byte[WzMsConstants.ChaCha20BlockSize];
                this.readOffset = this.buffer.Length;
            }

            public byte[] ReadBytes(int count)
            {
                byte[] result = new byte[count];
                ReadBytes(result);
                return result;
            }

            public void ReadBytes(Span<byte> destination)
            {
                while (destination.Length > 0)
                {
                    if (readOffset >= buffer.Length)
                    {
                        baseStream.ReadExactly(buffer);
                        chacha20Cipher.TransformInPlace(buffer);
                        readOffset = 0;
                    }

                    int readCount = Math.Min(destination.Length, buffer.Length - readOffset);
                    buffer.AsSpan(readOffset, readCount).CopyTo(destination);
                    destination = destination[readCount..];
                    readOffset += readCount;
                }

                if (readOffset >= buffer.Length)
                    chacha20Cipher.State[12] = 0;
            }

            public int ReadInt32()
            {
                Span<byte> value = stackalloc byte[4];
                ReadBytes(value);
                return BinaryPrimitives.ReadInt32LittleEndian(value);
            }

            public string ReadString()
            {
                int length = ReadInt32();
                if (length < 0)
                    throw new InvalidDataException($"Invalid version {WzMsConstants.Version4} entry name length: {length}");

                char[] rented = ArrayPool<char>.Shared.Rent(length);
                try
                {
                    Span<char> chars = rented.AsSpan(0, length);
                    ReadBytes(MemoryMarshal.AsBytes(chars));
                    return new string(rented, 0, length);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }

            public void Dispose()
            {
                chacha20Cipher.Dispose();
                Array.Clear(buffer);
                buffer = [];

                if (!leaveOpen)
                    baseStream.Dispose();
            }
        }

        /// <summary>
        /// Loads a .ms file and returns a WzFile containing all WzImages from this .ms file.
        /// </summary>
        /// <param name="msFilePath">Path to the .ms file</param>
        /// <returns>WzFile containing all images from the .ms file</returns>
        public WzFile LoadAsWzFile()
        {
            this.ReadEntries();

            WzMapleVersion mapleVersion = WzMapleVersion.BMS; // .ms files are always from BMS

            var wzFile = new WzFile(0, mapleVersion)
            {
                Name = Path.GetFileName(msFilePath.Replace(".ms", ".wz")),
                path = msFilePath
            }; // version is always 0 as a placeholder for .ms files
            var wzDir = wzFile.WzDirectory;

            wzDir.wzFile = wzFile;

            foreach (WzMsEntry entry in Entries)
            {
                byte[] decryptedData = DecryptDataToArray(entry);
                Stream decrypted = new MemoryStream(decryptedData, writable: false); // dont close this stream!
                WzBinaryReader reader = new(decrypted, WzTool.GetIvByMapleVersion(mapleVersion));

                int lastSlashIndex = entry.Name.LastIndexOf('/'); // Mob/0100000.img
                string entryImgName = (lastSlashIndex >= 0) ? entry.Name.Substring(lastSlashIndex + 1) : entry.Name;

                var wzImage = new WzImage(entryImgName, reader);
                wzImage.ParseImage();
                //wzImage.Changed = true;
                wzDir.AddImage(wzImage);
            }
            return wzFile;
        }
        #endregion

        #region Write file
        /// <summary>
        /// Initializes the header and entry list for the current object based on the specified WzFile and
        /// initialization vector.
        /// </summary>
        /// <remarks>This method generates a unique salt and constructs a header using the original file
        /// name and the generated salt. The Entries collection is populated with new entries for each image in the
        /// WzFile. Existing entries and header data will be replaced.</remarks>
        /// <param name="wzFile">The WzFile instance containing the directory and images to be processed into entries.</param>
        /// <param name="iv">The initialization vector used for encrypting image data when creating entries. Must not be null.</param>
        private void CreateHeader(WzFile wzFile, byte[] iv)
        {
            Random rng = new();

            this.Entries = [];

            foreach (var wzImage in wzFile.WzDirectory.WzImages)
            {
                using var ms = new MemoryStream();
                using var writer = new WzBinaryWriter(ms, iv);
                wzImage.SaveImage(writer, forceReadFromData:true);
                byte[] data = ms.ToArray();

                string category = Path.GetFileNameWithoutExtension(wzFile.Name);
                string entryName = category + "/" + wzImage.Name;

                byte[] entryKey = new byte[16];
                rng.NextBytes(entryKey);

                var entry = new WzMsEntry(entryName, 0, 0, 0, 0, 0, 0, 0, entryKey); // will be recalculated RecalculateFields()
                entry.Data = data;
                this.Entries.Add(entry);
            }

            string fileName_ = Path.GetFileName(this.originalFileName).ToLower();
            string saltStr = GenerateSalt(WzMsConstants.SaltMinLength, WzMsConstants.SaltMaxLength);
            string fileNameWithSalt = fileName_ + saltStr;
            Header = new WzMsHeader(originalFileName, saltStr, fileNameWithSalt, 0, WzMsConstants.SupportedVersion, Entries.Count, 0, 0);

            this.Header = new WzMsHeader(this.originalFileName, saltStr, fileNameWithSalt, 0, 2, this.Entries.Count, 0, 0);
        }

        /// <summary>
        /// Generates a random salt string of printable ASCII chars.
        /// </summary>
        /// <param name="minLen">Minimum length.</param>
        /// <param name="maxLen">Maximum length.</param>
        /// <returns>Salt string.</returns>
        private static string GenerateSalt(int minLen, int maxLen)
        {
            int saltLen = RandomNumberGenerator.GetInt32(minLen, maxLen + 1);
            var saltChars = new char[saltLen];
            byte[] bytes = new byte[saltLen];

            using var rng = RandomNumberGenerator.Create(); // Secure RNG
            rng.GetBytes(bytes);

            for (int i = 0; i < saltLen; i++)
            {
                saltChars[i] = (char)(WzMsConstants.AsciiPrintableMin + (bytes[i] % (WzMsConstants.AsciiPrintableMax - WzMsConstants.AsciiPrintableMin + 1)));
            }
            return new string(saltChars);
        }

        /// <summary>
        /// Saves the current WzMsFile data to the underlying stream in the BMS format, encrypting entries and updating
        /// header and entry metadata as required.
        /// </summary>
        /// <remarks>The method encrypts both the header and entry data using keys derived from the file
        /// name and salt. After saving, the returned stream is reset to the beginning and ready for reading or writing.
        /// The method does not close or dispose the returned stream; callers are responsible for managing its
        /// lifetime.</remarks>
        /// <param name="wzFile">The WzFile instance containing the data and entries to be saved. Must not be null.</param>
        /// <returns>A Stream positioned at the beginning, containing the saved and encrypted BMS file data.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the Header property is already present, indicating that this instance has already been used for
        /// saving. Create a new WzMsFile instance to perform another save operation.</exception>
        public Stream Save(WzFile wzFile)
        {
            byte[] iv = WzTool.GetIvByMapleVersion(WzMapleVersion.BMS);
            
            if (this.Header != null)
                throw new InvalidOperationException("Cannot save when Header is already present. Create a new WzMsFile instance for saving.");

            CreateHeader(wzFile, iv);

            if (this.Entries == null || this.Entries.Count == 0 || this.Header == null)
                return this.BaseStream;

            // Encrypt data and update entry fields
            var encryptedDatas = new List<byte[]>(this.Entries.Count);
            long currentBlockIndex = 0;
            foreach (var entry in this.Entries)
            {
                if (entry.Data == null)
                    continue;

                byte[] encrypted = this.EncryptData(entry, entry.Data); // Encrypt each WzImage
                int size = encrypted.Length;
                int sizeAligned = (size + WzMsConstants.PageAlignmentMask) & ~WzMsConstants.PageAlignmentMask;
                entry.Size = size;
                entry.SizeAligned = sizeAligned;
                entry.StartPos = currentBlockIndex;
                currentBlockIndex += sizeAligned / 1024;
                encryptedDatas.Add(encrypted);

                // Recalculate checksum using RecalculateFields
                entry.RecalculateFields(entry.Flags, (int)entry.StartPos, entry.Unk1);
            }

            // Prepare for writing
            this.BaseStream.Position = 0;
            this.BaseStream.SetLength(0);
            using var bWriter = new BinaryWriter(this.BaseStream, Encoding.ASCII, true);

            string fileName = Path.GetFileName(this.originalFileName).ToLower();

            Random rng = new();
            int fileNameCharSum = SumChars(fileName);
            int randByteCount = fileNameCharSum % WzMsConstants.RandByteMod + WzMsConstants.RandByteOffset;
            byte[] randBytes = new byte[randByteCount];
            for (int i = 0; i < randByteCount; i++)
            {
                randBytes[i] = (byte)rng.Next();
            }

            string saltStr = this.Header.Salt;
            int saltLen = saltStr.Length;
            byte xorVal = randBytes[0];
            byte lowByte = (byte)(saltLen ^ xorVal);
            int hashedSaltLen = lowByte;

            byte[] saltBytes = new byte[saltLen * 2];
            for (int i = 0; i < saltLen; i++)
            {
                saltBytes[i * 2] = (byte)(randBytes[i] ^ (byte)saltStr[i]);
                saltBytes[i * 2 + 1] = 0;
            }

            int sumSalt = SumUInt16Bytes(saltBytes);

            byte version = this.Header.Version;
            int entryCount = this.Entries.Count;
            int hash = hashedSaltLen + version + entryCount + sumSalt;

            string fileNameWithSalt = this.Header.FileNameWithSalt;

            // Write unencrypted prefix
            bWriter.Write(randBytes);
            bWriter.Write(hashedSaltLen);
            bWriter.Write(saltBytes);

            long headerStartPos = this.BaseStream.Position;

            // Header encryption key
            Span<byte> snowKey = stackalloc byte[WzMsConstants.SnowKeyLength];
            DeriveSnowKey(fileNameWithSalt, snowKey, false);

            // Write encrypted header
            using (var snowCipher = new Snow2CryptoTransform(snowKey, null, true))
            {
                using (var snowEncoderStream = new CryptoStream(this.BaseStream, snowCipher, CryptoStreamMode.Write, true))
                {
                    using (var snowWriter = new BinaryWriter(snowEncoderStream))
                    {
                        snowWriter.Write((int)hash);
                        snowWriter.Write((byte)version);
                        snowWriter.Write((int)entryCount); // TODO: verify if its reading and writing back in the same format

                        snowWriter.Flush();
                        snowEncoderStream.FlushFinalBlock();
                    }
                }
            }

            // Padding after header
            int padAmount = (fileNameCharSum * 3) % WzMsConstants.HeaderPadMod + WzMsConstants.HeaderPadOffset;
            byte[] padHeader = new byte[padAmount];
            rng.NextBytes(padHeader); // randomize the pad header

            bWriter.Write(padHeader);

            // Entries encryption key
            Span<byte> snowCipherKey2 = stackalloc byte[WzMsConstants.SnowKeyLength];
            DeriveSnowKey(fileNameWithSalt, snowCipherKey2, true);

            // Write encrypted entries
            using (var snowCipher2 = new Snow2CryptoTransform(snowCipherKey2, null, true))
            {
                using (var snowEncoderStream2 = new CryptoStream(this.BaseStream, snowCipher2, CryptoStreamMode.Write, true))
                {
                    using (var snowWriter2 = new BinaryWriter(snowEncoderStream2, Encoding.Unicode, true))
                    {
                        foreach (var entry in this.Entries)
                        {
                            int entryNameLen = entry.Name.Length;
                            snowWriter2.Write(entryNameLen);
                            snowWriter2.Write(entry.Name.ToCharArray());
                            snowWriter2.Write(entry.CheckSum);
                            snowWriter2.Write(entry.Flags);
                            snowWriter2.Write((int)entry.StartPos);
                            snowWriter2.Write(entry.Size);
                            snowWriter2.Write(entry.SizeAligned);
                            snowWriter2.Write(entry.Unk1);
                            snowWriter2.Write(entry.Unk2);
                            snowWriter2.Write(entry.EntryKey);
                        }
                    }
                }
            }

            // Align data start
            long dataStartPos = this.BaseStream.Position;
            if ((dataStartPos & 0x3ff) != 0)
            {
                long pad = 0x400 - (dataStartPos & 0x3ff);
                byte[] padBytes = new byte[pad];
                this.BaseStream.Write(padBytes);
                dataStartPos += pad;
            }

            // Write encrypted data blocks
            long maxPos = dataStartPos;
            for (int i = 0; i < this.Entries.Count; i++)
            {
                var entry = this.Entries[i];
                if (entry.Data == null)
                    continue;

                byte[] encrypted = encryptedDatas[i];
                long blockStart = dataStartPos + entry.StartPos * 1024;
                if (blockStart > this.BaseStream.Position)
                    this.BaseStream.Position = blockStart;
                this.BaseStream.Write(encrypted);

                int padSize = entry.SizeAligned - entry.Size;
                if (padSize > 0)
                {
                    byte[] padData = new byte[padSize];
                    this.BaseStream.Write(padData);
                }

                long blockEnd = blockStart + entry.SizeAligned;
                if (blockEnd > maxPos) maxPos = blockEnd;
            }

            this.BaseStream.SetLength(maxPos);
            this.BaseStream.Position = 0;

            // Update header positions
            this.Header.UpdateHeader(hash, entryCount, headerStartPos, headerStartPos + 9 + padAmount, dataStartPos);

            return this.BaseStream;
        }
        #endregion

        #region Entry Encryption and Decryption
        /// <summary>
        /// Derives the per-entry image key from salt, name, and entry key.
        /// </summary>
        /// <returns>16-byte imgKey.</returns>
        private void DeriveImgKey(WzMsEntry entry, Span<byte> imgKey)
        {
            uint keyHash = WzMsConstants.InitialKeyHash;
            foreach (char c in Header.Salt)
            {
                keyHash = (keyHash ^ c) * WzMsConstants.KeyHashMultiplier;
            }

            Span<char> keyHashChars = stackalloc char[10];
            keyHash.TryFormat(keyHashChars, out int keyHashLength);

            ReadOnlySpan<char> entryNameSpan = entry.Name.AsSpan();
            ReadOnlySpan<byte> entryKeySpan = entry.EntryKey;
            for (int i = 0; i < imgKey.Length; i++)
            {
                int digit = keyHashChars[i % keyHashLength] - '0';
                int nextDigit = keyHashChars[(i + 1) % keyHashLength] - '0';
                int entryKeyDigit = keyHashChars[(i + 2) % keyHashLength] - '0';
                int entryKeyIdx = (entryKeyDigit + i) % entryKeySpan.Length;
                imgKey[i] = (byte)(i + entryNameSpan[i % entryNameSpan.Length] * (
                    (digit % 2) + entryKeySpan[entryKeyIdx] + ((nextDigit + i) % 5)
                ));
            }
        }

        private void DeriveChaCha20ImgKey(WzMsEntry entry, Span<byte> imgKey, Span<byte> nonce, out uint counter)
        {
            uint keyHash = WzMsConstants.InitialKeyHash;
            foreach (char c in Header.Salt)
            {
                keyHash = (keyHash ^ c) * WzMsConstants.KeyHashMultiplier;
            }

            Span<char> keyHashChars = stackalloc char[10];
            keyHash.TryFormat(keyHashChars, out int keyHashLength);

            ReadOnlySpan<char> entryNameSpan = entry.Name.AsSpan();
            ReadOnlySpan<byte> entryKeySpan = entry.EntryKey;
            for (int i = 0; i < imgKey.Length; i++)
            {
                int digit = keyHashChars[i % keyHashLength] - '0';
                int nextDigit = keyHashChars[(i + 1) % keyHashLength] - '0';
                int entryKeyDigit = keyHashChars[(i + 2) % keyHashLength] - '0';
                int entryKeyIdx = (entryKeyDigit + i) % entryKeySpan.Length;
                imgKey[i] = (byte)(i + entryNameSpan[i % entryNameSpan.Length] * (
                    (digit % 2) + entryKeySpan[entryKeyIdx] + ((nextDigit + i) % 5)
                ));
                imgKey[i] ^= ChaCha20KeyObscure[i];
            }

            uint keyHash2 = keyHash >> 1;
            uint keyHash3 = keyHash2 ^ 0x6C;
            Span<byte> keyHashData = stackalloc byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(keyHashData[..4], keyHash);
            BinaryPrimitives.WriteUInt32LittleEndian(keyHashData.Slice(4, 4), keyHash2);
            BinaryPrimitives.WriteUInt32LittleEndian(keyHashData.Slice(8, 4), keyHash3);

            for (int i = 0, a = 0, b = 0, c = 90, d = 0; i < keyHashData.Length; i++)
            {
                keyHashData[i] ^= (byte)(d + 11 * (i / 11) + (c ^ (i >> 2)) + (a ^ b));
                --d;
                a += 8;
                b += 17;
                c += 43;
            }

            nonce.Clear();
            keyHashData[..8].CopyTo(nonce[4..]);
            counter = BinaryPrimitives.ReadUInt32LittleEndian(keyHashData[8..]);
        }

        /// <summary>
        /// Decrypts this entry's data from the given stream using the provided salt string.
        /// </summary>
        /// <param name="entryData">The entry data of .img.</param>
        /// <returns>Decrypted data as a byte array.</returns>
        private Stream DecryptData(WzMsEntry entry)
        {
            return new MemoryStream(DecryptDataToArray(entry), writable: false);
        }

        private byte[] DecryptDataToArray(WzMsEntry entry)
        {
            if (this.Header.Version == WzMsConstants.Version4)
                return DecryptDataToArrayVersion4(entry);

            Span<byte> imgKey = stackalloc byte[WzMsConstants.SnowKeyLength];
            DeriveImgKey(entry, imgKey);

            byte[] buffer = new byte[entry.Size];
            if (entry.Data != null)
            {
                Buffer.BlockCopy(entry.Data, 0, buffer, 0, entry.Size);
            }
            else
            {
                this.BaseStream.Position = entry.StartPos;
                this.BaseStream.ReadExactly(buffer);
            }

            using (var snowCipher = new Snow2CryptoTransform(imgKey, null, false))
            {
                snowCipher.TransformInPlace(buffer);
            }

            int dataLen = Math.Min(buffer.Length, WzMsConstants.DoubleEncryptInitialBytes);
            if (dataLen > 0)
            {
                using var snowCipher = new Snow2CryptoTransform(imgKey, null, false);
                snowCipher.TransformInPlace(buffer.AsSpan(0, dataLen));
            }

            return buffer;
        }

        private byte[] DecryptDataToArrayVersion4(WzMsEntry entry)
        {
            Span<byte> imgKey = stackalloc byte[WzMsConstants.ChaCha20KeyLength];
            Span<byte> nonce = stackalloc byte[WzMsConstants.ChaCha20NonceLength];
            DeriveChaCha20ImgKey(entry, imgKey, nonce, out uint counter);

            byte[] buffer = new byte[entry.Size];
            if (entry.Data != null)
            {
                Buffer.BlockCopy(entry.Data, 0, buffer, 0, entry.Size);
            }
            else
            {
                this.BaseStream.Position = entry.StartPos;
                this.BaseStream.ReadExactly(buffer);
            }

            int dataLen = Math.Min(buffer.Length, WzMsConstants.DoubleEncryptInitialBytes);
            if (dataLen > 0)
            {
                using var chacha20 = new ChaCha20CryptoTransform(imgKey, nonce, counter);
                chacha20.TransformInPlace(buffer.AsSpan(0, dataLen));
            }

            return buffer;
        }

        /// <summary>
        /// Encrypts the plainData into entry's data
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="plainData"></param>
        /// <returns></returns>
        private byte[] EncryptData(WzMsEntry entry, byte[] plainData)
        {
            Span<byte> imgKey = stackalloc byte[WzMsConstants.SnowKeyLength];
            DeriveImgKey(entry, imgKey);
            byte[] encrypted = plainData.ToArray();

            int dataLen = Math.Min(encrypted.Length, WzMsConstants.DoubleEncryptInitialBytes);
            if (dataLen > 0)
            {
                using var snowCipher = new Snow2CryptoTransform(imgKey, null, true);
                snowCipher.TransformInPlace(encrypted.AsSpan(0, dataLen));
            }

            using (var snowCipher = new Snow2CryptoTransform(imgKey, null, true))
            {
                snowCipher.TransformInPlace(encrypted);
            }

            return encrypted;
        }
        #endregion

        public void Close()
        {
            BaseStream?.Dispose();
            BaseStream = null;
        }

        public void Dispose()
        {
            this.Close();
        }
    }
}
