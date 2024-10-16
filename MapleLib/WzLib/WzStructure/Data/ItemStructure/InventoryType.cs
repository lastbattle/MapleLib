using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.WzStructure.Data.ItemStructure
{
    public enum InventoryType : byte
    {
        EQUIP = 1,
        USE = 2,
        SETUP = 3,
        ETC = 4,
        CASH = 5,
        EQUIPPED = 255, // Using 255 instead of -1 as enums in C# are unsigned by default

        NONE = 0, // or null
    }

    public static class InventoryTypeExtensions
    {
        public static short GetBitfieldEncoding(this InventoryType inventoryType)
        {
            return (short)(2 << (byte)inventoryType);
        }

        public static InventoryType? GetByType(byte type)
        {
            return Enum.GetValues<InventoryType>().FirstOrDefault(t => (byte)t == type);
        }

        public static InventoryType? GetByWZName(string name)
        {
            return name switch
            {
                "Install" => InventoryType.SETUP,
                "Consume" => InventoryType.USE,
                "Etc" => InventoryType.ETC,
                "Cash" => InventoryType.CASH,
                "Pet" => InventoryType.CASH,
                _ => null
            };
        }
    }
}