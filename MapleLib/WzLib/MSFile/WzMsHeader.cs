using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.MSFile
{
    public class WzMsHeader
    {
        private int _hash;
        private int _entryCount;
        private long _HeaderStartPosition;
        private long _EntryStartPosition;
        private readonly uint _saltKeyHash;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="salt"></param>
        /// <param name="fileNameWithSalt"></param>
        /// <param name="hash"></param>
        /// <param name="version"></param>
        /// <param name="entryCount"></param>
        /// <param name="headerStartPosition"></param>
        /// <param name="entryStartPosition"></param>
        public WzMsHeader(string fileName, string salt, string fileNameWithSalt, int hash, byte version, int entryCount, long headerStartPosition, long entryStartPosition)
        {
            FileName = fileName;
            Salt = salt;
            // The immutable salt contributes the same hash to every image key.
            // Keep null salts constructible; derivation still fails after entry validation.
            uint saltKeyHash = WzMsConstants.InitialKeyHash;
            if (salt != null)
            {
                foreach (char c in salt)
                    saltKeyHash = (saltKeyHash ^ c) * WzMsConstants.KeyHashMultiplier;
            }
            _saltKeyHash = saltKeyHash;
            FileNameWithSalt = fileNameWithSalt;
            Hash = hash;
            Version = version;
            EntryCount = entryCount;
            HeaderStartPosition = headerStartPosition;
            EntryStartPosition = entryStartPosition;
        }

        public string FileName { get; }
        public string Salt { get; }
        internal uint SaltKeyHash => Salt != null ? _saltKeyHash : throw new NullReferenceException();
        public string FileNameWithSalt { get; }
        public int Hash { get => _hash; private set => _hash = value; }
        public byte Version { get; }
        public int EntryCount { get => _entryCount; private set => _entryCount = value; }
        public long HeaderStartPosition { get => _HeaderStartPosition; private set => _HeaderStartPosition = value; }
        public long EntryStartPosition { get => _EntryStartPosition; private set => _EntryStartPosition = value; }
        public long DataStartPosition { get; set; }


        public void UpdateHeader(int hash, int entryCount, long headerStartPosition, long entryStartPosition, long dataStartPosition)
        {
            this.Hash = hash;
            this.EntryCount = entryCount;
            this.HeaderStartPosition = headerStartPosition;
            this.EntryStartPosition = entryStartPosition;
            this.DataStartPosition = dataStartPosition;
        }
    }
}
