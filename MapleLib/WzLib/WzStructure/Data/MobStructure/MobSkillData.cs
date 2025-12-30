namespace MapleLib.WzLib.WzStructure.Data.MobStructure
{
    /// <summary>
    /// Mob skill data structure
    /// </summary>
    public class MobSkillData
    {
        public int SkillAfter { get; set; }
        public int EffectAfter { get; set; }
        public int Skill { get; set; }
        public int Action { get; set; }
        public int Level { get; set; }
        public byte PreSkillIndex { get; set; }
        public byte PreSkillCount { get; set; }
        public bool OnlyFsm { get; set; }
        public int SkillForbid { get; set; }
    }
}
