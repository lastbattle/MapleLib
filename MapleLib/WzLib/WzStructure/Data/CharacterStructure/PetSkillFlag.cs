using System;

namespace MapleLib.WzLib.WzStructure.Data.CharacterStructure
{
    [Flags]
    public enum PetSkillFlag
    {
        PickupMeso = 0, // pet is always able to pickup meso
        PickupItem = 1, 
        LongRange = 2, 
        DropSweep = 3, 
        Ignore = 4, 
        PickupAll = 5,
        ConsumeHP = 6,
        ConsumeMP = 7,
        AutoBuff = 8,
        Smart = 9,
        Giant = 10,
        Shop = 11,
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

/* 566
enum $8093A0CAEB5D767A25125BE9634A1621
{
  PETSKILL_PICKUPITEM = 0x1,
  PETSKILL_LONGRANGE = 0x2,
  PETSKILL_DROPSWEEP = 0x4,
  PETSKILL_IGNORE = 0x8,
  PETSKILL_PICKUPALL = 0x10,
  PETSKILL_CONSUMEHP = 0x20,
  PETSKILL_CONSUMEMP = 0x40,
  PETSKILL_AUTOBUFF = 0x80,
  PETSKILL_SMART = 0x100,
  PETSKILL_GIANT = 0x200,
  PETSKILL_SHOP = 0x400,
  PETSKILL_NUM = 0xB,
};*/