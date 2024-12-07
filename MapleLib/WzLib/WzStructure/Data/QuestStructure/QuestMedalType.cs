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
    public enum QuestMedalType
    {
        NoneOrUnknown = 0,
        Job = 1,
        Normal = 2,
        Challenge = 3,
        Event = 4,
        NO = 5, // number?
    }

    public static class QuestMedalTypeExt
    {
        /// <summary>
        /// Human readable string
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static string ToReadableString(this QuestMedalType state)
        {
            return state.ToString().Replace("_", " ");
        }

        /// <summary>
        /// Converts from the string name back to enum
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static QuestMedalType ToEnum(this string name)
        {
            // Try to parse the string to enum
            if (Enum.TryParse<QuestMedalType>(name.Replace(" ", "_"), out QuestMedalType result))
            {
                return (QuestMedalType)result;
            }
            return QuestMedalType.NoneOrUnknown;
        }

        /// <summary>
        /// Converts from the area code value to enum type
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static QuestMedalType ToEnum(int value)
        {
            if (Enum.IsDefined(typeof(QuestMedalType), value))
            {
                return (QuestMedalType)value;
            }
            else
            {
                //Console.WriteLine($"Warning: Invalid QuestAreaCodeType value {value}. Defaulting to Unknown.");
                return QuestMedalType.NoneOrUnknown;
            }
        }
    }
}

/* 692 */
/*
enum $8B10128932F884E0B385C45C2A832C21
{
  NOT_MEDAL_QUEST = 0x0,
  MEDAL_QUEST_JOB = 0x1,
  MEDAL_QUEST_NORMAL = 0x2,
  MEDAL_QUEST_CHALLENGE = 0x3,
  MEDAL_QUEST_EVENT = 0x4,
  MEDAL_QUEST_NO = 0x5,
};*/