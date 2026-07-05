using System.Collections.Generic;
using System.IO;
using System;
using System.Buffers;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;

namespace MapleLib.WzLib
{
	/// <summary>
	/// A class that parses and contains the data of a wz list file
	/// </summary>
	public static class ListFileParser
	{
        /// <summary>
		/// Parses the List.wz file on the disk
		/// </summary>
		/// <param name="filePath">Path to the wz file</param>
        public static List<string> ParseListFile(string filePath, WzMapleVersion version)
        {
            return ParseListFile(filePath, WzTool.GetIvByMapleVersion(version));
        }

        /// <summary>
		/// Parses the List.wz file on the disk
		/// </summary>
		/// <param name="filePath">Path to the wz file</param>
        private static List<string> ParseListFile(string filePath, byte[] WzIv)
        {
            int fileLength = checked((int)new FileInfo(filePath).Length);
            byte[] fileBuffer = ArrayPool<byte>.Shared.Rent(fileLength);
            char[] charBuffer = ArrayPool<char>.Shared.Rent(64);
            int estimatedEntryCount = Math.Min(fileLength / 48, 65536);
            List<string> listEntries = new List<string>(estimatedEntryCount);
            try
            {
                using (FileStream fileStream = File.OpenRead(filePath))
                    fileStream.ReadExactly(fileBuffer, 0, fileLength);
                using MemoryStream memoryStream = new MemoryStream(fileBuffer, 0, fileLength, writable: false, publiclyVisible: true);
                using WzBinaryReader wzParser = new WzBinaryReader(memoryStream, WzIv);
                while (wzParser.BaseStream.Position < wzParser.BaseStream.Length) {
                    int len = wzParser.ReadInt32();
                    if (len < 0)
                        throw new InvalidDataException("List.wz contains a negative string length.");
                    if (len > charBuffer.Length)
                    {
                        ArrayPool<char>.Shared.Return(charBuffer);
                        charBuffer = ArrayPool<char>.Shared.Rent(len);
                    }
                    Span<char> strChrs = charBuffer.AsSpan(0, len);
                    for (int i = 0; i < len; i++) {
                        strChrs[i] = (char)wzParser.ReadInt16();
                    }
                    wzParser.ReadUInt16(); //encrypted null

                    string decryptedStr = wzParser.DecryptString(strChrs);
                    listEntries.Add(decryptedStr);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(charBuffer);
                ArrayPool<byte>.Shared.Return(fileBuffer);
            }
            if (listEntries.Count == 0)
                return listEntries;

            int lastIndex = listEntries.Count - 1;
            string lastEntry = listEntries[lastIndex];
            listEntries[lastIndex] = lastEntry.Substring(0, lastEntry.Length - 1) + "g";
            return listEntries;
        }

        public static void SaveToDisk(string path, WzMapleVersion version, List<string> listEntries)
        {
            SaveToDisk(path, WzTool.GetIvByMapleVersion(version), listEntries);
        }

		public static void SaveToDisk(string path, byte[] WzIv, List<string> listEntries)
		{
            using WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), WzIv);

            for (int i = 0; i < listEntries.Count; i++)
            {
                string listEntry = listEntries[i];
                if (i == listEntries.Count - 1 && listEntry.Length > 0)
                    listEntry = listEntry.Substring(0, listEntry.Length - 1) + "/";

                wzWriter.Write((int)listEntry.Length);
                char[] encryptedChars = wzWriter.EncryptString(listEntry + (char)0);
                for (int j = 0; j < encryptedChars.Length; j++)
                    wzWriter.Write((short)encryptedChars[j]);
            }
		}
    }
}
