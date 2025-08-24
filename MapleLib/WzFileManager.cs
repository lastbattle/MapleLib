using MapleLib.Helpers;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using System.Diagnostics;
using System.IO.Packaging;

namespace MapleLib {

    public class WzFileManager : IDisposable {
        #region Constants
        private static readonly string[] EXCLUDED_DIRECTORY_FROM_WZ_LIST = { "bak", "backup", "original", "xml", "hshield", "blackcipher", "harepacker", "hacreator" };

        public static readonly string[] COMMON_MAPLESTORY_DIRECTORY = new string[] {
            @"C:\Nexon\MapleStory",
            @"D:\Nexon\Maple",
            @"C:\Program Files\WIZET\MapleStory",
            @"C:\MapleStory",
            @"C:\NEXPACE\MapleStoryN",
            @"C:\Program Files (x86)\Wizet\MapleStorySEA"
        };
        public static string CANVAS_DIRECTORY_NAME = "_Canvas";
        private static bool IsListWz(string inBaseName)
        {
            return inBaseName == "list";
        }
        #endregion

        #region Fields
        public static WzFileManager fileManager; // static, to allow access from anywhere

        private readonly string baseDir;
        /// <summary>
        /// Gets the base directory of the WZ file.
        /// Returns the "Data" folder if 64-bit client.
        /// </summary>
        /// <returns></returns>
        public string WzBaseDirectory {
            get { return this._bInitAs64Bit ? (baseDir + "\\Data\\") : baseDir; }
            private set { }
        }

        private readonly bool _bIsStandAloneWzFile;

        private readonly bool _bInitAs64Bit;
        public bool Is64Bit {
            get { return _bInitAs64Bit; }
            private set { }
        }

        private readonly bool _bIsPreBBDataWzFormat;
        /// <summary>
        /// Defines if the currently loaded WZ directory are in the pre-BB format with only Data.wz (beta version?)
        /// </summary>
        public bool IsPreBBDataWzFormat {
            get { return _bIsPreBBDataWzFormat; }
            private set { }
        }


        private readonly ReaderWriterLockSlim _readWriteLock = new(); // for '_wzFiles', '_wzFilesUpdated', '_updatedImages', & '_wzDirs'
        private readonly Dictionary<string, WzFile> _wzFiles = [];
        private readonly Dictionary<WzFile, bool> _wzFilesUpdated = []; // key = WzFile, flag for the list of WZ files changed to be saved later via Repack
        private readonly Dictionary<string, WzMainDirectory> _wzDirs = [];

        private readonly Dictionary<string, WzImage> _wzImages = []; // The raw wz images loaded in memory.. Hotfix Data.wz or raw .img
        private readonly Dictionary<WzImage, bool> _wzImagesUpdated = [];

        private readonly HashSet<WzImage> _updatedWzImages = [];

        /// <summary>
        /// The list of list.wz image paths
        /// </summary>
        private readonly List<string> _listWzPaths = [];


        /// <summary>
        /// The list of sub wz files.
        /// Key, <List of files, directory path>
        /// i.e sound.wz expands to the list array of "Mob001", "Mob2"
        ///
        /// {[Map\Map\Map4, Count = 1]}
        /// </summary>
        private readonly Dictionary<string, List<string>> _wzFilesList = [];
        /// <summary>
        /// The list of directory where the wz file residues
        /// </summary>
        private readonly Dictionary<string, string> _wzFilesDirectoryList = [];

        #endregion

        #region Constructor
        /// <summary>
        /// Constructor to init WzFileManager for HaRepacker
        /// </summary>
        public WzFileManager() {
            this.baseDir = string.Empty;
            this._bInitAs64Bit = false;
            this._bIsStandAloneWzFile = false;

            fileManager = this;
        }

