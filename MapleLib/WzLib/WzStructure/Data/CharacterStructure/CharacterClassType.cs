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

using System.Collections.Generic;
using System;
using System.Linq;

namespace MapleLib.WzLib.WzStructure.Data.CharacterStructure
{
    /// <summary>
    /// The job type 
    /// </summary>
    public enum CharacterClassType
    {
        UltimateAdventurer = -1,
        Resistance = 0,
        Adventurer = 1,
        Cygnus = 2,
        Aran = 3,
        Evan = 4,
        Mercedes = 5,
        Demon = 6,
        Phantom = 7,
        DualBlade = 8,
        Mihile = 9,
        Luminous = 10,
        Kaiser = 11,
        AngelicBuster = 12,
        Cannoneer = 13,
        Xenon = 14,
        Zero = 15,
        Shade = 16,
        ZenOrJett = 17,
        Hayato = 18,
        Kanna = 19,
        BeastTamer = 20,
        PinkBean = 21,
        Kinesis = 22,
        NULL = 99999
    }

    public static class MapleJobTypeExtensions
    {
        /// <summary>
        /// Gets all CharacterJobType enum values except NULL.
        /// </summary>
        /// <returns>An IEnumerable of CharacterJobType containing all enum values except NULL.</returns>
        public static IEnumerable<CharacterClassType> GetAllJobTypes()
        {
            return Enum.GetValues(typeof(CharacterClassType))
                .Cast<CharacterClassType>()
                .Where(j => j != CharacterClassType.NULL);
        }

        /// <summary>
        /// Checks if the given job bitfield matches the specified job type.
        /// </summary>
        /// <param name="jobBitfield">The job bitfield to check against.  <int name="job" value="32800"/> </param>
        /// <returns>True if the jobBitfield matches the specified job type, false otherwise.</returns>
        public static List<CharacterClassType> GetMatchingJobs(int jobBitfield)
        {
            List<CharacterClassType> ret = new List<CharacterClassType>();

            IEnumerable<CharacterClassType> jobInfo = GetAllJobTypes();
            foreach (CharacterClassType job in jobInfo)
            {
                bool bMatch = IsJobMatching(job, jobBitfield);
                if (bMatch)
                {
                    ret.Add(job);
                }
            }
            return ret;
        }

        /// <summary>
        /// Checks if the given job bitfield matches the specified job type.
        /// </summary>
        /// <param name="job">The job selected</param>
        /// <param name="jobBitfield">The job bitfield to check against.  <int name="job" value="32800"/> </param>
        /// <returns>True if the jobBitfield matches the specified job type, false otherwise.</returns>
        public static bool IsJobMatching(CharacterClassType job, int jobBitfield)
        {
            bool bMatch = (jobBitfield & (1 << (int)job)) != 0;
            return bMatch;
        }
    }
}