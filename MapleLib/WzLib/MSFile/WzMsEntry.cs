using MapleLib.WzLib.Util;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

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
        public int CheckSum { get => _checkSum; set => _checkSum = value; }
        public int Flags { get => _flags; }
        public long StartPos { get; set; }
        public int Size { get => _size; set => _size = value; }
        public int SizeAligned { get => _sizeAligned; set => _sizeAligned = value; }
        public int Unk1 { get => _unk1; }
        public int Unk2 { get; }
        public byte[] EntryKey { get => _entryKey; }
        public int CalculatedCheckSum { get; private set; }

        /// <summary>
        /// Recalculates Size, SizeAligned, EntryKey (if null), and CheckSum/CalculatedCheckSum for this entry.
        /// </summary>
        /// <param name="flags">Flags value to use</param>
        /// <param name="startPos">StartPos value to use</param>
        /// <param name="unk1">Unk1 value to use</param>
        /// <param name="rng">Optional Random instance for EntryKey generation</param>
        public void RecalculateFields(int flags, int startPos, int unk1, Random rng = null)
        {
            if (Data == null)
                throw new InvalidOperationException("Data must be set before recalculation.");
            _size = Data.Length;
            _sizeAligned = ((_size + 1023) / 1024) * 1024;
            _flags = flags;
            this.StartPos = startPos;
            _unk1 = unk1;
            if (_entryKey == null)
            {
                rng ??= new Random();
                _entryKey = new byte[16];
                rng.NextBytes(_entryKey);
            }
            int keySum = _entryKey.Sum(b => (int)b);
            CalculatedCheckSum = _flags + (int)this.StartPos + _size + _sizeAligned + _unk1 + keySum;
            _checkSum = CalculatedCheckSum;
        }
    }
}