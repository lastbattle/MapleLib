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
    /// <summary>
    /// The quest state values
    /// </summary>
    public enum QuestStateType
    {
        Not_Started = 0,
        Started = 1,
        Completed = 2,
    }

    public static class QuestStateTypeExtensions
    {
        /// <summary>
        /// Human readable string
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static string ToReadableString(this QuestStateType state)
        {
            return state.ToString().Replace("_", " ");
        }

        /// <summary>
        /// Converts from the string back to enum
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static QuestStateType ToEnum(this string name)
        {
            // Try to parse the string to enum
            if (Enum.TryParse<QuestStateType>(name.Replace(" ", "_"), out QuestStateType result))
            {
                return (QuestStateType)result;
            }
            return QuestStateType.Not_Started;
        }
    }
}
