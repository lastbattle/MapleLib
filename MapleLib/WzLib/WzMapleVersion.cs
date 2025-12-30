using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapleLib.WzLib
{
    public enum WzMapleVersion
    {
        GMS = 0,
        EMS = 1,
        BMS = 2,
        CLASSIC = 3,
        GENERATE = 4,
        GETFROMZLZ = 5,
        CUSTOM = 6, // input bytes, for private servers

        UNKNOWN = 99,
    }
}