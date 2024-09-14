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
using System.Linq;
using System.Text.RegularExpressions;

namespace MapleLib.WzLib.WzStructure.Data.CharacterStructure
{
    public static class CharacterJobExtensions
    {
        /// <summary>
        /// Gets the formatted job name from enum
        /// </summary>
        /// <param name="job"></param>
        /// <param name="bRemoveJobProgressionNumber">i.e Evan5 -> Evan</param>
        /// <returns></returns>
        public static string GetFormattedJobName(this CharacterJob job, bool bRemoveJobProgressionNumber = true)
        {
            string jobName = job.ToString();

            // Handle special cases
            jobName = jobName
                .Replace("FirePoison", " (Fire/Poison)")
                .Replace("IceLightning", " (Ice/Lightning)")
                .Replace("CrossBowman", "Crossbowman")
                .Replace("CannonShooter", "Cannon Shooter");

            if (jobName == "Pirate" || jobName.StartsWith("Cannon"))
                return $"Pirate ({jobName})";

            // Remove number at the end (for job progressions)
            if (bRemoveJobProgressionNumber)
                jobName = Regex.Replace(jobName, @"\d+$", "");

            // Add spaces between words
            jobName = string.Concat(jobName.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).Trim();

            // Handle "Beginner" special cases
            if (jobName.EndsWith(" Beginner"))
                jobName = jobName.Replace(" Beginner", "") + " (Beginner)";

            return jobName;
        }
    }

    /// <summary>
    /// https://github.com/TEAM-SPIRIT-Productions/MapleStoryJobIDs/blob/main/jobid_to_name.yaml
    /// </summary>
    public enum CharacterJob
    {
        None = -1,

        // Explorer Classes
        Beginner = 0,

        // Explorer Warrior
        Warrior = 100,
        Fighter = 110,
        Crusader = 111,
        Hero = 112,
        Page = 120,
        WhiteKnight = 121,
        Paladin = 122,
        Spearman = 130,
        DragonKnight = 131,
        DarkKnight = 132,

        // Explorer Mage
        Magician = 200,
        FirePoisonWizard = 210,
        FirePoisonMage = 211,
        FirePoisonArchmage = 212,
        IceLightningWizard = 220,
        IceLightningMage = 221,
        IceLightningArchmage = 222,
        Cleric = 230,
        Priest = 231,
        Bishop = 232,

        // Explorer Bowmen
        Archer = 300,
        Hunter = 310,
        Ranger = 311,
        Bowmaster = 312,
        CrossBowman = 320,
        Sniper = 321,
        Marksman = 322,

        // Pathfinder
        Pathfinder1 = 301,
        Pathfinder2 = 330,
        Pathfinder3 = 331,
        Pathfinder4 = 332,

        // Explorer Thieves
        Rogue = 400,
        Assassin = 410,
        Hermit = 411,
        NightLord = 412,
        Bandit = 420,
        ChiefBandit = 421,
        Shadower = 422,

        // Dual Blades
        BladeRecruit = 430,
        BladeAcolyte = 431,
        BladeSpecialist = 432,
        BladeLord = 433,
        BladeMaster = 434,

        // Explorer Pirates
        Pirate = 500,
        Brawler = 510,
        Marauder = 511,
        Buccaneer = 512,
        Gunslinger = 520,
        Outlaw = 521,
        Corsair = 522,

        // Cannoneer
        CannonShooter = 501,
        Cannoneer = 530,
        CannonTrooper = 531,
        CannonMaster = 532,

        // Jett
        Jett1 = 508,
        Jett2 = 570,
        Jett3 = 571,
        Jett4 = 572,

        // Knights of Cygnus
        Noblesse = 1000,
        DawnWarrior1 = 1100,
        DawnWarrior2 = 1110,
        DawnWarrior3 = 1111,
        DawnWarrior4 = 1112,
        BlazeWizard1 = 1200,
        BlazeWizard2 = 1210,
        BlazeWizard3 = 1211,
        BlazeWizard4 = 1212,
        WindArcher1 = 1300,
        WindArcher2 = 1310,
        WindArcher3 = 1311,
        WindArcher4 = 1312,
        NightWalker1 = 1400,
        NightWalker2 = 1410,
        NightWalker3 = 1411,
        NightWalker4 = 1412,
        ThunderBreaker1 = 1500,
        ThunderBreaker2 = 1510,
        ThunderBreaker3 = 1511,
        ThunderBreaker4 = 1512,

