
namespace MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// Represents the different PNG compression formats used in MapleStory WZ files
    /// </summary>
    public enum WzPngFormat
    {
        /// <summary>
        /// BGRA4444 format (16-bit)
        /// </summary>
        Format1 = 1,

        /// <summary>
        /// BGRA8888 format (32-bit)
        /// </summary>
        Format2 = 2,

        /// <summary>
        /// DXT3 compression with grayscale
        /// </summary>
        Format3 = 3,

        /// <summary>
        /// ARGB1555 format (16-bit)
        /// </summary>
        Format257 = 257,

        /// <summary>
        /// RGB565 format (16-bit), used for Nexon/Wizet logo
        /// </summary>
        Format513 = 513,

        /// <summary>
        /// Special 16x16 block compressed RGB565
        /// </summary>
        Format517 = 517,

        /// <summary>
        /// DXT3 compression
        /// </summary>
        Format1026 = 1026,

        /// <summary>
        /// DXT5 compression
        /// </summary>
        Format2050 = 2050
    }

}
