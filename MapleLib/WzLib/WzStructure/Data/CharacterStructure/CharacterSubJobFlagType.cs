using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
namespace MapleLib.WzLib.WzStructure.Data.CharacterStructure
{
    /// <summary>
    /// Its the best guess so far, could not find it in KMST.
    /// </summary>
    public enum CharacterSubJobFlagType
    {
        Any = 0,
        Adventurer = 0x1,
        Adventurer_DualBlade = 0x2,
        Adventurer_Cannoner = 0x4,
    }

    public static class CharacterSubJobFlagTypeExt
    {
        /// <summary>
        /// Human readable string
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static string ToReadableString(this CharacterSubJobFlagType state)
        {
            return state.ToString().Replace("_", " ");
        }

        /// <summary>
        /// Converts from the string name back to enum
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static CharacterSubJobFlagType ToEnum(this string name)
        {
            // Try to parse the string to enum
            if (Enum.TryParse<CharacterSubJobFlagType>(name.Replace(" ", "_"), out CharacterSubJobFlagType result))
            {
                return (CharacterSubJobFlagType)result;
            }
            return CharacterSubJobFlagType.Any;
        }

        /// <summary>
        /// Converts from the area code value to enum type
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static CharacterSubJobFlagType ToEnum(int value)
        {
            if (Enum.IsDefined(typeof(CharacterSubJobFlagType), value))
            {
                return (CharacterSubJobFlagType)value;
            }
            else
            {
                //Console.WriteLine($"Warning: Invalid CharacterSubJobFlagTypeExt value {value}. Defaulting to Any.");
                return CharacterSubJobFlagType.Any;
            }
        }
    }
}
