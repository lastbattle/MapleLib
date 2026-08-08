using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System.Threading.Tasks;
using MapleLib.PacketLib;
using MapleLib.MapleCryptoLib;
using System.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MapleLib.ClientLib;

namespace MapleLib.WzLib
{
    /// <summary>
    /// A class that contains all the information of a wz file
    /// </summary>
    public class WzFile : WzObject
    {
        #region Fields
        internal string path;
        internal WzDirectory wzDir;
        internal WzHeader header;
        internal string name = "";

        internal ushort wzVersionHeader = 0;
        internal const ushort wzVersionHeader64bit_start = 770; // 777 for KMS, GMS v230 uses 778.. wut

        internal uint versionHash = 0;
        internal short mapleStoryPatchVersion = 0;
        internal WzMapleVersion maplepLocalVersion;
        internal MapleStoryLocalisation mapleLocaleVersion = MapleStoryLocalisation.Not_Known;

        internal bool wz_withEncryptVersionHeader = true;  // KMS update after Q4 2021, ver 1.2.357 does not contain any wz enc header information

        internal byte[] WzIv;
        // Retained after a successful parse because lazy WzImage instances
        // read directly from the directory's shared stream.  On a failed
        // parse this reader is disposed immediately so file handles do not
        // leak.
        private WzBinaryReader reader;
        #endregion

        /// <summary>
        /// The parsed IWzDir after having called ParseWzDirectory(), this can either be a WzDirectory or a WzListDirectory
        /// </summary>
        public WzDirectory WzDirectory
        {
            get { return wzDir; }
        }

        /// <summary>
        /// Name of the WzFile
        /// </summary>
        public override string Name
        {
            get { return name; }
            set { name = value; }
        }

        /// <summary>
        /// The WzObjectType of the file
        /// </summary>
        public override WzObjectType ObjectType
        {
            get { return WzObjectType.File; }
        }

        /// <summary>
        /// Returns WzDirectory[name]
        /// </summary>
        /// <param name="name">Name</param>
        /// <returns>WzDirectory[name]</returns>
        public new WzObject this[string name]
        {
            get { return WzDirectory[name]; }
        }
        /// <summary>
        /// Pathcaching to avoid repeated lookups for the same path
        /// Assuming data is immutable after loading and case-insensitive paths; adjust comparer if needed
        /// Note: If checkFirstDirectoryName varies often, consider including it in the cache key, e.g., path + "|" + checkFirstDirectoryName.ToString()
        /// </summary>
        private readonly Dictionary<string, WzObject> _pathCache = new(StringComparer.OrdinalIgnoreCase);

        public WzHeader Header { get { return header; } set { header = value; } }

        public short Version { get { return mapleStoryPatchVersion; } set { mapleStoryPatchVersion = value; } }

        public string FilePath { get { return path; } }

        public WzMapleVersion MapleVersion { get { return maplepLocalVersion; } set { maplepLocalVersion = value; } }

        /// <summary>
        /// The detected MapleStory locale version from 'MapleStory.exe' client.
        /// KMST, GMS, EMS, MSEA, CMS, TWMS, etc.
        /// </summary>
        public MapleStoryLocalisation MapleLocaleVersion { get { return mapleLocaleVersion; } private set { } }

        /// <summary>
        ///  Since KMST1132 / GMSv230 around 2022/02/09, wz removed the 2-byte encVer at position 0x3C, and use a fixed encVer 777.
        /// </summary>
        public bool Is64BitWzFile { get { return !wz_withEncryptVersionHeader; } private set { } }

        public override WzObject Parent { get { return null; } internal set { } }

        public override WzFile WzFileParent { get { return this; } }

        public override void Dispose()
        {
            _isUnloaded = true; // flag first

            WzBinaryReader ownedReader = reader ?? wzDir?.reader;
            reader = null;
            if (ownedReader != null)
            {
                ownedReader.Dispose();
                if (wzDir != null && ReferenceEquals(wzDir.reader, ownedReader))
                    wzDir.reader = null;
            }
            Header = null;
            path = null;
            name = null;
            _pathCache.Clear();
            wzDir?.Dispose();
        }

        private bool _isUnloaded = false;
        /// <summary>
        /// Returns true if this WZ file has been unloaded
        /// </summary>
        public bool IsUnloaded { get { return _isUnloaded; } private set { } }

        /// <summary>
        /// Initialize MapleStory WZ file
        /// </summary>
        /// <param name="gameVersion"></param>
        /// <param name="version"></param>
        public WzFile(short gameVersion, WzMapleVersion version)
        {
            wzDir = new WzDirectory();
            this.Header = WzHeader.GetDefault();
            mapleStoryPatchVersion = gameVersion;
            maplepLocalVersion = version;
            WzIv = WzTool.GetIvByMapleVersion(version);
            wzDir.WzIv = WzIv;
        }

        /// <summary>
        /// Open a wz file from a file on the disk
        /// </summary>
        /// <param name="filePath">Path to the wz file</param>
        /// <param name="version"></param>
        public WzFile(string filePath, WzMapleVersion version) : this(filePath, -1, version)
        {
        }

        /// <summary>
        /// Open a wz file from a file on the disk
        /// </summary>
        /// <param name="filePath">Path to the wz file</param>
        /// <param name="gameVersion"></param>
        /// <param name="version"></param>
        public WzFile(string filePath, short gameVersion, WzMapleVersion version)
        {
            name = Path.GetFileName(filePath);
            path = filePath;
            mapleStoryPatchVersion = gameVersion;
            maplepLocalVersion = version;

            if (version == WzMapleVersion.GETFROMZLZ)
            {
                using (FileStream zlzStream = File.OpenRead(Path.Combine(Path.GetDirectoryName(filePath), "ZLZ.dll")))
                {
                    this.WzIv = Util.WzKeyGenerator.GetIvFromZlz(zlzStream);
                }
            }
            else
                this.WzIv = WzTool.GetIvByMapleVersion(version);
        }

