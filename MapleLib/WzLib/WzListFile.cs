using System.Collections.Generic;
using System.IO;
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
            List<string> listEntries = new List<string>();
            byte[] wzFileBytes = File.ReadAllBytes(filePath);
            using (WzBinaryReader wzParser = new WzBinaryReader(new MemoryStream(wzFileBytes), WzIv)) {
                while (wzParser.PeekChar() != -1) {
                    int len = wzParser.ReadInt32();
                    char[] strChrs = new char[len];
                    for (int i = 0; i < len; i++) {
                        strChrs[i] = (char)wzParser.ReadInt16();
                    }
                    wzParser.ReadUInt16(); //encrypted null

                    string decryptedStr = wzParser.DecryptString(strChrs);
                    listEntries.Add(decryptedStr);
                }
            }
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
            int lastIndex = listEntries.Count - 1;
            string lastEntry = listEntries[lastIndex];
            listEntries[lastIndex] = lastEntry.Substring(0, lastEntry.Length - 1) + "/";
            WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), WzIv);

            foreach (string listEntry in listEntries)
            {
                wzWriter.Write((int)listEntry.Length);
                char[] encryptedChars = wzWriter.EncryptString(listEntry + (char)0);
                for (int j = 0; j < encryptedChars.Length; j++)
                    wzWriter.Write((short)encryptedChars[j]);
            }
            listEntries[lastIndex] = lastEntry.Substring(0, lastEntry.Length - 1) + "/";
		}
    }
}