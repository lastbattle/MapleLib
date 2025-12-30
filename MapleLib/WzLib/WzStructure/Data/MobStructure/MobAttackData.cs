namespace MapleLib.WzLib.WzStructure.Data.MobStructure
{
    /// <summary>
    /// Mob attack data structure
    /// </summary>
    public class MobAttackData
    {
        public byte AttackNum { get; set; }
        public byte Action { get; set; }
        public byte Magic { get; set; }
        public byte DeadlyAttack { get; set; }
        public byte Knockback { get; set; }
        public int BulletSpeed { get; set; }
        public int MpBurn { get; set; }
        public int Disease { get; set; }
        public int Level { get; set; }
        public int ConMP { get; set; }
    }
}