        /// <summary>
        /// Open a wz file from a file on the disk with a custom WzIv key
        /// </summary>
        /// <param name="filePath">Path to the wz file</param>
        public WzFile(string filePath, byte[] wzIv)
        {
            name = Path.GetFileName(filePath);
            path = filePath;
            mapleStoryPatchVersion = -1;
            maplepLocalVersion = WzMapleVersion.CUSTOM;

            this.WzIv = wzIv;
        }

        /// <summary>
        /// Parses the wz file, if the wz file is a list.wz file, WzDirectory will be a WzListDirectory, if not, it'll simply be a WzDirectory
        /// </summary>
        /// <param name="WzIv">WzIv is not set if null (Use existing iv)</param>
        public WzFileParseStatus ParseWzFile(byte[] WzIv = null)
        {
            /*if (maplepLocalVersion != WzMapleVersion.GENERATE)
            {
                parseErrorMessage = ("Cannot call ParseWzFile() if WZ file type is not GENERATE. Have you entered an invalid WZ key? ");
                return false;
            }*/
            if (WzIv != null)
            {
                this.WzIv = WzIv;
            }
            return ParseMainWzDirectory(false);
        }


        /// <summary>
        /// Parse directories in the WZ file
        /// </summary>
        /// <param name="parseErrorMessage"></param>
        /// <param name="lazyParse">Only load the firt WzDirectory found if true</param>
        /// <returns></returns>
        internal WzFileParseStatus ParseMainWzDirectory(bool lazyParse = false)
        {
            if (this.path == null)
            {
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Path is null");
                return WzFileParseStatus.Path_Is_Null;
            }

            // Keep the previous parse alive while reading a replacement stream.  A
            // malformed reparse must not leave the currently exposed directory
            // pointing at a reader that was already disposed.  All parser state is
            // restored below unless the replacement parse is accepted.
            WzBinaryReader previousReader = this.reader;
            WzDirectory previousDirectory = this.wzDir;
            WzHeader previousHeader = this.Header;
            uint previousVersionHash = this.versionHash;
            short previousPatchVersion = this.mapleStoryPatchVersion;
            MapleStoryLocalisation previousLocale = this.mapleLocaleVersion;
            ushort previousVersionHeader = this.wzVersionHeader;
            bool previousHasEncryptVersionHeader = this.wz_withEncryptVersionHeader;

            WzBinaryReader reader = new WzBinaryReader(File.Open(this.path, FileMode.Open, FileAccess.Read, FileShare.Read), WzIv);
            bool retainReader = false;
            void CommitReader()
            {
                this.reader = reader;
                retainReader = true;

                if (previousReader != null && !ReferenceEquals(previousReader, reader))
                {
                    previousReader.Dispose();
                    if (previousDirectory != null && ReferenceEquals(previousDirectory.reader, previousReader))
                        previousDirectory.reader = null;
                }
                if (previousDirectory != null && !ReferenceEquals(previousDirectory, this.wzDir))
                    previousDirectory.Dispose();
            }
            try
            {

            this.Header = new WzHeader();
            this.Header.Ident = reader.ReadString(4);
            this.Header.FSize = reader.ReadUInt64();
            this.Header.FStart = reader.ReadUInt32();

            long fileLength = reader.BaseStream.Length;
            if (this.Header.FStart < 17 || (ulong)this.Header.FStart > (ulong)fileLength)
                throw new InvalidDataException("WZ header FStart is outside the file.");

            long copyrightLength = (long)this.Header.FStart - 17L;
            if (copyrightLength > int.MaxValue)
                throw new InvalidDataException("WZ header copyright is too large.");

            this.Header.Copyright = reader.ReadString((int)copyrightLength);

            if (reader.BaseStream.Position >= fileLength)
                throw new InvalidDataException("WZ file is missing its header terminator.");
            byte unk1 = reader.ReadByte();
            long headerPadding = (long)this.Header.FStart - reader.BaseStream.Position;
            if (headerPadding < 0 || headerPadding > int.MaxValue)
                throw new InvalidDataException("WZ header padding is invalid.");
            byte[] unk2 = reader.ReadBytes((int)headerPadding);
            reader.Header = this.Header;

            Check64BitClient(reader);  // update b64BitClient flag

            // the value of wzVersionHeader is less important. It is used for reading/writing from/to WzFile Header, and calculating the versionHash.
            // it can be any number if the client is 64-bit. Assigning 777 is just for convenience when calculating the versionHash.
            if (this.wz_withEncryptVersionHeader && reader.BaseStream.Length - this.Header.FStart < sizeof(ushort))
                throw new InvalidDataException("WZ file is missing its version header.");
            this.wzVersionHeader = this.wz_withEncryptVersionHeader ? reader.ReadUInt16() : wzVersionHeader64bit_start;

            Debug.WriteLine("----------------------------------------");
            Debug.WriteLine(string.Format("Read Wz File {0}", this.Name));
            Debug.WriteLine(string.Format("wz_withEncryptVersionHeader: {0}", wz_withEncryptVersionHeader));
            Debug.WriteLine(string.Format("wzVersionHeader: {0}", wzVersionHeader));
            Debug.WriteLine("----------------------------------------");

            if (mapleStoryPatchVersion == -1)
            {
                // for 64-bit client, return immediately if version 777 works correctly.
                // -- the latest KMS update seems to have changed it to 778? 779?
                if (!this.wz_withEncryptVersionHeader) 
                {
                    for (ushort maplestoryVerToDecode = wzVersionHeader64bit_start; maplestoryVerToDecode < wzVersionHeader64bit_start + 10; maplestoryVerToDecode++) // 770 ~ 780
                    {
                        if (TryDecodeWithWZVersionNumber(reader, wzVersionHeader, maplestoryVerToDecode, lazyParse))
                        {
                            CommitReader();
                            return WzFileParseStatus.Success;
                        }
                    }
                }
                // Attempt to get version from MapleStory.exe first
                short maplestoryVerDetectedFromClient = GetMapleStoryVerFromExe(this.path, out this.mapleLocaleVersion);

                // this step is actually not needed if we know the maplestory patch version (the client .exe), but since we dont..
                // we'll need a bruteforce way around it. 
                const short MAX_PATCH_VERSION = 2000; // wont be reached for the forseeable future.

                for (int j = maplestoryVerDetectedFromClient; j < MAX_PATCH_VERSION; j++)
                {
                    //Debug.WriteLine("Try decode 1 with maplestory ver: " + j);

                    if (TryDecodeWithWZVersionNumber(reader, wzVersionHeader, j, lazyParse))
                    {
                        CommitReader();
                        return WzFileParseStatus.Success;
                    }
                }
                //parseErrorMessage = "Error with game version hash : The specified game version is incorrect and WzLib was unable to determine the version itself";
                return WzFileParseStatus.Error_Game_Ver_Hash;
            }
            else
            {
                this.versionHash = CheckAndGetVersionHash(wzVersionHeader, mapleStoryPatchVersion);
                reader.Hash = this.versionHash;

                WzDirectory directory = new WzDirectory(reader, this.name, this.versionHash, this.WzIv, this);
                directory.ParseDirectory();
                this.wzDir = directory;
            }
            CommitReader();
            return WzFileParseStatus.Success;
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidDataException("WZ file is truncated.", ex);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new InvalidDataException("WZ file contains an invalid offset or length.", ex);
            }
            finally
            {
                if (!retainReader)
                {
                    reader.Dispose();

                    // Restore the last known-good state.  This covers both
                    // exceptions and non-throwing version-detection failures.
                    this.reader = previousReader;
                    this.wzDir = previousDirectory;
                    this.Header = previousHeader;
                    this.versionHash = previousVersionHash;
                    this.mapleStoryPatchVersion = previousPatchVersion;
                    this.mapleLocaleVersion = previousLocale;
                    this.wzVersionHeader = previousVersionHeader;
                    this.wz_withEncryptVersionHeader = previousHasEncryptVersionHeader;
                }
            }
        }

