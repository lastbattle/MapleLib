using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Microsoft.VisualBasic;
using System;
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
    /// The .ms file format is a custom encrypted archive used to store WZ images. Encryption and decryption are performed using the Snow2 stream cipher, with keys derived from the file name and a randomly generated salt.
    /// </para>
    /// <para>
    /// <b>Encryption/Decryption Process:</b>
    /// <list type="number">
    /// <item>Random bytes and a salt are generated and written to the file header. The salt is obfuscated using XOR with the random bytes.</item>
    /// <item>The file name and salt are concatenated to form a base string for key derivation.</item>
    /// <item>Header and entry data are encrypted/decrypted using the Snow2 cipher, with keys derived from the base string and entry-specific data.</item>
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
        /// <returns>A 16-byte key as byte[].</returns>
        private byte[] DeriveSnowKey(ReadOnlySpan<char> fileNameWithSalt, bool isEntryKey = false)
        {
            Span<byte> key = stackalloc byte[WzMsConstants.SnowKeyLength];
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
            return key.ToArray();
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
            string saltStr = new(saltChars);

            string fileNameWithSalt = fileName + saltStr;
            byte[] snowCipherKey = DeriveSnowKey(fileNameWithSalt, false); // Replaces inline loop

            long headerStartPos = this.BaseStream.Position;
            using var snowCipher = new Snow2CryptoTransform(snowCipherKey, null, false);
            using var snowDecoderStream = new CryptoStream(this.BaseStream, snowCipher, CryptoStreamMode.Read, true);
            using (var snowReader = new BinaryReader(snowDecoderStream))
            {
                //byte[] last4 = snowReader.ReadBytes(16);
                //Debug.WriteLine($"Last 16 bytes: {BitConverter.ToString(last4)}");

                var (entryCount, headerHash) = ReadAndValidateHeader(snowReader, hashedSaltLen, saltBytes, saltLen);
                int padAmount = fileName.Sum(v => (int)v * 3) % WzMsConstants.HeaderPadMod + WzMsConstants.HeaderPadOffset;
                long entryStartPos = headerStartPos + 9 + padAmount;

                var header = new WzMsHeader(fullFileName, saltStr, fileNameWithSalt, headerHash, WzMsConstants.SupportedVersion, entryCount, headerStartPos, entryStartPos);
                this.Header = header;
            }
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
        private (int EntryCount, int CalculatedHash) ReadAndValidateHeader(BinaryReader snowReader, int hashedSaltLen, byte[] saltBytes, int saltLen)
        {
            int hash = snowReader.ReadInt32();
            byte version = snowReader.ReadByte();
            int entryCount = snowReader.ReadInt32();

            if (version != WzMsConstants.SupportedVersion)
                throw new Exception($"Unsupported version: expected {WzMsConstants.SupportedVersion}, got {version}");

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

            int entryCount = this.Header.EntryCount;
            if (this.Entries.Capacity < entryCount)
                this.Entries.Capacity = entryCount;

            string fileNameWithSalt = this.Header.FileNameWithSalt;
            byte[] snowKey = DeriveSnowKey(fileNameWithSalt, true); // Replaces inline loop

            using var snowCipher = new Snow2CryptoTransform(snowKey, null, false);
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

        /// <summary>
        /// Aligns position to the next 1024-byte page boundary.
        /// </summary>
        /// <param name="pos">Current position.</param>
        /// <returns>Aligned position.</returns>
        private static long AlignToPage(long pos) => (pos + WzMsConstants.PageAlignmentMask) & ~WzMsConstants.PageAlignmentMask;

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
                if (entry.Data == null)
                {
                    BaseStream.Position = entry.StartPos;
                    var buffer = new byte[entry.Size];
                    BaseStream.ReadExactly(buffer);
                    entry.Data = buffer;
                }
                Stream decrypted = DecryptData(entry); // dont close this stream!
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
            var encryptedDatas = new List<byte[]>();
            long currentBlockIndex = 0;
            foreach (var entry in this.Entries)
            {
                if (entry.Data == null)
                    continue;

                byte[] encrypted = this.EncryptData(entry, entry.Data); // Encrypt each WzImage
                int size = encrypted.Length;
                int sizeAligned = ((size + 1023) / 1024) * 1024;
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
            int randByteCount = fileName.Sum(c => (int)c) % 312 + 30;
            byte[] randBytes = new byte[randByteCount];
            for (int i = 0; i < randByteCount; i++)
            {
                randBytes[i] = (byte)rng.Next();
            }

            string saltStr = this.Header.Salt;
            char[] saltChars = saltStr.ToCharArray();
            int saltLen = saltStr.Length;
            byte xorVal = randBytes[0];
            byte lowByte = (byte)(saltLen ^ xorVal);
            int hashedSaltLen = lowByte;

            byte[] saltBytes = new byte[saltLen * 2];
            for (int i = 0; i < saltLen; i++)
            {
                saltBytes[i * 2] = (byte)(randBytes[i] ^ (byte)saltChars[i]);
                saltBytes[i * 2 + 1] = 0;
            }

            ReadOnlySpan<ushort> u16SaltBytes = MemoryMarshal.Cast<byte, ushort>(saltBytes);
            int sumSalt = 0;
            foreach (ushort u in u16SaltBytes)
            {
                sumSalt += u;
            }

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
            byte[] snowKey = DeriveSnowKey(fileNameWithSalt, false); // Replaces inline loop

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
                        snowEncoderStream.FlushFinalBlock();   // © explicitly force it
                    }
                }
            }

            // Padding after header
            int padAmount = fileName.Select(v => (int)v * 3).Sum() % 212 + 33;
            byte[] padHeader = new byte[padAmount];
            rng.NextBytes(padHeader); // randomize the pad header

            bWriter.Write(padHeader);

            // Entries encryption key
            byte[] snowCipherKey2 = DeriveSnowKey(fileNameWithSalt, true); // Replaces inline loop

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
        private byte[] DeriveImgKey(WzMsEntry entry)
        {
            uint keyHash = WzMsConstants.InitialKeyHash;
            foreach (char c in Header.Salt)
            {
                keyHash = (keyHash ^ c) * WzMsConstants.KeyHashMultiplier;
            }
            ReadOnlySpan<byte> keyHashDigits = keyHash.ToString().Select(static c => (byte)(c - '0')).ToArray().AsSpan(); // Temp array, but minimal

            Span<byte> imgKey = stackalloc byte[WzMsConstants.SnowKeyLength];
            ReadOnlySpan<char> entryNameSpan = entry.Name.AsSpan();
            ReadOnlySpan<byte> entryKeySpan = entry.EntryKey;
            for (int i = 0; i < imgKey.Length; i++)
            {
                int digitIdx = i % keyHashDigits.Length;
                int entryKeyIdx = (keyHashDigits[(i + 2) % keyHashDigits.Length] + i) % entryKeySpan.Length;
                imgKey[i] = (byte)(i + entryNameSpan[i % entryNameSpan.Length] * (
                    (keyHashDigits[digitIdx] % 2) + entryKeySpan[entryKeyIdx] + ((keyHashDigits[(i + 1) % keyHashDigits.Length] + i) % 5)
                ));
            }
            return imgKey.ToArray();
        }

        /// <summary>
        /// Decrypts this entry's data from the given stream using the provided salt string.
        /// </summary>
        /// <param name="entryData">The entry data of .img.</param>
        /// <returns>Decrypted data as a byte array.</returns>
        private Stream DecryptData(WzMsEntry entry)
        {
            byte[] imgKey = DeriveImgKey(entry);

            // Read
            using var ps = new PartialStream(this.BaseStream, entry.StartPos, entry.SizeAligned, true);
            var buffer = new byte[entry.Size];
            Span<byte> span = buffer;
            ps.Position = 0;

            var cs = new CryptoStream(ps, new Snow2CryptoTransform(imgKey, null, false), CryptoStreamMode.Read);

            // decrypt initial 1024 bytes twice
            {
                var cs2 = new CryptoStream(cs, new Snow2CryptoTransform(imgKey, null, false), CryptoStreamMode.Read);
                int dataLen = Math.Min(span.Length, WzMsConstants.DoubleEncryptInitialBytes);
                cs2.ReadExactly(span.Slice(0, dataLen));
                span = span.Slice(dataLen);
            }

            // decrypt subsequent bytes
            if (span.Length > 0)
            {
                cs.ReadExactly(span);
            }

            var ms = new MemoryStream(buffer);
            return ms;
        }

        /// <summary>
        /// Encrypts the plainData into entry's data
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="plainData"></param>
        /// <returns></returns>
        private byte[] EncryptData(WzMsEntry entry, byte[] plainData)
        {
            byte[] imgKey = DeriveImgKey(entry);

            var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, new Snow2CryptoTransform(imgKey, null, true), CryptoStreamMode.Write);

            int dataLen = Math.Min(plainData.Length, WzMsConstants.DoubleEncryptInitialBytes);
            using (var cs2 = new CryptoStream(cs, new Snow2CryptoTransform(imgKey, null, true), CryptoStreamMode.Write, true))
            {
                cs2.Write(plainData, 0, dataLen);
                cs2.Flush();
            }

            if (plainData.Length > WzMsConstants.DoubleEncryptInitialBytes)
            {
                cs.Write(plainData, WzMsConstants.DoubleEncryptInitialBytes, plainData.Length - WzMsConstants.DoubleEncryptInitialBytes);
                cs.Flush();
            }

            return ms.ToArray();
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
