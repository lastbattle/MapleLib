using System.Collections.Generic;
using System.IO;
using MapleLib.WzLib.Util;
using System;
using System.Diagnostics;
using MapleLib.PacketLib;
using MapleLib.WzLib.WzStructure.Enums;
using MapleLib.WzLib.WzProperties;
using System.Linq;
using System.Buffers;

namespace MapleLib.WzLib
{
    /// <summary>
    /// A directory in the wz file, which may contain sub directories or wz images
    /// </summary>
    public class WzDirectory : WzObject
    {
        // Directory records are attacker-controlled and recursively reference
        // one another by absolute stream offset.  Keep malformed chains from
        // exhausting the process stack even when every offset is unique.
        private const int MaxDirectoryDepth = 256;

        #region Fields
        private List<WzImage> images = new List<WzImage>();
        internal List<WzDirectory> subDirs = new List<WzDirectory>();
        private Dictionary<string, NameIndexEntry<WzImage>> imageIndex =
            new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, NameIndexEntry<WzDirectory>> directoryIndex =
            new(StringComparer.OrdinalIgnoreCase);
        private NameIndexEntry<WzImage>? nullImageIndex;
        private NameIndexEntry<WzDirectory>? nullDirectoryIndex;
        internal WzBinaryReader reader;
        internal long offset = 0;
        internal string name;
        internal uint hash;
        internal int size, checksum, offsetSize;
        internal byte[] WzIv;
        internal WzObject parent;
        internal WzFile wzFile;

        private struct NameIndexEntry<T> where T : WzObject
        {
            internal T First;
            internal int DuplicateCount;

            internal NameIndexEntry(T first)
            {
                First = first;
            }
        }
        #endregion

        #region Inherited Members
        /// <summary>  
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get { return parent; } internal set { parent = value; } }
        /// <summary>
        /// The name of the directory
        /// </summary>
        public override string Name { get { return name; } set { name = value; } }
        /// <summary>
        /// The WzObjectType of the directory
        /// </summary>
        public override WzObjectType ObjectType { get { return WzObjectType.Directory; } }

        public override WzFile WzFileParent
        {
            get { return wzFile; }
        }