        /// <summary>
        /// encVer detecting:
        /// Since KMST1132 (GMSv230, 2022/02/09), wz removed the 2-byte encVer at 0x3C, and use a fixed encVer 777.
        /// Here we try to read the first 2 bytes from data part (0x3C) and guess if it looks like an encVer.
        ///
        /// Credit: WzComparerR2 project
        /// </summary>
        private void Check64BitClient(WzBinaryReader reader)
        {
            if (this.Header.FSize >= 2)
            {
                reader.BaseStream.Position = this.header.FStart; // go back to 0x3C

                int encver = reader.ReadUInt16();
                if (encver > 0xff) // encver always less than 256
                {
                    this.wz_withEncryptVersionHeader = false;
                }
                else if (encver == 0x80)
                {
                    // there's an exceptional case that the first field of data part is a compressed int which determined property count,
                    // if the value greater than 127 and also to be a multiple of 256, the first 5 bytes will become to
                    //   80 00 xx xx xx
                    // so we additional check the int value, at most time the child node count in a wz won't greater than 65536.
                    if (this.Header.FSize >= 5)
                    {
                        reader.BaseStream.Position = this.header.FStart; // go back to 0x3C
                        int propCount = reader.ReadInt32();
                        if (propCount > 0 && (propCount & 0xff) == 0 && propCount <= 0xffff)
                        {
                            this.wz_withEncryptVersionHeader = false;
                        }
                    }
                } else
                {
                    // old wz file with header version
                }
            }
            else
            {
                // Obviously, if data part have only 1 byte, encver must be deleted.
                this.wz_withEncryptVersionHeader = false;
            }


            // reset position
            reader.BaseStream.Position = this.Header.FStart;
        }

        private bool TryDecodeWithWZVersionNumber(WzBinaryReader reader, int useWzVersionHeader, int useMapleStoryPatchVersion, bool lazyParse)
        {
            this.mapleStoryPatchVersion = (short)useMapleStoryPatchVersion;

            this.versionHash = CheckAndGetVersionHash(useWzVersionHeader, mapleStoryPatchVersion);
            if (this.versionHash == 0) // ugly hack, but that's the only way if the version number isnt known (nexon stores this in the .exe)
                return false;

            reader.Hash = this.versionHash;
            long fallbackOffsetPosition = reader.BaseStream.Position; // save position to rollback to, if should parsing fail from here
            WzDirectory testDirectory = null;
            bool keepTestDirectory = false;
            try
            {
                testDirectory = new WzDirectory(reader, this.name, this.versionHash, this.WzIv, this);
                testDirectory.ParseDirectory(lazyParse);
            }
            catch (Exception exp)
            {
                Debug.WriteLine(exp.ToString());

                reader.BaseStream.Position = fallbackOffsetPosition;
                testDirectory?.Dispose();
                return false;
            }

            // Test the image and see if its correct by parsing it.
            try
            {
                WzImage testImage = testDirectory.WzImages.FirstOrDefault();
                if (testImage != null)
                {
                    try
                    {
                        reader.BaseStream.Position = testImage.Offset;
                        byte checkByte = reader.ReadByte();
                        reader.BaseStream.Position = fallbackOffsetPosition;

                        switch (checkByte)
                        {
                            case WzImage.WzImageHeaderByte_Lua:
                            case 0x73:
                            case 0x1b:
                                {
                                    this.wzDir = testDirectory;
                                    keepTestDirectory = true;

                                    Debug.WriteLine("[WzFile] Accepted version {0} (hash={1}) for {2}, checkByte=0x{3:X2}",
                                        mapleStoryPatchVersion, versionHash, Name, checkByte);
                                    return true;
                                }
                            case 0x30:
                            case 0x6C: // idk
                            case 0xBC: // Map002.wz? KMST?
                            default:
                                {
                                    // checkByte did not match known image headers (0x73, 0x1b)
                                    // This version hash produces wrong offsets — reject and try next version
                                    break;
                                }
                        }
                        reader.BaseStream.Position = fallbackOffsetPosition; // reset
                    }
                    catch
                    {
                        reader.BaseStream.Position = fallbackOffsetPosition; // reset
                        return false;
                    }
                    return false;
                }
                else // if there's no image in the WZ file (new KMST Base.wz), test the directory instead
                {
                    // coincidentally in msea v194 Map001.wz, the hash matches exactly using mapleStoryPatchVersion of 113, and it fails to decrypt later on (probably 1 in a million chance? o_O).
                    // damn, technical debt accumulating here
                    // also needs to check for 'Is64BitWzFile' as it may match TaiwanMS v113 (pre-bb) and return as false.
                    if (Is64BitWzFile && mapleStoryPatchVersion == 113) 
                    {
                        // hack for now
                        reader.BaseStream.Position = fallbackOffsetPosition; // reset
                        return false;
                    }
                    else
                    {
                        this.wzDir = testDirectory;
                        keepTestDirectory = true;

                        return true;
                    }
                }
            }
            finally
            {
                if (!keepTestDirectory)
                    testDirectory?.Dispose();
            }
        }

