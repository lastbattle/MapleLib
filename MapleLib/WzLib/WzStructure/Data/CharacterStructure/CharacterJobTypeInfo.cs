using MapleLib.ClientLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.WzStructure.Data.CharacterStructure
{
    public class CharacterJobTypeInfo
    {
        public CharacterJobType Type { get; }
        public bool IsCreationEnabled { get; }
        public int JobId { get; }
        public int Map { get; }
        public int StartingETCBookItemId { get; }
        public bool HairColor { get; }
        public bool SkinColor { get; }
        public bool FaceMark { get; }
        public bool Hat { get; }
        public bool Bottom { get; }
        public bool Cape { get; }
        public bool Ears { get; }
        public bool Tail { get; }

        /// <summary>
        /// Character creation data
        /// </summary>
        /// <param name="type"></param>
        /// <param name="isCreationEnabled"></param>
        /// <param name="jobId"></param>
        /// <param name="map"></param>
        /// <param name="startingETCBookItemId"></param>
        /// <param name="hairColor"></param>
        /// <param name="skinColor"></param>
        /// <param name="faceMark"></param>
        /// <param name="hat"></param>
        /// <param name="bottom"></param>
        /// <param name="cape"></param>
        /// <param name="ears"></param>
        /// <param name="tail"></param>
        public CharacterJobTypeInfo(CharacterJobType type, bool isCreationEnabled, int jobId, int map, int startingETCBookItemId,
            bool hairColor, bool skinColor, bool faceMark, bool hat, bool bottom, bool cape, bool ears, bool tail)
        {
            Type = type;
            IsCreationEnabled = isCreationEnabled;
            JobId = jobId;
            Map = map;
            StartingETCBookItemId = startingETCBookItemId;
            HairColor = hairColor;
            SkinColor = skinColor;
            FaceMark = faceMark;
            Hat = hat;
            Bottom = bottom;
            Cape = cape;
            Ears = ears;
            Tail = tail;
        }

        public static CharacterJobTypeInfo[] JobTypes = new CharacterJobTypeInfo[]
        {
            new CharacterJobTypeInfo(CharacterJobType.UltimateAdventurer, false, 0, 100000000, ItemIds.BEGINNERS_GUIDE, true, true, false, false, true, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Resistance, true, 3000, 931000000, ItemIds.CITIZENS_GUIDE, false, true, false, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Adventurer, true, 0, 4000000, ItemIds.BEGINNERS_GUIDE, false, true, false, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Cygnus, true, 1000, 130030000, ItemIds.NOBLESS_GUIDE, false, true, false, false, false, true, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Aran, true, 2000, 914000000, ItemIds.LEGENDS_GUIDE, true, true, false, false, true, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Evan, true, 2001, 900010000, ItemIds.LEGENDS_GUIDE, true, true, false, false, true, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Mercedes, false, 2002, 910150000, 0, false, false, false, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Demon, false, 3001, 931050310, 0, false, false, true, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Phantom, false, 2003, 915000000, 0, false, true, false, false, false, true, false, false),
            new CharacterJobTypeInfo(CharacterJobType.DualBlade, false, 0, 103050900, ItemIds.BEGINNERS_GUIDE, false, true, false, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Mihile, false, 5000, 913070000, 0, true, true, false, false, true, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Luminous, false, 2004, 931030000, 0, false, true, false, false, false, true, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Kaiser, false, 6000, 940001000, 0, false, true, false, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.AngelicBuster, false, 6001, 940011000, 0, false, true, false, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Cannoneer, true, 0, 3000000, ItemIds.BEGINNERS_GUIDE, true, true, false, false, true, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Xenon, false, 3002, 931050920, 0, true, true, true, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Zero, false, 10112, 321000000, 0, false, true, false, false, false, true, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Shade, false, 2005, 927030050, 0, false, true, false, false, true, true, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Zen, false, 0, 552000050, 0, false, false, false, false, false, true, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Jett, false, 0, 552000050, 0, false, false, false, false, false, true, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Hayato, false, 4001, 807000000, 0, true, true, false, true, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Kanna, false, 4002, 807040000, 0, true, true, false, true, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.BeastTamer, false, 11212, 866000000, 0, false, true, true, false, false, false, true, true),
            new CharacterJobTypeInfo(CharacterJobType.PinkBean, false, 13100, 866000000, 0, false, false, false, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.Kinesis, false, 14000, 331001110, 0, false, true, false, false, false, false, false, false),
            new CharacterJobTypeInfo(CharacterJobType.NULL, false, 0, 999999999, 0, false, false, false, false, false, false, false, false)
        };

        /// <summary>
        /// Gets the job info by jobId & maplestory localisation
        /// </summary>
        /// <param name="job"></param>
        /// <param name="msLocalisation"></param>
        /// <returns></returns>
        public static CharacterJobTypeInfo GetByJobId(int job, MapleStoryLocalisation msLocalisation)
        {
            if (job == JobTypes[(int)CharacterJobType.Adventurer].JobId)
            {
                return JobTypes[(int)CharacterJobType.Adventurer];
            }
            if (job == 508)
            {
                if (msLocalisation == MapleStoryLocalisation.MapleStoryGlobal)
                {
                    return JobTypes[(int)CharacterJobType.Jett];
                }
                else if (msLocalisation == MapleStoryLocalisation.MapleStorySEA)
                {
                    return JobTypes[(int)CharacterJobType.Zen];
                }
                else
                {
                    return JobTypes[(int)CharacterJobType.NULL];
                }
            }
            foreach (var jobType in JobTypes)
            {
                if ((int)jobType.Type == job)
                {
                    return jobType;
                }
            }
            return JobTypes[(int)CharacterJobType.NULL];
        }

        public static CharacterJobTypeInfo GetByType(int g)
        {
            if (g == (int)CharacterJobType.Cannoneer)
            {
                return JobTypes[(int)CharacterJobType.Adventurer];
            }
            foreach (var jobType in JobTypes)
            {
                if ((int)jobType.Type == g)
                {
                    return jobType;
                }
            }
            return null;
        }
    }
}
