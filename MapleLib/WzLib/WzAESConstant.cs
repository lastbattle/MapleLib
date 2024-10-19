using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib
{
    /// <summary>
    /// Contains all the constant values used for various functions in the WZ file encryption
    /// </summary>
    public class WzAESConstant
    {
        /// <summary>
        /// ?s_BasicKey@CAESCipher@@2PAEA
        /// IV used to create the WzKey for GMS
        /// </summary>
        public static byte[] WZ_GMSIV = new byte[4] { 0x4D, 0x23, 0xC7, 0x2B };

        /// <summary>
        /// ?s_BasicKey@CAESCipher@@2PAEA
        /// IV used to create the WzKey for the latest version of GMS, MSEA, or KMS
        /// </summary>
        public static byte[] WZ_MSEAIV = new byte[4] { 0xB9, 0x7D, 0x63, 0xE9 };

        /// <summary>
        /// ?s_BasicKey@CAESCipher@@2PAEA
        /// IV used to create the WzKey for the latest version of GMS, MSEA, or KMS
        /// </summary>
        public static byte[] WZ_BMSCLASSIC = new byte[4] { 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// Constant used in WZ offset encryption
        /// </summary>
        public static uint WZ_OffsetConstant = 0x581C3F6D;
    }
}
