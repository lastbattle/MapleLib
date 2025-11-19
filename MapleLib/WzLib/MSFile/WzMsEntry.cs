using System;
using System.Linq;

namespace MapleLib.WzLib.MSFile
{
    public class WzMsEntry
    {
        public byte[] Data { get; set; }

        private int _checkSum;
        private int _flags;
        private int _size;
        private int _sizeAligned;
        private int _unk1;
        private byte[] _entryKey;

        public WzMsEntry(string name, int checkSum, int flags, int startPos, int size, int sizeAligned, int unk1, int unk2, byte[] entryKey)
        {
            this.Data = null;
            this.Name = name;
            _checkSum = checkSum;
            _flags = flags;
            this.StartPos = startPos;
            _size = size;
            _sizeAligned = sizeAligned;
            _unk1 = unk1;
            this.Unk2 = unk2;
            _entryKey = entryKey;
        }
        public string Name { get; }
        public int CheckSum { get => _checkSum; }
        public int Flags { get => _flags; }
        public long StartPos { get; set; }
        public int Size { get => _size; }
        public int SizeAligned { get => _sizeAligned; }
        public int Unk1 { get => _unk1; }
        public int Unk2 { get; }
        public byte[] EntryKey { get => _entryKey; }
        public int CalculatedCheckSum { get; private set; }
    }
}
