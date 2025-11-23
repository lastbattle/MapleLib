using System;
using System.Collections.Generic;
using System.Text;

namespace MapleLib.WzLib.MSFile
{
    public class WzMsConstants
    {
        public const int SupportedVersion = 2;
        public const int SnowKeyLength = 16;
        public const int BlockAlignment = 1024;
        public const int PageAlignmentMask = 0x3FF;
        public const int PageAlignmentSize = 0x400;
        public const int RandByteMod = 312;
        public const int RandByteOffset = 30;
        public const int HeaderPadMod = 212;
        public const int HeaderPadOffset = 33;
        public const uint InitialKeyHash = 0x811C9DC5;
        public const uint KeyHashMultiplier = 0x1000193;
        public const int SaltMinLength = 4;
        public const int SaltMaxLength = 12;
        public const int DoubleEncryptInitialBytes = 1024;
        public const int AsciiPrintableMin = 33;
        public const int AsciiPrintableMax = 127;
    }
}