        /// <summary>
        /// Disposes the obejct
        /// </summary>
        public override void Dispose()
        {
            name = null;
            reader = null;
            if (images != null)
            {
                foreach (WzImage img in images)
                    img.Dispose();
                images.Clear();
            }
            imageIndex.Clear();
            nullImageIndex = null;
            if (subDirs != null)
            {
                foreach (WzDirectory dir in subDirs)
                    dir.Dispose();
                subDirs.Clear();
            }
            directoryIndex.Clear();
            nullDirectoryIndex = null;
            images = null;
            subDirs = null;
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// The size of the directory in the wz file
        /// </summary>
        public int BlockSize { get { return size; } set { size = value; } }
        /// <summary>
        /// The directory's chceksum
        /// </summary>
        public int Checksum { get { return checksum; } set { checksum = value; } }
        /// <summary>
        /// The wz images contained in the directory
        /// </summary>
        public virtual List<WzImage> WzImages { get { return images; } private set { } }
        /// <summary>
        /// The sub directories contained in the directory
        /// </summary>
        public virtual List<WzDirectory> WzDirectories { get { return subDirs; } }
        /// <summary>
        /// Offset of the folder
        /// </summary>
        public long Offset { get { return offset; } set { offset = value; } }
        /// <summary>
        /// Returns a WzImage or a WzDirectory with the given name
        /// </summary>
        /// <param name="name">The name of the img or dir to find</param>
        /// <returns>A WzImage or WzDirectory</returns>
        public virtual new WzObject this[string name]
        {
            get
            {
                WzImage image = GetImageByName(name);
                if (image != null)
                    return image;

                WzDirectory directory = GetDirectoryByName(name);
                if (directory != null)
                    return directory;

                //throw new KeyNotFoundException("No wz image or directory was found with the specified name");
                return null;
            }
            set
            {
                if (value != null)
                {
                    value.Name = name;
                    if (value is WzDirectory directory)
                        AddDirectory(directory);
                    else if (value is WzImage image)
                        AddImage(image);
                    else
                        throw new ArgumentException("Value must be a Directory or Image");
                }
            }
        }



        /// <summary>
        /// Creates a blank WzDirectory
        /// </summary>
        public WzDirectory() { }
        /// <summary>
        /// Creates a WzDirectory with the given name
        /// </summary>
        /// <param name="dirName">The name of the directory</param>
        public WzDirectory(string dirName)
        {
            this.name = dirName;
        }

        public WzDirectory(string dirName, WzFile parentWzFileIvVerHashCloneSource)
        {
            this.name = dirName;
            this.hash = parentWzFileIvVerHashCloneSource.versionHash;
            this.WzIv = parentWzFileIvVerHashCloneSource.WzIv;
            this.wzFile = parentWzFileIvVerHashCloneSource;
        }

        /// <summary>
        /// Creates a WzDirectory
        /// </summary>
        /// <param name="reader">The BinaryReader that is currently reading the wz file</param>
        /// <param name="blockStart">The start of the data block</param>
        /// <param name="parentname">The name of the directory</param>
        /// <param name="wzFile">The parent Wz File</param>
        internal WzDirectory(WzBinaryReader reader, string dirName, uint verHash, byte[] WzIv, WzFile wzFile)
        {
            this.reader = reader;
            this.name = dirName;
            this.hash = verHash;
            this.WzIv = WzIv;
            this.wzFile = wzFile;
        }

        /// <summary>
        /// Parses the WzDirectory
        /// <paramref name="lazyParse">Only parses the first directory</paramref>
        /// </summary>
        internal void ParseDirectory(bool lazyParse = false)
        {
            ParseDirectory(lazyParse, 0, new HashSet<long>());
        }

        private void ParseDirectory(bool lazyParse, int depth, HashSet<long> visitedOffsets)
        {
            if (depth > MaxDirectoryDepth)
                throw new InvalidDataException("WZ directory nesting exceeds the supported depth.");

            long directoryPosition = reader.BaseStream.Position;
            if (directoryPosition < 0 || directoryPosition > reader.BaseStream.Length)
                throw new InvalidDataException("WZ directory offset is outside the stream.");

            if (!visitedOffsets.Add(directoryPosition))
                throw new InvalidDataException("WZ directory contains a cyclic offset.");

            //reader.PrintHexBytes(20);
            long available = reader.Available();
            if (available < 0)
                throw new InvalidDataException("WZ directory position is outside the stream.");
            if (available == 0)
                return;

            int entryCount = reader.ReadCompressedInt();
            if (entryCount < 0 || entryCount > 100000 || (long)entryCount > reader.Available()) // probably nothing > 100k folders for now.
                throw new InvalidDataException("Invalid WZ directory entry count.");

            for (int i = 0; i < entryCount; i++)
            {
                byte type = reader.ReadByte(); // see WzBinaryWriter.WriteWzObjectValue
                string fname = null;
                int fsize;
                int checksum;
                long offset;

                long rememberPos = 0;
                switch (type)
                {
                    case (byte) WzDirectoryType.UnknownType_1:  //01 XX 00 00 00 00 00 OFFSET (4 bytes) 
                        {
                            int unknown = reader.ReadInt32();
                            reader.ReadInt16();
                            long offs = reader.ReadOffset();
                            continue;
                        }
                    case (byte) WzDirectoryType.RetrieveStringFromOffset_2:
                        {
                            int stringOffset = reader.ReadInt32();
                            rememberPos = reader.BaseStream.Position;

                            // For 64-bit WZ files (no version header), the string offset needs +1 adjustment
                            int extraOffset = (wzFile != null && wzFile.Is64BitWzFile) ? 1 : 0;
                            long stringPosition = (long)reader.Header.FStart + stringOffset + extraOffset;

                            if (stringPosition < 0 || stringPosition >= reader.BaseStream.Length)
                                throw new InvalidDataException("WZ directory string offset is outside the stream.");

                            reader.BaseStream.Position = stringPosition;

                            type = reader.ReadByte();
                            fname = reader.ReadString();

                            if (type != (byte)WzDirectoryType.WzDirectory_3 && type != (byte)WzDirectoryType.WzImage_4)
                                throw new InvalidDataException("WZ directory string entry has an invalid type.");
                            break;
                        }
                    case (byte) WzDirectoryType.WzDirectory_3:
                    case (byte) WzDirectoryType.WzImage_4:
                        {
                            fname = reader.ReadString();
                            rememberPos = reader.BaseStream.Position;
                            break;
                        }
                    default:
                        {
                            reader.PrintHexBytes(20);
                            throw new Exception("[WzDirectory] Unknown directory. type = " + type);
                        }
                }
                reader.BaseStream.Position = rememberPos;
                fsize = reader.ReadCompressedInt();
                checksum = reader.ReadCompressedInt();
                offset = reader.ReadOffset(); // IWzArchive::Getposition(pArchive)

                if (fsize < 0)
                    throw new InvalidDataException("WZ directory entry has a negative block size.");
                if (offset < 0 || offset > reader.BaseStream.Length)
                    throw new InvalidDataException("WZ directory entry offset is outside the stream.");
                if (fsize > reader.BaseStream.Length - offset)
                    throw new InvalidDataException("WZ directory entry block exceeds the containing stream.");

                if (type == (byte) WzDirectoryType.WzDirectory_3)
                {
                    WzDirectory subDir = new WzDirectory(reader, fname, hash, WzIv, wzFile)
                    {
                        BlockSize = fsize,
                        Checksum = checksum,
                        Offset = offset,
                        Parent = this
                    };
                    AddDirectory(subDir);

                    if (lazyParse)
                        break;
                }
                else
                {
                    WzImage img = new WzImage(fname, reader, checksum)
                    {
                        BlockSize = fsize,
                        Offset = offset,
                        Parent = this
                    };
                    AddImage(img);
                    //Debug.WriteLine("Adding image: " + fname);

                    if (lazyParse)
                        break;
                }
            }

            foreach (WzDirectory subdir in subDirs)
            {
                reader.BaseStream.Position = subdir.offset;
                subdir.ParseDirectory(false, depth + 1, visitedOffsets);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wzWriter"></param>
        /// <param name="fs"></param>
        internal void SaveImages(BinaryWriter wzWriter, FileStream fs)
        {
            // List<string> wzImageNameTracking = new List<string>(); // Check for duplicate WZ image name that could cause errors later on.

            foreach (WzImage img in images)
            {
                // Check for duplicate WZ image name that could cause errors later on.
                // this will only be warning to the user, but it'll still save it fine
                //if (wzImageNameTracking.Contains(img.Name))
                //Debug.WriteLine("Duplicate img detected. Parent: {0}, Name: {1}", img.Parent.Name, img.Name);
                // else
                //wzImageNameTracking.Add(img.Name);

                // Write 
                if (img.Changed)
                {
                    fs.Position = img.tempFileStart;
                    CopyBytes(fs, wzWriter, img.size);
                }
                else
                {
                    img.reader.BaseStream.Position = img.tempFileStart;
                    CopyBytes(img.reader.BaseStream, wzWriter, img.tempFileEnd - img.tempFileStart);
                }
            }
            foreach (WzDirectory dir in subDirs)
            {
                dir.SaveImages(wzWriter, fs);
            }
        }

        private static void CopyBytes(Stream source, BinaryWriter destination, long byteCount)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(81920, byteCount));
            try
            {
                while (byteCount > 0)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, byteCount);
                    source.ReadExactly(buffer, 0, bytesToRead);
                    destination.Write(buffer, 0, bytesToRead);
                    byteCount -= bytesToRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="useIv">The IV to use while generating the data file. If null, it'll use the WzDirectory default</param>
        /// <param name="bIsWzUserKeyDefault">Uses the default MapleStory UserKey or a custom key.</param>
        /// <param name="prevOpenedStream">The previously opened file stream</param>
        /// <returns></returns>
        internal int GenerateDataFile(byte[] useIv, bool bIsWzUserKeyDefault, FileStream prevOpenedStream)
        {
            bool useCustomIv = useIv != null; // whole shit gonna be re-written if its a custom IV specified

            size = 0;
            int entryCount = subDirs.Count + images.Count;
            if (entryCount == 0)
            {
                offsetSize = 1;
                return (size = 0);
            }
            size = WzTool.GetCompressedIntLength(entryCount);
            offsetSize = WzTool.GetCompressedIntLength(entryCount);

            foreach (WzImage img in images)
            {
                // If useCustomIv or !bIsWzUserKeyDefault is true, the image must be marked as changed
                // Without this, it won't be re-written with the new custom IV and UserKey.
                img.bIsImageChanged = img.bIsImageChanged || useCustomIv || !bIsWzUserKeyDefault;
                if (img.bIsImageChanged)
                {
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        using (WzBinaryWriter imgWriter = new WzBinaryWriter(memStream, useCustomIv ? useIv : this.WzIv))
                        {
                            img.SaveImage(imgWriter, bIsWzUserKeyDefault, useCustomIv);

                            byte[] imageBytes = memStream.ToArray();
                            img.CalculateAndSetImageChecksum(imageBytes); // checksum

                            img.tempFileStart = prevOpenedStream.Position;
                            prevOpenedStream.Write(imageBytes, 0, imageBytes.Length);
                            img.tempFileEnd = prevOpenedStream.Position;
                        }
                    }
                }
                else
                {
                    img.tempFileStart = img.offset;
                    img.tempFileEnd = img.offset + img.size;
                }
                img.UnparseImage();

                int nameLen = WzTool.GetWzObjectValueLength(img.name, 4);
                size += nameLen;
                int imgLen = img.size;
                size += WzTool.GetCompressedIntLength(imgLen);
                size += imgLen;
                size += WzTool.GetCompressedIntLength(img.Checksum);
                size += 4;
                offsetSize += nameLen;
                offsetSize += WzTool.GetCompressedIntLength(imgLen);
                offsetSize += WzTool.GetCompressedIntLength(img.Checksum);
                offsetSize += 4;

                //Debug.WriteLine("Writing image :" + img.FullPath);
            }

            foreach (WzDirectory dir in subDirs)
            {
                int nameLen = WzTool.GetWzObjectValueLength(dir.name, 3);
                size += nameLen;
                size += dir.GenerateDataFile(useIv, bIsWzUserKeyDefault, prevOpenedStream);
                size += WzTool.GetCompressedIntLength(dir.size);
                size += WzTool.GetCompressedIntLength(dir.Checksum);
                size += 4;
                offsetSize += nameLen;
                offsetSize += WzTool.GetCompressedIntLength(dir.size);
                offsetSize += WzTool.GetCompressedIntLength(dir.Checksum);
                offsetSize += 4;

                //Debug.WriteLine("Writing dir :" + dir.FullPath);
            }
            return size;
        }
        internal void SaveDirectory(WzBinaryWriter writer)
        {
            offset = (uint)writer.BaseStream.Position;
            int entryCount = subDirs.Count + images.Count;
            if (entryCount == 0)
            {
                BlockSize = 0;
                return;
            }
            writer.WriteCompressedInt(entryCount);
            foreach (WzImage img in images)
            {
                if (!writer.WriteWzObjectValue(img.name, WzDirectoryType.WzImage_4))  // true if written as an offset
                {
                }
                writer.WriteCompressedInt(img.BlockSize);
                writer.WriteCompressedInt(img.Checksum);
                writer.WriteOffset(img.Offset);
            }
            foreach (WzDirectory dir in subDirs)
            {
                if (!writer.WriteWzObjectValue(dir.name, WzDirectoryType.WzDirectory_3)) // true if written as an offset
                {
                }
                writer.WriteCompressedInt(dir.BlockSize);
                writer.WriteCompressedInt(dir.Checksum);
                writer.WriteOffset(dir.Offset);
            }
            foreach (WzDirectory dir in subDirs)
                if (dir.BlockSize > 0)
                    dir.SaveDirectory(writer);
                else
                    writer.Write((byte)0);
        }
        internal uint GetOffsets(uint curOffset)
        {
            offset = curOffset;
            curOffset += (uint)offsetSize;
            foreach (WzDirectory dir in subDirs)
            {
                curOffset = dir.GetOffsets(curOffset);
            }
            return curOffset;
        }
        internal uint GetImgOffsets(uint curOffset)
        {
            foreach (WzImage img in images)
            {
                img.Offset = curOffset;
                curOffset += (uint)img.BlockSize;
            }
            foreach (WzDirectory dir in subDirs)
            {
                curOffset = dir.GetImgOffsets(curOffset);
            }
            return curOffset;
        }
        internal void ExportXml(StreamWriter writer, bool oneFile, int level, bool isDirectory)
        {
            if (oneFile)
            {
                if (isDirectory)
                {
                    writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag("WzDirectory", this.name, true));
                }
                foreach (WzDirectory subDir in WzDirectories)
                {
                    subDir.ExportXml(writer, oneFile, level + 1, isDirectory);
                }
                foreach (WzImage subImg in WzImages)
                {
                    subImg.ExportXml(writer, oneFile, level + 1);
                }
                if (isDirectory)
                {
                    writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.CloseTag("WzDirectory"));
                }
            }
        }
        /// <summary>
        /// Parses the wz images
        /// </summary>
        public void ParseImages()
        {
            foreach (WzImage img in images)
            {
                if (reader.BaseStream.Position != img.Offset)
                {
                    reader.BaseStream.Position = img.Offset;
                }
                img.ParseImage();
            }
            foreach (WzDirectory subdir in subDirs)
            {
                if (reader.BaseStream.Position != subdir.Offset)
                {
                    reader.BaseStream.Position = subdir.Offset;
                }
                subdir.ParseImages();
            }
        }

        /// <summary>
        /// Sets the version hash of the directory (see WzFile.CreateVersionHash() )
        /// </summary>
        /// <param name="newHash"></param>
        internal void SetVersionHash(uint newHash)
        {
            this.hash = newHash;
            foreach (WzDirectory dir in subDirs)
                dir.SetVersionHash(newHash);
        }

        /// <summary>
        /// Adds a WzImage to the list of wz images
        /// </summary>
        /// <param name="img">The WzImage to add</param>
        public void AddImage(WzImage img)
        {
            images.Add(img);
            img.Parent = this;
            AddToIndex(img, imageIndex, ref nullImageIndex);
        }
        /// <summary>
        /// Adds a WzDirectory to the list of sub directories
        /// </summary>
        /// <param name="dir">The WzDirectory to add</param>
        public void AddDirectory(WzDirectory dir)
        {
            ArgumentNullException.ThrowIfNull(dir);
            if (ReferenceEquals(dir, this))
                throw new InvalidOperationException("A directory cannot contain itself.");
            for (WzDirectory ancestor = this; ancestor != null; ancestor = ancestor.Parent as WzDirectory)
            {
                if (ReferenceEquals(ancestor, dir))
                    throw new InvalidOperationException("A directory cannot be added beneath one of its descendants.");
            }
            if (dir.Parent != null && !ReferenceEquals(dir.Parent, this))
                throw new InvalidOperationException("The directory already belongs to another parent.");
            if (subDirs.Contains(dir))
                throw new InvalidOperationException("The directory is already present.");

            subDirs.Add(dir);
            dir.wzFile = wzFile;
            dir.Parent = this;
            AddToIndex(dir, directoryIndex, ref nullDirectoryIndex);
        }
        /// <summary>
        /// Clears the list of images
        /// </summary>
        public void ClearImages()
        {
            foreach (WzImage img in images) 
                img.Parent = null;
            images.Clear();
            imageIndex.Clear();
            nullImageIndex = null;
        }

        /// <summary>
        /// Clears the list of sub directories
        /// </summary>
        public void ClearDirectories()
        {
            foreach (WzDirectory dir in subDirs) 
                dir.Parent = null;
            subDirs.Clear();
            directoryIndex.Clear();
            nullDirectoryIndex = null;
        }

        /// <summary>
        /// Gets an image in the list of images by it's name
        /// </summary>
        /// <param name="name">The name of the image</param>
        /// <returns>The wz image that has the specified name or null if none was found</returns>
        public virtual WzImage GetImageByName(string name)
        {
            return FindByName(name, images, imageIndex, ref nullImageIndex);
        }

        /// <summary>
        /// Gets a sub directory in the list of directories by it's name
        /// </summary>
        /// <param name="name">The name of the directory</param>
        /// <returns>The wz directory that has the specified name or null if none was found</returns>
        public virtual WzDirectory GetDirectoryByName(string name)
        {
            return FindByName(name, subDirs, directoryIndex, ref nullDirectoryIndex);
        }

        /// <summary>
        /// Removes an image from the list
        /// </summary>
        /// <param name="image">The image to remove</param>
        public void RemoveImage(WzImage image)
        {
            ArgumentNullException.ThrowIfNull(image);
            bool removed = images.Remove(image);
            if (removed)
            {
                image.Parent = null;
                RemoveFromIndex(image, images, imageIndex, ref nullImageIndex);
            }
        }
        /// <summary>
        /// Removes a sub directory from the list
        /// </summary>
        /// <param name="name">The sub directory to remove</param>
        public void RemoveDirectory(WzDirectory dir)
        {
            ArgumentNullException.ThrowIfNull(dir);
            bool removed = subDirs.Remove(dir);
            if (removed)
            {
                dir.Parent = null;
                RemoveFromIndex(dir, subDirs, directoryIndex, ref nullDirectoryIndex);
            }
        }

        private static T FindByName<T>(string name, List<T> items,
            Dictionary<string, NameIndexEntry<T>> index,
            ref NameIndexEntry<T>? nullEntry) where T : WzObject
        {
            if (items == null)
                return null;

            if (name == null)
            {
                if (nullEntry.HasValue && string.Equals(nullEntry.Value.First?.Name, null,
                    StringComparison.Ordinal))
                    return nullEntry.Value.First;

                for (int i = 0; i < items.Count; i++)
                {
                    T item = items[i];
                    if (item?.Name == null)
                    {
                        RebuildIndex(items, index, ref nullEntry);
                        return item;
                    }
                }

                return null;
            }

            if (index.TryGetValue(name, out NameIndexEntry<T> entry))
            {
                T indexed = entry.First;
                if (string.Equals(indexed?.Name, name, StringComparison.OrdinalIgnoreCase))
                    return indexed;

                RebuildIndex(items, index, ref nullEntry);
                return index.TryGetValue(name, out entry) ? entry.First : null;
            }

            // Name is mutable and has no setter callback into the directory.
            // Repair the index only on a miss; stable hits stay dictionary probes.
            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (string.Equals(item?.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    RebuildIndex(items, index, ref nullEntry);
                    return item;
                }
            }

            return null;
        }

        private static void AddToIndex<T>(T item,
            Dictionary<string, NameIndexEntry<T>> index,
            ref NameIndexEntry<T>? nullEntry) where T : WzObject
        {
            if (item == null)
                return;

            string name = item.Name;
            if (name == null)
            {
                if (!nullEntry.HasValue)
                    nullEntry = new NameIndexEntry<T>(item);
                else
                {
                    NameIndexEntry<T> entry = nullEntry.Value;
                    entry.DuplicateCount++;
                    nullEntry = entry;
                }
                return;
            }

            if (index.TryGetValue(name, out NameIndexEntry<T> existing))
            {
                existing.DuplicateCount++;
                index[name] = existing;
            }
            else
                index.Add(name, new NameIndexEntry<T>(item));
        }

        private static void RebuildIndex<T>(List<T> items,
            Dictionary<string, NameIndexEntry<T>> index,
            ref NameIndexEntry<T>? nullEntry) where T : WzObject
        {
            index.Clear();
            nullEntry = null;
            if (items == null)
                return;

            for (int i = 0; i < items.Count; i++)
                AddToIndex(items[i], index, ref nullEntry);
        }

        private static void RemoveFromIndex<T>(T item, List<T> items,
            Dictionary<string, NameIndexEntry<T>> index,
            ref NameIndexEntry<T>? nullEntry) where T : WzObject
        {
            if (item == null)
                return;

            string name = item.Name;
            if (name == null)
            {
                if (!nullEntry.HasValue)
                    return;

                NameIndexEntry<T> entry = nullEntry.Value;
                if (!ReferenceEquals(entry.First, item))
                {
                    if (entry.DuplicateCount > 0)
                    {
                        entry.DuplicateCount--;
                        nullEntry = entry;
                        return;
                    }

                    RebuildIndex(items, index, ref nullEntry);
                    return;
                }

                if (entry.DuplicateCount == 0)
                {
                    nullEntry = null;
                    return;
                }

                entry.DuplicateCount--;
                entry.First = FindFirstByName(items, null);
                nullEntry = entry;
                return;
            }

            if (!index.TryGetValue(name, out NameIndexEntry<T> indexedEntry))
            {
                RebuildIndex(items, index, ref nullEntry);
                return;
            }

            if (!ReferenceEquals(indexedEntry.First, item))
            {
                if (indexedEntry.DuplicateCount > 0)
                {
                    indexedEntry.DuplicateCount--;
                    index[name] = indexedEntry;
                    return;
                }

                RebuildIndex(items, index, ref nullEntry);
                return;
            }

            if (indexedEntry.DuplicateCount == 0)
            {
                index.Remove(name);
                return;
            }

            indexedEntry.DuplicateCount--;
            indexedEntry.First = FindFirstByName(items, name);
            index[name] = indexedEntry;
        }

        private static T FindFirstByName<T>(List<T> items, string name) where T : WzObject
        {
            if (items == null)
                return null;

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (string.Equals(item?.Name, name, StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }

        public WzDirectory DeepClone()
        {
            WzDirectory result = (WzDirectory)MemberwiseClone();
            result.parent = null;
            result.subDirs = new List<WzDirectory>(subDirs.Count);
            result.images = new List<WzImage>(images.Count);
            result.imageIndex = new Dictionary<string, NameIndexEntry<WzImage>>(
                StringComparer.OrdinalIgnoreCase);
            result.directoryIndex = new Dictionary<string, NameIndexEntry<WzDirectory>>(
                StringComparer.OrdinalIgnoreCase);
            result.nullImageIndex = null;
            result.nullDirectoryIndex = null;
            foreach (WzDirectory dir in WzDirectories)
                result.AddDirectory(dir.DeepClone());
            foreach (WzImage img in WzImages)
                result.AddImage(img.DeepClone());
            return result;
        }

        public virtual int CountImages()
        {
            int result = images.Count;
            foreach (WzDirectory subdir in WzDirectories)
                result += subdir.CountImages();
            return result;
        }
        #endregion

        public override void Remove()
        {
            ((WzDirectory)Parent).RemoveDirectory(this);
        }
    }
}
