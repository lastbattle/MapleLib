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
        Spiegelmann_Gonzo_Gallery_MSEA = 0x4,
        // all others are currently unused up till v180 GMS.
        Spiegelmann_Gonzo_Gallery_GMS = 0x13,
        UNKNOWN_37 = 0x37,
        Momijigaoka = 0x38,
        Character_Hayato = 0x39,
        Character_Kanna = 0x3A,
        UNKNOWN_3D = 0x3D,
        Explorer_Book = 0x53,
        Commerci = 0x54,
        Character_Beast_Tamer = 0x55,
        UNKNOWN_5A = 0x5A,
        UNKNOWN_5C = 0x5C,
        UNKNOWN_5E = 0x5E,
        Princess_No = 0x5F,
        UNKNOWN_60 = 0x60,
        Bounty_System = 0x61,
        Mr_Lee_Airlines = 0x67,
        Crusader_Codex = 0x68,
        Monster_Life = 0x6A,
        Familiar_System = 0x6B,
        Alishan = 0x6D,
        Blackgate_City = 0x6E,
        Tangyoon_Cooking_Class = 0x6F,
        Yu_Garden_Shanghai = 0x71,
        Shaolin_Temple = 0x72,
        Tynerum_And_Demon_Invitation = 0x73,
        Malaysia_GMS = 0x74,
        Beasts_Of_Fury = 0x75,
        Mushroom_Shrine_Tales = 0xCA,
        Masteria_Blockbuster = 0xCC,
        Stellar_Detectives = 0xCD,
        Hidden_Tales = 0xD1,
        Odium_Daily = 0xF4,
        Character_Khali = 0xF5,

        // Town areas
        CrossHunter = 0x1,
        Ardentmill_Crafting = 0x2,
        Golden_Temple = 0x3,       // 3, NEW: Based on Golden Temple quests
        Fantasy_Theme_World = 0x5, // 5
        Character_Aran = 0x6,
        Character_Evan = 0x7,
        Character_Mercedes = 0x8,
        Character_Phantom = 0x9,
        Job_Quest = 0xA, // 10
        Battle_Mode = 0x0B,           // 11 in log
        Special_Training = 0x0C,      // 12 in log
        Job_Training = 0x0D,          // 13 in log
        Character_Dual_Blade = 0x0E,           // 14 in log
        Character_Cygnus_Knights = 0x0F,       // 15 in log
        Character_Resistance = 0x10,           // 16 in log
        Silent_Crusade = 0x11, // 17
        Showa_Town = 0x12,               // 18 in log
        // Reserved 0x13
        Maple_Island = 0x14, // correct
        Kaiser_Nova = 0x16,                // 22 in log
        AngelicBurster = 0x17,           // 23 in log
        Edelstein = 0x18,           // 24 in log
        Story_Quests = 0x15, // 21 in hex

        Henesys = 0x19,             // 25 in log
        Ellinia = 0x1A,             // 26 in log
        Perion = 0x1B,              // 27 in log
        Kerning_City = 0x1C,        // 28 in log
        Nautilus = 0x1D,            // 29 in log
        VictoriaIsland_Misc = 0x1E,               // 30 in log (might be wrong)
        Sleepywood = 0x1F,          // 31 in log
        EvolvingSystem = 0x20,
        Orbis = 0x22,               // 34 in log
        El_Nath = 0x21,             // 33 in log
        Aqua_Road = 0x23,           // 35 in log
        Ludibrium = 0x24,           // 36 in log
        EOS_Tower = 0x25, // 37
        Omega_Sector = 0x26,        // 38 in log
        Ellin_Forest = 0x27, // 39
        Korean_Folk_Town = 0x28,    // 40 in log
        Leafre = 0x29, // 41
        Maple_High_School = 0x2A, // 42 or Red Leaf High
        Magatia = 0x2B,             // 43 in log
        Mu_Lung = 0x2C,             // 44 in log
        WorldTour_Singapore = 0x2D, // 45, Ulu city, Singapore, Boat Quay
        Temple_of_Time = 0x2E,      // 46 in log
        Knight_Stronghold = 0x2F,   // 47 in log

        WorldTour = 0x30, // 48, NLC, Shanghai, Taiwan, Neo Tokyo, Thailand [Floating market], Japan [Zipangu]
        ThemeDungeon = 0x31, // or is it [Party Quest]?

        // Special Content (0x30-0x3F)
        Event = 0x32,
        Achievement_Medals = 0x33,     // 51 in hex
        Event_Mission = 0x34, // anything below here is not used before v12x post big-bang update
        Pet = 0x35, // 53
        Boardgame = 0x36,
        Maple_Rewards = 0x3B, // 59
        System_Features = 0x3C,        // 60 in hex
        Root_Abyss = 0x3E,       // 62 in hex
        Mentoring = 0x3F, // 63
        PC_Room_MonsterArena = 0x40,// 64
        Character_Xenon = 0x41, // 65
        Crimsonheart = 0x42, // 66
        Stone_Colossus = 0x43, // 67

        // Character & Town Content (0x40-0x4F)
        Zero_Storyline = 0x44,   // 68 in hex
        Zero_Leafre = 0x45,             // 69 in log
        Zero_Ariant = 0x46,           // 70 in hex
        Zero_Henesys = 0x47,            // 71 in log
        Zero_Mulung = 0x48,              // 72 in log
        Zero_Edelstein = 0x49, // 73
        Zero_Magatia = 0x4A, // 74
        Zero_Ludibrium = 0x4B, // 75
        Zero_TempleOfTime = 0x4C,  // 76
        Tutorial_Guide = 0x4D,         // 77 in hex
        Riena_Strait = 0x4E,             // 78 in log
        Savage_Terminal = 0x4F,     // 79 in log
        Returning_Adventurer = 0x50, // 80

        // Progression Systems (0x50-0x5F)
        Kritias = 0x51,       // 81 in hex
        Grand_Athenaeum = 0x52,        // 82 in hex
        Character_Eunwol_And_Fox_Point_Village = 0x56,
        System_And_Tutorial = 0x57,    // 87
        Tower_Of_Oz = 0x58, // 88
        DailySpecial = 0x59,
        Mushroom_Castle = 0x5B, // 91
        Friends = 0x5D,

        // Modern Systems (0x60-0x6F)
        StarPlanet = 0x62,
        Netts_Pyramid_And_StarPlanet_Quest = 0x63,
        Completed_Before_BigBang = 0x64,
        StarPlanet_Guide = 0x65,
        Blockbuster_BlackHeaven = 0x66,
        Haven_Black_Heaven_And_Kerning_Square_MSEA = 0x69,
        Challenge_Quests = 0x6C,       // 108 in hex
        Character_Kinesis = 0x70, // 112

        Ursus = 0x76, // 118
        Maplerunner = 0x77, // 119

        Fishing = 0x78,
        Afterlands = 0x79,
        Best_Friends_Forever = 0x7B,
        Mechanical_Hearts = 0x7C,
        Monad = 0x7D,
        Captain_Vaga = 0x7E,
        UEvent = 0x7F,
        My_Home = 0x80,
        Singapore_MSEA = 0x81,
        Malaysia_MSEA = 0x82,
        Masteria = 0x83,
        New_Leaf_City = 0x84,
        Gollux_And_Phantom_Forest = 0x85,
        Extravagameza = 0x86,
        Sengoku_Asura_War = 0x87,
        Sengoku_Asura_Crisis = 0x89,
        Character_Mo_Xuan = 0x8A,
        Mushroom_Shrine = 0x8C,
        Goo_Island_And_Mochi_Paradise = 0x8D,
        Magical_Miracle_Time = 0x8F,
        Neo_Tokyo = 0x92,
        Guild_Castle = 0x93,
        Character_Lynn = 0x94,

        // High level Modern Content Areas (200+)
        Battle_Monster = 0xC8, // 200 [배틀 몬스터] 배몬 캡쳐 스킬과 캡쳐 게이지
        HOFM_HerosOfMaple = 0xC9, // 201
        Dark_World_Tree = 0xCB,        // 203
        Fifth_Job_V = 0xCE, // 206
        Arcane_River = 0xCF,          // 207 in hex
        Daily_Quest = 0xD0, // 208
        Lachelein = 0xD2,             // 210 in hex
        Kerning_Tower = 0xD3, // 211
        Legion_System = 0xD4,          // 212
        Arcana = 0xD5, // 213
        Character_Cadena = 0xD6,       // 214
        Character_Illium = 0xD7, // 215
        Maple_Achievements = 0xD8,      // 216
        Morass = 0xD9,                // 217 in hex
        Fox_Valley = 0xDA, // 218
        Character_Ark = 0xDB,          // 219
        Esfera = 0xDC,                // 220 in hex
        Lion_Kings_Castle = 0xDD,     // 221, NEW: Based on Lion King's Castle quests
        Particle_Movement_Use = 0xDE, // 222
        // 0xDE
        BlackMage_Alliance = 0xDF,              // 223 in hex
        Tenebris_Limen = 0xE0,          // 224
        Genesis_Weapon = 0xE1,               // 225 in hex
        Detective_Storyline = 0xE2,   // 226, NEW: Based on Detective/Investigation quests
        Ellinel_Fairy_Academy = 0xE3, // 227
        Gold_Beach = 0xE4,            // 228, NEW: Based on Gold Beach theme dungeon
        Elodin = 0xE5, // 229
        Pathfinder_Partem = 0xE6,                // 230 in hex
        Partem_Ruins = 0xE7, // 231
        Character_Hoyoung = 0xE8,      // 232
        Cernium = 0xE9,          // 233, NEW: Based on Glory event quests
        Reverse_City = 0xEA, // 234
        Character_Adele = 0xEB, // 235
        Yum_Yum = 0xEC, // 236
        Sellas = 0xED, // 237
        Cernium_Before = 0xEE,               // 238 in hex
        Cernium_After = 0xEF,               // 239 in hex
        Character_Kain = 0xF0,         // 240
        Hotel_Arcus = 0xF1, // 241
        Character_Lara = 0xF2,         // 242
        Ramuramu = 0xF3, // 243

        // Special Systems
        MapleStoryN_Guide = 0xF6,            // 246 in hex
        Achievement_System = 0xF7,     // 247 in hex
        Legend_Of_Mithra = 0xF8,

        Shangri_La = 0xF9,
        Arteria_Daily = 0xFA,
        Sixth_Job = 0xFB,
        Carcion_Daily = 0xFC,
        Riena_Strait_Modern = 0xFE,
        Verne_Mine = 0xFF,

        Maple_Alliance = 0x100,
        Azwan = 0x101,
        Magatia_Secret = 0x102,
        Heliseum = 0x103,
        Lion_Kings_Castle_Modern = 0x104,
        Root_Abyss_Modern = 0x105,
        Cygnus_Alliance = 0x106,
        Temple_Of_Time_Modern = 0x107,
        Silent_Crusade_Modern = 0x108,
        Stone_Colossus_Modern = 0x109,
        Henesys_Ruins = 0x10A,
        Kritias_Modern = 0x10B,
        Twilight_Perion = 0x10C,
        Haven = 0x10D,
        Dark_World_Tree_Modern = 0x10E,
        Silent_Crusade_Chapter_2 = 0x10F,
        Vanishing_Journey = 0x110,
        Reverse_City_Modern = 0x111,
        Chu_Chu_Island = 0x112,
        Yum_Yum_Island_Modern = 0x113,
        Lachelein_Modern = 0x114,
        Arcana_Modern = 0x115,
        Morass_Modern = 0x116,
        Esfera_Modern = 0x117,
        Sellas_Modern = 0x118,
        Moonbridge = 0x119,
        Labyrinth_Of_Suffering = 0x11A,
        Limina = 0x11B,
        Borderless_Convergence = 0x11C,
        Hotel_Arcus_Modern = 0x11D,
        Odium = 0x11E,
        Shangri_La_Story = 0x11F,
        Arteria = 0x120,
        Carcion = 0x121,

        Special_Eye = 0x122, // 290

        Tallahart = 0x123,
        Tallahart_Daily = 0x124,
        Azmoth_Chasm = 0x125,

        Malaysia_Event = 0x3E6,
        Veracent = 0x3E7,

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
