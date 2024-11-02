using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace MapleLib.WzLib.WzStructure.Data.ItemStructure
{
    public class ItemIdsCategory
    {
        public static int 
            MEDAL_CATEGORY = 114,
            BUFF_CATEGORY = 202,
            PET_CATEGORY = 500;

        /// <summary>
        /// Is Buff items
        /// </summary>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public static bool IsBuffItem(int itemId)
        {
            return itemId / 10000 == BUFF_CATEGORY;
        }

        /// <summary>
        /// Is equip item
        /// </summary>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public static bool IsEquipment(int itemId)
        {
            return itemId / 1000000 == 1;
        }
    }
}
