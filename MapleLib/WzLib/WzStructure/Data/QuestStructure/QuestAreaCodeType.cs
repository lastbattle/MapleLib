/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.ComponentModel.DataAnnotations;

namespace MapleLib.WzLib.WzStructure.Data.QuestStructure
{
    public enum QuestAreaCodeType
    {
        Unknown = 0,

        // TODO
        UNKNOWN_4 = 4, // seems to be ariant
        // all others are currently unused up till v180 GMS.
        UNKNOWN_13 = 0x13,  
        UNKNOWN_37 = 0x37,  
        UNKNOWN_38 = 0x38,  
        UNKNOWN_39 = 0x39,  
        UNKNOWN_3A = 0x3A,  
        UNKNOWN_3D = 0x3D,  
        UNKNOWN_53 = 0x53,  
        UNKNOWN_54 = 0x54,  
        UNKNOWN_55 = 0x55,  
        UNKNOWN_5A = 0x5A,  
        UNKNOWN_5C = 0x5C,  
        UNKNOWN_5E = 0x5E,  
        UNKNOWN_5F = 0x5F,  
        UNKNOWN_60 = 0x60,  
        UNKNOWN_61 = 0x61,  
        UNKNOWN_67 = 0x67,  
        UNKNOWN_68 = 0x68,  
        UNKNOWN_6A = 0x6A,  
        UNKNOWN_6B = 0x6B,  
        UNKNOWN_6D = 0x6D,  
        UNKNOWN_6E = 0x6E,  
        UNKNOWN_6F = 0x6F,  
        UNKNOWN_71 = 0x71,  
        UNKNOWN_72 = 0x72,  
        UNKNOWN_73 = 0x73,  
        UNKNOWN_74 = 0x74,  
        UNKNOWN_75 = 0x75,  
        UNKNOWN_CA = 0xCA,  
        UNKNOWN_CC = 0xCC,  
        UNKNOWN_CD = 0xCD,  
        UNKNOWN_D1 = 0xD1,  
        UNKNOWN_F4 = 0xF4,  
        UNKNOWN_F5 = 0xF5,  

        // Town areas
        CrossHunter = 0x1,
        Ardentmill_Crafting = 0x2,
        Golden_Temple = 0x3,       // 3, claude 3.5  NEW: Based on Golden Temple quests
        Fantasy_Theme_World = 0x5, // 5
        Character_Aran = 0x6,
        Character_Evan = 0x7,
        Character_Mercedes = 0x8,
        Character_Phantom = 0x9,
        Job_Quest = 0xA, // 10
        Battle_Mode = 0x0B,           // 11 in log, claude 3.5
        Special_Training = 0x0C,      // 12 in log, claude 3.5
        Job_Training = 0x0D,          // 13 in log, claude 3.5
        Character_Dual_Blade = 0x0E,           // 14 in log, claude 3.5
        Character_Cygnus_Knights = 0x0F,       // 15 in log, claude 3.5
        Character_Resistance = 0x10,           // 16 in log, claude 3.5
        Silent_Crusade = 0x11, // 17
        Showa_Town = 0x12,               // 18 in log, claude 3.5
        // Reserved 0x13
        Maple_Island = 0x14, // correct
        Kaiser_Nova = 0x16,                // 22 in log, claude 3.5
        AngelicBurster = 0x17,           // 23 in log, claude 3.5
        Edelstein = 0x18,           // 24 in log, claude 3.5
        Story_Quests = 0x15, // 21 in hex,claude 3.5

        Henesys = 0x19,             // 25 in log, claude 3.5
        Ellinia = 0x1A,             // 26 in log, claude 3.5
        Perion = 0x1B,              // 27 in log, claude 3.5
        Kerning_City = 0x1C,        // 28 in log, claude 3.5
        Nautilus = 0x1D,            // 29 in log, claude 3.5
        VictoriaIsland_Misc = 0x1E,               // 30 in log, claude 3.5 (might be wrong)
        Sleepywood = 0x1F,          // 31 in log, claude 3.5
        EvolvingSystem = 0x20,
        Orbis = 0x22,               // 34 in log, claude 3.5
        El_Nath = 0x21,             // 33 in log, claude 3.5
        Aqua_Road = 0x23,           // 35 in log, claude 3.5
        Ludibrium = 0x24,           // 36 in log, claude 3.5
        EOS_Tower = 0x25, // 37
        Omega_Sector = 0x26,        // 38 in log, claude 3.5
        Ellin_Forest = 0x27, // 39
        Korean_Folk_Town = 0x28,    // 40 in log, claude 3.5
        Leafre = 0x29, // 41
        Maple_High_School = 0x2A, // 42 or Red Leaf High
        Magatia = 0x2B,             // 43 in log, claude 3.5
        Mu_Lung = 0x2C,             // 44 in log, claude 3.5
        WorldTour_Singapore = 0x2D, // 45, Ulu city, Singapore, Boat Quay
        Temple_of_Time = 0x2E,      // 46 in log, claude 3.5
        Knight_Stronghold = 0x2F,   // 47 in log, claude 3.5