        /// <summary>
        /// Attempts to get the MapleStory patch version number from MapleStory.exe
        /// </summary>
        /// <returns>0 if the exe could not be found, or version number be detected</returns>
        private static short GetMapleStoryVerFromExe(string wzFilePath, out MapleStoryLocalisation mapleLocaleVersion)
        {
            // https://github.com/lastbattle/Harepacker-resurrected/commit/63e2d72ac006f0a45fc324a2c33c23f0a4a988fa#r56759414
            // <3 mechpaul
            const string MAPLESTORY_EXE_NAME = "MapleStory.exe";
            const string MAPLESTORYT_EXE_NAME = "MapleStoryT.exe";
            const string MAPLESTORYADMIN_EXE_NAME = "MapleStoryA.exe";

            FileInfo wzFileInfo = new FileInfo(wzFilePath);
            if (!wzFileInfo.Exists)
            {
                mapleLocaleVersion = MapleStoryLocalisation.Not_Known; // set
                return 0;
            }

            System.IO.DirectoryInfo currentDirectory = wzFileInfo.Directory;
            for (int i = 0; i < 4; i++)  // just attempt 4 directories here
            {
                FileInfo[] msExeFileInfos = currentDirectory.GetFiles(MAPLESTORY_EXE_NAME, SearchOption.TopDirectoryOnly); // case insensitive 
                FileInfo[] msTExeFileInfos = currentDirectory.GetFiles(MAPLESTORYT_EXE_NAME, SearchOption.TopDirectoryOnly);  // case insensitive 
                FileInfo[] msAdminExeFileInfos = currentDirectory.GetFiles(MAPLESTORYADMIN_EXE_NAME, SearchOption.TopDirectoryOnly);  // case insensitive 

                List<FileInfo> exeFileInfo = new List<FileInfo>();
                if (msTExeFileInfos.Length > 0 && msTExeFileInfos[0].Exists) // prioritize MapleStoryT.exe first
                {
                    exeFileInfo.Add(msTExeFileInfos[0]);
                }
                if (msAdminExeFileInfos.Length > 0 && msAdminExeFileInfos[0].Exists)
                {
                    exeFileInfo.Add(msAdminExeFileInfos[0]);
                }
                if (msExeFileInfos.Length > 0 && msExeFileInfos[0].Exists)
                {
                    exeFileInfo.Add(msExeFileInfos[0]);
                }

                foreach (FileInfo msExeFileInfo in exeFileInfo)
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(Path.Combine(currentDirectory.FullName, msExeFileInfo.FullName));

                    if ((versionInfo.FileMajorPart == 1 && versionInfo.FileMinorPart == 0 && versionInfo.FileBuildPart == 0)
                        || (versionInfo.FileMajorPart == 0 && versionInfo.FileMinorPart == 0 && versionInfo.FileBuildPart == 0)) // older client uses 1.0.0.1 
                        continue;

                    int locale = versionInfo.FileMajorPart;
                    MapleStoryLocalisation localeVersion = MapleStoryLocalisation.Not_Known;
                    if (Enum.IsDefined(typeof(MapleStoryLocalisation), locale))
                    {
                        localeVersion = (MapleStoryLocalisation)locale;
                    }
                    var msVersion = versionInfo.FileMinorPart;
                    var msMinorPatchVersion = versionInfo.FileBuildPart;

                    mapleLocaleVersion = localeVersion; // set
                    return (short)msVersion;
                }
                currentDirectory = currentDirectory.Parent; // check the parent folder on the next run
                if (currentDirectory == null)
                    break;
            }

            mapleLocaleVersion = MapleStoryLocalisation.Not_Known; // set
            return 0;
        }

        /// <summary>
        /// Check and gets the version hash.
        /// </summary>
        /// <param name="wzVersionHeader">The version header from .wz file.</param>
        /// <param name="maplestoryPatchVersion"></param>
        /// <returns></returns>
        private static uint CheckAndGetVersionHash(int wzVersionHeader, int maplestoryPatchVersion)
        {
            uint versionHash = 0;

            foreach (char ch in maplestoryPatchVersion.ToString())
            {
                versionHash = (versionHash * 32) + (byte)ch + 1;
            }

            if (wzVersionHeader == wzVersionHeader64bit_start)
                return (uint)versionHash; // always 59192

            int decryptedVersionNumber = (byte)~((versionHash >> 24) & 0xFF ^ (versionHash >> 16) & 0xFF ^ (versionHash >> 8) & 0xFF ^ versionHash & 0xFF);

            if (wzVersionHeader == decryptedVersionNumber)
                return (uint)versionHash;
            return 0; // invalid
        }

