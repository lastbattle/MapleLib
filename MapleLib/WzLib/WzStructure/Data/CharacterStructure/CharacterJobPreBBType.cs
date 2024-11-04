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
using System.Linq;
using System.Text.RegularExpressions;

namespace MapleLib.WzLib.WzStructure.Data.CharacterStructure
{
    /// <summary>
    /// The bitfield values for job categories
    /// for Quest Act.img 'job' WZ values before big-bang update.
    /// </summary>
    public enum CharacterJobPreBBType
    {
        None = 0x0,

        // Basic Classes (0x1 - 0x20)
        Beginner = 0x1,      // 0
        ExplorerWarrior = 0x2,      // 100
        ExplorerMagician = 0x4,     // 200
        ExplorerArcher = 0x8,      // 300
        ExplorerThief = 0x10,     // 400
        ExplorerPirate = 0x20,     // 500

        // Cygnus Knights (0x400 - 0x8000)
        Noblesse = 0x400,    // 1000
        DawnWarrior = 0x800,    // 1100
        BlazeWizard = 0x1000,   // 1200
        WindArcher = 0x2000,   // 1300
        NightWalker = 0x4000,   // 1400
        ThunderBreaker = 0x8000,   // 1500

        // Heroes/Legends (0x20000 - 0x400000)
        Evan = 0x20000,   // 2001, 2200
        Aran = 0x100000,  // 2000, 2001
        AranWarrior = 0x200000,  // 2100
        EvanMagician = 0x400000,  // 2001, 2200

        // Resistance (0x40000000)
        Resistance = 0x40000000,  // 3000, 3200, 3300, 3500

        // Combined Group Masks
        AllExplorers = ExplorerWarrior | ExplorerMagician | ExplorerArcher | ExplorerThief | ExplorerPirate,
        AllCygnus = DawnWarrior | BlazeWizard | WindArcher | NightWalker | ThunderBreaker,
        AllHeroes = Aran | AranWarrior | Evan | EvanMagician,

        // Class Type Masks (for similar class types across factions)
        AllWarriors = ExplorerWarrior | DawnWarrior | AranWarrior,
        AllMagicians = ExplorerMagician | BlazeWizard | Evan | EvanMagician,
        AllArchers = ExplorerArcher | WindArcher,
        AllThieves = ExplorerThief | NightWalker,
        AllPirates = ExplorerPirate | ThunderBreaker
    }

    public static class CharacterJobPreBBTypeExt
    {
        public static IEnumerable<CharacterJob> DecodeJobCodes(this CharacterJobPreBBType encoded)
        {
            var jobs = new List<CharacterJob>();

            // Basic Classes
            if (encoded.HasFlag(CharacterJobPreBBType.Beginner))
                jobs.Add(CharacterJob.Beginner);
            if (encoded.HasFlag(CharacterJobPreBBType.ExplorerWarrior))
                jobs.Add(CharacterJob.Warrior);
            if (encoded.HasFlag(CharacterJobPreBBType.ExplorerMagician))
                jobs.Add(CharacterJob.Magician);
            if (encoded.HasFlag(CharacterJobPreBBType.ExplorerArcher))
                jobs.Add(CharacterJob.Archer);
            if (encoded.HasFlag(CharacterJobPreBBType.ExplorerThief))
                jobs.Add(CharacterJob.Rogue);
            if (encoded.HasFlag(CharacterJobPreBBType.ExplorerPirate))
                jobs.Add(CharacterJob.Pirate);

            // Cygnus Knights
            if (encoded.HasFlag(CharacterJobPreBBType.Noblesse))
                jobs.Add(CharacterJob.Noblesse);
            if (encoded.HasFlag(CharacterJobPreBBType.DawnWarrior))
                jobs.Add(CharacterJob.DawnWarrior1);
            if (encoded.HasFlag(CharacterJobPreBBType.BlazeWizard))
                jobs.Add(CharacterJob.BlazeWizard1);
            if (encoded.HasFlag(CharacterJobPreBBType.WindArcher))
                jobs.Add(CharacterJob.WindArcher1);
            if (encoded.HasFlag(CharacterJobPreBBType.NightWalker))
                jobs.Add(CharacterJob.NightWalker1);
            if (encoded.HasFlag(CharacterJobPreBBType.ThunderBreaker))
                jobs.Add(CharacterJob.ThunderBreaker1);

            // Heroes/Legends
            if (encoded.HasFlag(CharacterJobPreBBType.Evan))
            {
                jobs.Add(CharacterJob.EvanBeginner);
                jobs.Add(CharacterJob.Evan1);
            }
            if (encoded.HasFlag(CharacterJobPreBBType.Aran))
            {
                jobs.Add(CharacterJob.AranBeginner);
                jobs.Add(CharacterJob.EvanBeginner);
            }
            if (encoded.HasFlag(CharacterJobPreBBType.AranWarrior))
                jobs.Add(CharacterJob.Aran1);

            if (encoded.HasFlag(CharacterJobPreBBType.EvanMagician))
            {
                jobs.Add(CharacterJob.EvanBeginner);
                jobs.Add(CharacterJob.Evan1);
            }

            // Resistance
            if (encoded.HasFlag(CharacterJobPreBBType.Resistance))
            {
                jobs.Add(CharacterJob.Citizen);
                jobs.Add(CharacterJob.BattleMage1);
                jobs.Add(CharacterJob.WildHunter1);
                jobs.Add(CharacterJob.Mechanic1);
            }

            return jobs.Distinct().OrderBy(x => (int)x);
        }