        WorldTour = 0x30, // 48, NLC, Shanghai, Taiwan, Neo Tokyo, Thailand [Floating market], Japan [Zipangu]
        ThemeDungeon = 0x31, // claude 3.5, or is it [Party Quest]?

        // Special Content (0x30-0x3F)
        Event = 0x32,
        Achievement_Medals = 0x33,     // 51 in hex, claude 3.5
        Event_Mission = 0x34, // anything below here is not used before v12x post big-bang update
        Pet = 0x35, // 53
        Boardgame = 0x36,
        Maple_Rewards = 0x3B, // 59
        System_Features = 0x3C,        // 60 in hex, claude 3.5
        Root_Abyss = 0x3E,       // 62 in hex, claude 3.5
        Mentoring = 0x3F, // 63
        PC_Room_MonsterArena = 0x40,// 64
        Character_Xenon = 0x41, // 65
        Crimsonheart = 0x42, // 66
        Stone_Colossus = 0x43, // 67

        // Character & Town Content (0x40-0x4F)
        Zero_Storyline = 0x44,   // 68 in hex, claude 3.5
        Zero_Leafre = 0x45,             // 69 in log, claude 3.5  
        Zero_Ariant = 0x46,           // 70 in hex, claude 3.5  
        Zero_Henesys = 0x47,            // 71 in log, claude 3.5 
        Zero_Mulung = 0x48,              // 72 in log, claude 3.5  
        Zero_Edelstein = 0x49, // 73, claude 3.5
        Zero_Magatia = 0x4A, // 74, claude 3.5 
        Zero_Ludibrium = 0x4B, // 75, claude 3.5
        Zero_TempleOfTime = 0x4C,  // 76, claude 3.5
        Tutorial_Guide = 0x4D,         // 77 in hex, claude 3.5
        Riena_Strait = 0x4E,             // 78 in log, claude 3.5
        Savage_Terminal = 0x4F,     // 79 in log, claude 3.5
        Returning_Adventurer = 0x50, // 80

        // Progression Systems (0x50-0x5F)
        Kritias = 0x51,       // 81 in hex, claude 3.5
        Grand_Athenaeum = 0x52,        // 82 in hex, claude 3.5
        Fox_Village = 0x56,         // 86 in hex, claude 3.5
        System_And_Tutorial = 0x57,    // 87, claude 3.5
        Tower_Of_Oz = 0x58, // 88
        DailySpecial = 0x59,
        Mushroom_Castle = 0x5B, // 91
        Friends = 0x5D,

        // Modern Systems (0x60-0x6F)
        StarPlanet = 0x62,
        StarPlanet_Quest = 0x63, // or boss contents primarily (star planet means boss channels?)
        Completed_Before_BigBang = 0x64,
        StarPlanet_Guide = 0x65,
        Blockbuster_BlackHeaven = 0x66,
        BlackHeaven = 0x69,           // 105 in hex, claude 3.5
        Challenge_Quests = 0x6C,       // 108 in hex, claude 3.5
        Character_Kinesis = 0x70, // 112

        Ursus = 0x76, // 118
        Maplerunner = 0x77, // 119

