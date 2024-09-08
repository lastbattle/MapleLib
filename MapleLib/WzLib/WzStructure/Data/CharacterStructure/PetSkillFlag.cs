using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace MapleLib.WzLib.WzStructure.Data.CharacterStructure
{
    [Flags]
    public enum PetSkillFlag
    {
        PickupMeso = 1, // 1
        PickupItem = 2, // 2
        LongRange = 3, // 4
        DropSweep = 4, // 8
        Ignore = 5, // 16
        PickupAll = 6, // 32
        ConsumeHP = 7, // 64
        ConsumeMP = 8, // 128
        AutoBuff = 9, // 256
        Smart = 10, // 512
        Giant = 11, // 1024
        Shop = 12, // 2048
    }

    public static class PetSkillFlagExtensions
    {
        public static int GetValue(this PetSkillFlag flag)
        {
            return 1 << (int)flag;
        }

        public static bool Check(this PetSkillFlag flag, int value)
        {
            return (value & GetValue(flag)) == GetValue(flag);
        }
    }
}