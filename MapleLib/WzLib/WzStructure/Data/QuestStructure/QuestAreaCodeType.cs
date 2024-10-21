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

namespace MapleLib.WzLib.WzStructure.Data.QuestStructure
{
    public enum QuestAreaCodeType
    {
        Unknown = 0,
        CrossHunter = 0x1,
        Maple_Island = 0x14,
        Event = 0x32,
        Event_Mission = 0x34, // anything below here is not used before v12x post big-bang update
        Boardgame = 0x36,
        EvolvingSystem = 0x20,
        DailySpecial = 0x59,
        Friends = 0x5D,
        StarPlanet = 0x62,
        StarPlanet_Quest = 0x63,
        Completed_Before_BigBang = 0x64,
        StarPlanet_Guide = 0x65,
        Blockbuster = 0x66,
        HOFM = 0xC9,
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