        // High level Modern Content Areas (200+)
        Battle_Monster = 0xC8, // 200 [배틀 몬스터] 배몬 캡쳐 스킬과 캡쳐 게이지
        HOFM_HerosOfMaple = 0xC9, // 201
        Dark_World_Tree = 0xCB,        // 203, claude 3.5
        Fifth_Job_V = 0xCE, // 206
        Arcane_River = 0xCF,          // 207 in hex, claude 3.5
        Daily_Quest = 0xD0, // 208
        Lachelein = 0xD2,             // 210 in hex, claude 3.5
        Kerning_Tower = 0xD3, // 211
        Legion_System = 0xD4,          // 212, claude 3.5
        Arcana = 0xD5, // 213
        Character_Cadena = 0xD6,       // 214
        Character_Illium = 0xD7, // 215
        Maple_Achievements = 0xD8,      // 216, claude 3.5
        Morass = 0xD9,                // 217 in hex, claude 3.5
        Fox_Valley = 0xDA, // 218
        Character_Ark = 0xDB,          // 219, claude 3.5
        Esfera = 0xDC,                // 220 in hex, claude 3.5
        Lion_Kings_Castle = 0xDD,     // 221, claude 3.5,  NEW: Based on Lion King's Castle quests
        Particle_Movement_Use = 0xDE, // 222
        // 0xDE
        BlackMage_Alliance = 0xDF,              // 223 in hex, claude 3.5
        Tenebris_Limen = 0xE0,          // 224 
        Genesis_Weapon = 0xE1,               // 225 in hex, claude 3.5
        Detective_Storyline = 0xE2,   // 226, claude 3.5,  NEW: Based on Detective/Investigation quests
        Ellinel_Fairy_Academy = 0xE3, // 227
        Gold_Beach = 0xE4,            // 228, claude 3.5,  NEW: Based on Gold Beach theme dungeon
        Elodin = 0xE5, // 229
        Pathfinder_Partem = 0xE6,                // 230 in hex, claude 3.5
        Partem_Ruins = 0xE7, // 231
        Character_Hoyoung = 0xE8,      // 232, claude 3.5
        Cernium = 0xE9,          // 233, claude 3.5,  NEW: Based on Glory event quests
        Reverse_City = 0xEA, // 234
        Character_Adele = 0xEB, // 235, claude 3.5
        Yum_Yum = 0xEC, // 236
        Sellas = 0xED, // 237
        Cernium_Before = 0xEE,               // 238 in hex, claude 3.5
        Cernium_After = 0xEF,               // 239 in hex, claude 3.5
        Character_Kain = 0xF0,         // 240, claude 3.5
        Hotel_Arcus = 0xF1, // 241
        Character_Lara = 0xF2,         // 242, claude 3.5
        Ramuramu = 0xF3, // 243

        // Special Systems
        MapleStoryN_Guide = 0xF6,            // 246 in hex, claude 3.5
        Achievement_System = 0xF7,     // 247 in hex, claude 3.5
        MapleStoryN = 0xF8,          // 248 in hex, claude 3.5

        Special_Eye = 0x122, // 290, claude 3.5

        Tutorial_And_Job = 0x2328,
    }

    public static class QuestAreaCodeTypeExt
    {
        /// <summary>
        /// Human readable string
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static string ToReadableString(this QuestAreaCodeType state)
        {
            return state.ToString().Replace("_", " ");
        }

        /// <summary>
        /// Converts from the string name back to enum
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static QuestAreaCodeType ToEnum(this string name)
        {
            // Try to parse the string to enum
            if (Enum.TryParse<QuestAreaCodeType>(name.Replace(" ", "_"), out QuestAreaCodeType result))
            {
                return (QuestAreaCodeType)result;
            }
            return QuestAreaCodeType.Unknown;
        }

        /// <summary>
        /// Converts from the area code value to enum type
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static QuestAreaCodeType ToEnum(int value)
        {
            if (Enum.IsDefined(typeof(QuestAreaCodeType), value))
            {
                return (QuestAreaCodeType)value;
            }
            else
            {
                //Console.WriteLine($"Warning: Invalid QuestAreaCodeType value {value}. Defaulting to Unknown.");
                return QuestAreaCodeType.Unknown;
            }
        }
    }
}

/* KMST1029
 * 457
enum $CD8523FFACD4BE0C585B1BFB07FFF800
{
  QUEST_CATEGORY_CROSSHUNTER = 0x1,
  QUEST_CATEGORY_MAPLE_ISLAND = 0x14,
  QUEST_CATEGORY_EVENT = 0x32,
  QUEST_CATEGORY_EVENT_MISSION = 0x34,
  QUEST_CATEGORY_BOARDGAME = 0x36,
  QUEST_CATEGORY_EVOLVINGSYSTEM = 0x20,
  QUEST_CATEGORY_DAILYSPECIAL = 0x59,
  QUEST_CATEGORY_FRIENDS = 0x5D,
  QUEST_CATEGORY_STARPLANET = 0x62,
  QUEST_CATEGORY_STARPLANET_QUEST = 0x63,
  QUEST_CATEGORY_COMPLETED_BEFORE_BIGBANG = 0x64,
  QUEST_CATEGORY_STARPLANET_GUIDE = 0x65,
  QUEST_CATEGORY_BLOCKBUSTER = 0x66,
  QUEST_CATEGORY_HOFM = 0xC9,
  QUEST_CATEGORY_TUTORIAL_AND_JOB = 0x2328,
};*/