        /// <summary>
        /// Version hash
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateWZVersionHash()
        {
            versionHash = 0;
            foreach (char ch in mapleStoryPatchVersion.ToString())
            {
                versionHash = (versionHash * 32) + (byte)ch + 1;
            }
            wzVersionHeader = (byte)~((versionHash >> 24) & 0xFF ^ (versionHash >> 16) & 0xFF ^ (versionHash >> 8) & 0xFF ^ versionHash & 0xFF);
        }

        /// <summary>
        /// Saves a wz file to the disk, AKA repacking.
        /// </summary>
        /// <param name="path">Path to the output wz file</param>
        /// <param name="override_saveAs64BitWZ"></param>
        /// <param name="savingToPreferredWzVer"></param>
        public void SaveToDisk(string path, bool? override_saveAs64BitWZ = null, WzMapleVersion savingToPreferredWzVer = WzMapleVersion.UNKNOWN)
        {
            // WZ IV
            if (savingToPreferredWzVer == WzMapleVersion.UNKNOWN)
                WzIv = WzTool.GetIvByMapleVersion(maplepLocalVersion); // get from local WzFile
            else
                WzIv = WzTool.GetIvByMapleVersion(savingToPreferredWzVer); // custom selected

            bool bIsWzIvSimilar = WzIv.SequenceEqual(wzDir.WzIv); // check if its saving to the same IV.
            wzDir.WzIv = WzIv;

            // MapleStory UserKey
            bool bIsWzUserKeyDefault = MapleCryptoConstants.IsDefaultMapleStoryUserKey(); // check if its saving to the same UserKey.
            // Save WZ as 64-bit wz format
            bool bSaveAs64BitWz = !this.wz_withEncryptVersionHeader; // 64 bit does not have this header
            if (override_saveAs64BitWZ != null)
            {
                bSaveAs64BitWz = (bool)override_saveAs64BitWZ;
            }

            CreateWZVersionHash();
            wzDir.SetVersionHash(versionHash);

            Debug.WriteLine("----------------------------------------");
            Debug.WriteLine(string.Format("Saving Wz File {0}", this.Name));
            Debug.WriteLine(string.Format("wzVersionHeader: {0}", wzVersionHeader));
            Debug.WriteLine(string.Format("bSaveAs64BitWz: {0}", bSaveAs64BitWz));
            Debug.WriteLine("----------------------------------------");

            string tempFile = Path.GetFileNameWithoutExtension(path) + ".TEMP";
            try
            {
                File.Create(tempFile).Close();
                using (FileStream fs = new FileStream(tempFile, FileMode.Append, FileAccess.Write))
                {
                    wzDir.GenerateDataFile(bIsWzIvSimilar ? null : WzIv, bIsWzUserKeyDefault, fs);
                }

                WzTool.StringCache.Clear();

                using (WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), WzIv))
                {
                    wzWriter.Hash = versionHash;

                    uint totalLen = wzDir.GetImgOffsets(wzDir.GetOffsets(Header.FStart + (!bSaveAs64BitWz ? 2u : 0)));
                    Header.FSize = totalLen - Header.FStart;
                    for (int i = 0; i < 4; i++)
                    {
                        wzWriter.Write((byte)Header.Ident[i]);
                    }
                    wzWriter.Write((long)Header.FSize);
                    wzWriter.Write(Header.FStart);
                    wzWriter.WriteNullTerminatedString(Header.Copyright);

                    long extraHeaderLength = Header.FStart - wzWriter.BaseStream.Position;
                    if (extraHeaderLength > 0)
                    {
                        wzWriter.Write(new byte[(int)extraHeaderLength]);
                    }
                    if (!bSaveAs64BitWz) // 64 bit doesnt have a version number.
                        wzWriter.Write((ushort) wzVersionHeader);

                    wzWriter.Header = Header;
                    wzDir.SaveDirectory(wzWriter);
                    wzWriter.StringCache.Clear();

                    using (FileStream fs = File.OpenRead(tempFile))
                    {
                        wzDir.SaveImages(wzWriter, fs);
                    }
                    wzWriter.StringCache.Clear();
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        public void ExportXml(string path, bool oneFile)
        {
            if (oneFile)
            {
                FileStream fs = File.Create(path + "/" + this.name + ".xml");
                StreamWriter writer = new StreamWriter(fs);

                int level = 0;
                writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag("WzFile", this.name, true));
                this.wzDir.ExportXml(writer, oneFile, level, false);
                writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.CloseTag("WzFile"));

                writer.Close();
            }
            else
            {
                throw new Exception("Under Construction");
            }
        }

        /// <summary>
        /// Returns an array of objects from a given path. Wild cards are supported
        /// For example :
        /// GetObjectsFromPath("Map.wz/Map0/*");
        /// Would return all the objects (in this case images) from the sub directory Map0
        /// </summary>
        /// <param name="path">The path to the object(s)</param>
        /// <returns>An array of IWzObjects containing the found objects</returns>
        public List<WzObject> GetObjectsFromWildcardPath(string path)
        {
            if (string.Equals(path, name, StringComparison.OrdinalIgnoreCase))
                return new List<WzObject> { WzDirectory };
            else if (path == "*")
            {
                var fullList = new List<WzObject> { WzDirectory };
                fullList.AddRange(GetObjectsFromDirectory(WzDirectory));
                return fullList;
            }
            else if (!path.Contains("*"))
                return new List<WzObject> { GetObjectFromPath(path) };

            string[] seperatedNames = path.Split("/".ToCharArray());
            if (seperatedNames.Length == 2 && seperatedNames[1] == "*")
                return GetObjectsFromDirectory(WzDirectory);

            var objList = new List<WzObject>();
            var pathSegments = new List<string>(8) { name };
            TraverseSearchImages(WzDirectory.WzImages, pathSegments, false, path, null, objList);
            TraverseSearchDirectories(wzDir.WzDirectories, pathSegments, path, null, objList);
            return objList;
        }

        public List<WzObject> GetObjectsFromRegexPath(string path)
        {
            if (string.Equals(path, name, StringComparison.OrdinalIgnoreCase))
                return new List<WzObject> { WzDirectory };

            Regex regex = new Regex(path);
            var objList = new List<WzObject>();
            var pathSegments = new List<string>(8) { name };
            TraverseSearchImages(WzDirectory.WzImages, pathSegments, false, null, regex, objList);
            TraverseSearchDirectories(wzDir.WzDirectories, pathSegments, null, regex, objList);
            return objList;
        }

        /// <summary>
        /// Walks images in their existing order and evaluates each generated path as it is
        /// visited.  Root images are intentionally not evaluated as objects: GetPathsFromImage
        /// (used by the legacy root search loops) emits only their property paths.  Images reached
        /// through a directory are evaluated by GetPathsFromDirectory and pass includeImage=true.
        /// The old implementation first materialized every path and then looked up each match
        /// again through the global file manager; traversing the object graph directly avoids both
        /// allocations and a second lookup while retaining the path enumeration order.
        /// </summary>
        private void TraverseSearchImages(
            IEnumerable<WzImage> images,
            List<string> pathSegments,
            bool includeImage,
            string wildcardPath,
            Regex regexPath,
            List<WzObject> results)
        {
            foreach (WzImage image in images)
            {
                pathSegments.Add(image.Name);
                if (includeImage)
                    AddSearchMatch(pathSegments, image, wildcardPath, regexPath, results);

                foreach (WzImageProperty property in image.WzProperties)
                {
                    pathSegments.Add(property.Name);
                    TraverseSearchProperty(property, pathSegments, true, wildcardPath, regexPath, results);
                    pathSegments.RemoveAt(pathSegments.Count - 1);
                }

                pathSegments.RemoveAt(pathSegments.Count - 1);
            }
        }

        /// <summary>
        /// Walks directories in their existing order.  Images are visited before child
        /// directories, matching GetPathsFromDirectory and GetObjectsFromDirectory.
        /// </summary>
        private void TraverseSearchDirectories(
            IEnumerable<WzDirectory> directories,
            List<string> pathSegments,
            string wildcardPath,
            Regex regexPath,
            List<WzObject> results)
        {
            foreach (WzDirectory directory in directories)
            {
                pathSegments.Add(directory.Name);
                AddSearchMatch(pathSegments, directory, wildcardPath, regexPath, results);

                TraverseSearchImages(directory.WzImages, pathSegments, true, wildcardPath, regexPath, results);
                TraverseSearchDirectories(directory.WzDirectories, pathSegments, wildcardPath, regexPath, results);

                pathSegments.RemoveAt(pathSegments.Count - 1);
            }
        }

        /// <summary>
        /// Evaluates an image property and then follows only the descendants represented by
        /// GetPathsFromProperty.  Scalar child properties do not have a path of their own;
        /// Canvas exposes PNG and Vector exposes X/Y terminal objects.
        /// </summary>
        private void TraverseSearchProperty(
            WzImageProperty property,
            List<string> pathSegments,
            bool includeProperty,
            string wildcardPath,
            Regex regexPath,
            List<WzObject> results)
        {
            if (includeProperty)
                AddSearchMatch(pathSegments, property, wildcardPath, regexPath, results);

            switch (property.PropertyType)
            {
                case WzPropertyType.Canvas:
                    pathSegments.Add("PNG");
                    AddSearchMatch(pathSegments, ((WzCanvasProperty)property).PngProperty, wildcardPath, regexPath, results);
                    pathSegments.RemoveAt(pathSegments.Count - 1);

                    foreach (WzImageProperty child in ((WzCanvasProperty)property).WzProperties)
                    {
                        pathSegments.Add(child.Name);
                        TraverseSearchProperty(child, pathSegments, false, wildcardPath, regexPath, results);
                        pathSegments.RemoveAt(pathSegments.Count - 1);
                    }
                    break;

                case WzPropertyType.Convex:
                    foreach (WzImageProperty child in ((WzConvexProperty)property).WzProperties)
                    {
                        pathSegments.Add(child.Name);
                        TraverseSearchProperty(child, pathSegments, false, wildcardPath, regexPath, results);
                        pathSegments.RemoveAt(pathSegments.Count - 1);
                    }
                    break;

                case WzPropertyType.SubProperty:
                    foreach (WzImageProperty child in ((WzSubProperty)property).WzProperties)
                    {
                        pathSegments.Add(child.Name);
                        TraverseSearchProperty(child, pathSegments, false, wildcardPath, regexPath, results);
                        pathSegments.RemoveAt(pathSegments.Count - 1);
                    }
                    break;

                case WzPropertyType.Vector:
                    WzVectorProperty vector = (WzVectorProperty)property;
                    pathSegments.Add("X");
                    AddSearchMatch(pathSegments, vector.X, wildcardPath, regexPath, results);
                    pathSegments.RemoveAt(pathSegments.Count - 1);

                    pathSegments.Add("Y");
                    AddSearchMatch(pathSegments, vector.Y, wildcardPath, regexPath, results);
                    pathSegments.RemoveAt(pathSegments.Count - 1);
                    break;
            }
        }

        private void AddSearchMatch(
            List<string> pathSegments,
            WzObject value,
            string wildcardPath,
            Regex regexPath,
            List<WzObject> results)
        {
            string candidatePath = string.Join("/", pathSegments);
            if ((wildcardPath != null && StringMatch(wildcardPath, candidatePath)) ||
                (regexPath != null && regexPath.IsMatch(candidatePath)))
            {
                results.Add(value);
            }
        }

        public List<WzObject> GetObjectsFromDirectory(WzDirectory dir)
        {
            List<WzObject> objList = new List<WzObject>();
            foreach (WzImage image in dir.WzImages)
                objList.AddRange(GetObjectsFromImage(image));
            foreach (WzDirectory subDirectory in dir.WzDirectories)
                objList.AddRange(GetObjectsFromDirectory(subDirectory));
            return objList;
        }

        public List<WzObject> GetObjectsFromImage(WzImage img)
        {
            var objList = new List<WzObject>();
            foreach (WzImageProperty property in img.WzProperties)
            {
                objList.Add(property);
                objList.AddRange(GetObjectsFromProperty(property));
            }
            return objList;
        }

        public List<WzObject> GetObjectsFromProperty(WzImageProperty prop)
        {
            List<WzObject> objList = new List<WzObject>();
            var subProperties = new List<WzImageProperty>();

            bool bAddRange = true;
            switch (prop.PropertyType)
            {
                case WzPropertyType.Canvas:
                    subProperties = ((WzCanvasProperty)prop).WzProperties;
                    objList.Add(((WzCanvasProperty)prop).PngProperty);
                    break;
                case WzPropertyType.Convex:
                    subProperties = ((WzConvexProperty)prop).WzProperties;
                    break;
                case WzPropertyType.SubProperty:
                    subProperties = ((WzSubProperty)prop).WzProperties;
                    break;
                case WzPropertyType.Vector:
                    objList.Add(((WzVectorProperty)prop).X);
                    objList.Add(((WzVectorProperty)prop).Y);
                    bAddRange = false;
                    break;
            }

            if (bAddRange)
            {
                foreach (WzImageProperty subProperty in subProperties)
                {
                    objList.AddRange(GetObjectsFromProperty(subProperty));
                }
            }

            return objList;
        }

        internal List<string> GetPathsFromDirectory(WzDirectory dir, string curPath)
        {
            var objList = new List<string>();
            foreach (WzImage image in dir.WzImages)
            {
                string imagePath = curPath + "/" + image.Name;
                objList.Add(imagePath);
                objList.AddRange(GetPathsFromImage(image, imagePath));
            }
            foreach (WzDirectory subDirectory in dir.WzDirectories)
            {
                string directoryPath = curPath + "/" + subDirectory.Name;
                objList.Add(directoryPath);
                objList.AddRange(GetPathsFromDirectory(subDirectory, directoryPath));
            }
            return objList;
        }


        internal List<string> GetPathsFromImage(WzImage img, string curPath)
        {
            var objList = new List<string>();
            foreach (WzImageProperty property in img.WzProperties)
            {
                string propertyPath = curPath + "/" + property.Name;
                objList.Add(propertyPath);
                objList.AddRange(GetPathsFromProperty(property, propertyPath));
            }
            return objList;
        }

        internal List<string> GetPathsFromProperty(WzImageProperty prop, string curPath)
        {
            List<string> objList = new List<string>();
            var subProperties = new List<WzImageProperty>();

            bool bAddRange = true;
            switch (prop.PropertyType)
            {
                case WzPropertyType.Canvas:
                    subProperties = ((WzCanvasProperty)prop).WzProperties;
                    objList.Add(curPath + "/PNG");
                    break;
                case WzPropertyType.Convex:
                    subProperties = ((WzConvexProperty)prop).WzProperties;
                    break;
                case WzPropertyType.SubProperty:
                    subProperties = ((WzSubProperty)prop).WzProperties;
                    break;
                case WzPropertyType.Vector:
                    objList.Add(curPath + "/X");
                    objList.Add(curPath + "/Y");
                    bAddRange = false;
                    break;
            }

            if (bAddRange)
            {
                foreach (WzImageProperty subProperty in subProperties)
                {
                    string propertyPath = curPath + "/" + subProperty.Name;
                    objList.AddRange(GetPathsFromProperty(subProperty, propertyPath));
                }
            }

            return objList;
        }

        /// <summary>
        /// Get WZ objects from path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="checkFirstDirectoryName"></param>
        /// <returns></returns>
        public WzObject GetObjectFromPath(string path, bool checkFirstDirectoryName = true)
        {
            // Add caching to avoid repeated lookups for the same path
            // Assuming data is immutable after loading and case-insensitive paths; adjust comparer if needed
            // Note: If checkFirstDirectoryName varies often, consider including it in the cache key, e.g., path + "|" + checkFirstDirectoryName.ToString()
            if (_pathCache.TryGetValue(path, out WzObject cached))
            {
                return cached;
            }

            string[] separatedPath = path.Split('/');
            if (separatedPath.Length == 1)
            {
                _pathCache[path] = WzDirectory;
                return WzDirectory;
            }

            WzObject curObj = null;
            int pathIndex = 0;

            if (checkFirstDirectoryName)
            {
                if (WzFileManager.fileManager == null)
                {
                    return null;
                }

                bool bIsCanvasDir = WzFileManager.ContainsCanvasDirectory(path);
                if (bIsCanvasDir)
                {
                    string beforeCanvasPath = WzFileManager.NormaliseWzCanvasDirectory(path).Replace("/", "\\");  // "map", "map\\back"
                    if (beforeCanvasPath.Length > 0)
                    {
                        beforeCanvasPath = beforeCanvasPath + "\\" + WzFileManager.CANVAS_DIRECTORY_NAME.ToLowerInvariant();
                    }
                    List<WzDirectory> wzDir = WzFileManager.fileManager.GetWzDirectoriesFromBase(beforeCanvasPath, true);  // all of the possible "._Canvas_000.wz" file that the image may be in

                    // path = "Map/_Canvas/MapHelper.img/mark/Hilla"
                    string canvasMarker = $"/{WzFileManager.CANVAS_DIRECTORY_NAME}/"; 
                    string itemDirectoryPath = path.Contains(canvasMarker) ?
                        path.Substring(path.IndexOf(canvasMarker) + canvasMarker.Length) : path;
                    string[] itemDirectoryPaths = itemDirectoryPath.Split('/');

                    bool found = false;
                    foreach (WzDirectory dir in wzDir)
                    {
                        WzObject innerWzObject = dir[itemDirectoryPaths[0]];
                        if (innerWzObject != null)
                        {
                            // Calculate start index in original separatedPath to avoid array copy
                            int canvasPathIndex = separatedPath.Length - itemDirectoryPaths.Length + 1;
                            WzObject resolvedObject = ResolveObjectPath(innerWzObject, separatedPath, canvasPathIndex);
                            if (resolvedObject != null)
                            {
                                curObj = resolvedObject;
                                pathIndex = separatedPath.Length;
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        return null;
                    }
                }
                else
                {
                    List<WzDirectory> wzDir = WzFileManager.fileManager.GetWzDirectoriesFromBase(separatedPath[0], true); 
                    WzDirectory wzInnerDir = null;
                    foreach (WzDirectory dir in wzDir)
                    {
                        ReadOnlySpan<char> nameSpan = dir.name.AsSpan();
                        ReadOnlySpan<char> partSpan = separatedPath[0].AsSpan();
                        if (string.Equals(dir.name, separatedPath[0], StringComparison.OrdinalIgnoreCase) ||
                            (dir.name.Length > 3 && nameSpan.Slice(0, dir.name.Length - 3).SequenceEqual(partSpan) && // SequenceEqual for spans, but for ignore case, use custom or fallback
                             string.Equals(dir.name.Substring(0, dir.name.Length - 3), separatedPath[0], StringComparison.OrdinalIgnoreCase))) // Fallback to Substring for ignore case; optimize if possible
                        {
                            wzInnerDir = dir;
                            break;
                        }
                    }

                    if (wzInnerDir != null)
                    {
                        // Fixed potential bug: Use the found wzInnerDir as starting point if available
                        curObj = wzInnerDir;
                        pathIndex = 1;
                    }
                    else if (separatedPath.Length >= 2)  // Map/Obj/xxx.img -> Obj.wz
                    {
                        curObj = WzFileManager.fileManager.FindWzImageByName(separatedPath[0], separatedPath[1]);  // Map/xxx.img
                        if (curObj != null)
                        {
                            pathIndex = 2;
                        }
                        else if (separatedPath.Length >= 3)
                        {
                            curObj = WzFileManager.fileManager.FindWzImageByName(separatedPath[0] + Path.DirectorySeparatorChar + separatedPath[1], separatedPath[2]);
                            if (curObj != null)
                            {
                                pathIndex = 3;
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            else
            {
                curObj = WzDirectory;
                pathIndex = 0;
            }

            if (curObj == null)
            {
                return null;
            }

            curObj = ResolveObjectPath(curObj, separatedPath, pathIndex);
            if (curObj == null)
            {
                return null;
            }

            _pathCache[path] = curObj;
            return curObj;
        }

        private static WzObject ResolveObjectPath(WzObject curObj, string[] separatedPath, int pathIndex)
        {
            for (int i = pathIndex; i < separatedPath.Length; i++)
            {
                string pathPart = separatedPath[i];
                if (curObj == null)
                {
                    return null;
                }

                switch (curObj.ObjectType)
                {
                    case WzObjectType.Directory:
                        curObj = ((WzDirectory)curObj)[pathPart];
                        continue;
                    case WzObjectType.Image:
                        curObj = ((WzImage)curObj)[pathPart];
                        continue;
                    case WzObjectType.Property:
                        switch (((WzImageProperty)curObj).PropertyType)
                        {
                            case WzPropertyType.Canvas:
                                curObj = ((WzCanvasProperty)curObj)[pathPart];
                                continue;
                            case WzPropertyType.Convex:
                                curObj = ((WzConvexProperty)curObj)[pathPart];
                                continue;
                            case WzPropertyType.SubProperty:
                                curObj = ((WzSubProperty)curObj)[pathPart];
                                continue;
                            case WzPropertyType.Vector:
                                if (pathPart == "X")
                                    return ((WzVectorProperty)curObj).X;
                                else if (pathPart == "Y")
                                    return ((WzVectorProperty)curObj).Y;
                                else
                                    return null;
                            default:
                                return null;
                        }
                }
            }

            return curObj;
        }

        /// <summary>
        /// Get WZ object from multiple loaded WZ files in memory
        /// </summary>
        /// <param name="path"></param>
        /// <param name="wzFiles"></param>
        /// <returns></returns>
        public static WzObject GetObjectFromMultipleWzFilePath(string path, IReadOnlyCollection<WzFile> wzFiles)
        {
            foreach (WzFile file in wzFiles)
            {
                WzObject result = file.GetObjectFromPath(path, false);
                if (result != null)
                    return result;
            }
            return null;
        }


        internal bool StringMatch(string strWildCard, string strCompare)
        {
            int wildCardIndex = 0;
            int compareIndex = 0;
            int lastStarIndex = -1;
            int starMatchIndex = 0;

            // Greedy matching with backtracking to the most recent star has the same
            // semantics as the recursive matcher, while avoiding Substring allocations and
            // exponential recursion for patterns containing multiple stars.
            while (compareIndex < strCompare.Length)
            {
                if (wildCardIndex < strWildCard.Length &&
                    strWildCard[wildCardIndex] == strCompare[compareIndex])
                {
                    wildCardIndex++;
                    compareIndex++;
                }
                else if (wildCardIndex < strWildCard.Length && strWildCard[wildCardIndex] == '*')
                {
                    lastStarIndex = wildCardIndex++;
                    starMatchIndex = compareIndex;
                }
                else if (lastStarIndex >= 0)
                {
                    wildCardIndex = lastStarIndex + 1;
                    compareIndex = ++starMatchIndex;
                }
                else
                {
                    return false;
                }
            }

            while (wildCardIndex < strWildCard.Length && strWildCard[wildCardIndex] == '*')
                wildCardIndex++;

            return wildCardIndex == strWildCard.Length;
        }

        public override void Remove()
        {
            Dispose();
        }
    }
}