        public static CharacterJobPreBBType EncodeJobs(IEnumerable<int> jobs)
        {
            CharacterJobPreBBType encoded = CharacterJobPreBBType.None;

            foreach (var job in jobs)
            {
                switch (job)
                {
                    case 0: encoded |= CharacterJobPreBBType.Beginner; break;
                    case 100: encoded |= CharacterJobPreBBType.ExplorerWarrior; break;
                    case 200: encoded |= CharacterJobPreBBType.ExplorerMagician; break;
                    case 300: encoded |= CharacterJobPreBBType.ExplorerArcher; break;
                    case 400: encoded |= CharacterJobPreBBType.ExplorerThief; break;
                    case 500: encoded |= CharacterJobPreBBType.ExplorerPirate; break;
                    case 1000: encoded |= CharacterJobPreBBType.Noblesse; break;
                    case 1100: encoded |= CharacterJobPreBBType.DawnWarrior; break;
                    case 1200: encoded |= CharacterJobPreBBType.BlazeWizard; break;
                    case 1300: encoded |= CharacterJobPreBBType.WindArcher; break;
                    case 1400: encoded |= CharacterJobPreBBType.NightWalker; break;
                    case 1500: encoded |= CharacterJobPreBBType.ThunderBreaker; break;
                    case 2000: 
                        encoded |= CharacterJobPreBBType.Aran; 
                        break;
                    case 2100: 
                        encoded |= CharacterJobPreBBType.AranWarrior; 
                        break;
                    case 2001:
                        encoded |= CharacterJobPreBBType.Evan;
                        break;
                    case 2200:
                        encoded |= CharacterJobPreBBType.EvanMagician;
                        break;
                    case 3000:
                    case 3200:
                    case 3300:
                    case 3500:
                        encoded |= CharacterJobPreBBType.Resistance;
                        break;
                }
            }

            return encoded;
        }

        /// <summary>
        /// Gets the formatted job name from enum
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public static string GetFormattedJobName(this CharacterJobPreBBType job)
        {
            string jobName = job.ToString();

            // Add spaces between words
            jobName = string.Concat(jobName.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).Trim();

            return jobName;
        }

        // Helper methods for common job checks
        public static bool IsExplorer(this CharacterJobPreBBType codes) => (codes & CharacterJobPreBBType.AllExplorers) != 0;
        public static bool IsCygnus(this CharacterJobPreBBType codes) => (codes & CharacterJobPreBBType.AllCygnus) != 0;
        public static bool IsHero(this CharacterJobPreBBType codes) => (codes & CharacterJobPreBBType.AllHeroes) != 0;

        public static bool IsWarrior(this CharacterJobPreBBType codes) => (codes & CharacterJobPreBBType.AllWarriors) != 0;
        public static bool IsMagician(this CharacterJobPreBBType codes) => (codes & CharacterJobPreBBType.AllMagicians) != 0;
        public static bool IsArcher(this CharacterJobPreBBType codes) => (codes & CharacterJobPreBBType.AllArchers) != 0;
        public static bool IsThief(this CharacterJobPreBBType codes) => (codes & CharacterJobPreBBType.AllThieves) != 0;
        public static bool IsPirate(this CharacterJobPreBBType codes) => (codes & CharacterJobPreBBType.AllPirates) != 0;

        // Helper method to convert from integer to JobCodes enum
        public static CharacterJobPreBBType ToJobCodes(this int encoded) => (CharacterJobPreBBType)encoded;
    }
}
