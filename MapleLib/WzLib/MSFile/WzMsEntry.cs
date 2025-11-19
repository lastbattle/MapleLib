using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.MSFile
{
    public class WzMsEntry
    {
        public WzMsEntry(string name, int checkSum, int flags, int startPos, int size, int sizeAligned, int unk1, int unk2, byte[] entryKey)
        {
            Name = name;
            CheckSum = checkSum;
            Flags = flags;
            StartPos = startPos;
            Size = size;
            SizeAligned = sizeAligned;
            Unk1 = unk1;
            Unk2 = unk2;
            EntryKey = entryKey;
        }
        public string Name { get; }
        public int CheckSum { get; }
        public int Flags { get; }
        public long StartPos { get; set; }
        public int Size { get; }
        public int SizeAligned { get; }
        public int Unk1 { get; }
        public int Unk2 { get; }
        public byte[] EntryKey { get; }
        public int CalculatedCheckSum { get; set; }
    }
}