        // Heroes of Maple/Legends
        AranBeginner = 2000,
        Aran1 = 2100,
        Aran2 = 2110,
        Aran3 = 2111,
        Aran4 = 2112,
        EvanBeginner = 2001,
        Evan1 = 2200,
        Evan2 = 2210,
        Evan3 = 2211,
        Evan4 = 2212,
        Evan5 = 2213,
        Evan6 = 2214,
        Evan7 = 2215,
        Evan8 = 2216,
        Evan9 = 2217,
        Evan10 = 2218,
        MercedesBeginner = 2002,
        Mercedes1 = 2300,
        Mercedes2 = 2310,
        Mercedes3 = 2311,
        Mercedes4 = 2312,
        PhantomBeginner = 2003,
        Phantom1 = 2400,
        Phantom2 = 2410,
        Phantom3 = 2411,
        Phantom4 = 2412,
        ShadeBeginner = 2005,
        Shade1 = 2500,
        Shade2 = 2510,
        Shade3 = 2511,
        Shade4 = 2512,
        LuminousBeginner = 2004,
        Luminous1 = 2700,
        Luminous2 = 2710,
        Luminous3 = 2711,
        Luminous4 = 2712,

        // Resistance
        Citizen = 3000,
        DemonBeginner = 3001,
        DemonSlayer1 = 3100,
        DemonSlayer2 = 3110,
        DemonSlayer3 = 3111,
        DemonSlayer4 = 3112,
        DemonAvenger1 = 3101,
        DemonAvenger2 = 3120,
        DemonAvenger3 = 3121,
        DemonAvenger4 = 3122,
        BattleMage1 = 3200,
        BattleMage2 = 3210,
        BattleMage3 = 3211,
        BattleMage4 = 3212,
        WildHunter1 = 3300,
        WildHunter2 = 3310,
        WildHunter3 = 3311,
        WildHunter4 = 3312,
        Mechanic1 = 3500,
        Mechanic2 = 3510,
        Mechanic3 = 3511,
        Mechanic4 = 3512,
        XenonBeginner = 3002,
        Xenon1 = 3600,
        Xenon2 = 3610,
        Xenon3 = 3611,
        Xenon4 = 3612,
        Blaster1 = 3700,
        Blaster2 = 3710,
        Blaster3 = 3711,
        Blaster4 = 3712,

        // Sengoku
        HayatoBeginner = 4001,
        Hayato1 = 4100,
        Hayato2 = 4110,
        Hayato3 = 4111,
        Hayato4 = 4112,
        KannaBeginner = 4002,
        Kanna1 = 4200,
        Kanna2 = 4210,
        Kanna3 = 4211,
        Kanna4 = 4212,

        // Special KoC
        MihileBeginner = 5000,
        Mihile1 = 5100,
        Mihile2 = 5110,
        Mihile3 = 5111,
        Mihile4 = 5112,

        // Nova
        KaiserBeginner = 6000,
        Kaiser1 = 6100,
        Kaiser2 = 6110,
        Kaiser3 = 6111,
        Kaiser4 = 6112,
        AngelicBusterBeginner = 6001,
        AngelicBuster1 = 6500,
        AngelicBuster2 = 6510,
        AngelicBuster3 = 6511,
        AngelicBuster4 = 6512,
        CadenaBeginner = 6002,
        Cadena1 = 6400,
        Cadena2 = 6410,
        Cadena3 = 6411,
        Cadena4 = 6412,
        KainBeginner = 6003,
        Kain1 = 6300,
        Kain2 = 6310,
        Kain3 = 6311,
        Kain4 = 6312,

        // Child of God
        ZeroBeginner = 10000,
        Zero1 = 10100,
        Zero2 = 10110,
        Zero3 = 10111,
        Zero4 = 10112,

        // Beast Tamer
        BeastTamerBeginner = 11000,
        BeastTamer1 = 11200,
        BeastTamer2 = 11210,
        BeastTamer3 = 11211,
        BeastTamer4 = 11212,

        // Kinesis
        KinesisBeginner = 14000,
        Kinesis1 = 14200,
        Kinesis2 = 14210,
        Kinesis3 = 14211,
        Kinesis4 = 14212,

        // Flora
        IlliumBeginner = 15000,
        Illium1 = 15200,
        Illium2 = 15210,
        Illium3 = 15211,
        Illium4 = 15212,
        ArkBeginner = 15001,
        Ark1 = 15500,
        Ark2 = 15510,
        Ark3 = 15511,
        Ark4 = 15512,
        AdeleBeginner = 15002,
        Adele1 = 15100,
        Adele2 = 15110,
        Adele3 = 15111,
        Adele4 = 15112,

        // Anima
        HoyoungBeginner = 16000,
        Hoyoung1 = 16400,
        Hoyoung2 = 16410,
        Hoyoung3 = 16411,
        Hoyoung4 = 16412,
        LaraBeginner = 16001,
        Lara1 = 16200,
        Lara2 = 16210,
        Lara3 = 16211,
        Lara4 = 16212,

        // Special jobs
        Manager = 800,
        GM = 900,
        SuperGM = 910,
        RidingSkills = 8000,
        AdditionalSkills = 9000,
        VSkills = 40000,

        // Event Classes
        PinkBeanBeginner = 13000,
        PinkBean1 = 13100,
        YetiBeginner = 13001,
        Yeti1 = 13500
    }
}
