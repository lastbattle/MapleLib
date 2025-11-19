using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.MSFile
{
    public class WzMsHeader
    {
        public WzMsHeader(string fileName, string salt, string fileNameWithSalt, int hash, byte version, int entryCount, long headerStartPosition, long entryStartPosition)
        {
            FileName = fileName;
            Salt = salt;
            FileNameWithSalt = fileNameWithSalt;
            Hash = hash;
            Version = version;
            EntryCount = entryCount;
            HeaderStartPosition = headerStartPosition;
            EntryStartPosition = entryStartPosition;
        }
        public string FileName { get; }
        public string Salt { get; }
        public string FileNameWithSalt { get; }
        public int Hash { get; }
        public byte Version { get; }
        public int EntryCount { get; }
        public long HeaderStartPosition { get; }
        public long EntryStartPosition { get; }
        public long DataStartPosition { get; set; }
    }
}