        /// <summary>
        /// Constructor to init WzFileManager for HaCreator
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="bIsStandAloneWzFile"></param>
        public WzFileManager(string directory, bool bIsStandAloneWzFile) {
            this.baseDir = directory;
            this._bIsStandAloneWzFile = bIsStandAloneWzFile;

            // interpret it as a stand-alone WZ file instead of MapleStory directory
            if (bIsStandAloneWzFile)
            {
                this._bInitAs64Bit = false;
                this._bIsPreBBDataWzFormat = false;
            }
            else
            {
                this._bInitAs64Bit = WzFileManager.Detect64BitDirectoryWzFileFormat(this.baseDir); // set
                this._bIsPreBBDataWzFormat = WzFileManager.DetectIsPreBBDataWZFileFormat(this.baseDir); // set
            }
            fileManager = this;
        }
        #endregion

        #region Loader
        /// <summary>
        /// Automagically detect if the following directory where MapleStory installation is saved
        /// is a 64-bit wz directory
        /// </summary>
        /// <returns></returns>
        public static bool Detect64BitDirectoryWzFileFormat(string baseDirectoryPath) {
            if (!Directory.Exists(baseDirectoryPath))
                throw new Exception("Non-existent directory provided.");

            string dataDirectoryPath = Path.Combine(baseDirectoryPath, "Data");
            bool bDirectoryContainsDataDir = Directory.Exists(dataDirectoryPath);

            if (bDirectoryContainsDataDir) {
                // Use a regular expression to search for .wz files in the Data directory
                string searchPattern = @"*.wz";
                int nNumWzFilesInDataDir = Directory.EnumerateFileSystemEntries(dataDirectoryPath, searchPattern, SearchOption.AllDirectories).Count();

                if (nNumWzFilesInDataDir > 40)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Automagically detect if the following directory where MapleStory installation is saved
        /// is a pre-bb WZ with only Data.wz
        /// </summary>
        /// <returns></returns>
        public static bool DetectIsPreBBDataWZFileFormat(string baseDirectoryPath) {
            if (!Directory.Exists(baseDirectoryPath))
                throw new Exception("Non-existent directory provided.");

            // Check if the directory contains a "Data.wz" file
            string dataWzFilePath = Path.Combine(baseDirectoryPath, "Data.wz");
            bool bDirectoryContainsDataWz = File.Exists(dataWzFilePath);
            if (bDirectoryContainsDataWz) {
                // Check if Skill.wz, String.wz, Character.wz exist in the base directory
                string skillWzFilePath = Path.Combine(baseDirectoryPath, "Skill.wz");
                string stringWzFilePath = Path.Combine(baseDirectoryPath, "String.wz");
                string characterWzFilePath = Path.Combine(baseDirectoryPath, "Character.wz");

                bool skillWzExist = File.Exists(skillWzFilePath);
                bool stringWzExist = File.Exists(stringWzFilePath);
                bool characterWzExist = File.Exists(characterWzFilePath);

                if (!skillWzExist && !stringWzExist && !characterWzExist) {
                    // Check if "Data" directory contains a "Character", "Skill", or "String" directory
                    // to filter for 64-bit wz maplestory
                    string skillDirectoryPath = Path.Combine(baseDirectoryPath, "Data", "Skill");
                    string stringDirectoryPath = Path.Combine(baseDirectoryPath, "Data", "String");
                    string characterDirectoryPath = Path.Combine(baseDirectoryPath, "Data", "Character");

                    bool skillDirExist = Directory.Exists(skillDirectoryPath);
                    bool stringDirExist = Directory.Exists(stringDirectoryPath);
                    bool characterDirExist = Directory.Exists(characterDirectoryPath);

                    if (!skillDirExist && !stringDirExist && !characterDirExist)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Builds the list of WZ files in the MapleStory directory (for HaCreator only, not used for HaRepacker)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public void BuildWzFileList() {
            if (_wzFilesDirectoryList.Count != 0) // dont load again
                return;

            bool b64BitClient = this._bInitAs64Bit;
            string baseDir = WzBaseDirectory;
            if (b64BitClient) {
                // parse through "Data" directory and iterate through every folder
                // Use Where() and Select() to filter and transform the directories
                var directories = Directory.EnumerateDirectories(baseDir, "*", SearchOption.AllDirectories)
                                           .Where(dir => !EXCLUDED_DIRECTORY_FROM_WZ_LIST.Any(x => dir.ToLower().Contains(x)));

                // Iterate over the filtered and transformed directories
                foreach (string dir in directories) {
                    //string folderName = new DirectoryInfo(Path.GetDirectoryName(dir)).Name.ToLower();
                    //Debug.WriteLine("----");
                    //Debug.WriteLine(dir);

                    string[] iniFiles = Directory.GetFiles(dir, "*.ini");
                    if (iniFiles.Length <= 0 || iniFiles.Length > 1)
                        throw new Exception(".ini file at the directory '" + dir + "' is missing, or unavailable.");

                    string iniFile = iniFiles[0];
                    if (!File.Exists(iniFile))
                        throw new Exception(".ini file at the directory '" + dir + "' is missing.");
                    else {
                        string[] iniFileLines = File.ReadAllLines(iniFile);
                        if (iniFileLines.Length <= 0)
                            throw new Exception(".ini file does not contain LastWzIndex information.");

                        string[] iniFileSplit = iniFileLines[0].Split('|');
                        if (iniFileSplit.Length <= 1)
                            throw new Exception(".ini file does not contain LastWzIndex information.");

                        int index = int.Parse(iniFileSplit[1]);

                        for (int i = 0; i <= index; i++) {
                            string partialWzFilePath = string.Format(iniFile.Replace(".ini", "_{0}.wz"), i.ToString("D3")); // 3 padding '0's
                            string fileName = Path.GetFileName(partialWzFilePath);
                            string fileName2 = fileName.Replace(".wz", "");

                            string wzDirectoryNameOfWzFile = dir.Replace(baseDir, "").ToLower();

                            if (EXCLUDED_DIRECTORY_FROM_WZ_LIST.Any(item => fileName2.ToLower().Contains(item)))
                                continue; // backup files

                            //Debug.WriteLine(partialWzFileName);
                            //Debug.WriteLine(wzDirectoryOfWzFile);

                            if (_wzFilesList.ContainsKey(wzDirectoryNameOfWzFile))
                                _wzFilesList[wzDirectoryNameOfWzFile].Add(fileName2);
                            else
                                _wzFilesList.Add(wzDirectoryNameOfWzFile, new List<string> { fileName2 });

                            // check if its a canvas directory
                            bool bIsCanvasDir = ContainsCanvasDirectory(partialWzFilePath);
                            if (!bIsCanvasDir)
                            {
                                // key looks like this: "skill", "mob_001"
                                if (!_wzFilesDirectoryList.ContainsKey(fileName2))
                                    _wzFilesDirectoryList.Add(fileName2, dir);
                                else
                                {
                                }
                            }
                            else
                            {
                                // key looks like this if its canvas: "character\\_canvas\\_Canvas_000"
                                string canvasDirKeyName = Path.Combine(wzDirectoryNameOfWzFile, fileName2.ToLower()).Replace(@"\", @"/");
                                if (!_wzFilesDirectoryList.ContainsKey(canvasDirKeyName))
                                    _wzFilesDirectoryList.Add(canvasDirKeyName, dir);
                            }
                        }
                    }
                }
            }
            else
            {
                var wzFilePathNames = Directory.EnumerateFileSystemEntries(baseDir, "*.wz", SearchOption.AllDirectories)
                    .Where(f => !File.GetAttributes(f).HasFlag(FileAttributes.Directory) // exclude directories
                                && !EXCLUDED_DIRECTORY_FROM_WZ_LIST.Any(x => x.ToLower() == new DirectoryInfo(Path.GetDirectoryName(f)).Name)); // exclude folders
                foreach (string wzFilePathName in wzFilePathNames) {
                    //string folderName = new DirectoryInfo(Path.GetDirectoryName(wzFileName)).Name;
                    string directory = Path.GetDirectoryName(wzFilePathName);

                    string fileName = Path.GetFileName(wzFilePathName);
                    string fileName2 = fileName.Replace(".wz", "");

                    // Mob2, Mob001, Map001, Map002
                    // remove the numbers to get the base name 'map'
                    string wzBaseFileName = new string(fileName2.ToLower().Where(c => char.IsLetter(c)).ToArray());

                    if (_wzFilesList.ContainsKey(wzBaseFileName))
                        _wzFilesList[wzBaseFileName].Add(fileName2);
                    else
                        _wzFilesList.Add(wzBaseFileName, new List<string> { fileName2 });

                    if (!_wzFilesDirectoryList.ContainsKey(fileName2))
                        _wzFilesDirectoryList.Add(fileName2, directory);
                }
            }
        }

        /// <summary>
        /// Load the list.wz file
        /// </summary>
        /// <param name="fileVersion"></param>
        /// <returns></returns>
        public bool LoadListWzFile(WzMapleVersion fileVersion) {
            if (!Is64Bit) {
                if (_listWzPaths.Count > 0) // already loaded
                    return true;

                const string listWzBaseName = "List";
                try {
                    string filePath = GetWzFilePath(listWzBaseName);

                    List<string> listEntries = ListFileParser.ParseListFile(filePath, fileVersion);
                    _listWzPaths.AddRange(listEntries);

                    //string combined = string.Join(", ", _listWzPaths);
                    //Debug.WriteLine(combined);
                    return true;
                }
                catch { }
            }
            return false;
        }

        /// <summary>
        /// Is the WZ file basename currently loaded.
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public bool IsWzFileLoaded(string baseName)
        {
            bool bIsCanvasDir = false;
            if (this.Is64Bit)
            {
                bIsCanvasDir = ContainsCanvasDirectory(baseName);
                // TODO
            }
            string fileName_ = baseName.ToLower().Replace(".wz", "");
            return _wzFiles.ContainsKey(fileName_);
        }

        /// <summary>
        /// Loads the oridinary WZ file
        /// </summary>
        /// <param name="baseName"></param>
        /// <param name="encVersion"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public WzFile LoadWzFile(string baseName, WzMapleVersion encVersion) {
            string filePath = GetWzFilePath(baseName);
            if (filePath == null)
                return null;
            WzFile wzf = new WzFile(filePath, encVersion);

            WzFileParseStatus parseStatus = wzf.ParseWzFile();
            if (parseStatus != WzFileParseStatus.Success) {
                throw new Exception("Error parsing " + baseName + ".wz (" + parseStatus.GetErrorDescription() + ")");
            }

            string fileName_ = baseName.ToLower().Replace(".wz", "");

            if (_wzFilesUpdated.ContainsKey(wzf)) // some safety check
                throw new Exception(string.Format("Wz {0} at the path {1} has already been loaded, and cannot be loaded again. Remove it from memory first.", fileName_, wzf.FilePath));
            
            // write lock to begin adding to the dictionary
            _readWriteLock.EnterWriteLock();
            try {
                _wzFiles[fileName_] = wzf;
                _wzFilesUpdated[wzf] = false;
                _wzDirs[fileName_] = new WzMainDirectory(wzf);
            }
            finally {
                _readWriteLock.ExitWriteLock();
            }
            return wzf;
        }

        /// <summary>
        /// Loads the Data.wz file (Legacy MapleStory WZ before version 30)
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public bool LoadLegacyDataWzFile(string baseName, WzMapleVersion encVersion) {
            string filePath = GetWzFilePath(baseName);
            WzFile wzf = new WzFile(filePath, encVersion);

            WzFileParseStatus parseStatus = wzf.ParseWzFile();
            if (parseStatus != WzFileParseStatus.Success) {
                MessageBox.Show("Error parsing " + baseName + ".wz (" + parseStatus.GetErrorDescription() + ")");
                return false;
            }

            baseName = baseName.ToLower();

            if (_wzFilesUpdated.ContainsKey(wzf)) // some safety check
                throw new Exception(string.Format("Wz file {0} at the path {1} has already been loaded, and cannot be loaded again.", baseName, wzf.FilePath));

            // write lock to begin adding to the dictionary
            _readWriteLock.EnterWriteLock();
            try {
                _wzFiles[baseName] = wzf;
                _wzFilesUpdated[wzf] = false;
                _wzDirs[baseName] = new WzMainDirectory(wzf);
            }
            finally {
                _readWriteLock.ExitWriteLock();
            }

            foreach (WzDirectory mainDir in wzf.WzDirectory.WzDirectories) {
                _wzDirs[mainDir.Name.ToLower()] = new WzMainDirectory(wzf, mainDir);
            }
            return true;
        }

        /// <summary>
        /// Loads the hotfix Data.wz file
        /// </summary>
        /// <param name="basePath"></param>
        /// <param name="encVersion"></param>
        /// <param name="panel"></param>
        /// <returns></returns>
        public WzImage LoadDataWzHotfixFile(string basePath, WzMapleVersion encVersion) {
            string filePath = GetWzFilePath(basePath);
            if (!File.Exists(filePath))
            {
                throw new Exception(string.Format("File '{0}' does not exist", basePath));
            }

            FileStream fs = File.Open(filePath, FileMode.Open); // dont close this file stream until it is unloaded from memory

            WzImage img = new WzImage(Path.GetFileName(filePath), fs, encVersion);
            img.ParseImage(true);

            // write lock to begin adding to the dictionary
            _readWriteLock.EnterWriteLock();
            try
            {
                _wzImages[basePath] = img; // store the image in the dictionary
                _wzImagesUpdated[img] = false; // set the image as not updated
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }

            return img;
        }
        #endregion

        #region Loaded Items
        /// <summary>
        /// Sets WZ file as updated for saving
        /// </summary>
        /// <param name="name"></param>
        /// <param name="img"></param>
        public void SetWzFileUpdated(string name, WzImage img) {
            img.Changed = true;
            _updatedWzImages.Add(img);

            WzFile wzFile = GetMainDirectoryByName(name).File;
            SetWzFileUpdated(wzFile);
        }

        /// <summary>
        /// Sets WZ file as updated for saving
        /// </summary>
        /// <param name="wzFile"></param>
        /// <exception cref="Exception"></exception>
        public void SetWzFileUpdated(WzFile wzFile) {
            if (_wzFilesUpdated.ContainsKey(wzFile)) {
                // write lock to begin adding to the dictionary
                _readWriteLock.EnterWriteLock();
                try {
                    _wzFilesUpdated[wzFile] = true;
                }
                finally {
                    _readWriteLock.ExitWriteLock();
                }
            }
            else
                throw new Exception("wz file to be flagged do not exist in memory " + wzFile.FilePath);
        }

        /// <summary>
        /// Gets the list of updated or changed WZ files.
        /// </summary>
        /// <returns></returns>
        public List<WzFile> GetUpdatedWzFiles() {
            List<WzFile> updatedWzFiles = new List<WzFile>();
            // readlock
            _readWriteLock.EnterReadLock();
            try {
                IEnumerable<WzFile> changedList = _wzFilesUpdated.Where(keyPair => keyPair.Value == true).Select(keyPair => keyPair.Key).AsEnumerable();
                updatedWzFiles.AddRange(changedList);
            }
            finally {
                _readWriteLock.ExitReadLock();
            }
            return updatedWzFiles;
        }

        /// <summary>
        /// Unload the wz file from memory
        /// </summary>
        /// <param name="wzFile"></param>
        public void UnloadWzFile(WzFile wzFile, string wzFilePath) {
            string baseName = wzFilePath.ToLower().Replace(".wz", "");
            if (_wzFiles.ContainsKey(baseName)) {
                // write lock to begin adding to the dictionary
                _readWriteLock.EnterWriteLock();
                try {
                    _wzFiles.Remove(baseName);
                    _wzFilesUpdated.Remove(wzFile);
                    _wzDirs.Remove(baseName);
                }
                finally {
                    _readWriteLock.ExitWriteLock();
                }
                wzFile.Dispose();
            }
        }

        /// <summary>
        /// Unload the wz image file from memory
        /// </summary>
        /// <param name="wzFile"></param>
        public void UnloadWzImgFile(WzImage wzImage)
        {
            string baseName = _wzImages.FirstOrDefault(kvp => kvp.Value == wzImage).Key;

            if (_wzImages.ContainsKey(baseName))
            {
                // write lock to begin adding to the dictionary
                _readWriteLock.EnterWriteLock();
                try
                {
                    _wzImages.Remove(baseName);
                    _wzImagesUpdated.Remove(wzImage);
                }
                finally
                {
                    _readWriteLock.ExitWriteLock();
                }
                wzImage.Dispose();
            }
        }
        #endregion

        #region Inherited Members
        /// <summary>
        /// Dispose when shutting down the application
        /// </summary>
        public void Dispose() {
            _readWriteLock.EnterWriteLock();
            try {
                foreach (WzFile wzf in _wzFiles.Values) {
                    wzf.Dispose();
                }
                _wzFiles.Clear();
                _wzFilesUpdated.Clear();
                _updatedWzImages.Clear();
                _wzDirs.Clear();
            }
            finally {
                _readWriteLock.ExitWriteLock();
            }
        }
        #endregion

        #region Custom Members
        public WzDirectory this[string name] {
            get {
                return _wzDirs.ContainsKey(name.ToLower()) ? _wzDirs[name.ToLower()].MainDir : null;
            } //really not very useful to return null in this case
        }

        /// <summary>
        /// Gets a read-only list of loaded WZ files in the WzFileManager
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<WzFile> WzFileList {
            get { return new List<WzFile>(this._wzFiles.Values).AsReadOnly(); }
            private set { }
        }

        /// <summary>
        /// Gets a read-only list of loaded WZ files in the WzFileManager
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<WzImage> WzUpdatedImageList {
            get { return new List<WzImage>(this._updatedWzImages).AsReadOnly(); }
            private set { }
        }

        /// <summary>
        /// Gets a read-only list of loaded image files in the WzFileManager
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<WzImage> WzImagesList
        {
            get { return new List<WzImage>(this._wzImages.Values).AsReadOnly(); }
            private set { }
        }
        #endregion

        #region Finder
        /// <summary>
        /// Checks if the directory path contains "_Canvas"
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool ContainsCanvasDirectory(string path)
        {
            path = path.ToLower();
            string canvasDirLower = CANVAS_DIRECTORY_NAME.ToLower();
            return Path.GetDirectoryName(path)?
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(dir => dir == canvasDirLower) ?? false;
        }

        /// <summary>
        /// Transform the path to canvas dictionary header str
        /// </summary>
        /// <param name="filePathOrBaseFileName"></param>
        /// <returns></returns>
        public static string NormaliseWzCanvasDirectory(string filePathOrBaseFileName) {
            // Step 1: Extract the part before "_Canvas"
            string beforeCanvasPath = Regex.Match(filePathOrBaseFileName, string.Format(@"^.*?(?=)/{0}", CANVAS_DIRECTORY_NAME)).Value.ToLower();
            return beforeCanvasPath; // "map/_canvas"
        }

        /// <summary>
        /// Gets WZ by name from the list of loaded files
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public WzMainDirectory GetMainDirectoryByName(string name) {
            name = name.ToLower();

            if (name.EndsWith(".wz"))
                name = name.Replace(".wz", "");

            return _wzDirs[name];
        }

        /// <summary>
        /// Get the list of sub wz files by its base name ("mob")
        /// i.e 'mob' expands to the list array of files "Mob001", "Mob2"
        /// exception: returns Data.wz regardless for pre-bb beta maplestory
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public List<string> GetWzFileNameListFromBase(string baseName) {
            if (_bIsPreBBDataWzFormat) {
                if (!_wzFilesList.ContainsKey("data"))
                    return new List<string>(); // return as an empty list if none
                return _wzFilesList["data"];
            }
            else {
                if (!_wzFilesList.ContainsKey(baseName))
                    return new List<string>(); // return as an empty list if none
                return _wzFilesList[baseName];
            }
        }

        /// <summary>
        /// Get the list of sub wz directories by its base name ("mob")
        /// </summary>
        /// <param name="baseName"></param>
        /// <param name="isCanvas"></param>
        /// <returns></returns>
        public List<WzDirectory> GetWzDirectoriesFromBase(string baseName, bool isCanvas = false) {
            List<string> wzDirs = GetWzFileNameListFromBase(baseName); // {[character\pants\_canvas, Count = 1]}
            // Use Select() and Where() to transform and filter the WzDirectory list
            if (_bIsPreBBDataWzFormat) {
                return wzDirs
                    .Select(name => this["data"][baseName] as WzDirectory)
                    .Where(dir => dir != null)
                    .ToList();
            }
            else {
                if (isCanvas)
                {
                    string canvasDir = baseName.Replace(@"\", @"/") + @"/";
                    return wzDirs
                        .Select(name => this[canvasDir + name])
                        .Where(dir => dir != null)
                        .ToList();
                } else {
                    return wzDirs
                        .Select(name => this[name])
                        .Where(dir => dir != null)
                        .ToList();
                }
            }
        }

        /// <summary>
        /// Finds the wz image within the multiple wz files (by the base wz name)
        /// </summary>
        /// <param name="baseWzName"></param>
        /// <param name="imageName">Matches any if string.empty.</param>
        /// <returns></returns>
        public WzObject FindWzImageByName(string baseWzName, string imageName) {
            baseWzName = baseWzName.ToLower();

            List<WzDirectory> dirs = GetWzDirectoriesFromBase(baseWzName);
            // Use Where() and FirstOrDefault() to filter the WzDirectories and find the first matching WzObject
            WzObject image;
            if (imageName != string.Empty) {
                image = dirs
                        .Where(wzFile => wzFile != null && wzFile[imageName] != null)
                        .Select(wzFile => wzFile[imageName])
                        .FirstOrDefault();
            }
            else {
                image = dirs
                        .Where(wzFile => wzFile != null)
                        .FirstOrDefault();
            }

            return image;
        }

        /// <summary>
        /// Finds the wz image within the multiple wz files (by the base wz name)
        /// </summary>
        /// <param name="baseWzName"></param>
        /// <param name="imageName">Matches any if string.empty.</param>
        /// <returns></returns>
        public List<WzObject> FindWzImagesByName(string baseWzName, string imageName) {
            baseWzName = baseWzName.ToLower();

            List<WzDirectory> dirs = GetWzDirectoriesFromBase(baseWzName);
            // Use Where() and FirstOrDefault() to filter the WzDirectories and find the first matching WzObject
            return dirs
                .Where(wzFile => wzFile != null && wzFile[imageName] != null)
                .Select(wzFile => wzFile[imageName])
                .ToList();
        }

        /// <summary>
        /// Gets the wz file path by its base name, or check if it is a file path.
        /// </summary>
        /// <param name="filePathOrBaseFileName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string GetWzFilePath(string filePathOrBaseFileName) {
            // find the base directory from 'wzFilesList'
            if (!_wzFilesDirectoryList.ContainsKey(filePathOrBaseFileName))  // if the key is not found, it might be a path instead
            {
                if (File.Exists(filePathOrBaseFileName)) // [142] = {[map\\_canvas\\_canvas_000, D:\Installations\MapleOrigin\Data\Map\_Canvas]}
                    return filePathOrBaseFileName;
                //throw new Exception("Couldnt find the directory key for the wz file " + filePathOrBaseFileName);
                return null;
            }

            if (!WzFileManager.ContainsCanvasDirectory(filePathOrBaseFileName))
            {
                string filePath_half = StringUtility.CapitalizeFirstCharacter(filePathOrBaseFileName) + ".wz";
                string fileName = Path.GetFileName(filePath_half);
                string filePath = Path.Combine(_wzFilesDirectoryList[filePathOrBaseFileName], fileName);
                if (!File.Exists(filePath))
                    throw new Exception("wz file at the path '" + filePath + "' does not exist.");
                return filePath;
            } else
            {
                // Map/_Canvas/Canvas_000 -> Map\_Canvas\Canvas_000.wz
                string fileDir =_wzFilesDirectoryList[filePathOrBaseFileName];
                string fileName = Path.GetFileName(filePathOrBaseFileName);
                string filePath = Path.Combine(fileDir, fileName + ".wz");

                if (!File.Exists(filePath))
                    throw new Exception("canvas wz file at the path '" + filePath + "' does not exist.");
                return filePath;

            }
            throw new Exception(string.Format("Canvas directory at '{0}' does not exist.", filePathOrBaseFileName));
        }
        #endregion
    }
}