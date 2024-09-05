using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.WzStructure.Data.CharacterStructure
{
    [Flags]
    public enum PetSkillFlag
    {
        PETSKILL_PickupMeso = 1 << 0,  // 1
        PETSKILL_PICKUPITEM = 1 << 1,  // 2
        PETSKILL_LONGRANGE = 1 << 2,   // 4
        PETSKILL_DROPSWEEP = 1 << 3,   // 8
        PETSKILL_IGNORE = 1 << 4,      // 16 (Ignore pickup of some items)
        PETSKILL_PICKUPALL = 1 << 5,   // 32 (Pickup unlooted items)
        PETSKILL_CONSUMEHP = 1 << 6,   // 64
        PETSKILL_CONSUMEMP = 1 << 7,   // 128
        PETSKILL_AUTOBUFF = 1 << 8,    // 256
        PETSKILL_SMART = 1 << 9,       // 512
        PETSKILL_GIANT = 1 << 10,      // 1024
        PETSKILL_SHOP = 1 << 11,       // 2048
        PETSKILL_NUM = 12  // This is not a flag, but represents the number of flags
    }

    public static class PetSkillFlagExtensions
    {
        public static int GetValue(this PetSkillFlag flag)
        {
            return (int)flag;
        }

        public static bool Check(this PetSkillFlag flag, int value)
        {
            return (value & (int)flag) == (int)flag;
        }
    }
}